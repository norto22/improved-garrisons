using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager
{
	public class TemplateManager : ImprovedGarrisonSettings
	{
		private enum ManagementType
		{
			Apply,
			Remove,
			Rename,
			Inspect
		}

		private TrainingTemplate _currentTrainingTemplate;

		private TrainingUIVM _trainingDataSource;

		private Town _currentTown;

		private static TemplateManager _instance;

		public static TemplateManager Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new TemplateManager();
				}
				return _instance;
			}
			set
			{
				_instance = value;
			}
		}

		public void PromptTemplateManager(Town town, TrainingUIVM trainingDataSource = null)
		{
			try
			{
				if (!CheckIfTownIsValid(town))
				{
					return;
				}
				List<InquiryElement> list = new List<InquiryElement>();
				List<TrainingTemplate> list2 = GlobalSettings.Instance.TrainingTemplates.Values.ToList();
				_currentTown = town;
				if (trainingDataSource != null)
				{
					_trainingDataSource = trainingDataSource;
				}
				list.Add(new InquiryElement(null, new TextObject("{=settings_templatemanager_savetemplate}Save current template").ToString(), new BannerImageIdentifier(new Banner("11.123.97.1836.1836.768.788.1.0.-30.505.0.0.34.317.777.771.1.0.90.505.0.0.34.317.777.771.1.0.-1"))));
				foreach (TrainingTemplate item in list2)
				{
					list.Add(new InquiryElement(item, item.Name, item.GetImage()));
				}
				string titleText = new TextObject("{=settings_templatemanager_title}Training templates").ToString();
				string descriptionText = new TextObject("{=settings_templatemanager_desc}Choose a training template.").ToString();
				MultiSelectionInquiryData data = new MultiSelectionInquiryData(titleText, descriptionText, list, isExitShown: true, 1, 1, new TextObject("{=menu_continue}Continue").ToString(), new TextObject("{=menu_back}Back").ToString(), Inquirydata_TemplateManager, null);
				MBInformationManager.ShowMultiSelectionInquiry(data);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void AddNewTemplate(List<InquiryElement> list = null)
		{
			try
			{
				GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(_currentTown);
				if (townSettings.Template.AmountOfTroopsInTemplate <= 0)
				{
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_template_nosetup}Your setup currently doesn't have training units.").ToString(), Color.FromUint(13897216u)));
					return;
				}
				string titleText = new TextObject("{=settings_templatemanager_addnew1}Add a new template").ToString();
				string text = new TextObject("{=settings_templatemanager_addnew2}This adds the current training setup to your template manager. The template will be accessible across all of your saves. \n \nPlease select a name for your template.").ToString();
				InformationManager.ShowTextInquiry(new TextInquiryData(titleText, text, isAffirmativeOptionShown: true, isNegativeOptionShown: true, new TextObject("{=menu_ok}Okay").ToString(), new TextObject("{=menu_cancel}Cancel").ToString(), InquiryData_NewTemplate, null));
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void Inquirydata_TemplateManager(List<InquiryElement> list)
		{
			try
			{
				if (list == null || list.Count <= 0)
				{
					return;
				}
				_currentTrainingTemplate = (TrainingTemplate)list.First().Identifier;
				if (_currentTrainingTemplate == null)
				{
					AddNewTemplate();
					return;
				}
				Banner banner = new Banner("11.116.1.1836.1836.768.788.1.0.-30.510.122.122.304.296.685.749.1.0.270.527.122.122.197.183.759.680.1.0.180.510.122.122.162.262.811.827.1.0.303");
				Banner banner2 = new Banner("11.116.1.1836.1836.768.788.1.0.-30.510.122.122.306.296.764.743.1.0.90");
				Banner banner3 = new Banner("11.116.1.1836.1836.768.788.1.0.-30.510.122.122.306.296.831.730.1.0.108.510.122.122.132.296.764.757.1.0.-1.510.122.122.306.296.692.730.1.1.71.510.122.122.88.296.761.601.1.1.0");
				Banner banner4 = new Banner("11.116.1.1836.1836.768.788.1.0.-30.510.122.122.359.296.764.732.1.0.45.510.122.122.359.296.764.732.1.1.315");
				List<InquiryElement> list2 = new List<InquiryElement>();
				list2.Add(new InquiryElement(ManagementType.Apply, new TextObject("{=settings_recruitmentsettings_apply}Apply template").ToString(), new BannerImageIdentifier(banner3)));
				list2.Add(new InquiryElement(ManagementType.Inspect, new TextObject("{=settings_recruitmentsettings_inspect}Inspect template").ToString(), new BannerImageIdentifier(banner2)));
				list2.Add(new InquiryElement(ManagementType.Rename, new TextObject("{=settings_recruitmentsettings_rename}Rename template").ToString(), new BannerImageIdentifier(banner)));
				list2.Add(new InquiryElement(ManagementType.Remove, new TextObject("{=settings_recruitmentsettings_remove}Delete template").ToString(), new BannerImageIdentifier(banner4)));
				string titleText = new TextObject("{=settings_recruitmentsettings_manage}Manage template").ToString();
				MultiSelectionInquiryData data = new MultiSelectionInquiryData(titleText, null, list2, isExitShown: true, 1, 1, new TextObject("{=menu_continue}Continue").ToString(), new TextObject("{=menu_cancel}Cancel").ToString(), Inquirydata_AddTemplate, null);
				Main.ExecuteActionOnNextTick(delegate
				{
					MBInformationManager.ShowMultiSelectionInquiry(data);
				});
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void Inquirydata_AddTemplate(List<InquiryElement> list)
		{
			try
			{
				if (list == null || list.Count <= 0)
				{
					return;
				}
				switch ((ManagementType)list.First().Identifier)
				{
				case ManagementType.Apply:
					Main.ExecuteActionOnNextTick(delegate
					{
						ApplyTemplate(_currentTrainingTemplate);
					});
					break;
				case ManagementType.Rename:
					Main.ExecuteActionOnNextTick(delegate
					{
						InformationManager.ShowTextInquiry(new TextInquiryData(new TextObject("{=settings_recruitmentsettings_rename}Rename template").ToString(), new TextObject("{=settings_recruitmentsettings_renamedesc}Enter a new name for your template.").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: true, new TextObject("{=menu_ok}Okay").ToString(), new TextObject("{=menu_no}No").ToString(), delegate(string x)
						{
							RenameCurrentTemplate(x);
							PromptTemplateManager(_currentTown);
						}, delegate
						{
							PromptTemplateManager(_currentTown);
						}));
					});
					break;
				case ManagementType.Remove:
					RemoveTemplate(_currentTrainingTemplate);
					PromptTemplateManager(_currentTown);
					break;
				case ManagementType.Inspect:
					InspectTemplate(_currentTrainingTemplate);
					break;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void ApplyTemplate(TrainingTemplate template)
		{
			try
			{
				GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(base.garrisonBehavior.CurrentTownForSettings);
				Dictionary<string, int> troopList = template.GetTroopList();
				if (troopList != null)
				{
					townSettings.Template = template;
					if (base.garrisonBehavior.SettlementSettingsData.TryGetValue(_currentTown.Name.ToString(), out var _))
					{
						base.garrisonBehavior.SettlementSettingsData[_currentTown.Name.ToString()] = townSettings;
					}
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=settings_recruitmentsettings_yourtemplate}Your template").ToString() + ModuleStrings._space + _currentTrainingTemplate.Name + ModuleStrings._space + new TextObject("{=info_template_apply}has been applied.").ToString(), Color.FromUint(ModuleColors.green)));
					if (_trainingDataSource != null)
					{
						_trainingDataSource.TroopListIsDirty = true;
						_trainingDataSource = null;
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void InspectTemplate(TrainingTemplate template)
		{
			try
			{
				List<InquiryElement> list = new List<InquiryElement>();
				Dictionary<string, int> troopList = template.GetTroopList();
				if (troopList == null)
				{
					return;
				}
				foreach (KeyValuePair<string, int> item in troopList)
				{
					CharacterObject characterObject = MBObjectManager.Instance.GetObject<CharacterObject>(item.Key);
					if (characterObject != null)
					{
						string text = ((item.Value >= 0 && item.Value <= 999) ? ("[" + item.Value + "] ") : ("[" + new TextObject("{=menu_train_pathamount_restr}no restr.").ToString() + "] "));
						ImageIdentifier imageIdentifier = null;
						try
						{
							imageIdentifier = new CharacterImageIdentifier(CampaignUIHelper.GetCharacterCode(characterObject));
						}
						catch (Exception)
						{
						}
						if (imageIdentifier == null)
						{
							imageIdentifier = new EmptyImageIdentifier();
						}
						list.Add(new InquiryElement(characterObject, text + characterObject.Name, imageIdentifier));
					}
				}
				string titleText = new TextObject("{=settings_recruitmentsettings_inspect2}Inspect").ToString() + ModuleStrings._space + template.Name;
				string text2 = new TextObject("{=settings_recruitmentsettings_yourtemplate}Your template").ToString();
				MultiSelectionInquiryData data = new MultiSelectionInquiryData(titleText, "", list, isExitShown: false, 0, 1, new TextObject("{=menu_back}Back").ToString(), null, delegate
				{
					Main.ExecuteActionOnNextTick(delegate
					{
						PromptTemplateManager(_currentTown);
					});
				}, null);
				MBInformationManager.ShowMultiSelectionInquiry(data);
			}
			catch (Exception ex2)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex2);
			}
		}

		private void RenameCurrentTemplate(string templateName)
		{
			try
			{
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=settings_recruitmentsettings_yourtemplate}Your template").ToString() + ModuleStrings._space + _currentTrainingTemplate.Name + ModuleStrings._space + new TextObject("{=info_template_rename}has been renamed to").ToString() + ModuleStrings._space + templateName, Color.FromUint(ModuleColors.green)));
				GlobalSettings.Instance.TrainingTemplates.Remove(_currentTrainingTemplate.Name);
				_currentTrainingTemplate.Name = templateName;
				GlobalSettings.Instance.TrainingTemplates.Add(templateName, _currentTrainingTemplate);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void RemoveTemplate(TrainingTemplate template)
		{
			try
			{
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=settings_recruitmentsettings_yourtemplate}Your template").ToString() + ModuleStrings._space + _currentTrainingTemplate.Name + ModuleStrings._space + new TextObject("{=info_template_remove}has been removed.").ToString(), Color.FromUint(ModuleColors.green)));
				GlobalSettings.Instance.TrainingTemplates.Remove(template.Name);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void InquiryData_NewTemplate(string templateName)
		{
			try
			{
				if (templateName != null && templateName.Length > 0)
				{
					GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(base.garrisonBehavior.CurrentTownForSettings);
					Dictionary<string, int> troopList = townSettings.Template.GetTroopList();
					TrainingTemplate trainingTemplate = new TrainingTemplate(templateName);
					trainingTemplate.SetTroops(troopList);
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=settings_recruitmentsettings_yourtemplate}Your template").ToString() + ModuleStrings._space + trainingTemplate.Name + ModuleStrings._space + new TextObject("{=info_template_add}has been added.").ToString(), Color.FromUint(ModuleColors.green)));
					GlobalSettings.Instance.AddTrainingTemplate(trainingTemplate);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}
	}
}
