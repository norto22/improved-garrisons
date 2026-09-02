using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.ImprovedGarrisonsUI.UIElements;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus
{
    public class RecruitmentUIVM : ViewModel
    {
        private MBBindingList<ImprovedGarrisonsOptionVM> _recruitmentSettings;

        private MBBindingList<ImprovedGarrisonsInformationListVM> _recruiterInformation;

        private bool _hasNoActiveRecruiter;

        private bool _hasActiveRecruiter;

        private string _recruiterStatus;

        private GarrisonSettings CurrentGarrisonSettings => Main.GarrisonBehavior.GetCurrentTownSettings();

        private GarrisonRecruiter CurrentRecruiter
        {
            get
            {
                Town currentTownForSettings = Main.GarrisonBehavior.CurrentTownForSettings;
                if (currentTownForSettings != null)
                {
                    return Main.PartyManagement.garrisonRecruiterPartyManagement.GetRecruiterOfSettlement(currentTownForSettings.Settlement);
                }
                return null;
            }
        }

        public string RecruiterInfoText { get; } = new TextObject("{=ui_recruitmentui_recruiterinfo}Recruiter information").ToString();

        public string CreateRecruiterText { get; } = new TextObject("{=ui_recruitmentui_createrecruiter}Create a new recruiter").ToString();

        public string RegionRecruitmentInfoText { get; } = new TextObject("{=ui_recruitmentui_regionrecruitment}Recruitment settings").ToString();

        public string ReturnRecruiterText { get; } = new TextObject("{=ui_recruitmentui_returnrecruiter}Return recruiter").ToString();

        public HintViewModel RecruiterHint { get; } = new HintViewModel(new TextObject("{=ui_recruitmentui_recruiterdesc}A recruiter is used to recruit new troops from outside this fief's region."));

        public HintViewModel RegionRecruitmentHint { get; } = new HintViewModel(new TextObject("{=ui_recruitmentui_regionrecruitmentdesc}The garrison will recruit troops from nearby villages and the current castle or town. \nTo recruit outside of this region, use a recruiter party."));

        public MBBindingList<ImprovedGarrisonsOptionVM> RecruitmentSettings
        {
            get
            {
                return _recruitmentSettings;
            }
            set
            {
                if (value != _recruitmentSettings)
                {
                    _recruitmentSettings = value;
                    OnPropertyChangedWithValue(value, "RecruitmentSettings");
                }
            }
        }

        private MBBindingList<ImprovedGarrisonsOptionVM> RecruitmentSettingsWithTemplate { get; set; }

        private MBBindingList<ImprovedGarrisonsOptionVM> RecruitmentSettingsNonTemplate { get; set; }

        public MBBindingList<ImprovedGarrisonsInformationListVM> RecruiterInformation
        {
            get
            {
                return _recruiterInformation;
            }
            set
            {
                if (value != _recruiterInformation)
                {
                    _recruiterInformation = value;
                    OnPropertyChangedWithValue(value, "RecruiterInformation");
                }
            }
        }

        public ImprovedGarrisonsOptionVM ToggleRegionRecruitment { get; set; }

        public ImprovedGarrisonsOptionVM TogglePrisonerRecruitment { get; set; }

        public ImprovedGarrisonsOptionVM ToggleVanillaRecruitment { get; set; }

        public ImprovedGarrisonsOptionVM ToggleFollowTemplate { get; set; }

        public bool HasNoActiveRecruiter
        {
            get
            {
                return _hasNoActiveRecruiter;
            }
            set
            {
                if (value != _hasNoActiveRecruiter)
                {
                    _hasNoActiveRecruiter = value;
                    OnPropertyChangedWithValue(value, "HasNoActiveRecruiter");
                    HasActiveRecruiter = !_hasNoActiveRecruiter;
                }
                if (_hasActiveRecruiter == _hasNoActiveRecruiter)
                {
                    HasActiveRecruiter = !_hasNoActiveRecruiter;
                }
            }
        }

        public bool HasActiveRecruiter
        {
            get
            {
                return _hasActiveRecruiter;
            }
            set
            {
                if (value != _hasActiveRecruiter)
                {
                    _hasActiveRecruiter = value;
                    OnPropertyChangedWithValue(value, "HasActiveRecruiter");
                }
            }
        }

        public string RecruiterStatus
        {
            get
            {
                return _recruiterStatus;
            }
            set
            {
                if (value != _recruiterStatus)
                {
                    _recruiterStatus = value;
                    OnPropertyChangedWithValue(value, "RecruiterStatus");
                }
            }
        }

        public RecruitmentUIVM()
        {
            ToggleRegionRecruitment = new ImprovedGarrisonsOptionVM();
            ToggleRegionRecruitment.SetAsBooleanOption(new TextObject("{=ui_recruitmentui_regionrecruitmentenable1}Recruit from region").ToString(), delegate { return CurrentGarrisonSettings != null && CurrentGarrisonSettings.EnableRecruitFromRegion; }, delegate (bool x)
            {
                ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.ToggleRegionRecruitment(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_recruitmentui_regionrecruitmentenable2}Allow this garrison to recruit troops from the villages and the town or castle."));
            TogglePrisonerRecruitment = new ImprovedGarrisonsOptionVM();
            TogglePrisonerRecruitment.SetAsBooleanOption(new TextObject("{=ui_recruitmentui_recruitprisoners1}Recruit prisoners").ToString(), delegate { return CurrentGarrisonSettings != null && CurrentGarrisonSettings.EnablePrisonerRecruitment; }, delegate (bool x)
            {
                ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.TogglePrisonerRecruitment(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_recruitmentui_recruitprisoners2}Allow this garrison to recruit prisoners that are in this location's dungeon over time."));
            ToggleFollowTemplate = new ImprovedGarrisonsOptionVM();
            ToggleFollowTemplate.SetAsBooleanOption(new TextObject("{=ui_recruitmentui_recruitmentfollowstemplate}Recruitment \n follows template").ToString(), delegate { return CurrentGarrisonSettings != null && CurrentGarrisonSettings.RecruitmentFollowsTemplate; }, delegate (bool x)
            {
                TrainingSettings.Instance.ToggleFollowTemplate(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_recruitmentui_recruitmentfollowstemplate2}This garrison will only recruit troops that are either part of the current training template or are needed for an upgrade towards a template troop."));
            ToggleVanillaRecruitment = new ImprovedGarrisonsOptionVM();
            ToggleVanillaRecruitment.SetAsBooleanOption(new TextObject("{=ui_recruitmentui_vanillarecruitment}Vanilla recruitment").ToString(), delegate { return CurrentGarrisonSettings != null && CurrentGarrisonSettings.VanillaRecruitment; }, delegate (bool x)
            {
                ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.ToggleVanillaRecruitment(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_recruitmentui_vanillarecruitment2}Enable the garrison recruitment of the base game. \n \nThis setting sets the automatic recruitment option of this garrison to false when disabled, which stops the daily recruitment of soldiers to the garrison. The vanilla recruitment is NOT controlled by Improved Garrison and may conflict with your template settings. \n \nIt is highly recommended to keep this option disabled for a better control of your garrison."));
            InitializeAll();
            RefreshValues();
        }

        public void InitializeAll()
        {
            InitializeRecruitmentSettings();
            InitializeRecruiterInformation();
        }

        private void InitializeRecruiterInformation()
        {
            RecruiterInformation = new MBBindingList<ImprovedGarrisonsInformationListVM>();
            RecruiterInformation.Add(new ImprovedGarrisonsInformationListVM(new TextObject("{=ui_recruitmentui_partysize}Party size").ToString(), "0", () => (CurrentRecruiter != null) ? CurrentRecruiter.mobileParty.Party.NumberOfAllMembers.ToString() : "0", "General\\Icons\\Party@2x"));
            RecruiterInformation.Add(new ImprovedGarrisonsInformationListVM(new TextObject("{=ui_recruitmentui_recruitedtroops}Recruited troops").ToString(), "0", () => (CurrentRecruiter != null) ? CurrentRecruiter.GetNumberOfRecruited().ToString() : "0", "General\\Icons\\PartyCost@2x"));
            RecruiterInformation.Add(new ImprovedGarrisonsInformationListVM(new TextObject("{=ui_recruitmentui_culture}Culture").ToString(), new TextObject("{=misc_any}any").ToString(), () => (CurrentGarrisonSettings != null) ? (CurrentGarrisonSettings.RecruitmentFollowsTemplate ? new TextObject("{=misc_template}template").ToString() : ((CurrentGarrisonSettings.RecruiterCultureToRecruit != null) ? CurrentGarrisonSettings.RecruiterCultureToRecruit : new TextObject("{=misc_any}any").ToString())) : new TextObject("{=misc_any}any").ToString(), "General\\Icons\\Walls"));
        }

        private void InitializeRecruitmentSettings()
        {
            InitializeRecruitmentSettingsWithoutTemplate();
            InitializeRecruitmentSettingsWithTemplate();
            if (CurrentGarrisonSettings.RecruitmentFollowsTemplate)
            {
                RecruitmentSettings = RecruitmentSettingsWithTemplate;
            }
            else
            {
                RecruitmentSettings = RecruitmentSettingsNonTemplate;
            }
        }

        private void InitializeRecruitmentSettingsWithoutTemplate()
        {
            RecruitmentSettingsNonTemplate = new MBBindingList<ImprovedGarrisonsOptionVM>();
            RecruitmentSettingsNonTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsTitle(new TextObject("{=ui_recruitmentui_recruitmentwithouttemplate_regiontitle}Region and prisoner recruitment settings").ToString()));
            int num = 350;
            if (Main.GarrisonBehavior.CurrentTownForSettings != null && Main.GarrisonBehavior.CurrentTownForSettings.GarrisonParty != null)
            {
                num = Main.PartyManagement.GetPartySizeLimit(Main.GarrisonBehavior.CurrentTownForSettings.GarrisonParty.Party);
                num = ((num < 0) ? 350 : num);
            }
            RecruitmentSettingsNonTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsSliderOption(new TextObject("{=ui_recruitmentui_recruitthreshold1}Maximum number of troops to recruit").ToString(), delegate { return CurrentGarrisonSettings.MaxRecruitThreshold; }, 0f, num, discrete: true, delegate (float x)
            {
                ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.SetRecruitmentThreshold(Main.GarrisonBehavior.CurrentTownForSettings, (int)x);
            }, new TextObject("{=ui_recruitmentui_recruitthreshold2}The maximum number of units this garrison will recruit from the dungeon and its region.")));
            RecruitmentSettingsNonTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_recruitmentui_recruitelite1}Recruit only elite troops from this region").ToString(), delegate { return CurrentGarrisonSettings.RecruitOnlyEliteUnits; }, delegate (bool x)
            {
                ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.ToggleRecruitOnlyElite(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_recruitmentui_recruitelite2}Force this garrison's region recruitment to only recruit elite units.")));
            RecruitmentSettingsNonTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_recruitmentui_prisonerthreshold1}Recruit prisoners above the threshold").ToString(), delegate { return CurrentGarrisonSettings.AllowPrisonerRecruitAboveThreshold; }, delegate (bool x)
            {
                ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.TogglePrisonerRecruitmentAboveThreshold(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_recruitmentui_prisonerthreshold2}Allow this garrison to recruit prisoners above the region recruitment threshold.")));
            RecruitmentSettingsNonTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsTitle(new TextObject("{=ui_recruitmentui_recruitmentwithouttemplate_recruitertitle}Recruiter recruitment settings").ToString()));
            RecruitmentSettingsNonTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_recruitmentui_recruiteronlyelite1}Recruiter only recruits elite troops").ToString(), delegate { return CurrentGarrisonSettings.RecruiterRecruitOnlyElites; }, delegate (bool x)
            {
                ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.ToggleRecruiterOnlyElites(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_recruitmentui_recruiteronlyelite2}If enabled, this garrison's recruiter will only gather elite units.")));
            RecruitmentSettingsNonTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsSliderOption(new TextObject("{=ui_recruitmentui_recruiterrecruitamount1}Recruitment headcount").ToString(), delegate { return CurrentGarrisonSettings.RecruiterRecruitAmount; }, 1f, 150f, discrete: true, delegate (float x)
            {
                ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.SetRecruiterAmountToRecruit(Main.GarrisonBehavior.CurrentTownForSettings, (int)x);
            }, new TextObject("{=ui_recruitmentui_recruiterrecruitamount2}Set the number of troops the recruiter should gather before returning")));
            RecruitmentSettingsNonTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_recruitmentui_recruiterhorses1}Allow to buy horses").ToString(), delegate { return CurrentGarrisonSettings.RecruiterAllowHorseBuy; }, delegate (bool x)
            {
                ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.ToggleRecruiterBuyHorses(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_recruitmentui_recruiterhorses2}Allow this garrison's recruiter party to buy horses to increase movement speed. \n \n(It can be expensive!)")));
            RecruitmentSettingsNonTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsButtonOption(new TextObject("{=ui_recruitmentui_recruiterculture1}Change the culture the recruiter recruits from").ToString(), delegate
            {
                ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.PromptChangeRecruitmentCulture(Main.GarrisonBehavior.CurrentTownForSettings);
            }, new TextObject("{=ui_recruitmentui_recruiterculture2}Change the culture this garrison's recruiter should recruit from.")));
        }

        private void InitializeRecruitmentSettingsWithTemplate()
        {
            RecruitmentSettingsWithTemplate = new MBBindingList<ImprovedGarrisonsOptionVM>();
            RecruitmentSettingsWithTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsTitle(new TextObject("{=ui_recruitmentui_recruitmentwithtemplate}Recruitment settings with template").ToString()));
            RecruitmentSettingsWithTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_trainingui_autogather1}Automatic recruiter creation").ToString(), delegate { return CurrentGarrisonSettings != null && CurrentGarrisonSettings.RecruiterAutoSpawn; }, delegate (bool x)
            {
                TrainingSettings.Instance.ToggleAutoSpawn(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_trainingui_autogather2}Allow this garrison to automatically create recruiters to gather all the necessary troops needed for your current training template. \n \nThis feature makes it incredibly convenient to train your armies. Just enable this setting, set up your training template and wait until the garrison gathers troops from all the necessary cultures and trains the units you desire.")));
            RecruitmentSettingsWithTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsSliderOption(new TextObject("{=ui_recruitmentui_recruiterrecruitamount1}Recruitment headcount").ToString(), delegate { return CurrentGarrisonSettings.RecruiterRecruitAmount; }, 1f, 150f, discrete: true, delegate (float x)
            {
                ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.SetRecruiterAmountToRecruit(Main.GarrisonBehavior.CurrentTownForSettings, (int)x);
            }, new TextObject("{=ui_recruitmentui_recruiterrecruitamount2}Set the number of troops the recruiter should gather before returning")));
            RecruitmentSettingsWithTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_recruitmentui_prisonertemplate1}Prisoner recruitment ignores template").ToString(), delegate { return CurrentGarrisonSettings.PrisonerRecruitmentIgnoresTemplate; }, delegate (bool x)
            {
                ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.TogglePrisonerRecruitmentIgnoresTemplate(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_recruitmentui_prisonertemplate2}The garrison will recruit your prisoners even if they are not part of your template.")));
            RecruitmentSettingsWithTemplate.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_recruitmentui_recruiterhorses1}Allow to buy horses").ToString(), delegate { return CurrentGarrisonSettings.RecruiterAllowHorseBuy; }, delegate (bool x)
            {
                ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.ToggleRecruiterBuyHorses(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_recruitmentui_recruiterhorses2}Allow this garrison's recruiter party to buy horses to increase movement speed. \n \n(It can be expensive!)")));
        }

        public void OnCreateRecruitButtonPress()
        {
            ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.PromptCreateRecruiter(Main.GarrisonBehavior.CurrentTownForSettings);
        }

        public void OnReturnRecruiterButtonPress()
        {
            ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.RecruitmentSettings.Instance.ReturnRecruiter(Main.GarrisonBehavior.CurrentTownForSettings);
        }

        public void ExecuteLink(string link)
        {
            Campaign.Current.EncyclopediaManager.GoToLink(link);
        }

        public void ForceFullRefresh()
        {
            InitializeRecruitmentSettings();
            InitializeRecruiterInformation();
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            HasNoActiveRecruiter = CurrentRecruiter == null;
            if (CurrentRecruiter != null)
            {
                RecruiterStatus = CurrentRecruiter.GetStatusText();
            }
            else
            {
                bool recruiterAutoSpawn = CurrentGarrisonSettings.RecruiterAutoSpawn;
                bool enableTraining = CurrentGarrisonSettings.EnableTraining;
                bool recruitmentFollowsTemplate = CurrentGarrisonSettings.RecruitmentFollowsTemplate;
                Town currentTownForSettings = Main.GarrisonBehavior.CurrentTownForSettings;
                bool flag = currentTownForSettings != null && currentTownForSettings.GarrisonParty != null && ((currentTownForSettings.GarrisonParty.Party.NumberOfAllMembers > 0) ? true : false);
                TrainingTemplate template = CurrentGarrisonSettings.Template;
                bool flag2 = template != null && template.AmountOfTroopsInTemplate > 0;
                if (!flag)
                {
                    RecruiterStatus = new TextObject("{=ui_recruitmentui_notenoughtroops}Not enough troops to automatically spawn a recruiter party!").ToString();
                }
                else if (recruitmentFollowsTemplate && recruiterAutoSpawn && !enableTraining)
                {
                    RecruiterStatus = new TextObject("{=ui_recruitmentui_trainingdisabled}Training has to be enabled to automatically spawn a recruiter party!").ToString();
                }
                else if (recruitmentFollowsTemplate && recruiterAutoSpawn && !flag2)
                {
                    RecruiterStatus = new TextObject("{=ui_recruitmentui_notemplate}A training template is needed to automatically spawn a recruiter!").ToString();
                }
                else
                {
                    RecruiterStatus = new TextObject("{=ui_recruitmentui_norecruiter}There is no active recruiter party").ToString();
                }
            }
            foreach (ImprovedGarrisonsInformationListVM item in RecruiterInformation)
            {
                item.RefreshValues();
            }
            if (CurrentGarrisonSettings.RecruitmentFollowsTemplate)
            {
                RecruitmentSettings = RecruitmentSettingsWithTemplate;
            }
            else
            {
                RecruitmentSettings = RecruitmentSettingsNonTemplate;
            }
            foreach (ImprovedGarrisonsOptionVM item2 in RecruitmentSettings)
            {
                item2.RefreshValues();
            }
            ToggleRegionRecruitment.RefreshValues();
            TogglePrisonerRecruitment.RefreshValues();
            ToggleFollowTemplate.RefreshValues();
            ToggleVanillaRecruitment.RefreshValues();
        }
    }
}
