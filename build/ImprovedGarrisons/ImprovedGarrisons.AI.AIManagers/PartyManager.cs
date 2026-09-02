using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Helpers;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.AI.Orders.PartyOrder;
using ImprovedGarrisons.AI.PartyComponent;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.ImprovedGarrisonsUI;
using ImprovedGarrisons.SaveSystem.Configuration;
using ImprovedGarrisons.Utils;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using SandBox.ViewModelCollection.Map.Tracker;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.Tracker;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.AI.AIManagers
{
	public class PartyManager
	{
		public readonly MobileGarrisonManager mobileGarrisonManagement;

		public readonly VillageRecruitPartyManager villageRecruitPartyManagement;

		public readonly TransferPartyManager transferPartyManagement;

		public readonly GarrisonRecruiterPartyManager garrisonRecruiterPartyManagement;

		public PartyManager()
		{
			mobileGarrisonManagement = new MobileGarrisonManager();
			villageRecruitPartyManagement = new VillageRecruitPartyManager();
			transferPartyManagement = new TransferPartyManager();
			garrisonRecruiterPartyManagement = new GarrisonRecruiterPartyManager();
		}

		public List<MobileParty> GetAllImprovedGarrisonParties()
		{
			List<MobileParty> list = new List<MobileParty>();
			list.AddRange(mobileGarrisonManagement.GetAllMobileGarrisons());
			list.AddRange(villageRecruitPartyManagement.GetAllVillageRecruitParties());
			list.AddRange(transferPartyManagement.GetAllTransferParties());
			list.AddRange(garrisonRecruiterPartyManagement.GetAllRecruiters());
			return list;
		}

		public PartyBase InitializeNewParty(string id, TextObject partyName, Settlement homeSettlement, Settlement spawnOn)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(id) || homeSettlement == null || homeSettlement.Owner == null || spawnOn == null)
				{
					return null;
				}
				Hero owner = homeSettlement.Owner;
				PartyBase partyBaseById = GetPartyBaseById(id);
				if (partyBaseById != null)
				{
					return partyBaseById;
				}
				ImprovedGarrisonPartyComponent improvedGarrisonPartyComponent = new ImprovedGarrisonPartyComponent(homeSettlement, owner, partyName);
				MobileParty mobileParty = MobileParty.CreateParty(id, null);
				if (mobileParty == null)
				{
					return null;
				}
				mobileParty.Party.SetCustomName(partyName);
				mobileParty.SetCustomHomeSettlement(homeSettlement);
				mobileParty.Party.SetCustomOwner(owner);
				mobileParty.ActualClan = owner.Clan;
				mobileParty.Ai.SetInitiative(0.8f, 0.5f, float.MaxValue);
				mobileParty.ShouldJoinPlayerBattles = true;
				GivePartyFood(mobileParty);
				mobileParty.InitializeMobilePartyAroundPosition(TroopRoster.CreateDummyTroopRoster(), TroopRoster.CreateDummyTroopRoster(), spawnOn.GatePosition, 0f, 0f, !spawnOn.GatePosition.IsOnLand);
				mobileParty.Party.SetVisualAsDirty();
				mobileParty.SetPartyUsedByQuest(isActivelyUsed: true);
				bool flag = villageRecruitPartyManagement.IsImprovedGarrisonVillageRecruitParty(mobileParty);
				if (ConfigManager.Instance.Config.EnableMapBannerTracker && owner == Hero.MainHero && !flag)
				{
					TrackPartyWithBanner(mobileParty);
				}
				else
				{
					UntrackPartyWithBanner(mobileParty);
				}
				return mobileParty.Party;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return null;
		}

		public bool TrackAllImprovedGarrisonparties()
		{
			try
			{
				List<MobileParty> allImprovedGarrisonParties = Main.PartyManagement.GetAllImprovedGarrisonParties();
				foreach (MobileParty item in allImprovedGarrisonParties)
				{
					Main.PartyManagement.TrackPartyWithBanner(item);
				}
				return true;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		public bool UntrackAllImprovedGarrisonparties()
		{
			try
			{
				List<MobileParty> allImprovedGarrisonParties = Main.PartyManagement.GetAllImprovedGarrisonParties();
				foreach (MobileParty item in allImprovedGarrisonParties)
				{
					Main.PartyManagement.UntrackPartyWithBanner(item);
				}
				return true;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		private bool TryGetMapTrackerCollectionVM(out MapTrackerCollectionVM vm)
		{
			vm = null;
			if (MapScreen.Instance == null)
			{
				return false;
			}
			GauntletMapTrackersView mapView = MapScreen.Instance.GetMapView<GauntletMapTrackersView>();
			if (mapView == null)
			{
				return false;
			}
			FieldInfo field = mapView.GetType().GetField("_dataSource", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field == null)
			{
				return false;
			}
			vm = field.GetValue(mapView) as MapTrackerCollectionVM;
			return vm != null;
		}

		private bool TryGetMapTrackerProvider(out MapTrackerProvider provider)
		{
			provider = null;
			if (!TryGetMapTrackerCollectionVM(out var vm))
			{
				return false;
			}
			FieldInfo[] fields = vm.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo fieldInfo = fields.FirstOrDefault((FieldInfo f) => typeof(MapTrackerProvider).IsAssignableFrom(f.FieldType));
			if (fieldInfo == null)
			{
				return false;
			}
			provider = fieldInfo.GetValue(vm) as MapTrackerProvider;
			return provider != null;
		}

		private bool TryInvokeProviderMethod(MapTrackerProvider provider, string methodName, MobileParty party)
		{
			Type type = provider.GetType();
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.NonPublic;
			MethodInfo methodInfo = null;
			object obj = null;
			(Type, Func<object>)[] array = new(Type, Func<object>)[3]
			{
				(typeof(MobileParty), () => party),
				(typeof(ITrackableCampaignObject), () => party),
				(typeof(PartyBase), () => party.Party)
			};
			(Type, Func<object>)[] array2 = array;
			for (int num = 0; num < array2.Length; num++)
			{
				(Type, Func<object>) tuple = array2[num];
				methodInfo = type.GetMethod(methodName, bindingAttr, null, new Type[1] { tuple.Item1 }, null);
				if (methodInfo != null)
				{
					obj = tuple.Item2();
					break;
				}
			}
			if (methodInfo == null)
			{
				return false;
			}
			methodInfo.Invoke(provider, new object[1] { obj });
			return true;
		}

		public bool TrackPartyWithBanner(MobileParty party)
		{
			try
			{
				if (party == null)
				{
					return false;
				}
				if (MapScreen.Instance == null || party.ActualClan != Clan.PlayerClan || villageRecruitPartyManagement.IsImprovedGarrisonVillageRecruitParty(party))
				{
					return false;
				}
				if (!TryGetMapTrackerProvider(out var provider))
				{
					return false;
				}
				return TryInvokeProviderMethod(provider, "AddIfEligible", party);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return false;
			}
		}

		public bool UntrackPartyWithBanner(MobileParty party)
		{
			try
			{
				if (party == null || MapScreen.Instance == null)
				{
					return false;
				}
				if (!TryGetMapTrackerProvider(out var provider))
				{
					return false;
				}
				return TryInvokeProviderMethod(provider, "RemoveIfExists", party);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return false;
			}
		}

		public bool IsTrackedWithBanner(MobileParty party)
		{
			try
			{
				if (party == null || MapScreen.Instance == null)
				{
					return false;
				}
				if (!TryGetMapTrackerCollectionVM(out var vm))
				{
					return false;
				}
				MBBindingList<MapTrackerItemVM> trackers = vm.Trackers;
				if (trackers == null)
				{
					return false;
				}
				foreach (MapTrackerItemVM item in trackers)
				{
					if (item?.TrackedObject == party)
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

		public void GivePartyFood(MobileParty party)
		{
			try
			{
				if (party == null || party.Party == null || party.ItemRoster == null)
				{
					return;
				}
				float num = 100f;
				try
				{
					num = Math.Abs(party.FoodChange * 2f) + 100f;
				}
				catch (Exception ex)
				{
					LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				}
				int inventoryCapacity = party.InventoryCapacity;
				int num2 = (int)party.TotalWeightCarried;
				int num3 = inventoryCapacity - num2;
				if (num3 < 0)
				{
					return;
				}
				num = ((num > (float)num3) ? ((float)num3) : num);
				foreach (ItemObject item in Items.All)
				{
					if (item != null && item.IsFood)
					{
						float num4 = num * item.Weight;
						if (num4 > (float)inventoryCapacity)
						{
							num = (float)inventoryCapacity / item.Weight - 1f;
						}
						party.ItemRoster.AddToCounts(item, (int)num);
						if (party.Food >= num)
						{
							party.ItemRoster.UpdateVersion();
							break;
						}
					}
				}
			}
			catch (Exception ex2)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex2);
			}
		}

		public void ExecutePartialHourlyAi(MobileParty party)
		{
			if (party.StringId.Contains("mobile") || party.StringId.Contains("recruiter"))
			{
			}
			garrisonRecruiterPartyManagement.GetRecruiterForParty(party)?.PartialHourlyThinkBehavior();
		}

		public void ExecutePartialHourlyAi()
		{
		}

		public void ExecuteHourlyAi()
		{
			mobileGarrisonManagement.ExecutePartialHourThinkBehavior();
			transferPartyManagement.ExecuteHourThinkBehavior();
			villageRecruitPartyManagement.ExecuteHourThinkBehavior();
			mobileGarrisonManagement.ExecuteHourThinkBehavior();
			garrisonRecruiterPartyManagement.ExecuteHourThinkBehaviorForAll();
		}

		public PartyBase GetPartyBaseById(string id)
		{
			try
			{
				MBReadOnlyList<MobileParty> mobileParties = Campaign.Current.MobileParties;
				PartyBase result = null;
				if (mobileParties != null)
				{
					foreach (MobileParty item in mobileParties)
					{
						if (string.Equals(item.StringId, id))
						{
							result = item.Party;
							break;
						}
					}
				}
				return result;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return null;
			}
		}

		public List<MobileParty> GetAllNearNearbyParties(CampaignVec2 position, float radius, List<MobileParty> blacklist = null)
		{
			LocatableSearchData<MobileParty> data = MobileParty.StartFindingLocatablesAroundPosition(position.ToVec2(), radius);
			List<MobileParty> list = new List<MobileParty>();
			for (MobileParty mobileParty = MobileParty.FindNextLocatable(ref data); mobileParty != null; mobileParty = MobileParty.FindNextLocatable(ref data))
			{
				if (blacklist != null && !blacklist.Contains(mobileParty))
				{
					list.Add(mobileParty);
				}
			}
			return list;
		}

		public void RecruitMobilePartyToGarrison(MobileParty party, Settlement settlement, TroopRoster blackList = null)
		{
			try
			{
				int numberOfAllMembers = party.Party.NumberOfAllMembers;
				int num = 0;
				foreach (TroopRosterElement item in party.MemberRoster.GetTroopRoster())
				{
					int num2 = item.Number;
					if (blackList != null)
					{
						int num3 = blackList.FindIndexOfTroop(item.Character);
						if (num3 >= 0)
						{
							int number = blackList.GetElementCopyAtIndex(num3).Number;
							num2 -= number;
							if (num2 <= 0)
							{
								continue;
							}
						}
					}
					int num4 = item.Number - item.WoundedNumber;
					if (num4 < 0)
					{
						num4 = 0;
					}
					num4 = ((num4 - num2 < 0) ? num4 : num2);
					int num5 = item.WoundedNumber;
					if (num4 - num2 < 0)
					{
						num5 = ((num5 + (num4 - num2) < 0) ? num5 : (-(num4 - num2)));
					}
					if (settlement.Town.GarrisonParty == null)
					{
						settlement.AddGarrisonParty();
					}
					settlement.Town.GarrisonParty.MemberRoster.AddToCounts(item.Character, num4, insertAtFront: false, num5);
					num += num2;
				}
				if (num > 0 && villageRecruitPartyManagement.IsImprovedGarrisonVillageRecruitParty(party) && settlement.Owner != null && settlement.Owner == Hero.MainHero)
				{
					Settlement villageFromMobileParty = villageRecruitPartyManagement.GetVillageFromMobileParty(party);
					Main.ActivityLogManager.AddNewRecruits(settlement.Town, num, villageFromMobileParty);
				}
				bool flag = settlement.Owner != null && settlement.Owner != null && settlement.Owner == Hero.MainHero;
				if (mobileGarrisonManagement.IsMobileGarrisonParty(party) && flag)
				{
					MobileGarrison mobileGarrisonForParty = mobileGarrisonManagement.GetMobileGarrisonForParty(party);
					if (mobileGarrisonForParty != null)
					{
						OrderMergeGarrison orderMergeGarrison = mobileGarrisonForParty.CurrentOrder as OrderMergeGarrison;
						if (orderMergeGarrison != null && orderMergeGarrison.isReturning)
						{
							string text = new TextObject("{=menu_your}Your").ToString() + ModuleStrings._space + party.Name.ToString() + ModuleStrings._space + new TextObject("{=info_guards_return2}have returned to your garrison.").ToString();
							InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Color.FromUint(ModuleColors.yellow)));
							Main.ActivityLogManager.AddPartyMergedWithGarrisonActivity(settlement.Town, mobileGarrisonForParty.mobileParty);
						}
						else if (orderMergeGarrison != null)
						{
							string text2 = new TextObject("{=menu_your}Your").ToString() + ModuleStrings._space + party.Name.ToString() + ModuleStrings._space + new TextObject("{=info_transfer_done}have reached their transfer location.").ToString();
							InformationManager.DisplayMessage(new InformationMessage(text2.ToString(), Color.FromUint(ModuleColors.yellow)));
							Main.ActivityLogManager.AddPartyMergedWithGarrisonActivity(settlement.Town, mobileGarrisonForParty.mobileParty);
						}
					}
				}
				else if (transferPartyManagement.IsTransferParty(party) && flag)
				{
					string text3 = new TextObject("{=menu_your}Your").ToString() + ModuleStrings._space + party.Name.ToString() + ModuleStrings._space + new TextObject("{=info_transfer_donehave reached their transfer location..").ToString();
					InformationManager.DisplayMessage(new InformationMessage(text3.ToString(), Color.FromUint(ModuleColors.yellow)));
					Main.ActivityLogManager.AddPartyMergedWithGarrisonActivity(settlement.Town, party);
				}
				else if (garrisonRecruiterPartyManagement.IsRecruiterParty(party) && flag)
				{
					string text4 = new TextObject("{=menu_your}Your").ToString() + ModuleStrings._space + party.Name.ToString() + ModuleStrings._space + new TextObject("{=info_recruiter_returned1}has returned with").ToString() + " " + numberOfAllMembers + " " + new TextObject("{=info_recruiter_returned2}troops to").ToString() + " " + settlement.Name;
					InformationManager.DisplayMessage(new InformationMessage(text4.ToString(), Color.FromUint(ModuleColors.yellow)));
					Main.ActivityLogManager.AddNewRecruits(settlement.Town, numberOfAllMembers, null, addActivity: false);
					Main.ActivityLogManager.AddPartyMergedWithGarrisonActivity(settlement.Town, party);
				}
				LeaveSettlementAction.ApplyForParty(party);
				Main.GarrisonPartyBehavior.OnPartyRemoved(party.Party);
				Main.GarrisonPartyBehavior.RemovePartyHelper(party);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void PromptPartyManagementMenu(PartyBase leftParty, MobileParty rightParty)
		{
			try
			{
				if (leftParty == null || leftParty.MobileParty == null || rightParty == null || rightParty.Party == null || leftParty.Name == null)
				{
					return;
				}
				PartyScreenLogic partyScreenLogic = new PartyScreenLogic();
				PartyScreenLogicInitializationData initializationData = new PartyScreenLogicInitializationData
				{
					LeftOwnerParty = leftParty,
					RightOwnerParty = rightParty.Party,
					LeftMemberRoster = leftParty.MobileParty.MemberRoster,
					LeftPrisonerRoster = leftParty.PrisonRoster,
					RightMemberRoster = rightParty.MemberRoster,
					RightPrisonerRoster = rightParty.Party.PrisonRoster,
					LeftLeaderHero = leftParty.LeaderHero,
					RightLeaderHero = rightParty.LeaderHero,
					LeftPartyMembersSizeLimit = leftParty.PartySizeLimit,
					LeftPartyPrisonersSizeLimit = leftParty.PrisonerSizeLimit,
					RightPartyMembersSizeLimit = rightParty.Party.PartySizeLimit,
					RightPartyPrisonersSizeLimit = rightParty.Party.PrisonerSizeLimit,
					LeftPartyName = leftParty.Name,
					RightPartyName = rightParty.Name,
					TroopTransferableDelegate = PartyScreenHelper.TroopTransferableDelegate,
					IsDismissMode = false,
					IsTroopUpgradesDisabled = true,
					Header = null,
					TransferHealthiesGetWoundedsFirst = true,
					ShowProgressBar = false,
					MemberTransferState = PartyScreenLogic.TransferState.Transferable,
					PrisonerTransferState = PartyScreenLogic.TransferState.Transferable,
					AccompanyingTransferState = PartyScreenLogic.TransferState.Transferable
				};
				initializationData.PartyPresentationDoneButtonDelegate = delegate
				{
					if (mobileGarrisonManagement.IsMobileGarrisonParty(leftParty.MobileParty))
					{
						MobileGarrison mobileGarrisonForParty = mobileGarrisonManagement.GetMobileGarrisonForParty(leftParty.MobileParty);
						if (mobileGarrisonForParty != null)
						{
							mobileGarrisonForParty.InitializeInitialTroopRoster(withReset: true);
							Main.ActivityLogManager.AddPartyCreationActivity(mobileGarrisonForParty.fromSettlement.Town, mobileGarrisonForParty.mobileParty);
							UIManager.Instance.ForceOverviewUpdate();
						}
					}
					else if (garrisonRecruiterPartyManagement.IsRecruiterParty(leftParty.MobileParty))
					{
						GarrisonRecruiter recruiterForParty = garrisonRecruiterPartyManagement.GetRecruiterForParty(leftParty.MobileParty);
						if (recruiterForParty != null)
						{
							UIManager.Instance.ForceFullRefresh();
							recruiterForParty.SetInitialSize();
							Main.ActivityLogManager.AddPartyCreationActivity(recruiterForParty.fromSettlement.Town, recruiterForParty.mobileParty);
							UIManager.Instance.ForceOverviewUpdate();
						}
					}
					if (leftParty.NumberOfAllMembers <= 0)
					{
						Main.GarrisonPartyBehavior.RemovePartyHelper(leftParty.MobileParty);
					}
					return true;
				};
				initializationData.PartyPresentationDoneButtonConditionDelegate = (TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, int leftLimitNum, int rightLimitNum) => new Tuple<bool, TextObject>(item1: true, new TextObject(""));
				partyScreenLogic.Initialize(initializationData);
				PartyState partyState = Game.Current.GameStateManager.CreateState<PartyState>();
				partyState.PartyScreenLogic = partyScreenLogic;
				partyState.IsDonating = false;
				partyState.PartyScreenMode = PartyScreenHelper.PartyScreenMode.TroopsManage;
				Game.Current.GameStateManager.PushState(partyState);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void PromptManagementScreenWithActions(PartyBase leftParty, MobileParty rightParty, Action<TroopRoster, TroopRoster> doneAction, Action cancelAction)
		{
			try
			{
				if (leftParty != null && leftParty.MobileParty != null && rightParty != null && rightParty.Party != null && !(leftParty.Name == null))
				{
					PartyScreenLogic partyScreenLogic = new PartyScreenLogic();
					PartyScreenLogicInitializationData initializationData = new PartyScreenLogicInitializationData
					{
						LeftLeaderHero = leftParty.LeaderHero,
						RightLeaderHero = rightParty.LeaderHero,
						LeftMemberRoster = leftParty.MobileParty.MemberRoster,
						RightMemberRoster = rightParty.MemberRoster,
						LeftPrisonerRoster = leftParty.PrisonRoster,
						RightPrisonerRoster = rightParty.Party.PrisonRoster,
						LeftOwnerParty = leftParty,
						RightOwnerParty = rightParty.Party,
						LeftPartyMembersSizeLimit = leftParty.PartySizeLimit,
						RightPartyMembersSizeLimit = rightParty.Party.PartySizeLimit,
						LeftPartyPrisonersSizeLimit = leftParty.PrisonerSizeLimit,
						RightPartyPrisonersSizeLimit = rightParty.Party.PrisonerSizeLimit,
						LeftPartyName = leftParty.Name,
						RightPartyName = rightParty.Name,
						MemberTransferState = PartyScreenLogic.TransferState.Transferable,
						PrisonerTransferState = PartyScreenLogic.TransferState.Transferable,
						AccompanyingTransferState = PartyScreenLogic.TransferState.NotTransferable,
						TroopTransferableDelegate = PartyScreenHelper.TroopTransferableDelegate,
						PartyScreenMode = PartyScreenHelper.PartyScreenMode.TroopsManage,
						PartyPresentationDoneButtonConditionDelegate = (TroopRoster leftMembers, TroopRoster leftPrisoners, TroopRoster rightMembers, TroopRoster rightPrisoners, int leftLimit, int rightLimit) => new Tuple<bool, TextObject>(item1: true, TextObject.GetEmpty()),
						PartyPresentationDoneButtonDelegate = delegate(TroopRoster leftMembers, TroopRoster leftPrisoners, TroopRoster rightMembers, TroopRoster rightPrisoners, FlattenedTroopRoster takenPrisoners, FlattenedTroopRoster releasedPrisoners, bool isForced, PartyBase leftOwner, PartyBase rightOwner)
						{
							doneAction?.Invoke(leftMembers, rightMembers);
							return true;
						},
						PartyPresentationCancelButtonActivateDelegate = delegate
						{
							cancelAction?.Invoke();
							return true;
						}
					};
					partyScreenLogic.Initialize(initializationData);
					GameStateManager gameStateManager = Game.Current.GameStateManager;
					PartyState partyState = gameStateManager.CreateState<PartyState>();
					partyState.PartyScreenLogic = partyScreenLogic;
					partyState.IsDonating = false;
					partyState.PartyScreenMode = PartyScreenHelper.PartyScreenMode.TroopsManage;
					gameStateManager.PushState(partyState);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public Dictionary<CharacterObject, int> CompareTwoRosters(TroopRoster initial, TroopRoster after)
		{
			try
			{
				if (initial == null || after == null)
				{
					return null;
				}
				Dictionary<CharacterObject, int> dictionary = new Dictionary<CharacterObject, int>();
				foreach (TroopRosterElement item in after.GetTroopRoster())
				{
					if (initial.Contains(item.Character))
					{
						int index = initial.FindIndexOfTroop(item.Character);
						int elementNumber = initial.GetElementNumber(index);
						int num = item.Number - elementNumber;
						if (num != 0)
						{
							dictionary.Add(item.Character, num);
						}
					}
					else
					{
						dictionary.Add(item.Character, item.Number);
					}
				}
				return dictionary;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return null;
			}
		}

		public int GetPartySizeLimit(PartyBase party)
		{
			try
			{
				float resultNumber = Campaign.Current.Models.PartySizeLimitModel.GetPartyMemberSizeLimit(party).ResultNumber;
				return (int)resultNumber;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return 0;
			}
		}

		public TroopRoster CopyTroopRoster(TroopRoster toCopy, PartyBase ownerParty)
		{
			try
			{
				if (toCopy == null)
				{
					return null;
				}
				TroopRoster troopRoster = new TroopRoster(ownerParty);
				foreach (TroopRosterElement item in toCopy.GetTroopRoster())
				{
					troopRoster.AddToCounts(item.Character, item.Number, insertAtFront: false, 0, 0, removeDepleted: false);
				}
				return troopRoster;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return null;
			}
		}
	}
}
