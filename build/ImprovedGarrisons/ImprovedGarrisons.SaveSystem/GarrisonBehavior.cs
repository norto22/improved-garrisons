using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.ImprovedGarrisonsUI;
using ImprovedGarrisons.Menu;
using ImprovedGarrisons.SaveSystem.SaveData;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace ImprovedGarrisons.SaveSystem
{
	public class GarrisonBehavior : CampaignBehaviorBase
	{
		private MainMenu mainMenu;

		public readonly Dictionary<Settlement, ImprovedSettlement> ImprovedSettlements = new Dictionary<Settlement, ImprovedSettlement>();

		public MainMenu MainMenu => mainMenu;

		public Dictionary<string, GarrisonSettings> SettlementSettingsData => IGSaveData.Instance.SettlementSettingsData;

		public Town CurrentTownForSettings { get; set; }

		public override void RegisterEvents()
		{
			CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, onSettlementOwnerChanged);
			CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, HourlyEvent);
			CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DailyEvent);
		}

		public void OnGameOpen(CampaignGameStarter campaignGameStarter)
		{
			try
			{
				mainMenu = new MainMenu(campaignGameStarter);
				SetAllAutomatedGarrisons();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void HourlyEvent()
		{
			try
			{
				foreach (ImprovedSettlement value in ImprovedSettlements.Values)
				{
					value.HourlyThinkBehavior();
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void DailyEvent()
		{
			try
			{
				foreach (ImprovedSettlement value in ImprovedSettlements.Values)
				{
					value.DailyThinkBehavior();
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void onSettlementOwnerChanged(Settlement settlement, bool x, Hero y, Hero z, Hero a, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail action)
		{
			try
			{
				if (settlement != null && (settlement.IsTown || settlement.IsCastle) && SettlementSettingsData.ContainsKey(settlement.Town.Name.ToString()) && settlement.Owner != Hero.MainHero)
				{
					SettlementSettingsData.Remove(settlement.Town.Name.ToString());
					if (UIManager.Instance.improvedGarrisonsUI != null)
					{
						UIManager.Instance.improvedGarrisonsUI.UpdateSettlementSelector();
					}
				}
				else if (settlement != null && (settlement.IsTown || settlement.IsCastle) && !SettlementSettingsData.ContainsKey(settlement.Town.Name.ToString()) && settlement.Owner == Hero.MainHero)
				{
					GetTownSettings(settlement.Town);
					if (UIManager.Instance.improvedGarrisonsUI != null)
					{
						UIManager.Instance.improvedGarrisonsUI.UpdateSettlementSelector();
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public GarrisonSettings GetCurrentTownSettings()
		{
			return GetTownSettings(CurrentTownForSettings);
		}

		public void ResetTownSettings(Town town)
		{
			if (SettlementSettingsData.ContainsKey(town.Name.ToString()))
			{
				SettlementSettingsData[town.Name.ToString()] = new GarrisonSettings();
			}
		}

		public GarrisonSettings GetTownSettings(Town town)
		{
			try
			{
				bool flag = SettlementSettingsData != null;
				if (town != null && town.Owner != null)
				{
					if (town.Settlement.OwnerClan.Equals(Hero.MainHero.Clan) && flag)
					{
						if (SettlementSettingsData.TryGetValue(town.Name.ToString(), out var value))
						{
							return value;
						}
						value = new GarrisonSettings();
						SettlementSettingsData.Add(town.Name.ToString(), value);
						return value;
					}
					return new NPCGarrisonSettings();
				}
				return new NPCGarrisonSettings();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return null;
			}
		}

		public bool PlayerHasAFief()
		{
			try
			{
				return SettlementSettingsData != null && SettlementSettingsData.Count > 0;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return false;
			}
		}

		public ImprovedSettlement GetAutomatedGarrisonForSettlement(Settlement settlement)
		{
			if (!ImprovedSettlements.TryGetValue(settlement, out var value))
			{
				return new ImprovedSettlement(settlement);
			}
			return value;
		}

		public Settlement GetSettlementFromName(string name)
		{
			if (name != null && name.Length > 0)
			{
				foreach (Settlement item in Settlement.All)
				{
					if (item.Name != null && item.Name.ToString() == name)
					{
						return item;
					}
				}
			}
			return null;
		}

		internal void InitializeSettlements()
		{
			try
			{
				List<string> list = new List<string>();
				foreach (Settlement item in Settlement.All)
				{
					if (item != null && item.Town != null && (item.IsTown || item.IsCastle) && item.Town.OwnerClan == Hero.MainHero.Clan)
					{
						GetTownSettings(item.Town);
					}
					else
					{
						if (item == null || (!item.IsTown && !item.IsCastle))
						{
							continue;
						}
						foreach (KeyValuePair<string, GarrisonSettings> settlementSettingsDatum in SettlementSettingsData)
						{
							if (item.Town.Name.ToString() == settlementSettingsDatum.Key)
							{
								list.Add(settlementSettingsDatum.Key);
							}
						}
					}
				}
				foreach (string item2 in list)
				{
					SettlementSettingsData.Remove(item2);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void SetAllAutomatedGarrisons()
		{
			try
			{
				foreach (Settlement item in Settlement.All)
				{
					if (item != null && (item.IsTown || item.IsCastle))
					{
						if (SettlementSettingsData.TryGetValue(item.Name.ToString(), out var _))
						{
							ImprovedSettlements.Add(item, new ImprovedSettlement(item));
							continue;
						}
						bool flag = true;
						ImprovedSettlement improvedSettlement = new ImprovedSettlement(item);
						ImprovedSettlements.Add(item, improvedSettlement);
						improvedSettlement.Activate();
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void UpdateNPCGarrisonSettings()
		{
			try
			{
				foreach (KeyValuePair<Settlement, ImprovedSettlement> item in ImprovedSettlements.ToList())
				{
					if (!SettlementSettingsData.TryGetValue(item.Key.Name.ToString(), out var _))
					{
						ImprovedSettlements[item.Key] = new ImprovedSettlement(item.Key);
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public List<Settlement> GetAllPlayerSettlements()
		{
			Settlement[] array = Enumerable.ToArray(Settlement.All);
			List<Settlement> list = new List<Settlement>();
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] != null && array[i].Town != null && (array[i].Town.IsCastle || array[i].Town.IsTown) && SettlementSettingsData.TryGetValue(array[i].Name.ToString(), out var _))
				{
					list.Add(array[i]);
				}
			}
			return list;
		}

		public List<Tuple<CharacterObject, int>> GetLowestTierUnitsByAmount(int amount, Town town)
		{
			if (town != null && amount > 0 && town.GarrisonParty != null)
			{
				MobileParty garrisonParty = town.GarrisonParty;
				List<TroopRosterElement> troopRoster = garrisonParty.MemberRoster.GetTroopRoster();
				List<TroopRosterElement> list = troopRoster.OrderBy((TroopRosterElement x) => x.Character.Tier).ToList();
				List<Tuple<CharacterObject, int>> list2 = new List<Tuple<CharacterObject, int>>();
				troopRoster = new List<TroopRosterElement>();
				while (amount > 0 && list.Count > 0)
				{
					TroopRosterElement item = list.First();
					int num = garrisonParty.MemberRoster.GetTroopCount(item.Character);
					if (num > amount)
					{
						num = amount;
					}
					list2.Add(new Tuple<CharacterObject, int>(item.Character, num));
					list.Remove(item);
					amount -= num;
				}
				return list2;
			}
			return null;
		}

		public override void SyncData(IDataStore dataStore)
		{
		}
	}
}
