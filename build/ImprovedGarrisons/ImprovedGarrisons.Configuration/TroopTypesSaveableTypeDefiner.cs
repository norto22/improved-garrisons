using ImprovedGarrisons.Upgrade;
using TaleWorlds.SaveSystem;

namespace ImprovedGarrisons.Configuration
{
	internal class TroopTypesSaveableTypeDefiner : SaveableTypeDefiner
	{
		public TroopTypesSaveableTypeDefiner()
			: base(62589786)
		{
		}

		protected override void DefineClassTypes()
		{
			AddClassDefinition(typeof(TroopTypes.Type), 1);
		}

		protected override void DefineContainerDefinitions()
		{
		}
	}
}
