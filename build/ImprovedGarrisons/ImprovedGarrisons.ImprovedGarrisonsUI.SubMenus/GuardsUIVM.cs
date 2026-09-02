using System.Collections.Generic;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus.GuardsUtils;
using ImprovedGarrisons.ImprovedGarrisonsUI.UIElements;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus
{
    public class GuardsUIVM : ViewModel
    {
        private bool _hasNoActiveGuard;

        private string _guardStatus;

        private GarrisonSettings CurrentGarrisonSettings => Main.GarrisonBehavior.GetCurrentTownSettings();

        private MobileGarrison CurrentMobileGarrison
        {
            get
            {
                Town currentTownForSettings = Main.GarrisonBehavior.CurrentTownForSettings;
                if (currentTownForSettings != null)
                {
                    return Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(currentTownForSettings.Settlement);
                }
                return null;
            }
        }

        public MBBindingList<ImprovedGarrisonsOptionVM> GuardSettings { get; set; }

        public MBBindingList<ImprovedGarrisonsTroopItemWidgetVM> GuardTroops { get; set; }

        public MBBindingList<GuardOrdersVM> GuardOrders { get; set; }

        public MBBindingList<ImprovedGarrisonsInformationListVM> GuardInformation { get; set; }

        public ImprovedGarrisonsOptionVM ToggleAutoGuardCreation { get; set; }

        public string GuardInfoText { get; } = new TextObject("{=ui_guardsui_infotitle}Guard party information").ToString();

        public string CreateGuardText { get; } = new TextObject("{=ui_guardsui_createguard}Create a new guard party").ToString();

        public string GuardOrdersTitleText { get; } = new TextObject("{=ui_guardsui_guardorders}Guard party orders").ToString();

        public bool HasNoActiveGuard
        {
            get
            {
                return _hasNoActiveGuard;
            }
            set
            {
                if (value != _hasNoActiveGuard)
                {
                    _hasNoActiveGuard = value;
                    OnPropertyChangedWithValue(value, "HasNoActiveGuard");
                }
            }
        }

        public string GuardStatus
        {
            get
            {
                return _guardStatus;
            }
            set
            {
                if (value != _guardStatus)
                {
                    _guardStatus = value;
                    OnPropertyChangedWithValue(value, "GuardStatus");
                }
            }
        }

        public GuardsUIVM()
        {
            InitializeAll();
            ToggleAutoGuardCreation = new ImprovedGarrisonsOptionVM();
            ToggleAutoGuardCreation.SetAsBooleanOption(new TextObject("{=ui_guardsui_autocreateguards1}Automatic guard party creation").ToString(), delegate { return CurrentGarrisonSettings != null && CurrentGarrisonSettings.GuardsAutoSpawn; }, delegate (bool x)
            {
                MobileGarrisonSettings.Instance.ToggleAutoGuards(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_guardsui_autocreateguards2}Allow this garrison to automatically create a guard party."));
            RefreshValues();
        }

        public void InitializeAll()
        {
            InitializeGuardSettings();
            InitializeGuardOrders();
            InitializeGuardInformation();
        }

        private void InitializeGuardInformation()
        {
            GuardInformation = new MBBindingList<ImprovedGarrisonsInformationListVM>();
            GuardInformation.Add(new ImprovedGarrisonsInformationListVM(new TextObject("{=ui_guardsui_healthytroops}Healthy troops").ToString(), "0", () => (CurrentMobileGarrison != null) ? CurrentMobileGarrison.mobileParty.Party.NumberOfAllMembers.ToString() : "0", "General\\Icons\\Party@2x"));
            GuardInformation.Add(new ImprovedGarrisonsInformationListVM(new TextObject("{=ui_guardsui_woundedtroops}Wounded troops").ToString(), "0", () => (CurrentMobileGarrison != null) ? CurrentMobileGarrison.mobileParty.Party.NumberOfWoundedTotalMembers.ToString() : "0", "General\\Icons\\Health@2x"));
            GuardInformation.Add(new ImprovedGarrisonsInformationListVM(new TextObject("{=ui_guardsui_prisoners}Prisoners").ToString(), "0", () => (CurrentMobileGarrison != null) ? CurrentMobileGarrison.mobileParty.Party.NumberOfPrisoners.ToString() : "0", "General\\Icons\\TroopCost@2x"));
        }

        private void InitializeGuardOrders()
        {
            GuardOrders = new MBBindingList<GuardOrdersVM>();
            GuardOrders.Add(new GuardOrdersVM(new TextObject("{=ui_guardsui_orderpatrol}Order to patrol").ToString(), delegate
            {
                MobileGarrisonSettings.Instance.OrderMobileGarrisonToPatrol(Main.GarrisonBehavior.CurrentTownForSettings);
            }));
            GuardOrders.Add(new GuardOrdersVM(new TextObject("{=ui_guardsui_orderescort}Order to escort").ToString(), delegate
            {
                MobileGarrisonSettings.Instance.PromptMobileGarrisonEscort(Main.GarrisonBehavior.CurrentTownForSettings);
            }));
            GuardOrders.Add(new GuardOrdersVM(new TextObject("{=ui_guardsui_orderreturn}Order to return").ToString(), delegate
            {
                MobileGarrisonSettings.Instance.OrderMobileGarrisonReturn(Main.GarrisonBehavior.CurrentTownForSettings);
            }));
        }

        private void InitializeGuardSettings()
        {
            GuardSettings = new MBBindingList<ImprovedGarrisonsOptionVM>();
            GuardSettings.Add(new ImprovedGarrisonsOptionVM().SetAsTitle(new TextObject("{=ui_guardsui_creationtitle}Smart guard creation settings").ToString()));
            GuardSettings.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_guardsui_autodefend1}Automatically create a guard party to defend villages").ToString(), delegate { return CurrentGarrisonSettings != null && CurrentGarrisonSettings.GuardsAutoSpawnToDefend; }, delegate (bool x)
            {
                MobileGarrisonSettings.Instance.ToggleAutoGuardDefend(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_guardsui_autodefend2}This allows the current garrison to automatically create a guard party to defend raided villages.")));
            GuardSettings.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_guardsui_autoroam1}Automatically create a guard party to roam the region").ToString(), delegate { return CurrentGarrisonSettings != null && CurrentGarrisonSettings.GuardsAutoSpawn; }, delegate (bool x)
            {
                MobileGarrisonSettings.Instance.ToggleAutoGuards(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_guardsui_autoroam2}Allow this garrison to automatically create a guard party to fight bandits and enemy parties.")));
            List<string> list = new List<string>();
            list.Add(new TextObject("{=ui_guardsui_partycreationsize_small}Small").ToString());
            list.Add(new TextObject("{=ui_guardsui_partycreationsize_medium}Medium").ToString());
            list.Add(new TextObject("{=ui_guardsui_partycreationsize_large}Large").ToString());
            GuardSettings.Add(new ImprovedGarrisonsOptionVM().SetAsSliderOption(new TextObject("{=ui_guardsui_partycreationAutomatic guard creation party sizetysize").ToString(), delegate { return CurrentGarrisonSettings.GuardsAutoSpawnSize; }, 10f, (Main.GarrisonBehavior.CurrentTownForSettings == null) ? 450 : ((Main.GarrisonBehavior.CurrentTownForSettings.GarrisonParty != null) ? Main.GarrisonBehavior.CurrentTownForSettings.GarrisonParty.Party.PartySizeLimit : 450), discrete: true, delegate (float x)
            {
                MobileGarrisonSettings.Instance.SetAutoGarrisonSize(Main.GarrisonBehavior.CurrentTownForSettings, (int)x);
            }, new TextObject("{=ui_guardsui_partycreationsize2}Set the size of your guard party when automatically created by your current garrison.")));
            GuardSettings.Add(new ImprovedGarrisonsOptionVM().SetAsSliderOption(new TextObject("{=ui_guardsui_autocreationthreshold1}Automatic guard creation threshold").ToString(), delegate { return CurrentGarrisonSettings.GuardsAutoSpawnThreshold; }, 1f, (Main.GarrisonBehavior.CurrentTownForSettings == null) ? 450 : ((Main.GarrisonBehavior.CurrentTownForSettings.GarrisonParty != null) ? Main.GarrisonBehavior.CurrentTownForSettings.GarrisonParty.Party.PartySizeLimit : 450), discrete: true, delegate (float x)
            {
                MobileGarrisonSettings.Instance.SetAutoGarrisonThreshold(Main.GarrisonBehavior.CurrentTownForSettings, (int)x);
            }, new TextObject("{=ui_guardsui_autocreationthreshold2}Set the garrison size that has to be reached for a guard to be automatically created. \n \nNote: the automatic guard party creation must be enabled.")));
            GuardSettings.Add(new ImprovedGarrisonsOptionVM().SetAsTitle(new TextObject("{=ui_guardsui_settings}Guard behavior settings").ToString()));
            GuardSettings.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_guardsui_allowupgrade1}Allow to upgrade troops").ToString(), delegate { return CurrentGarrisonSettings.GuardEnableUpgradeTroops; }, delegate (bool x)
            {
                MobileGarrisonSettings.Instance.ToggleUpgrade(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_guardsui_allowupgrade2}Allow this guard party to upgrade its troops.")));
            GuardSettings.Add(new ImprovedGarrisonsOptionVM().SetAsSliderOption(new TextObject("{=ui_guardsui_returnthreshold1}Return threshold (%)").ToString(), delegate { return CurrentGarrisonSettings.GuardReturnPercentage * 100f; }, 1f, 90f, discrete: true, delegate (float x)
            {
                MobileGarrisonSettings.Instance.SetReturnPercentage(Main.GarrisonBehavior.CurrentTownForSettings, x / 100f);
            }, new TextObject("{=ui_guardsui_returnthreshold2}Set the threshold in relation to the initial party size for the guard party to return. \n \n Set this to 0.9 and the guard party will return after losing 10% of its troops \n \n Set this to 0.1 and 90% of the party's initial size has to be lost before returning.")));
            GuardSettings.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_guardsui_allowreplenish1}Allow to replenish").ToString(), delegate { return CurrentGarrisonSettings.EnableReplenish; }, delegate (bool x)
            {
                MobileGarrisonSettings.Instance.ToggleReplenish(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_guardsui_allowreplenish2}Allow this guard party to go back to their town/castle to replenish their lost troops and to heal if they are wounded. \n \nGuard parties will only replenish troop types that were in their initial setup! Therefore, make sure to have these units recruited to the garrison if you want the guard party to pick them up when they replenish.")));
            GuardSettings.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_guardsui_allowsell1}Allow to ransom prisoners").ToString(), delegate { return CurrentGarrisonSettings.EnablePrisonerSell; }, delegate (bool x)
            {
                MobileGarrisonSettings.Instance.TogglePrisonerSell(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_guardsui_allowsell2}Allow this guard party to ransom captured prisoners.")));
            GuardSettings.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_guardsui_allowrecruit1}Allow to recruit prisoners").ToString(), delegate { return CurrentGarrisonSettings.GuardEnablePrisonerRecruitment; }, delegate (bool x)
            {
                MobileGarrisonSettings.Instance.TogglePrisonerRecruit(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_guardsui_allowrecruit2}Allow this guard party to recruit captured prisoners.")));
            GuardSettings.Add(new ImprovedGarrisonsOptionVM().SetAsBooleanOption(new TextObject("{=ui_guardsui_allowhorses1}Allow to buy horses").ToString(), delegate { return CurrentGarrisonSettings.EnableHorseBuy; }, delegate (bool x)
            {
                MobileGarrisonSettings.Instance.ToggleHorseBuy(Main.GarrisonBehavior.CurrentTownForSettings, x);
            }, new TextObject("{=ui_guardsui_allowhorses2}Allow this guard party to buy horses to increase its movement speed.")));
        }

        public void OnCreateGuardButtonPress()
        {
            MobileGarrisonSettings.Instance.PromptCreateMobileGarrison(Main.GarrisonBehavior.CurrentTownForSettings);
        }

        public void ExecuteLink(string link)
        {
            Campaign.Current.EncyclopediaManager.GoToLink(link);
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            HasNoActiveGuard = CurrentMobileGarrison == null;
            GuardStatus = ((CurrentMobileGarrison != null) ? CurrentMobileGarrison.GetStatusText() : new TextObject("{=ui_guardsui_noguards}There is no active guard party").ToString());
            foreach (ImprovedGarrisonsInformationListVM item in GuardInformation)
            {
                item.RefreshValues();
            }
            foreach (ImprovedGarrisonsOptionVM item2 in GuardSettings)
            {
                item2.RefreshValues();
            }
            ToggleAutoGuardCreation.RefreshValues();
        }
    }
}
