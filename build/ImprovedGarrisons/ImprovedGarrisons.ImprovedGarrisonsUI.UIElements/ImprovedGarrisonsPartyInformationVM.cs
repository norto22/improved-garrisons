using System;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.UIElements
{
	public class ImprovedGarrisonsPartyInformationVM : ViewModel
	{
		private Action<MobileParty> onPressAction;

		private string _name;

		private string _troopAmount;

		private ImageIdentifierVM _visual;

		private float _distanceInTimeFloat;

		public MobileParty Party { get; private set; }

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

		public string TroopAmount
		{
			get
			{
				return _troopAmount;
			}
			set
			{
				if (value != _troopAmount)
				{
					_troopAmount = value;
					OnPropertyChangedWithValue(value, "TroopAmount");
				}
			}
		}

		public ImageIdentifierVM Visual
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

		public string DistanceInTime => DistanceInTimeFloat + " " + new TextObject("{=misc_hour}h").ToString();

		public float DistanceInTimeFloat
		{
			get
			{
				return _distanceInTimeFloat;
			}
			set
			{
				if (value != _distanceInTimeFloat)
				{
					_distanceInTimeFloat = value;
					OnPropertyChangedWithValue(value, "DistanceInTimeFloat");
					OnPropertyChanged("DistanceInTime");
				}
			}
		}

		public ImprovedGarrisonsPartyInformationVM(MobileParty party, Settlement settlementForDistance, Action<MobileParty> onPressAction)
		{
			if (party != null)
			{
				Party = party;
				Name = ((party.Party.Name != null) ? party.Party.Name.ToString() : party.GetName().ToString());
				SetVisualsForParty(party);
				SetTroopAmountStringForParty(party);
				SetDistance(party, settlementForDistance);
				this.onPressAction = onPressAction;
			}
		}

		public void ExecuteClick()
		{
			if (onPressAction != null)
			{
				onPressAction(Party);
			}
		}

		private void SetVisualsForParty(MobileParty party)
		{
			if (party == null)
			{
				return;
			}
			Tuple<CharacterObject, int> tuple = new Tuple<CharacterObject, int>(null, 0);
			if (party.LeaderHero != null)
			{
				tuple = new Tuple<CharacterObject, int>(party.LeaderHero.CharacterObject, -1);
			}
			else
			{
				foreach (TroopRosterElement item in party.MemberRoster.GetTroopRoster())
				{
					if (tuple.Item1 == null || item.Number > tuple.Item2)
					{
						tuple = new Tuple<CharacterObject, int>(item.Character, item.Number);
					}
				}
			}
			if (tuple.Item1 != null)
			{
				ImageIdentifierVM visual = null;
				try
				{
					visual = new CharacterImageIdentifierVM(CampaignUIHelper.GetCharacterCode(tuple.Item1));
				}
				catch (Exception)
				{
				}
				Visual = visual;
			}
		}

		private void SetTroopAmountStringForParty(MobileParty party)
		{
			if (party != null)
			{
				int totalManCount = party.MemberRoster.TotalManCount;
				int totalWounded = party.MemberRoster.TotalWounded;
				TroopAmount = (totalManCount - totalWounded).ToString();
				if (totalWounded > 0)
				{
					TroopAmount = TroopAmount + "+" + totalWounded + "w";
				}
			}
		}

		private void SetDistance(MobileParty party, Settlement settlement)
		{
			try
			{
				if (party == null || settlement == null)
				{
					DistanceInTimeFloat = 0f;
					return;
				}
				Main.GarrisonPartyBehavior.DetermineNavigationForSettlement(party, settlement, out var navigationType, out var isTargetingThePort);
				float estimatedLandRatio;
				float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(party, settlement, isTargetingThePort, navigationType, out estimatedLandRatio);
				if (distance <= 0f || float.IsNaN(distance) || float.IsInfinity(distance))
				{
					DistanceInTimeFloat = 0f;
					return;
				}
				float num = ((Party != null) ? Party.Speed : party.Speed);
				if (num <= 0f)
				{
					DistanceInTimeFloat = 0f;
					return;
				}
				float distanceInTimeFloat = TaleWorlds.Library.MathF.Ceiling(distance / num);
				DistanceInTimeFloat = distanceInTimeFloat;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}
	}
}
