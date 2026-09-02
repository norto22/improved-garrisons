using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ActivityLogging.Activities
{
	[Serializable]
	public class UpgradeActivity : GarrisonActivity
	{
		private string previousCharacterNameWithLink;

		private string newCharacterNameWithLink;

		private int amount;

		public UpgradeActivity(CharacterObject previousCharacter, CharacterObject newCharacter, int amount)
		{
			previousCharacterNameWithLink = ((previousCharacter.EncyclopediaLinkWithName != null) ? previousCharacter.EncyclopediaLinkWithName.ToString() : previousCharacter.Name.ToString());
			newCharacterNameWithLink = ((newCharacter.EncyclopediaLinkWithName != null) ? newCharacter.EncyclopediaLinkWithName.ToString() : newCharacter.Name.ToString());
			this.amount = amount;
		}

		public override string GetLogDescription()
		{
			if (previousCharacterNameWithLink != null && newCharacterNameWithLink != null && amount > 0)
			{
				string result = previousCharacterNameWithLink + " " + new TextObject("{=ui_improvedgarrisonsui_activity_upgrade1}has been trained to").ToString() + " " + newCharacterNameWithLink;
				if (amount > 1)
				{
					result = amount + " " + previousCharacterNameWithLink + " " + new TextObject("{=ui_improvedgarrisonsui_activity_upgrade2}have been trained to").ToString() + " " + newCharacterNameWithLink;
				}
				return result;
			}
			return "";
		}
	}
}
