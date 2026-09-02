using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.AI.Orders.PartyOrder
{
	public class OrderMergeGarrison : ImprovedPartyOrder
	{
		private Settlement settlement;

		public readonly bool isReturning;

		public OrderMergeGarrison(Settlement settlementToMergeWith, bool isReturning = false)
		{
			settlement = settlementToMergeWith;
			this.isReturning = isReturning;
		}

		public override void ExecuteOrder()
		{
			if (base.PartyToOrder.mobileParty.CurrentSettlement != null && base.PartyToOrder.mobileParty.CurrentSettlement == settlement)
			{
				Main.PartyManagement.RecruitMobilePartyToGarrison(base.PartyToOrder.mobileParty, settlement);
				return;
			}
			Main.GarrisonPartyBehavior.SetMoveGoToSettlementHelper(settlement, base.PartyToOrder.mobileParty);
			base.PartyToOrder.mobileParty.Aggressiveness = 0f;
		}

		public override string GetStatusText()
		{
			string result = "";
			if (!isReturning)
			{
				result = ((settlement == null) ? new TextObject("{=menu_guard_status_isfortifyinThe guard party is reinforcingng").ToString() : ((!(settlement.EncyclopediaLinkWithName != null)) ? new TextObject("{=menu_guard_status_isfortifyingThe guard party is reinforcingg" + ModuleStrings._space + settlement.Name).ToString() : new TextObject("{=menu_guard_status_isfortifying}The guard party is reinforcing" + ModuleStrings._space + settlement.EncyclopediaLinkWithName).ToString()));
			}
			else if (isReturning)
			{
				result = ((settlement == null) ? new TextObject("{=menu_guard_status_returning}The guard party is returning").ToString() : ((!(settlement.EncyclopediaLinkWithName != null)) ? new TextObject("{=menu_guard_status_returningto}The guard party is returning to" + ModuleStrings._space + settlement.Name).ToString() : new TextObject("{=menu_guard_status_returningto}The guard party is returning to" + ModuleStrings._space + settlement.EncyclopediaLinkWithName).ToString()));
			}
			return result;
		}
	}
}
