using System;
namespace SSTUTools
{
	public class SSTUAnimateHeat : ModuleAnimateHeat
	{

		[KSPField]
		public int animationLayer = 0;

		public SSTUAnimateHeat ()
		{
		}

		public override void OnStart (StartState state)
		{
			base.OnStart (state);
			if (heatAnimStates != null && heatAnimStates.Length > 0)
			{
				for(int i = 0; i < heatAnimStates.Length; i++)
				{
					heatAnimStates[i].layer = animationLayer;
				}
			}
		}
	}
}

