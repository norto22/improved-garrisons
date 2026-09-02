using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Helpers;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus.ManagementUtils;
using ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus.OverviewUtils;
using ImprovedGarrisons.ImprovedGarrisonsUI.UIElements;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus
{
	public class ManagementUIVM : ViewModel
	{
		private bool _hasCurrentBuilding;

		private string _currentBuildingVisual;

		private HintViewModel _currentProjectHint;

		private string _currentBuildingProgress;

		private bool _hasQueue = false;

		private bool _hasNoQueue = true;

		private int _construction;

		private int _reserve;

		private BuildingVM _currentProjectVM;

		private Building lastKnownCurrentBuilding;

		private GarrisonSettings CurrentGarrisonSettings => Main.GarrisonBehavior.GetCurrentTownSettings();

		public bool HasCurrentBuilding
		{
			get
			{
				return _hasCurrentBuilding;
			}
			set
			{
				if (value != _hasCurrentBuilding)
				{
					_hasCurrentBuilding = value;
					OnPropertyChangedWithValue(value, "HasCurrentBuilding");
				}
			}
		}

		public string CurrentBuildingVisual
		{
			get
			{
				return _currentBuildingVisual;
			}
			set
			{
				if (value != _currentBuildingVisual)
				{
					_currentBuildingVisual = value;
					OnPropertyChangedWithValue(value, "CurrentBuildingVisual");
				}
			}
		}

		public HintViewModel CurrentProjectHint
		{
			get
			{
				return _currentProjectHint;
			}
			set
			{
				if (value != _currentProjectHint)
				{
					_currentProjectHint = value;
					OnPropertyChangedWithValue(value, "CurrentProjectHint");
				}
			}
		}

		public string CurrentBuildingProgress
		{
			get
			{
				return _currentBuildingProgress;
			}
			set
			{
				if (value != _currentBuildingProgress)
				{
					_currentBuildingProgress = value;
					OnPropertyChangedWithValue(value, "CurrentBuildingProgress");
				}
			}
		}

		public bool HasQueue
		{
			get
			{
				return _hasQueue;
			}
			set
			{
				if (value != _hasQueue)
				{
					_hasQueue = value;
					HasNoQueue = !value;
					OnPropertyChangedWithValue(value, "HasQueue");
				}
			}
		}

		public bool HasNoQueue
		{
			get
			{
				return _hasNoQueue;
			}
			set
			{
				if (value != _hasNoQueue)
				{
					_hasNoQueue = value;
					OnPropertyChangedWithValue(value, "HasNoQueue");
				}
			}
		}

		public int Construction
		{
			get
			{
				return _construction;
			}
			set
			{
				if (value != _construction)
				{
					_construction = value;
					OnPropertyChangedWithValue(value, "Construction");
				}
			}
		}

		public int Reserve
		{
			get
			{
				return _reserve;
			}
			set
			{
				if (value != _reserve)
				{
					_reserve = value;
					OnPropertyChangedWithValue(value, "Reserve");
				}
			}
		}

		public BuildingVM CurrentProjectVM
		{
			get
			{
				return _currentProjectVM;
			}
			set
			{
				if (value != _currentProjectVM)
				{
					_currentProjectVM = value;
					OnPropertyChangedWithValue(value, "CurrentProjectVM");
				}
			}
		}

		public string ProgressText { get; set; } = new TextObject("{=ui_managementui_progresstext}Progress").ToString();

		public string QueueText { get; set; } = new TextObject("{=ui_managementui_queuetext}Queue").ToString();

		public string NoProjectsText { get; set; } = new TextObject("{=ui_managementui_noqueuetext}No project in queue").ToString();

		public string DailyDefaultText { get; set; } = new TextObject("{=ui_managementui_dailydefaulttext}Default project").ToString();

		public MBBindingList<ImprovedGarrisonsOptionVM> ManagementSettingsVM { get; set; }

		public MBBindingList<BuildingVM> Buildings { get; set; } = new MBBindingList<BuildingVM>();

		public MBBindingList<BuildingVM> DailyDefaults { get; set; } = new MBBindingList<BuildingVM>();

		public MBBindingList<BuildingVM> CurrentQueue { get; set; } = new MBBindingList<BuildingVM>();

		public string CopyManagementTitle { get; } = new TextObject("{=ui_managementui_copymanager}Copy manager").ToString();

		public string BuildingsTitle { get; } = new TextObject("{=ui_managementui_townprojectstext}Town projects").ToString();

		public string GarrisonInformationTitle { get; } = new TextObject("{=ui_managementui_information}Location information").ToString();

		public MBBindingList<SettlementInformationVM> SettlementInformation { get; set; }

		public MBBindingList<SettlementInformationVM> SettlementBuildingInformation { get; set; }

		public ManagementUIVM()
		{
			InitializeAll();
			RefreshValues();
		}

		public void InitializeAll()
		{
			InitializeManagementSettings();
			InitializeSettlementInformation();
			InitializeBuildings();
			InitializeTownBuildingStats();
			InitializeDailyDefaults();
		}

		private void InitializeTownBuildingStats()
		{
			SettlementBuildingInformation = new MBBindingList<SettlementInformationVM>();
			if (Main.GarrisonBehavior.CurrentTownForSettings != null)
			{
				SettlementBuildingInformation.Add(new SettlementInformationVM(Main.GarrisonBehavior.CurrentTownForSettings.Settlement).SetAsConstructionInformation());
				SettlementBuildingInformation.Add(new SettlementInformationVM(Main.GarrisonBehavior.CurrentTownForSettings.Settlement).SetAsReserveInformation());
			}
		}

		private void InitializeSettlementInformation()
		{
			SettlementInformation = new MBBindingList<SettlementInformationVM>();
			if (Main.GarrisonBehavior.CurrentTownForSettings != null)
			{
				SettlementInformation.Add(new SettlementInformationVM(Main.GarrisonBehavior.CurrentTownForSettings.Settlement).SetAsConstructionInformation());
				SettlementInformation.Add(new SettlementInformationVM(Main.GarrisonBehavior.CurrentTownForSettings.Settlement).SetAsReserveInformation());
				SettlementInformation.Add(new SettlementInformationVM(Main.GarrisonBehavior.CurrentTownForSettings.Settlement).SetAsGarrisonInformation());
				SettlementInformation.Add(new SettlementInformationVM(Main.GarrisonBehavior.CurrentTownForSettings.Settlement).SetAsMilitiaInformation());
				SettlementInformation.Add(new SettlementInformationVM(Main.GarrisonBehavior.CurrentTownForSettings.Settlement).SetAsFoodChangeInformation());
				SettlementInformation.Add(new SettlementInformationVM(Main.GarrisonBehavior.CurrentTownForSettings.Settlement).SetAsGoldChangeInformation());
				SettlementInformation.Add(new SettlementInformationVM(Main.GarrisonBehavior.CurrentTownForSettings.Settlement).SetAsProsperityInformation());
				SettlementInformation.Add(new SettlementInformationVM(Main.GarrisonBehavior.CurrentTownForSettings.Settlement).SetAsLoyalityInformation());
			}
		}

		private void InitializeManagementSettings()
		{
			ManagementSettingsVM = new MBBindingList<ImprovedGarrisonsOptionVM>();
			if (CurrentGarrisonSettings != null)
			{
				ManagementSettingsVM.Add(new ImprovedGarrisonsOptionVM().SetAsButtonOption(new TextObject("{=ui_managementui_copycastles1}Copy to all castles").ToString(), delegate
				{
					ManagementSettings.Instance.PromptCopyToAllCastles(Main.GarrisonBehavior.CurrentTownForSettings);
				}, new TextObject("{=ui_managementui_copycastles2}Copy the current garrison settings to all of your castles.")));
				ManagementSettingsVM.Add(new ImprovedGarrisonsOptionVM().SetAsButtonOption(new TextObject("{=ui_managementui_copytown1}Copy to all towns").ToString(), delegate
				{
					ManagementSettings.Instance.PromptCopyToAllTowns(Main.GarrisonBehavior.CurrentTownForSettings);
				}, new TextObject("{=ui_managementui_copytown2}Copy the current garrison settings to all of your towns.")));
				ManagementSettingsVM.Add(new ImprovedGarrisonsOptionVM().SetAsButtonOption(new TextObject("{=ui_managementui_copyspecific1}Specific copy").ToString(), delegate
				{
					ManagementSettings.Instance.PromptCopyToSpecificTowns(Main.GarrisonBehavior.CurrentTownForSettings);
				}, new TextObject("{=ui_managementui_copyspecific2}Copy the current garrison settings to a specific town or castle that you own.")));
			}
		}

		private void InitializeDailyDefaults()
		{
			DailyDefaults = new MBBindingList<BuildingVM>();
			if (Main.GarrisonBehavior.CurrentTownForSettings == null)
			{
				return;
			}
			Town currentTownForSettings = Main.GarrisonBehavior.CurrentTownForSettings;
			List<Building> buildings = currentTownForSettings.Buildings;
			foreach (Building item in buildings)
			{
				if (item.BuildingType.IsDailyProject)
				{
					DailyDefaults.Add(new BuildingVM(item));
				}
			}
		}

		private void InitializeBuildings()
		{
			if (Main.GarrisonBehavior.CurrentTownForSettings == null)
			{
				return;
			}
			Town currentTownForSettings = Main.GarrisonBehavior.CurrentTownForSettings;
			List<Building> buildings = currentTownForSettings.Buildings;
			Building currentBuilding = currentTownForSettings.CurrentBuilding;
			List<Building> list = currentTownForSettings.BuildingsInProgress.ToList();
			CurrentQueue = new MBBindingList<BuildingVM>();
			if (list != null && list.Count > 1)
			{
				list.RemoveAt(0);
				foreach (Building item in list)
				{
					if (CurrentQueue.Count > 4)
					{
						break;
					}
					CurrentQueue.Add(new BuildingVM(item));
				}
				HasQueue = true;
				OnPropertyChanged("CurrentQueue");
			}
			else if (HasQueue)
			{
				OnPropertyChanged("CurrentQueue");
				HasQueue = false;
			}
			if (lastKnownCurrentBuilding == null || !currentBuilding.Name.ToString().Equals(lastKnownCurrentBuilding?.Name.ToString()))
			{
				list = currentTownForSettings.BuildingsInProgress.ToList();
				if (list.Count > 0)
				{
					CurrentProjectVM = new BuildingVM(currentBuilding);
					CurrentBuildingVisual = CurrentProjectVM.VisualCode;
					CurrentBuildingProgress = (int)(BuildingHelper.GetProgressOfBuilding(currentBuilding, currentTownForSettings) * 100f) + " %";
					HasCurrentBuilding = true;
					CurrentProjectHint = CurrentProjectVM.Hint;
				}
				else
				{
					CurrentProjectVM = null;
					CurrentBuildingProgress = "";
					HasCurrentBuilding = false;
					CurrentBuildingVisual = "";
					CurrentProjectHint = null;
				}
				Buildings = new MBBindingList<BuildingVM>();
				foreach (Building item2 in buildings)
				{
					bool isDailyProject = item2.BuildingType.IsDailyProject;
					bool flag = currentBuilding.Name.ToString().Equals(item2.Name.ToString());
					if (!isDailyProject && !flag)
					{
						Buildings.Add(new BuildingVM(item2));
					}
				}
				lastKnownCurrentBuilding = currentBuilding;
				OnPropertyChanged("Buildings");
				OnPropertyChanged("CurrentQueue");
			}
			else if (lastKnownCurrentBuilding != null && currentBuilding.Name.ToString().Equals(lastKnownCurrentBuilding?.Name.ToString()))
			{
				list = currentTownForSettings.BuildingsInProgress.ToList();
				if (list.Count > 0)
				{
					CurrentBuildingProgress = (int)(BuildingHelper.GetProgressOfBuilding(currentBuilding, currentTownForSettings) * 100f) + " %";
				}
				else
				{
					CurrentBuildingProgress = "";
				}
			}
		}

		public void ExecuteCopyToAllTowns()
		{
			try
			{
				ManagementSettings.Instance.PromptCopyToAllTowns(Main.GarrisonBehavior.CurrentTownForSettings);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ExecuteCopyToAllCastles()
		{
			try
			{
				ManagementSettings.Instance.PromptCopyToAllCastles(Main.GarrisonBehavior.CurrentTownForSettings);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ExecuteCopyToSpecific()
		{
			try
			{
				ManagementSettings.Instance.PromptCopyToSpecificTowns(Main.GarrisonBehavior.CurrentTownForSettings);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public override void RefreshValues()
		{
			base.RefreshValues();
			BuildingVM.EnsureSpritesLoaded();
			foreach (SettlementInformationVM item in SettlementInformation)
			{
				item.RefreshValues();
			}
			foreach (SettlementInformationVM item2 in SettlementBuildingInformation)
			{
				item2.RefreshValues();
			}
			foreach (BuildingVM dailyDefault in DailyDefaults)
			{
				dailyDefault.RefreshValues();
			}
			InitializeBuildings();
		}
	}
}
