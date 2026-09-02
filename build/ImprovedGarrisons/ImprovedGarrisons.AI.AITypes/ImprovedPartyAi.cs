using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Helpers;
using ImprovedGarrisons.AI.Orders;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.SaveSystem.Configuration;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.AI.AITypes
{
	public abstract class ImprovedPartyAi : ImprovedAi
	{
		internal readonly MobileParty mobileParty;

		public bool isNPC = false;

		private bool targetHasBeenReset = false;

		public readonly float settlementTradeRadius = 50f;

		protected int hourCounterTrade = 0;

		protected int hoursTillNextTrade = 48;

		protected int inventoryValueToTrade = 500;

		public Settlement settlementTradeTarget;

		public Dictionary<MobileParty, int> targetsWithTimer = new Dictionary<MobileParty, int>();

		public Hero ownerHero { get; }

		public float partySightRadius => (mobileParty != null) ? mobileParty.SeeingRange : 0f;

		public float settlementSightRadius => (mobileParty != null) ? mobileParty.SeeingRange : 0f;

		public int InitialSize { get; protected set; }

		public ImprovedPartyOrder CurrentOrder { get; private set; }

		private ImprovedPartyOrder PreviousOrder { get; set; }

		private ImprovedPartyOrder NextOrder { get; set; }

		public virtual bool ReplenishEnabled { get; private set; }

		public override void HourlyThinkBehavior()
		{
			try
			{
				if (CurrentOrder.IsOrderFinished())
				{
					CurrentOrder = null;
				}
				if (CurrentOrder != null && CurrentOrder.IsInitialized)
				{
					CurrentOrder.ExecuteOrder();
				}
				else if (NextOrder != null && NextOrder.IsInitialized)
				{
					GiveAndExecuteOrder(NextOrder);
					NextOrder = null;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public ImprovedPartyAi(MobileParty party)
		{
			mobileParty = party;
			ownerHero = ((party.ActualClan != null) ? party.ActualClan.Leader : party.Party.Owner);
			InitialSize = party.MemberRoster.TotalManCount;
		}

		public virtual void QueueNextOrder(ImprovedPartyOrder order)
		{
			if (order != null)
			{
				NextOrder = order;
				NextOrder.InitializeOrder(this);
			}
		}

		public virtual void GiveAndExecuteOrder(ImprovedPartyOrder order)
		{
			if (order != null)
			{
				PreviousOrder = CurrentOrder;
				CurrentOrder = order;
				CurrentOrder.InitializeOrder(this);
				CurrentOrder.ExecuteOrder();
			}
		}

		public virtual void StopOrder()
		{
			CurrentOrder = null;
		}

		public bool ReturnToPreviousOrder()
		{
			if (PreviousOrder != null)
			{
				ImprovedPartyOrder currentOrder = CurrentOrder;
				CurrentOrder = PreviousOrder;
				PreviousOrder = currentOrder;
				CurrentOrder.ExecuteOrder();
				return true;
			}
			return false;
		}

		public bool ResetTarget()
		{
			try
			{
				if (mobileParty.Ai.AiBehaviorPartyBase != null)
				{
					mobileParty.Aggressiveness = 0f;
					targetHasBeenReset = true;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		public Settlement BestNearbyTradeSettlement(bool toBuy = false)
		{
			try
			{
				List<Settlement> allNearbySettlements = GetAllNearbySettlements(settlementTradeRadius, mobileParty);
				Settlement result = null;
				float num = float.MaxValue;
				foreach (Settlement item in allNearbySettlements)
				{
					if (SettlementIsAValidTradePartner(item, mobileParty.MapFaction))
					{
						AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(mobileParty, item, mobileParty.HasNavalNavigationCapability, out var _, out var bestNavigationDistance, out var _);
						if (bestNavigationDistance < num && (!toBuy || SettlementHorseAmount(item) > 5))
						{
							result = item;
							num = bestNavigationDistance;
						}
					}
				}
				return result;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return null;
		}

		protected int SettlementHorseAmount(Settlement settlement)
		{
			int num = 0;
			ItemRoster itemRoster = settlement.Town.Owner.ItemRoster;
			for (int i = 0; i < itemRoster.Count; i++)
			{
				ItemObject itemAtIndex = itemRoster.GetItemAtIndex(i);
				if (ItemIsAHorse(itemAtIndex))
				{
					num += itemRoster.GetElementCopyAtIndex(i).Amount;
				}
			}
			return num;
		}

		protected bool OwnerHasEnoughMoneyToBuy(int gold, Clan owner)
		{
			return owner.Gold > gold + 5000;
		}

		public bool ExecuteTrade(bool sellPrisoners, bool sellItems, bool buyHorses)
		{
			try
			{
				if (mobileParty.CurrentSettlement == null)
				{
					return false;
				}
				Settlement currentSettlement = mobileParty.CurrentSettlement;
				if (mobileParty.PrisonRoster != null && sellPrisoners)
				{
					Main.GarrisonPartyBehavior.SellPrisoners(mobileParty, currentSettlement.Town);
				}
				if (SettlementIsAValidTradePartner(currentSettlement, mobileParty.MapFaction))
				{
					if (sellItems)
					{
						SellItems(currentSettlement);
					}
					if (buyHorses)
					{
						BuyHorses(currentSettlement);
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		protected void IfStuckPortToAFreeZone()
		{
			if (mobileParty.StationaryStartTime.ToDays >= 10.0 && mobileParty.MapEvent == null && mobileParty.CurrentSettlement != null && !mobileParty.Ai.IsDisabled && !mobileParty.Ai.DoNotMakeNewDecisions)
			{
				MobileParty.NavigationType navigationCapability = (mobileParty.Position.IsOnLand ? MobileParty.NavigationType.Default : MobileParty.NavigationType.Naval);
				CampaignVec2 position = NavigationHelper.FindReachablePointAroundPosition(mobileParty.Position, navigationCapability, 10f, 10f);
				mobileParty.Position = position;
			}
		}

		public bool NeedsHeal()
		{
			int totalManCount = mobileParty.MemberRoster.TotalManCount;
			int totalWounded = mobileParty.MemberRoster.TotalWounded;
			if (totalWounded == 0)
			{
				return false;
			}
			float patrolPartyHealPercentage = ConfigManager.Instance.Config.PatrolPartyHealPercentage;
			return (float)(totalManCount - totalWounded) < (float)InitialSize * patrolPartyHealPercentage + 1f;
		}

		public void SellItems(Settlement settlement, bool onlyNecessary = true)
		{
			try
			{
				if (!Campaign.Current.GameStarted || mobileParty == null || FactionManager.IsAtWarAgainstFaction(mobileParty.MapFaction, settlement.MapFaction) || mobileParty.IsDisbanding || !settlement.IsTown)
				{
					return;
				}
				int gold = settlement.SettlementComponent.Gold;
				int num = 0;
				int num2 = 0;
				foreach (ItemRosterElement item2 in mobileParty.ItemRoster)
				{
					ItemObject item = item2.EquipmentElement.Item;
					if (item.IsFood)
					{
						continue;
					}
					ItemModifier itemModifier = item2.EquipmentElement.ItemModifier;
					int amount = item2.Amount;
					if (!onlyNecessary || (!ItemIsUnnecessary(item) && itemModifier != null))
					{
						int itemPrice = settlement.Town.GetItemPrice(item2.EquipmentElement, mobileParty, isSelling: true);
						int num3 = ((itemPrice * amount < gold) ? amount : (gold / itemPrice));
						if (num3 > 0)
						{
							SellItemsAction.Apply(mobileParty.Party, settlement.Party, item2, num3, settlement);
							num2 += num3;
							num += itemPrice * num3;
						}
					}
				}
				if (num2 > 0 && !isNPC)
				{
					GiveGoldAction.ApplyForSettlementToCharacter(settlement, mobileParty.Party.Owner, num);
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=menu_your}Your").ToString() + ModuleStrings._space + mobileParty.Name.ToString() + ModuleStrings._space + new TextObject("{=info_guards_sellitems1}sold").ToString() + ModuleStrings._space + num2 + ModuleStrings._space + new TextObject("{=info_guards_sellitems2}items to").ToString() + ModuleStrings._space + settlement.Name.ToString() + ModuleStrings._space, Color.FromUint(ModuleColors.grey)));
				}
				hourCounterTrade = 0;
				settlementTradeTarget = null;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		protected void GiveFoodIfNeeded()
		{
			try
			{
				if (mobileParty != null && mobileParty.Party != null && mobileParty.ItemRoster != null && mobileParty.GetNumDaysForFoodToLast() <= 2)
				{
					Main.PartyManagement.GivePartyFood(mobileParty);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		protected void RemoveItemsIfOverburdened()
		{
			try
			{
				if (!(mobileParty.TotalWeightCarried >= (float)mobileParty.InventoryCapacity))
				{
					return;
				}
				for (int i = 0; i < mobileParty.ItemRoster.Count; i++)
				{
					if (!mobileParty.ItemRoster.GetItemAtIndex(i).IsFood)
					{
						mobileParty.ItemRoster.Remove(mobileParty.ItemRoster[i]);
					}
				}
				if (mobileParty.TotalWeightCarried >= (float)mobileParty.InventoryCapacity)
				{
					mobileParty.ItemRoster.Clear();
					GiveFoodIfNeeded();
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		protected bool ItemIsUnnecessary(ItemObject item)
		{
			return !item.IsFood && !ItemIsAHorse(item);
		}

		protected bool ItemIsAHorse(ItemObject item)
		{
			return item.ItemCategory == DefaultItemCategories.Horse;
		}

		protected void BuyHorses(Settlement settlement)
		{
			try
			{
				if (settlement.Town == null && mobileParty.CurrentSettlement != settlement)
				{
					return;
				}
				int num = 0;
				int num2 = 0;
				int numberOfRegularMembers = mobileParty.Party.NumberOfRegularMembers;
				ItemRoster itemRoster = settlement.Town.Owner.ItemRoster;
				for (int i = 0; i < itemRoster.Count; i++)
				{
					ItemObject itemAtIndex = itemRoster.GetItemAtIndex(i);
					if (!ItemIsAHorse(itemAtIndex))
					{
						continue;
					}
					int numberOfMounts = mobileParty.Party.NumberOfMounts;
					int itemPrice = settlement.Town.GetItemPrice(itemRoster.GetElementCopyAtIndex(i).EquipmentElement, mobileParty, isSelling: true);
					int num3 = itemRoster.GetElementCopyAtIndex(i).Amount;
					if (num3 > numberOfRegularMembers - numberOfMounts)
					{
						num3 = numberOfRegularMembers - numberOfMounts;
						if (num3 <= 0)
						{
							break;
						}
					}
					if (OwnerHasEnoughMoneyToBuy(itemPrice * num3, settlement.OwnerClan))
					{
						SellItemsAction.Apply(settlement.Town.Owner, mobileParty.Party, itemRoster.GetElementCopyAtIndex(i), num3, settlement);
						num += num3;
						num2 += itemPrice * num3;
						if (i != 0)
						{
							i--;
						}
					}
				}
				if (num > 0 && !isNPC)
				{
					GiveGoldAction.ApplyForCharacterToSettlement(mobileParty.Party.Owner, settlement, num2);
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=menu_your}Your").ToString() + ModuleStrings._space + mobileParty.Name.ToString() + ModuleStrings._space + new TextObject("{=menu_bought}bought").ToString() + ModuleStrings._space + num + ModuleStrings._space + new TextObject("{=info_guards_buyhorses}horses from").ToString() + ModuleStrings._space + settlement.Name.ToString(), Color.FromUint(ModuleColors.grey)));
				}
				hourCounterTrade = 0;
				settlementTradeTarget = null;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		protected bool WantsToSellItems()
		{
			if (hourCounterTrade >= hoursTillNextTrade)
			{
				int num = 0;
				foreach (ItemRosterElement item in mobileParty.ItemRoster)
				{
					if (ItemIsUnnecessary(item.EquipmentElement.Item))
					{
						num += item.EquipmentElement.GetBaseValue();
					}
				}
				if (num >= inventoryValueToTrade)
				{
					return true;
				}
			}
			return false;
		}

		public int WantsToBuyHorses()
		{
			int num = 0;
			if (hourCounterTrade >= hoursTillNextTrade)
			{
				int numberOfMounts = mobileParty.Party.NumberOfMounts;
				int numberOfMenWithoutHorse = mobileParty.Party.NumberOfMenWithoutHorse;
				num = numberOfMenWithoutHorse - numberOfMounts;
				if (num < 0)
				{
					num = 0;
				}
			}
			return num;
		}

		public void ReturnToBaseAggressiveness()
		{
			mobileParty.Aggressiveness = 0.9f;
		}

		public override void NextHour()
		{
			base.NextHour();
			base.RethinkNextHour = false;
			hourCounterTrade++;
			if (targetHasBeenReset)
			{
				ReturnToBaseAggressiveness();
				targetHasBeenReset = false;
			}
			List<MobileParty> list = new List<MobileParty>();
			foreach (MobileParty item in targetsWithTimer.Keys.ToList())
			{
				if (targetsWithTimer[item]++ >= 3)
				{
					targetsWithTimer.Remove(item);
				}
			}
			foreach (MobileParty item2 in list)
			{
				targetsWithTimer.Remove(item2);
			}
		}
	}
}
