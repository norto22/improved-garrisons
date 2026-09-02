using System.Collections.Generic;
using ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements.Elements;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements.ElementLists
{
	public class TownManagementActions
	{
		public MBBindingList<CascadeMenuElementVM> actions = new MBBindingList<CascadeMenuElementVM>();

		public string Title = new TextObject("{=ui_improvedgarrisonsui_activity_townmanagement}Town management").ToString();

		public CascadeMenu Menu;

		private Settlement settlementForAction;

		public TownManagementActions(Settlement settlement)
		{
			settlementForAction = settlement;
			InitializeProjectsActions();
			Menu = new CascadeMenu(new TextObject("{=ui_improvedgarrisonsui_activity_townmanagement}Town management").ToString(), actions);
		}

		private void InitializeProjectsActions()
		{
			List<Building> buildings = settlementForAction.Town.Buildings;
			foreach (Building item in buildings)
			{
			}
			MBBindingList<CascadeMenuElementVM> mBBindingList = new MBBindingList<CascadeMenuElementVM>();
			actions.Add(new CascadeMenuExtendButtonVM(new TextObject("{=ui_improvedgarrisonsui_activity_projects}Projects").ToString(), null));
		}
	}
}
