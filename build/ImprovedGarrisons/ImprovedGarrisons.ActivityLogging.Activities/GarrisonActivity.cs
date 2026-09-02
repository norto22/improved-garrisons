using System;
using TaleWorlds.CampaignSystem;

namespace ImprovedGarrisons.ActivityLogging.Activities
{
	[Serializable]
	public abstract class GarrisonActivity
	{
		public string CampaignDayOfTheActivity { get; private set; }

		public GarrisonActivity()
		{
			_ = CampaignTime.Now;
			CampaignDayOfTheActivity = CampaignTime.Now.ToString();
		}

		public abstract string GetLogDescription();
	}
}
