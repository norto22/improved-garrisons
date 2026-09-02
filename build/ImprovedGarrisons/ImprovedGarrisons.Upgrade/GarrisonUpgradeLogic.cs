using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.SaveSystem.Configuration;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ImprovedGarrisons.Upgrade
{
	public class GarrisonUpgradeLogic
	{
		public void GiveExpToAllGarrisons()
		{
			foreach (Settlement item in Settlement.All)
			{
				GiveGarrisonExp(item);
			}
		}

		public void GiveGarrisonExp(Settlement settlement)
		{
			if (settlement == null || (!settlement.IsCastle && !settlement.IsTown))
			{
				return;
			}
			GarrisonSettings townSettings = Main.GarrisonBehavior.GetTownSettings(settlement.Town);
			if (!townSettings.EnableTraining)
			{
				return;
			}
			try
			{
				int dailyEXPAmount = ConfigManager.Instance.Config.DailyEXPAmount;
				bool flag = settlement.Town.GarrisonParty != null && settlement.Town.GarrisonParty.MemberRoster.GetTroopRoster().Count() > 0;
				bool flag2 = !settlement.Town.IsUnderSiege;
				List<CharacterObject> list = new List<CharacterObject>();
				if (!(flag && flag2))
				{
					return;
				}
				int num = Campaign.Current.Models.DailyTroopXpBonusModel.CalculateDailyTroopXpBonus(settlement.Town);
				float num2 = Campaign.Current.Models.DailyTroopXpBonusModel.CalculateGarrisonXpBonusMultiplier(settlement.Town);
				int num3 = TaleWorlds.Library.MathF.Round((float)num * num2);
				foreach (TroopRosterElement item in settlement.Town.GarrisonParty.MemberRoster.GetTroopRoster())
				{
					list.Add(item.Character);
				}
				foreach (CharacterObject item2 in list)
				{
					if (item2 == null || townSettings == null || !UnitUpgradeIsAllowed(townSettings, item2, settlement))
					{
						continue;
					}
					bool flag3 = false;
					bool flag4 = CharacterIsASpecifiedUnit(item2, settlement.Town);
					bool flag5 = item2.Tier < townSettings.MaxUpgradeTier;
					flag3 = RecursivHasSpecifiedUnitInPaths(item2, settlement, dontCheckSelf: true);
					bool flag6 = flag3 && flag4;
					bool flag7 = !flag3 && !flag4 && flag5;
					bool flag8 = flag3 && !flag4;
					if (!(flag6 || flag7 || flag8))
					{
						TroopRoster memberRoster = settlement.Town.GarrisonParty.MemberRoster;
						int num4 = memberRoster.FindIndexOfTroop(item2);
						if (num4 < memberRoster.Count && num4 >= 0)
						{
							memberRoster.SetElementXp(num4, 0);
						}
						continue;
					}
					Dictionary<CharacterObject, int> dictionary = new Dictionary<CharacterObject, int>();
					bool[] troopsToUpgradeTo = townSettings.TroopsToUpgradeTo;
					List<CharacterObject> list2 = item2.UpgradeTargets.ToList();
					if (list2.Count <= 0)
					{
						continue;
					}
					bool flag9 = townSettings.Template.AmountOfTroopsInTemplate > 0;
					if (flag3 && flag9)
					{
						foreach (CharacterObject item3 in list2)
						{
							if (RecursivHasSpecifiedUnitInPaths(item3, settlement))
							{
								int xpCostForUpgrade = Campaign.Current.Models.PartyTroopUpgradeModel.GetXpCostForUpgrade(settlement.Town.GarrisonParty.Party, item2, item3);
								if (xpCostForUpgrade > 0)
								{
									dictionary.Add(item3, xpCostForUpgrade);
								}
							}
						}
						if (dictionary.Count <= 0)
						{
							continue;
						}
					}
					else
					{
						CharacterObject randomElement = list2.GetRandomElement();
						int xpCostForUpgrade2 = Campaign.Current.Models.PartyTroopUpgradeModel.GetXpCostForUpgrade(settlement.Town.GarrisonParty.Party, item2, randomElement);
						dictionary.Add(randomElement, xpCostForUpgrade2);
					}
					int num5 = settlement.Town.GarrisonParty.MemberRoster.GetTroopCount(item2);
					int elementXp = settlement.Town.GarrisonParty.MemberRoster.GetElementXp(settlement.Town.GarrisonParty.MemberRoster.FindIndexOfTroop(item2));
					int num6 = dailyEXPAmount * num5;
					int num7 = elementXp + num6 + num3;
					int num8 = -1;
					if (num5 <= num8)
					{
						continue;
					}
					if (dictionary.Count > 0)
					{
						Dictionary<CharacterObject, int> dictionary2 = new Dictionary<CharacterObject, int>();
						Dictionary<CharacterObject, int> dictionary3 = new Dictionary<CharacterObject, int>();
						if (flag3 && flag9)
						{
							foreach (KeyValuePair<CharacterObject, int> item4 in dictionary)
							{
								int num9 = 0;
								List<CharacterObject> list3 = RecursivGetSpecifiedUnitsOfCharacter(item4.Key, settlement);
								foreach (CharacterObject item5 in list3)
								{
									int amountForTemplateTroop = townSettings.Template.GetAmountForTemplateTroop(item5);
									num9 = ((amountForTemplateTroop >= 0 && amountForTemplateTroop != -1) ? (num9 + amountForTemplateTroop) : 9999);
								}
								dictionary2.Add(item4.Key, num9);
							}
							foreach (KeyValuePair<CharacterObject, int> item6 in dictionary)
							{
								if (num7 <= 0 || num5 <= 0)
								{
									break;
								}
								int value = 0;
								if (!(dictionary2.TryGetValue(item6.Key, out value) && flag9))
								{
									continue;
								}
								int num10 = RecursivGetUnitsOnPathInGarrison(item6.Key, settlement);
								int num11 = ((item6.Value > 0) ? (num7 / item6.Value) : 0);
								if (num11 > num5)
								{
									num11 = num5;
								}
								if (num10 + num11 > value)
								{
									int num12 = value - num10;
									if (num12 > 0)
									{
										dictionary3.Add(item6.Key, num12);
										num5 -= num12;
										num7 -= item6.Value * num12;
									}
								}
								else if (item6.Value > 0 && num11 > 0)
								{
									dictionary3.Add(item6.Key, num11);
									num5 -= num11;
									num7 -= item6.Value * num11;
								}
							}
						}
						else
						{
							foreach (KeyValuePair<CharacterObject, int> item7 in dictionary)
							{
								int num13 = ((item7.Value > 0) ? (num7 / item7.Value) : 0);
								if (num13 > 0)
								{
									dictionary3.Add(item7.Key, num13);
								}
							}
						}
						Dictionary<CharacterObject, int> dictionary4 = new Dictionary<CharacterObject, int>();
						foreach (KeyValuePair<CharacterObject, int> item8 in dictionary)
						{
							if (num7 <= 0 || num5 <= 0)
							{
								break;
							}
							int num14 = ((item8.Value > 0) ? (num7 / item8.Value) : 0);
							if (num14 > num5)
							{
								num14 = num5;
							}
							if (num14 > 0)
							{
								num5 -= num14;
								num7 -= item8.Value * num14;
								dictionary4.Add(item8.Key, num14);
							}
						}
						int xpAmount = num7;
						if (dictionary3.Count <= 0)
						{
							continue;
						}
						foreach (KeyValuePair<CharacterObject, int> item9 in dictionary3)
						{
							TroopRoster memberRoster2 = settlement.Town.GarrisonParty.MemberRoster;
							int roundedResultNumber = Campaign.Current.Models.PartyTroopUpgradeModel.GetGoldCostForUpgrade(settlement.Town.GarrisonParty.Party, item2, item9.Key).RoundedResultNumber;
							roundedResultNumber *= item9.Value;
							int num15 = memberRoster2.FindIndexOfTroop(item2);
							if (num15 < memberRoster2.Count && num15 >= 0)
							{
								Main.ActivityLogManager.AddUnitUpgradeCost(settlement.Town, roundedResultNumber);
								memberRoster2.SetElementXp(num15, 0);
								try
								{
									memberRoster2.AddToCounts(item9.Key, item9.Value, insertAtFront: false, 0, 0, removeDepleted: false);
									memberRoster2.RemoveTroop(item2, item9.Value);
								}
								catch (Exception)
								{
								}
								if (settlement.Owner != null && settlement.Owner != null && settlement.Owner == Hero.MainHero)
								{
									Main.ActivityLogManager.AddNewUpgrades(settlement.Town, item2, item9.Key, item9.Value);
								}
							}
						}
						settlement.Town.GarrisonParty.MemberRoster.AddXpToTroop(item2, xpAmount);
					}
					else
					{
						settlement.Town.GarrisonParty.MemberRoster.AddXpToTroop(item2, num6);
					}
				}
			}
			catch (Exception ex2)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex2);
			}
		}

		public List<CharacterObject> RecursivGetSpecifiedUnitsOfCharacter(CharacterObject character, Settlement settlement)
		{
			List<CharacterObject> list = new List<CharacterObject>();
			try
			{
				if (settlement != null)
				{
					GarrisonSettings townSettings = Main.GarrisonBehavior.GetTownSettings(settlement.Town);
					if (character.UpgradeTargets != null)
					{
						CharacterObject[] upgradeTargets = character.UpgradeTargets;
						foreach (CharacterObject character2 in upgradeTargets)
						{
							List<CharacterObject> list2 = RecursivGetSpecifiedUnitsOfCharacter(character2, settlement);
							foreach (CharacterObject item in list2)
							{
								list.Add(item);
							}
						}
					}
					if (townSettings.Template.Contains(character))
					{
						list.Add(character);
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return list;
		}

		public int RecursivGetUnitsOnPathInGarrison(CharacterObject character, Settlement settlement)
		{
			int num = 0;
			try
			{
				if (settlement == null || settlement.Town == null || settlement.Town.GarrisonParty == null)
				{
					return 0;
				}
				if (character.UpgradeTargets != null)
				{
					CharacterObject[] upgradeTargets = character.UpgradeTargets;
					foreach (CharacterObject character2 in upgradeTargets)
					{
						if (RecursivHasSpecifiedUnitInPaths(character2, settlement))
						{
							num += RecursivGetUnitsOnPathInGarrison(character2, settlement);
						}
					}
				}
				int troopCount = settlement.Town.GarrisonParty.MemberRoster.GetTroopCount(character);
				num += troopCount;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return num;
		}

		public Dictionary<CultureObject, int> GetAmountOfNeededCultureUnits(Settlement settlement, bool eliteUnits = false)
		{
			Dictionary<CultureObject, int> dictionary = new Dictionary<CultureObject, int>();
			try
			{
				if (settlement == null || settlement.Town == null)
				{
					return null;
				}
				GarrisonSettings townSettings = Main.GarrisonBehavior.GetTownSettings(settlement.Town);
				if (townSettings.Template != null && townSettings.Template.AmountOfTroopsInTemplate > 0)
				{
					foreach (KeyValuePair<CharacterObject, int> troopListAsCharacterObject in townSettings.Template.GetTroopListAsCharacterObjects())
					{
						CharacterObject key = troopListAsCharacterObject.Key;
						if (key.Culture == null)
						{
							continue;
						}
						bool flag = CharacterCanUpgradeToTarget(key.Culture.EliteBasicTroop, key);
						int value = troopListAsCharacterObject.Value;
						if ((flag || eliteUnits) && !(flag && eliteUnits))
						{
							continue;
						}
						int num = value;
						if (settlement.Town.GarrisonParty != null)
						{
							foreach (TroopRosterElement item in settlement.Town.GarrisonParty.MemberRoster.GetTroopRoster())
							{
								if (CharacterCanUpgradeToTarget(item.Character, key))
								{
									num = ((item.Number < num) ? (num - item.Number) : 0);
								}
							}
						}
						if (num > 0)
						{
							if (dictionary.TryGetValue(key.Culture, out var _))
							{
								dictionary[key.Culture] += num;
							}
							else
							{
								dictionary.Add(key.Culture, num);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return dictionary;
		}

		private int RecursivGetUnitsUntilLowestTarget(CharacterObject character, Settlement settlement, bool ignoreCurrentCharacter = false)
		{
			int num = 0;
			try
			{
				if (settlement == null || settlement.Town == null || settlement.Town.GarrisonParty == null)
				{
					return 0;
				}
				GarrisonSettings townSettings = Main.GarrisonBehavior.GetTownSettings(settlement.Town);
				if (townSettings.Template.Contains(character))
				{
					return settlement.Town.GarrisonParty.MemberRoster.GetTroopCount(character);
				}
				if (character.UpgradeTargets != null)
				{
					CharacterObject[] upgradeTargets = character.UpgradeTargets;
					foreach (CharacterObject character2 in upgradeTargets)
					{
						if (RecursivHasSpecifiedUnitInPaths(character, settlement))
						{
							num += RecursivGetUnitsOnPathInGarrison(character2, settlement);
						}
					}
				}
				int troopCount = settlement.Town.GarrisonParty.MemberRoster.GetTroopCount(character);
				num += troopCount;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return num;
		}

		public bool RecursivHasSpecifiedUnitInPaths(CharacterObject character, Settlement settlement, bool dontCheckSelf = false)
		{
			try
			{
				if (settlement != null && settlement.Town != null)
				{
					if (dontCheckSelf || !CharacterIsASpecifiedUnit(character, settlement.Town))
					{
						bool flag = false;
						List<CharacterObject> list = new List<CharacterObject>();
						List<CharacterObject> list2 = new List<CharacterObject>();
						GarrisonSettings townSettings = Main.GarrisonBehavior.GetTownSettings(settlement.Town);
						if (character.UpgradeTargets != null)
						{
							CharacterObject[] upgradeTargets = character.UpgradeTargets;
							foreach (CharacterObject character2 in upgradeTargets)
							{
								if (RecursivHasSpecifiedUnitInPaths(character2, settlement))
								{
									flag = true;
								}
								foreach (CharacterObject item in list)
								{
									list2.Add(item);
								}
							}
						}
						if (!flag)
						{
							CharacterIsASpecifiedUnit(character, settlement.Town);
						}
						return flag;
					}
					return true;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		public bool CharacterCanUpgradeToTarget(CharacterObject characterToCheck, CharacterObject finalTarget)
		{
			try
			{
				if (characterToCheck.UpgradeTargets != null)
				{
					CharacterObject[] upgradeTargets = characterToCheck.UpgradeTargets;
					foreach (CharacterObject characterToCheck2 in upgradeTargets)
					{
						if (CharacterCanUpgradeToTarget(characterToCheck2, finalTarget))
						{
							return true;
						}
					}
				}
				if (characterToCheck == finalTarget)
				{
					return true;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		public bool CharacterIsASpecifiedUnit(CharacterObject character, Town town)
		{
			try
			{
				bool result = false;
				if (character != null && town != null)
				{
					GarrisonSettings townSettings = Main.GarrisonBehavior.GetTownSettings(town);
					result = townSettings.Template.Contains(character);
				}
				return result;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
				return false;
			}
		}

		public bool UnitUpgradeIsAllowed(GarrisonSettings settings, CharacterObject character, Settlement settlement)
		{
			try
			{
				if (character != null && settlement != null && settlement.Town != null && settings != null)
				{
					return character.UpgradeTargets != null;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		private Dictionary<int, int> AmountOfTierTroops(TroopRoster troops)
		{
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			foreach (TroopRosterElement item in troops.GetTroopRoster())
			{
				int troopCount = troops.GetTroopCount(item.Character);
				if (dictionary.TryGetValue(item.Character.Tier, out var _))
				{
					dictionary[item.Character.Tier] += troopCount;
				}
				else
				{
					dictionary.Add(item.Character.Tier, troopCount);
				}
			}
			return dictionary;
		}
	}
}
