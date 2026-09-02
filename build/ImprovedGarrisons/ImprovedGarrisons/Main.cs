using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImprovedGarrisons.AI.AIManagers;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.ActivityLogging;
using ImprovedGarrisons.Behaviours;
using ImprovedGarrisons.ConfigOptionsMenu;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.ImprovedGarrisonsUI;
using ImprovedGarrisons.Models;
using ImprovedGarrisons.Recruitment;
using ImprovedGarrisons.SaveSystem;
using ImprovedGarrisons.SaveSystem.Configuration;
using ImprovedGarrisons.SaveSystem.SaveData;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using ImprovedGarrisons.Tutorial;
using ImprovedGarrisons.Upgrade;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace ImprovedGarrisons
{
	public class Main : MBSubModuleBase
	{
		private static readonly List<Action> ActionsToExecuteNextTick = new List<Action>();

		public static bool _configurationSettingsIsOpen = false;

		private static ConfigMenuGauntletScreen _configMenu;

		private readonly string modGameVersion = "v1.4.8.";

		private static Dictionary<string, Action> ActionsToExecuteEveryTick = new Dictionary<string, Action>();

		public static GarrisonPartyBehavior GarrisonPartyBehavior { get; set; }

		public static GarrisonBehavior GarrisonBehavior { get; set; }

		public static SaveBehavior SaveBehavior { get; set; }

		public static ActivityLogManager ActivityLogManager { get; set; }

		public static GarrisonRecruitmentLogic RecruitmentLogic { get; set; }

		public static GarrisonUpgradeLogic UpgradeLogic { get; set; }

		public static GarrisonCostModel GarrisonCostModel { get; set; }

		public static GarrisonFoodModel FoodModel { get; set; }

		public static PartyManager PartyManagement { get; set; }

		public static bool IsDedicatedServer => GameNetwork.IsDedicatedServer;

		public static bool IsMapState => Game.Current?.GameStateManager?.ActiveState?.GetType() == typeof(MapState) && !Game.Current.GameStateManager.ActiveState.IsMenuState;

		protected override void OnBeforeInitialModuleScreenSetAsRoot()
		{
			if (IsDedicatedServer)
			{
				return;
			}
			InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=misc_ig_onmapload}Loaded Improved Garrisons.").ToString(), Color.FromUint(ModuleColors.green)));
			ThrowWarningIfGameErrorDoesntMatchModVersion();
		}

		public override void OnGameLoaded(Game game, object initializerObject)
		{
			base.OnGameLoaded(game, initializerObject);
		}

		public override void OnGameInitializationFinished(Game game)
		{
		}

		protected override void OnSubModuleLoad()
		{
			base.OnSubModuleLoad();
		}

		protected override void OnApplicationTick(float dt)
		{
			try
			{
				base.OnApplicationTick(dt);
				foreach (Action item in ActionsToExecuteNextTick)
				{
					item();
				}
				ActionsToExecuteNextTick.Clear();
				foreach (Action item2 in ActionsToExecuteEveryTick.Values.ToList())
				{
					item2();
				}
				if (!IsDedicatedServer)
				{
					OnKeyPress();
				}
				if (GarrisonPartyBehavior != null && PartyManagement.mobileGarrisonManagement.MobileGarrisons != null)
				{
					List<MobileGarrison> list = new List<MobileGarrison>();
					foreach (MobileGarrison value in PartyManagement.mobileGarrisonManagement.MobileGarrisons.Values)
					{
						if (Game.Current != null && Game.Current.GameStateManager.ActiveState != null && !Game.Current.GameStateManager.ActiveState.IsMenuState && value.getMobileParty().MemberRoster.TotalManCount <= 0)
						{
							list.Add(value);
						}
					}
					foreach (MobileGarrison item3 in list)
					{
						GarrisonPartyBehavior.RemovePartyHelper(item3.getMobileParty());
					}
				}
				if (IsDedicatedServer)
				{
					return;
				}
				if (GlobalSettings.Instance.EnableImprovedGarrisonsUIOnMap)
				{
					UIManager.Instance.TryInitializeImprovedGarrisonsUI();
				}
				else
				{
					bool flag = UIManager.Instance.CurrentUiState == UIManager.UiState.Retracted;
					bool flag2 = IsMapState && Campaign.Current?.MainParty?.CurrentSettlement != null;
					if (!GlobalSettings.Instance.EnableImprovedGarrisonsUIOnMap && UIManager.Instance.improvedGarrisonsUI != null && flag && !flag2 && IsMapState)
					{
						UIManager.Instance.CloseImprovedGarrisonsUI();
					}
				}
				if (UIManager.Instance.cascadeMenuGauntlet != null && UIManager.Instance.cascadeMenuGauntlet.cascadeMenuIsOpen)
				{
					UIManager.Instance.cascadeMenuGauntlet.Tick();
				}
				UIManager.Instance.TryUpdateImprovedGarrisonsUI();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public override void OnCampaignStart(Game game, object starterObject)
		{
			base.OnCampaignStart(game, starterObject);
			if (game.GameType is Campaign)
			{
				try
				{
					InitializeGame(game, (IGameStarter)starterObject);
				}
				catch (Exception ex)
				{
					LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				}
			}
		}

		protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
		{
			try
			{
				base.OnGameStart(game, gameStarterObject);
				if (game.GameType is Campaign campaign && campaign.CampaignGameLoadingType != Campaign.GameLoadingType.NewCampaign)
				{
					InitializeGame(game, gameStarterObject);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public static void LoadImprovedGarrisonsIntoGame(Game game, IGameStarter gameStarter)
		{
		}

		public void InitializeGame(Game game, IGameStarter gameStarter)
		{
			try
			{
				if (!IsDedicatedServer)
				{
					UIManager.Instance.ResetInstance();
				}
				IGSaveData.Instance = null;
				GarrisonPartyBehavior = new GarrisonPartyBehavior();
				GarrisonBehavior = new GarrisonBehavior();
				SaveBehavior = new SaveBehavior();
				SaveSystemManager.Instance = new SaveSystemManager();
				ActivityLogManager = new ActivityLogManager();
				GarrisonCostModel = new GarrisonCostModel();
				RecruitmentLogic = new GarrisonRecruitmentLogic();
				UpgradeLogic = new GarrisonUpgradeLogic();
				FoodModel = new GarrisonFoodModel();
				PartyManagement = new PartyManager();
				ConfigManager.Instance.ReadConfigForCurrentGame();
				AddBehaviours(gameStarter as CampaignGameStarter);
				AddModels(gameStarter as CampaignGameStarter);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void AddBehaviours(CampaignGameStarter starter)
		{
			starter.AddBehavior(new GarrisonDailyBehavior());
			starter.AddBehavior(GarrisonBehavior);
			starter.AddBehavior(GarrisonPartyBehavior);
			if (!IsDedicatedServer)
			{
				starter.AddBehavior(new UiBehavior());
			}
			starter.AddBehavior(SaveBehavior);
			starter.AddBehavior(ActivityLogManager);
		}

		private void AddModels(CampaignGameStarter gameStarter)
		{
			if (gameStarter != null)
			{
				gameStarter.AddModel(GarrisonCostModel);
				if (ConfigManager.Instance.Config.LoadCustomPartySizeModel)
				{
					gameStarter.AddModel(new GarrisonpartySizeLimitModel());
				}
				if (ConfigManager.Instance.Config.LoadCustomPartySpeedModel)
				{
					gameStarter.AddModel(new GarrisonSpeedModel());
				}
				if (ConfigManager.Instance.Config.LoadFoodGatheringModule)
				{
					gameStarter.AddModel(FoodModel);
				}
			}
		}

		public static void ExecuteActionOnNextTick(Action action)
		{
			if (action != null)
			{
				ActionsToExecuteNextTick.Add(action);
			}
		}

		public static void AddActionToExecuteEachTick(string id, Action action)
		{
			if (!ActionsToExecuteEveryTick.ContainsKey(id))
			{
				ActionsToExecuteEveryTick.Add(id, action);
			}
		}

		public static void RemoveActionToExecuteEachTick(string id)
		{
			if (ActionsToExecuteEveryTick.ContainsKey(id))
			{
				ActionsToExecuteEveryTick.Remove(id);
			}
		}

		private void OnKeyPress()
		{
			try
			{
				InputKey key = InputKey.G;
				bool flag = Input.IsKeyDown(key) && Input.IsKeyDown(InputKey.LeftAlt);
				bool flag2 = Game.Current != null && Game.Current.GameStateManager != null && Game.Current.GameStateManager.ActiveState != null && Game.Current.GameStateManager.ActiveState.GetType() == typeof(MapState) && !Game.Current.GameStateManager.ActiveState.IsMenuState;
				if (flag && flag2)
				{
					OpenConfigurationScreen();
				}
				bool flag3 = Input.IsKeyDown(InputKey.D) && Input.IsKeyDown(InputKey.LeftAlt) && Input.IsKeyDown(InputKey.LeftControl);
				if (flag3 && flag2)
				{
					UIManager.Instance.CloseImprovedGarrisonsUI();
					UIManager.Instance.TryInitializeImprovedGarrisonsUI();
					GlobalSettings.Instance.EnableImprovedGarrisonsUIOnMap = true;
				}
				bool flag4 = Input.IsKeyDown(InputKey.F) && Input.IsKeyDown(InputKey.LeftAlt) && Input.IsKeyDown(InputKey.LeftControl);
				if (flag4 && flag2)
				{
					MobileParty targetParty = MobileParty.MainParty.TargetParty;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public static void OpenConfigurationScreen(TutorialUIVM tutorialVM = null)
		{
			if (!_configurationSettingsIsOpen)
			{
				ConfigMenuGauntletScreen configMenu = new ConfigMenuGauntletScreen(tutorialVM);
				_configMenu = configMenu;
				ScreenManager.PushScreen(_configMenu);
				_configurationSettingsIsOpen = true;
			}
		}

		public static void CloseConfigurationScreen()
		{
			_configMenu.CloseConfigurationMenu();
		}

		public void ThrowWarningIfGameErrorDoesntMatchModVersion()
		{
			try
			{
				string text = ApplicationVersion.FromParametersFile().ToString().Substring(0, 7);
				if (!text.Equals(modGameVersion))
				{
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("The game version you are running is " + text + ". The Improved Garrison version you are using is made for " + modGameVersion + ". There might be compatibility issues! Make sure to update to the latest mod version.").ToString(), Color.FromUint(ModuleColors.red)));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}
	}
}
