using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSTUTools
{
    public class TechLimit
    {
        public String name = String.Empty;

        public TechLimit(ConfigNode node)
        {
            name = node.GetStringValue("name");
        }

        public bool isUnlocked()
        {
            return SSTUUtils.isTechUnlocked(name);
        }
    }

    public class TechLimitDiameter : TechLimit
    {
        public float maxDiameter;
        public TechLimitDiameter(ConfigNode node) : base(node)
        {
            maxDiameter = node.GetFloatValue("maxDiameter");
        }

        public static void updateTechLimits(TechLimitDiameter[] limits, out float maxDiameter)
        {
            maxDiameter = float.PositiveInfinity;
            if (!SSTUUtils.isResearchGame()) { return; }
            if (HighLogic.CurrentGame == null) { return; }
            maxDiameter = 0;
            foreach (TechLimitDiameter limit in limits)
            {
                if (limit.isUnlocked())
                {
                    if (limit.maxDiameter > maxDiameter) { maxDiameter = limit.maxDiameter; }
                }
            }
        }

        public static TechLimitDiameter[] loadTechLimits(ConfigNode[] nodes)
        {
            int len = nodes.Length;
            TechLimitDiameter[] techLimits = new TechLimitDiameter[len];
            for (int i = 0; i < len; i++)
            {
                techLimits[i] = new TechLimitDiameter(nodes[i]);
            }
            return techLimits;
        }
    }


}
