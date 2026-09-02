using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ActivityLogging.Activities
{
	[Serializable]
	public class RecruitmentActivity : GarrisonActivity
	{
		private string settlementNameWithLink;

		private string prisonerNameWithLink;

		private int amount;

		public RecruitmentActivity(int amount, Settlement recruitedFrom = null)
		{
			this.amount = amount;
			if (recruitedFrom != null)
			{
				settlementNameWithLink = ((recruitedFrom.EncyclopediaLinkWithName != null) ? recruitedFrom.EncyclopediaLinkWithName.ToString() : recruitedFrom.Name.ToString());
			}
		}

		public RecruitmentActivity(int amount, CharacterObject prisoner)
		{
			if (prisoner != null)
			{
				this.amount = amount;
				prisonerNameWithLink = ((prisoner.EncyclopediaLinkWithName != null) ? prisoner.EncyclopediaLinkWithName.ToString() : prisoner.Name.ToString());
			}
		}

		public override string GetLogDescription()
		{
			string text = "";
			if (amount > 0 && settlementNameWithLink != null)
			{
				return amount + " " + new TextObject("{=ui_improvedgarrisonsui_activity_recruitment1}troops have been recruited from").ToString() + " " + settlementNameWithLink + ".";
			}
			if (prisonerNameWithLink != null && amount == 1)
			{
				return prisonerNameWithLink + " " + new TextObject("{=ui_improvedgarrisonsui_activity_recruitment2}has been recruited from the dungeon.").ToString();
			}
			if (prisonerNameWithLink != null && amount > 0)
			{
				return amount + " " + prisonerNameWithLink + " " + new TextObject("{=ui_improvedgarrisonsui_activity_recruitment3}have been recruited from the dungeon.").ToString();
			}
			return amount + " " + new TextObject("{=ui_improvedgarrisonsui_activity_recruitment4}troops have been recruited.").ToString();
		}
	}
}
