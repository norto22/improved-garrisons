using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements;
using ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements.Elements;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.TwoDimension;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus.ManagementUtils
{
	public class BuildingVM : ViewModel
	{
		private HintViewModel _hint;

		private string _iconPath;

		private string _tier;

		private bool _tierIsVisible;

		private bool _isDefault;

		private bool _isDaily;

		private string _name;

		private string _visualCode;

		private List<string> romanNumerals = new List<string>
		{
			"M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX",
			"V", "IV", "I"
		};

		private List<int> numerals = new List<int>
		{
			1000, 900, 500, 400, 100, 90, 50, 40, 10, 9,
			5, 4, 1
		};

		private Building currentBuilding;

		private readonly TextObject L1BonusText = new TextObject("{=PJZ8QYgA}L-I : {BONUS}");

		private readonly TextObject L2BonusText = new TextObject("{=9i0wnjJK}L-II : {BONUS}");

		private readonly TextObject L3BonusText = new TextObject("{=pRP2sOWP}L-III : {BONUS}");

		private static SpriteCategory _spriteCategory;

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

		public string Tier
		{
			get
			{
				return _tier;
			}
			set
			{
				if (value != _tier)
				{
					_tier = value;
					OnPropertyChangedWithValue(value, "Tier");
				}
			}
		}

		public bool TierIsVisible
		{
			get
			{
				return _tierIsVisible;
			}
			set
			{
				if (value != _tierIsVisible)
				{
					_tierIsVisible = value;
					OnPropertyChangedWithValue(value, "TierIsVisible");
				}
			}
		}

		public bool IsDefault
		{
			get
			{
				return _isDefault;
			}
			set
			{
				if (value != _isDefault)
				{
					_isDefault = value;
					OnPropertyChangedWithValue(value, "IsDefault");
				}
			}
		}

		public bool IsDaily
		{
			get
			{
				return _isDaily;
			}
			set
			{
				if (value != _isDaily)
				{
					_isDaily = value;
					OnPropertyChangedWithValue(value, "IsDaily");
				}
			}
		}

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

		public string VisualCode
		{
			get
			{
				return _visualCode;
			}
			set
			{
				if (value != _visualCode)
				{
					_visualCode = value;
					OnPropertyChangedWithValue(value, "VisualCode");
				}
			}
		}

		public BuildingVM(Building building)
		{
			if (building == null)
			{
				return;
			}
			currentBuilding = building;
			VisualCode = building.BuildingType.StringId.ToLower();
			Name = building.Name.ToString();
			Tier = ToRomanNumeral(building.CurrentLevel);
			if (building.CurrentLevel > 0)
			{
				TierIsVisible = true;
			}
			bool isDailyProject = building.BuildingType.IsDailyProject;
			if (isDailyProject)
			{
				UpdateDefault();
			}
			string text = "";
			if (IsCurrentProject())
			{
				text += new TextObject("{=ui_buildingui_currentproject}>> Current location project <<").ToString();
			}
			text = text + building.Explanation?.ToString() + "\n \n" + new TextObject("{=ui_buildingui_effects}Effects:").ToString() + "\n";
			if (!isDailyProject)
			{
				for (int i = 1; i <= 3; i++)
				{
					string bonusText = GetBonusText(currentBuilding, i);
					text += ((bonusText != "") ? ("> " + GetBonusText(currentBuilding, i) + "\n") : "");
				}
			}
			else if (Main.GarrisonBehavior.CurrentTownForSettings != null)
			{
				string text2 = currentBuilding.BuildingType.GetExplanationAtLevel(building.CurrentLevel).ToString();
				text = text + "> " + text2 + "\n \n" + new TextObject("{=ui_buildingui_effectshint}Note: this only works if there is no current project.").ToString();
			}
			Hint = new HintViewModel(new TextObject(text));
		}

		public static void EnsureSpritesLoaded()
		{
			LoadBuildingSprites();
		}

		private static void LoadBuildingSprites()
		{
			try
			{
				_spriteCategory = UIResourceManager.LoadSpriteCategory("ui_town_management");
			}
			catch (Exception)
			{
			}
		}

		private void UpdateDefault()
		{
			if (Main.GarrisonBehavior.CurrentTownForSettings != null && Main.GarrisonBehavior.CurrentTownForSettings.CurrentBuilding != null)
			{
				bool isDefault = Main.GarrisonBehavior.CurrentTownForSettings.CurrentBuilding.Name.ToString().Equals(currentBuilding.Name.ToString());
				IsDefault = isDefault;
			}
		}

		private string ToRomanNumeral(int number)
		{
			switch (number)
			{
			case 0:
				return "";
			case 1:
				return "SPGeneral\\TownManagement\\level_1";
			case 2:
				return "SPGeneral\\TownManagement\\level_2";
			case 3:
				return "SPGeneral\\TownManagement\\level_3";
			default:
				return "";
			}
		}

		public void ChangeToDefault()
		{
			if (Main.GarrisonBehavior.CurrentTownForSettings != null)
			{
				Town currentTownForSettings = Main.GarrisonBehavior.CurrentTownForSettings;
				BuildingHelper.ChangeDefaultBuilding(currentBuilding, currentTownForSettings);
				IsDefault = true;
			}
		}

		public void OpenContextMenu()
		{
			bool flag = currentBuilding.CurrentLevel >= 3;
			if (Main.GarrisonBehavior.CurrentTownForSettings == null || flag)
			{
				return;
			}
			Town currentTown = Main.GarrisonBehavior.CurrentTownForSettings;
			MBBindingList<CascadeMenuElementVM> mBBindingList = new MBBindingList<CascadeMenuElementVM>();
			bool flag2 = IsCurrentProject();
			if (!flag2)
			{
				CascadeMenuBaseButtonVM item = new CascadeMenuBaseButtonVM(new TextObject("{=ui_buildingui_setascurrent}Set as current project").ToString(), delegate
				{
					Queue<Building> buildingsInProgress = currentTown.BuildingsInProgress;
					if (buildingsInProgress.Contains(currentBuilding))
					{
						RemoveFromBuildingQueue();
					}
					ChangeCurrentBuilding(currentBuilding.BuildingType, currentTown);
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ui_buildingui_currenthint1}The current location project in").ToString() + ModuleStrings._space + Main.GarrisonBehavior.CurrentTownForSettings.Settlement.Name.ToString() + ModuleStrings._space + new TextObject("{=ui_buildingui_currenthint2}has been set to").ToString() + ModuleStrings._space + currentBuilding.Name, Color.FromUint(ModuleColors.green)));
					UIManager.Instance.CloseCascadeMenu();
				});
				mBBindingList.Add(item);
			}
			else
			{
				CascadeMenuBaseButtonVM item2 = new CascadeMenuBaseButtonVM(new TextObject("{=ui_buildingui_boostproject}Boost current project").ToString(), delegate
				{
					PromptReserveWindow();
					UIManager.Instance.CloseCascadeMenu();
				});
				mBBindingList.Add(item2);
				CascadeMenuBaseButtonVM item3 = new CascadeMenuBaseButtonVM(new TextObject("{=ui_buildingui_deselectproject}Deselect current project").ToString(), delegate
				{
					List<Building> list = currentTown.BuildingsInProgress.ToList();
					if (list.Count > 0)
					{
						list.RemoveAt(0);
						currentTown.BuildingsInProgress = new Queue<Building>(list);
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ui_buildingui_removequeue1}The project").ToString() + ModuleStrings._space + currentBuilding.Name?.ToString() + ModuleStrings._space + new TextObject("{=ui_buildingui_removequeue2}is no longer the current project of").ToString() + ModuleStrings._space + currentTown.Settlement.Name.ToString(), Color.FromUint(ModuleColors.green)));
						UIManager.Instance.CloseCascadeMenu();
					}
				});
				mBBindingList.Add(item3);
			}
			if ((from building in currentTown.BuildingsInProgress.ToList()
				where building.Name.ToString().Equals(currentBuilding.Name.ToString())
				select building).Count() <= 0)
			{
				CascadeMenuBaseButtonVM item4 = new CascadeMenuBaseButtonVM(new TextObject("{=ui_buildingui_addqueue1}Add to project queue").ToString(), delegate
				{
					Queue<Building> buildingsInProgress = currentTown.BuildingsInProgress;
					if (!buildingsInProgress.Contains(currentBuilding))
					{
						buildingsInProgress.Enqueue(currentBuilding);
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ui_buildingui_addqueue2}The project").ToString() + ModuleStrings._space + currentBuilding.Name?.ToString() + ModuleStrings._space + new TextObject("{=ui_buildingui_addqueue3}has been added to the project queue of").ToString() + ModuleStrings._space + currentTown.Settlement.Name.ToString(), Color.FromUint(ModuleColors.green)));
						UIManager.Instance.CloseCascadeMenu();
					}
					currentTown.BuildingsInProgress = buildingsInProgress;
				});
				mBBindingList.Add(item4);
			}
			else if (!flag2)
			{
				CascadeMenuBaseButtonVM item5 = new CascadeMenuBaseButtonVM(new TextObject("{=ui_buildingui_removequeue3}Remove from queue").ToString(), delegate
				{
					RemoveFromBuildingQueue();
				});
				mBBindingList.Add(item5);
			}
			UIManager.Instance.CreateCascadeMenuOnMousePointer(new TextObject("{=ui_buildingui_action}Project action").ToString(), mBBindingList);
		}

		public static void ChangeCurrentBuilding(BuildingType buildingType, Town town)
		{
			Building building = null;
			foreach (Building building2 in town.Buildings)
			{
				if (building2.BuildingType == buildingType)
				{
					building = building2;
					break;
				}
			}
			if (building == null)
			{
				return;
			}
			List<Building> list = new List<Building>();
			list.Add(building);
			foreach (Building item in town.BuildingsInProgress)
			{
				if (item != building && !item.BuildingType.IsDailyProject)
				{
					list.Add(item);
				}
			}
			BuildingHelper.ChangeCurrentBuildingQueue(list, town);
		}

		private void RemoveFromBuildingQueue()
		{
			if (Main.GarrisonBehavior.CurrentTownForSettings != null)
			{
				Town currentTownForSettings = Main.GarrisonBehavior.CurrentTownForSettings;
				List<Building> list = currentTownForSettings.BuildingsInProgress.ToList();
				int num = list.FindIndex((Building building) => building.Name.ToString().Equals(currentBuilding.Name.ToString()));
				if (num >= 0)
				{
					list.RemoveAt(num);
					currentTownForSettings.BuildingsInProgress = new Queue<Building>(list);
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ui_buildingui_addqueue2}The project").ToString() + ModuleStrings._space + currentBuilding.Name?.ToString() + ModuleStrings._space + new TextObject("{=ui_buildingui_removequeue4}has been removed from the project queue of").ToString() + ModuleStrings._space + currentTownForSettings.Settlement.Name.ToString(), Color.FromUint(ModuleColors.green)));
					UIManager.Instance.CloseCascadeMenu();
				}
			}
		}

		private void PromptReserveWindow()
		{
			TextObject textObject = GameTexts.FindText("str_town_management_reserve_explanation");
			textObject.SetTextVariable("BOOST", Campaign.Current.Models.BuildingConstructionModel.GetBoostAmount(Main.GarrisonBehavior.CurrentTownForSettings));
			textObject.SetTextVariable("COST", Campaign.Current.Models.BuildingConstructionModel.GetBoostCost(Main.GarrisonBehavior.CurrentTownForSettings));
			InformationManager.ShowTextInquiry(new TextInquiryData(new TextObject("{=ui_buildingui_addreserve}Add reserve to boost project").ToString(), string.Format(textObject.ToString()), isAffirmativeOptionShown: true, isNegativeOptionShown: true, new TextObject("{=menu_ok}Okay").ToString(), new TextObject("{=menu_cancel}Cancel").ToString(), delegate(string amount)
			{
				if (int.TryParse(amount, out var input))
				{
					Main.ExecuteActionOnNextTick(delegate
					{
						int boostBuildingProcess = Main.GarrisonBehavior.CurrentTownForSettings.BoostBuildingProcess;
						BuildingHelper.BoostBuildingProcessWithGold(boostBuildingProcess + input, Main.GarrisonBehavior.CurrentTownForSettings);
					});
				}
			}, delegate
			{
				InformationManager.HideInquiry();
			}, shouldInputBeObfuscated: false, delegate(string x)
			{
				int num = ((Hero.MainHero?.Clan != null) ? Hero.MainHero.Clan.Gold : int.MaxValue);
				int result;
				return (int.TryParse(x, out result) && result <= num && result > 0) ? new Tuple<bool, string>(item1: true, "") : new Tuple<bool, string>(item1: false, new TextObject("{=ui_buildingui_addreserve2}You don't have enough denars.").ToString());
			}));
		}

		private bool IsCurrentProject()
		{
			Town currentTownForSettings = Main.GarrisonBehavior.CurrentTownForSettings;
			if (currentTownForSettings == null)
			{
				return false;
			}
			List<Building> list = currentTownForSettings.BuildingsInProgress.ToList();
			if (list.Count > 0)
			{
				Building building = list.First();
				return building.Name.ToString().Equals(currentBuilding.Name.ToString());
			}
			return false;
		}

		private TextObject GetBonusExplanationOfLevel(int level)
		{
			if (level >= 0 && level <= 3)
			{
				return currentBuilding.BuildingType.GetExplanationAtLevel(level);
			}
			return TextObject.GetEmpty();
		}

		private string GetBonusText(Building building, int level)
		{
			if (level == 0 || level == 4)
			{
				return "";
			}
			string text;
			switch (level)
			{
			default:
				text = L3BonusText.ToString();
				break;
			case 2:
				text = L2BonusText.ToString();
				break;
			case 1:
				text = L1BonusText.ToString();
				break;
			}
			string text2 = text;
			TextObject bonusExplanationOfLevel = GetBonusExplanationOfLevel(level);
			return text2 + bonusExplanationOfLevel.ToString();
		}

		public override void RefreshValues()
		{
			base.RefreshValues();
			UpdateDefault();
		}
	}
}
