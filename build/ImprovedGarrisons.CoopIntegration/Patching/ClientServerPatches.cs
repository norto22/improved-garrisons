using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using ImprovedGarrisons.AI.AIManagers;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.CoopIntegration.Core;
using ImprovedGarrisons.CoopIntegration.Persistence;
using ImprovedGarrisons.CoopIntegration.Protocol;
using ImprovedGarrisons.CoopIntegration.Runtime;
using ImprovedGarrisons.ImprovedGarrisonsUI.UIElements;
using ImprovedGarrisons.SaveSystem;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.CoopIntegration.Patching
{
    internal static class ClientServerPatches
    {
        private const string HarmonyId = "ImprovedGarrisons.CoopIntegration.Runtime";
        private static readonly FieldInfo? RecruiterTownField = AccessTools.Field(typeof(RecruitmentSettings), "_recruiterTown");
        private static readonly FieldInfo? RecruiterCultureField = AccessTools.Field(typeof(RecruitmentSettings), "_cultureToRecruitFromForNewRecruiter");
        private static readonly FieldInfo? RecruiterAmountField = AccessTools.Field(typeof(RecruitmentSettings), "_amountToRecruitForNewRecruiter");
        private static bool _applied;

        // A dragged slider calls its bound setter once per value tick (every pixel of the drag), and each call
        // used to forward straight to the server and echo an on-screen outcome message -- dragging 0..100 spammed
        // ~100 network round-trips and ~100 chat lines. Coalesce to at most one send per throttle window, keyed
        // per (operation, settlement) so unrelated sliders never block each other; the pending value is always
        // eventually flushed via Main's every-tick queue so the final dragged value still reaches the server.
        private const int SettingForwardThrottleMs = 200;
        private static readonly Dictionary<(SettingsIntentKind Operation, string SettlementId), int> _lastSettingSendTick = new Dictionary<(SettingsIntentKind, string), int>();
        private static readonly Dictionary<(SettingsIntentKind Operation, string SettlementId), SettingsIntent> _pendingSettingIntent = new Dictionary<(SettingsIntentKind, string), SettingsIntent>();

        public static void Apply()
        {
            if (_applied)
            {
                return;
            }

            Harmony harmony = new Harmony(HarmonyId);
            int applied = 0;
            int failed = 0;

            Patch(harmony, typeof(PartyManager), "InitializeNewParty", nameof(InitializeNewPartyPrefix), nameof(InitializeNewPartyPostfix), ref applied, ref failed);
            PatchClientSimulation(harmony, ref applied, ref failed);
            PatchHeadlessServer(harmony, ref applied, ref failed);
            PatchClientActions(harmony, ref applied, ref failed);
            PatchIdentity(harmony, ref applied, ref failed);

            _applied = true;
            IntegrationLog.Information("runtime patches armed: " + applied + " applied, " + failed + " unavailable");
        }

        public static bool InitializeNewPartyPrefix(ref PartyBase? __result)
        {
            if (IntegrationRuntime.IsServer && IntegrationRuntime.NativePartyRegistrationReady)
            {
                return true;
            }

            __result = null;
            if (IntegrationRuntime.IsServer)
            {
                IntegrationLog.Error("blocked Improved Garrisons party creation because Coop-native MobileParty registration is not ready");
            }
            return false;
        }

        public static void InitializeNewPartyPostfix(PartyBase? __result)
        {
            if (IntegrationRuntime.IsServer && __result?.MobileParty != null)
            {
                CoopMobilePartyRegistration.ValidateCreatedParty(__result.MobileParty);
            }
        }

        public static bool SkipOnClientPrefix()
        {
            return !IsClient();
        }

        public static bool SkipOnServerPrefix()
        {
            return !IntegrationRuntime.IsServer;
        }

        public static bool SkipOnServerFalsePrefix(ref bool __result)
        {
            if (!IntegrationRuntime.IsServer)
            {
                return true;
            }

            __result = false;
            return false;
        }

        public static bool SkipConfigIoOnClientPrefix()
        {
            return !IsClient();
        }

        public static bool BlockConfigScreenPrefix()
        {
            if (!IntegrationRuntime.IsServer && !IsClient())
            {
                return true;
            }

            if (IsClient())
            {
                Show("IG: configuration is controlled by the Coop server.");
            }

            return false;
        }

        public static void CurrentTownPostfix(Town value)
        {
            if (IsClient() && value?.Settlement != null)
            {
                IntegrationTransport.SendIntent(Party(PartyIntentKind.SyncTown, value));
            }
        }

        public static bool ServerOnGameOpenPrefix(GarrisonBehavior __instance)
        {
            if (!IntegrationRuntime.IsServer)
            {
                return true;
            }

            MethodInfo? initialize = AccessTools.Method(typeof(GarrisonBehavior), "SetAllAutomatedGarrisons");
            initialize?.Invoke(__instance, null);
            return false;
        }

        public static bool ServerTownSettingsPrefix(GarrisonBehavior __instance, Town town, ref GarrisonSettings __result)
        {
            if (!IntegrationRuntime.IsServer || town?.Settlement == null || !ServerClanRegistry.Contains(town.Settlement.OwnerClan))
            {
                return true;
            }

            string key = town.Name?.ToString() ?? town.Settlement.StringId;
            if (!__instance.SettlementSettingsData.TryGetValue(key, out GarrisonSettings settings)
                || settings == null || settings is NPCGarrisonSettings)
            {
                settings = new GarrisonSettings();
                __instance.SettlementSettingsData[key] = settings;
            }

            __result = settings;
            return false;
        }

        public static bool ForwardSettingPrefix(MethodBase __originalMethod, object[] __args)
        {
            if (!IsClient())
            {
                return true;
            }

            Town? town = __args != null && __args.Length > 0 ? __args[0] as Town : null;
            if (town?.Settlement == null)
            {
                Show("IG: select a settlement before changing its settings.");
                return false;
            }

            if (!TryGetSettingOperation(__originalMethod, out SettingsIntentKind operation))
            {
                Show("IG: that settings operation is not supported by the Coop integration.");
                return false;
            }

            SettingsIntent request = Settings(operation, town);
            if (__args != null && __args.Length > 1)
            {
                object value = __args[1];
                if (value is int integer)
                {
                    request.ArgumentKind = 1;
                    request.IntegerArgument = integer;
                }
                else if (value is float floating)
                {
                    request.ArgumentKind = 2;
                    request.FloatArgument = floating;
                }
                else if (value is bool boolean)
                {
                    request.ArgumentKind = 3;
                    request.BooleanArgument = boolean;
                }
            }

            SendSettingThrottled(operation, request);
            return false;
        }

        private static void SendSettingThrottled(SettingsIntentKind operation, SettingsIntent request)
        {
            (SettingsIntentKind, string) key = (operation, request.SettlementId);
            int now = Environment.TickCount;
            if (_lastSettingSendTick.TryGetValue(key, out int lastTick) && unchecked(now - lastTick) < SettingForwardThrottleMs)
            {
                _pendingSettingIntent[key] = request;
                ScheduleSettingFlush(key);
                return;
            }

            _lastSettingSendTick[key] = now;
            _pendingSettingIntent.Remove(key);
            IntegrationTransport.SendIntent(request);
        }

        private static void ScheduleSettingFlush((SettingsIntentKind Operation, string SettlementId) key)
        {
            string flushId = "ig-coop-setting-flush:" + key.Operation + ":" + key.SettlementId;
            global::ImprovedGarrisons.Main.AddActionToExecuteEachTick(flushId, delegate
            {
                if (!_pendingSettingIntent.TryGetValue(key, out SettingsIntent pending))
                {
                    global::ImprovedGarrisons.Main.RemoveActionToExecuteEachTick(flushId);
                    return;
                }

                int elapsed = _lastSettingSendTick.TryGetValue(key, out int lastTick) ? unchecked(Environment.TickCount - lastTick) : SettingForwardThrottleMs;
                if (elapsed < SettingForwardThrottleMs)
                {
                    return;
                }

                _pendingSettingIntent.Remove(key);
                _lastSettingSendTick[key] = Environment.TickCount;
                IntegrationTransport.SendIntent(pending);
                global::ImprovedGarrisons.Main.RemoveActionToExecuteEachTick(flushId);
            });
        }

        public static bool CreateGuardsPrefix(Town town)
        {
            if (!IsClient())
            {
                return true;
            }

            if (town?.Settlement == null)
            {
                Show("IG: select a settlement before creating guards.");
                return false;
            }

            string settlementId = town.Settlement.StringId;
            InformationManager.ShowTextInquiry(new TextInquiryData(
                new TextObject("{=cmp_ig_guardcount1}Guard party size").ToString(),
                new TextObject("{=cmp_ig_guardcount2}How many garrison troops should form the guard party? (recommended: 30)").ToString(),
                true,
                true,
                new TextObject("{=menu_ok}Okay").ToString(),
                new TextObject("{=menu_cancel}Cancel").ToString(),
                input =>
                {
                    if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                    {
                        IntegrationTransport.SendIntent(new PartyIntent
                        {
                            Operation = PartyIntentKind.CreateGuards,
                            SettlementId = settlementId,
                            IntegerArgument = count
                        });
                    }
                },
                InformationManager.HideInquiry,
                false,
                input =>
                {
                    bool valid = int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) && count > 0 && count <= 300;
                    return new Tuple<bool, string>(valid, valid ? string.Empty : "Value must be between 1 and 300.");
                },
                "30"));
            return false;
        }

        public static bool CreateRecruiterPrefix(RecruitmentSettings __instance)
        {
            if (!IsClient())
            {
                return true;
            }

            Town? town = RecruiterTownField?.GetValue(__instance) as Town;
            if (town?.Settlement == null)
            {
                Show("IG: select a settlement before creating a recruiter.");
                return false;
            }

            CultureObject? culture = RecruiterCultureField?.GetValue(__instance) as CultureObject;
            int amount = RecruiterAmountField?.GetValue(__instance) is int value ? value : 0;
            PartyIntent request = Party(PartyIntentKind.CreateRecruiter, town);
            request.StringArgument = culture?.StringId ?? string.Empty;
            request.IntegerArgument = amount;
            IntegrationTransport.SendIntent(request);
            return false;
        }

        public static bool OrderPatrolPrefix(Town town)
        {
            return ForwardTownAction(PartyIntentKind.OrderPatrol, town);
        }

        public static bool OrderReturnPrefix(Town town)
        {
            return ForwardTownAction(PartyIntentKind.OrderReturn, town, true);
        }

        public static bool OrderAttackDefendPrefix(Town town)
        {
            return ForwardTownAction(PartyIntentKind.OrderReturn, town, false);
        }

        public static bool ReturnRecruiterPrefix(Town town)
        {
            return ForwardTownAction(PartyIntentKind.ReturnRecruiter, town);
        }

        public static bool ChangeRecruiterCulturePrefix(Town town)
        {
            if (!IsClient())
            {
                return true;
            }

            if (town?.Settlement == null)
            {
                return false;
            }

            string settlementId = town.Settlement.StringId;
            global::ImprovedGarrisons.Main.PartyManagement.garrisonRecruiterPartyManagement.PromptCultureSelection(selected =>
            {
                CultureObject? culture = selected != null && selected.Count > 0 ? selected[0].Identifier as CultureObject : null;
                if (culture != null)
                {
                    IntegrationTransport.SendIntent(new PartyIntent
                    {
                        Operation = PartyIntentKind.SetRecruiterCulture,
                        SettlementId = settlementId,
                        StringArgument = culture.StringId ?? string.Empty
                    });
                }
            });
            return false;
        }

        public static bool EscortSelectedPrefix(List<InquiryElement> list)
        {
            if (!IsClient())
            {
                return true;
            }

            Town? town = global::ImprovedGarrisons.Main.GarrisonBehavior?.CurrentTownForSettings;
            MobileParty? target = list != null && list.Count > 0 ? list[0].Identifier as MobileParty : null;
            if (town?.Settlement == null || target == null)
            {
                return false;
            }

            string id = target.StringId ?? string.Empty;
            if (GameInterface.ContainerProvider.TryResolve(out GameInterface.Services.ObjectManager.IObjectManager manager)
                && manager.TryGetId(target, out string coopId))
            {
                id = coopId;
            }

            PartyIntent request = Party(PartyIntentKind.Escort, town);
            request.StringArgument = id;
            IntegrationTransport.SendIntent(request);
            return false;
        }

        public static bool RemoveUpgradeTargetPrefix(Town town, CharacterObject character, ref bool __result)
        {
            if (!IsClient())
            {
                return true;
            }

            __result = false;
            if (town?.Settlement != null && character != null)
            {
                SettingsIntent request = Settings(SettingsIntentKind.RemoveUpgradeTarget, town);
                request.StringArgument = character.StringId ?? string.Empty;
                IntegrationTransport.SendIntent(request);
                __result = true;
            }

            return false;
        }

        public static bool ExecuteAddPrefix(ImprovedGarrisonsTroopItemWidgetVM __instance)
        {
            if (!IsClient())
            {
                return true;
            }

            ForwardTemplateDelta(__instance, 1);
            return false;
        }

        public static bool ExecuteRemovePrefix(ImprovedGarrisonsTroopItemWidgetVM __instance)
        {
            if (!IsClient())
            {
                return true;
            }

            ForwardTemplateDelta(__instance, -1);
            return false;
        }

        private static void ForwardTemplateDelta(ImprovedGarrisonsTroopItemWidgetVM item, int delta)
        {
            Town? town = global::ImprovedGarrisons.Main.GarrisonBehavior?.CurrentTownForSettings;
            CharacterObject? character = item?.CurrentTroop.Character;
            if (town?.Settlement == null || character == null)
            {
                return;
            }

            if (Hero.MainHero?.Clan == null || town.Settlement.OwnerClan == null ||
                !town.Settlement.OwnerClan.Equals(Hero.MainHero.Clan))
            {
                Show("IG: only the settlement owner's clan may change its training template.");
                return;
            }

            SettingsIntent request = Settings(SettingsIntentKind.AdjustTemplateCount, town);
            request.StringArgument = character.StringId ?? string.Empty;
            request.IntegerArgument = delta;
            IntegrationTransport.SendIntent(request);
        }

        public static bool SetTemplatePrefix(List<TroopRosterElement> list)
        {
            if (!IsClient())
            {
                return true;
            }

            Town? town = global::ImprovedGarrisons.Main.GarrisonBehavior?.CurrentTownForSettings;
            if (town?.Settlement == null || list == null)
            {
                return false;
            }

            List<string> entries = new List<string>();
            foreach (TroopRosterElement element in list)
            {
                if (element.Character != null && element.Number > 0)
                {
                    entries.Add((element.Character.StringId ?? string.Empty) + ":" + element.Number.ToString(CultureInfo.InvariantCulture));
                }
            }

            SettingsIntent request = Settings(SettingsIntentKind.SetTemplateFull, town);
            request.ListArgument = string.Join(";", entries.ToArray());
            IntegrationTransport.SendIntent(request);
            return false;
        }

        public static bool ApplySavedTemplatePrefix(TrainingTemplate template)
        {
            if (!IsClient())
            {
                return true;
            }

            Town? town = global::ImprovedGarrisons.Main.GarrisonBehavior?.CurrentTownForSettings;
            Dictionary<string, int>? troops = template?.GetTroopList();
            if (town?.Settlement == null || troops == null)
            {
                return false;
            }

            List<string> entries = new List<string>();
            foreach (KeyValuePair<string, int> troop in troops)
            {
                entries.Add(troop.Key + ":" + troop.Value.ToString(CultureInfo.InvariantCulture));
            }

            SettingsIntent request = Settings(SettingsIntentKind.SetTemplateFull, town);
            request.ListArgument = string.Join(";", entries.ToArray());
            IntegrationTransport.SendIntent(request);
            return false;
        }

        public static bool SetUpgradePathPrefix(List<InquiryElement> list)
        {
            if (!IsClient())
            {
                return true;
            }

            Town? town = global::ImprovedGarrisons.Main.GarrisonBehavior?.CurrentTownForSettings;
            if (town?.Settlement == null || list == null)
            {
                return false;
            }

            int mask = 0;
            foreach (InquiryElement element in list)
            {
                string value = element.Identifier?.ToString() ?? string.Empty;
                if (value.IndexOf("Archer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    mask |= 1;
                }
                else if (value.IndexOf("Infantry", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    mask |= 2;
                }
                else if (value.IndexOf("Caval", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    mask |= 4;
                }
            }

            SettingsIntent request = Settings(SettingsIntentKind.SetUpgradePath, town);
            request.IntegerArgument = mask;
            IntegrationTransport.SendIntent(request);
            return false;
        }

        public static bool PromptTransferPrefix(ManagementSettings __instance, Town fromTown)
        {
            if (!IsClient())
            {
                return true;
            }

            if (fromTown?.Settlement == null)
            {
                return false;
            }

            string sourceId = fromTown.Settlement.StringId;
            __instance.PromptGarrisonSelector(
                new TextObject("{=settings_managementsettings_select}Select a garrison").ToString(),
                new TextObject("{=settings_managementsettings_selectdesc}Select the destination garrison").ToString(),
                1,
                fromTown,
                selected =>
                {
                    Town? target = selected != null && selected.Count > 0 ? selected[0].Identifier as Town : null;
                    if (target?.Settlement == null)
                    {
                        return;
                    }

                    string targetId = target.Settlement.StringId;
                    InformationManager.ShowTextInquiry(new TextInquiryData(
                        "Transfer size",
                        "How many garrison troops should be transferred?",
                        true,
                        true,
                        "Okay",
                        "Cancel",
                        input =>
                        {
                            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                            {
                                IntegrationTransport.SendIntent(new ManagementIntent
                                {
                                    Operation = ManagementIntentKind.TransferDirect,
                                    SettlementId = sourceId,
                                    StringArgument = targetId,
                                    IntegerArgument = count
                                });
                            }
                        },
                        InformationManager.HideInquiry,
                        false,
                        input =>
                        {
                            bool valid = int.TryParse(input, out int count) && count > 0 && count <= 300;
                            return new Tuple<bool, string>(valid, valid ? string.Empty : "Value must be between 1 and 300.");
                        },
                        "30"));
                });
            return false;
        }

        public static bool CopyAllTownsPrefix(Town town)
        {
            return ForwardCopyAll(town, "towns");
        }

        public static bool CopyAllCastlesPrefix(Town town)
        {
            return ForwardCopyAll(town, "castles");
        }

        public static bool CopySpecificPrefix(ManagementSettings __instance, Town town)
        {
            if (!IsClient())
            {
                return true;
            }

            if (town?.Settlement == null)
            {
                return false;
            }

            string sourceId = town.Settlement.StringId;
            __instance.PromptGarrisonSelector("Copy settings", "Select destination garrisons", -1, town, selected =>
            {
                List<string> ids = new List<string>();
                if (selected != null)
                {
                    foreach (InquiryElement element in selected)
                    {
                        if (element.Identifier is Town destination && destination.Settlement != null)
                        {
                            ids.Add(destination.Settlement.StringId);
                        }
                    }
                }

                if (ids.Count > 0)
                {
                    IntegrationTransport.SendIntent(new ManagementIntent
                    {
                        Operation = ManagementIntentKind.CopySpecific,
                        SettlementId = sourceId,
                        ListArgument = string.Join(";", ids.ToArray())
                    });
                }
            });
            return false;
        }

        private static void PatchClientSimulation(Harmony harmony, ref int applied, ref int failed)
        {
            string[][] targets =
            {
                new[] { "ImprovedGarrisons.Behaviours.GarrisonPartyBehavior", "RemovePartyHelper" },
                new[] { "ImprovedGarrisons.Behaviours.GarrisonPartyBehavior", "PartyPartialHourlyAi" },
                new[] { "ImprovedGarrisons.Behaviours.GarrisonPartyBehavior", "PartyHourlyAi" },
                new[] { "ImprovedGarrisons.Behaviours.GarrisonPartyBehavior", "OnPartyEnteredSettlement" },
                new[] { "ImprovedGarrisons.Behaviours.GarrisonPartyBehavior", "OnSettlementOwnerChanged" },
                new[] { "ImprovedGarrisons.Behaviours.GarrisonPartyBehavior", "OnMapEventStarted" },
                new[] { "ImprovedGarrisons.Behaviours.GarrisonPartyBehavior", "OnPartyDestroyed" },
                new[] { "ImprovedGarrisons.Behaviours.GarrisonPartyBehavior", "OnGameStartSetAllIGParties" },
                new[] { "ImprovedGarrisons.Behaviours.GarrisonPartyBehavior", "OnGameStartDeleteAllIGParties" },
                new[] { "ImprovedGarrisons.SaveSystem.GarrisonBehavior", "HourlyEvent" },
                new[] { "ImprovedGarrisons.SaveSystem.GarrisonBehavior", "DailyEvent" },
                new[] { "ImprovedGarrisons.SaveSystem.GarrisonDailyBehavior", "DailyBehavior" }
            };
            foreach (string[] target in targets)
            {
                Patch(harmony, target[0], target[1], nameof(SkipOnClientPrefix), null, ref applied, ref failed);
            }
        }

        private static void PatchHeadlessServer(Harmony harmony, ref int applied, ref int failed)
        {
            string[][] targets =
            {
                new[] { "ImprovedGarrisons.ImprovedGarrisonsUI.UIManager", "TryUpdateImprovedGarrisonsUI" },
                new[] { "ImprovedGarrisons.ImprovedGarrisonsUI.UIManager", "CreateCascadeMenuOnMousePointer" },
                new[] { "ImprovedGarrisons.ImprovedGarrisonsUI.UIManager", "StartTutorial" },
                new[] { "ImprovedGarrisons.ImprovedGarrisonsUI.UIManager", "CloseCascadeMenu" },
                new[] { "ImprovedGarrisons.Ribbons.RibbonManagerGauntlet", "UpdateRibbons" },
                new[] { "ImprovedGarrisons.Ribbons.RibbonManagerGauntlet", "OpenAllRibbonsForGarrison" },
                new[] { "ImprovedGarrisons.Ribbons.RibbonManagerGauntlet", "CloseAllRibbons" },
                new[] { "ImprovedGarrisons.Main", "OnKeyPress" },
                new[] { "ImprovedGarrisons.AI.AIManagers.PartyManager", "TrackAllImprovedGarrisonparties" }
            };
            foreach (string[] target in targets)
            {
                Patch(harmony, target[0], target[1], nameof(SkipOnServerPrefix), null, ref applied, ref failed);
            }

            Patch(harmony, "ImprovedGarrisons.ImprovedGarrisonsUI.UIManager", "TryInitializeImprovedGarrisonsUI", nameof(SkipOnServerFalsePrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(GarrisonBehavior), "OnGameOpen", nameof(ServerOnGameOpenPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(GarrisonBehavior), "GetTownSettings", nameof(ServerTownSettingsPrefix), null, ref applied, ref failed);
            Patch(harmony, "ImprovedGarrisons.Main", "OpenConfigurationScreen", nameof(BlockConfigScreenPrefix), null, ref applied, ref failed);
        }

        private static void PatchClientActions(Harmony harmony, ref int applied, ref int failed)
        {
            Dictionary<Type, string[]> settings = new Dictionary<Type, string[]>
            {
                [typeof(MobileGarrisonSettings)] = new[] { "SetReturnPercentage", "SetAutoGarrisonThreshold", "SetAutoGarrisonSize", "TogglePrisonerSell", "ToggleAutoGuards", "ToggleAutoGuardDefend", "TogglePrisonerRecruit", "ToggleUpgrade", "ToggleReplenish", "ToggleDestroyHideout", "ToggleHorseBuy" },
                [typeof(RecruitmentSettings)] = new[] { "SetRecruiterAmountToRecruit", "SetRecruitmentThreshold", "ToggleRecruitOnlyElite", "TogglePrisonerRecruitmentAboveThreshold", "TogglePrisonerRecruitment", "ToggleVanillaRecruitment", "ToggleRegionRecruitment", "ToggleRecruiterOnlyElites", "ToggleRecruiterBuyHorses", "TogglePrisonerRecruitmentIgnoresTemplate", "ToggleRecruiterAutoSpawn" },
                [typeof(TrainingSettings)] = new[] { "SetTownMaxUpgradeTier", "ToggleVanillaTraining", "ToggleTraining", "ToggleAutoSpawn", "ToggleFollowTemplate", "ToggleRemoveNonTemplateTroops" }
            };
            foreach (KeyValuePair<Type, string[]> group in settings)
            {
                foreach (string method in group.Value)
                {
                    Patch(harmony, group.Key, method, nameof(ForwardSettingPrefix), null, ref applied, ref failed);
                }
            }

            Patch(harmony, typeof(MobileGarrisonSettings), "PromptCreateMobileGarrison", nameof(CreateGuardsPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(MobileGarrisonSettings), "OrderMobileGarrisonToPatrol", nameof(OrderPatrolPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(MobileGarrisonSettings), "OrderMobileGarrisonReturn", nameof(OrderReturnPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(MobileGarrisonSettings), "OrderMobileGarrisonAttackOrDefend", nameof(OrderAttackDefendPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(MobileGarrisonSettings), "Inquirydata_MobileGarrisonEscort", nameof(EscortSelectedPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(RecruitmentSettings), "PromptSelectorForRecruiter", nameof(CreateRecruiterPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(RecruitmentSettings), "ReturnRecruiter", nameof(ReturnRecruiterPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(RecruitmentSettings), "PromptChangeRecruitmentCulture", nameof(ChangeRecruiterCulturePrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(TrainingSettings), "RemoveUpgradeTarget", nameof(RemoveUpgradeTargetPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(TrainingSettings), "SetSpecifiedUpgradeTargets", nameof(SetTemplatePrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(TrainingSettings), "Inquirydata_SetUpgradePath", nameof(SetUpgradePathPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(ImprovedGarrisonsTroopItemWidgetVM), "ExecuteAdd", nameof(ExecuteAddPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(ImprovedGarrisonsTroopItemWidgetVM), "ExecuteRemove", nameof(ExecuteRemovePrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(ManagementSettings), "PromptTransfer", nameof(PromptTransferPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(ManagementSettings), "PromptCopyToSpecificTowns", nameof(CopySpecificPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(ManagementSettings), "PromptCopyToAllTowns", nameof(CopyAllTownsPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(ManagementSettings), "PromptCopyToAllCastles", nameof(CopyAllCastlesPrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(TemplateManager), "ApplyTemplate", nameof(ApplySavedTemplatePrefix), null, ref applied, ref failed);
            Patch(harmony, typeof(GarrisonBehavior), "set_CurrentTownForSettings", null, nameof(CurrentTownPostfix), ref applied, ref failed);

            Patch(harmony, "ImprovedGarrisons.SaveSystem.Configuration.ConfigManager", "ReadConfigForCurrentGame", nameof(SkipConfigIoOnClientPrefix), null, ref applied, ref failed);
            PatchAll(harmony, "ImprovedGarrisons.SaveSystem.Configuration.ConfigManager", "CreateAndUpdateConfig", nameof(SkipConfigIoOnClientPrefix), ref applied, ref failed);
            Patch(harmony, "ImprovedGarrisons.SaveSystem.Configuration.ConfigManager", "CreateAndUpdateConfigForCurrentGame", nameof(SkipConfigIoOnClientPrefix), null, ref applied, ref failed);

            string behavior = "ImprovedGarrisons.Behaviours.GarrisonPartyBehavior";
            Patch(harmony, behavior, "Conversation_improvedgarrison_mobilegarrison_return_on_consequence", nameof(ConversationPatches.GuardReturnPrefix), null, ref applied, ref failed);
            Patch(harmony, behavior, "Conversation_improvedgarrison_mobilegarrison_patrol_on_consequence", nameof(ConversationPatches.GuardPatrolPrefix), null, ref applied, ref failed);
            Patch(harmony, behavior, "Conversation_improvedgarrison_mobilegarrison_escort_on_consequence", nameof(ConversationPatches.GuardEscortPrefix), null, ref applied, ref failed);
            Patch(harmony, behavior, "Conversation_improvedgarrison_mobilegarrison_fortify_on_consequence", nameof(ConversationPatches.GuardFortifyPrefix), null, ref applied, ref failed);
            Patch(harmony, behavior, "Conversation_improvedgarrison_recruiter_return_on_consequence", nameof(ConversationPatches.RecruiterReturnPrefix), null, ref applied, ref failed);
            Patch(harmony, behavior, "Conversation_improvedgarrison_recruiter_changeCulture_on_consequence", nameof(ConversationPatches.RecruiterChangeCulturePrefix), null, ref applied, ref failed);
        }

        private static void PatchIdentity(Harmony harmony, ref int applied, ref int failed)
        {
            Patch(harmony, typeof(MobileGarrisonManager), "IsMobileGarrisonParty", null, nameof(PartyIdentityPatches.IsGuardPostfix), ref applied, ref failed);
            Patch(harmony, typeof(MobileGarrisonManager), "GetMobileGarrisonHome", null, nameof(PartyIdentityPatches.GuardHomePostfix), ref applied, ref failed);
            Patch(harmony, typeof(MobileGarrison), "GetStatusText", null, nameof(PartyIdentityPatches.GuardStatusPostfix), ref applied, ref failed);
            Patch(harmony, typeof(GarrisonRecruiterPartyManager), "IsRecruiterParty", null, nameof(PartyIdentityPatches.IsRecruiterPostfix), ref applied, ref failed);
            Patch(harmony, typeof(GarrisonRecruiterPartyManager), "GetRecruiterPartyHome", null, nameof(PartyIdentityPatches.RecruiterHomePostfix), ref applied, ref failed);
            Patch(harmony, typeof(GarrisonRecruiter), "GetStatusText", null, nameof(PartyIdentityPatches.RecruiterStatusPostfix), ref applied, ref failed);
            Patch(harmony, typeof(TransferPartyManager), "IsTransferParty", null, nameof(PartyIdentityPatches.IsTransferPostfix), ref applied, ref failed);
            Patch(harmony, typeof(TransferPartyManager), "GetTransferPartyHome", null, nameof(PartyIdentityPatches.TransferHomePostfix), ref applied, ref failed);
            Patch(harmony, typeof(TransferPartyManager), "CreateNewTransferParty", null, nameof(PartyIdentityPatches.TransferCreatedPostfix), ref applied, ref failed);
            Patch(harmony, typeof(VillageRecruitPartyManager), "IsImprovedGarrisonVillageRecruitParty", null, nameof(PartyIdentityPatches.IsVillageRecruitPostfix), ref applied, ref failed);
            Patch(harmony, typeof(VillageRecruitPartyManager), "GetVillageFromMobileParty", null, nameof(PartyIdentityPatches.VillageHomePostfix), ref applied, ref failed);
            Patch(harmony, typeof(VillageRecruitPartyManager), "InitializeVillageRecruitParty", null, nameof(PartyIdentityPatches.VillageRecruitCreatedPostfix), ref applied, ref failed);
        }

        private static bool ForwardTownAction(PartyIntentKind operation, Town town, bool booleanArgument = false)
        {
            if (!IsClient())
            {
                return true;
            }

            if (town?.Settlement != null)
            {
                PartyIntent request = Party(operation, town);
                request.BooleanArgument = booleanArgument;
                IntegrationTransport.SendIntent(request);
            }
            else
            {
                Show("IG: select a settlement first.");
            }

            return false;
        }

        private static bool ForwardCopyAll(Town town, string mode)
        {
            if (!IsClient())
            {
                return true;
            }

            if (town?.Settlement != null)
            {
                ManagementIntent request = new ManagementIntent
                {
                    Operation = ManagementIntentKind.CopyAll,
                    SettlementId = town.Settlement.StringId ?? string.Empty
                };
                request.StringArgument = mode;
                IntegrationTransport.SendIntent(request);
            }

            return false;
        }

        private static PartyIntent Party(PartyIntentKind operation, Town town)
        {
            return new PartyIntent { Operation = operation, SettlementId = town.Settlement.StringId ?? string.Empty };
        }

        private static SettingsIntent Settings(SettingsIntentKind operation, Town town)
        {
            return new SettingsIntent { Operation = operation, SettlementId = town.Settlement.StringId ?? string.Empty };
        }

        private static bool TryGetSettingOperation(MethodBase method, out SettingsIntentKind operation)
        {
            operation = default;
            Type? declaringType = method.DeclaringType;
            bool supportedType = declaringType == typeof(MobileGarrisonSettings) ||
                declaringType == typeof(RecruitmentSettings) ||
                declaringType == typeof(TrainingSettings);
            return supportedType && Enum.TryParse(method.Name, false, out operation) &&
                operation <= SettingsIntentKind.ToggleRemoveNonTemplateTroops;
        }

        private static bool IsClient()
        {
            return !IntegrationRoleRouter.ShouldExecuteLocally(IntegrationRuntime.CoopActive, IntegrationRuntime.IsServer);
        }

        private static void Show(string text)
        {
            InformationManager.DisplayMessage(new InformationMessage(text));
        }

        private static void Patch(Harmony harmony, Type type, string methodName, string? prefixName, string? postfixName, ref int applied, ref int failed)
        {
            Patch(harmony, type.FullName ?? type.Name, methodName, prefixName, postfixName, ref applied, ref failed);
        }

        private static void PatchAll(Harmony harmony, string typeName, string methodName, string prefixName, ref int applied, ref int failed)
        {
            Type? type = AccessTools.TypeByName(typeName);
            MethodInfo? prefix = AccessTools.Method(typeof(ClientServerPatches), prefixName);
            if (type == null || prefix == null)
            {
                failed++;
                return;
            }

            int found = 0;
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                found++;
                try
                {
                    harmony.Patch(method, new HarmonyMethod(prefix));
                    applied++;
                }
                catch (Exception exception)
                {
                    failed++;
                    IntegrationLog.Warning("patch failed: " + typeName + "." + methodName + ": " + exception.GetBaseException().Message);
                }
            }

            if (found == 0)
            {
                failed++;
            }
        }

        private static void Patch(Harmony harmony, string typeName, string methodName, string? prefixName, string? postfixName, ref int applied, ref int failed)
        {
            try
            {
                Type? targetType = AccessTools.TypeByName(typeName);
                MethodBase? target = targetType == null ? null : AccessTools.Method(targetType, methodName);
                MethodInfo? prefix = prefixName == null
                    ? null
                    : AccessTools.Method(typeof(ClientServerPatches), prefixName) ?? AccessTools.Method(typeof(ConversationPatches), prefixName);
                MethodInfo? postfix = postfixName == null
                    ? null
                    : AccessTools.Method(typeof(PartyIdentityPatches), postfixName) ?? AccessTools.Method(typeof(ClientServerPatches), postfixName);
                if (target == null || (prefixName != null && prefix == null) || (postfixName != null && postfix == null))
                {
                    failed++;
                    IntegrationLog.Warning("patch target unavailable: " + typeName + "." + methodName);
                    return;
                }

                harmony.Patch(target, prefix == null ? null : new HarmonyMethod(prefix), postfix == null ? null : new HarmonyMethod(postfix));
                applied++;
            }
            catch (Exception exception)
            {
                failed++;
                IntegrationLog.Warning("patch failed: " + typeName + "." + methodName + ": " + exception.GetBaseException().Message);
            }
        }
    }
}
