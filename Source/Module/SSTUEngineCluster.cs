using System;
using UnityEngine;
using System.Collections.Generic;

namespace SSTUTools
{
    public class SSTUEngineCluster : PartModule
    {
        public static Dictionary<String, SSTUEngineLayout> layoutMap = new Dictionary<String, SSTUEngineLayout>();
        public static Dictionary<String, SSTUEngineMountDefinition> mountMap = new Dictionary<string, SSTUEngineMountDefinition>();
        private static bool mapLoaded = false;

        [KSPField]
        public String modelName = String.Empty;

        [KSPField]
        public String layoutName = String.Empty;

        [KSPField]
        public String smokeTransformName = "SmokeTransform";

        [KSPField]
        public float mountSpacing = 3f;

        [KSPField]
        public float partTopY = 0f;

        [KSPField]
        public float engineYOffset = 0f;

        [KSPField]
        public float engineScale = 1f;

        [KSPField]
        public float engineHeight = 1f;

        [KSPField]
        public float smokeTransformOffset = -1f;

        //transforms of these names are removed from the model after it is cloned
        //this is to be used to remove stock fairing transforms from stock engine models
        [KSPField]
        public String transformsToRemove = String.Empty;

        //used to track current mount setup; at least one mount should be defined in the config
        [KSPField(isPersistant = true)]
        public int currentMountOption = 0;

        [Persistent]
        public String configNodeData = String.Empty;

        //below here are private-local tracking fields for various data
        private List<SSTUEngineMount> engineMounts = new List<SSTUEngineMount>();//mount-link-definitions
        public List<GameObject> models = new List<GameObject>();//actual engine models; kept so they can be repositioned //made public in hopes that unity will clone the fields, and that models need not be recreated after prefab is instantiated
        public List<GameObject> mountModels = new List<GameObject>();//mount models, kept so they can be easily deleted //public for unity serialization between prefab...

        //all public fields get serialized from the prefab...hopefully
        public bool engineModelsSetup = false;//don't recreate engine models if they were already setup, it causes problems with other modules
        public float engineY = 0;
        public float fairingTopY = 0;
        public float fairingBottomY = 0;
        public float partDefaultMass = 0;
                
        [KSPEvent(guiName = "Next Mount Type", guiActive = false, guiActiveEditor = true, active = true)]
        public void nextMountEvent()
        {
            int index = currentMountOption;
            index++;
            if (index >= engineMounts.Count) { index = 0; }
            if (index < 0) { index = 0; }
            enableMount(index);

            int moduleIndex = part.Modules.IndexOf(this);
            foreach (Part p in part.symmetryCounterparts)
            {
                ((SSTUEngineCluster)p.Modules[moduleIndex]).enableMount(index);
            }
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            initialize();
            if (engineMounts.Count <= 1)
            {
                Events["nextMountEvent"].active = false;
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
            {
                mapLoaded = false;
            }
            if (node.HasNode("MOUNT"))
            {
                configNodeData = node.ToString();
            }
            initialize();
        }

        /// <summary>
        /// Overriden to provide an opportunity to remove any existing models from the prefab part, so they do not get cloned into live parts
        /// as for some reason they cause issues when cloned in that fashion.
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
            //clearMountModels();
            return "This part may have multiple model variants, right click for more info.";
        }

        /// <summary>
        /// Removes all existing engine models (both created and pre-existing), and any created mounts
        /// TODO - Add capability to remove pre-existing mounts
        /// </summary>
        private void clearExistingModels()
        {
            foreach (GameObject go in models)
            {
                print("destroying existing model: " + go);
                GameObject.Destroy(go);
            }
            models.Clear();
            clearMountModels();
            //catch any existing/leftover engine models cloned from the prefab
            //this -should- allow for re-use of existing engine part.cfg files for patches without needing to change their model node/definition            
            Transform[] trs = part.FindModelTransforms(modelName);
            foreach (Transform tr in trs)
            {
                print("destroying existing model from transform: " + tr.gameObject);
                GameObject.Destroy(tr.gameObject);
            }
        }

