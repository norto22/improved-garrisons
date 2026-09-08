using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.UIElements
{
    // This is a read-only projection of stationed troops, never a template editor.
    public class ActualGarrisonVM : ViewModel
    {
        public MBBindingList<ActualGarrisonTroopVM> Troops { get; } = new MBBindingList<ActualGarrisonTroopVM>();

        public string Title { get; } = new TextObject("{=ui_trainingui_actualgarrison}Actual garrison").ToString();

        public string EmptyText { get; } = new TextObject("{=ui_trainingui_garrisonempty}No stationed troops").ToString();

        public int TotalCount { get; private set; }

        public int Capacity { get; private set; }

        public bool IsEmpty => Troops.Count == 0;

        public string CountText => TotalCount + " / " + Capacity;

        public void RefreshRoster(TroopRoster roster, int capacity)
        {
            bool wasEmpty = IsEmpty;
            int total = 0;
            HashSet<CharacterObject> present = new HashSet<CharacterObject>();
            if (roster != null)
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    TroopRosterElement troop = roster.GetElementCopyAtIndex(i);
                    if (troop.Character == null || troop.Number <= 0)
                    {
                        continue;
                    }
                    total += troop.Number; // Number already includes wounded troops and heroes.
                    present.Add(troop.Character);
                    ActualGarrisonTroopVM row = null;
                    foreach (ActualGarrisonTroopVM existing in Troops)
                    {
                        if (existing.Character == troop.Character)
                        {
                            row = existing;
                            break;
                        }
                    }
                    if (row == null)
                    {
                        row = new ActualGarrisonTroopVM(troop.Character);
                        Troops.Add(row);
                    }
                    row.RefreshCount(troop.Number, troop.WoundedNumber);
                }
            }
            for (int i = Troops.Count - 1; i >= 0; i--)
            {
                if (!present.Contains(Troops[i].Character))
                {
                    Troops.RemoveAt(i);
                }
            }
            capacity = Math.Max(0, capacity);
            if (total != TotalCount || capacity != Capacity)
            {
                TotalCount = total;
                Capacity = capacity;
                OnPropertyChanged("TotalCount");
                OnPropertyChanged("Capacity");
                OnPropertyChanged("CountText");
            }
            if (wasEmpty != IsEmpty)
            {
                OnPropertyChanged("IsEmpty");
            }
        }
    }

    public class ActualGarrisonTroopVM : ViewModel
    {
        internal CharacterObject Character { get; }

        public string Name { get; }

        public int Count { get; private set; }

        public int WoundedCount { get; private set; }

        public string CountText { get; private set; }

        public ActualGarrisonTroopVM(CharacterObject character)
        {
            Character = character;
            Name = character.Name.ToString();
        }

        internal void RefreshCount(int count, int woundedCount)
        {
            if (Count == count && WoundedCount == woundedCount)
            {
                return;
            }
            Count = count;
            WoundedCount = woundedCount;
            TextObject text = new TextObject(woundedCount > 0
                ? "{=ui_trainingui_garrisoncountwounded}{COUNT} troops ({WOUNDED} wounded)"
                : "{=ui_trainingui_garrisoncount}{COUNT} troops");
            text.SetTextVariable("COUNT", count);
            text.SetTextVariable("WOUNDED", woundedCount);
            CountText = text.ToString();
            OnPropertyChanged("Count");
            OnPropertyChanged("WoundedCount");
            OnPropertyChanged("CountText");
        }
    }
}
