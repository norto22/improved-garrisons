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
	internal class GarrisonSpeedModel : DefaultPartySpeedCalculatingModel
	{
		public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
		{
			try
			{
				ExplainedNumber result = base.CalculateFinalSpeed(mobileParty, finalSpeed);
				float customGuardAndTransferPartySpeed = ConfigManager.Instance.Config.CustomGuardAndTransferPartySpeed;
				if (mobileParty != null && customGuardAndTransferPartySpeed > result.ResultNumber && (Main.PartyManagement.mobileGarrisonManagement.IsMobileGarrisonParty(mobileParty) || Main.PartyManagement.transferPartyManagement.IsTransferParty(mobileParty) || Main.PartyManagement.garrisonRecruiterPartyManagement.IsRecruiterParty(mobileParty)))
				{
					result.Add(customGuardAndTransferPartySpeed - result.ResultNumber, new TextObject("Improved Garrisons custom minimum speed"));
				}
				return result;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return default(ExplainedNumber);
			}
		}
	}
}
