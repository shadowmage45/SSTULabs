using System;
using UnityEngine;
using System.Collections.Generic;

namespace SSTUTools
{
    public class SSTUEngineCluster : PartModule
    {
        //maps are public only for accessibility by the mountdef/etc classes
        public static Dictionary<String, SSTUEngineLayout> layoutMap = new Dictionary<String, SSTUEngineLayout>();
        private static bool mapLoaded = false;

        /// <summary>
        /// The URL of the model to use for this engine cluster
        /// </summary>
        [KSPField]
        public String modelName = String.Empty;

        /// <summary>
        /// Mounts will be parented to this transform; makes it easier to tell what models to delete, and which to keep.  This transform should only be generated the first time the part is initialized (prefab OnLoad).
        /// </summary>
        [KSPField]
        public String mountTransformName = "SSTEngineClusterMounts";

        /// <summary>
        /// The default engine layout to use if none is specified in the mount option(s)
        /// </summary>
        [KSPField]
        public String defaultLayoutName = String.Empty;

        /// <summary>
        /// The default engine spacing if none is defined in the mount definition
        /// </summary>
        [KSPField]
        public float defaultEngineSpacing = 3f;

        /// <summary>
        /// The default mount option.  When the part is initialized, this mount will be used for the in-editor model/etc.
        /// </summary>
        [KSPField]
        public String defaultMount = "None";

        /// <summary>
        /// A transform of this name will be added to the main model, at a position determined by mount height + smokeTransformOffset
        /// </summary>
        [KSPField]
        public String smokeTransformName = "SmokeTransform";

        /// <summary>
        /// Determines the position at which the smoke transform is added to the model.  This is an offset from the engine mounting position.  Should generally be >= engine height.
        /// </summary>
        [KSPField]
        public float smokeTransformOffset = -1f;

        /// <summary>
        /// This determines the top node position of the part, in part-relative space.  All other fields/values/positions are updated relative to this position.  Should generally be set at ~1/2 of engine height.
        /// </summary>
        [KSPField]
        public float partTopY = 0f;

        /// <summary>
        /// The scale to render the engine model at.  engineYOffset and engineHeight will both be scaled by this value
        /// </summary>
        [KSPField]
        public float engineScale = 1f;

        /// <summary>
        /// The height of the engine model at normal scale.  This should be the distance from the top mounting plane to the bottom of the engine (where the attach-node should be)
        /// </summary>
        [KSPField]
        public float engineHeight = 1f;

        /// <summary>
        /// This field determines how much vertical offset should be given to the engine model (to correct for the default-COM positioning of stock/other mods engine models).        
        /// A positive value will move the model up, a negative value moves it down.
        /// Should be the value of the distance between part origin and the top mounting plane of the part, as a negative value (as you are moving the engine model downward to place the mounting plane at COM/origin)
        /// </summary>
        [KSPField]
        public float engineYOffset = 0f;

        /// <summary>
        /// CSV list of transform names
        /// transforms of these names are removed from the model after it is cloned
        /// this is to be used to remove stock fairing transforms from stock engine models (module should be removed by the same patch that is making the custom cluster)
        /// </summary>
        [KSPField]
        public String transformsToRemove = String.Empty;

        [KSPField]
        public String interstageNodeName = "interstage";

        /// <summary>
        /// How much to increment the diameter with every step of the main diameter slider
        /// </summary>
        [KSPField]
        public float diameterMainIncrement = 0.625f;

        #region editor adjustment fields

        /// <summary>
        /// Used for fine adjustment (inbetween stack sizes) for the mount size/scale
        /// </summary>
        [KSPField(guiName = "Mount Size Adjust", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0f, maxValue = 1, stepIncrement = 0.1f)]
        public float editorMountSizeAdjust = 0f;

        /// <summary>
        /// Used for adjusting the inter-engine spacing
        /// </summary>
        [KSPField(guiName = "Engine Spacing Adjust", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0.25f, maxValue = 2, stepIncrement = 0.05f)]
        public float editorEngineSpacingAdjust = 1f;

