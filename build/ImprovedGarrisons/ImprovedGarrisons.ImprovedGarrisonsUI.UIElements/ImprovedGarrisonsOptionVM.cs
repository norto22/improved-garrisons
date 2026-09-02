using System;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.UIElements
{
    public class ImprovedGarrisonsOptionVM : ViewModel
    {
        private HintViewModel _hint;

        private bool _optionBooleanValue = false;

        private Action<bool> _onBooleanChangeAction;

        private Func<bool> _refreshBooleanValue;

        private float _optionFloatValue = 0f;

        private Action<float> _onFloatChangeAction;

        private Func<float> _refreshFloatValue;

        private float mousePositionX = 0f;

        private float mousePositionY = 0f;

        public int OptionTypeID { get; set; }

        public string Description { get; set; }

        public bool IsDiscrete { get; set; }

        public float Max { get; set; }

        public float Min { get; set; }

        public string ButtonName { get; set; }

        public Action OnPressAction { get; set; }

        public SelectorVM<SelectorItemVM> SelectorDatasource { get; set; }

        public bool OptionValueAsBoolean
        {
            get
            {
                return _optionBooleanValue;
            }
            set
            {
                if (value != _optionBooleanValue)
                {
                    _optionBooleanValue = value;
                    OnPropertyChanged("OptionValueAsBoolean");
                    _onBooleanChangeAction(value);
                }
            }
        }

        public float OptionValueAsFloat
        {
            get
            {
                return _optionFloatValue;
            }
            set
            {
                if (value != _optionFloatValue)
                {
                    mousePositionX = Input.MouseMoveX;
                    mousePositionY = Input.MouseMoveY;
                    _optionFloatValue = value;
                    OnPropertyChanged("OptionValueAsFloat");
                    OnPropertyChanged("OptionFloatValueAsString");
                    _onFloatChangeAction(value);
                }
            }
        }

        public string OptionFloatValueAsString => ((int)_optionFloatValue).ToString();

        public HintViewModel Hint
        {
            get
            {
                return _hint;
            }
            set
            {
                if (value != _hint)
                {
                    _hint = value;
                    OnPropertyChangedWithValue(value, "Hint");
                }
            }
        }

        public ImprovedGarrisonsOptionVM SetAsBooleanOption(string desc, bool initialValue, Action<bool> onChange, TextObject hintText = null)
        {
            try
            {
                OptionTypeID = 1;
                Description = desc;
                _optionBooleanValue = initialValue;
                _onBooleanChangeAction = onChange;
                if (hintText != null)
                {
                    Hint = new HintViewModel(hintText);
                }
            }
            catch (Exception ex)
            {
                LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
            }
            return this;
        }

        public ImprovedGarrisonsOptionVM SetAsBooleanOption(string desc, Func<bool> refreshValue, Action<bool> onChange, TextObject hintText = null)
        {
            _refreshBooleanValue = refreshValue;
            return SetAsBooleanOption(desc, refreshValue != null && refreshValue(), onChange, hintText);
        }

        public ImprovedGarrisonsOptionVM SetAsSliderOption(string text, float initialValue, float min, float max, bool discrete, Action<float> onChange, TextObject hintText = null)
        {
            try
            {
                Description = text;
                OptionTypeID = 2;
                _optionFloatValue = initialValue;
                _onFloatChangeAction = onChange;
                Max = max;
                Min = min;
                IsDiscrete = discrete;
                if (hintText != null)
                {
                    Hint = new HintViewModel(hintText);
                }
            }
            catch (Exception ex)
            {
                LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
            }
            return this;
        }

        public ImprovedGarrisonsOptionVM SetAsSliderOption(string text, Func<float> refreshValue, float min, float max, bool discrete, Action<float> onChange, TextObject hintText = null)
        {
            _refreshFloatValue = refreshValue;
            return SetAsSliderOption(text, (refreshValue != null) ? refreshValue() : 0f, min, max, discrete, onChange, hintText);
        }

        public ImprovedGarrisonsOptionVM SetAsButtonOption(string buttonName, Action onPress, TextObject hintText = null)
        {
            try
            {
                OptionTypeID = 3;
                ButtonName = buttonName;
                OnPressAction = onPress;
                if (hintText != null)
                {
                    Hint = new HintViewModel(hintText);
                }
            }
            catch (Exception ex)
            {
                LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
            }
            return this;
        }

        public ImprovedGarrisonsOptionVM SetAsDropdownOption(string selecorName, SelectorVM<SelectorItemVM> selector, TextObject hintText = null)
        {
            try
            {
                OptionTypeID = 4;
                Description = selecorName;
                SelectorDatasource = selector;
                if (hintText != null)
                {
                    Hint = new HintViewModel(hintText);
                }
            }
            catch (Exception ex)
            {
                LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
            }
            return this;
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            if (_refreshBooleanValue != null)
            {
                bool optionBooleanValue = _refreshBooleanValue();
                if (optionBooleanValue != _optionBooleanValue)
                {
                    _optionBooleanValue = optionBooleanValue;
                    OnPropertyChanged("OptionValueAsBoolean");
                }
            }
            if (_refreshFloatValue != null)
            {
                float optionFloatValue = _refreshFloatValue();
                if (optionFloatValue != _optionFloatValue)
                {
                    _optionFloatValue = optionFloatValue;
                    OnPropertyChanged("OptionValueAsFloat");
                    OnPropertyChanged("OptionFloatValueAsString");
                }
            }
        }

        public void OnPress()
        {
            if (OnPressAction != null)
            {
                OnPressAction();
            }
        }

        public ImprovedGarrisonsOptionVM SetAsTitle(string title, TextObject hintText = null)
        {
            OptionTypeID = 0;
            Description = title;
            if (hintText != null)
            {
                Hint = new HintViewModel(hintText);
            }
            return this;
        }
    }
}
