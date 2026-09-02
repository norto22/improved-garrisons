using ImprovedGarrisons.ActivityLogging;
using ImprovedGarrisons.SaveSystem.Configuration;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.SaveSystem
{
	public class GarrisonDailyBehavior : CampaignBehaviorBase
	{
		public override void RegisterEvents()
		{
			CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DailyBehavior);
		}

		public override void SyncData(IDataStore dataStore)
		{
		}

		private void DailyBehavior()
		{
			if (ConfigManager.Instance.Config.RecruitsAreRecruitedFromVillages)
			{
				Main.RecruitmentLogic.RecruitSurroundingForAllSettlements();
			}
			else
			{
				Main.RecruitmentLogic.CheatSpawnUnitInAllGarrisons(ConfigManager.Instance.Config.AmountOfUnitsToSpawn);
			}
			Main.RecruitmentLogic.TryRecruitAllPrisoners();
			Main.UpgradeLogic.GiveExpToAllGarrisons();
			if (ConfigManager.Instance.Config.DisableDailyMessage)
			{
				return;
			}
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			foreach (ActivityLog value in Main.ActivityLogManager.ActivityLogs.Values)
			{
				num += value.DailyRecruits;
				num2 += value.DailyUpgrades;
				num3 += value.DailyPrisonerTurnover;
				value.ResetDailies();
			}
			bool flag = num > 0;
			bool flag2 = num2 > 0;
			bool flag3 = num3 > 0;
			if (flag || flag2 || flag3)
			{
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_daily_info1}\"My lord your Improved Garrisons recruited").ToString() + " " + num + " " + new TextObject("{=info_daily_info2}soldiers, upgraded").ToString() + " " + num2 + " " + new TextObject("{=info_daily_info3}troops to the next tier and persuaded").ToString() + " " + num3 + " " + new TextObject("{=info_daily_info4}prisoners today.\"").ToString(), Color.FromUint(ModuleColors.modMainColor)));
			}
		}
	}
}