        /// <summary>
        /// Deletes all models currently in mount model list, does not touch engine models in any fashion
        /// </summary>
        private void clearMountModels()
        {
            foreach (GameObject go in mountModels)
            {
                GameObject.Destroy(go);
            }
            mountModels.Clear();
        }

        /// <summary>
        /// Runs initialization sequence.  Loads layout/mount definitions from config/files.  Sets up initial engine models and layout, and removes stock transforms.
        /// </summary>
        private void initialize()
        {
            loadMap();
            if (part.partInfo != null && part.partInfo.partPrefab != null)
            {
                partDefaultMass = part.partInfo.partPrefab.mass;
            }
            else
            {
                partDefaultMass = part.mass;
            }
            if (!String.IsNullOrEmpty(configNodeData))
            {
                ConfigNode mountData = SSTUNodeUtils.parseConfigNode(configNodeData);
                ConfigNode[] mountNodes = mountData.GetNodes("MOUNT");
                engineMounts.Clear();
                foreach (ConfigNode mn in mountNodes)
                {
                    engineMounts.Add(new SSTUEngineMount(mn));
                }
            }
            setupEngineModels();
            removeTransforms();
        }

        /// <summary>
        /// Removes the named transforms from the model hierarchy. Removes the entire branch of the tree starting at the named transform.<para/>
        /// This is intended to be used to remove stock ModuleJettison engine fairing transforms,
        /// but may have other use cases as well.  Should function as intended for any model transforms.
        /// </summary>
        private void removeTransforms()
        {
            if (String.IsNullOrEmpty(transformsToRemove)) { return; }
            String[] names = SSTUUtils.parseCSV(transformsToRemove);
            SSTUUtils.removeTransforms(part, names);
        }

        /// <summary>
        /// Sets up the engine models into the intial positions defined by the raw layout config.  Does not handle vertical offset, that is handled through updateModelPositions() method.<para/>
        /// This should only ever be called once for a given PartModule instance, and it should be called during the initial OnStart method.<para/>
        /// Additionally sets up the SmokeTransform for smoke particle effects (this transform will be cloned from prefab into live parts)
        /// </summary>
        private void setupEngineModels()
        {
            //don't replace engine models if they have already been set up; other modules likely depend on the transforms that were added
            if (engineModelsSetup)
            {
                //go ahead and re-enable the mount though...
                enableMount(currentMountOption);
                return;
            }
            engineModelsSetup = true;
            clearExistingModels();
            print("SSTUEngineCluster Cleared existing models from part.  This should only happen during prefab construction.");
            SSTUEngineLayout layout = getEngineLayout();

            GameObject engineModel = GameDatabase.Instance.GetModelPrefab(modelName);
            Transform modelBase = part.FindModelTransform("model");

            GameObject engineClone;
            foreach (SSTUEnginePosition position in layout.positions)
            {
                engineClone = (GameObject)GameObject.Instantiate(engineModel);
                engineClone.name = engineModel.name;
                engineClone.transform.name = engineModel.transform.name;
                engineClone.transform.NestToParent(modelBase);
                engineClone.transform.localScale = new Vector3(engineScale, engineScale, engineScale);
                engineClone.SetActive(true);
                models.Add(engineClone);
            }

            //add the smoke transform point, parented to the model base transform ('model')
            GameObject smokeObject = new GameObject();
            smokeObject.name = smokeTransformName;
            smokeObject.transform.name = smokeTransformName;
            Transform smokeTransform = smokeObject.transform;
            smokeTransform.NestToParent(modelBase);
            smokeTransform.localRotation = Quaternion.AngleAxis(90, new Vector3(1, 0, 0));//set it to default pointing downwards, as-per a thrust transform

            enableMount(currentMountOption);
        }

