using System.Collections.Generic;
using ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI.CascadeMenuElements;
using SandBox.View.Map;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ScreenSystem;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.CascadeMenuUI
{
	[OverrideView(typeof(CascadeMenuView))]
	public class CascadeMenuGauntlet : MapView
	{
		public bool cascadeMenuIsOpen = false;

		public bool cascadeLevelIsAboveTwo = false;

		private List<CascadeMenu> currentCascadeMenus;

		public int currentCascadeLevel { get; private set; } = -1;

		public CascadeMenuGauntlet()
		{
			currentCascadeMenus = new List<CascadeMenu>();
		}

		protected override void CreateLayout()
		{
			base.CreateLayout();
		}

		public void InitializeAndPushCascadeMenu(CascadeMenu cascadeMenu)
		{
			CreateLayout();
			AddNewCascadeMenu(cascadeMenu);
			cascadeMenu.Initialize();
			MapScreen.Instance.AddLayer(cascadeMenu.GauntletLayer);
			ScreenManager.TrySetFocus(cascadeMenu.GauntletLayer);
			if (!cascadeMenuIsOpen)
			{
				cascadeMenuIsOpen = true;
			}
			else
			{
				cascadeLevelIsAboveTwo = true;
			}
		}

		public void InitializeAndPushCascadeMenu(string title, MBBindingList<CascadeMenuElementVM> elements, int suggestedWidth = -1)
		{
			CreateLayout();
			CascadeMenu cascadeMenu = new CascadeMenu(title, elements, suggestedWidth);
			AddNewCascadeMenu(cascadeMenu);
			cascadeMenu.Initialize();
			MapScreen.Instance.AddLayer(cascadeMenu.GauntletLayer);
			ScreenManager.TrySetFocus(cascadeMenu.GauntletLayer);
			if (!cascadeMenuIsOpen)
			{
				cascadeMenuIsOpen = true;
			}
			else
			{
				cascadeLevelIsAboveTwo = true;
			}
		}

		private void AddNewCascadeMenu(CascadeMenu menu)
		{
			currentCascadeMenus.Add(menu);
			currentCascadeLevel++;
		}

		public CascadeMenu TryGetPreviousCascadeMenu()
		{
			if (currentCascadeLevel - 1 >= 0)
			{
				return currentCascadeMenus[currentCascadeLevel - 1];
			}
			return null;
		}

		public CascadeMenu GetLatestCascadeMenu()
		{
			return currentCascadeMenus[currentCascadeLevel];
		}

		public void CloseCascadeMenu()
		{
			if (cascadeMenuIsOpen)
			{
				MapScreen.Instance.RemoveMapView(this);
				currentCascadeMenus = new List<CascadeMenu>();
				cascadeMenuIsOpen = false;
				cascadeLevelIsAboveTwo = false;
				currentCascadeLevel = -1;
			}
		}

		public void Tick()
		{
			if (Input.IsKeyReleased(InputKey.Escape))
			{
				CloseCascadeMenu();
			}
		}

		protected override void OnFinalize()
		{
			base.OnFinalize();
			foreach (CascadeMenu currentCascadeMenu in currentCascadeMenus)
			{
				currentCascadeMenu.OnFinalize();
			}
			cascadeMenuIsOpen = false;
		}
	}
}
