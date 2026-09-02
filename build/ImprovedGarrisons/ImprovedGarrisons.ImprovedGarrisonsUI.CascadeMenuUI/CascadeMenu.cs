using ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements;
using SandBox.View.Map;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI
{
	public class CascadeMenu
	{
		public MBBindingList<CascadeMenuElementVM> cascadeMenuElements;

		private CascadeMenuVM _dataSource;

		private string cascadeGauntletID = "CascadeMenu";

		private GauntletLayer _gauntletLayer;

		public Widget cascadeMenuWidget { get; set; }

		public string TitleText { get; set; }

		public CascadeMenuVM Datasource => _dataSource;

		public GauntletLayer GauntletLayer
		{
			get
			{
				return _gauntletLayer;
			}
			set
			{
				_gauntletLayer = value;
			}
		}

		public CascadeMenu(string title, MBBindingList<CascadeMenuElementVM> elements, int suggestedWidth = -1)
		{
			TitleText = title;
			_dataSource = new CascadeMenuVM(title, elements, suggestedWidth);
			cascadeMenuElements = elements;
		}

		public void Initialize()
		{
			GauntletLayer = new GauntletLayer(cascadeGauntletID, 9000 + UIManager.Instance.cascadeMenuGauntlet.currentCascadeLevel + 1)
			{
				IsFocusLayer = true
			};
			GauntletLayer.InputRestrictions.SetInputRestrictions();
			GauntletLayer.LoadMovie("ImprovedGarrisonsCascadeMenu", Datasource);
		}

		public void OnFinalize()
		{
			if (GauntletLayer != null)
			{
				GauntletLayer.InputRestrictions.ResetInputRestrictions();
				MapScreen.Instance.RemoveLayer(GauntletLayer);
				ScreenManager.TryLoseFocus(GauntletLayer);
				_gauntletLayer = null;
			}
			_dataSource = null;
		}
	}
}
