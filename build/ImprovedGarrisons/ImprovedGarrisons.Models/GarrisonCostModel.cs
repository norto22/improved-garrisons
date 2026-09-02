using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImprovedGarrisons.ActivityLogging;
using ImprovedGarrisons.Behaviours;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.SaveSystem.Configuration;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.Models
{
	public class GarrisonCostModel : DefaultClanFinanceModel
	{
		public override ExplainedNumber CalculateClanExpenses(Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false, bool includeDetails = false)
		{
			ExplainedNumber explainedNumber = base.CalculateClanExpenses(clan, includeDescriptions: true, applyWithdrawals, includeDetails: true);
			return base.CalculateClanExpenses(clan, includeDescriptions, applyWithdrawals, includeDetails);
		}

		public override ExplainedNumber CalculateClanGoldChange(Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false, bool includeDetails = false)
		{
			ExplainedNumber currentValue = base.CalculateClanGoldChange(clan, includeDescriptions, applyWithdrawals, includeDetails);
			return CalculateImprovedGarrisonCosts(clan, includeDescriptions, applyWithdrawals, currentValue);
		}

		private ExplainedNumber CalculateImprovedGarrisonCosts(Clan clan, bool includeDescriptions, bool applyWithdrawals, ExplainedNumber currentValue)
		{
			ExplainedNumber result = currentValue;
			try
			{
				if (clan == Hero.MainHero.Clan)
				{
				}
				List<MobileParty> allClanParties = GarrisonPartyBehavior.GetAllClanParties(clan);
				bool flag = allClanParties != null;
				double num = ConfigManager.Instance.Config.GarrisonTrainingMultiplier;
				if (num <= 1.0 && num > 0.0 && flag)
				{
					foreach (Town fief in clan.Fiefs)
					{
						ActivityLog activityLog = Main.ActivityLogManager.GetActivityLog(fief);
						if (activityLog == null || (!fief.IsCastle && !fief.IsTown))
						{
							continue;
						}
						TextObject description = new TextObject("{=misc_costmodel_trainingcosts}Improved garrison training of" + ModuleStrings._space + fief.Name);
						if (activityLog.UnitUpgradeCosts > 0f)
						{
							result.Add(0f - activityLog.UnitUpgradeCosts, description);
							if (applyWithdrawals)
							{
								activityLog.ResetUpgradeCosts();
							}
						}
						TextObject description2 = new TextObject("{=misc_costmodel_recruitmentcosts}Improved garrison recruitment of" + ModuleStrings._space + fief.Name);
						if (activityLog.RecruitmentCosts > 0f)
						{
							result.Add(0f - activityLog.RecruitmentCosts, description2);
							if (applyWithdrawals)
							{
								activityLog.ResetRecruitmentCosts();
							}
						}
						foreach (KeyValuePair<string, float> item in activityLog.RecruiterCosts.ToList())
						{
							TextObject description3 = new TextObject(item.Key + ModuleStrings._space + new TextObject("{=misc_costs}costs").ToString());
							float value = item.Value;
							result.Add(0f - value, description3);
							if (applyWithdrawals)
							{
								activityLog.RecruiterCosts.Remove(item.Key);
							}
						}
					}
				}
				double num2 = ConfigManager.Instance.Config.GarrisonGuardsWageMultiplier;
				if (num2 <= 1.0 && num2 > 0.0 && flag)
				{
					foreach (MobileParty item2 in allClanParties)
					{
						if (item2 != null && item2.Party != null && item2.Party.IsMobile && Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(item2) && TryGetTotalWage(item2, out var totalWage))
						{
							Settlement mobileGarrisonHome = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonHome(item2);
							if (mobileGarrisonHome != null)
							{
								TextObject description4 = new TextObject(mobileGarrisonHome.Name?.ToString() + ModuleStrings._space + new TextObject("{=misc_guardwages}Guard wages").ToString());
								result.Add((float)(num2 * (double)(0f - (float)totalWage)), description4);
							}
						}
					}
				}
				float garrisonWageMultiplier = ConfigManager.Instance.Config.GarrisonWageMultiplier;
				if (!(garrisonWageMultiplier >= 1f) && flag)
				{
					foreach (MobileParty item3 in allClanParties)
					{
						if (item3 == null || !item3.IsActive || !item3.IsGarrison || !TryGetTotalWage(item3, out var totalWage2))
						{
							continue;
						}
						int num3 = ((item3.LeaderHero != null && item3.LeaderHero != clan.Leader && !item3.IsCaravan && item3.LeaderHero.Gold <= 10000) ? ((item3.LeaderHero.Gold < 5000) ? ((int)((float)(5000 - item3.LeaderHero.Gold) / 10f)) : 0) : 0);
						float num4 = (float)clan.Gold + result.ResultNumber;
						if (num4 > (float)(totalWage2 + num3))
						{
							int num5 = totalWage2;
							if (num3 > 0)
							{
								GiveGoldAction.ApplyBetweenCharacters(null, item3.LeaderHero, num3, disableNotification: true);
							}
						}
						else
						{
							int num5 = (int)(((int)num4 > 0) ? num4 : 0f);
							if (num5 > totalWage2)
							{
								num5 = totalWage2;
							}
						}
						TextObject textObject = new TextObject("{=rhKxsdtz} {PARTY_NAME} finance help");
						textObject.SetTextVariable("PARTY_NAME", item3.Name);
						double num6 = -1f * (garrisonWageMultiplier - 1f);
						result.Add((float)(num6 * (double)(float)totalWage2), textObject);
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return result;
		}

		private bool TryGetTotalWage(MobileParty party, out int totalWage)
		{
			totalWage = 0;
			try
			{
				if (party == null || party.Party == null || party.MemberRoster == null)
				{
					return false;
				}
				totalWage = party.TotalWage;
				return true;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return false;
			}
		}
	}
}
