using System;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ConfigOptionsMenu.Options
{
	[Serializable]
	public abstract class OptionUtils
	{
		private string _name;

		private string _description;

		protected float _value;

		private string name;

		private string description;

		public OptionUtils(string name, string description, float currentValue, string extraDescription = null, string requirements = null)
		{
			_name = name;
			_description = description;
			_value = currentValue;
			if (extraDescription != null && extraDescription.Length > 0)
			{
				_description = _description + "\n \n \n" + new TextObject("{=menu_description}Description:").ToString() + "\n" + extraDescription;
			}
			if (requirements != null && requirements.Length > 0)
			{
				_description = _description + "\n \n \n" + new TextObject("{=menu_requirements}Requirements:").ToString() + "\n" + requirements;
			}
		}

		protected OptionUtils(string name, string description)
		{
			this.name = name;
			this.description = description;
		}

		internal OptionUtils(string name)
		{
		}

		public string getName()
		{
			return _name;
		}

		public string getDescription()
		{
			return _description;
		}

		public float GetDefaultValue()
		{
			return 0f;
		}

		public float GetValue()
		{
			return _value;
		}

		public float GetValue(bool forceRefresh)
		{
			return _value;
		}

		public void SetValue(float value)
		{
			_value = value;
		}
	}
}
