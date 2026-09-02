using System.Collections.Generic;
using System.Linq;
using ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus.OverviewUtils;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus
{
	public class OverviewUIVM : ViewModel
	{
		private GarrisonSettings CurrentGarrisonSettings => Main.GarrisonBehavior.GetCurrentTownSettings();

		public MBBindingList<SettlementItemWidgetVM> Settlements { get; set; }

		public string OverviewTitleText { get; } = new TextObject("{=ui_overviewui_fiefs}Your fiefs").ToString();

		public OverviewUIVM()
		{
			Settlements = new MBBindingList<SettlementItemWidgetVM>();
			RefreshValues();
		}

		private void RefreshSettlements()
		{
			if (Clan.PlayerClan == null || Clan.PlayerClan.Fiefs == null)
			{
				Settlements.Clear();
				return;
			}
			List<Settlement> fiefs = Clan.PlayerClan.Fiefs.Select(fief => fief.Settlement).Where(settlement => settlement != null).ToList();
			if (Settlements.Count == fiefs.Count && fiefs.All(settlement => Settlements.Any(item => item.Settlement == settlement)))
			{
				foreach (SettlementItemWidgetVM settlement in Settlements)
				{
					settlement.RefreshValues();
				}
				return;
			}
			MBBindingList<SettlementItemWidgetVM> refreshedSettlements = new MBBindingList<SettlementItemWidgetVM>();
			foreach (Settlement fief in fiefs)
			{
				SettlementItemWidgetVM settlement = new SettlementItemWidgetVM(fief);
				if (settlement.IsWarningState)
				{
					refreshedSettlements.Insert(0, settlement);
				}
				else
				{
					refreshedSettlements.Add(settlement);
				}
			}
			Settlements = refreshedSettlements;
			OnPropertyChanged("Settlements");
		}

		public void OnCompactModePress()
		{
			UIManager.Instance.improvedGarrisonsUI.SwitchToCompactUI();
		}

		public void OnCompactModeReturnPress()
		{
			UIManager.Instance.improvedGarrisonsUI.SwitchBackToNormalUI();
		}

		public override void RefreshValues()
		{
			base.RefreshValues();
			RefreshSettlements();
		}
	}
}
