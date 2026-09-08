using System.Reflection;
using System.Runtime.CompilerServices;
using Autofac;
using Coop.Core.Server;
using Coop.Core.Server.States;
using GameInterface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using ImprovedGarrisons;
using ImprovedGarrisons.CoopIntegration.Protocol;
using ImprovedGarrisons.SaveSystem.SaveData;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using ImprovedGarrisons.SaveSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace ImprovedGarrisons.CoopRuntimeContract;

public static partial class ContractRunner
{
    private static void RunSettingsPersistenceTests()
    {
        IGSaveData previous = IGSaveData.Instance;
        try
        {
            IGSaveData.Instance = new IGSaveData();
            using SettingsTownFixture fixture = new();
            Type store = typeof(ConfigRequest).Assembly.GetType(
                "ImprovedGarrisons.CoopIntegration.Persistence.SettingsStateStore", true)!;
            MethodInfo build = store.GetMethod("BuildSettingsText")!;
            MethodInfo apply = store.GetMethod("ApplySettingsText", BindingFlags.Static | BindingFlags.NonPublic)!;
            fixture.Settings.Template.Clear();
            string snapshot = (string)build.Invoke(null, new object?[] { null })!;
            fixture.Settings.Template.SetTroops(new Dictionary<string, int> { ["old-target"] = 20 });
            apply.Invoke(null, new object[] { snapshot, false });
            Assert(fixture.Settings.Template.GetTroopList().Count == 0,
                "Reopening a town with an empty saved template retained its previous targets.");
            Console.WriteLine("PASS persisted empty template replaces previous targets");
            TestSettingsMalformedSnapshotsAreAtomic(apply, fixture);
            TestSettingsRestartAndWriteRetry(store, fixture);
            TestSettingsOwnershipRefresh(fixture);
            Main.GarrisonBehavior.SettlementSettingsData["NPC"] = new NPCGarrisonSettings();
            apply.Invoke(null, new object[] { string.Empty, true });
            Assert(Main.GarrisonBehavior.SettlementSettingsData.Count == 1
                && Main.GarrisonBehavior.SettlementSettingsData.ContainsKey("NPC"),
                "An empty authoritative snapshot retained stale player settings or removed NPC settings.");
        }
        finally
        {
            IGSaveData.Instance = previous;
        }
    }

    private static void TestSettingsMalformedSnapshotsAreAtomic(MethodInfo apply, SettingsTownFixture fixture)
    {
        string[] invalidRecords =
        {
            "EnableTraining",
            "MaxUpgradeTier=invalid",
            "Template=dHJvb3A=:oops",
            "TroopsToUpgradeTo=1,invalid"
        };
        fixture.Settings.GuardsAutoSpawnSize = 81;
        foreach (string record in invalidRecords)
        {
            try
            {
                apply.Invoke(null, new object[] { "[U3VycGx1cyBTZXR0aW5ncyBUb3du]\nGuardsAutoSpawnSize=12\n" + record, false });
                throw new InvalidOperationException("A malformed snapshot was accepted: " + record);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is FormatException)
            {
                Assert(fixture.Settings.GuardsAutoSpawnSize == 81,
                    "A malformed snapshot partially replaced valid live settings.");
            }
        }
        Console.WriteLine("PASS malformed and truncated snapshots leave live settings intact");
    }

