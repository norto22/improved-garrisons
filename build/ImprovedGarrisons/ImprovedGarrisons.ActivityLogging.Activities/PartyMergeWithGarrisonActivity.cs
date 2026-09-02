using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ActivityLogging.Activities
{
	[Serializable]
	public class PartyMergeWithGarrisonActivity : GarrisonActivity
	{
		private string partyName;

		private int size = 0;

		public PartyMergeWithGarrisonActivity(MobileParty party)
		{
			partyName = party.ArmyName.ToString();
			size = party.MemberRoster.TotalManCount;
		}

		public override string GetLogDescription()
		{
			string result = "";
			if (partyName != null)
			{
				result = "<a style=\"Link.Hero\">" + partyName + " </b></a> " + new TextObject("{=ui_improvedgarrisonsui_activity_partymerge1}has returned with").ToString() + " " + size + " " + new TextObject("{=ui_improvedgarrisonsui_activity_partymerge2}troops to the garrison.").ToString();
			}
			return result;
		}
	}
}