        /// <summary>
        /// Enable a specific engine mount type by index.  If the given index is not valid, the engine layout will revert to the 'no mount' state (for fairing/engine position/node positions).<para/>
        /// Updates the engine mount positions based on their definitions, and adjusts mass of the part based on the default part mass + mass for the mount.
        /// </summary>
        /// <param name="index"></param>
        private void enableMount(int index)
        {
            //remove existing mount models		
            clearMountModels();

            //update persistence. even if invalid, just leave it; we'll catch invalid stuff below (and on every reload if it stays invalid)
            currentMountOption = index;

            //set up default engine and fairing positions for if mount is invalid
            engineY = partTopY + (engineYOffset * engineScale);
            fairingBottomY = partTopY - (engineHeight * engineScale);
            fairingTopY = partTopY;
            if (index >= engineMounts.Count || index < 0 || engineMounts[index]==null)//invalid selection, set engines to default positioning
            {
                updateEngineModelPositions(this.layoutName, mountSpacing, true);
                updateFairingPosition(true);
                updateNodePositions();
                part.mass = partDefaultMass;
                return;
            }

            SSTUEngineMount mountDef = engineMounts[index];

            float mountY = partTopY + (mountDef.scale * mountDef.mountDefinition.verticalOffset);
            float mountScaledHeight = mountDef.mountDefinition.height * mountDef.scale;
            float localMountSpacing = 0;
            if (mountDef.mountSpacing > 0)
            {
                localMountSpacing = mountDef.mountSpacing;
            }
            else if (mountDef.mountDefinition.mountSpacing > 0 && mountDef.useSpacingOverride)
            {
                localMountSpacing = mountDef.mountDefinition.mountSpacing * mountDef.scale;
            }
            else
            {
                localMountSpacing = mountSpacing;
            }

            engineY -= mountScaledHeight;
            fairingBottomY -= mountScaledHeight;
            String localLayoutName = this.layoutName;
            if (!String.IsNullOrEmpty(mountDef.layoutName))
            {
                localLayoutName = mountDef.layoutName;
            }
            updateEngineModelPositions(localLayoutName, localMountSpacing, mountDef.mountSpacing<=0);
            fairingTopY = partTopY + (mountDef.mountDefinition.fairingTopOffset * mountDef.scale);
            updateFairingPosition(!mountDef.mountDefinition.fairingDisabled);
            updateNodePositions();

            SSTUEngineLayout layout = getEngineLayout(localLayoutName);
            if (mountDef.mountDefinition.singleModel)
            {
                part.mass = partDefaultMass + mountDef.mountDefinition.mountMass;
            }
            else
            {
                part.mass = partDefaultMass + (mountDef.mountDefinition.mountMass * layout.positions.Count);
            }
            if (!String.IsNullOrEmpty(mountDef.mountDefinition.modelName))//has mount model
            {
                GameObject mountModel = GameDatabase.Instance.GetModelPrefab(mountDef.mountDefinition.modelName);
                Transform modelBase = part.FindModelTransform("model");
                if (mountModel == null || modelBase == null) { return; }
                if (mountDef.mountDefinition.singleModel)
                {
                    GameObject mountClone = (GameObject)GameObject.Instantiate(mountModel);
                    mountClone.name = mountModel.name;
                    mountClone.transform.name = mountModel.transform.name;
                    mountClone.transform.NestToParent(modelBase);
                    mountClone.transform.localPosition = new Vector3(0, mountY, 0);
                    mountClone.transform.localRotation = mountDef.mountDefinition.invertModel ? Quaternion.AngleAxis(180, Vector3.forward) : Quaternion.AngleAxis(0, Vector3.up);
                    mountClone.transform.localScale = new Vector3(mountDef.scale, mountDef.scale, mountDef.scale);
                    mountClone.SetActive(true);
                    mountModels.Add(mountClone);
                }
                else
                {
                    float posX, posZ, rot;
                    float spacingScale = engineScale * mountSpacing;
                    GameObject mountClone;
                    foreach (SSTUEnginePosition position in layout.positions)
                    {
                        posX = position.scaledX(spacingScale);
                        posZ = position.scaledZ(spacingScale);
                        rot = position.rotation;
                        mountClone = (GameObject)GameObject.Instantiate(mountModel);
                        mountClone.name = mountModel.name;
                        mountClone.transform.name = mountModel.transform.name;
                        mountClone.transform.NestToParent(modelBase);
                        mountClone.transform.localPosition = new Vector3(posX, mountY, posZ);
                        mountClone.transform.localRotation = Quaternion.AngleAxis(rot, Vector3.up);
                        mountClone.transform.localScale = new Vector3(mountDef.scale, mountDef.scale, mountDef.scale);
                        mountClone.SetActive(true);
                        mountModels.Add(mountClone);
                    }
                }
            }
        }
 
