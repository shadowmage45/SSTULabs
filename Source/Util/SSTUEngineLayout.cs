using System;
using System.Collections.Generic;

namespace SSTUTools
{

    public class SSTUEngineLayout
    {
        public String name = String.Empty;
        public List<SSTUEnginePosition> positions = new List<SSTUEnginePosition>();

        public SSTUEngineLayout(ConfigNode node)
        {
            name = node.GetStringValue("name");
            ConfigNode[] posNodes = node.GetNodes("POSITION");
            foreach (ConfigNode posNode in posNodes)
            {
                positions.Add(new SSTUEnginePosition(posNode));
            }
        }
    }

    /// <summary>
    /// Individual engine position and rotation entry for an engine layout.  There may be many of these in any particular layout.
    /// </summary>
    public class SSTUEnginePosition
    {
        public float x;
        public float z;
        public float rotation;

        public SSTUEnginePosition(ConfigNode node)
        {
            x = node.GetFloatValue("x");
            z = node.GetFloatValue("z");
            rotation = node.GetFloatValue("rotation");
        }

        public float scaledX(float scale)
        {
            return scale * x;
        }

        public float scaledZ(float scale)
        {
            return scale * z;
        }
    }
}
