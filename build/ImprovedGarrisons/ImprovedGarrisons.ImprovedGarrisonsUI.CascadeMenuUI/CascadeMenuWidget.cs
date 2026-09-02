using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI
{
	public class CascadeMenuWidget : BrushWidget
	{
		private bool positionIsSet = false;

		public CascadeMenuWidget(UIContext context)
			: base(context)
		{
		}

		protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
		{
			base.OnRender(twoDimensionContext, drawContext);
			bool flag = UIManager.Instance.cascadeMenuGauntlet != null && UIManager.Instance.cascadeMenuGauntlet.cascadeMenuIsOpen;
			if (!(!positionIsSet && flag))
			{
				return;
			}
			CascadeMenuGauntlet cascadeMenuGauntlet = UIManager.Instance.cascadeMenuGauntlet;
			if (!cascadeMenuGauntlet.cascadeLevelIsAboveTwo)
			{
				Vec2 mousePositionPixel = Input.MousePositionPixel;
				base.ScaledPositionXOffset = mousePositionPixel.x - base.ScaledSuggestedWidth;
				base.ScaledPositionYOffset = mousePositionPixel.y - base.Size.Y / 2f;
				positionIsSet = true;
			}
			else
			{
				CascadeMenu cascadeMenu = cascadeMenuGauntlet.TryGetPreviousCascadeMenu();
				if (cascadeMenu != null && cascadeMenu.cascadeMenuWidget != null)
				{
					Widget cascadeMenuWidget = cascadeMenu.cascadeMenuWidget;
					base.ScaledPositionXOffset = cascadeMenuWidget.ScaledPositionXOffset - base.ScaledSuggestedWidth;
					base.ScaledPositionYOffset = cascadeMenuWidget.ScaledPositionYOffset;
					positionIsSet = true;
				}
			}
			if (base.ScaledPositionYOffset + base.Size.Y >= Input.Resolution.Y)
			{
				float num = base.ScaledPositionYOffset + base.Size.Y - Input.Resolution.Y;
				base.ScaledPositionYOffset = base.ScaledPositionYOffset - num - 15f;
			}
			cascadeMenuGauntlet.GetLatestCascadeMenu().cascadeMenuWidget = this;
		}
	}
}
