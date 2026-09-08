using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ImprovedGarrisons.AI.AIManagers;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;

namespace ImprovedGarrisons.CoopRuntimeContract;

public static partial class ContractRunner
{
    private static PartyBase? surplusCreatedParty;
    private static TroopRoster? surplusFailingRoster;
    private static int surplusDestinationWrites;

    private static void RunTemplateSurplusTests()
    {
        Type policy = typeof(Main).Assembly.GetType("ImprovedGarrisons.AI.AIManagers.TemplateSurplusTroops")
            ?? throw new MissingMemberException("The template surplus selection policy is missing.");
        MethodInfo select = policy.GetMethod("GetSurplusTroops", BindingFlags.Public | BindingFlags.Static)!;
        MethodInfo create = policy.GetMethod("TryCreateGuard", BindingFlags.Public | BindingFlags.Static)!;

        CharacterObject archers = SurplusCharacter("archers");
        CharacterObject infantry = SurplusCharacter("infantry");
        CharacterObject recruits = SurplusCharacter("recruits", infantry);
        CharacterObject outsiders = SurplusCharacter("outsiders");
        Dictionary<CharacterObject, int> targets = new() { [archers] = 50, [infantry] = 20 };
        TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
        roster.AddToCounts(archers, 70, woundedCount: 5, xpChange: 700);
        roster.AddToCounts(infantry, 10);
        roster.AddToCounts(recruits, 15);
        roster.AddToCounts(outsiders, 9);
        Dictionary<CharacterObject, int> excess = SurplusSelect(select, roster, targets);
        Assert(excess.Count == 3 && excess[archers] == 20 && excess[recruits] == 5 && excess[outsiders] == 9,
            "Surplus selection must include excess targets and absent types while reserving recruits for deficits.");
        Assert(roster.TotalManCount == 104 && targets[archers] == 50 && targets[infantry] == 20,
            "Computing surplus changed the input garrison or template.");

        CharacterObject common = SurplusCharacter("common", infantry, archers);
        CharacterObject onlyInfantry = SurplusCharacter("only_infantry", infantry);
        TroopRoster branching = TroopRoster.CreateDummyTroopRoster();
        branching.AddToCounts(common, 10);
        branching.AddToCounts(onlyInfantry, 10);
        Dictionary<CharacterObject, int> branchTargets = new() { [infantry] = 10, [archers] = 10 };
        Assert(SurplusSelect(select, branching, branchTargets).Count == 0,
            "Shared recruits must be reassigned to the other branch so specialized recruits remain needed.");
        TroopRoster shared = TroopRoster.CreateDummyTroopRoster();
        shared.AddToCounts(common, 25);
        Assert(SurplusSelect(select, shared, branchTargets)[common] == 5,
            "A recruit may satisfy only one target slot across branching upgrade paths.");

        Dictionary<CharacterObject, int> unlimited = new() { [infantry] = 0 };
        Assert(!SurplusSelect(select, roster, unlimited).ContainsKey(recruits)
            && !SurplusSelect(select, roster, unlimited).ContainsKey(infantry),
            "Unlimited targets must retain both their current troops and upgradeable recruits.");
        TroopRoster wounded = TroopRoster.CreateDummyTroopRoster();
        wounded.AddToCounts(outsiders, 12, woundedCount: 10);
        Assert(SurplusSelect(select, wounded, targets)[outsiders] == 2,
            "Wounded troops must remain home even when their type is outside the template.");
        Assert(SurplusSelect(select, roster, new Dictionary<CharacterObject, int>()).Count == 0
            && SurplusSelect(select, roster, null).Count == 0,
            "Missing or empty templates must not classify the entire garrison for dispatch.");

        GarrisonSettings settings = SurplusSettings(targets, true, 30);
        TroopRoster guards = TroopRoster.CreateDummyTroopRoster();
        bool created = (bool)create.Invoke(null, new object[] { roster, settings, true, (Func<TroopRoster>)(() => guards) })!;
        Assert(created && guards.TotalManCount == 30 && roster.TotalManCount == 74,
            "A full surplus guard batch must be transferred without creating or losing troops.");
        Assert(roster.GetTroopCount(infantry) == 10 && roster.GetTroopCount(recruits) >= 10
            && roster.GetTroopCount(archers) >= 50 && guards.GetTroopCount(infantry) == 0
            && guards.GetTroopCount(recruits) <= 5 && guards.GetTroopCount(archers) <= 20
            && guards.GetTroopCount(outsiders) <= 9,
            "Guard creation moved troops reserved for template targets instead of only selected surplus.");
        Assert(roster.TotalWounded == 5 && guards.TotalWounded == 0,
            "Guard creation moved or healed wounded defenders.");
        Assert(roster.GetElementXp(archers) + guards.GetElementXp(archers) == 700,
            "Guard creation lost or duplicated troop experience.");

        int factoryCalls = 0;
        Func<TroopRoster> unavailableFactory = () => { factoryCalls++; return null!; };
        int remaining = roster.TotalManCount;
        Assert(!(bool)create.Invoke(null, new object[] { roster, settings, true, unavailableFactory })!
            && roster.TotalManCount == remaining && factoryCalls == 0,
            "Too few surplus troops must wait without creating an empty guard.");
        settings.GuardsAutoSpawnSize = 1;
        Assert(!(bool)create.Invoke(null, new object[] { roster, settings, false, unavailableFactory })!
            && factoryCalls == 0 && roster.TotalManCount == remaining,
            "Unavailable creation conditions must prevent all roster and factory side effects.");
        Assert(!(bool)create.Invoke(null, new object[] { roster, settings, true, unavailableFactory })!
            && factoryCalls == 1 && roster.TotalManCount == remaining,
            "Failed native guard creation must leave garrison troops untouched.");
        Assert(!(bool)create.Invoke(null, new object[] { roster, settings, true, (Func<TroopRoster>)(() => guards) })!
            && guards.TotalManCount == 30 && roster.TotalManCount == remaining,
            "An existing populated guard returned by the native factory must remain untouched.");
        typeof(GarrisonSettings).GetProperty("GuardsAutoSpawnFromExcess")!.SetValue(settings, false);
        Assert(!(bool)create.Invoke(null, new object[] { roster, settings, true, unavailableFactory })!
            && factoryCalls == 1 && roster.TotalManCount == remaining,
            "Disabling surplus guards must leave both the roster and existing guards alone.");
        settings = SurplusSettings(new Dictionary<CharacterObject, int>(), true, 1);
        Assert(!(bool)create.Invoke(null, new object[] { roster, settings, true, unavailableFactory })!
            && factoryCalls == 1 && roster.TotalManCount == remaining,
            "An empty template must not create guards from the entire garrison.");
        settings.Template = null!;
        Assert(!(bool)create.Invoke(null, new object[] { roster, settings, true, unavailableFactory })!
            && factoryCalls == 1 && roster.TotalManCount == remaining,
            "A missing template must not cause automatic transfers.");
        TestSurplusRemovalPrecedence();
        TestSurplusManagerSuccess();
        TestSurplusTransferRollback(create);
        Console.WriteLine("PASS surplus allocation, branching reserves, unlimited targets, wounded retention, and actual-roster guard transfers");
    }

