using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Helpers;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.SaveSystem.Configuration;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.AI.Orders.PartyOrder
{
	public class OrderPatrol : ImprovedPartyOrder
	{
		public enum Mode
		{
			Patrol,
			Trade,
			ReturnToRegion,
			PrisonerTurnIn,
			Heal,
			ClearHideout
		}

		private Settlement settlementTarget;

		private readonly List<Settlement> BoundSettlements = new List<Settlement>();

		private Tuple<List<Settlement>, List<Settlement>> SettlementsToPatrol;

		private float patrolRadius = 60f;

		private MobileParty currentTarget;

		public Settlement hideoutTarget;

		private bool isMobileGarrison;

		private MobileGarrison mobileGarrison;

		private bool justHealedItself = false;

		public Mode CurrentMode { get; set; } = Mode.Patrol;

		protected Settlement MainPatrolSettlement { get; private set; }

		public ImprovedSettlement ImprovedSettlement { get; private set; }

		public OrderPatrol(Settlement patrolSettlement)
		{
			MainPatrolSettlement = patrolSettlement;
			ImprovedSettlement = Main.GarrisonBehavior.GetAutomatedGarrisonForSettlement(patrolSettlement);
			InitializePatrolSettlements();
			CalculatePatrolRadius();
		}

		public override void InitializeOrder(ImprovedPartyAi partyToOrder)
		{
			base.InitializeOrder(partyToOrder);
			isMobileGarrison = Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(partyToOrder.mobileParty);
			if (isMobileGarrison)
			{
				mobileGarrison = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonForParty(partyToOrder.mobileParty);
			}
		}

		public override void ExecuteOrder()
		{
			try
			{
				base.PartyToOrder.mobileParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: false);
				base.PartyToOrder.ReturnToBaseAggressiveness();
				DontRunToFar();
				SetTradeModeIfNeeded();
				switch (CurrentMode)
				{
				case Mode.Patrol:
				{
					bool flag = false;
					if (base.PartyToOrder.GetCurrentHourTimeOfCounter("PatrolCounter") <= 0)
					{
						base.PartyToOrder.AddHourCounter("PatrolCounter", 16);
						flag = true;
					}
					if ((flag || settlementTarget == null) && !base.PartyToOrder.RethinkNextHour)
					{
						if (BoundSettlements != null)
						{
							base.PartyToOrder.RethinkNextHour = true;
							if (SettlementsToPatrol.Item1.Count > 0)
							{
								Settlement settlement = SettlementsToPatrol.Item1.First();
								Main.GarrisonPartyBehavior.SetMoveGoToSettlementHelper(settlement, base.PartyToOrder.mobileParty);
								settlementTarget = settlement;
								SettlementsToPatrol.Item1.Remove(settlement);
								if (!SettlementsToPatrol.Item2.Contains(settlement))
								{
									SettlementsToPatrol.Item2.Add(settlement);
								}
							}
							else
							{
								List<Settlement> item = SettlementsToPatrol.Item2;
								SettlementsToPatrol = new Tuple<List<Settlement>, List<Settlement>>(item, new List<Settlement>());
							}
						}
					}
					else if (settlementTarget != null)
					{
						base.PartyToOrder.mobileParty.SetMovePatrolAroundSettlement(settlementTarget, base.PartyToOrder.mobileParty.NavigationCapability, isTargetingPort: false);
					}
					if (GiveDefenseOrderIfAttacked())
					{
						break;
					}
					if (base.PartyToOrder.ReplenishEnabled && !SetHealModeIfNeeded() && isMobileGarrison)
					{
						mobileGarrison.CheckForReturn();
					}
					MobileParty bestNearestHostileParty = base.PartyToOrder.GetBestNearestHostileParty(base.PartyToOrder.partySightRadius, base.PartyToOrder.mobileParty);
					if (bestNearestHostileParty != null && base.PartyToOrder.mobileParty.Aggressiveness > 0f && !base.PartyToOrder.targetsWithTimer.TryGetValue(bestNearestHostileParty, out var _))
					{
						if (bestNearestHostileParty != currentTarget)
						{
							base.PartyToOrder.ResetTarget();
							base.PartyToOrder.HourCounter = 0;
							currentTarget = bestNearestHostileParty;
							base.PartyToOrder.targetsWithTimer.Add(currentTarget, 0);
						}
						base.PartyToOrder.mobileParty.SetMoveEngageParty(bestNearestHostileParty, base.PartyToOrder.mobileParty.NavigationCapability);
					}
					if (currentTarget != null && currentTarget.IsVisible && base.PartyToOrder.MobilePartyEngageIsAllowed(currentTarget, base.PartyToOrder.mobileParty))
					{
						base.PartyToOrder.mobileParty.SetMoveEngageParty(currentTarget, base.PartyToOrder.mobileParty.NavigationCapability);
					}
					if (currentTarget != null && ((base.PartyToOrder.HourCounter > 0 && base.PartyToOrder.HourCounter % 6 == 0) || !base.PartyToOrder.MobilePartyEngageIsAllowed(currentTarget, base.PartyToOrder.mobileParty)))
					{
						base.PartyToOrder.ResetTarget();
						currentTarget = null;
						base.PartyToOrder.HourCounter = 0;
					}
					if (isMobileGarrison && mobileGarrison.ShortLife && CurrentMode == Mode.Patrol)
					{
						mobileGarrison.SetReturnMode();
					}
					break;
				}
				case Mode.PrisonerTurnIn:
					if (base.PartyToOrder.mobileParty.PrisonRoster != null && base.PartyToOrder.mobileParty.PrisonRoster.TotalManCount > 0)
					{
						Main.GarrisonPartyBehavior.SetMoveGoToSettlementHelper(MainPatrolSettlement, base.PartyToOrder.mobileParty);
					}
					else
					{
						SetPatrolMode();
					}
					GiveDefenseOrderIfAttacked();
					break;
				case Mode.ReturnToRegion:
					base.PartyToOrder.mobileParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: false);
					base.PartyToOrder.mobileParty.Aggressiveness = 0f;
					GiveDefenseOrderIfAttacked();
					if (GetDistanceToPatrolSettlement() > patrolRadius / 2f)
					{
						Main.GarrisonPartyBehavior.SetMoveGoToSettlementHelper(MainPatrolSettlement, base.PartyToOrder.mobileParty);
					}
					else
					{
						SetPatrolMode();
					}
					break;
				case Mode.Heal:
					if (!base.PartyToOrder.ReplenishEnabled)
					{
						SetPatrolMode();
						break;
					}
					base.PartyToOrder.mobileParty.Aggressiveness = 0f;
					if (base.PartyToOrder.mobileParty.CurrentSettlement != null && base.PartyToOrder.mobileParty.CurrentSettlement == MainPatrolSettlement)
					{
						if (base.PartyToOrder.NeedsHeal())
						{
							if (!GiveDefenseOrderIfAttacked())
							{
								Main.GarrisonPartyBehavior.SetMoveGoToSettlementHelper(MainPatrolSettlement, base.PartyToOrder.mobileParty);
								justHealedItself = true;
								base.PartyToOrder.mobileParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: true);
							}
							break;
						}
						if (isMobileGarrison && MainPatrolSettlement.Town.GarrisonParty != null)
						{
							List<Tuple<CharacterObject, int>> allReplenishTroops = mobileGarrison.GetAllReplenishTroops();
							if (allReplenishTroops != null && allReplenishTroops.Count > 0)
							{
								Main.GarrisonPartyBehavior.TransferTroopsFromPartyToParty(MainPatrolSettlement.Town.GarrisonParty, allReplenishTroops, base.PartyToOrder.mobileParty.Party);
								if (!base.PartyToOrder.isNPC)
								{
									InformationManager.DisplayMessage(new InformationMessage(base.PartyToOrder.mobileParty.Name.ToString() + ModuleStrings._space + new TextObject("{=party_replenished}replenished its troops.").ToString(), Color.FromUint(ModuleColors.green)));
								}
							}
						}
						if (isMobileGarrison)
						{
							bool enableHorseBuy = mobileGarrison.homeGarrisonSettings.EnableHorseBuy;
							bool enablePrisonerSell = mobileGarrison.homeGarrisonSettings.EnablePrisonerSell;
							mobileGarrison.ExecuteTrade(enablePrisonerSell, sellItems: true, enableHorseBuy);
						}
						SetPatrolMode();
						base.PartyToOrder.mobileParty.Aggressiveness = 0.9f;
						if (justHealedItself && !base.PartyToOrder.isNPC)
						{
							InformationManager.DisplayMessage(new InformationMessage(base.PartyToOrder.mobileParty.Name.ToString() + ModuleStrings._space + new TextObject("{=party_healed}healed its troops.").ToString(), Color.FromUint(ModuleColors.green)));
							justHealedItself = false;
							base.PartyToOrder.mobileParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: false);
						}
					}
					else
					{
						Main.GarrisonPartyBehavior.SetMoveGoToSettlementHelper(MainPatrolSettlement, base.PartyToOrder.mobileParty);
					}
					break;
				case Mode.Trade:
				{
					if (!isMobileGarrison)
					{
						SetPatrolMode();
						break;
					}
					bool flag2 = WantsToSellPrisoners();
					bool enableHorseBuy2 = mobileGarrison.homeGarrisonSettings.EnableHorseBuy;
					bool flag3 = false;
					if (enableHorseBuy2)
					{
						flag3 = base.PartyToOrder.WantsToBuyHorses() > 0;
					}
					if (flag2 || flag3)
					{
						if (base.PartyToOrder.mobileParty.CurrentSettlement != null && base.PartyToOrder.mobileParty.CurrentSettlement == base.PartyToOrder.settlementTradeTarget)
						{
							bool enablePrisonerSell2 = mobileGarrison.homeGarrisonSettings.EnablePrisonerSell;
							base.PartyToOrder.ExecuteTrade(enablePrisonerSell2, sellItems: true, enableHorseBuy2);
						}
						if (base.PartyToOrder.settlementTradeTarget == null)
						{
							if (!flag3)
							{
								base.PartyToOrder.settlementTradeTarget = MainPatrolSettlement;
							}
							else
							{
								base.PartyToOrder.settlementTradeTarget = base.PartyToOrder.BestNearbyTradeSettlement();
							}
						}
						Settlement settlementTradeTarget = base.PartyToOrder.settlementTradeTarget;
						if (settlementTradeTarget != null)
						{
							Main.GarrisonPartyBehavior.SetMoveGoToSettlementHelper(settlementTradeTarget, base.PartyToOrder.mobileParty);
						}
						else
						{
							SetPatrolMode();
						}
					}
					else
					{
						SetPatrolMode();
					}
					GiveDefenseOrderIfAttacked();
					break;
				}
				case Mode.ClearHideout:
					if (hideoutTarget == null || !hideoutTarget.IsHideout)
					{
						CurrentMode = Mode.Patrol;
						LogFileManager.WriteErrorLogEntry("Unepected AI behavior. Settlement was not a hideout");
					}
					else if (base.PartyToOrder.mobileParty.CurrentSettlement != null && hideoutTarget != null && base.PartyToOrder.mobileParty.CurrentSettlement == hideoutTarget)
					{
						List<MobileParty> list = new List<MobileParty>();
						foreach (MobileParty party in hideoutTarget.Parties)
						{
							if (party != base.PartyToOrder.mobileParty)
							{
								list.Add(party);
							}
						}
						if (list.Count > 0)
						{
							FieldBattleEventComponent.CreateFieldBattleEvent(base.PartyToOrder.mobileParty.Party, list.First().Party);
							list.RemoveAt(0);
							for (int num = list.Count - 1; num >= 0; num--)
							{
								MapEvent mapEvent = base.PartyToOrder.mobileParty.MapEvent;
								MethodInfo method = mapEvent.GetType().GetMethod("AddInvolvedPartyInternal", BindingFlags.Instance | BindingFlags.NonPublic);
								method.Invoke(mapEvent, new object[2]
								{
									list[num].Party,
									BattleSideEnum.Defender
								});
							}
						}
						else
						{
							hideoutTarget = null;
							CurrentMode = Mode.Patrol;
							if (!base.PartyToOrder.isNPC)
							{
								InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=menu_your}Your").ToString() + ModuleStrings._space + base.PartyToOrder.mobileParty.Name.ToString() + ModuleStrings._space + new TextObject("{=info_guards_hideoutcleared}cleared a hideout.").ToString(), Color.FromUint(ModuleColors.grey)));
							}
						}
					}
					else
					{
						Main.GarrisonPartyBehavior.SetMoveGoToSettlementHelper(hideoutTarget, base.PartyToOrder.mobileParty);
					}
					break;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private bool SetTradeModeIfNeeded()
		{
			if (CurrentMode == Mode.Patrol && isMobileGarrison)
			{
				if (WantsToSellPrisoners())
				{
					CurrentMode = Mode.Trade;
					base.PartyToOrder.settlementTradeTarget = null;
					return true;
				}
				if (mobileGarrison.homeGarrisonSettings.EnableHorseBuy && mobileGarrison.WantsToBuyHorses() > 0)
				{
					base.PartyToOrder.settlementTradeTarget = base.PartyToOrder.BestNearbyTradeSettlement();
					if (base.PartyToOrder.settlementTradeTarget != null)
					{
						CurrentMode = Mode.Trade;
						return true;
					}
				}
			}
			return false;
		}

		private bool SetMoveEngageNearestHideout()
		{
			try
			{
				Settlement nearestHideoutSettlement = base.PartyToOrder.GetNearestHideoutSettlement(base.PartyToOrder.settlementSightRadius, base.PartyToOrder.mobileParty);
				if (nearestHideoutSettlement != null)
				{
					float num = 0f;
					foreach (MobileParty party in nearestHideoutSettlement.Parties)
					{
						if (party != base.PartyToOrder.mobileParty)
						{
							if (party.Party != null && party.Party.Owner != null && party.Party.Owner.Clan != null && party.Party.Owner.Clan == base.PartyToOrder.mobileParty.Party.Owner.Clan)
							{
								return false;
							}
							num += party.Party.EstimatedStrength;
						}
					}
					if (num > 0f && base.PartyToOrder.CanDeafeat(num, base.PartyToOrder.mobileParty))
					{
						CurrentMode = Mode.ClearHideout;
						hideoutTarget = nearestHideoutSettlement;
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		private bool WantsToSellPrisoners()
		{
			if (isMobileGarrison)
			{
				bool enablePrisonerSell = mobileGarrison.homeGarrisonSettings.EnablePrisonerSell;
				if (mobileGarrison.mobileParty.PrisonRoster != null && enablePrisonerSell)
				{
					int totalManCount = mobileGarrison.mobileParty.PrisonRoster.TotalManCount;
					int num = (int)Campaign.Current.Models.PartySizeLimitModel.GetPartyPrisonerSizeLimit(mobileGarrison.mobileParty.Party).ResultNumber;
					float num2 = Math.Abs(ConfigManager.Instance.Config.GuardPrisonerSellThreshold - 1f);
					if ((float)totalManCount > (float)num * num2)
					{
						return true;
					}
				}
			}
			return false;
		}

		private float GetDistanceToPatrolSettlement()
		{
			return DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(base.PartyToOrder.mobileParty, MainPatrolSettlement, base.PartyToOrder.mobileParty.NavigationCapability);
		}

		private void DontRunToFar()
		{
			try
			{
				float distanceToPatrolSettlement = GetDistanceToPatrolSettlement();
				if (distanceToPatrolSettlement > 0f && distanceToPatrolSettlement > patrolRadius)
				{
					SetReturnToRegionMode();
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void SetReturnToRegionMode()
		{
			CurrentMode = Mode.ReturnToRegion;
		}

		private void SetPatrolMode()
		{
			CurrentMode = Mode.Patrol;
			currentTarget = null;
		}

		private bool SetHealModeIfNeeded()
		{
			try
			{
				if (isMobileGarrison)
				{
					List<Tuple<CharacterObject, int>> allReplenishTroops = mobileGarrison.GetAllReplenishTroops();
					if (allReplenishTroops != null)
					{
						int num = 0;
						int totalManCount = base.PartyToOrder.mobileParty.MemberRoster.TotalManCount;
						foreach (Tuple<CharacterObject, int> item in allReplenishTroops)
						{
							num += item.Item2;
						}
						float guardReplenishPercentage = ConfigManager.Instance.Config.GuardReplenishPercentage;
						float guardAvailableTroopsPercentage = ConfigManager.Instance.Config.GuardAvailableTroopsPercentage;
						if ((float)totalManCount < (float)base.PartyToOrder.InitialSize * guardReplenishPercentage && (float)num > (float)base.PartyToOrder.InitialSize * guardAvailableTroopsPercentage)
						{
							CurrentMode = Mode.Heal;
							return false;
						}
					}
				}
				if (base.PartyToOrder.NeedsHeal())
				{
					CurrentMode = Mode.Heal;
					return false;
				}
				return false;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return false;
			}
		}

		public bool GiveDefenseOrderIfAttacked()
		{
			Settlement settlement = CheckForBoundedAttack();
			if (settlement != null)
			{
				base.PartyToOrder.ResetTarget();
				base.PartyToOrder.GiveAndExecuteOrder(new OrderDefense(settlement));
				base.PartyToOrder.QueueNextOrder(this);
				return true;
			}
			return false;
		}

		protected Settlement CheckForBoundedAttack()
		{
			try
			{
				int num = MainPatrolSettlement.BoundVillages.Count + 1;
				if (BoundSettlements.Count == num)
				{
					foreach (Settlement boundSettlement in BoundSettlements)
					{
						if (boundSettlement.IsUnderRaid || boundSettlement.IsUnderSiege)
						{
							return boundSettlement;
						}
					}
					if (ImprovedSettlement != null)
					{
						foreach (Settlement neighbourSettlementsAndVillage in ImprovedSettlement.NeighbourSettlementsAndVillages)
						{
							if (neighbourSettlementsAndVillage.OwnerClan != ImprovedSettlement.Settlement.OwnerClan || (!neighbourSettlementsAndVillage.IsUnderRaid && !neighbourSettlementsAndVillage.IsUnderSiege))
							{
								continue;
							}
							return neighbourSettlementsAndVillage;
						}
					}
				}
				else
				{
					InitializePatrolSettlements();
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return null;
		}

		private void InitializePatrolSettlements()
		{
			List<Settlement> list = new List<Settlement>();
			List<Settlement> item = new List<Settlement>();
			foreach (Village boundVillage in MainPatrolSettlement.BoundVillages)
			{
				list.Add(boundVillage.Settlement);
				if (!BoundSettlements.Contains(boundVillage.Settlement))
				{
					BoundSettlements.Add(boundVillage.Settlement);
				}
			}
			list.Add(MainPatrolSettlement);
			if (!BoundSettlements.Contains(MainPatrolSettlement))
			{
				BoundSettlements.Add(MainPatrolSettlement);
			}
			SettlementsToPatrol = new Tuple<List<Settlement>, List<Settlement>>(list, item);
		}

		private void CalculatePatrolRadius()
		{
			float num = 0f;
			foreach (Settlement boundSettlement in BoundSettlements)
			{
				float num2 = DistanceHelper.FindClosestDistanceFromSettlementToSettlement(MainPatrolSettlement, boundSettlement, MobileParty.NavigationType.All);
				num = ((num < num2) ? num2 : num);
			}
			num += 30f;
			patrolRadius = num;
		}

		public override string GetStatusText()
		{
			string result = "";
			switch (CurrentMode)
			{
			case Mode.ClearHideout:
				result = ((!(hideoutTarget?.Name != null)) ? new TextObject("{=menu_guard_status_isattackinghideout}The guard party is attacking a hideout").ToString() : new TextObject("{=menu_guard_status_isattacking}The guard party is attacking" + ModuleStrings._space + hideoutTarget.Name).ToString());
				break;
			case Mode.Patrol:
				result = ((settlementTarget == null) ? new TextObject("{=menu_guard_status_ispatroling}The guard party is patrolling").ToString() : ((!(settlementTarget.EncyclopediaLinkWithName != null)) ? new TextObject("{=menu_guard_status_ispatrolingaroundThe guard party is patrolling aroundd" + ModuleStrings._space + settlementTarget.Name).ToString() : new TextObject("{=menu_guard_status_ispatrolingaround}The guard party is patrolling around" + ModuleStrings._space + settlementTarget.EncyclopediaLinkWithName).ToString()));
				break;
			case Mode.PrisonerTurnIn:
				result = new TextObject("{=menu_guard_status_turningprisoners}The guard party is turning in prisoners").ToString();
				break;
			case Mode.Heal:
				result = new TextObject("{=menu_guard_status_isreplenishing}The guard party is replenishing").ToString();
				break;
			case Mode.ReturnToRegion:
				result = new TextObject("{=menu_guard_status_returninghome}The guard party is returning to its home region").ToString();
				break;
			case Mode.Trade:
				result = ((settlementTarget == null) ? new TextObject("{=menu_guard_status_istrading}The guard party is trading").ToString() : ((!(settlementTarget.EncyclopediaLinkWithName != null)) ? new TextObject("{=menu_guard_status_istradingwith}The guard party is trading with" + ModuleStrings._space + settlementTarget.Name).ToString() : new TextObject("{=menu_guard_status_istradingwith}The guard party is trading with" + ModuleStrings._space + settlementTarget.EncyclopediaLinkWithName).ToString()));
				break;
			}
			return result;
		}
	}
}
