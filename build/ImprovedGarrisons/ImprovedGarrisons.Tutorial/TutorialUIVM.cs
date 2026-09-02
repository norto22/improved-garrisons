using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.Tutorial
{
	public class TutorialUIVM : ViewModel
	{
		private MBBindingList<TutorialItemUIVM> _items;

		private string _forwardButtonText;

		private string _backwardsButtonText;

		private Action _backwardsButtonAction;

		private Action _forwardsButtonAction;

		private int _positionYOffset = 0;

		private int _positionXOffset = 0;

		public MBBindingList<TutorialItemUIVM> Items
		{
			get
			{
				return _items;
			}
			set
			{
				if (_items != value)
				{
					_items = value;
					OnPropertyChanged("Items");
				}
			}
		}

		public string ForwardButtonText
		{
			get
			{
				return _forwardButtonText;
			}
			set
			{
				if (_forwardButtonText != value)
				{
					_forwardButtonText = value;
					OnPropertyChanged("ForwardButtonText");
				}
			}
		}

		public string BackwardsButtonText
		{
			get
			{
				return _backwardsButtonText;
			}
			set
			{
				if (_backwardsButtonText != value)
				{
					_backwardsButtonText = value;
					OnPropertyChanged("BackwardsButtonText");
				}
			}
		}

		public Action BackwardsButtonAction
		{
			get
			{
				return _backwardsButtonAction;
			}
			set
			{
				if (_backwardsButtonAction != value)
				{
					_backwardsButtonAction = value;
					OnPropertyChanged("BackwardsButtonAction");
				}
			}
		}

		public Action ForwardsButtonAction
		{
			get
			{
				return _forwardsButtonAction;
			}
			set
			{
				if (_forwardsButtonAction != value)
				{
					_forwardsButtonAction = value;
					OnPropertyChanged("ForwardsButtonAction");
				}
			}
		}

		public int PositionYOffset
		{
			get
			{
				return _positionYOffset;
			}
			set
			{
				if (_positionYOffset != value)
				{
					_positionYOffset = value;
					OnPropertyChangedWithValue(value, "PositionYOffset");
				}
			}
		}

		public int PositionXOffset
		{
			get
			{
				return _positionXOffset;
			}
			set
			{
				if (_positionXOffset != value)
				{
					_positionXOffset = value;
					OnPropertyChangedWithValue(value, "PositionXOffset");
				}
			}
		}

		public string TutorialTitle { get; } = new TextObject("{=tutorial_title}Improved Garrisons tutorial").ToString();

		public TutorialUIVM(MBBindingList<TutorialItemUIVM> items, string forwardButton, string backwardButton, Action forwardAction, Action backwardAction)
		{
			NewWindow(items, forwardButton, backwardButton, forwardAction, backwardAction);
		}

		private void NewWindow(MBBindingList<TutorialItemUIVM> items, string forwardButton, string backwardButton, Action forwardAction, Action backwardAction)
		{
			Items = items;
			ForwardButtonText = forwardButton;
			BackwardsButtonText = backwardButton;
			ForwardsButtonAction = forwardAction;
			BackwardsButtonAction = backwardAction;
		}

		public void OnForwardButtonClick()
		{
			ForwardsButtonAction();
		}

		public void OnBackwardButtonClick()
		{
			BackwardsButtonAction();
		}
	}
}
