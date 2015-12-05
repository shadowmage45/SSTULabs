using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{    
    /// <summary>
    /// Mount-definition-link that is defined in the EngineCluster partmodule config node.  Basically just holds name and scale of the full mount definition to use.
    /// </summary>
    public class EngineMount
    {
        /// <summary>
        /// name of the mount definition to load
        /// </summary>
        public String name = String.Empty;

        /// <summary>
        /// List of layout names that are possible for this mount.  If more than one layout is possible, the 'Next Layout' button will be visible in the VAB
        /// </summary>
        public String[] layoutNames = null;

        /// <summary>
        /// The default diameter for this mount option, can be further adjusted between minRadius and maxRadius.
        /// </summary>
        public float defaultDiameter = 5f;

        /// <summary>
        /// minimum selectable diameter for this mount option in the VAB
        /// </summary>
        public float minDiameter = 0.625f;

        /// <summary>
        /// maximum selectable diameter for this mount option in the VAB
        /// </summary>
        public float maxDiameter = 10f;

        /// <summary>
        /// Default spacing for this mount, when mount is switched to this spacing will be applied along with the default mount scale, and first listed layout name
        /// </summary>
        public float engineSpacing = 0f;

        /// <summary>
        /// If user can adjust mount size in VAB
        /// </summary>
        public bool canAdjustSize = true;

        /// <summary>
        /// If the engines should be rotated for
        /// </summary>
        public bool[] rotateEngineModels;

        //local cached reference to the full mount definition for this mount link
        public SSTUEngineMountDefinition mountDefinition = null;

        public EngineMount(ConfigNode node)
        {
            name = node.GetStringValue("name");
            layoutNames = node.GetValues("layoutName");
            defaultDiameter = node.GetFloatValue("size", defaultDiameter);
            minDiameter = node.GetFloatValue("minSize", minDiameter);
            maxDiameter = node.GetFloatValue("maxSize", maxDiameter);
            engineSpacing = node.GetFloatValue("engineSpacing", engineSpacing);
            canAdjustSize = node.GetBoolValue("canAdjustSize", canAdjustSize);
            rotateEngineModels = node.GetBoolValues("rotateEngines");
            mountDefinition = SSTUEngineMountDefinition.getMountDefinition(name);
        }

        public String getNextLayout(String currentLayout, bool iterateBackwards = false)
        {
            return SSTUUtils.findNext(layoutNames, l => l == currentLayout, iterateBackwards);
        }

        public int getLayoutIndex(String layoutName)
        {
            return SSTUUtils.findIndex<String>(layoutNames, l => l == layoutName);
        }
    }

    /// <summary>
    /// Full engine mount definition.  Values listed for height/offset are -pre-scaled- values.
    /// Those values will be scaled by the specific scale factor for the mount when it is actually used in-game by the plugin.
    /// </summary>
    public class SSTUEngineMountDefinition
    {
        private static Dictionary<String, SSTUEngineMountDefinition> mountMap = new Dictionary<string, SSTUEngineMountDefinition>();
        private static bool mapLoaded = false;

        //used by the engine cluster mount-link to locate this mount definition by name
        public String mountName = String.Empty;
        //URL of model to load
        public String modelName = String.Empty;
        //should the model be inverted (rotated 180' around x or z axis)?
        public bool invertModel = false;
        //if model should be cloned per-engine or is a single mount for the whole cluster
        public bool singleModel = true;
        //vertical offset for the mount model itself from origin; scale is applied to this value automatically
        public float verticalOffset = 0f;
        //height to offset engine model by when this mount is used; scale is applied to this value automatically
        public float height = 0f;
        //default is for fairings to be enabled for all mounts, must specifically disable it when needed (for lower-stage mounts)
        public bool fairingDisabled = false;
        //how far from the top of the part is the top of the fairing?  this value is automatically scaled by the mount scale
        public float fairingTopOffset = 0;
        //how much additional mass does each instance of this mount add to the part, at default scale
        public float mountMass = 0;
        //the diameter of the mount at default scale
        public float defaultDiameter = 5f;
        //the usable fuel volume of the mount at default scale
        public float volume = 0;
        //the vertical position of the RCS, relative to part origin
        public float rcsVerticalPosition = 0;
        //the horizontal position of the RCS, relative to part origin
        public float rcsHorizontalPosition = 0;
        //the vertical-axis rotation of the RCS. 0 = aligned on X/Z axis, 45 = halfway around; 90= aligned on X/Z axis again
        public float rcsVerticalRotation = 0;
        //the horizontal-axis rotation of the RCS placement, generally negative for inwards-sloping mounts.
        public float rcsHorizontalRotation = 0;
        //list of attach node positions with the model in its default orientation; these will be auto-inverted/rotated if 'invertModel' is set to true
        public List<AttachNodeData> nodePositions = new List<AttachNodeData>();

        public SSTUEngineMountDefinition(ConfigNode node)
        {
            mountName = node.GetStringValue("name");
            modelName = node.GetStringValue("modelName");
            invertModel = node.GetBoolValue("invertModel", invertModel);
            singleModel = node.GetBoolValue("singleModel", singleModel);
            verticalOffset = node.GetFloatValue("verticalOffset");
            height = node.GetFloatValue("height", height);
            fairingDisabled = node.GetBoolValue("fairingDisabled", fairingDisabled);
            fairingTopOffset = node.GetFloatValue("fairingTopOffset");
            mountMass = node.GetFloatValue("mass", mountMass);
            defaultDiameter = node.GetFloatValue("defaultDiameter", defaultDiameter);
            volume = node.GetFloatValue("volume", volume);
            rcsVerticalPosition = node.GetFloatValue("rcsVerticalPosition", rcsVerticalPosition);
            rcsHorizontalPosition = node.GetFloatValue("rcsHorizontalPosition", rcsHorizontalPosition);
            rcsVerticalRotation = node.GetFloatValue("rcsVerticalRotation");
            rcsHorizontalRotation = node.GetFloatValue("rcsHorizontalRotation");

            if (node.HasValue("node"))
            {
                String[] vals = node.GetStringValues("node");
                foreach (String val in vals) { nodePositions.Add(new AttachNodeData(val)); }
                if (invertModel) { foreach (AttachNodeData data in nodePositions) { data.invert(); } }
            }
        }

        public static void loadMap()
        {
            if (mapLoaded) { return; }
            mountMap.Clear();
            ConfigNode[] mountNodes = GameDatabase.Instance.GetConfigNodes("SSTU_ENGINEMOUNT");
            SSTUEngineMountDefinition mount;
            foreach (ConfigNode mountNode in mountNodes)
            {
                mount = new SSTUEngineMountDefinition(mountNode);
                mountMap.Add(mount.mountName, mount);
            }
        }

        public static SSTUEngineMountDefinition getMountDefinition(String name)
        {
            loadMap();
            SSTUEngineMountDefinition def = null;
            mountMap.TryGetValue(name, out def);
            return def;
        }
    }
    
    public class AttachNodeData
    {
        public Vector3 position;
        public Vector3 orientation;
        public int size;

        public AttachNodeData(String nodeData)
        {
            String[] dataVals = nodeData.Split(new String[] { "," }, StringSplitOptions.None);
            position = new Vector3(SSTUUtils.safeParseFloat(dataVals[0].Trim()), SSTUUtils.safeParseFloat(dataVals[1].Trim()), SSTUUtils.safeParseFloat(dataVals[2].Trim()));
            orientation = new Vector3(SSTUUtils.safeParseFloat(dataVals[3].Trim()), SSTUUtils.safeParseFloat(dataVals[4].Trim()), SSTUUtils.safeParseFloat(dataVals[5].Trim()));
            size = dataVals.Length >= 6 ? SSTUUtils.safeParseInt(dataVals[6]) : 2;
        }

        public AttachNodeData(AttachNodeData toCopy)
        {
            position = toCopy.position.CopyVector();
            orientation = toCopy.orientation.CopyVector();
            size = toCopy.size;
        }

        private AttachNodeData() { }

        public AttachNodeData getInverse()
        {
            AttachNodeData newData = new AttachNodeData();
            newData.size = size;
            newData.orientation = orientation * -1f;
            newData.position = position;
            newData.position.y *= -1;
            newData.position.x *= -1;
            return newData;
        }

        public void invert()
        {
            orientation *= -1;
            position.x *= -1;
            position.y *= -1;
        }
    }

    public class AttachNodeSet
    {
        public String[] nodeNames;
        public AttachNodeSet(String nodeNames)
        {

        }
    }
}
