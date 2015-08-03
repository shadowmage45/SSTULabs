using System;
using System.Collections.Generic;

namespace SSTUTools
{
	public class SSTUPropellant
	{
		public String resourceName = "LiquidFuel";
		public float ratio = 1;
		public float normalizedRatio = 1;
		public bool showFuelGuage = true;

		public void loadConfig(ConfigNode node)
		{
			resourceName = node.GetValue ("resource");
			ratio = (float)SSTUUtils.safeParseDouble(node.GetValue ("ratio"));
			showFuelGuage = Boolean.Parse(node.GetValue("showFuelGuage"));
		}
	}

	public class SSTUPropellantList
	{
		public List<SSTUPropellant> propellants = new List<SSTUPropellant>();

		public void addPropellant(ConfigNode node)
		{
			SSTUPropellant p = new SSTUPropellant ();
			p.loadConfig (node);
			propellants.Add (p);
			normalizeRatios ();
		}

		public void normalizeRatios()
		{
			//TODO this actually needs to be based off of resource density, not just ratio
			//so, will need to add up all resource mass * ratio
			//and then normalize to achieve the mass-normalized-ratio
			float totalRatios = 0f;
			float resourceDensity = 1f;
			foreach (SSTUPropellant p in propellants)
			{
				totalRatios += (p.ratio * (1f/resourceDensity));
			}
			totalRatios = 1f / totalRatios;//normalize to a total value of 1
			foreach (SSTUPropellant p in propellants)
			{
				p.normalizedRatio = p.ratio * totalRatios;
			}
		}
	}
}

