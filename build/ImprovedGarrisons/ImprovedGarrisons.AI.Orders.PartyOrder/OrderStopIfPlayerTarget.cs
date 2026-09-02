using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.AI.Orders.PartyOrder
{
	public class OrderStopIfPlayerTarget : ImprovedPartyOrder
	{
		public override void ExecuteOrder()
		{
			base.PartyToOrder.mobileParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: true);
			if (Hero.MainHero.PartyBelongedTo != null)
			{
				MobileParty targetParty = Hero.MainHero.PartyBelongedTo.TargetParty;
				if (targetParty != null && targetParty == base.PartyToOrder.mobileParty)
				{
					base.PartyToOrder.mobileParty.SetMoveModeHold();
					return;
				}
				base.PartyToOrder.mobileParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: false);
				SetOrderToFinished();
			}
			else
			{
				base.PartyToOrder.mobileParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: false);
				SetOrderToFinished();
			}
		}

		public override string GetStatusText()
		{
			return new TextObject("{=menu_guard_status_iswaiting}The guard party is waiting").ToString();
		}
	}
}
