using System;
using System.Collections.Generic;
using System.Reflection;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.ActivityLogging.Activities;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.SaveSystem.SaveData;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace ImprovedGarrisons.ActivityLogging
{
	public class ActivityLogManager : CampaignBehaviorBase
	{
		private DefaultPartyWageModel partyWageModel = new DefaultPartyWageModel();

		public Dictionary<string, ActivityLog> ActivityLogs => IGSaveData.Instance.ActivityLogs;

		public override void RegisterEvents()
		{
			CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, WeeklyEvent);
		}

		public override void SyncData(IDataStore dataStore)
		{
		}

		private void AddGarrisonTrainingCost(Town town, int cost)
		{
			ActivityLog activityLog = GetActivityLog(town);
			activityLog.AddUpgradeCosts(cost);
		}

		private void AddGarrisonRecruitmentCost(Town town, int cost)
		{
			ActivityLog activityLog = GetActivityLog(town);
			activityLog.AddRecruitmentCosts(cost);
		}

		private void AddRecruiterCost(GarrisonRecruiter recruiter, int cost)
		{
			ActivityLog activityLog = GetActivityLog(recruiter.fromSettlement.Town);
			activityLog.AddRecruiterCosts(recruiter.mobileParty.Name.ToString(), cost);
		}

		public void AddUnitRecruitmentCost(Town town, CharacterObject character, int units, Hero hero)
		{
			try
			{
				int cost = partyWageModel.GetTroopRecruitmentCost(character, hero).RoundedResultNumber * units;
				AddGarrisonRecruitmentCost(town, cost);
			}
			catch (Exception ex)
			{
				InformationManager.DisplayMessage(new InformationMessage("Improved Garrison: Could not get unit recruitment cost for: " + character.Name?.ToString() + " Exeption: " + ex.ToString()));
			}
		}

		public void AddUnitRecruitmentCost(GarrisonRecruiter recruiter, CharacterObject character, int units, Hero hero)
		{
			try
			{
				int cost = ((hero != null) ? (partyWageModel.GetTroopRecruitmentCost(character, hero).RoundedResultNumber * units) : (10 * units));
				AddRecruiterCost(recruiter, cost);
			}
			catch (Exception ex)
			{
				InformationManager.DisplayMessage(new InformationMessage("Improved Garrison: Could not get unit recruitment cost for: " + character.Name?.ToString() + " Exeption: " + ex.ToString()));
			}
		}

		public void AddUnitUpgradeCost(Town town, int cost)
		{
			AddGarrisonTrainingCost(town, cost);
		}

		public void AddNewRecruits(Town town, int amount, Settlement fromSettlement = null, bool addActivity = true)
		{
			ActivityLog activityLog = GetActivityLog(town);
			activityLog.AddNewRecruits(amount);
			if (addActivity)
			{
				activityLog.AddActivity(new RecruitmentActivity(amount, fromSettlement));
			}
		}

		public void AddNewUpgrades(Town town, CharacterObject previous, CharacterObject next, int amount)
		{
			ActivityLog activityLog = GetActivityLog(town);
			activityLog.AddNewUpgrades(amount);
			activityLog.AddActivity(new UpgradeActivity(previous, next, amount));
		}

		public void AddNewPrisonerTurnover(Town town, CharacterObject prisoner, int amount)
		{
			ActivityLog activityLog = GetActivityLog(town);
			activityLog.AddNewPrisonerTurnover(amount);
			activityLog.AddActivity(new RecruitmentActivity(amount, prisoner));
		}

		public void AddPartyCreationActivity(Town town, MobileParty party)
		{
			if (party != null)
			{
				ActivityLog activityLog = GetActivityLog(town);
				activityLog.AddActivity(new PartyCreationActivity(party));
			}
		}

		public void AddPartyMergedWithGarrisonActivity(Town town, MobileParty party)
		{
			if (party != null)
			{
				ActivityLog activityLog = GetActivityLog(town);
				activityLog.AddActivity(new PartyMergeWithGarrisonActivity(party));
			}
		}

		public ActivityLog GetActivityLog(Town town)
		{
			try
			{
				if (ActivityLogs.TryGetValue(town.Name.ToString(), out var value))
				{
					return value;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			ActivityLog activityLog = new ActivityLog();
			ActivityLogs.Add(town.Name.ToString(), activityLog);
			return activityLog;
		}

		private void WeeklyEvent()
		{
			foreach (KeyValuePair<string, ActivityLog> activityLog in ActivityLogs)
			{
				activityLog.Value.WeeklyRecruitmentCosts = 0f;
				activityLog.Value.WeeklyTrainingCosts = 0f;
			}
		}
	}
}
