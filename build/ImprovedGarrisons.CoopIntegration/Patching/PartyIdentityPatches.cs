using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.CoopIntegration.Persistence;
using ImprovedGarrisons.CoopIntegration.Runtime;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ImprovedGarrisons.CoopIntegration.Patching
{
    internal static class PartyIdentityPatches
    {
        public static void IsGuardPostfix(MobileParty party, ref bool __result)
        {
            if (!__result && party != null)
            {
                __result = IntegrationReferences.GuardHome(party) != null;
            }
        }

        public static void GuardHomePostfix(MobileParty party, ref Settlement? __result)
        {
            if (__result == null && party != null)
            {
                __result = IntegrationReferences.GuardHome(party);
            }
        }

        public static void GuardStatusPostfix(MobileGarrison __instance, ref string __result)
        {
            if (!IntegrationRuntime.IsServer && __instance != null &&
                PartyIdentity.TryGetStatus(__instance.getMobileParty(), out string statusText))
            {
                __result = statusText;
            }
        }

        public static void IsRecruiterPostfix(MobileParty party, ref bool __result)
        {
            if (!__result && party != null)
            {
                __result = IntegrationReferences.Recruiters()?.ContainsKey(party) ?? false;
            }
        }

        public static void RecruiterHomePostfix(MobileParty party, ref Settlement? __result)
        {
            if (__result == null && party != null)
            {
                __result = IntegrationReferences.RecruiterHome(party);
            }
        }

        public static void RecruiterStatusPostfix(GarrisonRecruiter __instance, ref string __result)
        {
            if (IntegrationRuntime.IsServer || __instance == null)
            {
                return;
            }

            var recruiters = IntegrationReferences.Recruiters();
            if (recruiters == null)
            {
                return;
            }

            foreach (var pair in recruiters)
            {
                if (ReferenceEquals(pair.Value, __instance) && PartyIdentity.TryGetStatus(pair.Key, out string statusText))
                {
                    __result = statusText;
                    return;
                }
            }
        }

        public static void IsTransferPostfix(MobileParty party, ref bool __result)
        {
            if (!__result && party != null)
            {
                __result = IntegrationReferences.Transfers()?.ContainsKey(party) ?? false;
            }
        }

        public static void TransferHomePostfix(MobileParty party, ref Settlement? __result)
        {
            if (__result == null && party != null && PartyIdentity.TransferSources.TryGetValue(party, out Settlement home))
            {
                __result = home;
            }
        }

        public static void TransferCreatedPostfix(Settlement fromSettlement, PartyBase __result)
        {
            MobileParty? party = __result?.MobileParty;
            if (party != null && fromSettlement != null)
            {
                PartyIdentity.TransferSources[party] = fromSettlement;
                PartyManifestStore.Capture("transfer", party, fromSettlement, party.HomeSettlement?.StringId ?? string.Empty);
            }
        }

        public static void IsVillageRecruitPostfix(MobileParty party, ref bool __result)
        {
            if (!__result && party != null)
            {
                __result = IntegrationReferences.VillageRecruiters()?.Contains(party) ?? false;
            }
        }

        public static void VillageHomePostfix(MobileParty party, ref Settlement? __result)
        {
            if (__result == null && party != null && PartyIdentity.RecruitVillages.TryGetValue(party, out Settlement village))
            {
                __result = village;
            }
        }

        public static void VillageRecruitCreatedPostfix(Settlement spawnOn, PartyBase __result)
        {
            MobileParty? party = __result?.MobileParty;
            if (party != null && spawnOn != null)
            {
                PartyIdentity.RecruitVillages[party] = spawnOn;
                if (party.HomeSettlement != null)
                {
                    PartyManifestStore.Capture("villagerecruit", party, party.HomeSettlement, spawnOn.StringId ?? string.Empty);
                }
            }
        }
    }
}
