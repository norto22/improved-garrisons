using System;
using System.Collections.Generic;
using System.Reflection;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.AI.Orders.PartyOrder;
using ImprovedGarrisons.Behaviours;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.ImprovedGarrisonsUI;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager
{
	public class MobileGarrisonSettings : ImprovedGarrisonSettings
	{
		private static MobileGarrisonSettings _instance;

		public static MobileGarrisonSettings Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new MobileGarrisonSettings();
				}
				return _instance;
			}
			set
			{
				_instance = value;
			}
		}

		public void PromptCreateMobileGarrison(Town town)
		{
			try
			{
				if (!CheckIfTownIsValid(town))
				{
					return;
				}
				if (!town.IsUnderSiege)
				{
					if (town.GarrisonParty != null && town.GarrisonParty.MemberRoster.TotalManCount > 0)
					{
						MobileGarrison mobileGarrisonPartyOfSettlement = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(town.Settlement);
						if (mobileGarrisonPartyOfSettlement != null)
						{
							if (mobileGarrisonPartyOfSettlement.mobileParty.CurrentSettlement == null || mobileGarrisonPartyOfSettlement.mobileParty.CurrentSettlement.Town != town)
							{
								InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_dupe}This garrison already has a guard party. Please order it to return first.").ToString(), Color.FromUint(13897216u)));
								return;
							}
							mobileGarrisonPartyOfSettlement.SetReturnMode();
						}
						PartyBase partyBase = Main.PartyManagement.mobileGarrisonManagement.CreateMobileGarrison(town.Settlement, town.Settlement);
						if (partyBase != null)
						{
							Main.PartyManagement.PromptPartyManagementMenu(partyBase, town.GarrisonParty);
							InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_new}Please select the troops for your new guard party. \nThe more troops you choose, the slower your guard party will be!\nIf you want your guards to have the upper hand on looters and bandits, the party size should be around 30. By default, the guard party will patrol your region. You can give them different orders in the Improved Garrisons menu.").ToString(), Color.FromUint(ModuleColors.modMainColor)));
						}
					}
					else
					{
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_emptygarrison}Your garrison is empty.").ToString(), Color.FromUint(13897216u)));
					}
				}
				else
				{
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_undersiege}This location is currently under siege. The guard party can't get out!").ToString(), Color.FromUint(13897216u)));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void PromptMobileGarrisonEscort(Town town)
		{
			try
			{
				if (!CheckIfTownIsValid(town))
				{
					return;
				}
				if (Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(base.garrisonBehavior.CurrentTownForSettings.Settlement) == null)
				{
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_noguards}This garrison has no guard party.").ToString(), Color.FromUint(13897216u)));
				}
				else
				{
					if (town == null || town.Owner == null || town.Owner.Owner == null || town.Owner.Owner.Clan == null)
					{
						return;
					}
					List<InquiryElement> list = new List<InquiryElement>();
					List<MobileParty> allClanParties = GarrisonPartyBehavior.GetAllClanParties(town.Owner.Owner.Clan);
					if (allClanParties == null)
					{
						return;
					}
					foreach (MobileParty item in allClanParties)
					{
						if (item != null && !item.IsGarrison && (!item.IsMilitia || (Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(item) && !Main.PartyManagement.villageRecruitPartyManagement.IsImprovedGarrisonVillageRecruitParty(item))) && !item.IsVillager && item != Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(town.Settlement).getMobileParty())
						{
							CharacterObject character = ((item.LeaderHero == null) ? item.MemberRoster.GetTroopRoster().GetRandomElement().Character : item.LeaderHero.CharacterObject);
							ImageIdentifier imageIdentifier = null;
							try
							{
								imageIdentifier = new CharacterImageIdentifier(CampaignUIHelper.GetCharacterCode(character));
							}
							catch (Exception)
							{
							}
							if (imageIdentifier == null)
							{
								imageIdentifier = new EmptyImageIdentifier();
							}
							list.Add(new InquiryElement(item, item.Name.ToString(), imageIdentifier));
						}
					}
					MultiSelectionInquiryData data = new MultiSelectionInquiryData(new TextObject("{=settings_managementsettings_selectionescort1}Escort selection").ToString(), new TextObject("{=settings_managementsettings_selectionescort2}Select the party that should be supported").ToString(), list, isExitShown: true, 1, 1, new TextObject("{=menu_ok}Ok").ToString(), new TextObject("{=menu_cancel}Cancel").ToString(), Inquirydata_MobileGarrisonEscort, null);
					MBInformationManager.ShowMultiSelectionInquiry(data);
				}
			}
			catch (Exception ex2)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex2);
			}
		}

		public void OrderMobileGarrisonToPatrol(Town town)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					MobileGarrison mobileGarrisonPartyOfSettlement = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(town.Settlement);
					if (mobileGarrisonPartyOfSettlement == null)
					{
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_noguards}This garrison has no guard party.").ToString(), Color.FromUint(13897216u)));
						return;
					}
					mobileGarrisonPartyOfSettlement.GiveAndExecuteOrder(new OrderPatrol(town.Settlement));
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_patrol_new}The guard party of").ToString() + str_space + town.Name?.ToString() + str_space + new TextObject("{=info_patrol_new2}is now patrolling the region.").ToString(), Color.FromUint(ModuleColors.green)));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void OrderMobileGarrisonReturn(Town town)
		{
			try
			{
				if (!CheckIfTownIsValid(town))
				{
					return;
				}
				MobileGarrison mobileGarrisonPartyOfSettlement = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(town.Settlement);
				if (mobileGarrisonPartyOfSettlement == null)
				{
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_noguards}This garrison has no guard party.").ToString(), Color.FromUint(13897216u)));
					return;
				}
				if (mobileGarrisonPartyOfSettlement.homeGarrisonSettings.GuardsAutoSpawn)
				{
					mobileGarrisonPartyOfSettlement.homeGarrisonSettings.GuardsAutoSpawn = false;
					UIManager.Instance.improvedGarrisonsUI.UpdateUiContents();
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_autoguardsstopped}The automatic creation of guard parties has been disabled.").ToString(), Color.FromUint(ModuleColors.yellow)));
				}
				mobileGarrisonPartyOfSettlement.SetReturnMode();
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_return_new}The guard party of").ToString() + str_space + town.Name?.ToString() + str_space + new TextObject("{=info_return_new2}is now returning to the garrison.").ToString(), Color.FromUint(ModuleColors.green)));
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void OrderMobileGarrisonAttackOrDefend(Town town)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					MobileGarrison mobileGarrisonPartyOfSettlement = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(town.Settlement);
					if (mobileGarrisonPartyOfSettlement == null)
					{
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_noguards}This garrison has no guard party.").ToString(), Color.FromUint(13897216u)));
						return;
					}
					mobileGarrisonPartyOfSettlement.SetReturnMode();
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_return_new}The guard party of").ToString() + str_space + town.Name?.ToString() + str_space + new TextObject("{=info_return_new2}is now returning to the garrison.").ToString(), Color.FromUint(ModuleColors.green)));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void SetReturnPercentage(Town town, float x)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
					townSettings.GuardReturnPercentage = x;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void SetAutoGarrisonThreshold(Town town, int x)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
					townSettings.GuardsAutoSpawnThreshold = x;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void SetAutoGarrisonSize(Town town, int x)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
					townSettings.GuardsAutoSpawnSize = x;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void Inquirydata_MobileGarrisonEscort(List<InquiryElement> list)
		{
			try
			{
				if (list != null && list.Count > 0)
				{
					MobileParty mobileParty = (MobileParty)list.GetRandomElement().Identifier;
					MobileGarrison mobileGarrisonPartyOfSettlement = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(base.garrisonBehavior.CurrentTownForSettings.Settlement);
					if (mobileGarrisonPartyOfSettlement != null)
					{
						mobileGarrisonPartyOfSettlement.GiveAndExecuteOrder(new OrderEscort(mobileParty));
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_escort_new}The guard party of").ToString() + str_space + base.garrisonBehavior.CurrentTownForSettings.Name?.ToString() + str_space + new TextObject("{=info_escort_new2}is now escorting").ToString() + str_space + mobileParty.Name.ToString(), Color.FromUint(ModuleColors.green)));
					}
					else
					{
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_noguards}This garrison has no guard party.").ToString(), Color.FromUint(13897216u)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void TogglePrisonerSell(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.EnablePrisonerSell = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_sell_enable}Enabled prisoner trade for the guards of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.EnablePrisonerSell = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_sell_disable}Disabled prisoner trade for the guards of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleAutoGuards(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.GuardsAutoSpawn = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=settings_mobilegarrisonsettings_autoguardcreation1}Enabled automatic guard creation for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.GuardsAutoSpawn = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=settings_mobilegarrisonsettings_autoguardcreation2}Disabled automatic guard creation for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleAutoGuardDefend(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.GuardsAutoSpawnToDefend = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=settings_mobilegarrisonsettings_autodefend1}Enable automatic guard creation to defend villages for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.GuardsAutoSpawnToDefend = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=settings_mobilegarrisonsettings_autodefend2}Disable automatic guard creation to defend villages for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void TogglePrisonerRecruit(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.GuardEnablePrisonerRecruitment = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_prisonerrecruit_enable}Enabled prisoner recruitment for the guards of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.GuardEnablePrisonerRecruitment = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_prisonerrecruit_disable}Disabled prisoner recruitment for the guards of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleUpgrade(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.GuardEnableUpgradeTroops = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_upgrade_enable}Enabled troops upgrading for the guards of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.GuardEnableUpgradeTroops = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_upgrade_disable}Disabled troops upgrading for the guards of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleReplenish(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.EnableReplenish = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_replenish_enable}Enabled replenish and heal for the guards of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.EnableReplenish = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_replenish_disable}Disabled replenish and heal for the guards of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleDestroyHideout(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.EnableHideoutClear = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_hideoutclear1}Enabled hideout clearing for the guards of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.EnableHideoutClear = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_hideoutclear2}Disabled hideout clearing for the guards of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleHorseBuy(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.EnableHorseBuy = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_horsebuy1}Enabled horse trading for the guards of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.EnableHorseBuy = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_guards_horsebuy2}Disabled horse trading for the guards of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}
	}
}
