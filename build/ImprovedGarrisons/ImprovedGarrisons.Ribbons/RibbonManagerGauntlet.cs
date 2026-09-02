using System;
using System.Linq;
using System.Reflection;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ScreenSystem;

namespace ImprovedGarrisons.Ribbons
{
	[OverrideView(typeof(Ribbon))]
	public class RibbonManagerGauntlet : MapView
	{
		protected RibbonManagerVM ribbonManagerDataSource;

		private string RibbonGauntletID = "RibbonGauntlet";

		protected GauntletLayer _ribbonLayer;

		public RibbonManagerGauntlet()
		{
			ribbonManagerDataSource = new RibbonManagerVM();
		}

		protected override void CreateLayout()
		{
			base.CreateLayout();
			if (_ribbonLayer != null)
			{
				CloseAllRibbons();
			}
			_ribbonLayer = new GauntletLayer(RibbonGauntletID, 105);
			_ribbonLayer.LoadMovie("ImprovedGarrisonsRibbonManager", ribbonManagerDataSource);
			_ribbonLayer.InputRestrictions.SetInputRestrictions(isMouseVisible: false, InputUsageMask.MouseButtons | InputUsageMask.Keyboardkeys);
			MapScreen.Instance.AddLayer(_ribbonLayer);
		}

		public void OpenAllRibbonsForGarrison(Town town)
		{
			SetTownRibbons(town);
			CreateLayout();
		}

		public void CloseAllRibbons()
		{
			GetCurrentRibbonFromMapScreen();
			if (_ribbonLayer != null && ribbonManagerDataSource != null)
			{
				MapScreen.Instance.RemoveLayer(_ribbonLayer);
				_ribbonLayer = null;
				ribbonManagerDataSource.RemoveAllRibbons();
			}
		}

		public void UpdateRibbons()
		{
			CloseAllRibbons();
			OpenAllRibbonsForGarrison(Main.GarrisonBehavior.CurrentTownForSettings);
			if (_ribbonLayer != null)
			{
				MapScreen.Instance.UpdateLayout();
			}
		}

		public void GetCurrentRibbonFromMapScreen()
		{
			try
			{
				bool flag = false;
				foreach (ScreenLayer item in MapScreen.Instance.Layers.ToList())
				{
					if (item is GauntletLayer ribbonLayer && item.Name == RibbonGauntletID)
					{
						if (!flag)
						{
							_ribbonLayer = ribbonLayer;
							flag = true;
						}
						else
						{
							MapScreen.Instance.RemoveLayer(item);
						}
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void AddNewRibbon(string title, string text)
		{
			ribbonManagerDataSource.AddRibbon(title, text);
		}

		private void SetTownRibbons(Town town)
		{
			try
			{
				if (town != null)
				{
					GarrisonSettings townSettings = Main.GarrisonBehavior.GetTownSettings(town);
					MobileGarrison mobileGarrisonPartyOfSettlement = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(town.Settlement);
					GarrisonRecruiter recruiterOfSettlement = Main.PartyManagement.garrisonRecruiterPartyManagement.GetRecruiterOfSettlement(town.Settlement);
					if (mobileGarrisonPartyOfSettlement != null)
					{
						string title = new TextObject("{=party_guards}Garrison guard").ToString();
						string statusText = mobileGarrisonPartyOfSettlement.GetStatusText();
						statusText = statusText.First().ToString().ToUpper() + statusText.Substring(1);
						AddNewRibbon(title, statusText);
					}
					if (recruiterOfSettlement != null)
					{
						string title2 = new TextObject("{=party_recruiter}Garrison recruiter").ToString();
						string statusText2 = recruiterOfSettlement.GetStatusText();
						AddNewRibbon(title2, statusText2);
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		protected override void OnFinalize()
		{
			base.OnFinalize();
			CloseAllRibbons();
		}
	}
}
