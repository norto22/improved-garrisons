using System;
using System.Collections.Generic;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.ImprovedGarrisonsUI;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.Menu
{
	public class MainMenu : ImprovedGarrisonMenu
	{
		public MainMenu(CampaignGameStarter gameStarter)
			: base(gameStarter)
		{
		}

		public override void AddGameMenus(CampaignGameStarter gameStarter)
		{
			try
			{
				gameStarter.AddGameMenuOption("town_keep", "town_garrison_options", new TextObject("{=menu_start}Improved Garrison").ToString(), Garrison_menu_on_playerowner_condition, delegate
				{
					garrisonBehavior.CurrentTownForSettings = Settlement.CurrentSettlement.Town;
					if (UIManager.Instance.TryInitializeImprovedGarrisonsUI() || UIManager.Instance.improvedGarrisonsUI != null)
					{
						UIManager.Instance.improvedGarrisonsUI.UpdateSettlementSelector();
						UIManager.Instance.CurrentUiState = UIManager.UiState.Expanded;
					}
				}, isLeave: false, 1);
				gameStarter.AddGameMenuOption("castle", "town_garrison_options", new TextObject("{=menu_start}Improved Garrison").ToString(), Garrison_menu_on_playerowner_condition, delegate
				{
					garrisonBehavior.CurrentTownForSettings = Settlement.CurrentSettlement.Town;
					if (UIManager.Instance.TryInitializeImprovedGarrisonsUI() || UIManager.Instance.improvedGarrisonsUI != null)
					{
						UIManager.Instance.improvedGarrisonsUI.UpdateSettlementSelector();
						UIManager.Instance.CurrentUiState = UIManager.UiState.Expanded;
					}
				}, isLeave: false, 1);
				gameStarter.AddGameMenuOption("town_keep", "town_garrison_options", new TextObject("{=menu_manage}Manage your Improved Garrisons").ToString(), Garrison_menu_on_playerNotowner_condition, delegate
				{
					garrisonBehavior.CurrentTownForSettings = Settlement.CurrentSettlement.Town;
					if (UIManager.Instance.TryInitializeImprovedGarrisonsUI() || UIManager.Instance.improvedGarrisonsUI != null)
					{
						UIManager.Instance.improvedGarrisonsUI.UpdateSettlementSelector();
						UIManager.Instance.CurrentUiState = UIManager.UiState.Expanded;
					}
				}, isLeave: false, 1);
				gameStarter.AddGameMenuOption("castle", "town_garrison_options", new TextObject("{=menu_manage}Manage your Improved Garrisons").ToString(), Garrison_menu_on_playerNotowner_condition, delegate
				{
					garrisonBehavior.CurrentTownForSettings = Settlement.CurrentSettlement.Town;
					if (UIManager.Instance.TryInitializeImprovedGarrisonsUI() || UIManager.Instance.improvedGarrisonsUI != null)
					{
						UIManager.Instance.improvedGarrisonsUI.UpdateSettlementSelector();
						UIManager.Instance.CurrentUiState = UIManager.UiState.Expanded;
					}
				}, isLeave: false, 1);
				gameStarter.AddGameMenu("garrison_options", "Menu no longer supported", null, GameMenu.MenuOverlayType.SettlementWithCharacters);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private bool Garrison_menu_on_playerowner_condition(MenuCallbackArgs args)
		{
			args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
			Settlement currentSettlement = Settlement.CurrentSettlement;
			return currentSettlement.OwnerClan == Clan.PlayerClan && currentSettlement.MapFaction == Hero.MainHero.MapFaction && currentSettlement.IsFortification;
		}

		private bool Garrison_menu_on_playerNotowner_condition(MenuCallbackArgs args)
		{
			args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
			Settlement currentSettlement = Settlement.CurrentSettlement;
			bool flag = currentSettlement.OwnerClan != Clan.PlayerClan;
			bool flag2 = MobileParty.MainParty != null && !MobileParty.MainParty.Party.MapFaction.IsAtWarWith(currentSettlement.MapFaction);
			bool flag3 = Main.GarrisonBehavior.SettlementSettingsData != null && Main.GarrisonBehavior.SettlementSettingsData.Count > 1;
			return flag && flag2 && currentSettlement.IsFortification && flag3;
		}

		private void Inquirydata_GlobalGarrisonSettingsMenu(List<InquiryElement> list)
		{
			if (list.Count > 0)
			{
				Town town = (Town)list.GetRandomElement().Identifier;
				garrisonBehavior.CurrentTownForSettings = town;
				GameMenu.SwitchToMenu("garrison_options");
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_global_conf}You are now editing the settings of").ToString() + str_space + town.Name, Color.FromUint(ModuleColors.green)));
			}
		}

		public void BackToMainMenu()
		{
			Back_on_consequence(null);
		}
	}
}
