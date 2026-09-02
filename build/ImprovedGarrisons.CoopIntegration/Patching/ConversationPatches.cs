using System.Collections.Generic;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using ImprovedGarrisons.CoopIntegration.Persistence;
using ImprovedGarrisons.CoopIntegration.Protocol;
using ImprovedGarrisons.CoopIntegration.Runtime;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.CoopIntegration.Patching
{
    internal static class ConversationPatches
    {
        public static bool GuardReturnPrefix()
        {
            return Forward(PartyIntentKind.OrderReturn, false);
        }

        public static bool GuardPatrolPrefix()
        {
            return Forward(PartyIntentKind.OrderPatrol, false);
        }

        public static bool GuardEscortPrefix()
        {
            return Forward(PartyIntentKind.EscortPlayer, false);
        }

        public static bool GuardFortifyPrefix()
        {
            if (IntegrationRuntime.IsServer)
            {
                return true;
            }

            MobileParty? party = PlayerEncounter.EncounteredParty?.MobileParty;
            Settlement? home = party == null ? null : IntegrationReferences.GuardHome(party);
            if (home?.Town != null && home.OwnerClan != null)
            {
                List<InquiryElement> targets = new List<InquiryElement>();
                foreach (Settlement settlement in Settlement.All)
                {
                    if (settlement?.Town != null && settlement.OwnerClan == home.OwnerClan &&
                        (settlement.IsTown || settlement.IsCastle))
                    {
                        targets.Add(new InquiryElement(settlement, settlement.Name?.ToString() ?? settlement.StringId, new EmptyImageIdentifier()));
                    }
                }

                string homeId = home.StringId ?? string.Empty;
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    new TextObject("{=menu_transfer_select}Select Garrison").ToString(),
                    new TextObject("{=menu_fortify_desc}Select the garrison you want your guards to fortify.").ToString(),
                    targets,
                    true,
                    1,
                    1,
                    new TextObject("{=menu_ok}Okay").ToString(),
                    new TextObject("{=menu_cancel}Cancel").ToString(),
                    selected =>
                    {
                        Settlement? target = selected != null && selected.Count > 0 ? selected[0].Identifier as Settlement : null;
                        if (target != null)
                        {
                            IntegrationTransport.SendIntent(new PartyIntent
                            {
                                Operation = PartyIntentKind.Fortify,
                                SettlementId = homeId,
                                StringArgument = target.StringId ?? string.Empty
                            });
                        }
                    },
                    null));
            }

            PlayerEncounter.LeaveEncounter = true;
            return false;
        }

        public static bool RecruiterReturnPrefix()
        {
            return Forward(PartyIntentKind.ReturnRecruiter, true);
        }

        public static bool RecruiterChangeCulturePrefix()
        {
            if (IntegrationRuntime.IsServer)
            {
                return true;
            }

            MobileParty? party = PlayerEncounter.EncounteredParty?.MobileParty;
            Settlement? home = party == null ? null : IntegrationReferences.RecruiterHome(party);
            if (home?.Town != null)
            {
                RecruitmentSettings.Instance.PromptChangeRecruitmentCulture(home.Town);
            }

            PlayerEncounter.LeaveEncounter = true;
            return false;
        }

        private static bool Forward(PartyIntentKind? operation, bool recruiter)
        {
            if (IntegrationRuntime.IsServer)
            {
                return true;
            }

            MobileParty? party = PlayerEncounter.EncounteredParty?.MobileParty;
            Settlement? home = party == null
                ? null
                : recruiter ? IntegrationReferences.RecruiterHome(party) : IntegrationReferences.GuardHome(party);
            if (home?.Town != null && operation.HasValue)
            {
                IntegrationTransport.SendIntent(new PartyIntent
                {
                    Operation = operation.Value,
                    SettlementId = home.StringId ?? string.Empty
                });
            }

            PlayerEncounter.LeaveEncounter = true;
            return false;
        }
    }
}
