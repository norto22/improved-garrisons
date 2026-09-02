using ImprovedGarrisons.SaveSystem.Configuration;
using ImprovedGarrisons.Tutorial;
using ImprovedGarrisons.Utils;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace ImprovedGarrisons.ConfigOptionsMenu
{
	internal class ConfigMenuGauntletScreen : ScreenBase
	{
		private TutorialUIVM tutorialVM;

		private GauntletLayer _gauntletLayer;

		private ConfigMenuVM _dataSource;

		private SpriteCategory _spriteCategory;

		public object TutorialUiVm { get; private set; }

		public ConfigMenuGauntletScreen(TutorialUIVM tutorial = null)
		{
			tutorialVM = tutorial;
		}

		protected override void OnInitialize()
		{
			base.OnInitialize();
			SpriteData spriteData = UIResourceManager.SpriteData;
			TwoDimensionEngineResourceContext resourceContext = UIResourceManager.ResourceContext;
			ResourceDepot resourceDepot = UIResourceManager.ResourceDepot;
			_spriteCategory = spriteData.SpriteCategories["ui_options"];
			_spriteCategory.Load(resourceContext, resourceDepot);
			_dataSource = new ConfigMenuVM();
			_gauntletLayer = new GauntletLayer("GauntletLayer", 4000);
			_gauntletLayer.LoadMovie("ImprovedGarrisonsConfigScreen", _dataSource);
			if (tutorialVM != null)
			{
				_gauntletLayer.LoadMovie("ImprovedGarrisonsTutorial", tutorialVM);
			}
			_gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
			_gauntletLayer.InputRestrictions.SetInputRestrictions();
			_gauntletLayer.IsFocusLayer = true;
			AddLayer(_gauntletLayer);
			ScreenManager.TrySetFocus(_gauntletLayer);
			Utilities.SetForceVsync(value: true);
		}

		public void CloseConfigurationMenu()
		{
			_dataSource.ExecuteCancel();
		}

		protected override void OnFinalize()
		{
			base.OnFinalize();
			_spriteCategory.Unload();
			Utilities.SetForceVsync(value: false);
		}

		protected override void OnDeactivate()
		{
			LoadingWindow.EnableGlobalLoadingWindow();
		}

		protected override void OnFrameTick(float dt)
		{
			base.OnFrameTick(dt);
			if ((Main._configurationSettingsIsOpen && _dataSource.IsFinished && !_dataSource.PromptIsOpen) || (_gauntletLayer.Input.IsHotKeyReleased("Exit") && !_dataSource.PromptIsOpen))
			{
				_dataSource.UnpauseGame();
				ScreenManager.PopScreen();
				Main._configurationSettingsIsOpen = false;
			}
			if (_dataSource.ResetMode)
			{
				ConfigManager.Instance.Config.ResetToDefault();
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_config_reset_done}The Improved Garrisons configuration has been reset to its default values.").ToString(), Color.FromUint(ModuleColors.yellow)));
				_dataSource.ResetMode = false;
			}
		}
	}
}
