using System.Collections.Generic;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Engine.Options;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ConfigOptionsMenu.Options
{
	public class SelectionOptionDataVM : ConfigOptionsMenuItemVM
	{
		private readonly int _initialValue;

		private ISelectionOptionData _selectionOptionData;

		public SelectorVM<SelectorItemVM> _selector;

		[DataSourceProperty]
		public SelectorVM<SelectorItemVM> Selector
		{
			get
			{
				SelectorVM<SelectorItemVM> selector = _selector;
				return _selector;
			}
			set
			{
				if (value != _selector)
				{
					_selector = value;
					OnPropertyChanged("Selector");
				}
			}
		}

		public SelectionOptionDataVM(ISelectionOptionData option, TextObject name, TextObject description, ConfigMenuVM.OptionsDataType typeID)
			: base(option, name, description, typeID)
		{
			_selectionOptionData = option;
			IEnumerable<SelectionData> selectableOptionNames = option.GetSelectableOptionNames();
			List<TextObject> list = new List<TextObject>();
			foreach (SelectionData item2 in selectableOptionNames)
			{
				TextObject item = new TextObject(item2.Data);
				list.Add(item);
			}
			_selector = new SelectorVM<SelectorItemVM>(list, (int)Option.GetValue(forceRefresh: false), UpdateValue);
			_initialValue = (int)Option.GetValue(forceRefresh: false);
			Selector.SelectedIndex = _initialValue;
		}

		public override void RefreshValues()
		{
			base.RefreshValues();
			_selector?.RefreshValues();
		}

		public void UpdateValue(SelectorVM<SelectorItemVM> selector)
		{
			if (selector.SelectedIndex >= 0)
			{
				Option.SetValue(selector.SelectedIndex);
				Option.Commit();
			}
		}

		public override void UpdateValue()
		{
			if (Selector.SelectedIndex >= 0 && (float)Selector.SelectedIndex != Option.GetValue(forceRefresh: false))
			{
				Option.Commit();
			}
		}

		public override void Cancel()
		{
			Selector.SelectedIndex = _initialValue;
			UpdateValue();
		}

		public override void SetValue(float value)
		{
			Selector.SelectedIndex = (int)value;
		}

		public override bool IsChanged()
		{
			return _initialValue != Selector.SelectedIndex;
		}
	}
}
