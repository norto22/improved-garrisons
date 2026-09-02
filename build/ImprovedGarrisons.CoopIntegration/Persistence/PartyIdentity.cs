using System.Collections.Generic;
using ImprovedGarrisons.CoopIntegration.Runtime;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ImprovedGarrisons.CoopIntegration.Persistence
{
    internal static class PartyIdentity
    {
        public static readonly Dictionary<MobileParty, Settlement> TransferSources = new Dictionary<MobileParty, Settlement>();
        public static readonly Dictionary<MobileParty, Settlement> RecruitVillages = new Dictionary<MobileParty, Settlement>();

        public static readonly Dictionary<MobileParty, string> StatusTexts = new Dictionary<MobileParty, string>();

        public static void Prune()
        {
            PruneMap(TransferSources);
            PruneMap(RecruitVillages);
            PruneStatuses();
        }

        public static void SetStatus(MobileParty party, string statusText)
        {
            if (IntegrationRuntime.IsServer || party == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(statusText))
            {
                StatusTexts.Remove(party);
            }
            else
            {
                StatusTexts[party] = statusText;
            }
        }

        public static bool TryGetStatus(MobileParty party, out string statusText)
        {
            if (party != null && StatusTexts.TryGetValue(party, out statusText) && !string.IsNullOrWhiteSpace(statusText))
            {
                return true;
            }

            statusText = string.Empty;
            return false;
        }

        private static void PruneMap(Dictionary<MobileParty, Settlement> map)
        {
            List<MobileParty>? stale = null;
            foreach (MobileParty party in map.Keys)
            {
                if (party == null || !party.IsActive)
                {
                    if (stale == null)
                    {
                        stale = new List<MobileParty>();
                    }

                    stale.Add(party!);
                }
            }

            if (stale == null)
            {
                return;
            }

            foreach (MobileParty party in stale)
            {
                map.Remove(party);
            }
        }

        private static void PruneStatuses()
        {
            List<MobileParty>? stale = null;
            foreach (MobileParty party in StatusTexts.Keys)
            {
                if (party == null || !party.IsActive)
                {
                    if (stale == null)
                    {
                        stale = new List<MobileParty>();
                    }

                    stale.Add(party!);
                }
            }

            if (stale == null)
            {
                return;
            }

            foreach (MobileParty party in stale)
            {
                StatusTexts.Remove(party);
            }
        }
    }
}
