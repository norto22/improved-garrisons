using ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements;
using ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements.ElementLists;
using ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements.Elements;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus.OverviewUtils
{
	public class SettlementItemWidgetVM : ViewModel
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

		public HintViewModel SettlementImageHoverHint { get; set; } = new HintViewModel(new TextObject("{=ui_settlementitemwidget_track}Track location"));

		public bool IsWarningState { get; set; }

		internal Settlement Settlement { get; private set; }

		public ImprovedGarrisonsUIWidget Widget { get; set; }

		public MBBindingList<SettlementInformationVM> SettlementInformation { get; set; }

		public MBBindingList<VillageItemWidgetVM> Villages { get; set; }

		public SettlementItemWidgetVM(Settlement settlement)
		{
			SettlementComponent settlementComponent = settlement.SettlementComponent;
			FileName = ((settlementComponent == null) ? "placeholder" : (settlementComponent.BackgroundMeshName + "_t"));
			Settlement = settlement;
			SettlementInformation = new MBBindingList<SettlementInformationVM>();
			SettlementInformation.Add(new SettlementInformationVM(Settlement).SetAsGarrisonInformation());
			SettlementInformation.Add(new SettlementInformationVM(Settlement).SetAsFoodChangeInformation());
			SettlementInformation.Add(new SettlementInformationVM(Settlement).SetAsGoldChangeInformation());
			SettlementInformation.Add(new SettlementInformationVM(Settlement).SetAsMobileGarrisonInformation());
			RefreshValues();
		}

		private void InitializeVillages()
		{
			Villages = new MBBindingList<VillageItemWidgetVM>();
			foreach (Village boundVillage in Settlement.BoundVillages)
			{
				if (boundVillage.Settlement != null)
				{
					if (boundVillage.Settlement.IsUnderRaid)
					{
						VillageItemWidgetVM villageItemWidgetVM = new VillageItemWidgetVM(boundVillage.Settlement);
						villageItemWidgetVM.Status = new TextObject("{=ui_settlementitemwidget_raid}Under raid").ToString();
						villageItemWidgetVM.Color = ModuleColors.uiColorAttacked;
						Villages.Add(villageItemWidgetVM);
						IsWarningState = true;
						Color = ModuleColors.uiColorWarning;
						Status = new TextObject("{=ui_settlementitemwidget_hostileraid}Hostile raid").ToString();
					}
					else if (boundVillage.Settlement.IsRaided)
					{
						VillageItemWidgetVM villageItemWidgetVM2 = new VillageItemWidgetVM(boundVillage.Settlement);
						villageItemWidgetVM2.Status = new TextObject("{=ui_settlementitemwidget_raided}Raided").ToString();
						villageItemWidgetVM2.Color = ModuleColors.uiColorDestroyed;
						Villages.Add(villageItemWidgetVM2);
					}
				}
			}
		}

		public void ExecuteTrack()
		{
			if (!Campaign.Current.VisualTrackerManager.CheckTracked(Settlement))
			{
				Campaign.Current.VisualTrackerManager.RegisterObject(Settlement);
			}
		}

		public void ExecuteChangeSelection()
		{
			UIManager.Instance.improvedGarrisonsUI.ChangeSelectorSelection(Settlement);
		}

		public void OpenContextMenu()
		{
			MBBindingList<CascadeMenuElementVM> mBBindingList = new MBBindingList<CascadeMenuElementVM>();
			CreateOrReturnGuardAction createOrReturnGuardAction = new CreateOrReturnGuardAction(Settlement);
			mBBindingList.Add(new CascadeMenuBaseButtonVM(createOrReturnGuardAction.Title, createOrReturnGuardAction.Action));
			CreateOrReturnRecruiterAction createOrReturnRecruiterAction = new CreateOrReturnRecruiterAction(Settlement);
			mBBindingList.Add(new CascadeMenuBaseButtonVM(createOrReturnRecruiterAction.Title, createOrReturnRecruiterAction.Action));
			if (Settlement.IsUnderRaid || Settlement.IsUnderSiege)
			{
				SettlementDefenceActions settlementDefenceActions = new SettlementDefenceActions(Settlement);
				mBBindingList.Add(new CascadeMenuExtendButtonVM(settlementDefenceActions.Title, settlementDefenceActions.Menu));
			}
			UIManager.Instance.CreateCascadeMenuOnMousePointer(new TextObject("{=ui_improvedgarrisonsui_activity}Settlement action").ToString(), mBBindingList);
		}

		public override void RefreshValues()
		{
			base.RefreshValues();
			foreach (SettlementInformationVM information in SettlementInformation)
			{
				information.RefreshValues();
			}
			Settlement settlement = Settlement;
			NameText = settlement?.Name.ToString() ?? "";
			InitializeVillages();
			if (settlement != null && settlement.Town != null)
			{
				if (settlement.IsUnderSiege)
				{
					Color = ModuleColors.uiColorAttacked;
					Status = new TextObject("{=ui_settlementitemwidget_siege}Under siege").ToString();
					IsWarningState = true;
				}
				else if (!IsWarningState)
				{
					Color = ModuleColors.uiColorPeace;
					Status = new TextObject("{=ui_settlementitemwidget_peace}Peaceful").ToString();
				}
			}
		}
	}
}
