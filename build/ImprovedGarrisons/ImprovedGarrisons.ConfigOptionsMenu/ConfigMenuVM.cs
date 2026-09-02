using System;
using System.Collections.Generic;
using System.Reflection;
using ImprovedGarrisons.ConfigOptionsMenu.Options;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.SaveSystem;
using ImprovedGarrisons.SaveSystem.Configuration;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using ImprovedGarrisons.Utils;
using TaleWorlds.Core;
using TaleWorlds.Engine.Options;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ConfigOptionsMenu
{
	public class ConfigMenuVM : ViewModel
	{
		public enum OptionsDataType
		{
			None = -1,
			BooleanOption = 0,
			NumericOption = 1,
			MultipleSelectionOption = 3,
			InputOption = 4,
			ActionOption = 5,
			Title = 6
		}

		private List<ImprovedGarrisonCategoryVM> _allCategories = new List<ImprovedGarrisonCategoryVM>();

		private readonly ImprovedGarrisonCategoryVM _cheatOptionCategory;

		private readonly ImprovedGarrisonCategoryVM _baseSettingsCategory;

		private readonly ImprovedGarrisonCategoryVM _npcOptionCategory;

		private readonly ImprovedGarrisonCategoryVM _defaultOptionCategory;

		public string Title { get; } = "Improved Garrisons Config";

		public string CloseText { get; } = new TextObject("{=menu_close}Close").ToString();

		public string SaveText { get; } = new TextObject("{=menu_save}Save").ToString();

		public bool OldGameStateManagerDisabledStatus { get; private set; }

		internal bool ResetMode { get; set; }

		internal bool PromptIsOpen { get; set; }

		internal bool IsFinished { get; set; } = false;

		public ImprovedGarrisonCategoryVM CheatOptions => _cheatOptionCategory;

		public ImprovedGarrisonCategoryVM BaseOptions => _baseSettingsCategory;

		public ImprovedGarrisonCategoryVM NpcOptions => _npcOptionCategory;

		public ImprovedGarrisonCategoryVM DefaultOptions => _defaultOptionCategory;

		private IEnumerable<IOptionData> CheatOptionsList
		{
			get
			{
				string name = new TextObject("{=config_category_dummyname}Category title").ToString();
				yield return new TitleText(name);
				string name2 = new TextObject("{=config_category_cheats_garrisonwage_name}Garrison wage multiplier").ToString();
				string description = new TextObject("{=config_category_cheats_garrisonwage_description}Lower the garrison wage. Set the value to 1 for normal costs and to 0 for no costs.").ToString();
				string extraDescription = new TextObject("{=config_category_cheats_garrisonwage_extradescription}By selecting a number below 1, a bonus equal to your garrison costs will be added to your daily finances.").ToString();
				float value = ConfigManager.Instance.Config.GarrisonWageMultiplier;
				Action<float> action = delegate(float x)
				{
					ConfigManager.Instance.Config.GarrisonWageMultiplier = x;
				};
				yield return new NumericOption(name2, description, extraDescription, null, isDiscrete: false, 0f, 1f, value, action);
				string name3 = new TextObject("{=config_category_cheats_trainingcost_name}Improved Garrison training multiplier").ToString();
				string description2 = new TextObject("{=config_category_cheats_trainingcost_description}Lower the costs of the Improved Garrison training of garrisoned troops. Set this to 1 for normal costs and to 0 for no costs.").ToString();
				string extraDescription2 = new TextObject("{=config_category_cheats_trainingcost_extradescription}This option affects the upgrade costs of your garrisoned troops. Use this if you want to disable the upgrade costs.").ToString();
				float value2 = ConfigManager.Instance.Config.GarrisonTrainingMultiplier;
				Action<float> action2 = delegate(float x)
				{
					ConfigManager.Instance.Config.GarrisonTrainingMultiplier = x;
				};
				yield return new NumericOption(name3, description2, extraDescription2, null, isDiscrete: false, 0f, 1f, value2, action2);
				string name4 = new TextObject("{=config_category_cheats_garrisonguardwage_name}Garrison guard parties wage multiplier").ToString();
				string description3 = new TextObject("{=config_category_cheats_garrisoguardnwage_description}Lower the wage of guard parties. Set this to 1 for normal costs and to 0 for no costs.").ToString();
				string extraDescription3 = new TextObject("").ToString();
				float value3 = ConfigManager.Instance.Config.GarrisonGuardsWageMultiplier;
				Action<float> action3 = delegate(float x)
				{
					ConfigManager.Instance.Config.GarrisonGuardsWageMultiplier = x;
				};
				yield return new NumericOption(name4, description3, extraDescription3, null, isDiscrete: false, 0f, 1f, value3, action3);
				string name5 = new TextObject("{=config_category_cheats_partyspeed_name}custom speed of guard parties, recruiting parties and transfer parties").ToString();
				MBTextManager.SetTextVariable("CustomGuardAndTransferPartySpeed", name5);
				string description4 = new TextObject("{=config_category_cheats_partyspeed_description}Set a custom movement speed for guard, recruiting and transfer parties.").ToString();
				string extraDescription4 = new TextObject("").ToString();
				string requirements = new TextObject("{=config_category_cheats_partyspeed_requirements}[{LoadCustomPartySpeedModel}] has to be enabled.").ToString();
				float value4 = ConfigManager.Instance.Config.CustomGuardAndTransferPartySpeed;
				Action<float> action4 = delegate(float x)
				{
					ConfigManager.Instance.Config.CustomGuardAndTransferPartySpeed = x;
				};
				yield return new NumericOption(name5, description4, extraDescription4, requirements, isDiscrete: false, 0f, 15f, value4, action4);
				string name6 = new TextObject("{=config_category_cheats_garrisonsize_name}custom garrison size multiplier").ToString();
				MBTextManager.SetTextVariable("CustomGarrisonSizeMultiplier", name6);
				string description5 = new TextObject("{=config_category_cheats_garrisonsize_description}Set a garrison size multiplier to increase the size of your garrisons. The automatic recruitment will not exceed the overall garrison size!").ToString();
				string requirements2 = new TextObject("{=config_category_cheats_garrisonsize_requirements}[{LoadCustomPartySizeModel}] has to be enabled.").ToString();
				float value5 = ConfigManager.Instance.Config.CustomGarrisonSizeMultiplier;
				Action<float> action5 = delegate(float x)
				{
					ConfigManager.Instance.Config.CustomGarrisonSizeMultiplier = x;
				};
				yield return new NumericOption(name6, description5, null, requirements2, isDiscrete: false, 1f, 10f, value5, action5);
				string name7 = new TextObject("{=config_category_cheats_partysize_name}custom player party size multiplier").ToString();
				string description6 = new TextObject("{=config_category_cheats_partysize_description}Set a party size multiplier to increase the maximum size of your own party.").ToString();
				string requirements3 = new TextObject("{=config_category_cheats_partysize_requirements}[{LoadCustomPartySizeModel}] has to be enabled.").ToString();
				float value6 = ConfigManager.Instance.Config.MainPartySizeMultiplier;
				Action<float> action6 = delegate(float x)
				{
					ConfigManager.Instance.Config.MainPartySizeMultiplier = x;
				};
				yield return new NumericOption(name7, description6, null, requirements3, isDiscrete: false, 1f, 10f, value6, action6);
				string name8 = new TextObject("{=config_category_cheats_clanpartysize_name}custom player clan party size multiplier").ToString();
				string description7 = new TextObject("{=config_category_cheats_clanpartysize_description}Set a party size multiplier to increase the maximum size of every party that belongs to your clan.").ToString();
				string requirements4 = new TextObject("{=config_category_cheats_clanpartysize_requirements}[{LoadCustomPartySizeModel}] has to be enabled.").ToString();
				float value7 = ConfigManager.Instance.Config.PlayerClanPartySizeMultiplier;
				Action<float> action7 = delegate(float x)
				{
					ConfigManager.Instance.Config.PlayerClanPartySizeMultiplier = x;
				};
				yield return new NumericOption(name8, description7, null, requirements4, isDiscrete: false, 1f, 10f, value7, action7);
				string name9 = new TextObject("{=config_category_cheats_aiclanpartysize_name}custom AI clan party size multiplier").ToString();
				string description8 = new TextObject("{=config_category_cheats_aiclanpartysize_description}Set a party size multiplier to increase the maximum size of every AI parties.").ToString();
				string requirements5 = new TextObject("{=config_category_cheats_aiclanpartysize_requirements}[{LoadCustomPartySizeModel}] has to be enabled.").ToString();
				float value8 = ConfigManager.Instance.Config.AIClanPartySizeMultiplier;
				Action<float> action8 = delegate(float x)
				{
					ConfigManager.Instance.Config.AIClanPartySizeMultiplier = x;
				};
				yield return new NumericOption(name9, description8, null, requirements5, isDiscrete: false, 1f, 10f, value8, action8);
				string name10 = new TextObject("{=config_category_base_fromvillages_name}Spawn new troops directly into the garrison").ToString();
				MBTextManager.SetTextVariable("RecruitsAreRecruitedFromVillages", name10);
				string description9 = new TextObject("{=config_category_base_fromvillages_description}With this option enabled the Improved Garrison will no longer have to wait for the troops to be generated by the surrounding villages. New troops will be spawned in the garrison \"out of thin air\" every day.").ToString();
				new TextObject("{=config_category_base_fromvillages_extradescription}If automatic recruitment and recruitment from villages are both enabled, this will look for recruitable units in the settlement and in its bound villages. Be aware that the number of available troops is affected by the relation you have with notables. Once the minimum amount of recruits is reached (4 by default), a party will be created and sent on its way to the garrison. The recruited troops will be removed from the village pool, so it will take some time for the village to generate additional troops.").ToString();
				float value9 = (ConfigManager.Instance.Config.RecruitsAreRecruitedFromVillages ? 0f : 1f);
				Action<bool> action9 = delegate(bool x)
				{
					ConfigManager.Instance.Config.RecruitsAreRecruitedFromVillages = !x;
				};
				yield return new ToggleOption(name10, description9, null, null, value9, action9);
				string name11 = new TextObject("{=config_category_base_spawnamount_name}Amount of troops to spawn").ToString();
				MBTextManager.SetTextVariable("AmountOfUnitsToSpawn", name11);
				string description10 = new TextObject("{=config_category_base_spawnamount_description}Set the number of troops that are spawned in garrisons if they are not recruited from nearby villages and settlements.").ToString();
				string requirements6 = new TextObject("{=config_category_base_spawnamount_requirements}[{RecruitsAreRecruitedFromVillages}] has to be enabled.").ToString();
				float value10 = ConfigManager.Instance.Config.AmountOfUnitsToSpawn;
				Action<float> action10 = delegate(float x)
				{
					ConfigManager.Instance.Config.AmountOfUnitsToSpawn = (int)x;
				};
				yield return new NumericOption(name11, description10, null, requirements6, isDiscrete: true, 0f, 300f, value10, action10);
				string name12 = new TextObject("{=config_category_cheats_onlynoble_name}Spawn noble troops in garrison").ToString();
				MBTextManager.SetTextVariable("SpawnOnlyNobleTroops", name12);
				string description11 = new TextObject("{=config_category_cheats_onlynoble_description}With this option enabled noble troops are spawned in the garrisons \"out of thin air\". The amount can be set with [{AmountOfUnitsToSpawn}].").ToString();
				string requirements7 = new TextObject("{=config_category_cheats_onlynoble_requirements}[{RecruitsAreRecruitedFromVillages}] has to be enabled!").ToString();
				float value11 = (ConfigManager.Instance.Config.SpawnOnlyNobleTroops ? 1f : 0f);
				Action<bool> action11 = delegate(bool x)
				{
					ConfigManager.Instance.Config.SpawnOnlyNobleTroops = x;
				};
				yield return new ToggleOption(name12, description11, requirements7, null, value11, action11);
				string name13 = new TextObject("{=config_category_base_loadfood_name}Load food gathering module").ToString();
				MBTextManager.SetTextVariable("LoadFoodGatheringModule", name13);
				string description12 = new TextObject("{=config_category_base_loadfood_description}Loads the food gathering module of Improved Garrisons. This enables the option to add a food bonus to castles and settlements.").ToString();
				string requirements8 = new TextObject("{=config_category_base_loadfood_requirements}The game has to be restarted.").ToString();
				float value12 = (ConfigManager.Instance.Config.LoadFoodGatheringModule ? 1f : 0f);
				Action<bool> action12 = delegate(bool x)
				{
					ConfigManager.Instance.Config.LoadFoodGatheringModule = x;
				};
				yield return new ToggleOption(name13, description12, null, requirements8, value12, action12);
				string name14 = new TextObject("{=config_category_cheats_foodbonusenable_name}Enable food bonus").ToString();
				string description13 = new TextObject("{=config_category_cheats_foodbonusenable_description}Activate the food bonus for your castles and settlements.").ToString();
				string requirements9 = new TextObject("{=config_category_cheats_foodbonusenable_requirements}[{LoadFoodGatheringModule}] has to be enabled.").ToString();
				float value13 = (ConfigManager.Instance.Config.LoadFoodGatheringModule ? 1f : 0f);
				Action<bool> action13 = delegate(bool x)
				{
					ConfigManager.Instance.Config.EnablePlayerFoodBonus = x;
				};
				yield return new ToggleOption(name14, description13, null, requirements9, value13, action13);
				string name15 = new TextObject("{=config_category_cheats_garrisondonteat_name}Enable the garrisons to not eat food").ToString();
				MBTextManager.SetTextVariable("DisableGarrisonNeedsFood", name15);
				string description14 = new TextObject("{=config_category_cheats_garrisondonteat_description}Add a food bonus to settlements equalling the amount of food consumed by the garrison. This is a workaround aimed at cancelling food consumption for garrisons.").ToString();
				string requirements10 = new TextObject("{=config_category_cheats_garrisondonteat_requirements}[{LoadFoodGatheringModule}] has to be enabled.").ToString();
				float value14 = (ConfigManager.Instance.Config.DisableGarrisonNeedsFood ? 1f : 0f);
				Action<bool> action14 = delegate(bool x)
				{
					ConfigManager.Instance.Config.DisableGarrisonNeedsFood = x;
				};
				yield return new ToggleOption(name15, description14, null, requirements10, value14, action14);
				string name16 = new TextObject("{=config_category_base_foodamount_name}Daily food bonus amount").ToString();
				MBTextManager.SetTextVariable("DailyFoodGatheringAmount", name16);
				string description15 = new TextObject("{=config_category_base_foodamount_description}Set the amount of daily food bonus each of the player's castles and settlements get each day.").ToString();
				string extraDescription5 = new TextObject("").ToString();
				string requirements11 = new TextObject("{=config_category_base_foodamount_requirements}[{LoadFoodGatheringModule}] has to be enabled.").ToString();
				float value15 = ConfigManager.Instance.Config.DailyFoodGatheringAmount;
				Action<float> action15 = delegate(float x)
				{
					ConfigManager.Instance.Config.DailyFoodGatheringAmount = (int)x;
				};
				yield return new NumericOption(name16, description15, extraDescription5, requirements11, isDiscrete: true, 0f, 100f, value15, action15);
			}
		}

		private IEnumerable<IOptionData> DefaultOptionsList
		{
			get
			{
				string name = new TextObject("{=config_category_default_recruitment_name}World recruitment enabled by default").ToString();
				MBTextManager.SetTextVariable("DefaultPlayerEnableRecruitment", name);
				string description = new TextObject("{=config_category_default_recruitment_description}Enables world recruitment for player garrisons by default.").ToString();
				float value = (ConfigManager.Instance.Config.DefaultPlayerEnableRecruitment ? 1f : 0f);
				Action<bool> action = delegate(bool x)
				{
					ConfigManager.Instance.Config.DefaultPlayerEnableRecruitment = x;
				};
				yield return new ToggleOption(name, description, null, null, value, action);
				string name2 = new TextObject("{=config_category_default_recruitthreshold_name}Maximum recruitment threshold default amount").ToString();
				MBTextManager.SetTextVariable("DefaultPlayerMaxRecruitThreshold", name2);
				string description2 = new TextObject("{=config_category_default_recruitthreshold_description}Set the default maximum number of garrison units until recruitment stops.").ToString();
				string requirements = new TextObject("").ToString();
				float value2 = ConfigManager.Instance.Config.DefaultPlayerMaxRecruitThreshold;
				Action<float> action2 = delegate(float x)
				{
					ConfigManager.Instance.Config.DefaultPlayerMaxRecruitThreshold = (int)x;
				};
				yield return new NumericOption(name2, description2, null, requirements, isDiscrete: true, 0f, 2500f, value2, action2);
				string name3 = new TextObject("{=config_category_default_onlyelite_name}Only recruit elites enabled by default").ToString();
				MBTextManager.SetTextVariable("", name3);
				string description3 = new TextObject("{=config_category_default_onlyelite_description}Enables the only recruit elite units option by default.").ToString();
				string requirements2 = new TextObject("").ToString();
				float value3 = (ConfigManager.Instance.Config.DefaultOnlyRecruitElites ? 1f : 0f);
				Action<bool> action3 = delegate(bool x)
				{
					ConfigManager.Instance.Config.DefaultOnlyRecruitElites = x;
				};
				yield return new ToggleOption(name3, description3, null, requirements2, value3, action3);
				string name4 = new TextObject("{=config_category_default_prisonerrecruit_name}Prisoner recruitment enabled by default").ToString();
				MBTextManager.SetTextVariable("DefaultPlayerEnablePrisonerRecruitment", name4);
				string description4 = new TextObject("{=config_category_default_prisonerrecruit_description}Enables prisoner recruitment for player garrisons by default.").ToString();
				float value4 = (ConfigManager.Instance.Config.DefaultPlayerEnablePrisonerRecruitment ? 1f : 0f);
				Action<bool> action4 = delegate(bool x)
				{
					ConfigManager.Instance.Config.DefaultPlayerEnablePrisonerRecruitment = x;
				};
				yield return new ToggleOption(name4, description4, null, null, value4, action4);
				string name5 = new TextObject("{=config_category_default_prisonerthreshold_name}Prisoner recruitment above threshold enabled by default").ToString();
				MBTextManager.SetTextVariable("DefaultAllowPrisonerRecruitAboveThreshold", name5);
				string description5 = new TextObject("{=config_category_default_prisonerthreshold_description}Enables prisoner recruitment above threshold for player garrisons by default.").ToString();
				float value5 = (ConfigManager.Instance.Config.DefaultAllowPrisonerRecruitAboveThreshold ? 1f : 0f);
				Action<bool> action5 = delegate(bool x)
				{
					ConfigManager.Instance.Config.DefaultAllowPrisonerRecruitAboveThreshold = x;
				};
				yield return new ToggleOption(name5, description5, null, null, value5, action5);
				string name6 = new TextObject("{=config_category_default_training_name}Training enabled by default").ToString();
				MBTextManager.SetTextVariable("DefaultPlayerEnableTraining", name6);
				string description6 = new TextObject("{=config_category_default_training_description}Enables training for player garrisons by default.").ToString();
				string extraDescription = new TextObject("{=config_category_default_training_requirements}Each day, your garrisoned units get a set (but customizable) amount of experience. Once they are able to upgrade, two things can happen. If you did choose their upgrade path by selecting a specific upgrade target, Improved Garrison will upgrade them towards this target. If no target has been set, the path will be choosen randomly. If you want to limit your costs, you can restrict their maximum upgrade tier (note that this is overwritten by your custom upgrade targets).").ToString();
				float value6 = (ConfigManager.Instance.Config.DefaultPlayerEnableTraining ? 1f : 0f);
				Action<bool> action6 = delegate(bool x)
				{
					ConfigManager.Instance.Config.DefaultPlayerEnableTraining = x;
				};
				yield return new ToggleOption(name6, description6, extraDescription, null, value6, action6);
				string name7 = new TextObject("{=config_category_default_maximumtier_name}Maximum training tier default setting").ToString();
				string description7 = new TextObject("{=config_category_default_maximumtier_description}The default maximum tier garrisoned troops are trained to.").ToString();
				string requirements3 = new TextObject("").ToString();
				float value7 = ConfigManager.Instance.Config.DefaultPlayerMaxUpgradeTier;
				Action<float> action7 = delegate(float x)
				{
					ConfigManager.Instance.Config.DefaultPlayerMaxUpgradeTier = (int)x;
				};
				yield return new NumericOption(name7, description7, null, requirements3, isDiscrete: true, 1f, 10f, value7, action7);
				string name8 = new TextObject("{=config_category_default_guardupgrade_name}Guards can upgrade troops by default").ToString();
				string description8 = new TextObject("{=config_category_default_guardupgrade_description}Allow player guard parties to upgrade their troops by default.").ToString();
				float value8 = (ConfigManager.Instance.Config.DefaultEnableGuardUpgrade ? 1f : 0f);
				Action<bool> action8 = delegate(bool x)
				{
					ConfigManager.Instance.Config.DefaultEnableGuardUpgrade = x;
				};
				yield return new ToggleOption(name8, description8, null, null, value8, action8);
				string name9 = new TextObject("{=config_category_default_replenish_name}Guard party replenishing enabled by default").ToString();
				MBTextManager.SetTextVariable("DefaultGuardsEnableReplenish", name9);
				string description9 = new TextObject("{=config_category_default_replenish_description}Enables replenishing and healing for guard parties by default.").ToString();
				float value9 = (ConfigManager.Instance.Config.DefaultGuardsEnableReplenish ? 1f : 0f);
				Action<bool> action9 = delegate(bool x)
				{
					ConfigManager.Instance.Config.DefaultGuardsEnableReplenish = x;
				};
				yield return new ToggleOption(name9, description9, null, null, value9, action9);
				string name10 = new TextObject("{=config_category_default_guardssell_name}Prisoner trading enabled by default for guards").ToString();
				MBTextManager.SetTextVariable("DefaultEnableGuardPrisonerSell", name10);
				string description10 = new TextObject("{=config_category_default_guardssell_description}Enables prisoner trading by default.").ToString();
				float value10 = (ConfigManager.Instance.Config.DefaultEnableGuardPrisonerSell ? 1f : 0f);
				Action<bool> action10 = delegate(bool x)
				{
					ConfigManager.Instance.Config.DefaultEnableGuardPrisonerSell = x;
				};
				yield return new ToggleOption(name10, description10, null, null, value10, action10);
				string name11 = new TextObject("{=config_category_default_guardprisonerrecruit_name}Guards can recruit prisoners by default").ToString();
				MBTextManager.SetTextVariable("DefaultEnableGuardPrisonerSell", name11);
				string description11 = new TextObject("{=config_category_default_guardprisonerrecruit_description}Allow the recruitment of prisoners by player guard parties by default.").ToString();
				float value11 = (ConfigManager.Instance.Config.DefaultEnableGuardPrisonerRecruitment ? 1f : 0f);
				Action<bool> action11 = delegate(bool x)
				{
					ConfigManager.Instance.Config.DefaultEnableGuardPrisonerRecruitment = x;
				};
				yield return new ToggleOption(name11, description11, null, null, value11, action11);
				string name12 = new TextObject("{=config_category_default_hideoutclear_name}Guards can clear hideouts by default").ToString();
				MBTextManager.SetTextVariable("DefaultEnableGuardHideoutClear", name12);
				string description12 = new TextObject("{=config_category_default_hideoutclear_description}Allow guards to clear hideouts by default.").ToString();
				float value12 = (ConfigManager.Instance.Config.DefaultEnableGuardHideoutClear ? 1f : 0f);
				Action<bool> action12 = delegate(bool x)
				{
					ConfigManager.Instance.Config.DefaultEnableGuardHideoutClear = x;
				};
				yield return new ToggleOption(name12, description12, null, null, value12, action12);
				string name13 = new TextObject("{=config_category_default_buyhorses_name}Guards can buy horses by default").ToString();
				string description13 = new TextObject("{=config_category_default_buyhorses_description}Allow guards to buy horses to gain additional movement speed by default.").ToString();
				float value13 = (ConfigManager.Instance.Config.DefaultEnableGuardBuyHorses ? 1f : 0f);
				Action<bool> action13 = delegate(bool x)
				{
					ConfigManager.Instance.Config.DefaultEnableGuardBuyHorses = x;
				};
				yield return new ToggleOption(name13, description13, null, null, value13, action13);
			}
		}

		private IEnumerable<IOptionData> BaseOptionsList
		{
			get
			{
				string name = new TextObject("{=config_category_base_minrecruits_name}Village recruits party size").ToString();
				MBTextManager.SetTextVariable("MinRecruitmentAmountFromVillages", name);
				string description = new TextObject("{=config_category_base_minrecruits_description}Set the minimum number of recruits that need to be gathered from villages before a recruit party is established and sent to the garrison..").ToString();
				float value = ConfigManager.Instance.Config.MinRecruitmentAmountFromVillages;
				Action<float> action = delegate(float x)
				{
					ConfigManager.Instance.Config.MinRecruitmentAmountFromVillages = (int)x;
				};
				yield return new NumericOption(name, description, null, null, isDiscrete: true, 1f, 20f, value, action);
				string name2 = new TextObject("{=config_category_base_dailyexp_name}Daily experience amount").ToString();
				MBTextManager.SetTextVariable("DailyEXPAmount", name2);
				string description2 = new TextObject("{=config_category_base_dailyexp_description}Set the amount of daily experience your garrisoned troops gain while training.").ToString();
				string extraDescription = new TextObject("{=config_category_base_dailyexp_extradescription}The mod gives each troop roster a set amount of experience multiplied by the number of units this roster has. For example, if you have 20 recruits and they each need 250 experience to be upgraded, and the daily experience gain is set to 50, they will get 50 * 20 = 1000 experience. Consequently, 4 units will be upgraded (1000 / 250 = 4).").ToString();
				float value2 = ConfigManager.Instance.Config.DailyEXPAmount;
				Action<float> action2 = delegate(float x)
				{
					ConfigManager.Instance.Config.DailyEXPAmount = (int)x;
				};
				yield return new NumericOption(name2, description2, extraDescription, null, isDiscrete: true, 0f, 500f, value2, action2);
				string name3 = new TextObject("{=config_category_base_prisonerconformity_name}Daily prisoner conformity amount").ToString();
				MBTextManager.SetTextVariable("DailyPrisonerConformityAmount", name3);
				string description3 = new TextObject("{=config_category_base_prisonerconformity_description}Set the amount of daily conformity each prisoner get.").ToString();
				string extraDescription2 = new TextObject("{=config_category_base_prisonerconformity_extradescription}Each prisoner has its own conformity value that needs to be reached in order to be recruitable. With this setting, you can set the daily amount of conformity they get.").ToString();
				float value3 = ConfigManager.Instance.Config.DailyPrisonerConformityAmount;
				Action<float> action3 = delegate(float x)
				{
					ConfigManager.Instance.Config.DailyPrisonerConformityAmount = (int)x;
				};
				yield return new NumericOption(name3, description3, extraDescription2, null, isDiscrete: true, 0f, 100f, value3, action3);
				string name4 = new TextObject("{=config_category_base_loadsize_name}Load custom size module").ToString();
				MBTextManager.SetTextVariable("LoadCustomPartySizeModel", name4);
				string description4 = new TextObject("{=config_category_base_loadsize_description}Loads the custom size module of Improved Garrisons. This enables you to set the maximum size of your garrison and guard parties.").ToString();
				string requirements = new TextObject("{=config_category_base_loadsize_requirements}The game has to be restarted.").ToString();
				float value4 = (ConfigManager.Instance.Config.LoadCustomPartySizeModel ? 1f : 0f);
				Action<bool> action4 = delegate(bool x)
				{
					ConfigManager.Instance.Config.LoadCustomPartySizeModel = x;
				};
				yield return new ToggleOption(name4, description4, description4, requirements, value4, action4);
				string name5 = new TextObject("{=config_category_base_partysize_name}Garrison guard and transfer party size").ToString();
				MBTextManager.SetTextVariable("CustomTransferAndGuardPartySize", name5);
				string description5 = new TextObject("{=config_category_base_partysize_description}Set a custom size for guard and transfer parties.").ToString();
				string extraDescription3 = new TextObject("{=config_category_base_partysize_extradescription}This is used for the base speed and morale calculations. If the size of your transfer party, recruiter party or guard party goes beyond this value, it will get slower.").ToString();
				string requirements2 = new TextObject("{=config_category_base_partysize_requirements}[{LoadCustomPartySizeModel}] has to be enabled.").ToString();
				float value5 = ConfigManager.Instance.Config.CustomTransferAndGuardPartySize;
				Action<float> action5 = delegate(float x)
				{
					ConfigManager.Instance.Config.CustomTransferAndGuardPartySize = (int)x;
				};
				yield return new NumericOption(name5, description5, extraDescription3, requirements2, isDiscrete: true, 1f, 650f, value5, action5);
				string name6 = new TextObject("{=config_category_base_loadspeed_name}Load custom speed module").ToString();
				MBTextManager.SetTextVariable("LoadCustomPartySpeedModel", name6);
				string description6 = new TextObject("{=config_category_base_loadspeed_description}Loads the custom speed module of Improved Garrisons. This enables you to manually set the speed for your guard party, recruiter party and transfer party. Use this setting if you experience compatibility issues with the custom size module.").ToString();
				string requirements3 = new TextObject("{=config_category_base_loadspeed_requirements}The game has to be restarted.").ToString();
				float value6 = (ConfigManager.Instance.Config.LoadCustomPartySpeedModel ? 1f : 0f);
				Action<bool> action6 = delegate(bool x)
				{
					ConfigManager.Instance.Config.LoadCustomPartySpeedModel = x;
				};
				yield return new ToggleOption(name6, description6, description6, requirements3, value6, action6);
				string name7 = new TextObject("{=config_Category_base_guardreplenishperc_name}Guards replenishment percentage").ToString();
				MBTextManager.SetTextVariable("GuardReplenishPercentage", name7);
				string description7 = new TextObject("{=config_Category_base_guardreplenishperc_description}The percentage the current party size has to drop to in order for the guards to go back to their garrison to replenish their troops. The guards will only replenish if there are valid troops in your garrison! They only pick up units which were in their initial party, and only if these units party amount is not above the initial amount.").ToString();
				string extraDescription4 = new TextObject("{=config_Category_base_guardreplenishperc_extradescription}> The guards will try to replenish to mirror their initial composition.\n> With 0.0, the guards will never return to the garrison to replenish.\n> With 0.5, the guards will replenish at half their initial size.\n> With 1.0, the guards will replenish as soon as they lose troops, and the amount of available troops to be picked up are above the [{GuardAvailableTroopsPercentage}]").ToString();
				string requirements4 = new TextObject("{=config_Category_base_guardreplenishperc_requirements}[{GuardAvailableTroopsPercentage}] has to be reached").ToString();
				float value7 = ConfigManager.Instance.Config.GuardReplenishPercentage;
				Action<float> action7 = delegate(float x)
				{
					ConfigManager.Instance.Config.GuardReplenishPercentage = x;
				};
				yield return new NumericOption(name7, description7, extraDescription4, requirements4, isDiscrete: false, 0f, 1f, value7, action7);
				string name8 = new TextObject("{=config_Category_base_guardreplenishavail_name}Guards replenishment available troops percentage").ToString();
				MBTextManager.SetTextVariable("GuardAvailableTroopsPercentage", name8);
				string description8 = new TextObject("{=config_Category_base_guardreplenishavail_description}The number of troops in relation to the initial guard party size that needs to be available in the garrison in order for the guards to return and refill.").ToString();
				string extraDescription5 = new TextObject("{=config_Category_base_guardreplenishavail_extradescription}> With 1.0, the entire initial guard party size needs to be available in the garrison\n> With 0.5, the guards will replenish if at least half their size is available in the garrison.\n> With 0.0, the guards will replenish as soon as there are available troops to pick up, and if their party size is below the [{GuardReplenishPercentage}]").ToString();
				new TextObject("{=config_Category_base_guardreplenishavail_requirements}[{GuardReplenishPercentage}] has to be reached").ToString();
				float value8 = ConfigManager.Instance.Config.GuardAvailableTroopsPercentage;
				Action<float> action8 = delegate(float x)
				{
					ConfigManager.Instance.Config.GuardAvailableTroopsPercentage = x;
				};
				yield return new NumericOption(name8, description8, extraDescription5, null, isDiscrete: false, 0f, 1f, value8, action8);
				string name9 = new TextObject("{=config_category_base_guardheal_name}Guards heal percentage").ToString();
				MBTextManager.SetTextVariable("GuardHealPercentage", name9);
				string description9 = new TextObject("{=config_category_base_guardheal_description}The percentage of killed or wounded troops in relation to the initial party size that needs to be reached in order for the patrolling party to return to a town or castle to heal.").ToString();
				string extraDescription6 = new TextObject("{=config_category_base_guardheal_extradescription}> With 0.0, the party will never heal because they would need to be dead in order to be healed\n> With 0.5, the party will heal when its current size reaches half its initial size\n> With 1.0, the party will heal as soon as its size gets lower than its initial party size").ToString();
				float value9 = ConfigManager.Instance.Config.PatrolPartyHealPercentage;
				Action<float> action9 = delegate(float x)
				{
					ConfigManager.Instance.Config.PatrolPartyHealPercentage = x;
				};
				yield return new NumericOption(name9, description9, extraDescription6, null, isDiscrete: false, 0f, 1f, value9, action9);
				string name10 = new TextObject("{=config_category_base_guardsell_name}Guards prisoner ransom threshold").ToString();
				MBTextManager.SetTextVariable("GuardPrisonerSellThreshold", name10);
				string description10 = new TextObject("{=config_category_base_guardsell_description}This lowers the threshold that has to be reached for prisoners to be ransomed").ToString();
				string extraDescription7 = new TextObject("{=config_category_base_guardsell_prompt}> With 0.0, the maximum amount of prisoners has to be reached before prisoners are ransomed.\n> With 0.5, the guards will sell their prisoners at half their prisoners capability.\n> With 1.0 the guards will turn in their prisoners the moment they are captured.").ToString();
				float value10 = ConfigManager.Instance.Config.GuardPrisonerSellThreshold;
				Action<float> action10 = delegate(float x)
				{
					ConfigManager.Instance.Config.GuardPrisonerSellThreshold = x;
				};
				yield return new NumericOption(name10, description10, extraDescription7, null, isDiscrete: false, 0f, 1f, value10, action10);
				string name11 = new TextObject("{=}Enable track all Improved Garrison Parties").ToString();
				string description11 = new TextObject("{=}Enable a banner frame that is used to track parties across the map for all Improved Garrison Parties").ToString();
				float value11 = (ConfigManager.Instance.Config.EnableMapBannerTracker ? 1f : 0f);
				Action<bool> action11 = delegate(bool x)
				{
					ConfigManager.Instance.Config.EnableMapBannerTracker = x;
				};
				yield return new ToggleOption(name11, description11, null, null, value11, action11);
				string name12 = new TextObject("{=config_category_default_dailymessage_name}Disable the daily message").ToString();
				MBTextManager.SetTextVariable("DisableDailyMessage", name12);
				string description12 = new TextObject("{=config_category_default_dailymessage_description}Disables the daily chat message.").ToString();
				string requirements5 = new TextObject("").ToString();
				float value12 = (ConfigManager.Instance.Config.DisableDailyMessage ? 1f : 0f);
				Action<bool> action12 = delegate(bool x)
				{
					ConfigManager.Instance.Config.DisableDailyMessage = x;
				};
				yield return new ToggleOption(name12, description12, null, requirements5, value12, action12);
				string name13 = new TextObject("{=config_category_default_tutorial_name}Deactivate tutorial").ToString();
				MBTextManager.SetTextVariable("DeactivateTutorial", name13);
				string description13 = new TextObject("{=config_category_default_tutorial_description}Disables the tutorial that starts upon entering an owned fief.").ToString();
				string requirements6 = new TextObject("").ToString();
				float value13 = (ConfigManager.Instance.Config.DeactivateTutorial ? 1f : 0f);
				Action<bool> action13 = delegate(bool x)
				{
					ConfigManager.Instance.Config.DeactivateTutorial = x;
				};
				yield return new ToggleOption(name13, description13, null, requirements6, value13, action13);
				string name14 = new TextObject("{=config_category_base_errormessage_name}Disable error messages").ToString();
				string description14 = new TextObject("{=config_category_base_errormessage_description}Disables the error messages that are shown whenever the mod encounters an error.").ToString();
				new TextObject("").ToString();
				string extraDescription8 = new TextObject("{=config_category_base_errormessage_extradescription}WARNING: if you disable error messages, you will no longer be notified if this mod encounters an error!").ToString();
				float value14 = (GlobalSettings.Instance.DisableErrorMessage ? 1f : 0f);
				Action<bool> action14 = delegate(bool x)
				{
					GlobalSettings.Instance.DisableErrorMessage = x;
				};
				yield return new ToggleOption(name14, description14, extraDescription8, null, value14, action14);
				string name15 = new TextObject("{=config_category_base_reset_name}Reset to default").ToString();
				MBTextManager.SetTextVariable("ResetToDefault", name15);
				string description15 = new TextObject("{=config_category_base_reset_description}This resets the configuration of Improved Garrisons to its default values.").ToString();
				string requirements7 = new TextObject("").ToString();
				float value15 = 0f;
				Action<bool> action15 = delegate(bool x)
				{
					if (x)
					{
						PromptIsOpen = true;
						InformationManager.ShowInquiry(new InquiryData(new TextObject("{=config_category_base_reset_name}Reset to default").ToString(), new TextObject("{=config_category_base_reset_prompt}Do you want to reset the Improved Garrison configuration to its default values?").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: true, new TextObject("{=menu_yes}Yes").ToString(), new TextObject("{=menu_cancel}Cancel").ToString(), delegate
						{
							ResetMode = true;
							PromptIsOpen = false;
						}, delegate
						{
							PromptIsOpen = false;
						}));
					}
				};
				yield return new ToggleOption(name15, description15, null, requirements7, value15, action15);
				string name16 = new TextObject("{=config_category_base_deleteparties_name}Delete all Improved Garrison parties").ToString();
				string description16 = new TextObject("{=config_category_base_deleteparties_description}Enable this to delete all Improved Garrison parties in this campaign.").ToString();
				string requirements8 = new TextObject("").ToString();
				float value16 = 0f;
				Action<bool> action16 = delegate(bool x)
				{
					if (x)
					{
						PromptIsOpen = true;
						InformationManager.ShowInquiry(new InquiryData(new TextObject("{=info_deletemode1}Delete all Improved Garrison Parties?").ToString(), new TextObject("{=menu_deletemode2}Do you really want to delete all Improved Garrison Parties?").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: true, new TextObject("{=menu_yes}Yes").ToString(), new TextObject("{=menu_cancel}Cancel").ToString(), delegate
						{
							Main.GarrisonPartyBehavior.OnGameStartDeleteAllIGParties();
							PromptIsOpen = false;
						}, delegate
						{
							PromptIsOpen = false;
						}));
					}
				};
				yield return new ToggleOption(name16, description16, null, requirements8, value16, action16);
				string name17 = new TextObject("{=config_category_base_deletesavedata_name}Delete unnecessary save data").ToString();
				string description17 = new TextObject("{=config_category_base_deletesavedata_description}If you delete a save file, the saved data of Improved Garrisons for this specific save file might still remain. By enabling this, the mod will search for data that is mapped to a save file that no longer exists and will then proceed to delete those data, as they are no longer used in any way.\n \nNote: this process might take a moment").ToString();
				string extraDescription9 = new TextObject("{=config_category_base_deletesavedata_extradescription}You can also manually delete those data by finding the Mount and Blade II Bannerlord folder in your documents folder, and look into the configuration files of Improved Garrison.").ToString();
				float value17 = 0f;
				Action<bool> action17 = delegate(bool x)
				{
					if (x)
					{
						SaveSystemManager.Instance.DeleteUnnecessarySaveAndConfigFiles();
					}
				};
				yield return new ToggleOption(name17, description17, extraDescription9, null, value17, action17);
			}
		}

		private IEnumerable<IOptionData> NpcOptionsList
		{
			get
			{
				string name = new TextObject("{=config_category_npc_guardparties_name}Allow NPCs to create guard parties").ToString();
				MBTextManager.SetTextVariable("EnableNPCMode", name);
				string description = new TextObject("{=config_category_npc_guardparties_description}Allows other factions to create guard parties to defend their lands.").ToString();
				float value = (ConfigManager.Instance.Config.NPCSpawnGuards ? 1f : 0f);
				Action<bool> action = delegate(bool x)
				{
					ConfigManager.Instance.Config.NPCSpawnGuards = x;
				};
				yield return new ToggleOption(name, description, null, null, value, action);
				string name2 = new TextObject("{=config_category_npc_npcguardspawnthreshold_name}NPC guard spawn threshold").ToString();
				string description2 = new TextObject("{=config_category_npc_npcguardspawnthreshold_description}The size an NPC garrison has to reach before a new guard party can be created").ToString();
				string requirements = new TextObject("{=config_category_npc_npcguardspawnthreshold_requirements}[{EnableNPCMode}] has to be enabled.").ToString();
				float value2 = ConfigManager.Instance.Config.NPCGuardSpawnThreshold;
				Action<float> action2 = delegate(float x)
				{
					ConfigManager.Instance.Config.NPCGuardSpawnThreshold = (int)x;
				};
				yield return new NumericOption(name2, description2, null, requirements, isDiscrete: true, 1f, 650f, value2, action2);
				string name3 = new TextObject("{=config_category_npc_npcguardsizemultiplier_name}NPC guard party size multiplier").ToString();
				string description3 = new TextObject("{=config_category_npc_npcguardsizemultiplier_description}Customize the size of the NPC guard parties. \n \nSet to 0.2 for small guard parties\nSet to 0.8 for large guard parties.").ToString();
				string requirements2 = new TextObject("{=config_category_npc_npcguardsizemultiplier_requirements}[{EnableNPCMode}] has to be enabled.").ToString();
				float value3 = (float)ConfigManager.Instance.Config.NPCGuardCreationMultiplier;
				Action<float> action3 = delegate(float x)
				{
					ConfigManager.Instance.Config.NPCGuardCreationMultiplier = x;
				};
				yield return new NumericOption(name3, description3, null, requirements2, isDiscrete: false, 0.2f, 0.8f, value3, action3);
				string name4 = new TextObject("{=config_category_npc_worldrecruit_name}NPC world recruitment").ToString();
				MBTextManager.SetTextVariable("EnableNPCWorldRecruitment", name4);
				string description4 = new TextObject("{=config_category_npc_worldrecruit_description}Enables automatic recruitment for all NPC garrisons.").ToString();
				float value4 = (ConfigManager.Instance.Config.EnableNPCWorldRecruitment ? 1f : 0f);
				Action<bool> action4 = delegate(bool x)
				{
					ConfigManager.Instance.Config.EnableNPCWorldRecruitment = x;
				};
				yield return new ToggleOption(name4, description4, null, null, value4, action4);
				string name5 = new TextObject("{=config_category_npc_prisonerrecruit_name}NPC prisoner recruitment").ToString();
				string description5 = new TextObject("{=config_category_npc_prisonerrecruit_description}Enables the recruitment of prisoners for all NPC garrisons.").ToString();
				float value5 = (ConfigManager.Instance.Config.EnableNPCPrisonerRecruitment ? 1f : 0f);
				Action<bool> action5 = delegate(bool x)
				{
					ConfigManager.Instance.Config.EnableNPCPrisonerRecruitment = x;
				};
				yield return new ToggleOption(name5, description5, null, null, value5, action5);
				string name6 = new TextObject("{=config_category_npc_recruitthreshold_name}Maximum NPC recruitment threshold").ToString();
				MBTextManager.SetTextVariable("MaximumNPCRecruitmentThreshold", name6);
				string description6 = new TextObject("{=config_category_npc_recruitthreshold_description}Maximum number of garrison units until recruitment stops.").ToString();
				string requirements3 = new TextObject("{=config_category_npc_recruitthreshold_requirements}[{EnableNPCWorldRecruitment}] has to be enabled.").ToString();
				float value6 = ConfigManager.Instance.Config.MaximumNPCRecruitmentThreshold;
				Action<float> action6 = delegate(float x)
				{
					ConfigManager.Instance.Config.MaximumNPCRecruitmentThreshold = (int)x;
				};
				yield return new NumericOption(name6, description6, null, requirements3, isDiscrete: true, 0f, 1000f, value6, action6);
				string name7 = new TextObject("{=config_category_npc_training_name}NPC garrison training").ToString();
				MBTextManager.SetTextVariable("EnableNPCGarrisonTraining", name7);
				string description7 = new TextObject("{=config_category_npc_training_description}Enables the training of garrisoned troops for all NPC garrisons.").ToString();
				float value7 = (ConfigManager.Instance.Config.EnableNPCGarrisonTraining ? 1f : 0f);
				Action<bool> action7 = delegate(bool x)
				{
					ConfigManager.Instance.Config.EnableNPCGarrisonTraining = x;
				};
				yield return new ToggleOption(name7, description7, null, null, value7, action7);
				string name8 = new TextObject("{=config_category_npc_trainingtier_name}NPC maximum training tier").ToString();
				string description8 = new TextObject("{=config_category_npc_trainingtier_description}The maximum tier NPC garrisoned troops are trained to.").ToString();
				string requirements4 = new TextObject("{=config_category_npc_trainingtier_requirements}[{EnableNPCGarrisonTraining}] has to be enabled.").ToString();
				float value8 = ConfigManager.Instance.Config.NPCMaxUpgradeTier;
				Action<float> action8 = delegate(float x)
				{
					ConfigManager.Instance.Config.NPCMaxUpgradeTier = (int)x;
				};
				yield return new NumericOption(name8, description8, null, requirements4, isDiscrete: true, 0f, 10f, value8, action8);
				string name9 = new TextObject("{=config_category_npc_foodgathering_name}NPC bonus food gathering").ToString();
				string description9 = new TextObject("{=config_category_npc_foodgathering_description}Gives a bonus food amount to all NPC garrisons.").ToString();
				string requirements5 = new TextObject("{=config_category_npc_foodgathering_requirements}[{LoadFoodGatheringModule}] has to be enabled.").ToString();
				float value9 = (ConfigManager.Instance.Config.EnabeNPCFoodbonus ? 1f : 0f);
				Action<bool> action9 = delegate(bool x)
				{
					ConfigManager.Instance.Config.EnabeNPCFoodbonus = x;
				};
				yield return new ToggleOption(name9, description9, null, requirements5, value9, action9);
				string name10 = new TextObject("{=config_category_npc_foodamount_name}NPC food bonus amount").ToString();
				string description10 = new TextObject("{=config_category_npc_foodamount_description}The food bonus amount all NPC garrisons get if NPC bonus food gathering is enabled.").ToString();
				string requirements6 = new TextObject("{=config_category_npc_foodamount_requirements}[{LoadFoodGatheringModule}] has to be enabled.").ToString();
				float value10 = ConfigManager.Instance.Config.NPCBonusFoodGathering;
				Action<float> action10 = delegate(float x)
				{
					ConfigManager.Instance.Config.NPCBonusFoodGathering = (int)x;
				};
				yield return new NumericOption(name10, description10, null, requirements6, isDiscrete: true, 0f, 150f, value10, action10);
			}
		}

		public ConfigMenuVM()
		{
			_baseSettingsCategory = new ImprovedGarrisonCategoryVM(new TextObject("{=config_category_base_title}Mod Settings"), BaseOptionsList);
			_npcOptionCategory = new ImprovedGarrisonCategoryVM(new TextObject("{=config_category_npc_title}NPC Settings"), NpcOptionsList);
			_defaultOptionCategory = new ImprovedGarrisonCategoryVM(new TextObject("{=config_category_default_title}Default Settings"), DefaultOptionsList);
			_cheatOptionCategory = new ImprovedGarrisonCategoryVM(new TextObject("{=config_category_cheats_title}Cheats"), CheatOptionsList);
			_allCategories.Add(_baseSettingsCategory);
			_allCategories.Add(_cheatOptionCategory);
			_allCategories.Add(_npcOptionCategory);
			_allCategories.Add(_defaultOptionCategory);
			PauseGame();
			RefreshValues();
		}

		public override void RefreshValues()
		{
			try
			{
				base.RefreshValues();
				BaseOptions.RefreshValues();
				CheatOptions.RefreshValues();
				DefaultOptions.RefreshValues();
				NpcOptions.RefreshValues();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ExecuteCancel()
		{
			try
			{
				foreach (ImprovedGarrisonCategoryVM allCategory in _allCategories)
				{
					foreach (ConfigOptionsMenuItemVM option in allCategory.Options)
					{
						option.Cancel();
					}
				}
				OnFinished();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void ExecuteDone()
		{
			try
			{
				bool loadFoodGatheringModule = ConfigManager.Instance.Config.LoadFoodGatheringModule;
				bool loadCustomPartySpeedModel = ConfigManager.Instance.Config.LoadCustomPartySpeedModel;
				bool loadCustomPartySizeModel = ConfigManager.Instance.Config.LoadCustomPartySizeModel;
				foreach (ImprovedGarrisonCategoryVM allCategory in _allCategories)
				{
					foreach (ConfigOptionsMenuItemVM option in allCategory.Options)
					{
						option.UpdateValue();
					}
				}
				if (ConfigManager.Instance.Config.LoadFoodGatheringModule != loadFoodGatheringModule || ConfigManager.Instance.Config.LoadCustomPartySpeedModel != loadCustomPartySpeedModel || ConfigManager.Instance.Config.LoadCustomPartySizeModel != loadCustomPartySizeModel)
				{
					PromptIsOpen = true;
					InformationManager.ShowInquiry(new InquiryData(new TextObject("{=config_reload_title}Reload required").ToString(), new TextObject("{=config_reload_desc}Reloading your save is required for all Improved Garrison settings to apply.").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: false, new TextObject("{=menu_ok}Okay").ToString(), string.Empty, delegate
					{
						PromptIsOpen = false;
					}, delegate
					{
						PromptIsOpen = false;
					}));
				}
				if (!ConfigManager.Instance.Config.EnableMapBannerTracker)
				{
					Main.PartyManagement.UntrackAllImprovedGarrisonparties();
				}
				else
				{
					Main.PartyManagement.TrackAllImprovedGarrisonparties();
				}
				OnFinished();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void OnFinished()
		{
			try
			{
				if (Main._configurationSettingsIsOpen)
				{
					IsFinished = true;
				}
				Main.GarrisonBehavior.UpdateNPCGarrisonSettings();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void OnResetToDefaults()
		{
			ConfigManager.Instance.Config.ResetToDefault();
		}

		public void SaveToConfig()
		{
			try
			{
				ConfigManager.Instance.CreateAndUpdateConfigForCurrentGame();
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_config_save_done}Improved Garrison configuration saved successfully.").ToString(), Color.FromUint(ModuleColors.green)));
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ReadConfig()
		{
			ConfigManager.Instance.ReadConfigForCurrentGame();
		}

		internal void PauseGame()
		{
			try
			{
				if (Game.Current != null)
				{
					OldGameStateManagerDisabledStatus = Game.Current.GameStateManager.ActiveStateDisabledByUser;
					Game.Current.GameStateManager.RegisterActiveStateDisableRequest(this);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		internal void UnpauseGame()
		{
			try
			{
				if (Game.Current != null)
				{
					Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(this);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}
	}
}
