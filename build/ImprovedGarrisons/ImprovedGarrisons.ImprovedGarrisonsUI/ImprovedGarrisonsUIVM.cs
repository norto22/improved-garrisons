using System;
using System.Collections.Generic;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.HintManager;
using ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI
{
    public class ImprovedGarrisonsUIVM : ViewModel
    {
        public const string OverviewTabString = "OverviewTab";

        public const string TrainingTabString = "TrainingTab";

        public const string RecruitmentTabString = "RecruitmentTab";

        public const string GuardsTabString = "GuardsTab";

        public const string ManagementTabString = "ManagementTab";

        public const string GarrisonTabString = "GarrisonTab";

        private bool _mapToggleValue = GlobalSettings.Instance.EnableImprovedGarrisonsUIOnMap;

        private bool overviewNeedsUpdate;

        private SelectorVM<SelectorItemVM> _playerSettlementsSelector;

        public OverviewUIVM OverviewDatasource { get; set; } = new OverviewUIVM();

        public GuardsUIVM GuardsDatasource { get; set; } = new GuardsUIVM();

        public RecruitmentUIVM RecruitmentDatasource { get; set; } = new RecruitmentUIVM();

        public TrainingUIVM TrainingDatasource { get; set; } = new TrainingUIVM();

        public ManagementUIVM ManagementDatasource { get; set; } = new ManagementUIVM();

        public GarrisonUIVM GarrisonDatasource { get; set; } = new GarrisonUIVM();

        public HintManagerVM HintManagerDatasource { get; set; } = new HintManagerVM();

        public string Title { get; } = new TextObject("{=ui_improvedgarrisonsui_title}Improved Garrisons").ToString();

        public string OverviewTabText { get; } = new TextObject("{=ui_improvedgarrisonsui_overview}Overview").ToString();

        public string RecruitmentTabText { get; } = new TextObject("{=ui_improvedgarrisonsui_recruitment}Recruitment").ToString();

        public string GuardTabText { get; } = new TextObject("{=ui_improvedgarrisonsui_guards}Guards").ToString();

        public string TrainingTabText { get; } = new TextObject("{=ui_improvedgarrisonsui_training}Training").ToString();

        public string ManagementTabText { get; } = new TextObject("{=ui_improvedgarrisonsui_management}Management").ToString();

        public string FinanceTabText { get; } = new TextObject("{=ui_improvedgarrisonsui_finance}Finance").ToString();

        public string GarrisonTabText { get; } = new TextObject("{=ui_improvedgarrisonsui_garrison}Garrison").ToString();

        public string MapToggleText { get; } = new TextObject("{=ui_improvedgarrisonsui_maptoggle}Display on map").ToString();

        public string TutorialButtonText { get; } = new TextObject("{=ui_improvedgarrisonsui_tutorialtext}Tutorial").ToString();

        public string ConfigurationManagerButtonText { get; } = new TextObject("{=ui_improvedgarrisonsui_configurationtext}Configuration").ToString();

        public string ResetSettingsButtonText { get; } = new TextObject("{=ui_improvedgarrisonsui_defaulttext}Default settings").ToString();

        public HintViewModel MapToggleHint { get; } = new HintViewModel(new TextObject("{=ui_improvedgarrisonsui_maptogglehint}Display the Improved Garrisons menu on the overworld map."));

        public HintViewModel TutorialHint { get; } = new HintViewModel(new TextObject("{=ui_improvedgarrisonsui_tutorialhint}Start the Improved Garrisons tutorial."));

        public HintViewModel ConfigmanagerHint { get; } = new HintViewModel(new TextObject("{=ui_improvedgarrisonsui_configurationhint}Display the Improved Garrisons settings."));

        public HintViewModel ResetSettingsHint { get; } = new HintViewModel(new TextObject("{=ui_improvedgarrisonsui_resethint}Reset the currently selected location to its default settings."));

        public bool MapToggleValue
        {
            get
            {
                return _mapToggleValue;
            }
            set
            {
                if (value != _mapToggleValue)
                {
                    _mapToggleValue = value;
                    OnPropertyChanged("MapToggleValue");
                    StayOnMapToggle(value);
                }
            }
        }

        public SelectorVM<SelectorItemVM> PlayerSettlementsSelector
        {
            get
            {
                if (_playerSettlementsSelector == null)
                {
                    List<Settlement> allPlayerSettlements = Main.GarrisonBehavior.GetAllPlayerSettlements();
                    Settlement currentSettlement = Settlement.CurrentSettlement;
                    _playerSettlementsSelector = new SelectorVM<SelectorItemVM>(0, OnSelectorChange);
                    bool flag = Clan.PlayerClan != null && Main.GarrisonBehavior.CurrentTownForSettings != null && Clan.PlayerClan == Main.GarrisonBehavior.CurrentTownForSettings.OwnerClan;
                    foreach (Settlement item2 in allPlayerSettlements)
                    {
                        SelectorItemVM item = new SelectorItemVM(item2.Name.ToString());
                        _playerSettlementsSelector.AddItem(item);
                    }
                }
                if (_playerSettlementsSelector.SelectedIndex < 0 && _playerSettlementsSelector.ItemList.Count > 0)
                {
                    _playerSettlementsSelector.SelectedIndex = 0;
                }
                return _playerSettlementsSelector;
            }
        }

        public ImprovedGarrisonsUIVM()
        {
            UpdateUiContents();
        }

        public void UpdateUiContents()
        {
            OverviewDatasource = new OverviewUIVM();
            OnPropertyChanged("OverviewDatasource");
            GuardsDatasource = new GuardsUIVM();
            OnPropertyChanged("GuardsDatasource");
            RecruitmentDatasource = new RecruitmentUIVM();
            OnPropertyChanged("RecruitmentDatasource");
            TrainingDatasource = new TrainingUIVM();
            OnPropertyChanged("TrainingDatasource");
            ManagementDatasource = new ManagementUIVM();
            OnPropertyChanged("ManagementDatasource");
            GarrisonDatasource = new GarrisonUIVM();
            OnPropertyChanged("GarrisonDatasource");
        }

        public void ForceOverviewUpdate()
        {
            overviewNeedsUpdate = true;
        }

        public void UpdateSettlementSelector()
        {
            _playerSettlementsSelector = null;
            OnPropertyChanged("PlayerSettlementsSelector");
        }

        public void ChangeSelectorSelectionToCurrentSettlement()
        {
            UpdateSettlementSelector();
            if (Settlement.CurrentSettlement == null || _playerSettlementsSelector == null)
            {
                return;
            }
            Settlement currentSettlement = Settlement.CurrentSettlement;
            MBBindingList<SelectorItemVM> itemList = _playerSettlementsSelector.ItemList;
            for (int i = 0; i < _playerSettlementsSelector.ItemList.Count; i++)
            {
                if (itemList[i].StringItem.Equals(currentSettlement.Name.ToString()))
                {
                    _playerSettlementsSelector.SelectedIndex = i;
                }
            }
        }

        public void ChangeSelectorSelection(Settlement settlement)
        {
            UpdateSettlementSelector();
            if (settlement == null || _playerSettlementsSelector == null)
            {
                return;
            }
            MBBindingList<SelectorItemVM> itemList = _playerSettlementsSelector.ItemList;
            for (int i = 0; i < _playerSettlementsSelector.ItemList.Count; i++)
            {
                if (itemList[i].StringItem.Equals(settlement.Name.ToString()))
                {
                    _playerSettlementsSelector.SelectedIndex = i;
                }
            }
        }

        private void OnSelectorChange(SelectorVM<SelectorItemVM> selector)
        {
            try
            {
                if (selector == null)
                {
                    return;
                }
                SelectorItemVM currentItem = selector.GetCurrentItem();
                if (currentItem != null)
                {
                    Settlement settlementFromName = Main.GarrisonBehavior.GetSettlementFromName(currentItem.StringItem);
                    if (settlementFromName != null)
                    {
                        Main.GarrisonBehavior.CurrentTownForSettings = settlementFromName.Town;
                    }
                    UpdateUiContents();
                }
            }
            catch (Exception ex)
            {
                LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
            }
        }

        private Settlement GetCurrentlySelectedSettlement()
        {
            SelectorItemVM currentItem = PlayerSettlementsSelector.GetCurrentItem();
            if (currentItem == null)
            {
                return null;
            }
            return Main.GarrisonBehavior.GetSettlementFromName(currentItem.StringItem);
        }

        public void OnTutorialButtonClick()
        {
            UIManager.Instance.StartTutorial(onMapTutorial: true);
        }

        public void OnConfigurationButtonClick()
        {
            Main.OpenConfigurationScreen();
        }

        public void OnResetSettingsButtonClick()
        {
            InformationManager.ShowInquiry(new InquiryData(new TextObject("{=ui_improvedgarrisonsui_resetdefault_title}Reset the location to its default settings").ToString(), new TextObject("{=ui_improvedgarrisonsui_resetdefault_description}Are you sure you want to reset the currently selected location to its default settings?").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: true, new TextObject("{=menu_yes}Yes").ToString(), new TextObject("{=menu_cancel}Cancel").ToString(), delegate
            {
                Settlement currentlySelectedSettlement = GetCurrentlySelectedSettlement();
                if (currentlySelectedSettlement != null)
                {
                    Main.GarrisonBehavior.ResetTownSettings(currentlySelectedSettlement.Town);
                }
                UpdateUiContents();
            }, delegate
            {
            }));
        }

        public void StayOnMapToggle(bool value)
        {
            GlobalSettings.Instance.EnableImprovedGarrisonsUIOnMap = value;
        }

        public void ForceFullRefresh()
        {
            switch (UIManager.Instance.improvedGarrisonsUI.ActualCurrentTabId)
            {
                case "OverviewTab":
                    OverviewDatasource.RefreshValues();
                    break;
                case "TrainingTab":
                    TrainingDatasource.InitializeAll();
                    break;
                case "RecruitmentTab":
                    RecruitmentDatasource.InitializeAll();
                    break;
                case "GuardsTab":
                    GuardsDatasource.InitializeAll();
                    break;
                case "ManagementTab":
                    ManagementDatasource.InitializeAll();
                    break;
                case "GarrisonTab":
                    GarrisonDatasource.InitializeAll();
                    break;
            }
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            HintManagerDatasource.RefreshValues();
            switch (UIManager.Instance.improvedGarrisonsUI.ActualCurrentTabId)
            {
                case "OverviewTab":
                    OverviewDatasource.RefreshValues();
                    overviewNeedsUpdate = false;
                    break;
                case "TrainingTab":
                    TrainingDatasource.RefreshValues();
                    break;
                case "RecruitmentTab":
                    RecruitmentDatasource.RefreshValues();
                    break;
                case "GuardsTab":
                    GuardsDatasource.RefreshValues();
                    break;
                case "ManagementTab":
                    ManagementDatasource.RefreshValues();
                    break;
                case "GarrisonTab":
                    GarrisonDatasource.RefreshValues();
                    break;
            }
        }
    }
}
