using ImprovedGarrisons.ImprovedGarrisonsUI.UIElements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements.Elements
{
	public class CascadeMenuPartyListVM : CascadeMenuElementVM
	{
		private bool _isEmpty;

		public new int OptionTypeID { get; set; } = 3;

		public bool IsEmpty
		{
			get
			{
				return _isEmpty;
			}
			set
			{
				_isEmpty = value;
				IsNotEmpty = !value;
			}
		}

		public bool IsNotEmpty { get; private set; }

		public string EmptyText { get; set; } = new TextObject("{=ui_improvedgarrisonsui_activity_noguards}There are no active guard parties").ToString();

		public MBBindingList<ImprovedGarrisonsPartyInformationVM> Parties { get; set; }

		public CascadeMenuPartyListVM(MBBindingList<ImprovedGarrisonsPartyInformationVM> partyList)
		{
			Parties = partyList;
			if (Parties != null && Parties.Count > 0)
			{
				IsEmpty = false;
			}
			else
			{
				IsEmpty = true;
			}
		}
	}
}
