using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager
{
	public class ManagementSettings : ImprovedGarrisonSettings
	{
		private Town _transferTarget;

		private Town _currentTown;

		private static ManagementSettings _instance;

		public static ManagementSettings Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new ManagementSettings();
				}
				return _instance;
			}
			set
			{
				_instance = value;
			}
		}

		public void PromptTransfer(Town fromTown)
		{
			try
			{
				if (!CheckIfTownIsValid(fromTown))
				{
					return;
				}
				if (!fromTown.IsUnderSiege)
				{
					if (fromTown.GarrisonParty != null && fromTown.GarrisonParty.MemberRoster.TotalManCount > 0)
					{
						if (Main.PartyManagement.transferPartyManagement.SettlementHasTransferParty(fromTown.Settlement))
						{
							InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_transfer_dupe}This garrison already has a transfer party. Please wait for it to reach its destination target.").ToString(), Color.FromUint(13897216u)));
							return;
						}
						string title = new TextObject("{=settings_managementsettings_select}Select a garrison").ToString();
						string desc = new TextObject("{=settings_managementsettings_selectdesc}Select the garrison you want to transfer these units to").ToString();
						PromptGarrisonSelector(title, desc, 1, fromTown, Inquirydata_TranferGarrison);
					}
					else
					{
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_emptygarrison}Your garrison is empty.").ToString(), Color.FromUint(13897216u)));
					}
				}
				else
				{
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_transfer_undersiege}This location is currently under siege. The transfer party can't get out!").ToString(), Color.FromUint(13897216u)));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void PromptGarrisonSelector(string title, string desc, int selectableAmount, Town currentTown, Action<List<InquiryElement>> positiveAction)
		{
			try
			{
				if (!CheckIfTownIsValid(currentTown))
				{
					return;
				}
				_currentTown = currentTown;
				List<InquiryElement> list = new List<InquiryElement>();
				Settlement[] array = Enumerable.ToArray(Settlement.All);
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i] != null && array[i].Town != null && (array[i].Town.IsCastle || array[i].Town.IsTown))
					{
						bool flag = array[i].Town != _currentTown;
						if (base.garrisonBehavior.SettlementSettingsData.TryGetValue(array[i].Name.ToString(), out var _) && flag)
						{
							list.Add(new InquiryElement(array[i].Town, array[i].Name.ToString(), new EmptyImageIdentifier()));
						}
					}
				}
				string affirmativeText = new TextObject("{=menu_ok}Okay").ToString();
				string negativeText = new TextObject("{=menu_back}Back").ToString();
				MultiSelectionInquiryData data = new MultiSelectionInquiryData(title, desc, list, isExitShown: true, 0, selectableAmount, affirmativeText, negativeText, positiveAction.Invoke, null);
				MBInformationManager.ShowMultiSelectionInquiry(data);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void PromptCopyToSpecificTowns(Town town)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					_currentTown = town;
					string title = new TextObject("{=settings_managementsettings_copyselction}Copy to garrison").ToString();
					string desc = new TextObject("{=settings_managementsettings_copydesc}Select the garrisons you want to copy the settings of the current garrison to.").ToString();
					PromptGarrisonSelector(title, desc, -1, town, Inquirydata_CopySpecific);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void PromptCopyToAllTowns(Town town)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					_currentTown = town;
					InformationManager.ShowInquiry(new InquiryData(new TextObject("{=settings_managementsettings_copytownselection}Copy these settings to all my towns").ToString(), new TextObject("{=settings_managementsettings_copytownselectiondesc}If you continue, all of your towns will have the same settings as this one.").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: true, new TextObject("{=menu_yes}Yes").ToString(), new TextObject("{=menu_no}No").ToString(), delegate
					{
						CopyToAllTowns(town);
						InformationManager.HideInquiry();
					}, delegate
					{
						InformationManager.HideInquiry();
					}));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void PromptCopyToAllCastles(Town town)
		{
			try
			{
				if (CheckIfTownIsValid(town))
				{
					_currentTown = town;
					InformationManager.ShowInquiry(new InquiryData(new TextObject("{=settings_managementsettings_copycastleselection}Copy these settings to all my castles").ToString(), new TextObject("{=settings_managementsettings_copycastleselectiondesc}If you continue, all of your castles will have the same settings as this one.").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: true, new TextObject("{=menu_yes}Yes").ToString(), new TextObject("{=menu_no}No").ToString(), delegate
					{
						CopyToAllCastles(town);
						InformationManager.HideInquiry();
					}, delegate
					{
						InformationManager.HideInquiry();
					}));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void Inquirydata_TranferGarrison(List<InquiryElement> list)
		{
			try
			{
				if (list == null || list.Count <= 0)
				{
					return;
				}
				_transferTarget = (Town)list.GetRandomElement().Identifier;
				Main.ExecuteActionOnNextTick(delegate
				{
					Settlement settlement = base.garrisonBehavior.CurrentTownForSettings.Settlement;
					PartyBase partyBase = Main.PartyManagement.transferPartyManagement.CreateNewTransferParty(settlement, _transferTarget.Settlement);
					if (partyBase != null)
					{
						Main.PartyManagement.PromptPartyManagementMenu(partyBase, base.garrisonBehavior.CurrentTownForSettings.GarrisonParty);
					}
				});
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void Inquirydata_CopySpecific(List<InquiryElement> list)
		{
			try
			{
				if (list == null || list.Count <= 0)
				{
					return;
				}
				foreach (InquiryElement item in list)
				{
					CopyGarrisonSettings(base.garrisonBehavior.CurrentTownForSettings, (Town)item.Identifier);
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void CopyToAllCastles(Town town)
		{
			try
			{
				GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
				List<KeyValuePair<string, GarrisonSettings>> list = base.garrisonBehavior.SettlementSettingsData.ToList();
				foreach (KeyValuePair<string, GarrisonSettings> item in list)
				{
					Settlement settlementFromName = base.garrisonBehavior.GetSettlementFromName(item.Key);
					if (settlementFromName != null && settlementFromName.IsCastle && settlementFromName.Town != town)
					{
						CopyGarrisonSettings(town, settlementFromName.Town);
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void CopyToAllTowns(Town town)
		{
			try
			{
				GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(town);
				if (townSettings == null)
				{
					return;
				}
				List<KeyValuePair<string, GarrisonSettings>> list = base.garrisonBehavior.SettlementSettingsData.ToList();
				foreach (KeyValuePair<string, GarrisonSettings> item in list)
				{
					Settlement settlementFromName = base.garrisonBehavior.GetSettlementFromName(item.Key);
					if (settlementFromName != null && settlementFromName.IsTown && settlementFromName.Town != town)
					{
						CopyGarrisonSettings(town, settlementFromName.Town);
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private void CopyGarrisonSettings(Town from, Town to)
		{
			try
			{
				GarrisonSettings townSettings = base.garrisonBehavior.GetTownSettings(from);
				GarrisonSettings value = townSettings.clone();
				base.garrisonBehavior.SettlementSettingsData[to.Name.ToString()] = value;
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_manage_copy}Copied settings to").ToString() + str_space + to.Name, Color.FromUint(ModuleColors.green)));
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}
	}
}