        /// <summary>
        /// Updates the vertical position of the engine models based on the current engineY value.  That value should be pre-computed for scale and verticalOffset value.
        /// </summary>
        private void updateEngineModelPositions(String layoutName, float mountSpacing, bool useScale)
        {
            SSTUEngineLayout layout = null;
            layoutMap.TryGetValue(layoutName, out layout);

            if (layout.positions.Count != getNumOfEngines())
            {
                layout = getEngineLayout();
            }

            float posX, posZ, rot;
            float spacingScale = mountSpacing * (useScale ? engineScale : 1);

            GameObject model;
            SSTUEnginePosition position;
            int length = layout.positions.Count;
            for (int i = 0; i < length; i++)
            {
                position = layout.positions[i];
                model = models[i];
                posX = position.scaledX(spacingScale);
                posZ = position.scaledZ(spacingScale);
                rot = position.rotation;
                model.transform.localPosition = new Vector3(posX, engineY, posZ);
                model.transform.localRotation = Quaternion.AngleAxis(rot, Vector3.up);
            }
                        
            Transform smokeTransform = part.FindModelTransform(smokeTransformName);
            if (smokeTransform != null)
            {
                Vector3 pos = smokeTransform.localPosition;
                pos.y = engineY + (engineScale * smokeTransformOffset);
                smokeTransform.localPosition = pos;
            }
        }

        /// <summary>
        /// Proper method to return number of engines that this engine cluster supports.  To be used by editor-part-group testing code?
        /// </summary>
        /// <returns></returns>
        private int getNumOfEngines()
        {
            SSTUEngineLayout engineLayout = getEngineLayout();
            if (engineLayout != null)
            {
                return engineLayout.positions.Count;
            }
            return 1;
        }

        /// <summary>
        /// 'Safe' method to get the current default engine layout
        /// </summary>
        /// <returns></returns>
        private SSTUEngineLayout getEngineLayout()
        {
            return getEngineLayout(layoutName);
        }

        private SSTUEngineLayout getEngineLayout(String layoutName)
        {
            loadMap();
            SSTUEngineLayout engineLayout = null;
            if (!layoutMap.TryGetValue(layoutName, out engineLayout))
            {
                print("ERROR: Could not locate engine layout for definition name: " + layoutName);
            }
            return engineLayout;
        }

        /// <summary>
        /// Updates attach node position based on the current mount/parameters
        /// </summary>
        private void updateNodePositions()
        {
            AttachNode bottomNode = part.findAttachNode("bottom");
            if (bottomNode == null) { print("ERROR, could not locate bottom node"); return; }
            Vector3 pos = bottomNode.position;
            pos.y = fairingBottomY;
            SSTUUtils.updateAttachNodePosition(part, bottomNode, pos, bottomNode.orientation);
        }

        /// <summary>
        /// Updates the position and enable/disable status of the SSTUNodeFairing (if present). <para/>
        /// Called by VesselModule callback (onVesselLoad()) and whenever the model setup is changed through user input.
        /// </summary>
        private void updateFairingPosition(bool enable)
        {
            print("updating fairing position: " + fairingTopY + ", " + fairingBottomY);
            SSTUNodeFairing fairing = part.GetComponent<SSTUNodeFairing>();
            if (fairing == null) { print("could not update fairing position, module could not be located"); return; }
            if (enable)
            {
                fairing.updateFairingPosition("Fairing", fairingTopY, fairingBottomY);
            }
            fairing.enableFairing(enable);
        }

