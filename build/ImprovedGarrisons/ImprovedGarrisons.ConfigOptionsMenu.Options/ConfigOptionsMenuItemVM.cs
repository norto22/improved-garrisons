using TaleWorlds.Engine.Options;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ConfigOptionsMenu.Options
{
	public abstract class ConfigOptionsMenuItemVM : ViewModel
	{
		private TextObject _nameObj;

		protected IOptionData Option;

		private TextObject _descriptionObj;

		private int _optionTypeId = -1;

		private string[] _imageIDs;

		[DataSourceProperty]
		public string Name
		{
			get
			{
				return _nameObj.ToString();
			}
			set
			{
				_nameObj = new TextObject(value);
				OnPropertyChanged("Name");
			}
		}

		[DataSourceProperty]
		public string Description
		{
			get
			{
				return _descriptionObj.ToString();
			}
			set
			{
				_descriptionObj = new TextObject(value);
				OnPropertyChanged("Description");
			}
		}

		[DataSourceProperty]
		public string[] ImageIDs
		{
			get
			{
				return _imageIDs;
			}
			set
			{
				if (value != _imageIDs)
				{
					_imageIDs = value;
					OnPropertyChanged("ImageIDs");
				}
			}
		}

		[DataSourceProperty]
		public int OptionTypeID
		{
			get
			{
				return _optionTypeId;
			}
			set
			{
				if (value != _optionTypeId)
				{
					_optionTypeId = value;
					OnPropertyChanged("OptionTypeID");
				}
			}
		}

		public ConfigOptionsMenuItemVM(IOptionData option, TextObject name, TextObject description, ConfigMenuVM.OptionsDataType typeID)
		{
			_nameObj = name;
			Option = option;
			OptionTypeID = (int)typeID;
			_descriptionObj = description;
			RefreshValues();
		}

		public override void RefreshValues()
		{
			base.RefreshValues();
		}

		public void ExecuteAction()
		{
		}

		public abstract void UpdateValue();

		public abstract void Cancel();

		public abstract bool IsChanged();

		public abstract void SetValue(float value);
	}
}
