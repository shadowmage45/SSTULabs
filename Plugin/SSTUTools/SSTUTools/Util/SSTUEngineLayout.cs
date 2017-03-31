using System;
using UnityEngine;
using System.Collections.Generic;

namespace SSTUTools
{

    public class SSTUEngineLayout
    {
        public String name = String.Empty;
        public List<SSTUEnginePosition> positions = new List<SSTUEnginePosition>();
        public readonly String defaultUpperStageMount = "Mount-None";
        public readonly String defaultLowerStageMount = "Mount-None";
        public readonly float mountSizeMult = 1f;
        public SSTUEngineLayoutMountOption[] mountOptions;

        public SSTUEngineLayout(ConfigNode node)
        {
            name = node.GetStringValue("name");
            mountSizeMult = node.GetFloatValue("mountSizeMult", mountSizeMult);
            defaultUpperStageMount = node.GetStringValue("defaultUpperStageMount", defaultUpperStageMount);
            defaultLowerStageMount = node.GetStringValue("defaultLowerStageMount", defaultLowerStageMount);

            ConfigNode[] posNodes = node.GetNodes("POSITION");
            int len = posNodes.Length;
            for (int i = 0; i < len; i++)
            {
                positions.Add(new SSTUEnginePosition(posNodes[i]));
            }

            ConfigNode[] mountNodes = node.GetNodes("MOUNT");
            len = mountNodes.Length;
            List<SSTUEngineLayoutMountOption> mountOptionsList = new List<SSTUEngineLayoutMountOption>();
            
            string mountName;
            ModelDefinition md;
            for (int i = 0; i < len; i++)
            {
                mountName = mountNodes[i].GetStringValue("name");
                md = SSTUModelData.getModelDefinition(mountName);
                if (md != null)
                {
                    mountOptionsList.Add(new SSTUEngineLayoutMountOption(mountNodes[i]));
                }
                else
                {
                    MonoBehaviour.print("ERROR: Could not locate mount model data for name: " + mountName + " -- please check your configs for errors.");
                }
            }
            mountOptions = mountOptionsList.ToArray();
        }
        
        public static SSTUEngineLayout findLayoutForName(String name)
        {
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("SSTU_ENGINELAYOUT");
            foreach (ConfigNode node in nodes)
            {
                if (node.GetStringValue("name") == name)
                {
                    return new SSTUEngineLayout(node);
                }
            }
            MonoBehaviour.print("ERROR: Could not locate engine layout for name: " + name + ". Please check the spelling and make sure it is defined properly.");
            return null;
        }

        public static SSTUEngineLayout[] getAllLayouts()
        {
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("SSTU_ENGINELAYOUT");
            int len = nodes.Length;
            SSTUEngineLayout[] layouts = new SSTUEngineLayout[len];
            for (int i = 0; i < len; i++)
            {
                layouts[i] = new SSTUEngineLayout(nodes[i]);
            }
            return layouts;
        }

        public static Dictionary<String, SSTUEngineLayout> getAllLayoutsDict()
        {
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("SSTU_ENGINELAYOUT");
            int len = nodes.Length;
            Dictionary<String, SSTUEngineLayout> layouts = new Dictionary<String, SSTUEngineLayout>();
            SSTUEngineLayout layout;
            for (int i = 0; i < len; i++)
            {
                layout = new SSTUEngineLayout(nodes[i]);
                if (!layouts.ContainsKey(layout.name))
                {
                    layouts.Add(layout.name, layout);
                }
            }
            return layouts;
        }
    }

    /// <summary>
    /// Individual engine position and rotation entry for an engine layout.  There may be many of these in any particular layout.
    /// </summary>
    public class SSTUEnginePosition
    {
        public readonly float x;
        public readonly float z;
        public readonly float rotation;
        public readonly float rotationDirection = 1f;

        public SSTUEnginePosition(ConfigNode node)
        {
            x = node.GetFloatValue("x");
            z = node.GetFloatValue("z");
            rotation = node.GetFloatValue("rotation", 0f);
            rotationDirection = node.GetFloatValue("direction", 1f);
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

    public class SSTUEngineLayoutMountOption
    {
        public readonly String mountName;
        public readonly bool upperStage = true;
        public readonly bool lowerStage = true;
        public SSTUEngineLayoutMountOption(ConfigNode node)
        {
            mountName = node.GetStringValue("name");
            upperStage = node.GetBoolValue("upperStage", upperStage);
            lowerStage = node.GetBoolValue("lowerStage", lowerStage);
        }
    }
}
