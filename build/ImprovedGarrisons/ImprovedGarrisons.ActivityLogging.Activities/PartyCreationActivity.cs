using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ActivityLogging.Activities
{
	[Serializable]
	public class PartyCreationActivity : GarrisonActivity
	{
		private string partyName;

		private int size = 0;

		public PartyCreationActivity(MobileParty party)
		{
			partyName = party.ArmyName.ToString();
			size = party.MemberRoster.TotalManCount;
		}

		public override string GetLogDescription()
		{
			string result = "";
			if (partyName != null)
			{
				result = new TextObject("{=ui_improvedgarrisonsui_activity_partycreation1}A new party").ToString() + " <a style=\"Link.Hero\">" + partyName + " </b></a> " + new TextObject("{=ui_improvedgarrisonsui_activity_partycreation2}has been created with").ToString() + " " + size + " " + new TextObject("{=ui_improvedgarrisonsui_activity_partycreation3}troops.").ToString();
			}
			return result;
		}
	}
}
