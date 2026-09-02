using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Helpers;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.AI.Orders.PartyOrder;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.ImprovedGarrisonsUI;
using ImprovedGarrisons.SaveSystem.Configuration;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using ImprovedGarrisons.Utils;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.Behaviours
{
	public class GarrisonPartyBehavior : CampaignBehaviorBase
	{
		private bool trackersInitialized = false;

		private MobileGarrison _currentMobileGarrisonForFortification;

		private static MethodInfo _removePartyMethodInfo;

		public override void RegisterEvents()
		{
			CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnGameOpen);
			CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, OnAfterGameOpened);
			CampaignEvents.AfterSettlementEntered.AddNonSerializedListener(this, OnPartyEnteredSettlement);
			CampaignEvents.TickPartialHourlyAiEvent.AddNonSerializedListener(this, PartyPartialHourlyAi);
			CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, PartyHourlyAi);
			CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, PartyDailyAi);
			CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
			CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(this, OnPartyRemoved);
			CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnPartyDestroyed);
			CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
			CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, AiHourlyTickEvent);
		}

		public override void SyncData(IDataStore dataStore)
		{
		}

		private void AiHourlyTickEvent(MobileParty party, PartyThinkParams para)
		{
		}

		private void OnGameOpen(CampaignGameStarter campaignGameStarter)
		{
			try
			{
				Main.GarrisonBehavior.OnGameOpen(campaignGameStarter);
				AddDialog(campaignGameStarter);
				if (ConfigManager.Instance.Config.ActivateDeleteAllModsPartiesMode)
				{
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=misc_deletepartiesmode}Delete all Improved Garrisons parties mode has been activated and will now be executed.").ToString(), Color.FromUint(ModuleColors.modMainColor)));
					OnGameStartDeleteAllIGParties();
				}
				else
				{
					OnGameStartSetAllIGParties();
				}
				if (!ConfigManager.Instance.Config.EnableMapBannerTracker)
				{
					return;
				}
				Action action = delegate
				{
					if (MapScreen.Instance != null)
					{
						Main.PartyManagement.TrackAllImprovedGarrisonparties();
						trackersInitialized = true;
						Main.RemoveActionToExecuteEachTick("tryTrackAllParties");
					}
				};
				Main.AddActionToExecuteEachTick("tryTrackAllParties", action);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void OnGameTick(float tick)
		{
		}

		private void OnAfterGameOpened(CampaignGameStarter campaignGameStarter)
		{
			try
			{
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void PartyPartialHourlyAi(MobileParty party)
		{
			try
			{
				Main.PartyManagement.ExecutePartialHourlyAi(party);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void PartyPartialHourlyAi()
		{
			try
			{
				Main.PartyManagement.ExecutePartialHourlyAi();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void PartyHourlyAi()
		{
			try
			{
				Main.PartyManagement.ExecuteHourlyAi();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void PartyDailyAi(MobileParty party)
		{
		}

		public void OnPartyDestroyed(MobileParty party, PartyBase partyBase)
		{
			try
			{
				if (party == null || !(party.Name != null) || Main.PartyManagement.villageRecruitPartyManagement.IsImprovedGarrisonVillageRecruitParty(party))
				{
					return;
				}
				if (Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(party))
				{
					if (Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonHome(party) == null || Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonHome(party).OwnerClan == Hero.MainHero.Clan)
					{
						TextObject textObject = new TextObject("{=party_destroyed_new}Your" + ModuleStrings._space + party.Name.ToString() + ModuleStrings._space + new TextObject("{=party_destroyed_new2}have been destroyed.").ToString());
						MBInformationManager.AddQuickInformation(textObject);
						InformationManager.DisplayMessage(new InformationMessage(textObject.ToString(), Color.FromUint(ModuleColors.yellow)));
					}
				}
				else if (Main.PartyManagement.transferPartyManagement.IsTransferParty(party))
				{
					TextObject textObject2 = new TextObject("{=party_destroyed_new}Your" + ModuleStrings._space + party.Name.ToString() + ModuleStrings._space + new TextObject("{=party_destroyed_new2}have been destroyed.").ToString());
					MBInformationManager.AddQuickInformation(textObject2);
					InformationManager.DisplayMessage(new InformationMessage(textObject2.ToString(), Color.FromUint(ModuleColors.yellow)));
				}
				else if (Main.PartyManagement.garrisonRecruiterPartyManagement.IsRecruiterParty(party))
				{
					TextObject textObject3 = new TextObject("{=party_destroyed_new}Your" + ModuleStrings._space + party.Name.ToString() + ModuleStrings._space + new TextObject("{=party_destroyed_new2}have been destroyed.").ToString());
					MBInformationManager.AddQuickInformation(textObject3);
					InformationManager.DisplayMessage(new InformationMessage(textObject3.ToString(), Color.FromUint(ModuleColors.yellow)));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void OnPartyRemoved(PartyBase party)
		{
			try
			{
				if (party == null || party.MobileParty == null)
				{
					return;
				}
				if (Main.PartyManagement.villageRecruitPartyManagement.IsImprovedGarrisonVillageRecruitParty(party.MobileParty))
				{
					Main.PartyManagement.villageRecruitPartyManagement.VillageRecruitParties.Remove(party.MobileParty);
				}
				else if (Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(party.MobileParty))
				{
					List<MobileGarrison> list = new List<MobileGarrison>();
					foreach (KeyValuePair<string, MobileGarrison> mobileGarrison in Main.PartyManagement.mobileGarrisonManagement.MobileGarrisons)
					{
						if (mobileGarrison.Value.getMobileParty() == party.MobileParty)
						{
							list.Add(mobileGarrison.Value);
						}
					}
					foreach (MobileGarrison item in list)
					{
						item.RemoveMobileGarrison(forceRemove: false);
					}
					Main.PartyManagement.UntrackPartyWithBanner(party.MobileParty);
				}
				else if (Main.PartyManagement.transferPartyManagement.IsTransferParty(party.MobileParty) && Main.PartyManagement.transferPartyManagement.TransferParties.ContainsKey(party.MobileParty))
				{
					Main.PartyManagement.transferPartyManagement.TransferParties.Remove(party.MobileParty);
					Main.PartyManagement.UntrackPartyWithBanner(party.MobileParty);
				}
				else if (Main.PartyManagement.garrisonRecruiterPartyManagement.IsRecruiterParty(party.MobileParty))
				{
					Main.PartyManagement.garrisonRecruiterPartyManagement.GarrisonRecruiterParties.Remove(party.MobileParty);
					Main.PartyManagement.UntrackPartyWithBanner(party.MobileParty);
				}
			}
			catch (Exception ex)
			{
				InformationManager.DisplayMessage(new InformationMessage("Improved Garrisons: Could not remove party please restart the game. See moduledata/errorlog.xml for more."));
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void OnPartyEnteredSettlement(MobileParty party, Settlement settlement, Hero hero)
		{
			try
			{
				bool flag = party != null;
				bool flag2 = settlement != null;
				if (!(flag && flag2))
				{
					return;
				}
				bool flag3 = party.StringId != null;
				bool flag4 = settlement.IsTown || settlement.IsCastle;
				bool flag5 = party.HomeSettlement == settlement;
				bool flag6 = party == MobileParty.MainParty;
				if (flag4 && flag6 && settlement.Town.OwnerClan == Hero.MainHero.Clan)
				{
					UIManager.Instance.improvedGarrisonsUI.ChangeSelectorSelectionToCurrentSettlement();
					if (!ConfigManager.Instance.Config.DeactivateTutorial)
					{
						UIManager.Instance.StartTutorial();
					}
				}
				bool flag7 = Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(party);
				if (!(flag3 && flag4 && flag7))
				{
					return;
				}
				Settlement mobileGarrisonHome = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonHome(party);
				if (mobileGarrisonHome != null)
				{
					if (Main.PartyManagement.mobileGarrisonManagement.MobileGarrisons.TryGetValue(mobileGarrisonHome.StringId, out var value))
					{
						if (value.CurrentOrder is OrderMergeGarrison)
						{
							value.SellItems(settlement, onlyNecessary: false);
							Main.PartyManagement.RecruitMobilePartyToGarrison(party, settlement);
							SellPrisoners(party, settlement.Town);
						}
						OrderPatrol orderPatrol = value.CurrentOrder as OrderPatrol;
						if (orderPatrol != null && orderPatrol.CurrentMode == OrderPatrol.Mode.PrisonerTurnIn)
						{
							PutPrisonersIntoDungeon(party, settlement.Town);
						}
						if (orderPatrol != null && orderPatrol.CurrentMode == OrderPatrol.Mode.Trade)
						{
							bool enablePrisonerSell = value.homeGarrisonSettings.EnablePrisonerSell;
							bool enableHorseBuy = value.homeGarrisonSettings.EnableHorseBuy;
							value.ExecuteTrade(enablePrisonerSell, sellItems: true, enableHorseBuy);
						}
					}
				}
				else
				{
					Main.PartyManagement.RecruitMobilePartyToGarrison(party, settlement);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void OnSettlementOwnerChanged(Settlement settlement, bool b, Hero x, Hero y, Hero z, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail details)
		{
			try
			{
				if (settlement == null || x == null || x.Clan == null || y == null || y.Clan == null || x.Clan == y.Clan)
				{
					return;
				}
				List<MobileParty> list = new List<MobileParty>();
				foreach (KeyValuePair<MobileParty, Hero> transferParty in Main.PartyManagement.transferPartyManagement.TransferParties)
				{
					if (transferParty.Key.HomeSettlement == null || transferParty.Key.HomeSettlement != settlement)
					{
						continue;
					}
					if (transferParty.Value != null)
					{
						Settlement bestGarrisonToReturnTo = GetBestGarrisonToReturnTo(transferParty.Key);
						transferParty.Key.SetCustomHomeSettlement(bestGarrisonToReturnTo);
						if (bestGarrisonToReturnTo != null)
						{
							if (!TrySetPartyMoveToSettlement(transferParty.Key, bestGarrisonToReturnTo))
							{
								list.Add(transferParty.Key);
							}
						}
						else
						{
							list.Add(transferParty.Key);
						}
					}
					else
					{
						list.Add(transferParty.Key);
					}
				}
				foreach (KeyValuePair<string, MobileGarrison> item in Main.PartyManagement.mobileGarrisonManagement.MobileGarrisons.ToList())
				{
					if (item.Value.fromSettlement != null && item.Value.fromSettlement == settlement)
					{
						Settlement bestGarrisonToReturnTo2 = GetBestGarrisonToReturnTo(item.Value.mobileParty);
						if (bestGarrisonToReturnTo2 != null)
						{
							item.Value.fromSettlement = bestGarrisonToReturnTo2;
							item.Value.mobileParty.StringId += "_returning";
							item.Value.SetReturnMode();
						}
						else
						{
							list.Add(item.Value.mobileParty);
						}
					}
				}
				foreach (KeyValuePair<MobileParty, GarrisonRecruiter> garrisonRecruiterParty in Main.PartyManagement.garrisonRecruiterPartyManagement.GarrisonRecruiterParties)
				{
					if (garrisonRecruiterParty.Key.HomeSettlement != null && garrisonRecruiterParty.Key.HomeSettlement == settlement)
					{
						Settlement bestGarrisonToReturnTo3 = GetBestGarrisonToReturnTo(garrisonRecruiterParty.Value.mobileParty);
						if (bestGarrisonToReturnTo3 != null)
						{
							garrisonRecruiterParty.Value.fromSettlement = bestGarrisonToReturnTo3;
							garrisonRecruiterParty.Value.mobileParty.StringId += "_returning";
							garrisonRecruiterParty.Value.SetReturnMode();
						}
						else
						{
							list.Add(garrisonRecruiterParty.Value.mobileParty);
						}
					}
				}
				foreach (MobileParty item2 in list)
				{
					Main.GarrisonPartyBehavior.RemovePartyHelper(item2);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void OnMapEventStarted(MapEvent mapEvent, PartyBase leftParty, PartyBase rightParty)
		{
			try
			{
				if (mapEvent == null || leftParty == null || rightParty == null || PartyBase.MainParty == null)
				{
					return;
				}
				if (mapEvent.IsPlayerMapEvent)
				{
					PartyBase mainParty = PartyBase.MainParty;
					Clan clan = mainParty.Owner.Clan;
					BattleSideEnum playerSide = mapEvent.PlayerSide;
					MapEventSide mapEventSide = ((playerSide != BattleSideEnum.Attacker) ? mapEvent.DefenderSide : mapEvent.AttackerSide);
					List<MobileParty> allNearNearbyParties = Main.PartyManagement.GetAllNearNearbyParties(mapEvent.Position, 5f);
					{
						foreach (MobileParty item in allNearNearbyParties)
						{
							if (Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(item) && item.Party.Owner != null && item.Party.Owner.Clan == clan)
							{
								MethodInfo method = mapEventSide.GetType().GetMethod("AddNearbyPartyToPlayerMapEvent", BindingFlags.Instance | BindingFlags.NonPublic);
								method.Invoke(mapEventSide, new object[1] { item });
							}
						}
						return;
					}
				}
				MobileParty mobileParty = null;
				if (Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(leftParty.MobileParty))
				{
					mobileParty = leftParty.MobileParty;
				}
				else if (Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(rightParty.MobileParty))
				{
					mobileParty = rightParty.MobileParty;
				}
				if (mobileParty != null)
				{
					Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonForParty(mobileParty)?.SaveRosterBeforeFight();
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void SellPrisoners(MobileParty party, Town town)
		{
			try
			{
				if (party.PrisonRoster != null && party.PrisonRoster.Count > 0)
				{
					SellPrisonersAction.ApplyForAllPrisoners(party.Party, town.Settlement.Party);
					MobileGarrison mobileGarrisonForParty = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonForParty(party);
					if (mobileGarrisonForParty != null && !mobileGarrisonForParty.isNPC)
					{
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=menu_your}Your").ToString() + ModuleStrings._space + party.Name.ToString() + ModuleStrings._space + new TextObject("{=info_guards_sell}sold its prisoners.").ToString(), Color.FromUint(ModuleColors.grey)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void PutPrisonersIntoDungeon(MobileParty party, Town town)
		{
			try
			{
				int num = 0;
				List<TroopRosterElement> list = new List<TroopRosterElement>();
				foreach (TroopRosterElement item in party.PrisonRoster.GetTroopRoster())
				{
					if (!item.Character.IsHero)
					{
						int num2 = item.Number - item.WoundedNumber;
						if (num2 < 0)
						{
							num2 = 0;
						}
						town.Settlement.Party.AddPrisoner(item.Character, num2);
						list.Add(item);
						num += num2 + item.WoundedNumber;
					}
				}
				foreach (TroopRosterElement item2 in list)
				{
					if (item2.Character.IsHero)
					{
						TransferPrisonerAction.Apply(item2.Character, party.Party, town.Settlement.Party);
						num++;
					}
					else
					{
						party.PrisonRoster.RemoveTroop(item2.Character, item2.Number);
					}
				}
				if (town.Owner != null && town.Owner.Owner != null && town.Owner.Owner == Hero.MainHero)
				{
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=menu_your}Your").ToString() + ModuleStrings._space + party.Name.ToString() + ModuleStrings._space + new TextObject("{=info_guards_prisoners_put}put").ToString() + ModuleStrings._space + num + ModuleStrings._space + new TextObject("{=info_guards_prisoners_intodungeon}prisoners into the dungeon.").ToString(), Color.FromUint(ModuleColors.green)));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public bool TransferTroopsFromPartyToParty(MobileParty party, List<Tuple<CharacterObject, int>> troops, PartyBase partyToTransferTo)
		{
			try
			{
				if (party != null && partyToTransferTo != null)
				{
					foreach (Tuple<CharacterObject, int> troop in troops)
					{
						int num = troop.Item2;
						CharacterObject item = troop.Item1;
						int troopCount = party.MemberRoster.GetTroopCount(item);
						if (troopCount == 0)
						{
							return false;
						}
						if (troopCount - num < 0)
						{
							num = troopCount;
						}
						partyToTransferTo.MobileParty.MemberRoster.AddToCounts(item, num);
						party.MemberRoster.RemoveTroop(item, num);
					}
					return true;
				}
				return false;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return false;
			}
		}

		public void OnGameStartDeleteAllIGParties()
		{
			try
			{
				if (Campaign.Current != null)
				{
					foreach (MobileParty item in Campaign.Current.MobileParties.ToList())
					{
						if (Main.PartyManagement.villageRecruitPartyManagement.IsImprovedGarrisonVillageRecruitParty(item))
						{
							Main.GarrisonPartyBehavior.RemovePartyHelper(item);
						}
						else if (Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(item))
						{
							Main.GarrisonPartyBehavior.RemovePartyHelper(item);
						}
						else if (Main.PartyManagement.transferPartyManagement.IsTransferParty(item))
						{
							Main.GarrisonPartyBehavior.RemovePartyHelper(item);
						}
						else if (Main.PartyManagement.garrisonRecruiterPartyManagement.IsRecruiterParty(item))
						{
							Main.GarrisonPartyBehavior.RemovePartyHelper(item);
						}
					}
				}
				ConfigManager.Instance.Config.ActivateDeleteAllModsPartiesMode = false;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ReturnAllIGParties()
		{
			try
			{
				if (Campaign.Current == null)
				{
					return;
				}
				foreach (MobileParty item in Campaign.Current.MobileParties.ToList())
				{
					if (Main.PartyManagement.villageRecruitPartyManagement.IsImprovedGarrisonVillageRecruitParty(item))
					{
						continue;
					}
					if (Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(item))
					{
						MobileGarrison mobileGarrisonForParty = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonForParty(item);
						if (mobileGarrisonForParty != null)
						{
							mobileGarrisonForParty.SetReturnMode();
						}
						else
						{
							Main.GarrisonPartyBehavior.RemovePartyHelper(item);
						}
					}
					else if (!Main.PartyManagement.transferPartyManagement.IsTransferParty(item) && Main.PartyManagement.garrisonRecruiterPartyManagement.IsRecruiterParty(item))
					{
						GarrisonRecruiter recruiterForParty = Main.PartyManagement.garrisonRecruiterPartyManagement.GetRecruiterForParty(item);
						if (recruiterForParty != null)
						{
							recruiterForParty.SetReturnMode();
						}
						else
						{
							Main.GarrisonPartyBehavior.RemovePartyHelper(item);
						}
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void OnGameStartSetAllIGParties()
		{
			try
			{
				List<MobileParty> list = new List<MobileParty>();
				List<MobileParty> list2 = new List<MobileParty>();
				if (Campaign.Current != null)
				{
					foreach (MobileParty mobileParty in Campaign.Current.MobileParties)
					{
						if (Main.PartyManagement.villageRecruitPartyManagement.IsImprovedGarrisonVillageRecruitParty(mobileParty))
						{
							Main.PartyManagement.villageRecruitPartyManagement.VillageRecruitParties.Add(mobileParty);
							SetPartyOwner(mobileParty);
						}
						else if (Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(mobileParty))
						{
							list.Add(mobileParty);
							SetPartyOwner(mobileParty);
						}
						else if (Main.PartyManagement.transferPartyManagement.IsTransferParty(mobileParty))
						{
							Main.PartyManagement.transferPartyManagement.TransferParties.Add(mobileParty, mobileParty.Party.Owner);
							SetPartyOwner(mobileParty);
						}
						else if (Main.PartyManagement.garrisonRecruiterPartyManagement.IsRecruiterParty(mobileParty))
						{
							list2.Add(mobileParty);
							SetPartyOwner(mobileParty);
						}
					}
				}
				foreach (MobileParty item in list)
				{
					Main.PartyManagement.mobileGarrisonManagement.GiveMobilePartyAMobileGarrison(item);
					item.SetPartyUsedByQuest(isActivelyUsed: true);
				}
				foreach (MobileParty item2 in list2)
				{
					Main.PartyManagement.garrisonRecruiterPartyManagement.GiveMobilePartyARecruiter(item2);
					item2.SetPartyUsedByQuest(isActivelyUsed: true);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void SetPartyOwner(MobileParty party)
		{
			if (party.Party.Owner == null)
			{
				if (party.HomeSettlement != null && party.HomeSettlement.Owner != null)
				{
					party.Party.SetCustomOwner(party.HomeSettlement.Owner);
				}
				else
				{
					Main.GarrisonPartyBehavior.RemovePartyHelper(party);
				}
			}
		}

		private void AddDialog(CampaignGameStarter starter)
		{
			try
			{
				starter.AddDialogLine("improvedgarrison_recruit_talk_start", "start", "improvedgarrison_recruit_talk", new TextObject("{=dialog_recruit_start}Greetings, my Lord, we are the new garrison recruits heading to your settlement!").ToString(), ImprovedGarrison_recruit_talk_start_on_condition, null);
				starter.AddDialogLine("improvedgarrison_recruit_talk_start", "start", "improvedgarrison_recruit_talk", new TextObject("{=dialog_recruit_neutral_start}Greetings, we are the new recruits, we will help keeping our home safe!").ToString(), ImprovedGarrison_recruit_talk_start_on_neutral_condition, null);
				starter.AddPlayerLine("improvedgarrison_recruit_talk_leave", "improvedgarrison_recruit_talk", "close_window", new TextObject("{=dialog_end_nice}Carry on, then. Farewell.").ToString(), null, Conversation_improvedgarrison_recruit_leave_on_consequence);
				starter.AddDialogLine("improvedgarrison_transferparty_talk_start", "start", "improvedgarrison_transferparty_talk", new TextObject("{=dialog_transfer_start}Greetings, my Lord, we are the garrison transfer party!").ToString(), ImprovedGarrison_transferparty_talk_start_on_condition, null);
				starter.AddPlayerLine("improvedgarrison_transferparty_talk_leave", "improvedgarrison_transferparty_talk", "close_window", new TextObject("{=dialog_end_nice}Carry on, then. Farewell.").ToString(), null, Conversation_improvedgarrison_transferparty_leave_on_consequence);
				starter.AddDialogLine("improvedgarrison_mobilegarrison_talk_start", "start", "improvedgarrison_mobilegarrison_talk", new TextObject("{=dialog_guard_start}Greetings, my Lord! How can we be of service?").ToString(), ImprovedGarrison_mobilegarrison_talk_start_on_condition, null);
				starter.AddDialogLine("improvedgarrison_mobilegarrison_pretalk_start", "improvedgarrison_mobilegarrison_inspect_pretalk", "close_window", new TextObject("{=dialog_guard_pretalk}It's a pleasure serving you, my Lord.").ToString(), null, null);
				starter.AddPlayerLine("improvedgarrison_mobilegarrison_talk_inspect", "improvedgarrison_mobilegarrison_talk", "improvedgarrison_mobilegarrison_inspect_pretalk", new TextObject("{=dialog_guard_inspect}Let me inspect your troops.").ToString(), null, Conversation_improvedgarrison_mobilegarrison_inspect_on_consequence);
				starter.AddDialogLine("improvedgarrison_mobilegarrison_pretalk_start", "improvedgarrison_mobilegarrison_transfer_pretalk", "close_window", new TextObject("{=dialog_guard_fortify_answer}Very well, we will reinforce this location.").ToString(), null, null);
				starter.AddPlayerLine("improvedgarrison_mobilegarrison_talk_transfer", "improvedgarrison_mobilegarrison_talk", "improvedgarrison_mobilegarrison_transfer_pretalk", new TextObject("{=dialog_guard_fortify}Reinforce a garrison.").ToString(), null, Conversation_improvedgarrison_mobilegarrison_fortify_on_consequence);
				starter.AddDialogLine("improvedgarrison_mobilegarrison_pretalk_start", "improvedgarrison_mobilegarrison_return_pretalk", "close_window", new TextObject("{=dialog_guard_return_answer}Very well, we will return home at once, my Lord!").ToString(), null, null);
				starter.AddPlayerLine("improvedgarrison_mobilegarrison_talk_return", "improvedgarrison_mobilegarrison_talk", "improvedgarrison_mobilegarrison_return_pretalk", new TextObject("{=dialog_guard_return}Return to your garrison.").ToString(), null, Conversation_improvedgarrison_mobilegarrison_return_on_consequence);
				starter.AddDialogLine("improvedgarrison_mobilegarrison_pretalk_start", "improvedgarrison_mobilegarrison_escort_pretalk", "close_window", new TextObject("{=dialog_guard_escort_answer}Very well, we will protect you with our life, my Lord!").ToString(), null, null);
				starter.AddPlayerLine("improvedgarrison_mobilegarrison_talk_escort", "improvedgarrison_mobilegarrison_talk", "improvedgarrison_mobilegarrison_escort_pretalk", new TextObject("{=dialog_guard_escort}Fight by my side!").ToString(), null, Conversation_improvedgarrison_mobilegarrison_escort_on_consequence);
				starter.AddDialogLine("improvedgarrison_mobilegarrison_pretalk_start", "improvedgarrison_mobilegarrison_patrol_pretalk", "close_window", new TextObject("{=dialog_guard_patrol_answer}Very well, we will protect your lands, my Lord!").ToString(), null, null);
				starter.AddPlayerLine("improvedgarrison_mobilegarrison_talk_patrol", "improvedgarrison_mobilegarrison_talk", "improvedgarrison_mobilegarrison_patrol_pretalk", new TextObject("{=dialog_guard_patrol}Patrol this region!").ToString(), null, Conversation_improvedgarrison_mobilegarrison_patrol_on_consequence);
				starter.AddPlayerLine("improvedgarrison_mobilegarrison_talk_leave", "improvedgarrison_mobilegarrison_talk", "close_window", new TextObject("{=dialog_end_nice}Carry on, then. Farewell.").ToString(), null, Conversation_improvedgarrison_mobilegarrison_leave_on_consequence);
				starter.AddDialogLine("improvedgarrison_npcmobilegarrison_talk_start_nice", "start", "improvedgarrison_npcmobilegarrison_talk", new TextObject("{=dialog_npcguard_neutral_start}Greetings! We are the guards patrolling this region.").ToString(), () => IsNPCMobileGarrison() && !IsHostileParty(), null);
				starter.AddPlayerLine("improvedgarrison_npcmobilegarrison_talk_leave", "improvedgarrison_npcmobilegarrison_talk", "close_window", new TextObject("{=dialog_npcguard_hostile_fight}Surrender or die!").ToString(), () => IsNPCMobileGarrison() && !IsHostileParty(), conversation_fight_on_consequence);
				starter.AddPlayerLine("improvedgarrison_npcmobilegarrison_talk_leave", "improvedgarrison_npcmobilegarrison_talk", "close_window", new TextObject("{=dialog_end_niceCarry on, then. Farewell..").ToString(), () => IsNPCMobileGarrison() && !IsHostileParty(), Conversation_improvedgarrison_mobilegarrison_leave_on_consequence);
				starter.AddDialogLine("improvedgarrison_npcmobilegarrison_talk_start_hostile", "start", "improvedgarrison_npcmobilegarrison_talk", new TextObject("{=dialog_npcguard_hostile_startplayer}We are the guards patrolling this region.").ToString(), () => IsNPCMobileGarrison() && IsHostileParty() && PlayerHasEngaged(), null);
				starter.AddPlayerLine("improvedgarrison_npcmobilegarrison_talk_start_hostile_notplayer", "start", "close_window", new TextObject("{=dialog_npcguard_hostile_startnpc}We are the guards patrolling this region. You know we are at war! Trespassing is not allowed here.").ToString(), () => IsNPCMobileGarrison() && IsHostileParty() && !PlayerHasEngaged(), conversation_fight_on_consequence);
				starter.AddDialogLine("improvedgarrison_recruiter_talk_start", "start", "improvedgarrison_recruiter_talk", new TextObject("{=dialog_recruiter_start}Greetings, my Lord, we are your garrison recruiters!").ToString(), ImprovedGarrison_recruiter_talk_start_on_condition, null);
				starter.AddDialogLine("improvedgarrison_recruiter_pretalk_start", "improvedgarrison_recruiter_continue_pretalk", "close_window", new TextObject("{=dialog_guard_pretalkIt's a pleasure serving you, my Lord..").ToString(), null, null);
				starter.AddPlayerLine("improvedgarrison_recruiter_talk_inspect", "improvedgarrison_recruiter_talk", "improvedgarrison_recruiter_continue_pretalk", new TextObject("{=dialog_recruiter_inspect}Let me inspect your troops.").ToString(), null, Conversation_improvedgarrison_recruiter_inspect_on_consequence);
				starter.AddDialogLine("improvedgarrison_recruiter_pretalk_start", "improvedgarrison_recruiter_changeculture_pretalk", "improvedgarrison_recruiter_talk", new TextObject("{=dialog_recruiter_culturechange_answer}Very well, we will change our recruitment to another culture!").ToString(), null, null);
				starter.AddPlayerLine("improvedgarrison_recruiter_talk_changeculture", "improvedgarrison_recruiter_talk", "improvedgarrison_recruiter_changeculture_pretalk", new TextObject("{=dialog_recruiter_culturechange}Change your recruitment culture.").ToString(), null, Conversation_improvedgarrison_recruiter_changeCulture_on_consequence);
				starter.AddDialogLine("improvedgarrison_recruiter_pretalk_start", "improvedgarrison_recruiter_return_pretalk", "close_window", new TextObject("{=dialog_guard_return_anVery well, we will return home at once, my Lord!lord!").ToString(), null, null);
				starter.AddPlayerLine("improvedgarrison_recruiter_talk_return", "improvedgarrison_recruiter_talk", "improvedgarrison_recruiter_return_pretalk", new TextObject("{=dialog_guard_return}Return to your garrison.").ToString(), null, Conversation_improvedgarrison_recruiter_return_on_consequence);
				starter.AddPlayerLine("improvedgarrison_recruiter_talk_leave", "improvedgarrison_recruiter_talk", "close_window", new TextObject("{=dialog_end_nice}Carry on, then. Farewell.").ToString(), null, Conversation_improvedgarrison_recruiter_leave_on_consequence);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void conversation_fight_on_consequence()
		{
			BeHostileAction.ApplyEncounterHostileAction(PartyBase.MainParty, MobileParty.ConversationParty.Party);
		}

		private bool TryToLeaveCondition()
		{
			try
			{
				return PlayerEncounter.EncounteredParty != null && PlayerEncounter.EncounteredParty.CalculateCurrentStrength() <= PartyBase.MainParty.CalculateCurrentStrength();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		private bool PlayerHasEngaged()
		{
			try
			{
				return PlayerEncounter.PlayerIsAttacker;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		private bool IsHostileParty()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				if (encounteredParty != null && encounteredParty.MobileParty != null)
				{
					bool flag = encounteredParty.MobileParty.IsInitialized && encounteredParty.MobileParty.IsMoving;
					bool flag2 = encounteredParty.MapFaction.IsAtWarWith(MobileParty.MainParty.MapFaction);
					return flag2 && flag;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		private bool IsStuckInMainParty()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				MobileParty mainParty = MobileParty.MainParty;
				if (encounteredParty != null && encounteredParty.MobileParty != null && mainParty != null && encounteredParty.MobileParty.GetPosition2D == mainParty.GetPosition2D)
				{
					return true;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		private bool IsNPCMobileGarrison()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				if (encounteredParty != null && encounteredParty.MobileParty != null)
				{
					MobileGarrison mobileGarrisonForParty = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonForParty(encounteredParty.MobileParty);
					if (mobileGarrisonForParty != null && PlayerEncounter.Current != null && Campaign.Current.CurrentConversationContext == ConversationContext.PartyEncounter && encounteredParty.IsMobile && mobileGarrisonForParty.isNPC)
					{
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		private void Conversation_improvedgarrison_recruiter_changeCulture_on_consequence()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				if (encounteredParty != null && encounteredParty.MobileParty != null)
				{
					GarrisonRecruiter recruiterForParty = Main.PartyManagement.garrisonRecruiterPartyManagement.GetRecruiterForParty(encounteredParty.MobileParty);
					if (recruiterForParty != null)
					{
						RecruitmentSettings.Instance.PromptChangeRecruitmentCulture(recruiterForParty.fromSettlement.Town);
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private bool ImprovedGarrison_recruit_talk_start_on_condition()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				if (encounteredParty != null && encounteredParty.MobileParty != null)
				{
					bool flag = Main.PartyManagement.villageRecruitPartyManagement.IsImprovedGarrisonVillageRecruitParty(encounteredParty.MobileParty);
					bool flag2 = encounteredParty.MobileParty.ActualClan == Hero.MainHero.Clan;
					if (flag && PlayerEncounter.Current != null && Campaign.Current.CurrentConversationContext == ConversationContext.PartyEncounter && encounteredParty.IsMobile && flag2)
					{
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		private bool ImprovedGarrison_recruit_talk_start_on_neutral_condition()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				if (encounteredParty != null && encounteredParty.MobileParty != null)
				{
					bool flag = Main.PartyManagement.villageRecruitPartyManagement.IsImprovedGarrisonVillageRecruitParty(encounteredParty.MobileParty);
					bool flag2 = encounteredParty.MobileParty.ActualClan != Hero.MainHero.Clan;
					if (flag && PlayerEncounter.Current != null && Campaign.Current.CurrentConversationContext == ConversationContext.PartyEncounter && encounteredParty.IsMobile && flag2)
					{
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		private void Conversation_improvedgarrison_recruit_leave_on_consequence()
		{
			PlayerEncounter.LeaveEncounter = true;
		}

		private bool ImprovedGarrison_transferparty_talk_start_on_condition()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				if (encounteredParty != null && encounteredParty.MobileParty != null && Main.PartyManagement.transferPartyManagement.IsTransferParty(encounteredParty.MobileParty) && PlayerEncounter.Current != null && Campaign.Current.CurrentConversationContext == ConversationContext.PartyEncounter && encounteredParty.IsMobile)
				{
					return true;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		private void Conversation_improvedgarrison_transferparty_leave_on_consequence()
		{
			PlayerEncounter.LeaveEncounter = true;
		}

		private bool ImprovedGarrison_recruiter_talk_start_on_condition()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				if (encounteredParty != null && encounteredParty.MobileParty != null && Main.PartyManagement.garrisonRecruiterPartyManagement.IsRecruiterParty(encounteredParty.MobileParty) && PlayerEncounter.Current != null && Campaign.Current.CurrentConversationContext == ConversationContext.PartyEncounter && encounteredParty.IsMobile)
				{
					return true;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		private void Conversation_improvedgarrison_recruiter_leave_on_consequence()
		{
			PlayerEncounter.LeaveEncounter = true;
		}

		private bool ImprovedGarrison_mobilegarrison_talk_start_on_condition()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				if (encounteredParty != null && encounteredParty.MobileParty != null)
				{
					MobileGarrison mobileGarrisonForParty = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonForParty(encounteredParty.MobileParty);
					bool flag = mobileGarrisonForParty != null;
					bool flag2 = IsStuckInMainParty();
					if (flag && !flag2 && PlayerEncounter.Current != null && Campaign.Current.CurrentConversationContext == ConversationContext.PartyEncounter && encounteredParty.IsMobile && !mobileGarrisonForParty.isNPC)
					{
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		private void Conversation_improvedgarrison_mobilegarrison_escort_on_consequence()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				if (encounteredParty.MobileParty != null && encounteredParty.MobileParty.HomeSettlement != null)
				{
					Settlement mobileGarrisonHome = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonHome(encounteredParty.MobileParty);
					if (mobileGarrisonHome != null && Main.PartyManagement.mobileGarrisonManagement.MobileGarrisons.TryGetValue(mobileGarrisonHome.StringId, out var value))
					{
						value.GiveAndExecuteOrder(new OrderEscort(Hero.MainHero.PartyBelongedTo));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			PlayerEncounter.LeaveEncounter = true;
		}

		private void Conversation_improvedgarrison_mobilegarrison_showloot_on_consequence()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				if (encounteredParty.MobileParty == null)
				{
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			PlayerEncounter.LeaveEncounter = true;
		}

		private void Conversation_improvedgarrison_mobilegarrison_patrol_on_consequence()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				if (encounteredParty.MobileParty != null)
				{
					Settlement mobileGarrisonHome = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonHome(encounteredParty.MobileParty);
					if (mobileGarrisonHome != null && Main.PartyManagement.mobileGarrisonManagement.MobileGarrisons.TryGetValue(mobileGarrisonHome.StringId, out var value))
					{
						value.GiveAndExecuteOrder(new OrderPatrol(mobileGarrisonHome));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			PlayerEncounter.LeaveEncounter = true;
		}

		private void Conversation_improvedgarrison_mobilegarrison_inspect_on_consequence()
		{
			PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
			if (encounteredParty.MobileParty != null && encounteredParty.MobileParty.HomeSettlement != null)
			{
				Settlement mobileGarrisonHome = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonHome(encounteredParty.MobileParty);
				if (Main.PartyManagement.mobileGarrisonManagement.MobileGarrisons.TryGetValue(mobileGarrisonHome.StringId, out var _))
				{
					Main.PartyManagement.PromptPartyManagementMenu(encounteredParty, MobileParty.MainParty);
				}
			}
			PlayerEncounter.LeaveEncounter = true;
		}

		private void Conversation_improvedgarrison_recruiter_inspect_on_consequence()
		{
			PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
			if (encounteredParty.MobileParty != null && encounteredParty.MobileParty.HomeSettlement != null)
			{
				GarrisonRecruiter recruiterForParty = Main.PartyManagement.garrisonRecruiterPartyManagement.GetRecruiterForParty(encounteredParty.MobileParty);
				if (recruiterForParty != null)
				{
					Main.PartyManagement.PromptPartyManagementMenu(encounteredParty, MobileParty.MainParty);
				}
			}
			PlayerEncounter.LeaveEncounter = true;
		}

		private void Conversation_improvedgarrison_mobilegarrison_fortify_on_consequence()
		{
			PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
			if (encounteredParty.MobileParty != null && encounteredParty.MobileParty.HomeSettlement != null)
			{
				Settlement mobileGarrisonHome = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonHome(encounteredParty.MobileParty);
				if (Main.PartyManagement.mobileGarrisonManagement.MobileGarrisons.TryGetValue(mobileGarrisonHome.StringId, out var value))
				{
					PromptForitfyGarrison(value);
				}
			}
			PlayerEncounter.LeaveEncounter = true;
		}

		private void Conversation_improvedgarrison_mobilegarrison_return_on_consequence()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				if (encounteredParty.MobileParty != null && encounteredParty.MobileParty.HomeSettlement != null)
				{
					Settlement mobileGarrisonHome = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonHome(encounteredParty.MobileParty);
					if (mobileGarrisonHome != null && Main.PartyManagement.mobileGarrisonManagement.MobileGarrisons.TryGetValue(mobileGarrisonHome.StringId, out var value))
					{
						value.SetReturnMode();
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			PlayerEncounter.LeaveEncounter = true;
		}

		private void Conversation_improvedgarrison_recruiter_return_on_consequence()
		{
			try
			{
				PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
				if (encounteredParty.MobileParty != null && encounteredParty.MobileParty.HomeSettlement != null)
				{
					Main.PartyManagement.garrisonRecruiterPartyManagement.GetRecruiterForParty(encounteredParty.MobileParty)?.SetReturnMode();
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			PlayerEncounter.LeaveEncounter = true;
		}

		private void Conversation_improvedgarrison_mobilegarrison_leave_on_consequence()
		{
			PlayerEncounter.LeaveEncounter = true;
		}

		private Settlement GetBestGarrisonToReturnTo(MobileParty party)
		{
			Settlement best;
			float bestScore;
			Settlement bestCapacityOk;
			float bestCapacityOkScore;
			try
			{
				if (party == null || party.MapFaction == null)
				{
					return null;
				}
				best = null;
				bestScore = 0f;
				bestCapacityOk = null;
				bestCapacityOkScore = 0f;
				foreach (Settlement settlement2 in party.MapFaction.Settlements)
				{
					if (settlement2.IsFortification)
					{
						if (settlement2 == party.CurrentSettlement)
						{
							return settlement2;
						}
						considerCandidate(settlement2);
					}
				}
				if (best == null)
				{
					float maxDistance = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType((!party.IsCurrentlyAtSea) ? MobileParty.NavigationType.Default : MobileParty.NavigationType.Naval) * 2f;
					int lastIndex = -1;
					Func<Settlement, bool> condition = (Settlement settlement2) => settlement2.OwnerClan != null && !settlement2.OwnerClan.IsAtWarWith(party.MapFaction) && settlement2.IsFortification;
					int num;
					do
					{
						num = SettlementHelper.FindNextSettlementAroundMobileParty(party, party.NavigationCapability, maxDistance, lastIndex, condition);
						if (num >= 0)
						{
							lastIndex = num;
							Settlement s = Settlement.All[num];
							considerCandidate(s);
						}
					}
					while (num >= 0);
				}
				if (best == null)
				{
					Settlement settlement = SettlementHelper.FindNearestFortificationToMobileParty(party, party.NavigationCapability, (Settlement x) => x.OwnerClan != null && !x.OwnerClan.IsAtWarWith(party.MapFaction));
					if (settlement != null)
					{
						considerCandidate(settlement);
					}
				}
				return (bestCapacityOk != null) ? bestCapacityOk : best;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return null;
			}
			void considerCandidate(Settlement settlement2)
			{
				if (settlement2 != null && settlement2.IsFortification && settlement2.OwnerClan != null && !settlement2.OwnerClan.IsAtWarWith(party.MapFaction))
				{
					CalculateTargetSettlementScore(party, settlement2, out var _, out var bestScore2, out var _);
					if (bestScore2 > bestScore)
					{
						best = settlement2;
						bestScore = bestScore2;
					}
					bool flag = true;
					if (settlement2.Town != null && settlement2.Town.GarrisonParty != null)
					{
						try
						{
							int partySizeLimit = Main.PartyManagement.GetPartySizeLimit(settlement2.Town.GarrisonParty.Party);
							int num2 = settlement2.Town.GarrisonParty.Party.NumberOfAllMembers + party.Party.NumberOfAllMembers;
							flag = num2 < partySizeLimit;
						}
						catch
						{
						}
					}
					if (flag && bestScore2 > bestCapacityOkScore)
					{
						bestCapacityOk = settlement2;
						bestCapacityOkScore = bestScore2;
					}
				}
			}
		}

		public void DetermineNavigationForSettlement(MobileParty party, Settlement targetSettlement, out MobileParty.NavigationType navigationType, out bool isTargetingThePort)
		{
			navigationType = party?.NavigationCapability ?? MobileParty.NavigationType.None;
			isTargetingThePort = false;
			try
			{
				if (party == null || targetSettlement == null || targetSettlement.Town == null)
				{
					return;
				}
				AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, targetSettlement, isTargetingPort: false, out navigationType, out var bestNavigationDistance, out var isFromPort);
				float num = bestNavigationDistance;
				MobileParty.NavigationType navigationType2 = navigationType;
				bool flag = false;
				if (targetSettlement.HasPort && party.HasNavalNavigationCapability)
				{
					AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, targetSettlement, isTargetingPort: true, out var bestNavigationType, out var bestNavigationDistance2, out isFromPort);
					if (bestNavigationDistance2 < num)
					{
						num = bestNavigationDistance2;
						navigationType2 = bestNavigationType;
						flag = true;
					}
				}
				navigationType = navigationType2;
				isTargetingThePort = flag;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void SetMoveGoToSettlementHelper(Settlement settlement, MobileParty party)
		{
			Main.GarrisonPartyBehavior.DetermineNavigationForSettlement(party, settlement, out var navigationType, out var isTargetingThePort);
			party.SetMoveGoToSettlement(settlement, navigationType, isTargetingThePort);
		}

		private bool TrySetPartyMoveToSettlement(MobileParty party, Settlement targetSettlement)
		{
			try
			{
				if (party == null || targetSettlement == null)
				{
					return false;
				}
				DetermineNavigationForSettlement(party, targetSettlement, out var navigationType, out var isTargetingThePort);
				party.SetMoveGoToSettlement(targetSettlement, navigationType, isTargetingThePort);
				return true;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return false;
			}
		}

		private void CalculateTargetSettlementScore(MobileParty disbandParty, Settlement settlement, out MobileParty.NavigationType bestNavigationType, out float bestScore, out bool isTargetingPort)
		{
			isTargetingPort = false;
			bestScore = 0f;
			bestNavigationType = disbandParty?.NavigationCapability ?? MobileParty.NavigationType.None;
			if (disbandParty == null || settlement == null)
			{
				return;
			}
			AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(disbandParty, settlement, isTargetingPort: false, out bestNavigationType, out var bestNavigationDistance, out var isFromPort);
			float num = bestNavigationDistance;
			MobileParty.NavigationType navigationType = bestNavigationType;
			if (settlement.HasPort && disbandParty.HasNavalNavigationCapability)
			{
				AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(disbandParty, settlement, isTargetingPort: true, out var bestNavigationType2, out var bestNavigationDistance2, out isFromPort);
				if (bestNavigationDistance2 < num)
				{
					num = bestNavigationDistance2;
					navigationType = bestNavigationType2;
					isTargetingPort = true;
				}
			}
			float num2 = TaleWorlds.Library.MathF.Pow(1f - 0.95f * (TaleWorlds.Library.MathF.Min(Campaign.MapDiagonal, num) / Campaign.MapDiagonal), 3f);
			float num3 = ((disbandParty.Party.Owner?.Clan == settlement.OwnerClan) ? 1f : ((disbandParty.Party.Owner?.MapFaction == settlement.MapFaction) ? 0.1f : 0.01f));
			float num4 = ((disbandParty.DefaultBehavior == AiBehavior.GoToSettlement && disbandParty.TargetSettlement == settlement) ? 1f : 0.3f);
			bestScore = num2 * num3 * num4;
			bestNavigationType = navigationType;
		}

		private void PromptForitfyGarrisonFilter(MobileGarrison mobileGarrison)
		{
			try
			{
				_currentMobileGarrisonForFortification = mobileGarrison;
				MobileParty mobileParty = mobileGarrison.getMobileParty();
				List<InquiryElement> list = new List<InquiryElement>();
				List<InquiryElement> list2 = new List<InquiryElement>();
				List<InquiryElement> list3 = new List<InquiryElement>();
				Settlement[] array = Enumerable.ToArray(Settlement.All);
				for (int i = 0; i < array.Length; i++)
				{
					Kingdom kingdom = null;
					if (mobileParty.Party.Owner != null && mobileParty.Party.Owner.Clan != null && mobileParty.Party.Owner.Clan.Kingdom != null)
					{
						kingdom = mobileParty.Party.Owner.Clan.Kingdom;
					}
					if (array[i] != null && array[i].Town != null && (array[i].Town.IsCastle || array[i].Town.IsTown) && (Main.GarrisonBehavior.SettlementSettingsData.TryGetValue(array[i].Name.ToString(), out var _) || (array[i].OwnerClan != null && mobileParty.Party.Owner != null && array[i].OwnerClan == mobileParty.Party.Owner.Clan)))
					{
						list3.Add(new InquiryElement(array[i], array[i].Name.ToString(), new BannerImageIdentifier(array[i].Banner)));
					}
				}
				for (int j = 0; j < array.Length; j++)
				{
					Kingdom kingdom2 = null;
					if (mobileParty.Party.Owner != null && mobileParty.Party.Owner.Clan != null && mobileParty.Party.Owner.Clan.Kingdom != null)
					{
						kingdom2 = mobileParty.Party.Owner.Clan.Kingdom;
					}
					if (array[j] != null && array[j].Town != null && (array[j].Town.IsCastle || array[j].Town.IsTown) && array[j].OwnerClan != null && array[j].OwnerClan.Kingdom != null && array[j].OwnerClan.Kingdom == kingdom2 && mobileParty.Party.Owner != null && array[j].OwnerClan != mobileParty.Party.Owner.Clan)
					{
						list2.Add(new InquiryElement(array[j], array[j].Name.ToString(), new BannerImageIdentifier(array[j].Banner)));
					}
				}
				list.Add(new InquiryElement(list3, new TextObject("Show your clans garrisons").ToString(), new EmptyImageIdentifier()));
				list.Add(new InquiryElement(list2, new TextObject("Show all alliance garrisons").ToString(), new EmptyImageIdentifier()));
				MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(new TextObject("Select Garrison").ToString(), new TextObject("").ToString(), list, isExitShown: true, 1, 1, new TextObject("{=menu_continue}Continue").ToString(), new TextObject("{=menu_cancel}Cancel").ToString(), null, null));
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void PromptForitfyGarrison(MobileGarrison mobileGarrison)
		{
			try
			{
				_currentMobileGarrisonForFortification = mobileGarrison;
				MobileParty mobileParty = mobileGarrison.getMobileParty();
				List<InquiryElement> list = new List<InquiryElement>();
				Settlement[] array = Enumerable.ToArray(Settlement.All);
				for (int i = 0; i < array.Length; i++)
				{
					Kingdom kingdom = null;
					if (mobileParty.Party.Owner != null && mobileParty.Party.Owner.Clan != null && mobileParty.Party.Owner.Clan.Kingdom != null)
					{
						kingdom = mobileParty.Party.Owner.Clan.Kingdom;
					}
					if (array[i] != null && array[i].Town != null && (array[i].Town.IsCastle || array[i].Town.IsTown) && (Main.GarrisonBehavior.SettlementSettingsData.TryGetValue(array[i].Name.ToString(), out var _) || (array[i].OwnerClan != null && mobileParty.Party.Owner != null && array[i].OwnerClan == mobileParty.Party.Owner.Clan)))
					{
						ImageIdentifier imageIdentifier = new BannerImageIdentifier(array[i].Banner);
						list.Add(new InquiryElement(array[i], array[i].Name.ToString(), imageIdentifier));
					}
				}
				string titleText = new TextObject("{=menu_transfer_select}Select Garrison").ToString();
				string descriptionText = new TextObject("{=menu_fortify_desc}Select the garrison you want your guards to fortify.").ToString();
				string affirmativeText = new TextObject("{=menu_ok}Okay").ToString();
				string negativeText = new TextObject("{=menu_back}Back").ToString();
				MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(titleText, descriptionText, list, isExitShown: true, 1, 1, affirmativeText, negativeText, Inquirydata_FortifyGarrison, null));
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void Inquirydata_FortifyGarrison(List<InquiryElement> list)
		{
			try
			{
				if (_currentMobileGarrisonForFortification != null)
				{
					Settlement settlement = (Settlement)list.First().Identifier;
					_currentMobileGarrisonForFortification.getMobileParty().SetCustomHomeSettlement(settlement);
					_currentMobileGarrisonForFortification.SetFortifyMode(settlement);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public static List<MobileParty> GetAllClanParties(Clan clan)
		{
			try
			{
				if (clan == null)
				{
					return null;
				}
				List<MobileParty> list = new List<MobileParty>();
				foreach (MobileParty mobileParty in Campaign.Current.MobileParties)
				{
					try
					{
						if (mobileParty != null && mobileParty.Party != null && (mobileParty.ActualClan == clan || (mobileParty.Owner != null && mobileParty.Owner.Clan == clan)))
						{
							list.Add(mobileParty);
						}
					}
					catch
					{
					}
				}
				return list;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return null;
		}

		public void RemovePartyHelper(MobileParty party)
		{
			try
			{
				if (party == null)
				{
					return;
				}
				if (_removePartyMethodInfo == null)
				{
					_removePartyMethodInfo = typeof(MobileParty).GetMethod("RemoveParty", BindingFlags.Instance | BindingFlags.NonPublic);
					if (_removePartyMethodInfo == null)
					{
						InformationManager.DisplayMessage(new InformationMessage("Improved Garrisons: Could not access MobileParty.RemoveParty via reflection. Falling back to DestroyParty2Action.", Color.FromUint(ModuleColors.yellow)));
						Main.GarrisonPartyBehavior.RemovePartyHelper(party);
						return;
					}
				}
				_removePartyMethodInfo.Invoke(party, null);
			}
			catch (TargetInvocationException ex)
			{
				Exception ex2 = ex.InnerException ?? ex;
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex2);
				Main.GarrisonPartyBehavior.RemovePartyHelper(party);
			}
			catch (Exception ex3)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex3);
				Main.GarrisonPartyBehavior.RemovePartyHelper(party);
			}
		}
	}
}
