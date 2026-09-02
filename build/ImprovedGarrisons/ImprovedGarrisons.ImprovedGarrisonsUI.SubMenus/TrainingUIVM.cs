using System;
using System.Collections.Generic;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.ImprovedGarrisonsUI.UIElements;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus
{
    public class TrainingUIVM : ViewModel
    {
        private string _estimatedTemplateCosts;

        private GarrisonSettings CurrentGarrisonSettings => Main.GarrisonBehavior.GetCurrentTownSettings();

        public MBBindingList<ImprovedGarrisonsTroopItemWidgetVM> Troops { get; set; }

        public MBBindingList<ImprovedGarrisonsOptionVM> TrainingSettingsVM { get; set; }

        public bool TroopListIsDirty { get; set; } = true;

        public ImprovedGarrisonsOptionVM ToggleTraining { get; set; }

        public string CurrentTemplateAddTroopText { get; } = new TextObject("{=ui_trainingui_addtroops}Add troops").ToString();

        public string CurrentTemplateTitleText { get; } = new TextObject("{=ui_trainingui_currenttemplate}Current template").ToString();

        public string TemplateManagerText { get; } = new TextObject("{=ui_trainingui_templatemanager}Template manager").ToString();

        public string TrainingSettingsText { get; } = new TextObject("{=ui_trainingui_trainingsettings}Training settings").ToString();

        public HintViewModel AddTroopsButtonHint { get; } = new HintViewModel(new TextObject("{=ui_trainingui_addtroopshint}Add new troops to the training template.\n\nA training template is used to define the upgrade path this garrison will take when training troops. The troops you select here are not affected by the training tier restriction.\n\n Use a training template to compose your army as you want it. You can set the number of troops that should be trained for each upgrade target you define. You could, for example, define the number of infantry, ranged or cavalry troops your garrison should have."));

        public HintViewModel TemplateManagerButtonHint { get; } = new HintViewModel(new TextObject("{=ui_trainingui_templatemanagerhint}A training template is used to define the upgrade path this garrison will take when training troops. The troops you select here are not affected by the training tier restriction.\n \nUse a training template to compose your army as you want it. You can set the number of troops that should be trained for each upgrade target you define. You could, for example, define the number of infantry, ranged or cavalry troops your garrison should have.\n \nThe template manager is used to save, apply, inspect or remove your training templates. Your training templates are synchronized across your garrisons and game saves."));

        public HintViewModel EstimatedCostsHint { get; } = new HintViewModel(new TextObject("{=ui_trainingui_dailywagetemplatehint}The daily wage for each troop in your template."));

        public string EstimatedTemplateCosts
        {
            get
            {
                return _estimatedTemplateCosts;
            }
            set
            {
                if (value != _estimatedTemplateCosts)
                {
                    _estimatedTemplateCosts = value;
                    OnPropertyChangedWithValue(value, "EstimatedTemplateCosts");
                }
            }
        }

        public TrainingUIVM()
        {
            InitializeAll();
            RefreshValues();
        }

        public void InitializeAll()
        {
            InitializeTrainingSettings();
            InitializeTroops();
        }

        private void InitializeTrainingSettings()
        {
            TrainingSettingsVM = new MBBindingList<ImprovedGarrisonsOptionVM>();
            if (CurrentGarrisonSettings != null)
            {
                TrainingSettingsVM.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_recruitmentui_vanillatraining}Vanilla training").ToString(), delegate
                {
                    return CurrentGarrisonSettings.VanillaTraining;
                }, delegate (bool x)
                {
                    TrainingSettings.Instance.ToggleVanillaTraining(Main.GarrisonBehavior.CurrentTownForSettings, x);
                }, new TextObject("{=ui_recruitmentui_vanillatraining2}Enable the garrison training of the base game. \n \nThis setting sets the garrison wage limit to 0 when disabled, which stops the wage control of the base game. The vanilla training is NOT controlled by Improved Garrison and may conflict with your template settings. If you enable this setting, the base game will downgrade and upgrade units without the control of Improved Garrison. \n \nIt is highly recommended to keep this option disabled for a better control of your garrison.")));
                TrainingSettingsVM.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_trainingui_garrisontraining1}Improved garrison training").ToString(), delegate
                {
                    return CurrentGarrisonSettings.EnableTraining;
                }, delegate (bool x)
                {
                    TrainingSettings.Instance.ToggleTraining(Main.GarrisonBehavior.CurrentTownForSettings, x);
                }, new TextObject("{=ui_trainingui_garrisontraining2}Allow this garrison to train its troops. \n \nYou may select the maximum tier the garrison should train to and/or set a training template to determine the upgrade paths.")));
                TrainingSettingsVM.Add(new ImprovedGarrisonsOptionVM().SetAsSliderOption(new TextObject("{=ui_trainingui_maxupgradetier1}Max upgrade tier for troops \n (Non template)").ToString(), delegate
                {
                    return CurrentGarrisonSettings.MaxUpgradeTier;
                }, 1f, 10f, discrete: true, delegate (float x)
                {
                    TrainingSettings.Instance.SetTownMaxUpgradeTier(Main.GarrisonBehavior.CurrentTownForSettings, (int)x);
                }, new TextObject("{=ui_trainingui_maxupgradetier2}The maximum tier this garrison will train troops that are NOT specified in the current training template.")));
            }
        }

        private void InitializeTroops()
        {
            Troops = new MBBindingList<ImprovedGarrisonsTroopItemWidgetVM>();
            if (CurrentGarrisonSettings == null)
            {
                return;
            }
            foreach (CharacterObject item in CharacterObject.All)
            {
                if (item.StringId != null && CurrentGarrisonSettings.Template.Contains(item))
                {
                    int num = CurrentGarrisonSettings.Template.GetAmountForTemplateTroop(item);
                    if (num <= 0)
                    {
                        num = 9999;
                    }
                    TroopRosterElement troopRosterElement = new TroopRosterElement(item);
                    troopRosterElement.Number = num;
                    TroopRosterElement troop = troopRosterElement;
                    Troops.Add(new ImprovedGarrisonsTroopItemWidgetVM(troop, this));
                }
            }
            OnPropertyChanged("Troops");
        }

        public void OpenTemplateManager()
        {
            try
            {
                TemplateManager.Instance.PromptTemplateManager(Main.GarrisonBehavior.CurrentTownForSettings, this);
            }
            catch (Exception ex)
            {
                LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
            }
        }

        public void AddNewCurrentTemplateTroop()
        {
            try
            {
                TrainingSettings.Instance.PromptFilterForNewTroopsToAdd(Main.GarrisonBehavior.CurrentTownForSettings, this);
            }
            catch (Exception ex)
            {
                LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
            }
        }

        public void CalculateTemplateWage()
        {
            try
            {
                int num = 0;
                Dictionary<CharacterObject, int> troopListAsCharacterObjects = CurrentGarrisonSettings.Template.GetTroopListAsCharacterObjects();
                if (troopListAsCharacterObjects != null)
                {
                    foreach (KeyValuePair<CharacterObject, int> item in troopListAsCharacterObjects)
                    {
                        num += item.Key.TroopWage * item.Value;
                    }
                }
                EstimatedTemplateCosts = new TextObject("{=ui_trainingui_dailywagetext}Daily wage:").ToString() + " " + num;
            }
            catch (Exception ex)
            {
                LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
            }
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            foreach (ImprovedGarrisonsOptionVM item in TrainingSettingsVM)
            {
                item.RefreshValues();
            }
            if (TroopListIsDirty)
            {
                InitializeTroops();
                TroopListIsDirty = false;
                CalculateTemplateWage();
            }
        }
    }
}
