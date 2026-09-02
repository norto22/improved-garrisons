using ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

namespace ImprovedGarrisons.ImprovedGarrisonsUI
{
    public class ImprovedGarrisonsUIGauntlet : MapView
    {
        public bool WantToChangeTab = false;

        private string _wantToChangeTabToID;

        private GauntletLayer _layer;

        private ImprovedGarrisonsUIVM _datasource;

        private OverviewUIVM _compactDatsource;

        private GauntletLayer _compactLayer;

        public string ActualCurrentTabId { get; set; }

        public string WantToChangeTabToID
        {
            get
            {
                return _wantToChangeTabToID;
            }
            set
            {
                _wantToChangeTabToID = value;
                WantToChangeTab = true;
            }
        }

        public ImprovedGarrisonsUIGauntlet()
        {
            CreateLayout();
        }

        protected override void CreateLayout()
        {
            base.CreateLayout();
            _layer = new GauntletLayer("GauntletLayer", 550);
            _datasource = new ImprovedGarrisonsUIVM();
            _layer.LoadMovie("ImprovedGarrisonsMenu", _datasource);
            _layer.InputRestrictions.SetInputRestrictions(isMouseVisible: false);
            MapScreen.Instance.AddLayer(_layer);
            ScreenManager.TrySetFocus(_layer);
        }

        public void ChangeSelectorSelectionToCurrentSettlement()
        {
            if (_datasource != null)
            {
                _datasource.ChangeSelectorSelectionToCurrentSettlement();
            }
        }

        public void ChangeSelectorSelection(Settlement settlement)
        {
            if (_datasource != null)
            {
                _datasource.ChangeSelectorSelection(settlement);
            }
        }

        public void SwitchToCompactUI()
        {
            CloseUi();
            _compactLayer = new GauntletLayer("GauntletLayer", 560);
            _compactDatsource = new OverviewUIVM();
            _compactLayer.LoadMovie("ImprovedGarrisonsCompactOverview", _compactDatsource);
            _compactLayer.InputRestrictions.SetInputRestrictions();
            MapScreen.Instance.AddLayer(_compactLayer);
            ScreenManager.TrySetFocus(_compactLayer);
        }

        public void SwitchToTab(string tabId)
        {
            WantToChangeTabToID = tabId;
        }

        public void SwitchBackToNormalUI()
        {
            CloseCompactUI();
            CreateLayout();
        }

        public void CloseCompactUI()
        {
            if (_compactLayer != null)
            {
                MapScreen.Instance.RemoveLayer(_compactLayer);
                _compactLayer = null;
            }
            _compactDatsource = null;
        }

        public void CloseUi()
        {
            if (_layer != null)
            {
                MapScreen.Instance.RemoveLayer(_layer);
                _layer = null;
            }
            _datasource = null;
            CloseCompactUI();
        }

        public void UpdateUiContents()
        {
            if (_compactDatsource != null)
            {
                _compactDatsource.RefreshValues();
            }
            else
            {
                _datasource?.UpdateUiContents();
            }
        }

        public void ForceFullRefresh()
        {
            if (_compactDatsource != null)
            {
                _compactDatsource.RefreshValues();
            }
            else
            {
                _datasource?.ForceFullRefresh();
            }
        }

        public void ForceOverviewUpdate()
        {
            if (_compactDatsource != null)
            {
                _compactDatsource.RefreshValues();
            }
            else
            {
                _datasource?.ForceOverviewUpdate();
            }
        }

        public void UpdateCurrentUiTab()
        {
            if (_compactDatsource != null)
            {
                _compactDatsource.RefreshValues();
            }
            else
            {
                _datasource?.RefreshValues();
            }
        }

        public void UpdateSettlementSelector()
        {
            _datasource?.UpdateSettlementSelector();
        }

        public void MarkTrainingTroopsDirty()
        {
            if (_datasource != null)
            {
                _datasource.TrainingDatasource.TroopListIsDirty = true;
            }
        }
    }
}
