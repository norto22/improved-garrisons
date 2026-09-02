using System;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.UIElements
{
	public class ImprovedGarrisonsTroopItemWidgetVM : ViewModel
	{
		private int _currentAmount;

		private int _heroHealthPercent;

		private CharacterImageIdentifierVM _visual;

		private bool _isTroopHero;

		private string _name;

		private string _amountText;

		private StringItemWithHintVM _tierIconData;

		private StringItemWithHintVM _typeIconData;

		private TrainingUIVM _trainingDatasource;

		private ManagementUIVM _managementDatasource;

		public HintViewModel RemoveHint { get; } = new HintViewModel(new TextObject("{=ui_improvedgarrisonwidget_removetroop}Right-click to remove a troop type from a template."));

		public HintViewModel TemplateAmountHint { get; } = new HintViewModel(new TextObject("{=ui_improvedgarrisonwidget_currentamount}The current amount of this troop type in the template."));

		public HintViewModel GarrisonAmountHint { get; } = new HintViewModel(new TextObject("{=ui_improvedgarrisonwidget_currentgarrisonamount}The current amount of this troop type in the garrison."));

		public TroopRosterElement CurrentTroop { get; set; }

		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				if (value != _name)
				{
					_name = value;
					OnPropertyChangedWithValue(value, "Name");
				}
			}
		}

		public int CurrentAmount
		{
			get
			{
				return _currentAmount;
			}
			set
			{
				if (value != _currentAmount)
				{
					_currentAmount = value;
					OnPropertyChangedWithValue(value, "CurrentAmount");
				}
			}
		}

		public bool IsTroopHero
		{
			get
			{
				return _isTroopHero;
			}
			set
			{
				if (value != _isTroopHero)
				{
					_isTroopHero = value;
					OnPropertyChangedWithValue(value, "IsTroopHero");
				}
			}
		}

		public int HeroHealthPercent
		{
			get
			{
				return _heroHealthPercent;
			}
			set
			{
				if (value != _heroHealthPercent)
				{
					_heroHealthPercent = value;
					OnPropertyChangedWithValue(value, "HeroHealthPercent");
				}
			}
		}

		public CharacterImageIdentifierVM Visual
		{
			get
			{
				return _visual;
			}
			set
			{
				if (value != _visual)
				{
					_visual = value;
					OnPropertyChangedWithValue(value, "Visual");
				}
			}
		}

		public StringItemWithHintVM TierIconData
		{
			get
			{
				return _tierIconData;
			}
			set
			{
				if (value != _tierIconData)
				{
					_tierIconData = value;
					OnPropertyChangedWithValue(value, "TierIconData");
				}
			}
		}

		public StringItemWithHintVM TypeIconData
		{
			get
			{
				return _typeIconData;
			}
			set
			{
				if (value != _typeIconData)
				{
					_typeIconData = value;
					OnPropertyChangedWithValue(value, "TypeIconData");
				}
			}
		}

		public ImprovedGarrisonsTroopItemWidgetVM(TroopRosterElement troop, TrainingUIVM trainingDatasource = null, ManagementUIVM managementDatasource = null)
		{
			CurrentTroop = troop;
			_trainingDatasource = trainingDatasource;
			_managementDatasource = managementDatasource;
			CurrentAmount = CurrentTroop.Number;
			Name = CurrentTroop.Character.Name.ToString();
			IsTroopHero = CurrentTroop.Character.IsHero;
			HeroHealthPercent = (CurrentTroop.Character.IsHero ? ((int)Math.Ceiling((double)((float)CurrentTroop.Character.HeroObject.HitPoints / (float)CurrentTroop.Character.MaxHitPoints()) * 100.0)) : 0);
			CharacterImageIdentifierVM visual = null;
			try
			{
				visual = new CharacterImageIdentifierVM(CampaignUIHelper.GetCharacterCode(CurrentTroop.Character));
			}
			catch (Exception)
			{
			}
			Visual = visual;
			TierIconData = CampaignUIHelper.GetCharacterTierData(CurrentTroop.Character);
			TypeIconData = CampaignUIHelper.GetCharacterTypeData(CurrentTroop.Character);
			RefreshValues();
		}

		private void ExecuteAdd()
		{
			try
			{
				GarrisonSettings currentTownSettings = Main.GarrisonBehavior.GetCurrentTownSettings();
				if (currentTownSettings != null)
				{
					currentTownSettings.Template.AddOrUpdateCharacter(CurrentTroop.Character, currentTownSettings.Template.GetAmountForTemplateTroop(CurrentTroop.Character) + 1);
					_trainingDatasource.TroopListIsDirty = true;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void ExecuteRemove()
		{
			try
			{
				GarrisonSettings currentTownSettings = Main.GarrisonBehavior.GetCurrentTownSettings();
				if (currentTownSettings == null)
				{
					return;
				}
				if (!currentTownSettings.Template.Contains(CurrentTroop.Character))
				{
					_trainingDatasource.TroopListIsDirty = true;
					return;
				}
				currentTownSettings.Template.AddOrUpdateCharacter(CurrentTroop.Character, currentTownSettings.Template.GetAmountForTemplateTroop(CurrentTroop.Character) - 1);
				if (currentTownSettings.Template.GetAmountForTemplateTroop(CurrentTroop.Character) <= 0)
				{
					ExecuteDelete();
				}
				_trainingDatasource.TroopListIsDirty = true;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void ExecuteDelete()
		{
			try
			{
				bool flag = TrainingSettings.Instance.RemoveUpgradeTarget(Main.GarrisonBehavior.CurrentTownForSettings, CurrentTroop.Character, _trainingDatasource);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public override void RefreshValues()
		{
			base.RefreshValues();
		}
	}
}
