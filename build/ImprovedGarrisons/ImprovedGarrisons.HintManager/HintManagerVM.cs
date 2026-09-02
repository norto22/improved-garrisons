using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.HintManager
{
	public class HintManagerVM : ViewModel
	{
		private DateTime lastUpdateTime = DateTime.Now;

		public MBBindingList<ImprovedGarrisonsHintVM> Hints;

		public ImprovedGarrisonsHintVM CurrentHint
		{
			get
			{
				if (Hints != null)
				{
					if (Index > Hints.Count - 1)
					{
						Index = 0;
					}
					Index = ((Index <= Hints.Count - 1) ? Index : 0);
					Index = ((Index < 0) ? (Hints.Count - 1) : Index);
					return Hints[Index];
				}
				return null;
			}
		}

		private int Index { get; set; }

		public string TipsText { get; } = new TextObject("{=tipstext}Tips").ToString();

		public HintManagerVM()
		{
			InitializeHints();
		}

		private void InitializeHints()
		{
			Hints = new MBBindingList<ImprovedGarrisonsHintVM>();
			Hints.Add(new ImprovedGarrisonsHintVM(new TextObject("{=tips_tip1}Any garrison can recruit and train template troops for you.").ToString()));
			Hints.Add(new ImprovedGarrisonsHintVM(new TextObject("{=tips_tip2}Guard parties can join you during sieges.").ToString()));
			Hints.Add(new ImprovedGarrisonsHintVM(new TextObject("{=tips_tip3}A training template can be used to specify the upgrade target path of any garrison.").ToString()));
			Hints.Add(new ImprovedGarrisonsHintVM(new TextObject("{=tips_tip4}Clicking on the icon in the overview section tracks the village or settlement.").ToString()));
			Hints.Add(new ImprovedGarrisonsHintVM(new TextObject("{=tips_tip5}You can copy the Improved Garrison settings from one garrison to another.").ToString()));
			Hints.Add(new ImprovedGarrisonsHintVM(new TextObject("{=tips_tip6}The configuration manager is used to change many of this mod's values. It can be opened by pressing (ALT + G).").ToString()));
			Hints.Add(new ImprovedGarrisonsHintVM(new TextObject("{=tips_tip7}Recruiters can automatically collect troops depending on your current training template.").ToString()));
			Hints.Add(new ImprovedGarrisonsHintVM(new TextObject("{=tips_tip8}The garrison wages can be disabled in the cheat section of the Improved Garrison settings.").ToString()));
			Hints.Add(new ImprovedGarrisonsHintVM(new TextObject("{=tips_tip9}If set up, a garrison can automatically send a guard party to defend raided villages.").ToString()));
			Hints.Add(new ImprovedGarrisonsHintVM(new TextObject("{=tips_tip10}A new guard party can be automatically created after the last one has been destroyed.").ToString()));
			Hints.Add(new ImprovedGarrisonsHintVM(new TextObject("{=tips_tip11}Troops train faster if there are more units of one type.").ToString()));
			Hints.Add(new ImprovedGarrisonsHintVM(new TextObject("{=tips_tip12}Having trouble with prosperity and food shortage? Try out the food gathering cheat in the mods configuration!").ToString()));
			ShuffleList(Hints);
			Index = new Random().Next(0, Hints.Count - 1);
		}

		public void ExecuteNextHint()
		{
			Index++;
			OnPropertyChanged("CurrentHint");
		}

		public void ExecutePreviousHint()
		{
			Index--;
			OnPropertyChanged("CurrentHint");
		}

		public void ShuffleList(IList<ImprovedGarrisonsHintVM> list)
		{
			Random random = new Random();
			int count = list.Count;
			for (int num = list.Count - 1; num > 1; num--)
			{
				int index = random.Next(num + 1);
				ImprovedGarrisonsHintVM value = list[index];
				list[index] = list[num];
				list[num] = value;
			}
		}

		public override void RefreshValues()
		{
			base.RefreshValues();
			DateTime t = DateTime.Now.AddMilliseconds(-20000.0);
			if (DateTime.Compare(t, lastUpdateTime) > 0)
			{
				ExecuteNextHint();
				lastUpdateTime = DateTime.Now;
			}
		}
	}
}
