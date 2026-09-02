using System;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.SaveSystem.Configuration;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.Models
{
	public class GarrisonpartySizeLimitModel : DefaultPartySizeLimitModel
	{
		public override ExplainedNumber GetPartyMemberSizeLimit(PartyBase party, bool includeDescriptions = false)
		{
			ExplainedNumber result = base.GetPartyMemberSizeLimit(party, includeDescriptions);
			try
			{
				bool flag = Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(party.MobileParty);
				bool flag2 = Main.PartyManagement.transferPartyManagement.IsTransferParty(party.MobileParty);
				bool flag3 = Main.PartyManagement.garrisonRecruiterPartyManagement.IsRecruiterParty(party.MobileParty);
				if (flag || flag2 || flag3)
				{
					return result = new ExplainedNumber(ConfigManager.Instance.Config.CustomTransferAndGuardPartySize, includeDescriptions: true, new TextObject("[IG-cheats] custom party size"));
				}
				double num = ConfigManager.Instance.Config.CustomGarrisonSizeMultiplier;
				double num2 = ConfigManager.Instance.Config.MainPartySizeMultiplier;
				double num3 = ConfigManager.Instance.Config.PlayerClanPartySizeMultiplier;
				double num4 = ConfigManager.Instance.Config.AIClanPartySizeMultiplier;
				if (party != null && party.MobileParty != null)
				{
					if (party.MobileParty.IsGarrison && num > 0.0)
					{
						float num5 = (float)((double)result.ResultNumber * num - (double)result.ResultNumber);
						if (num5 > 1f)
						{
							result.Add(num5, new TextObject("[IG-cheats] custom garrison size"));
						}
					}
					if (party.MobileParty == MobileParty.MainParty && num2 > 0.0)
					{
						float num6 = (float)((double)result.ResultNumber * num2 - (double)result.ResultNumber);
						if (num6 > 1f)
						{
							result.Add(num6, new TextObject("[IG-cheats] custom player party size"));
						}
					}
					if (party.MobileParty != MobileParty.MainParty && party.MobileParty.ActualClan != null && MobileParty.MainParty.ActualClan != null && party.MobileParty.ActualClan == MobileParty.MainParty.ActualClan && num3 > 0.0)
					{
						float num7 = (float)((double)result.ResultNumber * num3 - (double)result.ResultNumber);
						if (num7 > 1f)
						{
							result.Add(num7, new TextObject("[IG-cheats] custom player clan party size"));
						}
					}
					if (party.MobileParty.ActualClan != null && MobileParty.MainParty.ActualClan != null && party.MobileParty.ActualClan != MobileParty.MainParty.ActualClan && num3 > 0.0)
					{
						float num8 = (float)((double)result.ResultNumber * num3 - (double)result.ResultNumber);
						if (num8 > 1f)
						{
							result.Add(num8, new TextObject("[IG-cheats] custom ai clan party size"));
						}
					}
				}
				if (party != null && party.MobileParty != null && party.MobileParty.StringId == "improvedgarrisons_template_party")
				{
					result = new ExplainedNumber(100000f, includeDescriptions: true, new TextObject("Improved Garrison template party size"));
				}
				return result;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return result;
			}
		}
	}
}
