using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.ImprovedGarrisonsUI;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager
{
	public class RecruitmentSettings : ImprovedGarrisonSettings
	{
		private int _amountToRecruitForNewRecruiter = 50;

		private CultureObject _cultureToRecruitFromForNewRecruiter = null;

		private Town _recruiterTown;

		private static RecruitmentSettings _instance;

		public static RecruitmentSettings Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new RecruitmentSettings();
				}
				return _instance;
			}
			set
			{
				_instance = value;
			}
		}

		public void PromptCreateRecruiter(Town town)
		{
			try
			{
				if (!CheckIfTownIsValid(town))
				{
					return;
				}
				if (!base.garrisonBehavior.CurrentTownForSettings.IsUnderSiege)
				{
					if (base.garrisonBehavior.CurrentTownForSettings.GarrisonParty != null && base.garrisonBehavior.CurrentTownForSettings.GarrisonParty.MemberRoster.TotalManCount > 0)
					{
						if (!Main.PartyManagement.garrisonRecruiterPartyManagement.SettlementHasARecruiter(base.garrisonBehavior.CurrentTownForSettings.Settlement))
						{
							PromptAmountSelectorForRecruiter(town);
						}
						else
						{
							InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_dupe}This garrison already has a recruiter party.").ToString(), Color.FromUint(13897216u)));
						}
					}
					else
					{
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_emptygarrison}Your garrison is empty. You need at least one unit to create a recruiter.").ToString(), Color.FromUint(13897216u)));
					}
				}
				else
				{
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_siege}This garrison is currently under siege. The recruiter can't get out!").ToString(), Color.FromUint(13897216u)));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void PromptAmountSelectorForRecruiter(Town town)
		{
			int result;
			InformationManager.ShowTextInquiry(new TextInquiryData(new TextObject("{=settings_recruitmentsettings_recruiteramount1}Recruitment headcount").ToString(), string.Format(new TextObject("{=settings_recruitmentsettings_recruiteramount2}Set the number of troops the recruiter must recruit before returning").ToString()), isAffirmativeOptionShown: true, isNegativeOptionShown: true, new TextObject("{=menu_ok}Okay").ToString(), new TextObject("{=menu_cancel}Cancel").ToString(), delegate(string amount)
			{
				GarrisonSettings settings = base.garrisonBehavior.GetTownSettings(town);
				_recruiterTown = town;
				if (int.TryParse(amount, out result))
				{
					_amountToRecruitForNewRecruiter = result;
					Main.ExecuteActionOnNextTick(delegate
					{
						if (!settings.RecruitmentFollowsTemplate)
						{
							Main.PartyManagement.garrisonRecruiterPartyManagement.PromptCultureSelection(Inquirydata_SetRecruiterCulture);
						}
						else
						{
							_cultureToRecruitFromForNewRecruiter = null;
							PromptSelectorForRecruiter();
						}
					});
				}
			}, delegate
			{
				InformationManager.HideInquiry();
			}, shouldInputBeObfuscated: false, (string x) => (int.TryParse(x, out result) && result <= 150 && result > 0) ? new Tuple<bool, string>(item1: true, "") : new Tuple<bool, string>(item1: false, new TextObject("{=info_recruitment_recruitervalue}Value has to be between 1 and 150").ToString())));
		}

		public void SetRecruiterAmountToRecruit(Town town, int amount)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
					townSettings.RecruiterRecruitAmount = amount;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void PromptChangeRecruitmentCulture(Town town)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					_recruiterTown = town;
					Main.PartyManagement.garrisonRecruiterPartyManagement.PromptCultureSelection(InquiryData_CultureToRecruitFrom);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ReturnRecruiter(Town town)
		{
			try
			{
				if (!CheckIfTownIsValid(town))
				{
					return;
				}
				if (Main.PartyManagement.garrisonRecruiterPartyManagement.SettlementHasARecruiter(town.Settlement))
				{
					GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(_recruiterTown);
					bool recruiterAutoSpawn = townSettings.RecruiterAutoSpawn;
					townSettings.RecruiterAutoSpawn = false;
					if (recruiterAutoSpawn)
					{
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_disabledautocreation}Disabled automatic recruiter creation for" + ModuleStrings._space + town.Name).ToString(), Color.FromUint(ModuleColors.yellow)));
						UIManager.Instance.RefreshWholeUI();
					}
					Main.PartyManagement.garrisonRecruiterPartyManagement.GetRecruiterOfSettlement(town.Settlement).SetReturnMode();
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_of}The recruiter of").ToString() + str_space + town.Name?.ToString() + str_space + new TextObject("{=info_recruiter_return}is now returning to the garrison.").ToString(), Color.FromUint(ModuleColors.green)));
				}
				else
				{
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_norecruiter}The garrison currently has no active recruiter").ToString(), Color.FromUint(ModuleColors.red)));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void Inquirydata_SetRecruiterCulture(List<InquiryElement> list)
		{
			if (list == null || list.Count <= 0)
			{
				return;
			}
			try
			{
				CultureObject cultureObject = (CultureObject)list.First().Identifier;
				GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(_recruiterTown);
				if (cultureObject != null)
				{
					_cultureToRecruitFromForNewRecruiter = cultureObject;
				}
				else
				{
					_cultureToRecruitFromForNewRecruiter = null;
				}
				townSettings.RecruiterAutoSpawn = false;
				PromptSelectorForRecruiter();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void PromptSelectorForRecruiter()
		{
			PartyBase partyBase = Main.PartyManagement.garrisonRecruiterPartyManagement.CreateGarrisonRecruiterParty(_recruiterTown.Settlement, _recruiterTown.Settlement);
			if (partyBase != null)
			{
				Main.PartyManagement.PromptPartyManagementMenu(partyBase, _recruiterTown.GarrisonParty);
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_newrecruiter_desc}Select units for your recruiter party. You need at least one unit to establish the party!").ToString(), Color.FromUint(ModuleColors.modMainColor)));
				GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(_recruiterTown);
				townSettings.RecruiterCultureToRecruit = ((_cultureToRecruitFromForNewRecruiter == null) ? null : _cultureToRecruitFromForNewRecruiter.StringId);
				townSettings.RecruiterRecruitAmount = _amountToRecruitForNewRecruiter;
				if (base.garrisonBehavior.SettlementSettingsData.TryGetValue(_recruiterTown.Name.ToString(), out var _))
				{
					base.garrisonBehavior.SettlementSettingsData[_recruiterTown.Name.ToString()] = townSettings;
				}
			}
		}

		private void InquiryData_CultureToRecruitFrom(List<InquiryElement> list)
		{
			if (list == null || list.Count <= 0)
			{
				return;
			}
			try
			{
				CultureObject cultureObject = (CultureObject)list.First().Identifier;
				GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(_recruiterTown);
				if (cultureObject != null)
				{
					townSettings.RecruiterCultureToRecruit = cultureObject.StringId;
				}
				else
				{
					townSettings.RecruiterCultureToRecruit = null;
				}
				if (base.garrisonBehavior.SettlementSettingsData.TryGetValue(_recruiterTown.Name.ToString(), out var _))
				{
					base.garrisonBehavior.SettlementSettingsData[_recruiterTown.Name.ToString()] = townSettings;
				}
				Main.PartyManagement.garrisonRecruiterPartyManagement.GetRecruiterOfSettlement(_recruiterTown.Settlement)?.ResetTradeTarget();
				string text = new TextObject("{=misc_any}any").ToString();
				if (cultureObject != null)
				{
					text = cultureObject.Name.ToString();
				}
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_changeculture1}Changed the recruitment culture to").ToString() + " " + text + " " + new TextObject("{=info_recruiter_changeculture2}for the recruiter of").ToString() + str_space + _recruiterTown.Name, Color.FromUint(ModuleColors.green)));
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void SetRecruitmentThreshold(Town town, int threshold)
		{
			try
			{
				if (CheckIfTownIsValid(town) && threshold >= 0)
				{
					GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
					townSettings.MaxRecruitThreshold = threshold;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleRecruitOnlyElite(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.RecruitOnlyEliteUnits = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruit_onlyelites_enable}Enabled only elite recruitment for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.RecruitOnlyEliteUnits = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruit_onlyelites_disable}Disabled only elite recruitment for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void TogglePrisonerRecruitmentAboveThreshold(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.AllowPrisonerRecruitAboveThreshold = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_prison_above_enable}Enabled prisoner recruitment above threshold for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.AllowPrisonerRecruitAboveThreshold = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_prison_above_disable}Disabled prisoner recruitment above threshold for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void TogglePrisonerRecruitment(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.EnablePrisonerRecruitment = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_prison_enable}Enabled prisoner recruitment for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.EnablePrisonerRecruitment = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_prison_disable}Disabled prisoner recruitment for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleVanillaRecruitment(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.VanillaRecruitment = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_vanillarecruitment_enable}Enabled vanilla recruitment for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.VanillaRecruitment = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_vanillarecruitment_disable}Disabled vanilla recruitment for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleRegionRecruitment(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.EnableRecruitFromRegion = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruit_enable}Enabled region recruitment for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.EnableRecruitFromRegion = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruit_disable}Disabled region recruitment for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleRecruiterOnlyElites(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.RecruiterRecruitOnlyElites = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_onlyelite_enabled}Enabled only elite recruitment for the recruiter of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.RecruiterRecruitOnlyElites = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_onlyelite_disabled}Disabled only elite recruitment for the recruiter of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleRecruiterBuyHorses(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.RecruiterAllowHorseBuy = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_buyhorses_enabled}Enabled horse trading for the recruiter of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.RecruiterAllowHorseBuy = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_buyhorses_disabled}Disabled horse trading for the recruiter of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void TogglePrisonerRecruitmentIgnoresTemplate(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.PrisonerRecruitmentIgnoresTemplate = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruit_prisonertemplate_enable}Prisoner recruitment now ignores the training template of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.PrisonerRecruitmentIgnoresTemplate = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruit_prisonertemplate_disable}Prisoner recruitment no longer ignores the training template of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleRecruiterAutoSpawn(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						bool recruiterAutoSpawn = townSettings.RecruiterAutoSpawn;
						townSettings.RecruiterAutoSpawn = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_autocreate_enabled}This garrison will now automatically gather the necessary troops for the current template. Make sure to have at least 1 unit for the recruiter to spawn in the garrison of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						bool recruiterAutoSpawn2 = townSettings2.RecruiterAutoSpawn;
						townSettings2.RecruiterAutoSpawn = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_autocreate_disabled}Disabled automatic troop gathering for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
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
