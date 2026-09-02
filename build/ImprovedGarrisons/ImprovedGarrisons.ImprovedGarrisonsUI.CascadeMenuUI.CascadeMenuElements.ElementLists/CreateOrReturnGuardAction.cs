using System;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements.ElementLists
{
	public class CreateOrReturnGuardAction
	{
		public Action Action;

		private Settlement settlementForAction;

		private MobileGarrison mobileGarrison;

		public string Title
		{
			get
			{
				if (mobileGarrison == null)
				{
					return new TextObject("{=ui_improvedgarrisonsui_activity_guard1}Create guard party").ToString();
				}
				return new TextObject("{=ui_improvedgarrisonsui_activity_guard2}Return guard party").ToString();
			}
		}

		public CreateOrReturnGuardAction(Settlement settlement)
		{
			settlementForAction = settlement;
			mobileGarrison = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(settlementForAction);
			InitializeAction();
		}

		private void InitializeAction()
		{
			Action = delegate
			{
				UIManager.Instance.CloseCascadeMenu();
				if (mobileGarrison == null)
				{
					MobileGarrisonSettings.Instance.PromptCreateMobileGarrison(settlementForAction.Town);
				}
				else
				{
					MobileGarrisonSettings.Instance.OrderMobileGarrisonReturn(settlementForAction.Town);
				}
			};
		}
	}
}