        /// <summary>
        /// Determines the y-position of the engine model (and node position/fairing position).  Can be used to offset an engine inside of its included mount.
        /// </summary>
        [KSPField(guiName = "Engine Height Adjust", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = -2.0f, maxValue = 2f, stepIncrement = 0.10f)]
        public float editorEngineHeightAdjust = 0f;

        #endregion

        #region persistent field values, should not be edited in config

        /// <summary>
        /// This is the currently selected mount.  Field is updated whenever the mount model is changed.  Populated initially with value of 'defaultMount'.
        /// </summary>
        [KSPField(isPersistant = true)]
        public String currentMountName = String.Empty;              

        /// <summary>
        /// Determines the current scale of the mount, persistent value.  Indirectly edited by user in the VAB
        /// </summary>
        [KSPField (isPersistant = true)]
        public float currentMountSize = 1;

        /// <summary>
        /// Determines the spacing between each engine.  This is the not intended for config editing, and is set by the values in the mount options.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentEngineSpacing = 3f;

        /// <summary>
        /// Currently enabled engine layout, set from the default mount option, and may be user-editable in VAB if multiple layouts are enabled for that mount option
        /// </summary>
        [KSPField(isPersistant = true)]
        public String currentEngineLayout = String.Empty;

        [KSPField(isPersistant = true)]
        public float currentEngineVerticalOffset = 0f;

        [KSPField(isPersistant = true)]
        public bool fairingInitialized = false;

        #endregion

        /// <summary>
        /// Hack around stock not passing the prefab config nodes back in at any point after init, just save them as text/reparse when needed.
        /// </summary>
        [Persistent]
        public String configNodeData = String.Empty;

        //below here are private-local tracking fields for various data
        private List<MountModelData> engineMounts = new List<MountModelData>();//mount-link-definitions
        private MountModelData currentMountOption = null;
        private float editorMountSize = 0;
        private float prevMountSizeAdjust = 0;
        private float prevEngineSpacingAdjust = 0;
        private float prevEngineHeightAdjust = 0;

        [Persistent]
        //all public fields get serialized from the prefab...hopefully
        public bool engineModelsSetup = false;//don't recreate engine models if they were already setup, it causes problems with other modules (all of them...)
        public float engineY = 0;
        public float fairingTopY = 0;
        public float fairingBottomY = 0;
        public float partDefaultMass = 0;

        [KSPEvent(guiName = "Clear Mount Type", guiActive = false, guiActiveEditor = true, active = true)]
        public void clearMountEvent()
        {
            int index = SSTUUtils.findIndex(engineMounts, m=>m.name=="Mount-None");
            if (index >= 0)
            {
                enableMount(index, true);
                updateFairing();
                updateGuiState();

                int moduleIndex = part.Modules.IndexOf(this);
                foreach (Part p in part.symmetryCounterparts)
                {
                    ((SSTUEngineCluster)p.Modules[moduleIndex]).enableMount(index, true);
                    ((SSTUEngineCluster)p.Modules[moduleIndex]).updateFairing();
                    ((SSTUEngineCluster)p.Modules[moduleIndex]).updateGuiState();
                }
            }
        }

        [KSPEvent(guiName = "Prev Mount Type", guiActive = false, guiActiveEditor = true, active = true)]
        public void prevMountEvent()
        {
            int index = getNextMountIndex(currentMountName, true);
            enableMount(index, true);
            updateFairing();
            updateGuiState();

            int moduleIndex = part.Modules.IndexOf(this);
            foreach (Part p in part.symmetryCounterparts)
            {
                ((SSTUEngineCluster)p.Modules[moduleIndex]).enableMount(index, true);
                ((SSTUEngineCluster)p.Modules[moduleIndex]).updateFairing();
                ((SSTUEngineCluster)p.Modules[moduleIndex]).updateGuiState();
            }
        }

