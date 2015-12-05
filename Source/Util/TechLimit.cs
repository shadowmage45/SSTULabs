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

    public class TechLimitDiameterHeight : TechLimit
    {
        public float maxHeight;
        public float maxDiameter;
        public TechLimitDiameterHeight(ConfigNode node) : base(node)
        {
            maxDiameter = node.GetFloatValue("maxDiameter");
            maxHeight = node.GetFloatValue("maxHeight");
        }
    }


}
