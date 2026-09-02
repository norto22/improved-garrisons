using System;
using System.Reflection;
using Helpers;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.AI.Orders.PartyOrder
{
	public class OrderDefense : ImprovedPartyOrder
	{
		private MobileParty currentTarget;

		private bool defendMessageSent = false;

		public Settlement SettlementToDefend { get; private set; }

		public OrderDefense(Settlement settlementToDefend)
		{
			SettlementToDefend = settlementToDefend;
		}

		public override void ExecuteOrder()
		{
			try
			{
				base.PartyToOrder.mobileParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: false);
				if (SettlementToDefend.IsUnderRaid || SettlementToDefend.IsUnderSiege)
				{
					DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(base.PartyToOrder.mobileParty, SettlementToDefend, base.PartyToOrder.mobileParty.NavigationCapability, out var isTargetingPort, out var _);
					base.PartyToOrder.mobileParty.SetMoveDefendSettlement(SettlementToDefend, isTargetingPort, base.PartyToOrder.mobileParty.NavigationCapability);
					base.PartyToOrder.mobileParty.Aggressiveness = 1f;
					base.PartyToOrder.HourCounter = 0;
					if (defendMessageSent)
					{
						return;
					}
					bool isVillage = SettlementToDefend.IsVillage;
					MobileParty mobileParty = null;
					if (isVillage)
					{
						if (SettlementToDefend.LastAttackerParty != null)
						{
							mobileParty = SettlementToDefend.LastAttackerParty;
						}
					}
					else if (SettlementToDefend?.SiegeEvent?.BesiegerCamp?.LeaderParty != null)
					{
						mobileParty = SettlementToDefend.SiegeEvent.BesiegerCamp.LeaderParty;
					}
					if (mobileParty != null && !base.PartyToOrder.isNPC)
					{
						float partyStrength = base.PartyToOrder.GetPartyStrength(mobileParty);
						float num = mobileParty.MemberRoster.TotalManCount;
						if (mobileParty.Army != null)
						{
							partyStrength = mobileParty.Army.EstimatedStrength;
							num = mobileParty.Army.TotalManCount;
						}
						if (base.PartyToOrder.CanDeafeat(partyStrength, base.PartyToOrder.mobileParty) && !base.PartyToOrder.isNPC)
						{
							InformationManager.DisplayMessage(new InformationMessage(base.PartyToOrder.mobileParty.Name.ToString() + ModuleStrings._space + new TextObject("{=info_guards_defending1}are defending").ToString() + ModuleStrings._space + SettlementToDefend.Name.ToString() + ModuleStrings._space + new TextObject("{=info_guards_defending2}against").ToString() + ModuleStrings._space + mobileParty.Name.ToString() + ModuleStrings._space + new TextObject("{=info_guards_defending3}of size").ToString() + ModuleStrings._space + num, Color.FromUint(ModuleColors.modMainColor)));
						}
						else if (!base.PartyToOrder.isNPC)
						{
							InformationManager.DisplayMessage(new InformationMessage(base.PartyToOrder.mobileParty.Name.ToString() + ModuleStrings._space + new TextObject("{=info_guards_needshelpdefending1}needs help defending").ToString() + ModuleStrings._space + SettlementToDefend.Name.ToString() + ModuleStrings._space + new TextObject("{=info_guards_needshelpdefending2}against").ToString() + ModuleStrings._space + mobileParty.Name.ToString() + ModuleStrings._space + new TextObject("{=info_guards_needshelpdefending3}of size").ToString() + ModuleStrings._space + num, Color.FromUint(ModuleColors.yellow)));
						}
						defendMessageSent = true;
					}
				}
				else if (currentTarget != null && base.PartyToOrder.HourCounter % 6 != 0)
				{
					base.PartyToOrder.mobileParty.Aggressiveness = 0.9f;
					base.PartyToOrder.mobileParty.SetMoveEngageParty(currentTarget, base.PartyToOrder.mobileParty.NavigationCapability);
				}
				else
				{
					SetOrderToFinished();
					base.PartyToOrder.mobileParty.Aggressiveness = 0.9f;
					defendMessageSent = false;
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
			if (SettlementToDefend != null)
			{
				if (SettlementToDefend.EncyclopediaLinkWithName != null)
				{
					return new TextObject("{=menu_guard_status_isdefending}The guard party is defending" + ModuleStrings._space + SettlementToDefend.EncyclopediaLinkWithName).ToString();
				}
				return new TextObject("{=menu_guard_status_isdefending}The guard party is defending" + ModuleStrings._space + SettlementToDefend.Name).ToString();
			}
			return new TextObject("{=menu_guard_status_isdefending}The guard party is defending").ToString();
		}
	}
}