    private static void TestSettingsRestartAndWriteRetry(Type store, SettingsTownFixture fixture)
    {
        Type paths = GetIntegrationDataPathsType();
        FieldInfo directoryField = paths.GetField("_directory", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? previousDirectory = directoryField.GetValue(null);
        string directory = Path.Combine(Path.GetTempPath(), "ig-settings-restart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settlement-settings.txt");
        try
        {
            directoryField.SetValue(null, directory);
            Common.ModInformation.IsServer = true;
            IServerLogic server = DispatchProxy.Create<IServerLogic, SettingsServerLogicProxy>();
            SettingsServerLogicProxy logic = (SettingsServerLogicProxy)server;
            logic.State = null;
            IPlayerManager players = DispatchProxy.Create<IPlayerManager, SettingsLookupProxy>();
            ((SettingsLookupProxy)players).Player = new Player("saved-offline", "hero", "party", fixture.Clan.StringId, "character");
            IObjectManager objects = DispatchProxy.Create<IObjectManager, SettingsLookupProxy>();
            ((SettingsLookupProxy)objects).Clan = fixture.Clan;
            using IContainer container = BuildContainer(builder =>
            {
                builder.RegisterInstance(server).As<IServerLogic>();
                builder.RegisterInstance(players).As<IPlayerManager>();
                builder.RegisterInstance(objects).As<IObjectManager>();
            });
            ContainerProvider.SetContainer(container);
            fixture.Settings.GuardsAutoSpawnSize = 73;
            fixture.Settings.EnableTraining = true;
            bool[] upgradePaths = { true, false, true };
            fixture.Settings.TroopsToUpgradeTo = upgradePaths;
            fixture.Settings.Template.SetTroops(new Dictionary<string, int> { ["saved-target"] = 42, ["unlimited-target"] = 0 });
            string saved = (string)store.GetMethod("BuildSettingsText")!.Invoke(null, new object?[] { null })!;
            File.WriteAllText(path, saved);
            IGSaveData.Instance = new IGSaveData();
            Type registry = typeof(ConfigRequest).Assembly.GetType("ImprovedGarrisons.CoopIntegration.Persistence.ServerClanRegistry", true)!;
            ((HashSet<string>)registry.GetField("ClanIds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Remove(fixture.Clan.StringId);
            MethodInfo poll = store.GetMethod("PollServer", BindingFlags.Static | BindingFlags.NonPublic)!;
            poll.Invoke(null, null);
            Assert(Main.GarrisonBehavior.SettlementSettingsData.Count == 0 && File.ReadAllText(path) == saved,
                "Polling before Coop finishes loading restored or overwrote settlement settings.");

            logic.State = CreateSettingsRunningState();
            poll.Invoke(null, null);
            GarrisonSettings restored = Main.GarrisonBehavior.SettlementSettingsData["Surplus Settings Town"];
            Assert(restored.GuardsAutoSpawnSize == 73 && restored.EnableTraining
                && restored.TroopsToUpgradeTo.SequenceEqual(upgradePaths)
                && restored.Template.GetTroopList()["saved-target"] == 42
                && restored.Template.GetTroopList()["unlimited-target"] == 0,
                "Starting an updated server did not restore settings, upgrade paths, and template targets.");
            Assert((bool)registry.GetMethod("Contains", new[] { typeof(string) })!.Invoke(null, new object[] { fixture.Clan.StringId })!,
                "A saved player's clan was not restored until that player connected.");

            restored.GuardsAutoSpawnSize = 93;
            poll.Invoke(null, null);
            Assert(File.ReadAllText(path).Contains("GuardsAutoSpawnSize=93", StringComparison.Ordinal),
                "A changed town setting was not persisted.");
            string backup = File.ReadAllText(path + ".bak");
            store.GetMethod("MarkDirty")!.Invoke(null, null);
            poll.Invoke(null, null);
            Assert(File.ReadAllText(path + ".bak") == backup,
                "An unchanged settings broadcast replaced the previous settings backup.");

            restored.GuardsAutoSpawnSize = 127;
            Directory.CreateDirectory(path + ".tmp");
            poll.Invoke(null, null);
            Assert(File.ReadAllText(path).Contains("GuardsAutoSpawnSize=93", StringComparison.Ordinal),
                "A failed write damaged the existing settings.");
            Directory.Delete(path + ".tmp");
            poll.Invoke(null, null);
            Assert(File.ReadAllText(path).Contains("GuardsAutoSpawnSize=127", StringComparison.Ordinal),
                "A failed settings write was marked complete and never retried.");

            IGSaveData.Instance = new IGSaveData();
            poll.Invoke(null, null);
            Assert(Main.GarrisonBehavior.SettlementSettingsData["Surplus Settings Town"].GuardsAutoSpawnSize == 127,
                "Replacing the loaded campaign data skipped restoration because this process already restored once.");
            const string corrupt = "[U3VycGx1cyBTZXR0aW5ncyBUb3du]\nMaxUpgradeTier=invalid\n";
            backup = File.ReadAllText(path + ".bak");
            File.WriteAllText(path, corrupt);
            IGSaveData.Instance = new IGSaveData();
            poll.Invoke(null, null);
            poll.Invoke(null, null);
            Assert(File.ReadAllText(path) == corrupt && File.ReadAllText(path + ".bak") == backup,
                "A failed restore overwrote the settings file or its backup with defaults.");
            Console.WriteLine("PASS Coop load gating, offline player restore, disk edits, retry, backups, and campaign reload");
        }
        finally
        {
            directoryField.SetValue(null, previousDirectory);
            ContainerProvider.Clear();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void TestSettingsOwnershipRefresh(SettingsTownFixture fixture)
    {
        Type patches = typeof(ConfigRequest).Assembly.GetType("ImprovedGarrisons.CoopIntegration.Patching.ClientServerPatches", true)!;
        MethodInfo ownerChanged = typeof(GarrisonBehavior).GetMethod("onSettlementOwnerChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo initialize = typeof(GarrisonBehavior).GetMethod("InitializeSettlements", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Harmony harmony = new("ig-contract-settings-ownership");
        harmony.Patch(ownerChanged, prefix: new HarmonyMethod(patches.GetMethod("SettlementOwnerChangedPrefix")!));
        harmony.Patch(initialize, prefix: new HarmonyMethod(patches.GetMethod("InitializeSettlementsPrefix")!));
        harmony.Patch(typeof(GarrisonBehavior).GetMethod("GetTownSettings")!,
            prefix: new HarmonyMethod(patches.GetMethod("ServerTownSettingsPrefix")!));
        try
        {
            Main.GarrisonBehavior.SettlementSettingsData["Surplus Settings Town"] = fixture.Settings;
            Hero owner = (Hero)RuntimeHelpers.GetUninitializedObject(typeof(Hero));
            Hero previousOwner = (Hero)RuntimeHelpers.GetUninitializedObject(typeof(Hero));
            SettingsSetField(owner, "_clan", fixture.Clan);
            SettingsSetField(previousOwner, "_clan", fixture.Clan);
            Common.ModInformation.IsServer = true;
            ownerChanged.Invoke(Main.GarrisonBehavior, new object?[]
                { fixture.Settlement, false, owner, previousOwner, null, default(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail) });
            initialize.Invoke(Main.GarrisonBehavior, null);
            Assert(ReferenceEquals(Main.GarrisonBehavior.SettlementSettingsData["Surplus Settings Town"], fixture.Settings),
                "A same-clan ownership refresh or single-player cleanup discarded Coop town settings.");
            Assert(ReferenceEquals(Main.GarrisonBehavior.GetTownSettings(fixture.Town), fixture.Settings),
                "Reopening server town settings returned defaults instead of the saved configuration.");

            SettingsSetField(previousOwner, "_clan", RuntimeHelpers.GetUninitializedObject(typeof(Clan)));
            ownerChanged.Invoke(Main.GarrisonBehavior, new object?[]
                { fixture.Settlement, false, owner, previousOwner, null, default(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail) });
            Assert(!ReferenceEquals(Main.GarrisonBehavior.GetTownSettings(fixture.Town), fixture.Settings),
                "A genuine transfer between clans retained the previous owner's settings.");
            Main.GarrisonBehavior.SettlementSettingsData["Surplus Settings Town"] = fixture.Settings;

            ResetIntegrationTransport();
            ConnectAsClient();
            SettingsSetField(previousOwner, "_clan", RuntimeHelpers.GetUninitializedObject(typeof(Clan)));
            ownerChanged.Invoke(Main.GarrisonBehavior, new object?[]
                { fixture.Settlement, false, owner, previousOwner, null, default(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail) });
            Assert(ReferenceEquals(Main.GarrisonBehavior.SettlementSettingsData["Surplus Settings Town"], fixture.Settings),
                "A client ownership notification discarded authoritative settings.");
            Console.WriteLine("PASS actual patched ownership notifications and load cleanup retain Coop settings");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            ResetIntegrationTransport();
        }
    }
}
