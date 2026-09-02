using System;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.AI.Orders.PartyOrder
{
	public class OrderEscort : ImprovedPartyOrder
	{
		private MobileParty escortParty;

		public OrderEscort(MobileParty partyToEscort)
		{
			if (partyToEscort != null)
			{
				escortParty = partyToEscort;
			}
			else
			{
				SetOrderToFinished();
			}
		}

		public override void ExecuteOrder()
		{
			try
			{
				base.PartyToOrder.mobileParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: true);
				if (escortParty != null && escortParty.IsActive)
				{
					base.PartyToOrder.mobileParty.SetMoveEscortParty(escortParty, base.PartyToOrder.mobileParty.NavigationCapability, escortParty.IsTargetingPort);
				}
				else if (escortParty != null && escortParty.Party.Owner != null)
				{
					base.PartyToOrder.mobileParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: false);
					Hero owner = escortParty.Party.Owner;
					PartyBase partyBelongedToAsPrisoner = owner.PartyBelongedToAsPrisoner;
					if (partyBelongedToAsPrisoner != null)
					{
						base.PartyToOrder.mobileParty.SetMoveEngageParty(partyBelongedToAsPrisoner.MobileParty, base.PartyToOrder.mobileParty.NavigationCapability);
					}
				}
				else
				{
					SetOrderToFinished();
				}
				base.PartyToOrder.mobileParty.Aggressiveness = 0.4f;
				if (escortParty != null && !escortParty.IsActive && escortParty.IsLordParty)
				{
					if (escortParty != null && escortParty.Ai.AiBehaviorPartyBase != null)
					{
						MobileParty mobileParty = escortParty.Ai.AiBehaviorPartyBase.MobileParty;
						base.PartyToOrder.mobileParty.SetMoveEngageParty(mobileParty, base.PartyToOrder.mobileParty.NavigationCapability);
						base.PartyToOrder.mobileParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: false);
					}
				}
				else if (escortParty != null && !escortParty.IsActive)
				{
					SetOrderToFinished();
					base.PartyToOrder.mobileParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: false);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public override string GetStatusText()
		{
			string text = "";
			if (escortParty != null && escortParty.Name != null)
			{
				return new TextObject("{=menu_guard_status_issupporting}The guard party is escorting" + ModuleStrings._space + escortParty.Name).ToString();
			}
			return new TextObject("{=menu_guard_status_issupporting}The guard party is escorting").ToString();
		}
	}
}