        /// <summary>
        /// Loads the engine layout definitions from config file in GameData/SSTU/Data/Engines/engineLayouts.cfg.  Rather, it will load any config node with a name of "SSTU_ENGINELAYOUT".
        /// </summary>
		private void loadMap()
        {
            if (mapLoaded) { return; }
            layoutMap.Clear();
            ConfigNode[] layoutNodes = GameDatabase.Instance.GetConfigNodes("SSTU_ENGINELAYOUT");
            SSTUEngineLayout layout;
            foreach (ConfigNode layoutNode in layoutNodes)
            {
                layout = new SSTUEngineLayout(layoutNode);
                layoutMap.Add(layout.name, layout);
            }
            mountMap.Clear();
            ConfigNode[] mountNodes = GameDatabase.Instance.GetConfigNodes("SSTU_ENGINEMOUNT");
            SSTUEngineMountDefinition mount;
            foreach (ConfigNode mountNode in mountNodes)
            {
                mount = new SSTUEngineMountDefinition(mountNode);
                mountMap.Add(mount.mountName, mount);
            }
        }

    }

    /// <summary>
    /// Mount-definition-link that is defined in the EngineCluster partmodule config node.  Basically just holds name and scale of the full mount definition to use.
    /// </summary>
    public class SSTUEngineMount
    {
        //name of the mount definition to load
        public String name = String.Empty;
        //engine layout override for this mount option; -must- have the same number of engines, or will be ignored
        public String layoutName = String.Empty;
        //scale to render model at
        public float scale = 1f;
        //local mount spacing override; will -always- be used if it is >0
        //it is an -unscaled- value, as it is a config-local option; it uses neither engine scale, nor mount scale; and must be scaled manually
        public float mountSpacing = 0f;
        //if should use the spacing override defined in the base mount definition
        public bool useSpacingOverride = true;
        //local cached reference to the full mount definition for this mount link
        public SSTUEngineMountDefinition mountDefinition = null;

        public SSTUEngineMount(ConfigNode node)
        {
            name = node.GetStringValue("name");
            layoutName = node.GetStringValue("layoutName", String.Empty);
            scale = node.GetFloatValue("scale", scale);
            mountSpacing = node.GetFloatValue("mountSpacing", mountSpacing);
            useSpacingOverride = node.GetBoolValue("useSpacingOverride", useSpacingOverride);
            SSTUEngineCluster.mountMap.TryGetValue(name, out mountDefinition);
        }
    }

    /// <summary>
    /// Full engine mount definition.  Values listed for height/offset are -pre-scaled- values.
    /// Those values will be scaled by the specific scale factor for the mount when it is actually used in-game by the plugin.
    /// </summary>
    public class SSTUEngineMountDefinition
    {
        //used by the engine cluster mount-link to locate this mount definition by name
        public String mountName = String.Empty;
        //URL of model to load
        public String modelName = String.Empty;
        //should the model be inverted (rotated 180' around x or z axis)?
        public bool invertModel = false;
        //if model should be cloned per-engine or is a single mount for the whole cluster
        public bool singleModel = false;
        //vertical offset for the mount model itself from origin; scale is applied to this value automatically
        public float verticalOffset = 0f;
        //height to offset engine model by when this mount is used; scale is applied to this value automatically
        public float height = 0f;
        //default is for fairings to be enabled for all mounts, must specifically disable it when needed (for lower-stage mounts)
        public bool fairingDisabled = true;
        //how far from the top of the part is the top of the fairing?  this value is automatically scaled by the mount scale
        public float fairingTopOffset = 0;
        //how much additional mass does each instance of this mount add to the part, at default scale
        public float mountMass = 0;
        //override spacing for engine mount; only needed if mount requires custom spacing layout
        public float mountSpacing = 0f;

        public SSTUEngineMountDefinition(ConfigNode node)
        {
            mountName = node.GetStringValue("name");
            modelName = node.GetStringValue("modelName");
            invertModel = node.GetBoolValue("invertModel");
            singleModel = node.GetBoolValue("singleModel");
            verticalOffset = node.GetFloatValue("verticalOffset");
            height = node.GetFloatValue("height");
            fairingDisabled = node.GetBoolValue("fairingDisabled");
            fairingTopOffset = node.GetFloatValue("fairingTopOffset");
            mountMass = node.GetFloatValue("mass");
            mountSpacing = node.GetFloatValue("mountSpacing");
        }
    }

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

