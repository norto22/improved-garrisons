using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ActivityLogging.Activities
{
	[Serializable]
	public class PartyDestructionActivity : GarrisonActivity
	{
		private string partyName;

		public PartyDestructionActivity(MobileParty party)
		{
			partyName = party.GetName().ToString();
		}

		public override string GetLogDescription()
		{
			string result = "";
			if (partyName != null)
			{
				result = partyName + new TextObject("{=ui_improvedgarrisonsui_activity_destroyed1}has been destroyed.").ToString();
			}
			return result;
		}
	}
}
