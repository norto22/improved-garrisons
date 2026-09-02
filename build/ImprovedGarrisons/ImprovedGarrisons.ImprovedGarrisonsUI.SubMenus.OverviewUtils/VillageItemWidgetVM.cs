using ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements;
using ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements.ElementLists;
using ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements.Elements;
using ImprovedGarrisons.ImprovedGarrisonsUI.UIElements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus.OverviewUtils
{
	public class VillageItemWidgetVM : ViewModel
	{
		private string _fileName;

		private string _nameText;

		private string _color;

		private string _status;

		public string FileName
		{
			get
			{
				return _fileName;
			}
			set
			{
				if (value != _fileName)
				{
					_fileName = value;
					OnPropertyChangedWithValue(value, "FileName");
				}
			}
		}

		public string NameText
		{
			get
			{
				return _nameText;
			}
			set
			{
				if (value != _nameText)
				{
					_nameText = value;
					OnPropertyChangedWithValue(value, "NameText");
				}
			}
		}

		public string Color
		{
			get
			{
				return _color;
			}
			set
			{
				if (value != _color)
				{
					_color = value;
					OnPropertyChangedWithValue(value, "Color");
				}
			}
		}

		public string Status
		{
			get
			{
				return _status;
			}
			set
			{
				if (value != _status)
				{
					_status = value;
					OnPropertyChangedWithValue(value, "Status");
				}
			}
		}

		private Settlement Settlement { get; set; }

		public HintViewModel SettlementImageHoverHint { get; set; } = new HintViewModel(new TextObject("{=ui_villagetitemwidget_track}Track village"));

		public VillageItemWidgetVM(Settlement settlement)
		{
			SettlementComponent settlementComponent = settlement.SettlementComponent;
			FileName = ((settlementComponent == null) ? "placeholder" : (settlementComponent.BackgroundMeshName + "_t"));
			Settlement = settlement;
			RefreshValues();
		}

		public void OpenContextMenu()
		{
			MBBindingList<CascadeMenuElementVM> mBBindingList = new MBBindingList<CascadeMenuElementVM>();
			MBBindingList<ImprovedGarrisonsPartyInformationVM> mBBindingList2 = new MBBindingList<ImprovedGarrisonsPartyInformationVM>();
			if (Settlement.IsUnderRaid || Settlement.IsUnderSiege)
			{
				SettlementDefenceActions settlementDefenceActions = new SettlementDefenceActions(Settlement);
				mBBindingList.Add(new CascadeMenuExtendButtonVM(settlementDefenceActions.Title, settlementDefenceActions.Menu));
			}
			UIManager.Instance.CreateCascadeMenuOnMousePointer(new TextObject("{=ui_improvedgarrisonsui_activity_village1}Village action").ToString(), mBBindingList);
		}

		public void ExecuteTrack()
		{
			if (!Campaign.Current.VisualTrackerManager.CheckTracked(Settlement))
			{
				Campaign.Current.VisualTrackerManager.RegisterObject(Settlement);
			}
		}

		public override void RefreshValues()
		{
			base.RefreshValues();
			NameText = Settlement?.Name.ToString() ?? "";
		}
	}
}
