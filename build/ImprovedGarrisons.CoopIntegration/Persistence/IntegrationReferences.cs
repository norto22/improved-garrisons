using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ImprovedGarrisons.AI.AIManagers;
using ImprovedGarrisons.AI.AITypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ImprovedGarrisons.CoopIntegration.Persistence
{
    internal static class IntegrationReferences
    {
        private static readonly PropertyInfo? GuardDictionaryProperty = AccessTools.Property(typeof(MobileGarrisonManager), "MobileGarrisons");
        private static readonly PropertyInfo? RecruiterDictionaryProperty = AccessTools.Property(typeof(GarrisonRecruiterPartyManager), "GarrisonRecruiterParties");
        private static readonly PropertyInfo? TransferDictionaryProperty = AccessTools.Property(typeof(TransferPartyManager), "TransferParties");
        private static readonly PropertyInfo? VillageSetProperty = AccessTools.Property(typeof(VillageRecruitPartyManager), "VillageRecruitParties");

        public static IDictionary<string, MobileGarrison>? Guards()
        {
            object? manager = global::ImprovedGarrisons.Main.PartyManagement?.mobileGarrisonManagement;
            return manager == null ? null : GuardDictionaryProperty?.GetValue(manager, null) as IDictionary<string, MobileGarrison>;
        }

        public static IDictionary<MobileParty, GarrisonRecruiter>? Recruiters()
        {
            object? manager = global::ImprovedGarrisons.Main.PartyManagement?.garrisonRecruiterPartyManagement;
            return manager == null ? null : RecruiterDictionaryProperty?.GetValue(manager, null) as IDictionary<MobileParty, GarrisonRecruiter>;
        }

        public static IDictionary<MobileParty, Hero>? Transfers()
        {
            object? manager = global::ImprovedGarrisons.Main.PartyManagement?.transferPartyManagement;
            return manager == null ? null : TransferDictionaryProperty?.GetValue(manager, null) as IDictionary<MobileParty, Hero>;
        }

        public static HashSet<MobileParty>? VillageRecruiters()
        {
            object? manager = global::ImprovedGarrisons.Main.PartyManagement?.villageRecruitPartyManagement;
            return manager == null ? null : VillageSetProperty?.GetValue(manager, null) as HashSet<MobileParty>;
        }

        public static Settlement? GuardHome(MobileParty party)
        {
            IDictionary<string, MobileGarrison>? guards = Guards();
            if (party == null || guards == null)
            {
                return null;
            }

            foreach (MobileGarrison guard in guards.Values)
            {
                if (guard != null && ReferenceEquals(guard.getMobileParty(), party))
                {
                    return guard.fromSettlement;
                }
            }

            return null;
        }

        public static Settlement? RecruiterHome(MobileParty party)
        {
            IDictionary<MobileParty, GarrisonRecruiter>? recruiters = Recruiters();
            return recruiters != null && party != null && recruiters.TryGetValue(party, out GarrisonRecruiter recruiter)
                ? recruiter?.fromSettlement
                : null;
        }
    }
}