        [KSPEvent(guiName = "Next Mount Type", guiActive = false, guiActiveEditor = true, active = true)]
        public void nextMountEvent()
        {
            int index = getNextMountIndex(currentMountName);
            enableMount(index, true);
            updateFairing();
            updateGuiState();

            int moduleIndex = part.Modules.IndexOf(this);
            foreach (Part p in part.symmetryCounterparts)
            {
                ((SSTUEngineCluster)p.Modules[moduleIndex]).enableMount(index, true);
                ((SSTUEngineCluster)p.Modules[moduleIndex]).updateFairing();
                ((SSTUEngineCluster)p.Modules[moduleIndex]).updateGuiState();
            }
        }

        [KSPEvent(guiName = "Next Engine Layout", guiActive = false, guiActiveEditor = true, active = true)]
        public void nextLayoutEvent()
        {            
            currentEngineLayout = currentMountOption.getNextLayout(currentEngineLayout);
            updateMountPositions(true);

            int moduleIndex = part.Modules.IndexOf(this);
            foreach (Part p in part.symmetryCounterparts)
            {
                ((SSTUEngineCluster)p.Modules[moduleIndex]).currentEngineLayout = currentEngineLayout;
                ((SSTUEngineCluster)p.Modules[moduleIndex]).updateMountPositions(true);
            }

        }

        [KSPEvent(guiName = "Mount Size --", guiActive = false, guiActiveEditor = true, active = true)]
        public void prevSizeEvent()
        {
            editorMountSize -= diameterMainIncrement;
            if (editorMountSize < currentMountOption.minDiameter) { editorMountSize = currentMountOption.minDiameter; }
            updateMountSizeFromEditor();
            updateFairing();
            foreach (Part p in part.symmetryCounterparts)
            {
                ((SSTUEngineCluster)p.Modules[part.Modules.IndexOf(this)]).editorMountSize = editorMountSize;
                ((SSTUEngineCluster)p.Modules[part.Modules.IndexOf(this)]).updateMountSizeFromEditor();
            }
        }

