using System;
using System.Collections.Generic;
using System.Reflection;
using Common;
using GameInterface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.AI.Orders.PartyOrder;
using ImprovedGarrisons.CoopIntegration.Core;
using ImprovedGarrisons.CoopIntegration.Persistence;
using ImprovedGarrisons.CoopIntegration.Protocol;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace ImprovedGarrisons.CoopIntegration.Runtime
{
    internal static class ServerActionDispatcher
    {
        private const int MaximumTextArgumentLength = 32_768;
        private const int MaximumListItems = 256;
        private const int DeduplicationCapacity = 512;
        private static readonly HashSet<string> Completed = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Queue<string> CompletedOrder = new Queue<string>();

        public static ActionOutcome Dispatch(ServerAction request, NetPeer? peer)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!ValidateRequest(request, out string validationError))
            {
                IntegrationLog.Warning("rejected invalid intent " + request.Kind + ": " + validationError);
                return Reject(request, "invalid", validationError);
            }

            if (Completed.Contains(request.OperationId))
            {
                IntegrationLog.Warning("ignored duplicate intent " + request.Kind + " " + request.OperationId);
                return Reject(request, "duplicate", "IG: that action was already processed by the server.");
            }

            ActionOutcome result;
            try
            {
                result = ExecuteAuthorized(request, peer);
            }
            catch (Exception exception)
            {
                IntegrationLog.Error("intent " + request.Kind + " failed: " + exception);
                result = Reject(request, "server_error", "IG: the server failed to run that action (" + exception.GetBaseException().Message + ").");
            }

            CacheCompleted(request.OperationId);
            IntegrationLog.Information("intent completed: " + request.Kind + " " + result.Code + " " + request.OperationId);
            return result;
        }

        private static ActionOutcome ExecuteAuthorized(ServerAction request, NetPeer? peer)
        {
            if (peer == null)
            {
                return Reject(request, "unknown_peer", "IG: the server could not identify the requesting player.");
            }

            if (global::ImprovedGarrisons.Main.PartyManagement == null || global::ImprovedGarrisons.Main.GarrisonBehavior == null)
            {
                return Reject(request, "not_ready", "IG: Improved Garrisons is not initialized on the server.");
            }

            Settlement? settlement = MBObjectManager.Instance?.GetObject<Settlement>(request.SettlementId);
            Town? town = settlement?.Town;
            if (settlement == null || town == null)
            {
                return Reject(request, "settlement_missing", "IG: the server could not find that settlement.");
            }

            string peerClanId = ResolvePeerClanId(peer);
            string ownerClanId = settlement.OwnerClan?.StringId ?? string.Empty;
            if (!ActionAuthorization.CanMutateSettlement(peerClanId, ownerClanId))
            {
                return Reject(request, "forbidden", "IG: only the settlement owner's clan may perform that action.");
            }

            ServerClanRegistry.Record(settlement.OwnerClan);
            IntegrationLog.Information("authorized " + request.Kind + " for " + settlement.StringId + " from clan " + peerClanId);
            switch (request.Kind)
            {
                case "CreateGuards":
                    return CreateGuards(request, settlement, town);
                case "CreateRecruiter":
                    return CreateRecruiter(request, settlement, town);
                case "OrderPatrol":
                    return OrderPatrol(request, settlement);
                case "OrderReturn":
                    return OrderReturn(request, settlement);
                case "ReturnRecruiter":
                    return ReturnRecruiter(request, settlement);
                case "SetRecruiterCulture":
                    return SetRecruiterCulture(request, settlement, town);
                case "ApplySetting":
                    return ApplySetting(request, town);
                case "RemoveUpgradeTarget":
                    return RemoveUpgradeTarget(request, town);
                case "SetTemplateFull":
                    return SetTemplate(request, town);
                case "AdjustTemplateCount":
                    return AdjustTemplateCount(request, town);
                case "SetUpgradePath":
                    return SetUpgradePath(request, town);
                case "TransferDirect":
                    return TransferDirect(request, settlement, town);
                case "CopyAll":
                    return CopyAll(request, town);
                case "CopySpecific":
                    return CopySpecific(request, town, peerClanId);
                case "Escort":
                    return Escort(request, settlement);
                case "EscortPlayer":
                    return EscortPlayer(request, peer, settlement);
                case "Fortify":
                    return Fortify(request, settlement);
                case "SyncTown":
                    SettingsStateStore.MarkDirty();
                    return Accept(request, "synced", string.Empty);
                default:
                    return Reject(request, "unsupported", "IG: action '" + request.Kind + "' is not supported by this integration build.");
            }
        }

        private static ActionOutcome CreateGuards(ServerAction request, Settlement settlement, Town town)
        {
            if (town.IsUnderSiege)
            {
                return Reject(request, "under_siege", "IG: the guard party cannot leave while the settlement is under siege.");
            }

            if (town.GarrisonParty?.MemberRoster == null || town.GarrisonParty.MemberRoster.TotalManCount <= 1)
            {
                return Reject(request, "garrison_small", "IG: the garrison needs at least two troops before a guard can be created.");
            }

            MobileGarrison? existing = global::ImprovedGarrisons.Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(settlement);
            if (existing != null && existing.IsValidAndActive())
            {
                return Reject(request, "already_exists", "IG: this settlement already has a guard party.");
            }

            int count = request.IntegerArgument <= 0 ? 30 : Math.Min(request.IntegerArgument, 300);
            count = Math.Min(count, town.GarrisonParty.MemberRoster.TotalManCount - 1);
            PartyBase? created = global::ImprovedGarrisons.Main.PartyManagement.mobileGarrisonManagement.CreateMobileGarrisonWithUnits(settlement, count);
            if (created?.MobileParty == null)
            {
                return Reject(request, "creation_failed", "IG: the server could not create the guard party.");
            }

            PartyManifestStore.Capture("guard", created.MobileParty, settlement, "OrderPatrol");
            return Accept(request, "created", "IG: created a guard party of " + count + " troops.", created.MobileParty.StringId ?? string.Empty);
        }

        private static ActionOutcome CreateRecruiter(ServerAction request, Settlement settlement, Town town)
        {
            if (town.IsUnderSiege)
            {
                return Reject(request, "under_siege", "IG: the recruiter cannot leave while the settlement is under siege.");
            }

            if (town.GarrisonParty?.MemberRoster == null || town.GarrisonParty.MemberRoster.TotalManCount == 0)
            {
                return Reject(request, "garrison_empty", "IG: the garrison needs a troop before a recruiter can be created.");
            }

            var manager = global::ImprovedGarrisons.Main.PartyManagement.garrisonRecruiterPartyManagement;
            if (manager.SettlementHasARecruiter(settlement))
            {
                return Reject(request, "already_exists", "IG: this settlement already has a recruiter.");
            }

            PartyBase? created = manager.CreateGarrisonRecruiterParty(settlement, settlement, true);
            if (created?.MobileParty == null)
            {
                return Reject(request, "creation_failed", "IG: the server could not create the recruiter.");
            }

            GarrisonSettings? settings = global::ImprovedGarrisons.Main.GarrisonBehavior.GetTownSettings(town);
            if (settings != null)
            {
                settings.RecruiterCultureToRecruit = string.IsNullOrWhiteSpace(request.StringArgument) ? null : request.StringArgument;
                if (request.IntegerArgument > 0)
                {
                    settings.RecruiterRecruitAmount = Math.Min(request.IntegerArgument, 150);
                }
            }

            SettingsStateStore.MarkDirty();
            PartyManifestStore.Capture("recruiter", created.MobileParty, settlement, string.Empty);
            return Accept(request, "created", "IG: created a recruiter party.", created.MobileParty.StringId ?? string.Empty);
        }

        private static ActionOutcome OrderPatrol(ServerAction request, Settlement settlement)
        {
            MobileGarrison? guard = global::ImprovedGarrisons.Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(settlement);
            if (guard == null)
            {
                return Reject(request, "not_found", "IG: this settlement has no guard party.");
            }

            guard.GiveAndExecuteOrder(new OrderPatrol(settlement));
            PartyManifestStore.Capture("guard", guard.getMobileParty(), settlement, "OrderPatrol");
            return Accept(request, "ordered", "IG: the guard party is now patrolling.");
        }

        private static ActionOutcome OrderReturn(ServerAction request, Settlement settlement)
        {
            MobileGarrison? guard = global::ImprovedGarrisons.Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(settlement);
            if (guard == null)
            {
                return Reject(request, "not_found", "IG: this settlement has no guard party.");
            }

            if (request.BooleanArgument)
            {
                GarrisonSettings? settings = global::ImprovedGarrisons.Main.GarrisonBehavior.GetTownSettings(settlement.Town);
                if (settings != null)
                {
                    settings.GuardsAutoSpawn = false;
                    SettingsStateStore.MarkDirty();
                }
            }

            guard.SetReturnMode();
            PartyManifestStore.Capture("guard", guard.getMobileParty(), settlement, "OrderMergeGarrison");
            return Accept(request, "ordered", "IG: the guard party is returning to its garrison.");
        }

        private static ActionOutcome ReturnRecruiter(ServerAction request, Settlement settlement)
        {
            GarrisonRecruiter? recruiter = global::ImprovedGarrisons.Main.PartyManagement.garrisonRecruiterPartyManagement.GetRecruiterOfSettlement(settlement);
            if (recruiter == null)
            {
                return Reject(request, "not_found", "IG: this settlement has no active recruiter.");
            }

            GarrisonSettings? settings = global::ImprovedGarrisons.Main.GarrisonBehavior.GetTownSettings(settlement.Town);
            if (settings != null)
            {
                settings.RecruiterAutoSpawn = false;
                SettingsStateStore.MarkDirty();
            }

            recruiter.SetReturnMode();
            return Accept(request, "ordered", "IG: the recruiter is returning to its garrison.");
        }

        private static ActionOutcome SetRecruiterCulture(ServerAction request, Settlement settlement, Town town)
        {
            GarrisonSettings? settings = global::ImprovedGarrisons.Main.GarrisonBehavior.GetTownSettings(town);
            if (settings == null)
            {
                return Reject(request, "settings_missing", "IG: no settings exist for that settlement.");
            }

            settings.RecruiterCultureToRecruit = string.IsNullOrWhiteSpace(request.StringArgument) ? null : request.StringArgument;
            global::ImprovedGarrisons.Main.PartyManagement.garrisonRecruiterPartyManagement.GetRecruiterOfSettlement(settlement)?.ResetTradeTarget();
            SettingsStateStore.MarkDirty();
            return Accept(request, "updated", "IG: recruiter culture updated.");
        }

        private static ActionOutcome ApplySetting(ServerAction request, Town town)
        {
            SettingsIntentKind operation = (SettingsIntentKind)request.SettingOperation;
            switch (operation)
            {
                case SettingsIntentKind.SetReturnPercentage:
                    MobileGarrisonSettings.Instance.SetReturnPercentage(town, request.FloatArgument);
                    break;
                case SettingsIntentKind.SetAutoGarrisonThreshold:
                    MobileGarrisonSettings.Instance.SetAutoGarrisonThreshold(town, request.IntegerArgument);
                    break;
                case SettingsIntentKind.SetAutoGarrisonSize:
                    MobileGarrisonSettings.Instance.SetAutoGarrisonSize(town, request.IntegerArgument);
                    break;
                case SettingsIntentKind.TogglePrisonerSell:
                    MobileGarrisonSettings.Instance.TogglePrisonerSell(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleAutoGuards:
                    MobileGarrisonSettings.Instance.ToggleAutoGuards(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleAutoGuardDefend:
                    MobileGarrisonSettings.Instance.ToggleAutoGuardDefend(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.TogglePrisonerRecruit:
                    MobileGarrisonSettings.Instance.TogglePrisonerRecruit(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleUpgrade:
                    MobileGarrisonSettings.Instance.ToggleUpgrade(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleReplenish:
                    MobileGarrisonSettings.Instance.ToggleReplenish(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleDestroyHideout:
                    MobileGarrisonSettings.Instance.ToggleDestroyHideout(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleHorseBuy:
                    MobileGarrisonSettings.Instance.ToggleHorseBuy(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.SetRecruiterAmountToRecruit:
                    RecruitmentSettings.Instance.SetRecruiterAmountToRecruit(town, request.IntegerArgument);
                    break;
                case SettingsIntentKind.SetRecruitmentThreshold:
                    RecruitmentSettings.Instance.SetRecruitmentThreshold(town, request.IntegerArgument);
                    break;
                case SettingsIntentKind.ToggleRecruitOnlyElite:
                    RecruitmentSettings.Instance.ToggleRecruitOnlyElite(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.TogglePrisonerRecruitmentAboveThreshold:
                    RecruitmentSettings.Instance.TogglePrisonerRecruitmentAboveThreshold(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.TogglePrisonerRecruitment:
                    RecruitmentSettings.Instance.TogglePrisonerRecruitment(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleVanillaRecruitment:
                    RecruitmentSettings.Instance.ToggleVanillaRecruitment(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleRegionRecruitment:
                    RecruitmentSettings.Instance.ToggleRegionRecruitment(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleRecruiterOnlyElites:
                    RecruitmentSettings.Instance.ToggleRecruiterOnlyElites(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleRecruiterBuyHorses:
                    RecruitmentSettings.Instance.ToggleRecruiterBuyHorses(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.TogglePrisonerRecruitmentIgnoresTemplate:
                    RecruitmentSettings.Instance.TogglePrisonerRecruitmentIgnoresTemplate(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleRecruiterAutoSpawn:
                    RecruitmentSettings.Instance.ToggleRecruiterAutoSpawn(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.SetTownMaxUpgradeTier:
                    TrainingSettings.Instance.SetTownMaxUpgradeTier(town, request.IntegerArgument);
                    break;
                case SettingsIntentKind.ToggleVanillaTraining:
                    TrainingSettings.Instance.ToggleVanillaTraining(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleTraining:
                    TrainingSettings.Instance.ToggleTraining(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleAutoSpawn:
                    TrainingSettings.Instance.ToggleAutoSpawn(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleFollowTemplate:
                    TrainingSettings.Instance.ToggleFollowTemplate(town, request.BooleanArgument);
                    break;
                case SettingsIntentKind.ToggleRemoveNonTemplateTroops:
                    TrainingSettings.Instance.ToggleRemoveNonTemplateTroops(town, request.BooleanArgument);
                    break;
                default:
                    return Reject(request, "unsupported", "IG: that settings operation is not authorized.");
            }

            SettingsStateStore.MarkDirty();
            return Accept(request, "updated", string.Empty);
        }

        private static ActionOutcome RemoveUpgradeTarget(ServerAction request, Town town)
        {
            CharacterObject? character = MBObjectManager.Instance?.GetObject<CharacterObject>(request.StringArgument);
            if (character == null)
            {
                return Reject(request, "troop_missing", "IG: the server could not find that troop type.");
            }

            TrainingSettings.Instance.RemoveUpgradeTarget(town, character);
            SettingsStateStore.MarkDirty();
            return Accept(request, "updated", "IG: upgrade target removed.");
        }

        private static ActionOutcome AdjustTemplateCount(ServerAction request, Town town)
        {
            if (request.IntegerArgument != -1 && request.IntegerArgument != 1)
            {
                return Reject(request, "invalid_count_delta", "IG: template adjustments must add or remove one troop.");
            }

            CharacterObject? character = MBObjectManager.Instance?.GetObject<CharacterObject>(request.StringArgument);
            if (character == null)
            {
                return Reject(request, "troop_missing", "IG: the server could not find that troop type.");
            }

            GarrisonSettings? settings = global::ImprovedGarrisons.Main.GarrisonBehavior.GetTownSettings(town);
            if (settings?.Template == null)
            {
                return Reject(request, "settings_missing", "IG: no training template exists for that settlement.");
            }

            int current = Math.Max(0, settings.Template.GetAmountForTemplateTroop(character));
            int adjusted = request.IntegerArgument > 0
                ? (current >= 10_000 ? 10_000 : current + 1)
                : Math.Max(0, current - 1);
            if (adjusted == 0)
            {
                TrainingSettings.Instance.RemoveUpgradeTarget(town, character);
            }
            else
            {
                settings.Template.AddOrUpdateCharacter(character, adjusted);
            }

            SettingsStateStore.MarkDirty();
            return Accept(request, "updated", "IG: training template count updated.");
        }

        private static ActionOutcome SetUpgradePath(ServerAction request, Town town)
        {
            GarrisonSettings? settings = global::ImprovedGarrisons.Main.GarrisonBehavior.GetTownSettings(town);
            if (settings == null)
            {
                return Reject(request, "settings_missing", "IG: no settings exist for that settlement.");
            }

            settings.TroopsToUpgradeTo = new[]
            {
                (request.IntegerArgument & 1) != 0,
                (request.IntegerArgument & 2) != 0,
                (request.IntegerArgument & 4) != 0
            };
            SettingsStateStore.MarkDirty();
            return Accept(request, "updated", "IG: upgrade paths updated.");
        }

        private static ActionOutcome SetTemplate(ServerAction request, Town town)
        {
            GarrisonSettings? settings = global::ImprovedGarrisons.Main.GarrisonBehavior.GetTownSettings(town);
            if (settings?.Template == null)
            {
                return Reject(request, "settings_missing", "IG: no training template exists for that settlement.");
            }

            settings.Template.Clear();
            int added = 0;
            foreach (string item in SplitList(request.ListArgument))
            {
                string[] fields = item.Split(':');
                if (fields.Length != 2 || !int.TryParse(fields[1], out int count) || count <= 0)
                {
                    continue;
                }

                CharacterObject? character = MBObjectManager.Instance?.GetObject<CharacterObject>(fields[0]);
                if (character != null)
                {
                    settings.Template.AddOrUpdateCharacter(character, Math.Min(count, 1_000));
                    added++;
                }
            }

            SettingsStateStore.MarkDirty();
            return Accept(request, "updated", "IG: training template updated with " + added + " troop types.");
        }

        private static ActionOutcome TransferDirect(ServerAction request, Settlement source, Town sourceTown)
        {
            Settlement? target = MBObjectManager.Instance?.GetObject<Settlement>(request.StringArgument);
            if (target?.Town == null || target.OwnerClan != source.OwnerClan)
            {
                return Reject(request, "target_forbidden", "IG: the transfer target is missing or belongs to another clan.");
            }

            int available = Math.Max(0, (sourceTown.GarrisonParty?.MemberRoster.TotalManCount ?? 0) - 1);
            int count = Math.Min(Math.Min(Math.Max(request.IntegerArgument, 1), 300), available);
            if (count <= 0)
            {
                return Reject(request, "garrison_small", "IG: the source garrison is too small for a transfer.");
            }

            PartyBase? created = global::ImprovedGarrisons.Main.PartyManagement.transferPartyManagement.CreateNewTransferParty(source, target);
            if (created?.MobileParty == null)
            {
                return Reject(request, "creation_failed", "IG: the server could not create the transfer party.");
            }

            List<Tuple<CharacterObject, int>> troops = global::ImprovedGarrisons.Main.GarrisonBehavior.GetLowestTierUnitsByAmount(count, sourceTown);
            if (troops == null || troops.Count == 0)
            {
                DestroyPartyAction.Apply(null, created.MobileParty);
                return Reject(request, "transfer_empty", "IG: no transferable troops were available.");
            }

            global::ImprovedGarrisons.Main.GarrisonPartyBehavior.TransferTroopsFromPartyToParty(sourceTown.GarrisonParty, troops, created);
            PartyManifestStore.Capture("transfer", created.MobileParty, source, target.StringId ?? string.Empty);
            return Accept(request, "transfer_committed", "IG: transfer party dispatched with " + count + " troops.", created.MobileParty.StringId ?? string.Empty);
        }

        private static ActionOutcome Escort(ServerAction request, Settlement settlement)
        {
            MobileGarrison? guard = global::ImprovedGarrisons.Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(settlement);
            if (guard == null)
            {
                return Reject(request, "not_found", "IG: this settlement has no guard party.");
            }

            MobileParty? target = null;
            if (ContainerProvider.TryResolve(out IObjectManager objectManager))
            {
                objectManager.TryGetObject(request.StringArgument, out target);
            }

            target = target ?? MBObjectManager.Instance?.GetObject<MobileParty>(request.StringArgument);
            if (target == null || target.ActualClan != settlement.OwnerClan || ReferenceEquals(target, guard.getMobileParty()))
            {
                return Reject(request, "target_forbidden", "IG: the escort target is missing or belongs to another clan.");
            }

            guard.GiveAndExecuteOrder(new OrderEscort(target));
            PartyManifestStore.Capture("guard", guard.getMobileParty(), settlement, "OrderEscort:" + request.StringArgument);
            return Accept(request, "ordered", "IG: the guard party is now escorting the selected clan party.");
        }

        private static ActionOutcome EscortPlayer(ServerAction request, NetPeer peer, Settlement settlement)
        {
            if (!ContainerProvider.TryResolve(out IPlayerManager playerManager)
                || !ContainerProvider.TryResolve(out IObjectManager objectManager)
                || !playerManager.TryGetPlayer(peer, out Player player)
                || string.IsNullOrWhiteSpace(player.MobilePartyId)
                || !objectManager.TryGetObject(player.MobilePartyId, out MobileParty party))
            {
                return Reject(request, "target_missing", "IG: the server could not resolve your party for the escort order.");
            }

            request.StringArgument = player.MobilePartyId;
            return Escort(request, settlement);
        }

        private static ActionOutcome Fortify(ServerAction request, Settlement settlement)
        {
            Settlement? target = MBObjectManager.Instance?.GetObject<Settlement>(request.StringArgument);
            if (target?.Town == null || target.OwnerClan != settlement.OwnerClan)
            {
                return Reject(request, "target_forbidden", "IG: the fortification target is missing or belongs to another clan.");
            }

            MobileGarrison? guard = global::ImprovedGarrisons.Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(settlement);
            if (guard == null || !guard.IsValidAndActive())
            {
                return Reject(request, "not_found", "IG: this settlement has no active guard party.");
            }

            guard.getMobileParty().SetCustomHomeSettlement(target);
            guard.SetFortifyMode(target);
            PartyManifestStore.Capture("guard", guard.getMobileParty(), settlement, "fortify:" + (target.StringId ?? string.Empty));
            return Accept(request, "ordered", "IG: the guard party is reinforcing " + target.Name + ".");
        }

        private static ActionOutcome CopyAll(ServerAction request, Town source)
        {
            string methodName = string.Equals(request.StringArgument, "castles", StringComparison.Ordinal) ? "CopyToAllCastles" : "CopyToAllTowns";
            MethodInfo? method = typeof(ManagementSettings).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                return Reject(request, "unsupported", "IG: the copy operation is unavailable on the server.");
            }

            method.Invoke(ManagementSettings.Instance, new object[] { source });
            SettingsStateStore.MarkDirty();
            return Accept(request, "updated", "IG: settings copied to all eligible garrisons.");
        }

        private static ActionOutcome CopySpecific(ServerAction request, Town source, string clanId)
        {
            MethodInfo? method = typeof(ManagementSettings).GetMethod("CopyGarrisonSettings", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                return Reject(request, "unsupported", "IG: the copy operation is unavailable on the server.");
            }

            int copied = 0;
            foreach (string id in SplitList(!string.IsNullOrEmpty(request.ListArgument) ? request.ListArgument : request.StringArgument))
            {
                Settlement? destination = MBObjectManager.Instance?.GetObject<Settlement>(id);
                Town? destinationTown = destination?.Town;
                if (destinationTown != null && ActionAuthorization.CanMutateSettlement(clanId, destination?.OwnerClan?.StringId ?? string.Empty))
                {
                    method.Invoke(ManagementSettings.Instance, new object[] { source, destinationTown });
                    copied++;
                }
            }

            SettingsStateStore.MarkDirty();
            return Accept(request, "updated", "IG: settings copied to " + copied + " garrisons.");
        }

        private static string ResolvePeerClanId(NetPeer peer)
        {
            if (!ContainerProvider.TryResolve(out IPlayerManager playerManager)
                || !ContainerProvider.TryResolve(out IObjectManager objectManager))
            {
                return string.Empty;
            }

            if (!playerManager.TryGetPlayer(peer, out Player player)
                || string.IsNullOrWhiteSpace(player.ClanId)
                || !objectManager.TryGetObject(player.ClanId, out Clan clan))
            {
                return string.Empty;
            }

            return clan?.StringId ?? string.Empty;
        }

        private static bool ValidateRequest(ServerAction request, out string error)
        {
            if (string.IsNullOrWhiteSpace(request.OperationId) || request.OperationId.Length > 128)
            {
                error = "IG: the operation id is invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Kind) || request.Kind.Length > 64
                || string.IsNullOrWhiteSpace(request.SettlementId) || request.SettlementId.Length > 256)
            {
                error = "IG: the action or settlement id is invalid.";
                return false;
            }

            if ((request.StringArgument?.Length ?? 0) > MaximumTextArgumentLength ||
                (request.ListArgument?.Length ?? 0) > MaximumTextArgumentLength)
            {
                error = "IG: the action payload is too large.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static IEnumerable<string> SplitList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            string[] items = value.Split(';');
            int count = Math.Min(items.Length, MaximumListItems);
            for (int index = 0; index < count; index++)
            {
                if (!string.IsNullOrWhiteSpace(items[index]))
                {
                    yield return items[index];
                }
            }
        }

        private static ActionOutcome Accept(ServerAction request, string code, string text, string data = "")
        {
            return new ActionOutcome { Success = true, Code = code, Text = text, Data = data };
        }

        private static ActionOutcome Reject(ServerAction request, string code, string text)
        {
            return new ActionOutcome { Success = false, Code = code, Text = text };
        }

        private static void CacheCompleted(string operationId)
        {
            Completed.Add(operationId);
            CompletedOrder.Enqueue(operationId);
            while (CompletedOrder.Count > DeduplicationCapacity)
            {
                Completed.Remove(CompletedOrder.Dequeue());
            }
        }
    }
}
