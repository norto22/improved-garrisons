using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.AI.AIManagers
{
	public class TransferPartyManager
	{
		public string GarrisonTransferPartyID { get; } = "garrisontransferparty_";

		internal Dictionary<MobileParty, Hero> TransferParties { get; } = new Dictionary<MobileParty, Hero>();

		public List<MobileParty> GetAllTransferParties()
		{
			List<MobileParty> list = new List<MobileParty>();
			foreach (MobileParty key in TransferParties.Keys)
			{
				list.Add(key);
			}
			return list;
		}

		public PartyBase CreateNewTransferParty(Settlement fromSettlement, Settlement transferTarget)
		{
			try
			{
				if (fromSettlement == null || transferTarget == null)
				{
					return null;
				}
				string text = fromSettlement.Name.ToString();
				string id = GarrisonTransferPartyID + text;
				TextObject partyName = new TextObject(new TextObject("{=party_transfer_name}Garrison transfer party of").ToString() + ModuleStrings._space + text);
				PartyBase partyBase = Main.PartyManagement.InitializeNewParty(id, partyName, transferTarget, fromSettlement);
				if (partyBase == null)
				{
					return null;
				}
				partyBase.MobileParty.Aggressiveness = 0f;
				if (!TransferParties.ContainsKey(partyBase.MobileParty))
				{
					TransferParties.Add(partyBase.MobileParty, fromSettlement.Owner);
				}
				return partyBase;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return null;
		}

		public void ExecuteHourThinkBehavior()
		{
			foreach (MobileParty item in TransferParties.Keys.ToList())
			{
				if (item != null && item.HomeSettlement != null)
				{
					if (item.CurrentSettlement == item.HomeSettlement)
					{
						Main.PartyManagement.RecruitMobilePartyToGarrison(item, item.HomeSettlement);
						Main.PartyManagement.transferPartyManagement.TransferParties.Remove(item);
					}
					else
					{
						Settlement homeSettlement = item.HomeSettlement;
						Main.GarrisonPartyBehavior.SetMoveGoToSettlementHelper(homeSettlement, item);
					}
					if (item.Food <= 100f)
					{
						Main.PartyManagement.GivePartyFood(item);
					}
				}
			}
		}

		public bool SettlementHasTransferParty(Settlement settlement)
		{
			foreach (MobileParty key in TransferParties.Keys)
			{
				Settlement transferPartyHome = GetTransferPartyHome(key);
				if (transferPartyHome != null && transferPartyHome == settlement)
				{
					return true;
				}
			}
			return false;
		}

		public Settlement GetTransferPartyHome(MobileParty party)
		{
			int num = party.StringId.IndexOf(GarrisonTransferPartyID);
			if (num < 0)
			{
				return null;
			}
			string text = party.StringId.Substring(num, party.StringId.Length - num).Replace(GarrisonTransferPartyID, "");
			int num2 = text.IndexOf('_');
			if (num2 > 0)
			{
				text = text.Substring(0, text.IndexOf('_'));
			}
			return Main.GarrisonBehavior.GetSettlementFromName(text);
		}

		public bool IsTransferParty(MobileParty party)
		{
			if (party != null && party.StringId != null)
			{
				return party.StringId.Contains(GarrisonTransferPartyID);
			}
			return false;
		}
	}
}
