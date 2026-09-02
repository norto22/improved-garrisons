using System;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements.ElementLists
{
	public class CreateOrReturnRecruiterAction
	{
		public Action Action;

		private Settlement settlementForAction;

		private GarrisonRecruiter recruiter;

		public string Title
		{
			get
			{
				if (recruiter == null)
				{
					return new TextObject("{=ui_improvedgarrisonsui_activity_recruiter1}Create recruiter").ToString();
				}
				return new TextObject("{=ui_improvedgarrisonsui_activity_recruiter2}Return recruiter").ToString();
			}
		}

		public CreateOrReturnRecruiterAction(Settlement settlement)
		{
			settlementForAction = settlement;
			recruiter = Main.PartyManagement.garrisonRecruiterPartyManagement.GetRecruiterOfSettlement(settlementForAction);
			InitializeAction();
		}

		private void InitializeAction()
		{
			Action = delegate
			{
				UIManager.Instance.CloseCascadeMenu();
				if (recruiter == null)
				{
					RecruitmentSettings.Instance.PromptCreateRecruiter(settlementForAction.Town);
				}
				else
				{
					RecruitmentSettings.Instance.ReturnRecruiter(settlementForAction.Town);
				}
			};
		}
	}
}
