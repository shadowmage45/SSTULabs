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

    public class TechLimitHeightDiameter : TechLimit
    {
        public float maxHeight;
        public float maxDiameter;
        public TechLimitHeightDiameter(ConfigNode node) : base(node)
        {
            maxDiameter = node.GetFloatValue("maxDiameter");
            maxHeight = node.GetFloatValue("maxHeight");
        }

        public static void updateTechLimits(TechLimitHeightDiameter[] limits, out float maxHeight, out float maxDiameter)
        {
            maxHeight = float.PositiveInfinity;
            maxDiameter = float.PositiveInfinity;
            if (!SSTUUtils.isResearchGame()) { return; }
            if (HighLogic.CurrentGame == null) { return; }
            maxHeight = 0;
            maxDiameter = 0;
            foreach (TechLimitHeightDiameter limit in limits)
            {
                if (limit.isUnlocked())
                {
                    if (limit.maxHeight > maxHeight) { maxHeight = limit.maxHeight; }
                    if (limit.maxDiameter > maxDiameter) { maxDiameter = limit.maxDiameter; }
                }
            }
        }

        public static TechLimitHeightDiameter[] loadTechLimits(ConfigNode[] nodes)
        {
            int len = nodes.Length;
            TechLimitHeightDiameter[] techLimits = new TechLimitHeightDiameter[len];
            for (int i = 0; i < len; i++)
            {
                techLimits[i] = new TechLimitHeightDiameter(nodes[i]);
            }
            return techLimits;
        }
    }


}
