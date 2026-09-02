using System;
using System.Collections.Generic;
using System.IO;
using GameInterface;
using GameInterface.Services.ObjectManager;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.AI.Orders.PartyOrder;
using ImprovedGarrisons.CoopIntegration.Core;
using ImprovedGarrisons.CoopIntegration.Runtime;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace ImprovedGarrisons.CoopIntegration.Persistence
{
    internal static class PartyManifestStore
    {
        private const int ServerPollMilliseconds = 10_000;
        private const int ClientRetryMilliseconds = 3_000;
        private static readonly string StorePath = IntegrationDataPaths.FilePath("party-manifest.txt");
        private static int _nextPoll;
        private static bool _serverRestored;
        private static bool _clientUnresolved;
        private static IReadOnlyList<PartyManifestEntry>? _remoteEntries;
        private static long _remoteRevision;

        public static string SerializedManifest { get; private set; } = string.Empty;

        public static long Revision { get; private set; }

        internal static void RequestImmediatePoll()
        {
            _nextPoll = 0;
        }

        public static void Poll()
        {
            int now = Environment.TickCount;
            if (_nextPoll != 0 && unchecked(now - _nextPoll) < 0)
            {
                return;
            }

            if (IntegrationRuntime.IsServer)
            {
                _nextPoll = unchecked(now + ServerPollMilliseconds);
                PollServer();
            }
            else
            {
                _nextPoll = unchecked(now + ClientRetryMilliseconds);
                if (_clientUnresolved)
                {
                    Adopt(_remoteEntries ?? Array.Empty<PartyManifestEntry>(), false, true);
                }

                PartyIdentity.Prune();
            }
        }

        public static void ApplyRemote(string serialized, long revision)
        {
            if (IntegrationRuntime.IsServer || revision < _remoteRevision)
            {
                return;
            }

            try
            {
                IReadOnlyList<PartyManifestEntry> entries = PartyManifestCodec.Parse(serialized ?? string.Empty);
                _remoteEntries = entries;
                _remoteRevision = revision;
                SerializedManifest = serialized ?? string.Empty;
                Revision = revision;
                Adopt(entries, false, true);
            }
            catch (FormatException exception)
            {
                IntegrationLog.Error("rejected malformed party manifest: " + exception.Message);
            }
        }

        public static void Capture(string kind, MobileParty party, Settlement home, string detail)
        {
            if (party == null || home == null)
            {
                return;
            }

            if (string.Equals(kind, "transfer", StringComparison.Ordinal))
            {
                PartyIdentity.TransferSources[party] = home;
            }
            else if (string.Equals(kind, "villagerecruit", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(detail))
            {
                Settlement? village = FindSettlement(detail);
                if (village != null)
                {
                    PartyIdentity.RecruitVillages[party] = village;
                }
            }

            _nextPoll = 0;
        }

        public static void Remove(MobileParty party)
        {
            if (party == null)
            {
                return;
            }

            PartyIdentity.TransferSources.Remove(party);
            PartyIdentity.RecruitVillages.Remove(party);
            PartyIdentity.StatusTexts.Remove(party);
            _nextPoll = 0;
        }

        private static void PollServer()
        {
            if (Campaign.Current == null || global::ImprovedGarrisons.Main.PartyManagement == null)
            {
                return;
            }

            if (!_serverRestored)
            {
                _serverRestored = true;
                RestoreServerManifest();
            }

            SettingsStateStore.PollServer();

            PartyIdentity.Prune();
            List<PartyManifestEntry> entries = BuildEntries();
            string serialized = PartyManifestCodec.Serialize(entries);
            if (string.Equals(serialized, SerializedManifest, StringComparison.Ordinal))
            {
                return;
            }

            SerializedManifest = serialized;
            Revision++;
            try
            {
                IntegrationDataPaths.WriteAtomic(StorePath, serialized);
            }
            catch (IOException exception)
            {
                IntegrationLog.Error("party manifest persist failed: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                IntegrationLog.Error("party manifest persist denied: " + exception.Message);
            }

            IntegrationTransport.BroadcastManifest(SerializedManifest, Revision);
            IntegrationLog.Information("party manifest updated: " + entries.Count + " entries, revision " + Revision);
        }

        private static void RestoreServerManifest()
        {
            if (string.IsNullOrEmpty(StorePath) || !File.Exists(StorePath))
            {
                return;
            }

            try
            {
                string serialized = File.ReadAllText(StorePath);
                IReadOnlyList<PartyManifestEntry> entries = PartyManifestCodec.Parse(serialized);
                SerializedManifest = serialized;
                Adopt(entries, true, false);
                IntegrationLog.Information("restored " + entries.Count + " persisted party identities");
            }
            catch (IOException exception)
            {
                IntegrationLog.Error("party manifest restore failed: " + exception.Message);
            }
            catch (FormatException exception)
            {
                IntegrationLog.Error("party manifest restore rejected: " + exception.Message);
            }
        }

        private static List<PartyManifestEntry> BuildEntries()
        {
            List<PartyManifestEntry> entries = new List<PartyManifestEntry>();
            IDictionary<string, MobileGarrison>? guards = IntegrationReferences.Guards();
            if (guards != null)
            {
                foreach (MobileGarrison guard in guards.Values)
                {
                    MobileParty? party = guard?.getMobileParty();
                    Settlement? home = guard?.fromSettlement;
                    if (IsManifestParty(party) && home != null)
                    {
                        entries.Add(new PartyManifestEntry(
                            "guard",
                            GetCoopId(party!),
                            home.StringId ?? string.Empty,
                            BuildGuardDetail(guard!, party!),
                            guard!.GetStatusText() ?? string.Empty));
                    }
                }
            }

            IDictionary<MobileParty, GarrisonRecruiter>? recruiters = IntegrationReferences.Recruiters();
            if (recruiters != null)
            {
                foreach (KeyValuePair<MobileParty, GarrisonRecruiter> pair in recruiters)
                {
                    Settlement? home = pair.Value?.fromSettlement;
                    if (IsManifestParty(pair.Key) && pair.Value != null && home != null)
                    {
                        entries.Add(new PartyManifestEntry(
                            "recruiter",
                            GetCoopId(pair.Key),
                            home.StringId ?? string.Empty,
                            BuildRecruiterDetail(pair.Value),
                            pair.Value.GetStatusText() ?? string.Empty));
                    }
                }
            }

            IDictionary<MobileParty, Hero>? transfers = IntegrationReferences.Transfers();
            if (transfers != null)
            {
                foreach (MobileParty party in transfers.Keys)
                {
                    if (!IsManifestParty(party))
                    {
                        continue;
                    }

                    PartyIdentity.TransferSources.TryGetValue(party, out Settlement source);
                    Settlement? destination = party.HomeSettlement;
                    entries.Add(new PartyManifestEntry("transfer", GetCoopId(party), source?.StringId ?? string.Empty, destination?.StringId ?? string.Empty, string.Empty));
                }
            }

            HashSet<MobileParty>? villages = IntegrationReferences.VillageRecruiters();
            if (villages != null)
            {
                foreach (MobileParty party in villages)
                {
                    if (!IsManifestParty(party))
                    {
                        continue;
                    }

                    PartyIdentity.RecruitVillages.TryGetValue(party, out Settlement village);
                    entries.Add(new PartyManifestEntry("villagerecruit", GetCoopId(party), party.HomeSettlement?.StringId ?? string.Empty, village?.StringId ?? string.Empty, string.Empty));
                }
            }

            entries.Sort((left, right) => string.CompareOrdinal(left.PartyId, right.PartyId));
            return entries;
        }

        private static void Adopt(IReadOnlyList<PartyManifestEntry> entries, bool server, bool prune)
        {
            if (Campaign.Current == null || global::ImprovedGarrisons.Main.PartyManagement == null)
            {
                _clientUnresolved = !server && entries.Count > 0;
                return;
            }

            int adopted = 0;
            int unresolved = 0;
            foreach (PartyManifestEntry entry in entries)
            {
                MobileParty? party = FindParty(entry.PartyId);
                if (party == null || !party.IsActive)
                {
                    unresolved++;
                    continue;
                }

                Settlement? home = FindSettlement(entry.HomeSettlementId) ?? party.HomeSettlement;
                if (home == null)
                {
                    continue;
                }

                if (server && party.HomeSettlement != home)
                {
                    party.SetCustomHomeSettlement(home);
                }

                if (!server)
                {
                    PartyIdentity.SetStatus(party, entry.StatusText);
                }

                if (AdoptEntry(entry, party, home, server))
                {
                    adopted++;
                }
            }

            if (prune && unresolved == 0)
            {
                RemoveStale(entries);
            }

            _clientUnresolved = !server && unresolved > 0;
            if (adopted > 0 || unresolved > 0)
            {
                IntegrationLog.Information("party manifest adoption: +" + adopted + ", pending replicas=" + unresolved);
            }

            if (!server && adopted > 0)
            {
                // RefreshValues() skips the Overview tab while the campaign is paused unless this flag is set --
                // without it, newly adopted guard/recruiter/transfer parties stay invisible on a paused Overview
                // tab until the whole panel is closed and reopened (UpdateUiContents rebuilds unconditionally).
                global::ImprovedGarrisons.ImprovedGarrisonsUI.UIManager.Instance.ForceOverviewUpdate();
            }
        }

        private static bool AdoptEntry(PartyManifestEntry entry, MobileParty party, Settlement home, bool server)
        {
            if (entry.Kind == "guard")
            {
                IDictionary<string, MobileGarrison>? guards = IntegrationReferences.Guards();
                if (guards == null)
                {
                    return false;
                }

                if (guards.TryGetValue(home.StringId, out MobileGarrison current) && ReferenceEquals(current?.getMobileParty(), party))
                {
                    if (server)
                    {
                        ApplyGuardDetail(current, party, entry.Detail);
                    }
                    return false;
                }

                MobileGarrison guard = new MobileGarrison(party, home);
                guards[home.StringId] = guard;
                if (server)
                {
                    ApplyGuardDetail(guard, party, entry.Detail);
                }

                return true;
            }

            if (entry.Kind == "recruiter")
            {
                IDictionary<MobileParty, GarrisonRecruiter>? recruiters = IntegrationReferences.Recruiters();
                if (recruiters == null)
                {
                    return false;
                }

                if (recruiters.TryGetValue(party, out GarrisonRecruiter current))
                {
                    ApplyRecruiterDetail(current, entry.Detail);
                    return false;
                }

                GarrisonRecruiter recruiter = new GarrisonRecruiter(party, home);
                ApplyRecruiterDetail(recruiter, entry.Detail);
                recruiters[party] = recruiter;
                return true;
            }

            if (entry.Kind == "transfer")
            {
                IDictionary<MobileParty, Hero>? transfers = IntegrationReferences.Transfers();
                if (transfers == null)
                {
                    return false;
                }

                transfers[party] = home.Owner;
                PartyIdentity.TransferSources[party] = home;
                Settlement? destination = FindSettlement(entry.Detail);
                if (server && destination != null && party.HomeSettlement != destination)
                {
                    party.SetCustomHomeSettlement(destination);
                }

                return true;
            }

            if (entry.Kind == "villagerecruit")
            {
                HashSet<MobileParty>? villages = IntegrationReferences.VillageRecruiters();
                if (villages == null)
                {
                    return false;
                }

                bool added = villages.Add(party);
                Settlement? village = FindSettlement(entry.Detail);
                if (village != null)
                {
                    PartyIdentity.RecruitVillages[party] = village;
                }

                return added;
            }

            return false;
        }

        private static void RemoveStale(IReadOnlyList<PartyManifestEntry> entries)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (PartyManifestEntry entry in entries)
            {
                ids.Add(entry.PartyId);
            }

            List<MobileParty> staleStatuses = new List<MobileParty>();
            foreach (MobileParty party in PartyIdentity.StatusTexts.Keys)
            {
                if (party == null || !ids.Contains(GetCoopId(party)))
                {
                    staleStatuses.Add(party!);
                }
            }

            foreach (MobileParty party in staleStatuses)
            {
                PartyIdentity.StatusTexts.Remove(party);
            }

            IDictionary<MobileParty, GarrisonRecruiter>? recruiters = IntegrationReferences.Recruiters();
            RemoveStaleKeys(recruiters, ids);
            IDictionary<MobileParty, Hero>? transfers = IntegrationReferences.Transfers();
            RemoveStaleKeys(transfers, ids);
            HashSet<MobileParty>? villages = IntegrationReferences.VillageRecruiters();
            if (villages != null)
            {
                villages.RemoveWhere(party => party == null || !ids.Contains(GetCoopId(party)));
            }

            IDictionary<string, MobileGarrison>? guards = IntegrationReferences.Guards();
            if (guards == null)
            {
                return;
            }

            List<string> stale = new List<string>();
            foreach (KeyValuePair<string, MobileGarrison> pair in guards)
            {
                MobileParty? party = pair.Value?.getMobileParty();
                if (party == null || !ids.Contains(GetCoopId(party)))
                {
                    stale.Add(pair.Key);
                }
            }

            foreach (string key in stale)
            {
                guards.Remove(key);
            }
        }

        private static void RemoveStaleKeys<T>(IDictionary<MobileParty, T>? dictionary, HashSet<string> ids)
        {
            if (dictionary == null)
            {
                return;
            }

            List<MobileParty> stale = new List<MobileParty>();
            foreach (MobileParty party in dictionary.Keys)
            {
                if (party == null || !ids.Contains(GetCoopId(party)))
                {
                    stale.Add(party!);
                }
            }

            foreach (MobileParty party in stale)
            {
                dictionary.Remove(party);
            }
        }

        private static bool IsManifestParty(MobileParty? party)
        {
            return party != null && party.IsActive && !string.IsNullOrWhiteSpace(GetCoopId(party));
        }

        private static string BuildGuardDetail(MobileGarrison guard, MobileParty party)
        {
            if (guard.CurrentOrder is OrderMergeGarrison merge)
            {
                string targetId = party.HomeSettlement?.StringId ?? guard.fromSettlement?.StringId ?? string.Empty;
                return (merge.isReturning ? "return:" : "fortify:") + targetId;
            }

            if (guard.CurrentOrder is OrderPatrol)
            {
                return "patrol";
            }

            if (guard.CurrentOrder is OrderEscort)
            {
                MobileParty? target = party.TargetParty ?? party.Ai?.AiBehaviorPartyBase?.MobileParty;
                return "escort:" + (target == null ? string.Empty : GetCoopId(target));
            }

            if (guard.CurrentOrder is OrderDefense defense)
            {
                return "defense:" + (defense.SettlementToDefend?.StringId ?? string.Empty);
            }

            return guard.CurrentOrder?.GetType().Name ?? "none";
        }

        private static void ApplyGuardDetail(MobileGarrison guard, MobileParty party, string detail)
        {
            if (string.Equals(detail, nameof(OrderMergeGarrison), StringComparison.Ordinal) || detail.StartsWith("return:", StringComparison.Ordinal))
            {
                guard.SetReturnMode();
                return;
            }

            if (string.Equals(detail, nameof(OrderPatrol), StringComparison.Ordinal) || string.Equals(detail, "patrol", StringComparison.Ordinal))
            {
                guard.GiveAndExecuteOrder(new OrderPatrol(guard.fromSettlement));
                return;
            }

            if (detail.StartsWith("fortify:", StringComparison.Ordinal))
            {
                Settlement? target = FindSettlement(detail.Substring("fortify:".Length));
                if (target != null)
                {
                    party.SetCustomHomeSettlement(target);
                    guard.SetFortifyMode(target);
                }
                return;
            }

            if (detail.StartsWith("escort:", StringComparison.Ordinal))
            {
                MobileParty? target = FindParty(detail.Substring("escort:".Length));
                if (target != null)
                {
                    guard.GiveAndExecuteOrder(new OrderEscort(target));
                }
                return;
            }

            if (detail.StartsWith("defense:", StringComparison.Ordinal))
            {
                Settlement? target = FindSettlement(detail.Substring("defense:".Length));
                if (target != null)
                {
                    guard.GiveAndExecuteOrder(new OrderDefense(target));
                }
            }
        }

        private static string BuildRecruiterDetail(GarrisonRecruiter recruiter)
        {
            return recruiter.currentMode + ":" + (recruiter.tradeTarget?.StringId ?? string.Empty);
        }

        private static void ApplyRecruiterDetail(GarrisonRecruiter recruiter, string detail)
        {
            int separator = detail.IndexOf(':');
            string mode = separator < 0 ? detail : detail.Substring(0, separator);
            string targetId = separator < 0 ? string.Empty : detail.Substring(separator + 1);
            if (Enum.TryParse(mode, false, out GarrisonRecruiter.Mode parsedMode))
            {
                recruiter.currentMode = parsedMode;
            }

            recruiter.tradeTarget = FindSettlement(targetId);
        }

        private static string GetCoopId(MobileParty party)
        {
            if (ContainerProvider.TryResolve(out IObjectManager manager)
                && manager.TryGetId(party, out string id)
                && !string.IsNullOrWhiteSpace(id))
            {
                return id;
            }

            return party.StringId ?? string.Empty;
        }

        private static MobileParty? FindParty(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (ContainerProvider.TryResolve(out IObjectManager manager) && manager.TryGetObject(id, out MobileParty party))
            {
                return party;
            }

            MobileParty? registered = MBObjectManager.Instance?.GetObject<MobileParty>(id);
            if (registered != null)
            {
                return registered;
            }

            foreach (MobileParty candidate in MobileParty.All)
            {
                if (candidate != null && string.Equals(candidate.StringId, id, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Settlement? FindSettlement(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (ContainerProvider.TryResolve(out IObjectManager manager) && manager.TryGetObject(id, out Settlement settlement))
            {
                return settlement;
            }

            return MBObjectManager.Instance?.GetObject<Settlement>(id);
        }
    }
}
