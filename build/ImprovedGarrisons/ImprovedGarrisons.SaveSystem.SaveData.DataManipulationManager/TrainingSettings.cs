using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using ImprovedGarrisons.Upgrade;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager
{
	public class TrainingSettings : ImprovedGarrisonSettings
	{
		private List<TroopTypes.Type> _unitTypeForSpecificUpgrade = new List<TroopTypes.Type>();

		private static TrainingSettings _instance;

		private TrainingUIVM _trainingDataSource;

		private Town _currentTown;

		public static TrainingSettings Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new TrainingSettings();
				}
				return _instance;
			}
			set
			{
				_instance = value;
			}
		}

		public void PromptCurrentTemplateManagement(Town town, TrainingUIVM trainingDataSource = null)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					List<InquiryElement> list = new List<InquiryElement>();
					GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
					_currentTown = town;
					if (trainingDataSource != null)
					{
						_trainingDataSource = trainingDataSource;
					}
					Banner banner = new Banner("11.116.1.1836.1836.768.788.1.0.-30.527.0.0.304.450.763.786.1.0.1");
					Banner banner2 = new Banner("11.116.1.1836.1836.768.788.1.0.-30.510.122.122.304.296.665.772.1.0.-91.510.122.122.304.296.859.766.1.0.-91.510.122.122.328.296.764.772.1.0.-56");
					bool flag;
					list.Add(new InquiryElement(flag = false, new TextObject("{=settings_trainingsettings_currenttemplate1}Current training template").ToString(), new BannerImageIdentifier(banner), isEnabled: true, new TextObject("{=settings_trainingsettings_currenttemplate2}Your list of specified training targets").ToString()));
					list.Add(new InquiryElement(flag = true, new TextObject("{=settings_trainingsettings_specifytroops1}Specify troops to train").ToString(), new BannerImageIdentifier(banner2), isEnabled: true, new TextObject("{=settings_trainingsettings_specifytroops2}List of every troops").ToString()));
					MultiSelectionInquiryData data = new MultiSelectionInquiryData(new TextObject("{=settings_trainingsettings_selecttargets}Select training troops").ToString(), new TextObject("{=settings_trainingsettings_selecttargetsdesc}Normally, if a unit can be upgraded to its next tier, Improved Garrison randomly chooses which path to take. Here, you can select wether you want to remove or add troops to specify their upgrade path. This upgrade is NOT affected by your tier restriction. If a unit's upgrade path hasn't been specified by the player, it will still upgrade, but randomly, and only up to the tier restriction. \n> The first list is an overview of the current upgrade paths Improved Garrison is upgrading to. \n> The second list contains all possible upgrade paths for you to select.").ToString(), list, isExitShown: true, 1, 1, new TextObject("{=menu_continue}Continue").ToString(), new TextObject("{=menu_cancel}Cancel").ToString(), Inquirydata_SelectPathList, null);
					Main.ExecuteActionOnNextTick(delegate
					{
						MBInformationManager.ShowMultiSelectionInquiry(data);
					});
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void PromptFilterForNewTroopsToAdd(Town town, TrainingUIVM trainingDataSource = null)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					_currentTown = town;
					if (trainingDataSource != null)
					{
						_trainingDataSource = trainingDataSource;
					}
					List<InquiryElement> list = new List<InquiryElement>();
					TroopTypes.Type type = TroopTypes.Type.Archer;
					TroopTypes.Type type2 = TroopTypes.Type.Infantry;
					TroopTypes.Type type3 = TroopTypes.Type.Cavalary;
					CharacterCode characterCode = CampaignUIHelper.GetCharacterCode(town.Culture.RangedMilitiaTroop);
					CharacterCode characterCode2 = CampaignUIHelper.GetCharacterCode(town.Culture.MeleeEliteMilitiaTroop);
					CharacterCode characterCode3 = CampaignUIHelper.GetCharacterCode(MBObjectManager.Instance.GetObject<CharacterObject>("aserai_vanguard_faris"));
					ImageIdentifier imageIdentifier = new CharacterImageIdentifier(characterCode);
					if (imageIdentifier == null)
					{
						imageIdentifier = new EmptyImageIdentifier();
					}
					ImageIdentifier imageIdentifier2 = new CharacterImageIdentifier(characterCode2);
					if (imageIdentifier2 == null)
					{
						imageIdentifier2 = new EmptyImageIdentifier();
					}
					ImageIdentifier imageIdentifier3 = new CharacterImageIdentifier(characterCode3);
					if (imageIdentifier3 == null)
					{
						imageIdentifier3 = new EmptyImageIdentifier();
					}
					list.Add(new InquiryElement(type, new TextObject("{=settings_trainingsettings_archer}Ranged").ToString(), imageIdentifier));
					list.Add(new InquiryElement(type2, new TextObject("{=settings_trainingsettings_infantry}Infantry").ToString(), imageIdentifier2));
					list.Add(new InquiryElement(type3, new TextObject("{=settings_trainingsettings_cavalary}Cavalry").ToString(), imageIdentifier3));
					MultiSelectionInquiryData data = new MultiSelectionInquiryData(new TextObject("{=settings_trainingsettings_filtertype}Filter by type").ToString(), new TextObject("{=settings_trainingsettings_filtertypedesc}What is the troop type you want to add to the template?").ToString(), list, isExitShown: true, 1, list.Count, new TextObject("{=menu_continue}Continue").ToString(), new TextObject("{=menu_back}Back").ToString(), Inquirydata_SetSpecificUpgradePath, null);
					MBInformationManager.ShowMultiSelectionInquiry(data);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void Inquirydata_SetSpecificUpgradePath(List<InquiryElement> list)
		{
			try
			{
				_unitTypeForSpecificUpgrade.Clear();
				if (list != null && list.Count > 0)
				{
					foreach (InquiryElement item in list)
					{
						_unitTypeForSpecificUpgrade.Add((TroopTypes.Type)item.Identifier);
					}
				}
				else
				{
					_unitTypeForSpecificUpgrade.Add(TroopTypes.Type.Infantry);
					_unitTypeForSpecificUpgrade.Add(TroopTypes.Type.Cavalary);
					_unitTypeForSpecificUpgrade.Add(TroopTypes.Type.Archer);
				}
				SpecifyUpgradePath(addNewUnits: true);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void Inquirydata_SelectPathList(List<InquiryElement> list)
		{
			try
			{
				if (list == null || list.Count <= 0)
				{
					return;
				}
				InquiryElement randomElement = list.GetRandomElement();
				if (randomElement.Identifier != null && (bool)randomElement.Identifier)
				{
					Main.ExecuteActionOnNextTick(delegate
					{
						PromptFilterForNewTroopsToAdd(_currentTown);
					});
				}
				else
				{
					SpecifyUpgradePath(addNewUnits: false);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void SpecifyUpgradePath(bool addNewUnits)
		{
			try
			{
				if (base.garrisonBehavior == null)
				{
					LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, new InvalidOperationException("Garrison behavior is not available."), withoutMessage: true);
					return;
				}
				Town town = _currentTown ?? base.garrisonBehavior.CurrentTownForSettings;
				if (town == null)
				{
					LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, new InvalidOperationException("No town is available for training settings."), withoutMessage: true);
					return;
				}
				_currentTown = town;
				List<InquiryElement> list = new List<InquiryElement>();
				if (addNewUnits)
				{
					HashSet<CharacterObject> hashSet = new HashSet<CharacterObject>();
					foreach (TroopTypes.Type item2 in _unitTypeForSpecificUpgrade)
					{
						IEnumerable<CharacterObject> enumerable = new List<CharacterObject>();
						switch (item2)
						{
						case TroopTypes.Type.Archer:
							enumerable = CharacterObject.FindAll(delegate(CharacterObject x)
							{
								bool flag2 = x.StringId.Contains("militia");
								return x.IsRanged && !x.IsHero && x.IsSoldier && !flag2;
							});
							break;
						case TroopTypes.Type.Infantry:
							enumerable = CharacterObject.FindAll(delegate(CharacterObject x)
							{
								bool flag2 = x.StringId.Contains("militia");
								return x.IsInfantry && !x.IsHero && x.IsSoldier && !flag2;
							});
							break;
						case TroopTypes.Type.Cavalary:
							enumerable = CharacterObject.FindAll(delegate(CharacterObject x)
							{
								bool flag2 = x.StringId.Contains("militia");
								return x.IsMounted && !x.IsHero && x.IsSoldier && !flag2;
							});
							break;
						}
						if (enumerable == null)
						{
							continue;
						}
						foreach (CharacterObject item3 in enumerable)
						{
							hashSet.Add(item3);
						}
					}
					List<InquiryElement> list2 = new List<InquiryElement>();
					Dictionary<CultureObject, List<InquiryElement>> dictionary = new Dictionary<CultureObject, List<InquiryElement>>();
					foreach (CharacterObject item4 in hashSet)
					{
						if (item4.IsInitialized && item4.Name != null && item4.Culture != null)
						{
							CultureObject culture = item4.Culture;
							List<InquiryElement> value;
							bool flag = dictionary.TryGetValue(culture, out value);
							ImageIdentifier imageIdentifier = null;
							try
							{
								imageIdentifier = new CharacterImageIdentifier(CampaignUIHelper.GetCharacterCode(item4));
							}
							catch (Exception)
							{
							}
							if (imageIdentifier == null)
							{
								imageIdentifier = new EmptyImageIdentifier();
							}
							InquiryElement item = new InquiryElement(item4, item4.Name.ToString(), imageIdentifier);
							if (!flag)
							{
								value = new List<InquiryElement>();
							}
							value.Add(item);
							list2.Add(item);
							if (!flag)
							{
								dictionary.Add(culture, value);
							}
						}
					}
					List<Kingdom> list3 = Kingdom.All.ToList();
					list.Add(new InquiryElement(list2, new TextObject("{=menu_selectall}Select all").ToString(), new EmptyImageIdentifier(), isEnabled: true, new TextObject("{=hint_select_all_clans}Select all clans in the list").ToString()));
					foreach (KeyValuePair<CultureObject, List<InquiryElement>> item5 in dictionary)
					{
						item5.Value.Insert(0, new InquiryElement(item5.Value, new TextObject("{=menu_selectall}Select all").ToString(), new EmptyImageIdentifier(), isEnabled: true, new TextObject("{=hint_select_all_units}Select all units in this list").ToString()));
						ImageIdentifier imageIdentifier2 = new EmptyImageIdentifier();
						if (list3 != null)
						{
							foreach (Kingdom item6 in list3)
							{
								if (item6.Culture != null && item6.Culture == item5.Key && item6.Banner != null)
								{
									imageIdentifier2 = new BannerImageIdentifier(item6.Banner);
									if (imageIdentifier2 == null)
									{
										imageIdentifier2 = new EmptyImageIdentifier();
									}
								}
							}
						}
						list.Add(new InquiryElement(item5.Value, (item5.Key.Name != null) ? item5.Key.Name.ToString() : item5.Key.StringId, imageIdentifier2));
					}
					MultiSelectionInquiryData data = new MultiSelectionInquiryData(new TextObject("{=settings_trainingsettings_filterculture1}Filter by culture").ToString(), new TextObject("{=settings_trainingsettings_filterculture2}Please select the culture of your desired troops.").ToString(), list, isExitShown: true, 1, list.Count, new TextObject("{=menu_ok}Okay").ToString(), new TextObject("{=menu_cancel}Cancel").ToString(), PromptClanSpecificUnitsWithPartyManager, null);
					Main.ExecuteActionOnNextTick(delegate
					{
						MBInformationManager.ShowMultiSelectionInquiry(data);
					});
					return;
				}
				GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
				if (townSettings == null || townSettings.Template == null || townSettings.Template.AmountOfTroopsInTemplate <= 0)
				{
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_template_notargets}There are currently no Improved Garrisons upgrade targets defined.").ToString(), Color.FromUint(ModuleColors.red)));
					return;
				}
				MobileParty mobileParty = new MobileParty();
				mobileParty.Party.SetCustomName(new TextObject("{=settings_trainingsettings_currenttroops}Current troops to train"));
				mobileParty.StringId = "improvedgarrisons_template_party";
				MobileParty mobileParty2 = new MobileParty();
				mobileParty2.Party.SetCustomName(new TextObject("{=settings_trainingsettings_troopstoselect}Troops to select"));
				mobileParty2.StringId = "improvedgarrisons_template_party";
				Campaign.Current.Models.PartySizeLimitModel.GetPartyMemberSizeLimit(mobileParty2.Party);
				foreach (CharacterObject item7 in CharacterObject.All)
				{
					if (townSettings.Template.Contains(item7))
					{
						int num = townSettings.Template.GetAmountForTemplateTroop(item7);
						if (num <= 0)
						{
							num = 9999;
						}
						mobileParty.AddElementToMemberRoster(item7, num);
						mobileParty2.AddElementToMemberRoster(item7, 900);
					}
				}
				Main.PartyManagement.PromptManagementScreenWithActions(mobileParty.Party, mobileParty2, delegate(TroopRoster leftMemberRoster, TroopRoster rightMemberRoster)
				{
					if (leftMemberRoster != null)
					{
						List<TroopRosterElement> list4 = new List<TroopRosterElement>();
						foreach (TroopRosterElement item8 in leftMemberRoster.GetTroopRoster())
						{
							list4.Add(item8);
						}
						SetSpecifiedUpgradeTargets(list4);
					}
				}, delegate
				{
				});
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_template_managetargets}In this screen, you can adjust the number of units this garrison should train. You may also remove troops from the list. \n \nOn the left side are the troops this garrison is training towards. On the right side is a copy of the same units that can be added to the left side.").ToString(), Color.FromUint(ModuleColors.modMainColor)));
			}
			catch (Exception ex2)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex2);
			}
		}

		private void SetSpecifiedUpgradeTargets(List<TroopRosterElement> list)
		{
			try
			{
				if (list == null)
				{
					return;
				}
				GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(_currentTown);
				if (townSettings == null)
				{
					return;
				}
				townSettings.Template.Clear();
				foreach (TroopRosterElement item in list)
				{
					CharacterObject character = item.Character;
					if (item.Number > 0)
					{
						townSettings.Template.AddOrUpdateCharacter(item.Character, item.Number);
						GarrisonSettings currentTownSettings = Main.GarrisonBehavior.GetCurrentTownSettings();
					}
				}
				if (base.garrisonBehavior.SettlementSettingsData.TryGetValue(_currentTown.Name.ToString(), out var _))
				{
					base.garrisonBehavior.SettlementSettingsData[_currentTown.Name.ToString()] = townSettings;
				}
				if (townSettings.Template.AmountOfTroopsInTemplate > 0)
				{
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_template_newtargets1}The training template for").ToString() + ModuleStrings._space + base.garrisonBehavior.CurrentTownForSettings.Name?.ToString() + new TextObject("{=info_template_newtargets2}has been set.").ToString(), Color.FromUint(ModuleColors.green)));
				}
				else
				{
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_template_removedtargets1}The garrison of").ToString() + ModuleStrings._space + base.garrisonBehavior.CurrentTownForSettings.Name?.ToString() + new TextObject("{=info_template_removedtargets1}The garrison of").ToString(), Color.FromUint(ModuleColors.yellow)));
				}
				if (_trainingDataSource != null)
				{
					_trainingDataSource.TroopListIsDirty = true;
					_trainingDataSource = null;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void PromptClanSpecificUnitsWithPartyManager(List<InquiryElement> list)
		{
			try
			{
				if (list == null || list.Count <= 0)
				{
					return;
				}
				MobileParty mobileParty = new MobileParty();
				mobileParty.Party.SetCustomName(new TextObject("{=settings_trainingsettings_currenttroops}Current troops to train"));
				mobileParty.StringId = "improvedgarrisons_template_party";
				MobileParty mobileParty2 = new MobileParty();
				mobileParty2.Party.SetCustomName(new TextObject("{=settings_trainingsettings_troopstoselect}Troops to select"));
				mobileParty2.StringId = "improvedgarrisons_template_party";
				Campaign.Current.Models.PartySizeLimitModel.GetPartyMemberSizeLimit(mobileParty2.Party);
				foreach (InquiryElement item in list)
				{
					List<InquiryElement> list2 = (List<InquiryElement>)item.Identifier;
					foreach (InquiryElement item2 in list2)
					{
						if (item2.Title != new TextObject("{=menu_selectall}Select all").ToString())
						{
							CharacterObject element = (CharacterObject)item2.Identifier;
							mobileParty2.AddElementToMemberRoster(element, 900);
						}
					}
				}
				GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(base.garrisonBehavior.CurrentTownForSettings);
				foreach (CharacterObject item3 in CharacterObject.All)
				{
					if (townSettings.Template.Contains(item3))
					{
						int num = townSettings.Template.GetAmountForTemplateTroop(item3);
						if (num <= 0)
						{
							num = 9999;
						}
						mobileParty.AddElementToMemberRoster(item3, num);
					}
				}
				Main.PartyManagement.PromptManagementScreenWithActions(mobileParty.Party, mobileParty2, delegate(TroopRoster leftMemberRoster, TroopRoster rightMemberRoster)
				{
					if (leftMemberRoster != null && leftMemberRoster.Count > 0)
					{
						List<TroopRosterElement> list3 = new List<TroopRosterElement>();
						foreach (TroopRosterElement item4 in leftMemberRoster.GetTroopRoster())
						{
							list3.Add(item4);
						}
						SetSpecifiedUpgradeTargets(list3);
					}
				}, delegate
				{
				});
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_template_newtargets_desc}Move the troops that are to be trained by this garrison to the left side of the screen. \nMake sure to select the number you want to have trained. \n \nNote: Improved Garrison will always try to have this number of units in the garrison and will automatically train new units when this number is no longer reached.").ToString(), Color.FromUint(ModuleColors.modMainColor)));
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void Inquirydata_SetUpgradePath(List<InquiryElement> list)
		{
			if (list == null || list.Count <= 0)
			{
				return;
			}
			try
			{
				Settlement settlement = base.garrisonBehavior.CurrentTownForSettings.Settlement;
				if (settlement == null || settlement.Town == null || list == null || list.Count <= 0)
				{
					return;
				}
				bool[] array = new bool[3];
				string text = "";
				foreach (InquiryElement item in list)
				{
					text = text + ((TroopTypes.Type)item.Identifier/*cast due to .constrained prefix*/).ToString() + str_space + new TextObject("{=menu_and}and").ToString() + str_space;
					switch ((TroopTypes.Type)item.Identifier)
					{
					case TroopTypes.Type.Archer:
						array[0] = true;
						break;
					case TroopTypes.Type.Infantry:
						array[1] = true;
						break;
					case TroopTypes.Type.Cavalary:
						array[2] = true;
						break;
					}
				}
				if (text.Length < 1)
				{
					text = new TextObject("{=menu_none}none").ToString();
				}
				else
				{
					int length = text.LastIndexOf(str_space + new TextObject("{=menu_and}and").ToString() + str_space);
					text = text.Substring(0, length);
				}
				GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(settlement.Town);
				townSettings.TroopsToUpgradeTo = array;
				if (base.garrisonBehavior.SettlementSettingsData.TryGetValue(_currentTown.Name.ToString(), out var _))
				{
					base.garrisonBehavior.SettlementSettingsData[_currentTown.Name.ToString()] = townSettings;
				}
				if (_trainingDataSource != null)
				{
					_trainingDataSource.TroopListIsDirty = true;
					_trainingDataSource = null;
				}
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_path_set}The upgrade paths for").ToString() + ModuleStrings._space + settlement.Name?.ToString() + new TextObject("{=info_path_set2}has been set to").ToString() + text, Color.FromUint(ModuleColors.green)));
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public bool RemoveUpgradeTarget(Town town, CharacterObject character, TrainingUIVM dataSource = null)
		{
			try
			{
				if (!CheckIfTownIsValid(town))
				{
					return false;
				}
				if (dataSource != null)
				{
					_trainingDataSource = dataSource;
				}
				GarrisonSettings settings = Main.GarrisonBehavior.GetTownSettings(town);
				if (settings != null)
				{
					InformationManager.ShowInquiry(new InquiryData(new TextObject("{=settings_trainingsettings_removetroop1}Remove template troop").ToString(), new TextObject("{=settings_trainingsettings_removetroop2}Are you sure you want to remove this troop from the current template?").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: true, new TextObject("{=menu_yes}Yes").ToString(), new TextObject("{=menu_no}No").ToString(), delegate
					{
						settings.Template.RemoveCharacter(character);
						if (_trainingDataSource != null)
						{
							_trainingDataSource.TroopListIsDirty = true;
							_trainingDataSource = null;
						}
						InformationManager.HideInquiry();
					}, delegate
					{
						InformationManager.HideInquiry();
					}));
					return true;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		public void SetTownMaxUpgradeTier(Town town, int tier)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
					townSettings.MaxUpgradeTier = tier;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleVanillaTraining(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.VanillaTraining = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_vanillatrain_enable}Enabled vanilla training for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.VanillaTraining = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_vanillatrain_disable}Disabled vanilla training for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleTraining(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.EnableTraining = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_train_enable}Enabled training for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.EnableTraining = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_train_disable}Disabled training for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleAutoSpawn(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.RecruiterAutoSpawn = true;
						Main.PartyManagement.garrisonRecruiterPartyManagement.GetRecruiterOfSettlement(town.Settlement)?.SetReturnMode();
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruiter_autocreate_enabled}This garrison will now automatically gather the necessary troops for the current template. Make sure to have at least 1 unit for the recruiter to spawn in the garrison of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
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

		public void ToggleFollowTemplate(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.RecruitmentFollowsTemplate = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruit_followtemplate_enable}The Improved Garrison recruitment is now only recruiting troops that are either part of the template or needed for upgrades towards a template troop.").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.RecruitmentFollowsTemplate = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruit_followtemplate_disable}The Improved Garrison recruitment is no longer only recruiting template-related troops.").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ToggleRemoveNonTemplateTroops(Town town, bool enable)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					if (enable)
					{
						GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
						townSettings.AutoRemoveNonTemplateTroops = true;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruit_removenontemplate_enable}Enabled automatic removal of non template troops for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
					}
					else
					{
						GarrisonSettings townSettings2 = base.garrisonBehavior.GetTownSettings(town);
						townSettings2.AutoRemoveNonTemplateTroops = false;
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_recruit_removenontemplate_disable}Disabled automatic removal of non template troops for").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.yellow)));
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
