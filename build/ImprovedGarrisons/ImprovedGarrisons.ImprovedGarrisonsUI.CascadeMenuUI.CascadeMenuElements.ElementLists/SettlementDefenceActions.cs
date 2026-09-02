using System;
using System.Collections.Generic;
using System.Linq;
using ImprovedGarrisons.AI.Orders.PartyOrder;
using ImprovedGarrisons.Behaviours;
using ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements.Elements;
using ImprovedGarrisons.ImprovedGarrisonsUI.UIElements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements.ElementLists
{
	public class SettlementDefenceActions
	{
		public MBBindingList<CascadeMenuElementVM> actions = new MBBindingList<CascadeMenuElementVM>();

		public string Title = new TextObject("{=ui_improvedgarrisonsui_activity_defend1}Defend settlement").ToString();

		public CascadeMenu Menu;

		private Settlement settlementForAction;

		public SettlementDefenceActions(Settlement settlement)
		{
			settlementForAction = settlement;
			InitializeDefendActions();
			Menu = new CascadeMenu(new TextObject("{=ui_improvedgarrisonsui_activity_defend2}Defend").ToString(), actions, 470);
		}

		private void InitializeDefendActions()
		{
			List<ImprovedGarrisonsPartyInformationVM> list = new List<ImprovedGarrisonsPartyInformationVM>();
			Clan playerClan = Clan.PlayerClan;
			MobileParty mainParty = MobileParty.MainParty;
			if (playerClan == null || mainParty == null)
			{
				return;
			}
			MBBindingList<ImprovedGarrisonsPartyInformationVM> mBBindingList = new MBBindingList<ImprovedGarrisonsPartyInformationVM>();
			List<MobileParty> allClanParties = GarrisonPartyBehavior.GetAllClanParties(playerClan);
			foreach (MobileParty item in allClanParties)
			{
				if (item != mainParty && !item.IsCaravan && !item.IsGarrison && !item.IsMilitia && !item.IsVillager && !Main.PartyManagement.garrisonRecruiterPartyManagement.IsRecruiterParty(item))
				{
					Action<MobileParty> onPressAction = delegate(MobileParty actionParty)
					{
						Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonForParty(actionParty)?.GiveAndExecuteOrder(new OrderDefense(settlementForAction));
						UIManager.Instance.CloseCascadeMenu();
					};
					list.Add(new ImprovedGarrisonsPartyInformationVM(item, settlementForAction, onPressAction));
				}
			}
			list = list.OrderBy((ImprovedGarrisonsPartyInformationVM o) => o.DistanceInTimeFloat).ToList();
			MBBindingList<ImprovedGarrisonsPartyInformationVM> mBBindingList2 = new MBBindingList<ImprovedGarrisonsPartyInformationVM>();
			foreach (ImprovedGarrisonsPartyInformationVM item2 in list)
			{
				mBBindingList2.Add(item2);
			}
			actions.Add(new CascadeMenuPartyListVM(mBBindingList2));
		}
	}
}
