using System.Collections.Generic;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Options;
using TaleWorlds.TwoDimension;

namespace ImprovedGarrisons.ConfigOptionsMenu
{
	public class ConfigMenuWidget : Widget
	{
		private Widget _currentOptionWidget;

		private bool _initialized;

		public RichTextWidget CurrentOptionDescriptionWidget { get; set; }

		public RichTextWidget CurrentOptionNameWidget { get; set; }

		public Widget CurrentOptionImageWidget { get; set; }

		public ConfigMenuWidget(UIContext context)
			: base(context)
		{
		}

		protected override void OnUpdate(float dt)
		{
			base.OnUpdate(dt);
			if (_initialized)
			{
				return;
			}
			using (IEnumerator<Widget> enumerator = base.Children.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (enumerator.Current is ConfigMenuItemWidget configMenuItemWidget)
					{
						configMenuItemWidget.SetCurrentScreenWidget(this);
					}
				}
			}
			_initialized = true;
		}

		private void OnActiveTabChange()
		{
		}

		protected override void OnDisconnectedFromRoot()
		{
			base.OnDisconnectedFromRoot();
		}

		public void SetCurrentOption(Widget currentOptionWidget, Sprite newgraphicsSprite)
		{
			if (_currentOptionWidget != currentOptionWidget)
			{
				_currentOptionWidget = currentOptionWidget;
				string text = "";
				string text2 = "";
				if (_currentOptionWidget != null)
				{
					if (_currentOptionWidget is ConfigMenuItemWidget configMenuItemWidget)
					{
						text = configMenuItemWidget.OptionDescription;
						text2 = configMenuItemWidget.OptionTitle;
					}
					else if (_currentOptionWidget is OptionsKeyItemListPanel optionsKeyItemListPanel)
					{
						text = optionsKeyItemListPanel.OptionDescription;
						text2 = optionsKeyItemListPanel.OptionTitle;
					}
				}
				if (CurrentOptionDescriptionWidget != null)
				{
					CurrentOptionDescriptionWidget.Text = text;
				}
				if (CurrentOptionDescriptionWidget != null)
				{
					CurrentOptionNameWidget.Text = text2;
				}
			}
			if (CurrentOptionImageWidget != null && CurrentOptionImageWidget.Sprite != newgraphicsSprite)
			{
				CurrentOptionImageWidget.Sprite = newgraphicsSprite;
				if (newgraphicsSprite != null)
				{
					float num = CurrentOptionImageWidget.SuggestedWidth / (float)newgraphicsSprite.Width;
					CurrentOptionImageWidget.SuggestedHeight = (float)newgraphicsSprite.Height * num;
				}
			}
		}
	}
}
