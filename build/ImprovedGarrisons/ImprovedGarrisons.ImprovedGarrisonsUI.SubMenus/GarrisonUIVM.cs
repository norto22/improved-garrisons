using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImprovedGarrisons.ActivityLogging;
using ImprovedGarrisons.ActivityLogging.Activities;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus.GarrisonUtils;
using ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus.OverviewUtils;
using ImprovedGarrisons.ImprovedGarrisonsUI.UIElements;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus
{
	public class GarrisonUIVM : ViewModel
	{
		private int versionNumber = -1;

		private string _dailyWageOfGarrison;

		private GarrisonSettings CurrentGarrisonSettings => Main.GarrisonBehavior.GetCurrentTownSettings();

		public MBBindingList<ImprovedGarrisonsTroopItemWidgetVM> GarrisonTroops { get; set; }

		private MobileParty CurrentGarrisonParty
		{
			get
			{
				Town currentTownForSettings = Main.GarrisonBehavior.CurrentTownForSettings;
				if (currentTownForSettings != null && currentTownForSettings.GarrisonParty != null)
				{
					return currentTownForSettings.GarrisonParty;
				}
				return null;
			}
		}

		public string DailyWageOfGarrison
		{
			get
			{
				return _dailyWageOfGarrison;
			}
			set
			{
				if (value != _dailyWageOfGarrison)
				{
					_dailyWageOfGarrison = value;
					OnPropertyChangedWithValue(value, "DailyWageOfGarrison");
				}
			}
		}

		public MBBindingList<SettlementInformationVM> GarrisonInformation { get; set; }

		public MBBindingList<LogEntryVM> LogEntries { get; set; } = new MBBindingList<LogEntryVM>();

		public string CurrentGarrisonTitle { get; } = new TextObject("{=ui_garrisonui_currentgarrison}Current garrison").ToString();

		public string TransferTroopsText { get; } = new TextObject("{=ui_garrisonui_transfer}Transfer troops").ToString();

		public string GarrisonActivityLogText { get; } = new TextObject("{=ui_garrisonui_garrisonactivity}Garrison activity").ToString();

		public string WeeklyInformationText { get; } = new TextObject("{=ui_garrisonui_weeklyinformation}Weekly garrison information").ToString();

		public GarrisonUIVM()
		{
			InitializeAll();
			RefreshValues();
		}

		public void InitializeAll()
		{
			InitializeGarrisonTroops();
			InitializeWeeklyCosts();
			InitializeLogEntries();
		}

		private void InitializeLogEntries()
		{
			bool flag = LogEntries == null || LogEntries.Count <= 0;
			Town currentTownForSettings = Main.GarrisonBehavior.CurrentTownForSettings;
			if (currentTownForSettings == null)
			{
				return;
			}
			ActivityLog activityLog = Main.ActivityLogManager.GetActivityLog(currentTownForSettings);
			if (activityLog == null || !(activityLog.ActivitiesHaveBeenUpdated || flag))
			{
				return;
			}
			LogEntries = new MBBindingList<LogEntryVM>();
			activityLog.ActivitiesHaveBeenUpdated = false;
			foreach (GarrisonActivity activity in activityLog.GetActivities())
			{
				if (!(activity.CampaignDayOfTheActivity == ""))
				{
					LogEntries.Add(new LogEntryVM(activity.CampaignDayOfTheActivity, activity.GetLogDescription()));
				}
			}
			OnPropertyChanged("LogEntries");
		}

		private void InitializeWeeklyCosts()
		{
			try
			{
				GarrisonInformation = new MBBindingList<SettlementInformationVM>();
				Town currentTown = Main.GarrisonBehavior.CurrentTownForSettings;
				if (currentTown == null)
				{
					return;
				}
				GarrisonInformation.Add(new SettlementInformationVM(currentTown.Settlement).SetAsCustomInformation("General\\Icons\\Party@2x", new HintViewModel(new TextObject("{=ui_garrisonui_weeklycosts1}This week's number of recruited troops.")), new TextObject("{=ui_garrisonui_weeklycosts2}Recruited troops").ToString(), delegate
				{
					ActivityLog activityLog = Main.ActivityLogManager.GetActivityLog(currentTown);
					return (activityLog != null) ? activityLog.WeeklyRecruits.ToString() : "0";
				}));
				GarrisonInformation.Add(new SettlementInformationVM(currentTown.Settlement).SetAsCustomInformation("General\\Icons\\Coin@2x", new HintViewModel(new TextObject("{=ui_garrisonui_weeklycosts3}This week's recruitment costs")), new TextObject("{=ui_garrisonui_weeklycosts4}Recruitment costs").ToString(), delegate
				{
					float num = 0f;
					ActivityLog activityLog = Main.ActivityLogManager.GetActivityLog(currentTown);
					if (activityLog != null)
					{
						foreach (KeyValuePair<string, float> recruiterCost in activityLog.RecruiterCosts)
						{
							num += recruiterCost.Value;
						}
						num += activityLog.WeeklyRecruitmentCosts;
					}
					return (activityLog != null) ? num.ToString() : "0";
				}));
				GarrisonInformation.Add(new SettlementInformationVM(currentTown.Settlement).SetAsCustomInformation("General\\Icons\\Party@2x", new HintViewModel(new TextObject("{=ui_garrisonui_weeklycosts5}This week's number of upgraded troops.")), new TextObject("{=ui_garrisonui_weeklycosts6}Upgraded troops").ToString(), delegate
				{
					ActivityLog activityLog = Main.ActivityLogManager.GetActivityLog(currentTown);
					return (activityLog != null) ? activityLog.WeeklyUpgrades.ToString() : "0";
				}));
				GarrisonInformation.Add(new SettlementInformationVM(currentTown.Settlement).SetAsCustomInformation("General\\Icons\\Coin@2x", new HintViewModel(new TextObject("{=ui_garrisonui_weeklycosts7}This week's training costs")), new TextObject("{=ui_garrisonui_weeklycosts8}Training costs").ToString(), delegate
				{
					ActivityLog activityLog = Main.ActivityLogManager.GetActivityLog(currentTown);
					return (activityLog != null) ? activityLog.WeeklyTrainingCosts.ToString() : "0";
				}));
				OnPropertyChanged("GarrisonInformation");
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void InitializeGarrisonTroops()
		{
			if (CurrentGarrisonParty != null && CurrentGarrisonParty.VersionNo == versionNumber)
			{
				return;
			}
			if (CurrentGarrisonParty == null)
			{
				GarrisonTroops = new MBBindingList<ImprovedGarrisonsTroopItemWidgetVM>();
				OnPropertyChanged("GarrisonTroops");
				return;
			}
			GarrisonTroops = new MBBindingList<ImprovedGarrisonsTroopItemWidgetVM>();
			List<TroopRosterElement> list = CurrentGarrisonParty.MemberRoster.GetTroopRoster().ToList();
			foreach (TroopRosterElement item in list)
			{
				GarrisonTroops.Add(new ImprovedGarrisonsTroopItemWidgetVM(item));
			}
			OnPropertyChanged("GarrisonTroops");
			versionNumber = CurrentGarrisonParty.VersionNo;
		}

		public void ExecuteTransfer()
		{
			try
			{
				ManagementSettings.Instance.PromptTransfer(Main.GarrisonBehavior.CurrentTownForSettings);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void CalculateGarrisonWage()
		{
			try
			{
				int num = 0;
				MobileParty currentGarrisonParty = CurrentGarrisonParty;
				if (currentGarrisonParty != null)
				{
					num += currentGarrisonParty.TotalWage;
				}
				DailyWageOfGarrison = new TextObject("{=ui_trainingui_dailywagetext}Daily wage:").ToString() + " " + num;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public override void RefreshValues()
		{
			base.RefreshValues();
			InitializeGarrisonTroops();
			CalculateGarrisonWage();
			InitializeLogEntries();
			foreach (SettlementInformationVM item in GarrisonInformation)
			{
				item.RefreshValues();
			}
		}
	}
}
