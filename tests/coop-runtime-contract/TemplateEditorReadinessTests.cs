using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ImprovedGarrisons.AI.AIManagers;
using ImprovedGarrisons.CoopIntegration.Protocol;
using ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace ImprovedGarrisons.CoopRuntimeContract;

public static partial class ContractRunner
{
    private static TroopRoster? templateEditorLeft;
    private static TroopRoster? templateEditorRight;
    private static Action<TroopRoster, TroopRoster>? templateEditorDone;
    private static Action? templateEditorCancel;
    private static Exception? templateEditorError;

    private static void RunTemplateEditorReadinessTests()
    {
        TestTemplateEditorSavesEmptyRoster();
        TestSurplusReadinessStates();
    }

    private static void TestTemplateEditorSavesEmptyRoster()
    {
        using SettingsTownFixture fixture = new();
        var previousParties = Main.PartyManagement;
        PropertyInfo currentCampaign = typeof(Campaign).GetProperty("Current")!;
        object? previousCampaign = currentCampaign.GetValue(null);
        FieldInfo objectManagerInstance = typeof(MBObjectManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? previousObjectManager = objectManagerInstance.GetValue(null);
        Harmony harmony = new("improvedgarrisons.tests.template.editor");
        List<MethodBase> patched = new();
        try
        {
            Main.PartyManagement = new PartyManager();
            Main.GarrisonBehavior.CurrentTownForSettings = fixture.Town;
            Campaign campaign = (Campaign)RuntimeHelpers.GetUninitializedObject(typeof(Campaign));
            GameModels models = (GameModels)RuntimeHelpers.GetUninitializedObject(typeof(GameModels));
            typeof(GameModels).GetProperty("PartySizeLimitModel")!.SetValue(models,
                RuntimeHelpers.GetUninitializedObject(typeof(TemplateEditorSizeModel)));
            SettingsSetField(campaign, "_gameModels", models);
            currentCampaign.SetValue(null, campaign);
            MBObjectManager manager = MBObjectManager.Init();
            manager.RegisterType<CharacterObject>("Character", "Characters", 1, autoCreateInstance: false);
            CharacterObject first = SurplusCharacter("editor_first");
            CharacterObject second = SurplusCharacter("editor_second");
            manager.RegisterObject(first);
            manager.RegisterObject(second);
            SettingsSetField(campaign, "_characters", manager.GetObjectTypeList<CharacterObject>());
            fixture.Settings.Template = SurplusSettings(new Dictionary<CharacterObject, int> { [first] = 3, [second] = 4 }, false, 50).Template;
            TrainingSettings training = new();
            SettingsSetField(training, "_currentTown", fixture.Town);
            MethodInfo open = typeof(TrainingSettings).GetMethod("PromptClanSpecificUnitsWithPartyManager", BindingFlags.NonPublic | BindingFlags.Instance)!;
            MethodInfo screen = typeof(PartyManager).GetMethods().Single(x => x.Name == "PromptManagementScreenWithActions" && x.GetParameters().Length == 5);
            ConstructorInfo constructor = typeof(MobileParty).GetConstructor(Type.EmptyTypes)!;
            MethodInfo log = typeof(ImprovedGarrisons.Debugging.LogFileSystem.LogFileManager).GetMethod("WriteErrorLogEntry",
                new[] { typeof(string), typeof(Exception), typeof(bool) })!;
            harmony.Patch(log, prefix: new HarmonyMethod(typeof(ContractRunner).GetMethod(nameof(TemplateEditorLogPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
            patched.Add(log);
            harmony.Patch(constructor, prefix: new HarmonyMethod(typeof(ContractRunner).GetMethod(nameof(TemplateEditorPartyPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
            patched.Add(constructor);
            harmony.Patch(screen, prefix: new HarmonyMethod(typeof(ContractRunner).GetMethod(nameof(TemplateEditorCapturePrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
            patched.Add(screen);
            List<InquiryElement> choices = new() { new InquiryElement(new List<InquiryElement>
                { new(first, "First", null), new(second, "Second", null) }, "Culture", null) };

            void OpenEditor()
            {
                templateEditorDone = null;
                templateEditorCancel = null;
                templateEditorError = null;
                open.Invoke(training, new object[] { choices });
                if (templateEditorError != null)
                {
                    int offset = new System.Diagnostics.StackTrace(templateEditorError).GetFrame(0)!.GetILOffset();
                    throw new InvalidOperationException($"Template editor logged an error at IL_{offset:x4}.", templateEditorError);
                }
                Assert(templateEditorDone != null && templateEditorCancel != null && templateEditorLeft != null,
                    "The actual template editor failed to open its party-screen callbacks.");
            }

            OpenEditor();
            Assert(templateEditorLeft!.GetTroopCount(first) == 3 && templateEditorLeft.GetTroopCount(second) == 4,
                "The editor did not open with current template targets.");
            templateEditorLeft.AddToCounts(first, -3);
            templateEditorDone!(templateEditorLeft, templateEditorRight!);
            Assert(templateEditorError == null, "Saving template edits logged an error.");
            OpenEditor();
            Assert(templateEditorLeft!.GetTroopCount(first) == 0 && templateEditorLeft.GetTroopCount(second) == 4,
                "A removed troop type returned when reopening the saved template.");

            templateEditorLeft.Clear();
            templateEditorCancel!();
            OpenEditor();
            Assert(templateEditorLeft!.GetTroopCount(second) == 4,
                "Cancelling changed the stored template.");

            templateEditorLeft.Clear();
            templateEditorDone!(templateEditorLeft, templateEditorRight!);
            Assert(templateEditorError == null, "Clearing the template logged an error.");
            Assert(fixture.Settings.Template.AmountOfTroopsInTemplate == 0,
                "Saving after removing the last troop type must persist an empty template.");
            OpenEditor();
            Assert(templateEditorLeft!.TotalManCount == 0 && templateEditorRight!.TotalManCount > 0,
                "Cleared targets returned, or the available troop catalogue disappeared.");
            TrainingUIVM view = new();
            Assert(view.CurrentTemplateAddTroopText == "Edit template troops", "The editor button still implies that it only adds troops.");

            fixture.Settings.Template = SurplusSettings(new Dictionary<CharacterObject, int> { [second] = 4 }, false, 50).Template;
            ResetIntegrationTransport();
            var (network, _) = ConnectAsClient();
            Type patches = typeof(ConfigRequest).Assembly.GetType("ImprovedGarrisons.CoopIntegration.Patching.ClientServerPatches", true)!;
            MethodInfo save = typeof(TrainingSettings).GetMethod("SetSpecifiedUpgradeTargets", BindingFlags.NonPublic | BindingFlags.Instance)!;
            harmony.Patch(save, prefix: new HarmonyMethod(patches.GetMethod("SetTemplatePrefix")!));
            patched.Add(save);
            OpenEditor();
            templateEditorLeft!.Clear();
            templateEditorDone!(templateEditorLeft, templateEditorRight!);
            SettingsIntent request = network.SentAll.OfType<SettingsIntent>().Single(x => x.Operation == SettingsIntentKind.SetTemplateFull);
            Assert(string.IsNullOrEmpty(request.ListArgument) && request.SettlementId == fixture.Settlement.StringId
                && fixture.Settings.Template.AmountOfTroopsInTemplate == 1,
                "Clearing the editor on a Coop client must forward an empty template without local mutation.");
            ResetIntegrationTransport();
            Common.ModInformation.IsServer = true;
            Type actionType = typeof(ConfigRequest).Assembly.GetType("ImprovedGarrisons.CoopIntegration.Runtime.ServerAction", true)!;
            object action = Activator.CreateInstance(actionType, nonPublic: true)!;
            actionType.GetProperty("ListArgument")!.SetValue(action, request.ListArgument);
            Type dispatcher = typeof(ConfigRequest).Assembly.GetType("ImprovedGarrisons.CoopIntegration.Runtime.ServerActionDispatcher", true)!;
            object outcome = dispatcher.GetMethod("SetTemplate", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new[] { action, fixture.Town })!;
            Assert((bool)outcome.GetType().GetProperty("Success")!.GetValue(outcome)! && fixture.Settings.Template.AmountOfTroopsInTemplate == 0,
                "The server did not apply the empty template sent by the editor.");
            Console.WriteLine("PASS actual template editor removes types, persists empty targets, reopens correctly, and cancels without changes");
            Console.WriteLine("PASS empty template editor change is forwarded by Coop and applied by the server");
        }
        finally
        {
            foreach (MethodBase method in patched)
            {
                harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
            }
            templateEditorLeft = null;
            templateEditorRight = null;
            templateEditorDone = null;
            templateEditorCancel = null;
            templateEditorError = null;
            Main.PartyManagement = previousParties;
            ResetIntegrationTransport();
            currentCampaign.SetValue(null, previousCampaign);
            objectManagerInstance.SetValue(null, previousObjectManager);
        }
    }

    private static bool TemplateEditorPartyPrefix(MobileParty __instance)
    {
        PartyBase party = SurplusParty(TroopRoster.CreateDummyTroopRoster()).Party;
        SettingsSetField(party, "<MobileParty>k__BackingField", __instance);
        SettingsSetField(__instance, "<Party>k__BackingField", party);
        return false;
    }

    private static bool TemplateEditorLogPrefix(Exception ex)
    {
        templateEditorError = ex;
        return false;
    }

    private static bool TemplateEditorCapturePrefix(PartyBase leftParty, MobileParty rightParty,
        Action<TroopRoster, TroopRoster> doneAction, Action cancelAction)
    {
        templateEditorLeft = leftParty.MemberRoster;
        templateEditorRight = rightParty.MemberRoster;
        templateEditorDone = doneAction;
        templateEditorCancel = cancelAction;
        return false;
    }

    private sealed class TemplateEditorSizeModel : DefaultPartySizeLimitModel
    {
        public override ExplainedNumber GetPartyMemberSizeLimit(PartyBase party, bool includeDescriptions = false) => new(20);
    }

    private static void TestSurplusReadinessStates()
    {
        Type type = typeof(Main).Assembly.GetType("ImprovedGarrisons.ImprovedGarrisonsUI.UIElements.SurplusGuardReadinessVM", true)!;
        object view = Activator.CreateInstance(type)!;
        MethodInfo refresh = type.GetMethod("Refresh")!;
        CharacterObject target = SurplusCharacter("ready_target");
        CharacterObject outsider = SurplusCharacter("ready_outsider");
        TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
        roster.AddToCounts(target, 20);
        roster.AddToCounts(outsider, 10, woundedCount: 2);
        GarrisonSettings settings = SurplusSettings(new Dictionary<CharacterObject, int> { [target] = 10 }, true, 50);

        string Refresh(TroopRoster? troops, GarrisonSettings? options, bool guard = false, bool attack = false)
        {
            refresh.Invoke(view, new object?[] { troops, options, guard, attack });
            return (string)type.GetProperty("Text")!.GetValue(view)!;
        }

        Assert(Refresh(roster, settings).Contains("18 / 50") && Refresh(roster, settings).Contains("Waiting for troops"),
            "Readiness must use healthy surplus after reserving template targets.");
        Assert(Refresh(roster, settings, guard: true).Contains("Guard already active"), "Readiness missed the active-guard blocker.");
        Assert(Refresh(roster, settings, attack: true).Contains("under attack"), "Readiness missed the settlement attack blocker.");
        settings.GuardsAutoSpawnSize = 18;
        Assert(Refresh(roster, settings).Contains("Ready on next hourly check"), "A full batch did not become ready.");
        roster.AddToCounts(outsider, -1);
        Assert(Refresh(roster, settings).Contains("17 / 18") && Refresh(roster, settings).Contains("Waiting for troops"),
            "Readiness did not refresh after the real roster changed.");
        typeof(GarrisonSettings).GetProperty("GuardsAutoSpawnFromExcess")!.SetValue(settings, false);
        Assert(Refresh(roster, settings).Contains("off"), "The disabled option did not explain that automatic surplus guards are off.");
        typeof(GarrisonSettings).GetProperty("GuardsAutoSpawnFromExcess")!.SetValue(settings, true);
        Assert(Refresh(null, settings).Contains("No garrison"), "The missing-garrison state was not explained.");
        settings.GuardsAutoSpawnSize = 0;
        Assert(Refresh(roster, settings).Contains("Set a guard party size"), "An invalid party size was incorrectly shown as ready.");
        settings.GuardsAutoSpawnSize = 18;
        settings.Template = new TrainingTemplate("Empty");
        Assert(Refresh(roster, settings).Contains("Set a template"), "An empty template was incorrectly shown as ready.");
        Assert(Refresh(roster, null).Contains("Select a settlement"), "Missing settlement settings were not explained.");
        Console.WriteLine("PASS surplus guard readiness counts, blockers, disabled/empty states, and live refresh");
    }
}