        [KSPEvent(guiName = "Mount Size ++", guiActive = false, guiActiveEditor = true, active = true)]
        public void nextSizeEvent()
        {            
            editorMountSize += diameterMainIncrement;
            if (editorMountSize > currentMountOption.maxDiameter) { editorMountSize = currentMountOption.maxDiameter; }
            updateMountSizeFromEditor();
            updateFairing();
            foreach (Part p in part.symmetryCounterparts)
            {
                ((SSTUEngineCluster)p.Modules[part.Modules.IndexOf(this)]).editorMountSize = editorMountSize;
                ((SSTUEngineCluster)p.Modules[part.Modules.IndexOf(this)]).updateMountSizeFromEditor();
            }
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            initialize();
            updateGuiState();
            restoreEditorFields();
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
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

        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        public void onEditorVesselModified(ShipConstruct ship)
        {
            if (!HighLogic.LoadedSceneIsEditor) { return; }
            bool updated = false;
            if (prevMountSizeAdjust != editorMountSizeAdjust)
            {
                prevMountSizeAdjust = editorMountSizeAdjust;
                updateMountSizeFromEditor();

                SSTUEngineCluster module;
                int moduleIndex = part.Modules.IndexOf(this);
                foreach (Part p in part.symmetryCounterparts)
                {
                    module = (SSTUEngineCluster)p.Modules[moduleIndex];
                    module.prevMountSizeAdjust = module.editorMountSizeAdjust = editorMountSizeAdjust;
                    module.editorMountSize = editorMountSize;                    
                    module.updateMountSizeFromEditor();
                }
                updated = true;
            }
            if (prevEngineSpacingAdjust != editorEngineSpacingAdjust)
            {
                prevEngineSpacingAdjust = editorEngineSpacingAdjust;
                updateEngineSpacingFromEditor();

                SSTUEngineCluster module;
                int moduleIndex = part.Modules.IndexOf(this);
                foreach (Part p in part.symmetryCounterparts)
                {
                    module = (SSTUEngineCluster)p.Modules[moduleIndex];
                    module.prevEngineSpacingAdjust = module.editorEngineSpacingAdjust = editorEngineSpacingAdjust;
                    updateEngineSpacingFromEditor();
                }
                updated = true;
            }
            if (prevEngineHeightAdjust != editorEngineHeightAdjust)
            {
                prevEngineHeightAdjust = editorEngineHeightAdjust;
                currentEngineVerticalOffset = editorEngineHeightAdjust;
                updateMountPositions(true);
                updated = true;

                SSTUEngineCluster module;
                int moduleIndex = part.Modules.IndexOf(this);
                foreach (Part p in part.symmetryCounterparts)
                {
                    module = (SSTUEngineCluster)p.Modules[moduleIndex];
                    module.prevEngineHeightAdjust = module.editorEngineHeightAdjust = editorEngineHeightAdjust;
                    updateMountPositions(true);
                }
            }
            if (!updated)
            {
                updateNodePositions(true);
            }
        }

        /// <summary>
        /// Overriden to provide an opportunity to remove any existing models from the prefab part, so they do not get cloned into live parts
        /// as for some reason they cause issues when cloned in that fashion.
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {            
            return "This part may have multiple model variants, right click for more info.";
        }

        public void Start()
        {
            if (!fairingInitialized && HighLogic.LoadedSceneIsEditor)
            {
                fairingInitialized = true;
                updateFairing();
            }
        }

        /// <summary>
        /// Restores the editor-adjustment values from the current/persistent tank size data
        /// Should only be called when a new mount is selected, or the part is fist initialized in the editor
        /// </summary>
        private void restoreEditorFields()
        {
            float div = currentMountSize / diameterMainIncrement;
            float whole = (int)div;
            float extra = div - whole;
            editorMountSize = whole * diameterMainIncrement;
            editorMountSizeAdjust = extra;
            prevMountSizeAdjust = extra;

            float defSpacing = defaultEngineSpacing;
            if (currentMountOption.engineSpacing > 0) { defSpacing = currentMountOption.engineSpacing; }
            float scale = currentEngineSpacing / defSpacing;
            editorEngineSpacingAdjust = scale;
            prevEngineSpacingAdjust = editorEngineSpacingAdjust;

            editorEngineHeightAdjust = currentEngineVerticalOffset;
        }

        /// <summary>
        /// Updates the 'current' values based on the in-editor value updates, and calls updateMountPositions (which repositions everything else)
        /// </summary>
        private void updateEngineSpacingFromEditor()
        {
            float defSpacing = defaultEngineSpacing;
            if (currentMountOption.engineSpacing > 0) { defSpacing = currentMountOption.engineSpacing; }
            currentEngineSpacing = editorEngineSpacingAdjust * defSpacing;
            updateMountPositions(true);
        }

        /// <summary>
        /// local gui-interface method for updating size
        /// </summary>
        /// <param name="newSize"></param>
        private void updateMountSizeFromEditor()
        {
            currentMountSize = editorMountSize + diameterMainIncrement * editorMountSizeAdjust;
            if (currentMountSize > currentMountOption.maxDiameter) { currentMountSize = currentMountOption.maxDiameter; }
            if (currentMountSize < currentMountOption.minDiameter) { currentMountSize = currentMountOption.minDiameter; }
            updateMountPositions(true);
        }

        /// <summary>
        /// Updates the context-menu GUI buttons/etc as the config of the part changes.
        /// </summary>
        private void updateGuiState()
        {
            Events["nextMountEvent"].active = engineMounts.Count>1;
            Events["nextLayoutEvent"].active = currentMountOption.layoutNames.Length > 1;

            BaseField sizeAdjustSecondary = Fields["editorMountSizeAdjust"];
            sizeAdjustSecondary.guiActiveEditor = currentMountOption.canAdjustSize;

            Events["prevSizeEvent"].active = currentMountOption.canAdjustSize;
            Events["nextSizeEvent"].active = currentMountOption.canAdjustSize;
        }

        /// <summary>
        /// Removes all existing engine models (both created and pre-existing), and any created mounts
        /// TODO - Add capability to remove pre-existing mounts
        /// </summary>
        private void clearExistingModels()
        {
            clearMountModels();
        }

        /// <summary>
        /// Deletes all models currently in mount model list, does not touch engine models in any fashion
        /// </summary>
        private void clearMountModels()
        {
            SSTUUtils.destroyChildren(part.transform.FindRecursive(mountTransformName));
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
                ConfigNode mountData = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
                ConfigNode[] mountNodes = mountData.GetNodes("MOUNT");
                engineMounts.Clear();
                foreach (ConfigNode mn in mountNodes)
                {
                    engineMounts.Add(new MountModelData(mn));
                }
            }
            Transform tr = part.transform.FindRecursive("model").FindOrCreate(mountTransformName);
            tr.NestToParent(part.transform.FindRecursive("model"));
            tr.localScale = Vector3.one;
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
            int mountIndex = String.IsNullOrEmpty(currentMountName) ? getMountIndex(defaultMount) : getMountIndex(currentMountName);

            if (engineModelsSetup)
            {
                //go ahead and re-enable the mount though...
                enableMount(mountIndex, false);
                return;
            }

            engineModelsSetup = true;

            clearExistingModels();

            SSTUEngineLayout layout = getEngineLayout(defaultLayoutName);
            currentEngineLayout = defaultLayoutName;
            currentEngineSpacing = defaultEngineSpacing;

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
            }

