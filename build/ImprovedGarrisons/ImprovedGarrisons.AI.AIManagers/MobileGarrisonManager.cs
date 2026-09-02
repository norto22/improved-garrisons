using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.AI.Orders.PartyOrder;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.AI.AIManagers
{
	public class MobileGarrisonManager
	{
		public readonly string _mobileGarrisonPartyID = "mobilegarrison_";

		internal Dictionary<string, MobileGarrison> MobileGarrisons { get; private set; } = new Dictionary<string, MobileGarrison>();

		public List<MobileParty> GetAllMobileGarrisons()
		{
			List<MobileParty> list = new List<MobileParty>();
			foreach (MobileGarrison value in MobileGarrisons.Values)
			{
				list.Add(value.mobileParty);
			}
			return list;
		}

		public void ExecutePartialHourThinkBehavior()
		{
			try
			{
				if (MobileGarrisons == null)
				{
					return;
				}
				foreach (MobileGarrison item in MobileGarrisons.Values.ToList())
				{
					item.PartialHourlyThinkBehavior();
				}
			}
			catch (Exception)
			{
			}
		}

		public void ExecuteHourThinkBehavior()
		{
			try
			{
				if (MobileGarrisons == null)
				{
					return;
				}
				foreach (MobileGarrison item in MobileGarrisons.Values.ToList())
				{
					item.NextHour();
					item.HourlyThinkBehavior();
					item.RethinkNextHour = true;
				}
			}
			catch (Exception)
			{
			}
		}

		public void RemoveNotValidMobileGarrisons()
		{
			foreach (KeyValuePair<string, MobileGarrison> item in MobileGarrisons.ToList())
			{
				if (!item.Value.IsValidAndActive())
				{
					MobileGarrisons.Remove(item.Key);
				}
			}
		}

		public Settlement GetMobileGarrisonHome(MobileParty party)
		{
			int num = party.StringId.IndexOf(_mobileGarrisonPartyID);
			if (num < 0)
			{
				return null;
			}
			if (party.StringId.Contains(ModuleStrings.newOwnerString))
			{
				return null;
			}
			string text = party.StringId.Substring(num, party.StringId.Length - num).Replace(_mobileGarrisonPartyID, "");
			int num2 = text.IndexOf('_');
			if (num2 > 0)
			{
				text = text.Substring(0, text.IndexOf('_'));
			}
			text = Regex.Replace(text, "[\\d-]", string.Empty);
			return Main.GarrisonBehavior.GetSettlementFromName(text);
		}

		public MobileGarrison GetMobileGarrisonForParty(MobileParty party)
		{
			try
			{
				if (party == null)
				{
					return null;
				}
				foreach (KeyValuePair<string, MobileGarrison> mobileGarrison in MobileGarrisons)
				{
					if (mobileGarrison.Value.getMobileParty().StringId == party.StringId)
					{
						return mobileGarrison.Value;
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return null;
		}

		internal MobileGarrison GiveMobilePartyAMobileGarrison(MobileParty party)
		{
			try
			{
				if (party != null && IsMobileGarrisonParty(party))
				{
					if (Main.PartyManagement.mobileGarrisonManagement.GetMobileGarrisonForParty(party) != null)
					{
						Main.GarrisonPartyBehavior.RemovePartyHelper(party);
						return null;
					}
					Settlement mobileGarrisonHome = GetMobileGarrisonHome(party);
					if (mobileGarrisonHome == null)
					{
						Main.GarrisonPartyBehavior.RemovePartyHelper(party);
						return null;
					}
					MobileGarrison mobileGarrison = new MobileGarrison(party, mobileGarrisonHome);
					party.SetCustomHomeSettlement(mobileGarrison.fromSettlement);
					GarrisonSettings townSettings = Main.GarrisonBehavior.GetTownSettings(mobileGarrison.fromSettlement.Town);
					MobileGarrison value;
					bool flag = MobileGarrisons.TryGetValue(mobileGarrison.fromSettlement.StringId, out value);
					party.ShouldJoinPlayerBattles = true;
					if (value != null && mobileGarrison != value)
					{
						party.StringId = "IGParty_should_be_removed";
						Main.GarrisonPartyBehavior.RemovePartyHelper(party);
						return null;
					}
					if (value != null)
					{
						mobileGarrison = value;
					}
					else if (value == null)
					{
						MobileGarrisons.Add(mobileGarrison.fromSettlement.StringId, mobileGarrison);
					}
					if (mobileGarrison.fromSettlement.Owner != null && mobileGarrison.fromSettlement.Owner != Hero.MainHero)
					{
						mobileGarrison.isNPC = true;
					}
					if (party.ShortTermBehavior == AiBehavior.EscortParty)
					{
						mobileGarrison.GiveAndExecuteOrder(new OrderEscort(party.Ai.AiBehaviorPartyBase.MobileParty));
					}
					return mobileGarrison;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return null;
		}

		public PartyBase CreateMobileGarrison(Settlement forSettlement, Settlement spawnSettlement, bool npc = false, bool shortlived = false)
		{
			try
			{
				if (forSettlement == null || spawnSettlement == null)
				{
					return null;
				}
				string text = forSettlement.Name.ToString();
				string id = _mobileGarrisonPartyID + text;
				TextObject partyName = new TextObject(new TextObject("{=party_guards_name}Garrison guards of").ToString() + ModuleStrings._space + text);
				PartyBase partyBase = Main.PartyManagement.InitializeNewParty(id, partyName, forSettlement, spawnSettlement);
				if (partyBase != null)
				{
					MobileGarrison mobileGarrison = new MobileGarrison(partyBase.MobileParty, forSettlement, shortlived);
					if (!MobileGarrisons.ContainsKey(forSettlement.StringId))
					{
						MobileGarrisons.Add(forSettlement.StringId, mobileGarrison);
					}
					if (npc)
					{
						mobileGarrison.isNPC = true;
					}
				}
				return partyBase;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return null;
		}

		public PartyBase TryCreateMobileGarrisonByStrength(Settlement forSettlement, float strength, bool npc = false, bool shortLived = false)
		{
			try
			{
				if (forSettlement == null || forSettlement.IsVillage || forSettlement.IsHideout || forSettlement.Town == null || forSettlement.Town.GarrisonParty == null)
				{
					return null;
				}
				List<Tuple<CharacterObject, int>> list = new List<Tuple<CharacterObject, int>>();
				List<TroopRosterElement> list2 = (from x in forSettlement.Town.GarrisonParty.MemberRoster.GetTroopRoster()
					orderby x.Character.Tier descending
					select x).ToList();
				float num = 0f;
				foreach (TroopRosterElement item in list2)
				{
					num += Campaign.Current.Models.MilitaryPowerModel.GetDefaultTroopPower(item.Character) * (float)item.Number;
				}
				double num2 = 0.275;
				double num3 = 0.45;
				double num4 = 0.275;
				int numberOfAllMembers = forSettlement.Town.GarrisonParty.Party.NumberOfAllMembers;
				int num5 = (int)((double)numberOfAllMembers * num2);
				int num6 = (int)((double)numberOfAllMembers * num3) + 1;
				int num7 = (int)((double)numberOfAllMembers * num4);
				if (num > strength)
				{
					PartyBase partyBase = CreateMobileGarrison(forSettlement, forSettlement, npc, shortLived);
					float num8 = strength;
					if (partyBase != null)
					{
						foreach (TroopRosterElement item2 in list2)
						{
							CharacterObject character = item2.Character;
							int number = item2.Number;
							int num9 = 0;
							if (character.IsHero)
							{
								continue;
							}
							if (num8 <= 0f)
							{
								break;
							}
							if (character.IsRanged && !character.IsMounted && num5 > 0)
							{
								num9 = ((num5 - number <= 0) ? num5 : number);
								num5 -= num9;
							}
							else if (character.IsInfantry && !character.IsMounted && num6 > 0)
							{
								num9 = ((num6 - number <= 0) ? num6 : number);
								num6 -= num9;
							}
							else
							{
								if (!character.IsMounted || num7 <= 0)
								{
									continue;
								}
								num9 = ((num7 - number <= 0) ? num7 : number);
								num7 -= num9;
							}
							float num10 = Campaign.Current.Models.MilitaryPowerModel.GetDefaultTroopPower(item2.Character) * (float)num9;
							num8 -= num10;
							list.Add(new Tuple<CharacterObject, int>(item2.Character, num9));
						}
						foreach (TroopRosterElement item3 in forSettlement.Town.GarrisonParty.MemberRoster.GetTroopRoster())
						{
							if (num8 <= 0f)
							{
								break;
							}
							CharacterObject character2 = item3.Character;
							int number2 = item3.Number;
							float num11 = Campaign.Current.Models.MilitaryPowerModel.GetDefaultTroopPower(item3.Character) * (float)item3.Number;
							num8 -= num11;
							list.Add(new Tuple<CharacterObject, int>(item3.Character, number2));
						}
						Main.GarrisonPartyBehavior.TransferTroopsFromPartyToParty(forSettlement.Town.GarrisonParty, list, partyBase);
					}
					return partyBase;
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return null;
		}

		public PartyBase CreateMobileGarrisonWithUnits(Settlement forSettlement, int amountOfUnits, bool npc = false)
		{
			try
			{
				if (forSettlement == null || forSettlement.IsVillage || forSettlement.IsUnderRaid || forSettlement.IsUnderSiege || forSettlement.IsHideout || forSettlement.Town == null || forSettlement.Town.GarrisonParty == null || forSettlement.Town.GarrisonParty.Party.NumberOfAllMembers <= amountOfUnits)
				{
					return null;
				}
				PartyBase partyBase = CreateMobileGarrison(forSettlement, forSettlement, npc);
				if (partyBase != null)
				{
					List<Tuple<CharacterObject, int>> list = new List<Tuple<CharacterObject, int>>();
					double num = 0.275;
					double num2 = 0.45;
					double num3 = 0.275;
					int num4 = (int)((double)amountOfUnits * num);
					int num5 = (int)((double)amountOfUnits * num2) + 1;
					int num6 = (int)((double)amountOfUnits * num3);
					List<TroopRosterElement> list2 = (from x in forSettlement.Town.GarrisonParty.MemberRoster.GetTroopRoster()
						orderby x.Character.Tier descending
						select x).ToList();
					foreach (TroopRosterElement item in list2)
					{
						CharacterObject character = item.Character;
						int number = item.Number;
						int num7 = 0;
						if (character.IsHero)
						{
							continue;
						}
						if (character.IsRanged && !character.IsMounted && num4 > 0)
						{
							num7 = ((num4 - number <= 0) ? num4 : number);
							num4 -= num7;
						}
						else if (character.IsInfantry && !character.IsMounted && num5 > 0)
						{
							num7 = ((num5 - number <= 0) ? num5 : number);
							num5 -= num7;
						}
						else
						{
							if (!character.IsMounted || num6 <= 0)
							{
								continue;
							}
							num7 = ((num6 - number <= 0) ? num6 : number);
							num6 -= num7;
						}
						list.Add(new Tuple<CharacterObject, int>(item.Character, num7));
					}
					int num8 = num4 + num5 + num6;
					foreach (TroopRosterElement item2 in forSettlement.Town.GarrisonParty.MemberRoster.GetTroopRoster())
					{
						if (num8 <= 0)
						{
							break;
						}
						CharacterObject character2 = item2.Character;
						int number2 = item2.Number;
						int num9 = 0;
						num9 = ((num8 - number2 <= 0) ? num8 : number2);
						num8 -= num9;
						list.Add(new Tuple<CharacterObject, int>(item2.Character, num9));
					}
					Main.GarrisonPartyBehavior.TransferTroopsFromPartyToParty(forSettlement.Town.GarrisonParty, list, partyBase);
				}
				return partyBase;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return null;
		}

		public bool IsMobileGarrisonParty(MobileParty party)
		{
			if (party != null && party.StringId != null)
			{
				return party.StringId.Contains(_mobileGarrisonPartyID);
			}
			return false;
		}

		public MobileGarrison GetMobileGarrisonPartyOfSettlement(Settlement settlement)
		{
			if (MobileGarrisons.TryGetValue(settlement.StringId, out var value))
			{
				return value;
			}
			return null;
		}
	}
}
