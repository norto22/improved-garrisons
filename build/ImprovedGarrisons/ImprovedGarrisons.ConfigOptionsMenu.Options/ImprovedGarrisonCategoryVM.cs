using System;
using System.Collections.Generic;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using TaleWorlds.Engine.Options;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ConfigOptionsMenu.Options
{
	public class ImprovedGarrisonCategoryVM : ViewModel
	{
		private readonly TextObject _nameObj;

		private string _name;

		private MBBindingList<ConfigOptionsMenuItemVM> _options;

		[DataSourceProperty]
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
					OnPropertyChanged("Name");
				}
			}
		}

		[DataSourceProperty]
		public MBBindingList<ConfigOptionsMenuItemVM> Options
		{
			get
			{
				return _options;
			}
			set
			{
				if (value != _options)
				{
					_options = value;
					OnPropertyChanged("Options");
				}
			}
		}

		public ImprovedGarrisonCategoryVM(TextObject name, IEnumerable<IOptionData> targetList)
		{
			try
			{
				_options = new MBBindingList<ConfigOptionsMenuItemVM>();
				_nameObj = name;
				foreach (IOptionData target in targetList)
				{
					TextObject name2 = new TextObject((target as OptionUtils).getName());
					TextObject description = new TextObject((target as OptionUtils).getDescription());
					if (target is IBooleanOptionData)
					{
						_options.Add(new ToggleOptionDataVM(target as IBooleanOptionData, name2, description, ConfigMenuVM.OptionsDataType.BooleanOption));
					}
					else if (target is INumericOptionData)
					{
						_options.Add(new NumericOptionDataVM(target as INumericOptionData, name2, description, ConfigMenuVM.OptionsDataType.NumericOption));
					}
					else if (target is ISelectionOptionData)
					{
						_options.Add(new SelectionOptionDataVM(target as ISelectionOptionData, name2, description, ConfigMenuVM.OptionsDataType.MultipleSelectionOption));
					}
					else if (target is TitleText)
					{
						_options.Add(new TitleTextVM(target as TitleText, name2, description, ConfigMenuVM.OptionsDataType.Title));
					}
				}
				RefreshValues();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public override void RefreshValues()
		{
			base.RefreshValues();
			Name = _nameObj.ToString();
			Options.ApplyActionOnAllItems(delegate(ConfigOptionsMenuItemVM x)
			{
				x.RefreshValues();
			});
		}
	}
}