            //add the smoke transform point, parented to the model base transform ('model')
            GameObject smokeObject = new GameObject();
            smokeObject.name = smokeTransformName;
            smokeObject.transform.name = smokeTransformName;
            Transform smokeTransform = smokeObject.transform;
            smokeTransform.NestToParent(modelBase);
            smokeTransform.localRotation = Quaternion.AngleAxis(90, new Vector3(1, 0, 0));//set it to default pointing downwards, as-per a thrust transform            
            enableMount(mountIndex, false);
        }

        /// <summary>
        /// Enable a specific engine mount type by index.  If the given index is not valid, the engine layout will revert to the 'no mount' state (for fairing/engine position/node positions).<para/>
        /// Updates the engine mount positions based on their definitions, and adjusts mass of the part based on the default part mass + mass for the mount.
        /// </summary>
        /// <param name="index"></param>
        private void enableMount(int index, bool userInput)
        {
            //basic vars setup for enabling the mount
            currentMountOption = engineMounts[index];
            //determine if this mount is already enabled (e.g. being called during OnStart); if already enabled, use the current layout spacing and mount scale values rather than defaults
            bool init = currentMountName != currentMountOption.name;
            currentMountName = currentMountOption.name;
            currentMountSize = init? currentMountOption.defaultDiameter : currentMountSize;
            currentEngineSpacing = init? (currentMountOption.engineSpacing > 0 ? currentMountOption.engineSpacing : defaultEngineSpacing) : currentEngineSpacing;

            restoreEditorFields();//this updates the editor adjust values for the new-updated mount size

            bool hasLayout = currentMountOption.layoutNames != null && currentMountOption.layoutNames.Length > 0;
            String localLayoutName = init ? (hasLayout ? currentMountOption.layoutNames[0] : defaultLayoutName) : (currentEngineLayout);
            SSTUEngineLayout layout = getEngineLayout(localLayoutName);
            currentEngineLayout = localLayoutName;

            String modelName = currentMountOption.modelDefinition.modelName;
            int numOfModels = currentMountOption.singleModel ? 1 : layout.positions.Count;
            if (!String.IsNullOrEmpty(modelName))//has mount model
            {
                Transform[] potentialMountModels = part.transform.FindChildren(modelName);
                if (potentialMountModels.Length != numOfModels)
                {
                    clearMountModels();
                    GameObject mountModel = GameDatabase.Instance.GetModelPrefab(modelName);
                    Transform modelBase = part.transform.FindRecursive(mountTransformName);
                    if (mountModel == null || modelBase == null) { return; }
                    if (currentMountOption.singleModel)
                    {
                        GameObject mountClone = (GameObject)GameObject.Instantiate(mountModel);
                        mountClone.name = mountModel.name;
                        mountClone.transform.name = mountModel.transform.name;
                        mountClone.transform.NestToParent(modelBase);
                        mountClone.SetActive(true);
                    }
                    else
                    {
                        GameObject mountClone;
                        foreach (SSTUEnginePosition position in layout.positions)
                        {
                            mountClone = (GameObject)GameObject.Instantiate(mountModel);
                            mountClone.name = mountModel.name;
                            mountClone.transform.name = mountModel.transform.name;
                            mountClone.transform.NestToParent(modelBase);
                            mountClone.SetActive(true);
                        }
                    }
                }
            }
            else
            {
                clearMountModels();
            }

            //update the current mount positions and cached vars for stuff like fairing and engine position
            updateMountPositions(userInput);
            
            if (currentMountOption.singleModel)
            {
                part.mass = partDefaultMass + currentMountOption.modelDefinition.mass;
            }
            else
            {
                part.mass = partDefaultMass + (currentMountOption.modelDefinition.mass * layout.positions.Count);
            }
        }
 
