using System;
using ImprovedGarrisons.AI.AITypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus.OverviewUtils
{
	public class SettlementInformationVM : ViewModel
	{
		private enum Type
		{
			Custom,
			Garrison,
			FoodChange,
			MobileGarrison,
			GoldChange,
			Prosperity,
			Militia,
			Loyality,
			Construction,
			Reserve
		}

		private Func<string> onCustomChange;

		private Type _type = Type.Custom;

		private HintViewModel _hint;

		private string _iconPath;

		private string _value;

		private string _description;

		private Settlement _settlement;

		public HintViewModel Hint
		{
			get
			{
				return _hint;
			}
			set
			{
				if (value != _hint)
				{
					_hint = value;
					OnPropertyChangedWithValue(value, "Hint");
				}
			}
		}

		public string Iconpath
		{
			get
			{
				return _iconPath;
			}
			set
			{
				if (value != _iconPath)
				{
					_iconPath = value;
					OnPropertyChangedWithValue(value, "Iconpath");
				}
			}
		}

		public string Value
		{
			get
			{
				return _value;
			}
			set
			{
				if (value != _value)
				{
					_value = value;
					OnPropertyChangedWithValue(value, "Value");
				}
			}
		}

		public string Description
		{
			get
			{
				return _description;
			}
			set
			{
				if (value != _description)
				{
					_description = value;
					OnPropertyChangedWithValue(value, "Description");
				}
			}
		}

		public SettlementInformationVM(Settlement settlement)
		{
			_settlement = settlement;
		}

		public SettlementInformationVM SetAsCustomInformation(string iconPath, HintViewModel hint, string description, Func<string> onChange)
		{
			Iconpath = iconPath;
			_type = Type.Custom;
			Hint = hint;
			Description = description;
			onCustomChange = onChange;
			RefreshValues();
			return this;
		}

		public SettlementInformationVM SetAsGarrisonInformation()
		{
			Iconpath = "General\\Icons\\Garrison";
			_type = Type.Garrison;
			Hint = new HintViewModel(new TextObject("{=ui_settlementinformation_garrisonsize1}The current garrison size."));
			Description = new TextObject("{=ui_settlementinformation_garrisonsize2}Garrison size").ToString();
			RefreshValues();
			return this;
		}

		public SettlementInformationVM SetAsFoodChangeInformation()
		{
			Iconpath = "General\\Icons\\Food@2x";
			_type = Type.FoodChange;
			Hint = new HintViewModel(new TextObject("{=ui_settlementinformation_foodamount1}The current food value in this location."));
			Description = new TextObject("{=ui_settlementinformation_foodamount2}Food").ToString();
			RefreshValues();
			return this;
		}

		public SettlementInformationVM SetAsMobileGarrisonInformation()
		{
			Iconpath = "General\\Icons\\Party@2x";
			_type = Type.MobileGarrison;
			Hint = new HintViewModel(new TextObject("{=ui_settlementinformation_guardparty1}Has an active guard party."));
			Description = new TextObject("{=ui_settlementinformation_guardparty2}Active guard party").ToString();
			RefreshValues();
			return this;
		}

		public SettlementInformationVM SetAsGoldChangeInformation()
		{
			Iconpath = "General\\Icons\\Coin@2x";
			_type = Type.GoldChange;
			Hint = new HintViewModel(new TextObject("{=ui_settlementinformation_goldchange1}The calculated value of this location's income and workshops minus the garrison and guard party wages."));
			Description = new TextObject("{=ui_settlementinformation_goldchange2}Denars growth").ToString();
			RefreshValues();
			return this;
		}

		public SettlementInformationVM SetAsProsperityInformation()
		{
			Iconpath = "General\\Icons\\Prosperity";
			_type = Type.Prosperity;
			Hint = new HintViewModel(new TextObject("{=ui_settlementinformation_prosperity1}The current prosperity value in this location."));
			Description = new TextObject("{=ui_settlementinformation_prosperity2}Prosperity").ToString();
			RefreshValues();
			return this;
		}

		public SettlementInformationVM SetAsMilitiaInformation()
		{
			Iconpath = "General\\Icons\\Militia";
			_type = Type.Militia;
			Hint = new HintViewModel(new TextObject("{=ui_settlementinformation_militia1}The current militia headcount in this location."));
			Description = new TextObject("{=ui_settlementinformation_militia2}Militia").ToString();
			RefreshValues();
			return this;
		}

		public SettlementInformationVM SetAsLoyalityInformation()
		{
			Iconpath = "SPGeneral\\MapOverlay\\Settlement\\icon_loyalty";
			_type = Type.Loyality;
			Hint = new HintViewModel(new TextObject("{=ui_settlementinformation_loyality1}The current loyalty value in this location."));
			Description = new TextObject("{=ui_settlementinformation_loyality2}Loyalty").ToString();
			RefreshValues();
			return this;
		}

		public SettlementInformationVM SetAsConstructionInformation()
		{
			Iconpath = "SPGeneral\\TownManagement\\production_icon";
			_type = Type.Construction;
			Hint = new HintViewModel(new TextObject("{=ui_settlementinformation_construction1}The current construction value in this location."));
			Description = new TextObject("{=ui_settlementinformation_construction2}Construction").ToString();
			RefreshValues();
			return this;
		}

		public SettlementInformationVM SetAsReserveInformation()
		{
			Iconpath = "General\\Icons\\Coin@2x";
			_type = Type.Reserve;
			Hint = new HintViewModel(new TextObject("{=ui_settlementinformation_reserve1}The current reserve in this location."));
			Description = new TextObject("{=ui_settlementinformation_reserve2}Reserve").ToString();
			RefreshValues();
			return this;
		}

		public override void RefreshValues()
		{
			base.RefreshValues();
			switch (_type)
			{
			case Type.Custom:
				Value = onCustomChange();
				break;
			case Type.Garrison:
				if (_settlement.Town != null && _settlement.Town.GarrisonParty != null)
				{
					Value = _settlement.Town.GarrisonParty.Party.NumberOfAllMembers.ToString();
				}
				else
				{
					Value = "0";
				}
				break;
			case Type.FoodChange:
				if (_settlement.Town != null)
				{
					Value = (int)_settlement.Town.FoodStocks + " (";
					double num7 = Math.Round(_settlement.Town.FoodChange, 2);
					Value += ((num7 > 0.0) ? "+" : "");
					Value = Value + num7 + ")";
				}
				break;
			case Type.GoldChange:
			{
				if (_settlement.Town == null || Campaign.Current == null || Campaign.Current.Models.PartyWageModel == null)
				{
					break;
				}
				float num3 = 0f;
				Workshop[] workshops = _settlement.Town.Workshops;
				Workshop[] array = workshops;
				foreach (Workshop workshop in array)
				{
					if (workshop.Owner == Hero.MainHero)
					{
						num3 += (float)workshop.ProfitMade;
					}
				}
				ExplainedNumber explainedNumber = new ExplainedNumber(0f, includeDescriptions: false, null);
				if (Campaign.Current != null && Campaign.Current.Models.SettlementTaxModel != null)
				{
					explainedNumber = Campaign.Current.Models.SettlementTaxModel.CalculateTownTax(_settlement.Town);
				}
				float num4 = 0f;
				MobileGarrison mobileGarrisonPartyOfSettlement2 = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(_settlement);
				if (mobileGarrisonPartyOfSettlement2 != null)
				{
					num4 = mobileGarrisonPartyOfSettlement2.mobileParty.TotalWage;
				}
				float num5 = 0f;
				MobileParty garrisonParty = _settlement.Town.GarrisonParty;
				if (garrisonParty != null)
				{
					num5 = garrisonParty.TotalWage;
				}
				double num6 = Math.Round(explainedNumber.ResultNumber + num3 - (num4 + num5));
				Value = ((num6 > 0.0) ? "+" : "");
				Value += num6;
				break;
			}
			case Type.MobileGarrison:
				if (_settlement.Town != null)
				{
					MobileGarrison mobileGarrisonPartyOfSettlement = Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonPartyOfSettlement(_settlement);
					Value = ((mobileGarrisonPartyOfSettlement != null) ? new TextObject("{=ui_settlementinformation_activ}active").ToString() : new TextObject("{=ui_settlementinformation_none}none").ToString());
				}
				break;
			case Type.Prosperity:
				if (_settlement.Town != null)
				{
					Value = (int)_settlement.Town.Prosperity + " (";
					double num = Math.Round(_settlement.Town.ProsperityChange, 2);
					Value += ((num > 0.0) ? "+" : "");
					Value = Value + num + ")";
				}
				break;
			case Type.Militia:
				if (_settlement.Town != null)
				{
					Value = ((int)_settlement.Town.Militia).ToString();
				}
				break;
			case Type.Loyality:
				if (_settlement.Town != null)
				{
					Value = (int)_settlement.Town.Loyalty + " (";
					double num2 = Math.Round(_settlement.Town.LoyaltyChange, 2);
					Value += ((num2 > 0.0) ? "+" : "");
					Value = Value + num2 + ")";
				}
				break;
			case Type.Construction:
				if (_settlement.Town != null)
				{
					Value = ((int)_settlement.Town.Construction).ToString();
				}
				break;
			case Type.Reserve:
				if (_settlement.Town != null)
				{
					int boostBuildingProcess = _settlement.Town.BoostBuildingProcess;
					Value = boostBuildingProcess.ToString();
				}
				break;
			}
		}
	}
}
