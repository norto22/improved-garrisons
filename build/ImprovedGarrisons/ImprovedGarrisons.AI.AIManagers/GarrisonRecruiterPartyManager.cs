using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImprovedGarrisons.AI.AITypes;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.AI.AIManagers
{
	public class GarrisonRecruiterPartyManager
	{
		public string GarrisonRecruiterPartyID { get; } = "improvedgarrison_recruiter_";

		internal Dictionary<MobileParty, GarrisonRecruiter> GarrisonRecruiterParties { get; } = new Dictionary<MobileParty, GarrisonRecruiter>();

		public List<MobileParty> GetAllRecruiters()
		{
			List<MobileParty> list = new List<MobileParty>();
			foreach (MobileParty key in GarrisonRecruiterParties.Keys)
			{
				list.Add(key);
			}
			return list;
		}

		public PartyBase CreateGarrisonRecruiterParty(Settlement forSettlement, Settlement spawnSettlement, bool autoAddTroop = false)
		{
			try
			{
				if (forSettlement == null || spawnSettlement == null)
				{
					return null;
				}
				string text = forSettlement.Name.ToString();
				string id = GenerateRecruiterId(forSettlement.Town);
				if (string.IsNullOrEmpty(id))
				{
					return null;
				}
				TextObject partyName = new TextObject(new TextObject("{=party_recruiter_name}Garrison recruiter of").ToString() + ModuleStrings._space + text);
				PartyBase partyBase = Main.PartyManagement.InitializeNewParty(id, partyName, forSettlement, spawnSettlement);
				if (partyBase != null)
				{
					GarrisonRecruiter garrisonRecruiter = new GarrisonRecruiter(partyBase.MobileParty, forSettlement);
					partyBase.MobileParty.Aggressiveness = 0f;
					if (autoAddTroop)
					{
						List<Tuple<CharacterObject, int>> bestRecruiterUnits = GetBestRecruiterUnits(forSettlement, 15);
						if (bestRecruiterUnits == null || bestRecruiterUnits.Count <= 0)
						{
							Main.GarrisonPartyBehavior.RemovePartyHelper(partyBase.MobileParty);
							return null;
						}
						Main.GarrisonPartyBehavior.TransferTroopsFromPartyToParty(forSettlement.Town.GarrisonParty, bestRecruiterUnits, partyBase);
						garrisonRecruiter.SetInitialSize();
					}
					if (!GarrisonRecruiterParties.ContainsKey(partyBase.MobileParty))
					{
						GarrisonRecruiterParties.Add(partyBase.MobileParty, garrisonRecruiter);
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

		public void GiveMobilePartyARecruiter(MobileParty party)
		{
			try
			{
				if (party != null && IsRecruiterParty(party))
				{
					Settlement recruiterPartyHome = GetRecruiterPartyHome(party);
					GarrisonRecruiterParties.Add(party, new GarrisonRecruiter(party, recruiterPartyHome));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void ExecutePartialHourlyBehavior()
		{
			try
			{
				foreach (GarrisonRecruiter value in GarrisonRecruiterParties.Values)
				{
					value.PartialHourlyThinkBehavior();
				}
			}
			catch (Exception)
			{
			}
		}

		public void ExecuteHourThinkBehaviorForAll()
		{
			try
			{
				foreach (GarrisonRecruiter value in GarrisonRecruiterParties.Values)
				{
					value.NextHour();
					value.HourlyThinkBehavior();
					value.RethinkNextHour = true;
				}
			}
			catch (Exception)
			{
			}
		}

		private string GenerateRecruiterId(Town town)
		{
			if (town == null)
			{
				return null;
			}
			int num = 0;
			foreach (GarrisonRecruiter value in GarrisonRecruiterParties.Values)
			{
				Settlement fromSettlement = value.fromSettlement;
				if (fromSettlement != null && fromSettlement.Town == town)
				{
					num++;
				}
			}
			return Main.PartyManagement.garrisonRecruiterPartyManagement.GarrisonRecruiterPartyID + town.Name?.ToString() + "_" + num;
		}

		private List<Tuple<CharacterObject, int>> GetBestRecruiterUnits(Settlement settlement, int amount)
		{
			if (settlement == null || settlement.Town == null || settlement.Town.GarrisonParty == null)
			{
				return null;
			}
			return Main.GarrisonBehavior.GetLowestTierUnitsByAmount(amount, settlement.Town);
		}

		public bool SettlementHasARecruiter(Settlement settlement)
		{
			GarrisonRecruiter recruiterOfSettlement = GetRecruiterOfSettlement(settlement);
			return recruiterOfSettlement != null;
		}

		public bool IsRecruiterParty(MobileParty party)
		{
			if (party != null && party.StringId != null)
			{
				return party.StringId.Contains(GarrisonRecruiterPartyID);
			}
			return false;
		}

		public Settlement GetRecruiterPartyHome(MobileParty party)
		{
			int num = party.StringId.IndexOf(GarrisonRecruiterPartyID);
			if (num < 0)
			{
				return null;
			}
			string text = party.StringId.Substring(num, party.StringId.Length - num).Replace(GarrisonRecruiterPartyID, "");
			int num2 = text.IndexOf('_');
			if (num2 > 0)
			{
				text = text.Substring(0, text.IndexOf('_'));
			}
			return Main.GarrisonBehavior.GetSettlementFromName(text);
		}

		public GarrisonRecruiter GetRecruiterForParty(MobileParty party)
		{
			try
			{
				foreach (KeyValuePair<MobileParty, GarrisonRecruiter> garrisonRecruiterParty in GarrisonRecruiterParties)
				{
					if (garrisonRecruiterParty.Key.StringId == party.StringId)
					{
						return garrisonRecruiterParty.Value;
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return null;
		}

		public GarrisonRecruiter GetRecruiterOfSettlement(Settlement settlement)
		{
			List<GarrisonRecruiter> list = new List<GarrisonRecruiter>();
			foreach (GarrisonRecruiter value in GarrisonRecruiterParties.Values)
			{
				if (value.fromSettlement == settlement)
				{
					list.Add(value);
				}
			}
			if (list.Count <= 0)
			{
				return null;
			}
			return list.First();
		}

		public void PromptCultureSelection(Action<List<InquiryElement>> positiveAction)
		{
			try
			{
				List<InquiryElement> list = new List<InquiryElement>();
				List<CultureObject> allCultures = GetAllCultures();
				if (allCultures == null)
				{
					return;
				}
				list.Add(new InquiryElement(null, "Any", new EmptyImageIdentifier()));
				foreach (CultureObject item in allCultures)
				{
					ImageIdentifier imageIdentifier = null;
					foreach (Kingdom item2 in Kingdom.All)
					{
						if (item2.Culture != null && item2.Culture == item)
						{
							imageIdentifier = new BannerImageIdentifier(item2.Banner);
							break;
						}
					}
					if (imageIdentifier == null)
					{
						imageIdentifier = new EmptyImageIdentifier();
					}
					list.Add(new InquiryElement(item, item.StringId, imageIdentifier));
				}
				MultiSelectionInquiryData data = new MultiSelectionInquiryData(new TextObject("{=settings_recruitmentsettings_recruiterculture1}Recruiter recruitment culture").ToString(), new TextObject("{=settings_recruitmentsettings_recruiterculture2}Which culture should the recruiter party recruit from?").ToString(), list, isExitShown: true, 1, 1, new TextObject("{=menu_ok}Ok").ToString(), new TextObject("{=menu_cancel}Cancel").ToString(), positiveAction, null);
				MBInformationManager.ShowMultiSelectionInquiry(data);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public List<CultureObject> GetAllCultures()
		{
			try
			{
				HashSet<CultureObject> hashSet = new HashSet<CultureObject>();
				foreach (Settlement item in Settlement.All)
				{
					if (item.Culture != null && !item.Culture.IsBandit)
					{
						hashSet.Add(item.Culture);
					}
				}
				return hashSet.ToList();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return null;
		}
	}
}