        /// <summary>
        /// Updates the vertical position of the engine models based on the current engineY value.  That value should be pre-computed for scale and verticalOffset value.
        /// </summary>
        private void updateEngineModelPositions(String layoutName)
        {
            SSTUEngineLayout layout = null;
            layoutMap.TryGetValue(layoutName, out layout);

            if (layout.positions.Count != getNumOfEngines())
            {
                layout = getEngineLayout();
            }

            float posX, posZ, rot;
            GameObject model;
            SSTUEnginePosition position;
            int length = layout.positions.Count;

            bool rotateEngines = false;
            int layoutIndex = currentMountOption.getLayoutIndex(layoutName);
            if (layoutIndex >= 0 && layoutIndex < currentMountOption.rotateEngineModels.Length)
            {
                rotateEngines = currentMountOption.rotateEngineModels[layoutIndex];
            }
            else if (currentMountOption.rotateEngineModels.Length >= 1)//catches the case of rotating the engines on the default engine layout
            {
                rotateEngines = currentMountOption.rotateEngineModels[0];
            }

            Transform[] models = part.transform.FindRecursive("model").FindChildren(modelName);
            
            for (int i = 0; i < length; i++)
            {
                position = layout.positions[i];
                model = models[i].gameObject;
                posX = position.scaledX(currentEngineSpacing);
                posZ = position.scaledZ(currentEngineSpacing);
                rot = position.rotation;
                if (rotateEngines) { rot += 180; }
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
        /// Positions the existing mount models according the the current mount scale.  Subsequently calls updateEnginePositions, updateFairingPosition, and updateNodePosition as a result of the model udpates.
        /// </summary>
        private void updateMountPositions(bool userInput)
        {            
            SSTUEngineLayout layout = null;
            layoutMap.TryGetValue(currentEngineLayout, out layout);
            float posX, posZ, rot;
            float currentMountScale = getCurrentMountScale();
            float mountY = partTopY + (currentMountScale * currentMountOption.modelDefinition.verticalOffset);
            int len = layout.positions.Count;
            if (currentMountOption.singleModel) { len = 1; }
            Transform[] mountModels = part.transform.FindChildren(currentMountOption.modelDefinition.modelName);
            if (len > mountModels.Length) { len = mountModels.Length; }
            GameObject mountModel = null;
            SSTUEnginePosition position;
            for (int i = 0; i < len; i++)
            {
                position = layout.positions[i];
                mountModel = mountModels[i].gameObject;
                posX = currentMountOption.singleModel ? 0 : position.scaledX(currentEngineSpacing);
                posZ = currentMountOption.singleModel ? 0 : position.scaledZ(currentEngineSpacing);
                rot = currentMountOption.singleModel ? 0 : position.rotation;
                mountModel.transform.localPosition = new Vector3(posX, mountY, posZ);
                mountModel.transform.localRotation = currentMountOption.modelDefinition.invertForBottom ? Quaternion.AngleAxis(180, Vector3.forward) : Quaternion.AngleAxis(0, Vector3.up);
                mountModel.transform.localScale = new Vector3(currentMountScale, currentMountScale, currentMountScale);
            }

            //set up fairing/engine/node positions
            float mountScaledHeight = currentMountOption.modelDefinition.height * currentMountScale;
            fairingTopY = partTopY + (currentMountOption.modelDefinition.fairingTopOffset * currentMountScale);
            engineY = partTopY + (engineYOffset * engineScale) - mountScaledHeight + editorEngineHeightAdjust;
            fairingBottomY = partTopY - (engineHeight * engineScale) - mountScaledHeight + editorEngineHeightAdjust;

            //set up engine positions based on current/default/mount layout and if this is running during init or not
            updateEngineModelPositions(currentEngineLayout);
            
            //udpate attach node positions based on values calced in updateMountPositions()
            updateNodePositions(userInput);

            SSTUModInterop.onPartGeometryUpdate(part, true);
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
            return getEngineLayout(currentEngineLayout);
        }

        /// <summary>
        /// Retrieve a specific engine layout by name.
        /// </summary>
        /// <param name="layoutName"></param>
        /// <returns></returns>
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
        private void updateNodePositions(bool userInput)
        {
            AttachNode bottomNode = part.findAttachNode("bottom");
            if (bottomNode == null) { print("ERROR, could not locate bottom node"); return; }
            Vector3 pos = bottomNode.position;
            pos.y = fairingBottomY;
            SSTUAttachNodeUtils.updateAttachNodePosition(part, bottomNode, pos, bottomNode.orientation, userInput);

            AttachNode interstage = part.findAttachNode(interstageNodeName);
            if (interstage != null)
            {
                float y = partTopY + (currentMountOption.modelDefinition.fairingTopOffset * getCurrentMountScale());
                pos = new Vector3(0, y, 0);
                Vector3 orientation = new Vector3(0, -1, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, interstage, pos, orientation, userInput);
            }
        }

        /// <summary>
        /// Updates the position and enable/disable status of the SSTUNodeFairing (if present). <para/>
        /// </summary>
        private void updateFairing()
        {
            SSTUNodeFairing fairing = part.GetComponent<SSTUNodeFairing>();
            if (fairing == null) { return; }
            else if (!fairing.initialized()) { return; }
            bool enable = !currentMountOption.modelDefinition.fairingDisabled;
            fairing.canDisableInEditor = enable;
            if (enable)
            {
                fairing.setFairingTopY(fairingTopY);
                fairing.setFairingTopRadius(currentMountSize * 0.5f);
                fairing.setFairingBottomRadius(currentMountSize * 0.5f);
            }
            fairing.enableFairingFromEditor(enable);
        }

        /// <summary>
        /// Loads the engine layout definitions from config file in GameData/SSTU/Data/Engines/engineLayouts.cfg.  Rather, it will load any config node with a name of "SSTU_ENGINELAYOUT".
        /// </summary>
		public static void loadMap()
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
        }

        /// <summary>
        /// Returns the next mount index from the list of available mounts, given the name of the currently enabled mount.
        /// </summary>
        /// <param name="currentMountName"></param>
        /// <param name="iterateBackwards"></param>
        /// <returns></returns>
        private int getNextMountIndex(String currentMountName, bool iterateBackwards = false)
        {
            int len = engineMounts.Count;
            if (len == 0) { return 0; }//error...
            int iter = iterateBackwards ? -1 : 1;
            int index = getMountIndex(currentMountName);
            index += iter;
            if (index < 0) { index = len - 1; }
            if (index >= len) { index = 0; }
            return index;
        }

        /// <summary>
        /// Returns the index of the currently enabled mount option, or zero if invalid
        /// </summary>
        /// <param name="currentMountName"></param>
        /// <returns></returns>
        private int getMountIndex(String currentMountName)
        {
            int len = engineMounts.Count;            
            int index = -1;
            for (int i = 0; i < len; i++)
            {
                if (engineMounts[i].name == currentMountName)
                {
                    index = i;
                    break;
                }
            }
            if (index == -1)
            {
                //could not find, return 0;
                return 0;
            }
            return index;
        }

        /// <summary>
        /// Returns the current mount scale; calculated by the current user-set size and the default size specified in definition file
        /// </summary>
        /// <returns></returns>
        private float getCurrentMountScale()
        {
            return currentMountSize / currentMountOption.modelDefinition.diameter;
        }
    }
    
    /// <summary>
    /// Live config data class for engine layout.
    /// Positions in the layout should be defined in a 1m scale
    /// </summary>
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

