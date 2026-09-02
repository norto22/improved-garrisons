using ImprovedGarrisons.SaveSystem.Configuration;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.Models
{
	public class GarrisonFoodModel : DefaultSettlementFoodModel
	{
		public override ExplainedNumber CalculateTownFoodStocksChange(Town town, bool includeMarketStocks = true, bool includeDescriptions = false)
		{
			ExplainedNumber result = base.CalculateTownFoodStocksChange(town, includeMarketStocks, includeDescriptions);
			bool enablePlayerFoodBonus = ConfigManager.Instance.Config.EnablePlayerFoodBonus;
			bool enabeNPCFoodbonus = ConfigManager.Instance.Config.EnabeNPCFoodbonus;
			bool loadFoodGatheringModule = ConfigManager.Instance.Config.LoadFoodGatheringModule;
			bool flag = false;
			if (Hero.MainHero != null)
			{
				flag = town.OwnerClan != Hero.MainHero.Clan;
			}
			if (((flag && enabeNPCFoodbonus) || (!flag && enablePlayerFoodBonus)) && loadFoodGatheringModule)
			{
				ExplainedNumber explainedNumber = new ExplainedNumber(0f, includeDescriptions);
				TextObject description = new TextObject("[IG-Cheats] Garrison Food Bonus");
				explainedNumber.Add(ConfigManager.Instance.Config.DailyFoodGatheringAmount, description);
				result.Add(explainedNumber.ResultNumber, description);
			}
			if (ConfigManager.Instance.Config.DisableGarrisonNeedsFood)
			{
				int num = town.GarrisonParty?.Party.NumberOfAllMembers ?? 0;
				num /= 20;
				ExplainedNumber explainedNumber2 = new ExplainedNumber(0f, includeDescriptions);
				TextObject description2 = new TextObject("[IG-Cheats] Garrison needs no food");
				explainedNumber2.Add(num, description2);
				result.Add(explainedNumber2.ResultNumber, description2);
			}
			return result;
		}
	}
}