    private static void TestSurplusManagerSuccess()
    {
        using SettingsTownFixture fixture = new();
        var previousParties = Main.PartyManagement;
        Harmony harmony = new("improvedgarrisons.tests.surplus.manager");
        MethodInfo factory = typeof(PartyManager).GetMethod("InitializeNewParty")!;
        bool patched = false;
        try
        {
            Main.PartyManagement = new PartyManager();
            CharacterObject target = SurplusCharacter("manager_target");
            CharacterObject recruit = SurplusCharacter("manager_recruit", target);
            CharacterObject outsider = SurplusCharacter("manager_outsider");
            TroopRoster stationed = TroopRoster.CreateDummyTroopRoster();
            stationed.AddToCounts(target, 7);
            stationed.AddToCounts(recruit, 6);
            stationed.AddToCounts(outsider, 5, woundedCount: 1, xpChange: 50);
            MobileParty garrison = SurplusParty(stationed);
            GarrisonPartyComponent component = (GarrisonPartyComponent)RuntimeHelpers.GetUninitializedObject(typeof(GarrisonPartyComponent));
            SettingsSetField(component, "<MobileParty>k__BackingField", garrison);
            fixture.Town.GarrisonPartyComponent = component;
            GarrisonSettings settings = SurplusSettings(new Dictionary<CharacterObject, int> { [target] = 10 }, true, 7);
            fixture.Settings.Template = settings.Template;
            fixture.Settings.GuardsAutoSpawnSize = 7;
            typeof(GarrisonSettings).GetProperty("GuardsAutoSpawnFromExcess")!.SetValue(fixture.Settings, true);
            TroopRoster stationedBefore = TroopRoster.CreateDummyTroopRoster();
            stationedBefore.Add(stationed);
            TroopRoster guardRoster = TroopRoster.CreateDummyTroopRoster();
            surplusCreatedParty = SurplusParty(guardRoster).Party;
            SettingsSetField(surplusCreatedParty.MobileParty, "_actualClan", fixture.Clan);
            harmony.Patch(factory, prefix: new HarmonyMethod(typeof(ContractRunner)
                .GetMethod(nameof(SurplusCreatePartyPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
            patched = true;

            PartyBase actual = Main.PartyManagement.mobileGarrisonManagement.CreateMobileGarrisonFromExcess(fixture.Settlement);
            Assert(actual == surplusCreatedParty, "The actual manager did not return the successfully created guard.");
            Assert(stationed.GetTroopCount(target) == 7 && stationed.GetTroopCount(recruit) == 3
                && stationed.GetTroopCount(outsider) == 1 && stationed.TotalWounded == 1,
                "The actual manager failed to preserve the exact expected template reserve and wounded roster.");
            Assert(guardRoster.GetTroopCount(target) == 0 && guardRoster.GetTroopCount(recruit) == 3
                && guardRoster.GetTroopCount(outsider) == 4 && guardRoster.TotalWounded == 0,
                "The actual manager did not transfer exactly the selected surplus troops.");
            Assert(stationed.TotalManCount + guardRoster.TotalManCount == stationedBefore.TotalManCount
                && stationed.GetElementXp(outsider) + guardRoster.GetElementXp(outsider) == 50,
                "The actual manager lost or duplicated soldiers or experience.");
            MobileGarrison registered = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(fixture.Settlement);
            Assert(registered != null && registered.getMobileParty() == actual.MobileParty && registered.InitialSize == 7
                && fixture.Settings.InitialTroopRoster.Sum(x => x.Item2) == 7,
                "The successful manager path did not register and initialize the new guard roster.");
            Console.WriteLine("PASS actual surplus manager success, registration, selected rosters, and initial guard state");
        }
        finally
        {
            if (patched)
            {
                harmony.Unpatch(factory, HarmonyPatchType.Prefix, harmony.Id);
            }
            surplusCreatedParty = null;
            Main.PartyManagement = previousParties;
        }
    }

    private static bool SurplusCreatePartyPrefix(ref PartyBase __result)
    {
        // Replace only unavailable engine creation; the new manager and registration paths run normally.
        __result = surplusCreatedParty!;
        return false;
    }

    private static void TestSurplusTransferRollback(MethodInfo create)
    {
        CharacterObject desired = SurplusCharacter("rollback_target");
        CharacterObject first = SurplusCharacter("rollback_a");
        CharacterObject second = SurplusCharacter("rollback_b");
        TroopRoster source = TroopRoster.CreateDummyTroopRoster();
        source.AddToCounts(desired, 10);
        source.AddToCounts(first, 5, woundedCount: 1, xpChange: 50);
        source.AddToCounts(second, 6, woundedCount: 2, xpChange: 60);
        TroopRoster destination = TroopRoster.CreateDummyTroopRoster();
        string[] beforeSource = SurplusRosterSnapshot(source);
        string[] beforeDestination = SurplusRosterSnapshot(destination);
        GarrisonSettings settings = SurplusSettings(new Dictionary<CharacterObject, int> { [desired] = 10 }, true, 6);
        Harmony harmony = new("improvedgarrisons.tests.surplus.rollback");
        MethodInfo add = typeof(TroopRoster).GetMethod("AddToCounts")!;
        surplusFailingRoster = destination;
        surplusDestinationWrites = 0;
        bool threw = false;
        bool patched = false;
        try
        {
            harmony.Patch(add, prefix: new HarmonyMethod(typeof(ContractRunner)
                .GetMethod(nameof(SurplusFailSecondTransferPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
            patched = true;
            try
            {
                create.Invoke(null, new object[] { source, settings, true, (Func<TroopRoster>)(() => destination) });
            }
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException failure
                && failure.Message == "Injected second troop transfer failure")
            {
                threw = true;
            }
            Assert(threw && surplusDestinationWrites == 2, "The rollback test must fail after one completed troop transfer.");
            Assert(beforeSource.SequenceEqual(SurplusRosterSnapshot(source))
                && beforeDestination.SequenceEqual(SurplusRosterSnapshot(destination)),
                "A mid-transfer exception failed to restore exact troop counts, wounded counts, and experience in both rosters.");
            Console.WriteLine("PASS injected second-transfer failure restores both full roster snapshots");
        }
        finally
        {
            if (patched)
            {
                harmony.Unpatch(add, HarmonyPatchType.Prefix, harmony.Id);
            }
            surplusFailingRoster = null;
            surplusDestinationWrites = 0;
        }
    }

    private static void SurplusFailSecondTransferPrefix(TroopRoster __instance, int count)
    {
        if (__instance == surplusFailingRoster && count > 0 && ++surplusDestinationWrites == 2)
        {
            throw new InvalidOperationException("Injected second troop transfer failure");
        }
    }

    private static string[] SurplusRosterSnapshot(TroopRoster roster)
    {
        return roster.GetTroopRoster().Select(x => $"{x.Character.StringId}:{x.Number}:{x.WoundedNumber}:{x.Xp}")
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static void TestSurplusRemovalPrecedence()
    {
        using SettingsTownFixture fixture = new();
        PropertyInfo campaignProperty = typeof(Campaign).GetProperty("Current", BindingFlags.Public | BindingFlags.Static)!;
        object? previousCampaign = campaignProperty.GetValue(null);
        var previousUpgrade = Main.UpgradeLogic;
        var previousParties = Main.PartyManagement;
        try
        {
            Campaign campaign = (Campaign)RuntimeHelpers.GetUninitializedObject(typeof(Campaign));
            GameModels models = (GameModels)RuntimeHelpers.GetUninitializedObject(typeof(GameModels));
            typeof(GameModels).GetProperty("CharacterStatsModel")!.SetValue(models, new DefaultCharacterStatsModel());
            SettingsSetField(campaign, "_gameModels", models);
            campaignProperty.SetValue(null, campaign);
            Main.UpgradeLogic = new ImprovedGarrisons.Upgrade.GarrisonUpgradeLogic();
            Main.PartyManagement = new PartyManager();

            CharacterObject desired = SurplusCharacter("desired");
            CharacterObject outsider = SurplusCharacter("outsider");
            TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
            roster.AddToCounts(outsider, 5);
            MobileParty garrison = SurplusParty(roster);
            GarrisonPartyComponent component = (GarrisonPartyComponent)RuntimeHelpers.GetUninitializedObject(typeof(GarrisonPartyComponent));
            SettingsSetField(component, "<MobileParty>k__BackingField", garrison);
            fixture.Town.GarrisonPartyComponent = component;
            GarrisonSettings targetSettings = SurplusSettings(new Dictionary<CharacterObject, int> { [desired] = 5 }, false, 5);
            fixture.Settings.Template = targetSettings.Template;
            ImprovedSettlement automated = (ImprovedSettlement)RuntimeHelpers.GetUninitializedObject(typeof(ImprovedSettlement));
            SettingsSetField(automated, "<Settlement>k__BackingField", fixture.Settlement);
            MethodInfo remove = typeof(ImprovedSettlement).GetMethod("RemoveNonTemplateUnitsFromGarrison", BindingFlags.NonPublic | BindingFlags.Instance)!;
            MethodInfo autoSpawn = typeof(ImprovedSettlement).GetMethod("AutoSpawnGuardsIfNeeded", BindingFlags.NonPublic | BindingFlags.Instance)!;
            PropertyInfo guardOption = typeof(GarrisonSettings).GetProperty("GuardsAutoSpawnFromExcess")!;

            fixture.Settings.AutoRemoveNonTemplateTroops = false;
            remove.Invoke(automated, null);
            autoSpawn.Invoke(automated, null);
            Assert(roster.GetTroopCount(outsider) == 5, "Both options off must leave unneeded troops stationed.");
            guardOption.SetValue(fixture.Settings, true);
            fixture.Settings.AutoRemoveNonTemplateTroops = true;
            remove.Invoke(automated, null);
            Assert(roster.GetTroopCount(outsider) == 5, "Guard routing must take precedence over automatic removal.");

            // A registered guard blocks creation; pending surplus must still survive the removal pass.
            MobileGarrison existing = (MobileGarrison)RuntimeHelpers.GetUninitializedObject(typeof(MobileGarrison));
            IDictionary<string, MobileGarrison> guards = (IDictionary<string, MobileGarrison>)typeof(MobileGarrisonManager)
                .GetProperty("MobileGarrisons", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(Main.PartyManagement.mobileGarrisonManagement)!;
            guards[fixture.Settlement.StringId] = existing;
            autoSpawn.Invoke(automated, null);
            remove.Invoke(automated, null);
            Assert(roster.GetTroopCount(outsider) == 5 && guards[fixture.Settlement.StringId] == existing,
                "Waiting for an existing guard must not dismiss surplus or replace that guard.");
            Assert(Main.PartyManagement.mobileGarrisonManagement.CreateMobileGarrisonFromExcess(fixture.Settlement) == null,
                "The native manager must reject creation when a guard is already registered.");
            guards.Clear();
            PropertyInfo siege = fixture.Settlement.GetType().GetProperty("SiegeEvent")!;
            siege.SetValue(fixture.Settlement, RuntimeHelpers.GetUninitializedObject(siege.PropertyType));
            Assert(Main.PartyManagement.mobileGarrisonManagement.CreateMobileGarrisonFromExcess(fixture.Settlement) == null
                && roster.GetTroopCount(outsider) == 5 && guards.Count == 0,
                "A besieged settlement must keep all surplus stationed.");
            siege.SetValue(fixture.Settlement, null);
            fixture.Town.GarrisonPartyComponent = null!;
            Assert(Main.PartyManagement.mobileGarrisonManagement.CreateMobileGarrisonFromExcess(fixture.Settlement) == null
                && guards.Count == 0, "A missing garrison must not create an empty guard.");
            fixture.Town.GarrisonPartyComponent = component;
            guards[fixture.Settlement.StringId] = existing;
            guardOption.SetValue(fixture.Settings, false);
            fixture.Settings.AutoRemoveNonTemplateTroops = false;
            autoSpawn.Invoke(automated, null);
            remove.Invoke(automated, null);
            Assert(roster.GetTroopCount(outsider) == 5 && guards[fixture.Settlement.StringId] == existing,
                "Disabling the new option must leave existing guards and garrison troops alone.");
            // Reconstructed guards can carry the legacy NPC marker despite belonging to a player.
            existing.isNPC = true;
            var patrol = (ImprovedGarrisons.AI.Orders.PartyOrder.OrderPatrol)RuntimeHelpers.GetUninitializedObject(
                typeof(ImprovedGarrisons.AI.Orders.PartyOrder.OrderPatrol));
            SettingsSetField(existing, "<CurrentOrder>k__BackingField", patrol);
            SettingsSetField(garrison, "_actualClan", fixture.Clan);
            typeof(Campaign).GetProperty("MainParty")!.SetValue(campaign, garrison);
            typeof(ImprovedSettlement).GetMethod("ReturnGuardsIfNeeded", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(automated, null);
            Assert(existing.CurrentOrder == patrol,
                "Disabling surplus guards must not recall a player's already-created patrol.");

            fixture.Settings.AutoRemoveNonTemplateTroops = true;
            remove.Invoke(automated, null);
            Assert(roster.GetTroopCount(outsider) == 0,
                "With only removal enabled, the existing non-template removal path must still run.");

            fixture.Settings.Template = SurplusSettings(new Dictionary<CharacterObject, int> { [desired] = 5 }, true, 5).Template;
            guardOption.SetValue(fixture.Settings, true);
            roster.AddToCounts(desired, 7);
            roster.AddToCounts(outsider, 8);
            TroopRoster guardRoster = TroopRoster.CreateDummyTroopRoster();
            guardRoster.AddToCounts(desired, 5);
            guardRoster.AddToCounts(outsider, 5);
            SettingsSetField(existing, "mobileParty", SurplusParty(guardRoster));
            existing.fromSettlement = fixture.Settlement;
            existing.InitializeInitialTroopRoster(withReset: true);
            Assert(existing.InitialSize == 10 && fixture.Settings.InitialTroopRoster.Sum(x => x.Item2) == 10,
                "Automatic guards need their initial roster recorded even when their owner has no party.");
            guardRoster.AddToCounts(desired, -3);
            guardRoster.AddToCounts(outsider, -4);
            List<Tuple<CharacterObject, int>> refill = existing.GetAllReplenishTroops();
            Assert(refill.Count == 2 && refill.Single(x => x.Item1 == desired).Item2 == 2
                && refill.Single(x => x.Item1 == outsider).Item2 == 4,
                "Guard replenishment must not take troops reserved for the template while surplus mode is enabled.");
            guardOption.SetValue(fixture.Settings, false);
            refill = existing.GetAllReplenishTroops();
            Assert(refill.Single(x => x.Item1 == desired).Item2 == 3,
                "Disabling surplus mode must preserve the existing guard replenishment behavior.");
            Console.WriteLine("PASS actual automatic-removal toggle combinations and existing-guard wait behavior");
        }
        finally
        {
            Main.PartyManagement = previousParties;
            Main.UpgradeLogic = previousUpgrade;
            campaignProperty.SetValue(null, previousCampaign);
        }
    }

    private static MobileParty SurplusParty(TroopRoster roster)
    {
        MobileParty mobile = (MobileParty)RuntimeHelpers.GetUninitializedObject(typeof(MobileParty));
        PartyBase party = (PartyBase)RuntimeHelpers.GetUninitializedObject(typeof(PartyBase));
        SettingsSetField(party, "<MemberRoster>k__BackingField", roster);
        SettingsSetField(party, "<MobileParty>k__BackingField", mobile);
        SettingsSetField(mobile, "<Party>k__BackingField", party);
        return mobile;
    }

    private static CharacterObject SurplusCharacter(string id, params CharacterObject[] upgrades)
    {
        CharacterObject character = new() { StringId = id };
        typeof(CharacterObject).GetProperty("UpgradeTargets")!.SetValue(character, upgrades);
        return character;
    }

    private static Dictionary<CharacterObject, int> SurplusSelect(MethodInfo select, TroopRoster roster,
        IDictionary<CharacterObject, int>? targets)
    {
        return (Dictionary<CharacterObject, int>)select.Invoke(null, new object?[] { roster, targets })!;
    }

    private static GarrisonSettings SurplusSettings(Dictionary<CharacterObject, int> targets, bool enabled, int size)
    {
        GarrisonSettings settings = new() { GuardsAutoSpawnSize = size, Template = new TrainingTemplate("Surplus test") };
        typeof(GarrisonSettings).GetProperty("GuardsAutoSpawnFromExcess")!.SetValue(settings, enabled);
        typeof(TrainingTemplate).GetField("troopListWithCharacter", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(settings.Template, targets);
        typeof(TrainingTemplate).GetField("troopList", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(settings.Template, targets.ToDictionary(pair => pair.Key.StringId, pair => pair.Value));
        return settings;
    }
}
