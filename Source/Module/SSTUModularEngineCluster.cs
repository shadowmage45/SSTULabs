using System;
using UnityEngine;
using System.Collections.Generic;

namespace SSTUTools
{
    public class SSTUModularEngineCluster : PartModule, IPartCostModifier, IPartMassModifier
    {

        #region REGION - Standard KSPField variables

        /// <summary>
        /// The URL of the model to use for this engine cluster
        /// </summary>
        [KSPField]
        public String engineModelName = String.Empty;
        
        /// <summary>
        /// The default engine spacing if none is defined in the mount definition
        /// </summary>
        [KSPField]
        public float engineSpacing = 3f;
        
        [KSPField]
        public float engineMountDiameter = 3f;

        /// <summary>
        /// The height of the engine model at normal scale.  This should be the distance from the top mounting plane to the bottom of the engine (where the attach-node should be)
        /// </summary>
        [KSPField]
        public float engineHeight = 1f;

        /// <summary>
        /// The scale to render the engine model at.  engineYOffset and engineHeight will both be scaled by this value
        /// </summary>
        [KSPField]
        public float engineScale = 1f;

        /// <summary>
        /// This field determines how much vertical offset should be given to the engine model (to correct for the default-COM positioning of stock/other mods engine models).        
        /// A positive value will move the model up, a negative value moves it down.
        /// Should be the value of the distance between part origin and the top mounting plane of the part, as a negative value (as you are moving the engine model downward to place the mounting plane at COM/origin)
        /// </summary>
        [KSPField]
        public float engineYOffset = 0f;

        [KSPField]
        public bool upperStageMounts = true;

        [KSPField]
        public bool lowerStageMounts = true;

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
        /// How much to increment the diameter with every step of the main diameter slider
        /// </summary>
        [KSPField]
        public float diameterIncrement = 0.625f;

        /// <summary>
        /// CSV list of transform names
        /// transforms of these names are removed from the model after it is cloned
        /// this is to be used to remove stock fairing transforms from stock engine models (module should be removed by the same patch that is making the custom cluster)
        /// </summary>
        [KSPField]
        public String transformsToRemove = String.Empty;

        [KSPField]
        public String interstageNodeName = "interstage";
        
        [KSPField]
        public String mountTransformName = "SSTEngineClusterMounts";

        [KSPField]
        public String engineTransformName = "SSTEngineClusterEngines";

        /// <summary>
        /// If true, the engine cluster module will update the part mass based on the number of engines in the layout and the 'engineMass' specified in its config, otherwise it will leave it unaltered.
        /// </summary>
        [KSPField]
        public bool modifyMass = true;

        #endregion ENDREGION - Standard KSPField variables

        #region REGION - KSP Editor Adjust Fields (Float Sliders) and KSP GUI Fields (visible data)

        /// <summary>
        /// Used for adjusting the inter-engine spacing, this is a scale value that is applied to the config-specified engine spacing
        /// </summary>
        [KSPField(guiName = "Spacing", guiActive = false, guiActiveEditor = true),
         //UI_FloatRange(minValue = -2.0f, maxValue = 2f, stepIncrement = 0.10f),
         UI_FloatEdit(sigFigs = 2, suppressEditorShipModified = true, minValue = -2, maxValue = 2, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.05f)]
        public float editorEngineSpacingAdjust = 0f;

        /// <summary>
        /// Determines the y-position of the engine model (and node position/fairing position).  Can be used to offset an engine inside of its included mount.
        /// </summary>
        [KSPField(guiName = "Vert. Pos.", guiActive = false, guiActiveEditor = true),
         UI_FloatEdit(sigFigs =2, suppressEditorShipModified = true, minValue = -2, maxValue = 2, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.05f)]
        public float editorEngineHeightAdjust = 0f;

        [KSPField(guiName = "Layout", guiActive = false, guiActiveEditor = true),
         UI_ChooseOption(display = new string[] { "Single" }, options = new string[] { "Single" }, suppressEditorShipModified = true)]
        public string guiLayoutOption = "Single";

        [KSPField(guiName = "Mount", guiActive = false, guiActiveEditor = true),
         UI_ChooseOption(display = new string[] { "Mount-None"}, options = new string[] { "Mount-None"}, suppressEditorShipModified = true)]
        public string guiMountOption = "Mount-None";        

        #endregion ENDREGION - KSP Editor Adjust Fields (Float Sliders)

        #region REGION - persistent save-data values, should not be edited in config

        /// <summary>
        /// Currently enabled engine layout, set from the default mount option, and may be user-editable in VAB if multiple layouts are enabled for that mount option
        /// </summary>
        [KSPField(isPersistant = true)]
        public String currentEngineLayoutName = String.Empty;

        /// <summary>
        /// This is the currently selected mount.  Field is updated whenever the mount model is changed.  Populated initially with value of 'defaultMount'.
        /// </summary>
        [KSPField(isPersistant = true)]
        public String currentMountName = String.Empty;              

        /// <summary>
        /// Determines the current scale of the mount, persistent value.  Indirectly edited by user in the VAB
        /// </summary>
        [KSPField(isPersistant = true, guiName = "Mount Diam", guiActive = false, guiActiveEditor = true ),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true )]
        public float currentMountDiameter = 5;

        /// <summary>
        /// Determines the spacing between each engine.  This is the not intended for config editing, and is set by the values in the mount options.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentEngineSpacing = 3f;

        /// <summary>
        /// How far from default vertical position is the current engine model offset, in meters -- adjusted in VAB through the height-adjust slider
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentEngineVerticalOffset = 0f;

        [KSPField(isPersistant = true)]
        public String currentMountTexture = String.Empty;

        [KSPField(isPersistant = true)]
        public bool fairingInitialized = false;

        #endregion ENDREGION - persistent save-data values, should not be edited in config

        #region REGION - Private working variables

        private List<MountModelData> mountModelData = new List<MountModelData>();
        private EngineClusterLayoutMountData currentMountData = null;
        private EngineClusterLayoutData[] engineLayouts;     
        private EngineClusterLayoutData currentEngineLayout = null;

        private bool initialized = false;
        
        private float prevEngineSpacingAdjust = 0;
        private float prevEngineHeightAdjust = 0;
        private float prevMountDiameter = 0f;

        private float engineMountingY = 0;
        private float fairingTopY = 0;
        private float fairingBottomY = 0;
        
        private float modifiedCost = 0;
        private float modifiedMass = 1f;
        
        #endregion ENDREGION - Private working variables

        #region REGION - GUI Interaction Methods
        
        [KSPEvent(guiName = "Clear Mount Type", guiActive = false, guiActiveEditor = true, active = true)]
        public void clearMountEvent()
        {
            EngineClusterLayoutMountData mountData = Array.Find(currentEngineLayout.mountData, m => m.name == "Mount-None");
            if (mountData != null)
            {
                updateMountFromEditor(mountData.name, true, true);
                SSTUStockInterop.fireEditorUpdate();
                SSTUModInterop.onPartGeometryUpdate(part, true);
            }
        }

        [KSPEvent(guiName = "Next Mount Texture", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextMountTextureEvent()
        {
            setMountTextureFromEditor(currentMountData.getNextTextureSetName(currentMountTexture, false), true);
        }

        public void onDiameterUpdated(BaseField field, object obj)
        {
            updateMountSizeFromEditor(currentMountDiameter, true);
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        public void onMountUpdated(BaseField field, object obj)
        {
            updateMountFromEditor(guiMountOption, true, false);
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        public void onLayoutUpdated(BaseField field, object obj)
        {
            updateLayoutFromEditor(guiLayoutOption, true, false);
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        public void onSpacingUpdated(BaseField field, object obj)
        {
            updateEngineSpacingFromEditor(editorEngineSpacingAdjust, true);
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        public void onHeightUpdated(BaseField field, object obj)
        {
            updateEngineOffsetFromEditor(editorEngineHeightAdjust, true);
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        private void updateEngineSpacingFromEditor(float newSpacing, bool updateSymmetry)
        {            
            currentEngineSpacing = currentEngineLayout.getEngineSpacing(engineSpacing, currentMountData) + newSpacing;
            positionMountModels();
            positionEngineModels();
            updateNodePositions(true);
            updateDragCubes();
            updateEditorFields();
            updateGuiState();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts) { p.GetComponent<SSTUModularEngineCluster>().updateEngineSpacingFromEditor(newSpacing, false); }
            }
        }
        
        private void updateMountSizeFromEditor(float newSize, bool updateSymmetry)
        {
            if (newSize < currentMountData.minDiameter) { newSize = currentMountData.minDiameter; }
            if (newSize > currentMountData.maxDiameter) { newSize = currentMountData.maxDiameter; }
            if (newSize != currentMountDiameter) { currentMountDiameter = newSize; }
            positionMountModels();
            positionEngineModels();
            updateNodePositions(true);
            updateFairing(true);
            updateDragCubes();
            updateEditorFields();
            updateMountSizeGuiControl(false);
            updatePartCostAndMass();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularEngineCluster>().updateMountSizeFromEditor(newSize, false);
                }                
            }
        }

        private void updateLayoutFromEditor(String newLayout, bool updateSymmetry, bool forceGuiUpdate)
        {
            currentEngineLayoutName = newLayout;
            currentEngineLayout = Array.Find(engineLayouts, m => m.layoutName == newLayout);
            if (currentMountName == "Mount-None" && currentEngineLayout.isValidMount(currentMountName))
            {
                //NOOP
            }
            else
            {
                currentMountName = currentEngineLayout.defaultMount;
            }
            currentMountData = currentEngineLayout.getMountData(currentMountName);
            currentMountDiameter = currentMountData.initialDiameter;
            currentEngineSpacing = currentEngineLayout.getEngineSpacing(engineSpacing, currentMountData) + editorEngineSpacingAdjust;
            setupMountModels();
            updateMountTexture();
            positionMountModels();
            setupEngineModels();
            positionEngineModels();
            reInitEngineModule();
            updateNodePositions(true);
            updateDragCubes();
            updateEditorFields();
            updateMountSizeGuiControl(true, currentMountData.initialDiameter);
            updateMountOptionsGuiControl(true);
            updatePartCostAndMass();
            updateGuiState();
            updateFairing(true);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts) { p.GetComponent<SSTUModularEngineCluster>().updateLayoutFromEditor(newLayout, false, false); }
            }
        }

        private void updateMountFromEditor(String newMount, bool updateSymmetry, bool forceGuiUpdate)
        {
            currentMountName = newMount;
            currentMountData = currentEngineLayout.getMountData(currentMountName);
            currentMountDiameter = currentMountData.initialDiameter;
            setupMountModels();
            updateMountTexture();
            positionMountModels();
            positionEngineModels();
            updateNodePositions(true);
            updateDragCubes();
            updateEditorFields();
            updateMountOptionsGuiControl(forceGuiUpdate);
            updateMountSizeGuiControl(true);
            updatePartCostAndMass();
            updateGuiState();
            updateFairing(true);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts) { p.GetComponent<SSTUModularEngineCluster>().updateMountFromEditor(newMount, false, false); }
            }
        }

        private void updateEngineOffsetFromEditor(float newOffset, bool updateSymmetry)
        {
            currentEngineVerticalOffset = newOffset;
            positionMountModels();
            positionEngineModels();
            updateNodePositions(true);
            updateFairing(true);
            updateDragCubes();
            updateEditorFields();
            updateGuiState();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts) { p.GetComponent<SSTUModularEngineCluster>().updateEngineOffsetFromEditor(newOffset, false); }                
            }
        }

        private void setMountTextureFromEditor(String newSet, bool updateSymmetry)
        {
            currentMountTexture = newSet;

            if (!currentMountData.isValidTextureSet(currentMountTexture))
            {
                currentMountTexture = currentMountData.modelDefinition.defaultTextureSet;
            }
            currentMountData.enableTextureSet(currentMountTexture);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularEngineCluster>().setMountTextureFromEditor(currentMountTexture, false);
                }
            }
        }

        #endregion ENDREGION - GUI Interaction Methods

        #region REGION - Standard KSP Overrides

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                initialize();
            }
            Fields["currentMountDiameter"].uiControlEditor.onFieldChanged = onDiameterUpdated;
            Fields["guiMountOption"].uiControlEditor.onFieldChanged = onMountUpdated;
            Fields["guiLayoutOption"].uiControlEditor.onFieldChanged = onLayoutUpdated;
            Fields["editorEngineHeightAdjust"].uiControlEditor.onFieldChanged = onHeightUpdated;
            Fields["editorEngineSpacingAdjust"].uiControlEditor.onFieldChanged = onSpacingUpdated;
            editorEngineSpacingAdjust = currentEngineSpacing - currentEngineLayout.getEngineSpacing(engineSpacing, currentMountData);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
            {
                //prefab init... do not do init during OnLoad for editor or flight... trying for some consistent loading sequences this time around
                initializePrefab(node);
            }
            else
            {
                initialize();
            }            
        }

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                reInitEngineModule();
                if (!fairingInitialized)
                {
                    fairingInitialized = true;
                    updateFairing(true);
                }
            }
            updateEditorFields();
        }

        /// <summary>
        /// Overriden to provide an opportunity to remove any existing models from the prefab part, so they do not get cloned into live parts
        /// as for some reason they cause issues when cloned in that fashion.
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {            
            return "This part may have multiple mount variants, right click for more info.";
        }

        //IModuleCostModifier Override
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (!modifyMass) { return 0; }
            return -defaultCost + modifiedCost;
        }

        //IModuleMassModifier Override
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (!modifyMass) { return 0; }
            return -defaultMass + modifiedMass;
        }
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Initialization

        private void initialize()
        {
            if (initialized) { return; }
            loadConfigNodeData(SSTUStockInterop.getPartModuleConfig(part, part.Modules.IndexOf(this)));
            removeStockTransforms();
            initializeSmokeTransform();
            setupMountModels();
            positionMountModels();
            setupEngineModels();
            positionEngineModels();
            updateNodePositions(false);
            updateDragCubes();
            updateEditorFields();
            updateLayoutOptionsGuiControl(false);
            updateMountSizeGuiControl(false);
            updateMountOptionsGuiControl(false);
            updatePartCostAndMass();
            updateGuiState();
            if (!currentMountData.isValidTextureSet(currentMountTexture))
            {
                currentMountTexture = currentMountData.modelDefinition.defaultTextureSet;
            }
            updateMountTexture();
        }

        private void initializePrefab(ConfigNode node)
        {
            loadConfigNodeData(node);      
            currentEngineSpacing = currentEngineLayout.getEngineSpacing(engineSpacing, currentMountData) + editorEngineSpacingAdjust;
            removeStockTransforms();
            initializeSmokeTransform();
            setupMountModels();
            positionMountModels();
            setupEngineModels();
            positionEngineModels();
            updatePartCostAndMass();
            if (!currentMountData.isValidTextureSet(currentMountTexture))
            {
                currentMountTexture = currentMountData.modelDefinition.defaultTextureSet;
            }
            updateMountTexture();
        }

        private void loadConfigNodeData(ConfigNode node)
        {            
            ConfigNode[] layoutNodes = node.GetNodes("LAYOUT");
            loadEngineLayouts(layoutNodes);

            if (String.IsNullOrEmpty(currentEngineLayoutName))
            {
                currentEngineLayoutName = engineLayouts[0].layoutName;
            }
            currentEngineLayout = Array.Find(engineLayouts, m => m.layoutName == currentEngineLayoutName);
            
            if (currentEngineLayout == null)
            {
                currentEngineLayout = engineLayouts[0];
                currentEngineLayoutName = currentEngineLayout.layoutName;
            }
            if (!currentEngineLayout.isValidMount(currentMountName))//catches the case of an uninitilized part and those where mount data has been removed from the config.
            {                
                currentMountName = currentEngineLayout.defaultMount;
                currentMountDiameter = currentEngineLayout.getMountData(currentMountName).initialDiameter;
                currentEngineSpacing = currentEngineLayout.getEngineSpacing(engineSpacing, currentEngineLayout.getMountData(currentMountName));
                editorEngineSpacingAdjust = prevEngineSpacingAdjust = 0f;
            }
            currentMountData = currentEngineLayout.getMountData(currentMountName);
            if (currentMountDiameter > currentMountData.maxDiameter) { currentMountDiameter = currentMountData.maxDiameter; }
            if (currentMountDiameter < currentMountData.minDiameter) { currentMountDiameter = currentMountData.minDiameter; }
        }

        private void loadEngineLayouts(ConfigNode[] moduleLayoutNodes)
        {
            Dictionary<String, SSTUEngineLayout> allBaseLayouts = SSTUEngineLayout.getAllLayoutsDict();
            Dictionary<String, ConfigNode> layoutConfigNodes = new Dictionary<string, ConfigNode>();
            String name;
            ConfigNode localLayoutNode;
            int len = moduleLayoutNodes.Length;            
            for (int i = 0; i < len; i++)
            {
                localLayoutNode = moduleLayoutNodes[i];
                name = localLayoutNode.GetStringValue("name");
                if (allBaseLayouts.ContainsKey(name))
                {
                    if (localLayoutNode.GetBoolValue("remove", false))//remove any layouts flagged for removal
                    {
                        allBaseLayouts.Remove(name);
                    }
                    else
                    {
                        //MonoBehaviour.print("storing customized config for layout: " + name + " for part: " + part);
                        layoutConfigNodes.Add(name, localLayoutNode);
                    }
                }
            }

            len = allBaseLayouts.Keys.Count;
            engineLayouts = new EngineClusterLayoutData[len];
            int index = 0;
            foreach (String key in allBaseLayouts.Keys)
            {
                localLayoutNode = null;
                layoutConfigNodes.TryGetValue(key, out localLayoutNode);
                //MonoBehaviour.print("Loading layout : " + key + " for engine: " + part+" with custom data of: "+localLayoutNode);
                engineLayouts[index] = new EngineClusterLayoutData(allBaseLayouts[key], localLayoutNode, engineSpacing*engineScale, engineMountDiameter*engineScale, upperStageMounts, lowerStageMounts);
                index++;
            }
            //sort usable layout list by the # of positions in the layout, 1..2..3..x..99
            Array.Sort(engineLayouts, delegate (EngineClusterLayoutData x, EngineClusterLayoutData y) { return x.getLayoutData().positions.Count - y.getLayoutData().positions.Count; });
        }

        private void initializeSmokeTransform()
        {
            //add the smoke transform point, parented to the model base transform ('model')
            Transform modelBase = part.transform.FindRecursive("model");
            GameObject smokeObject = modelBase.FindOrCreate(smokeTransformName).gameObject;
            smokeObject.name = smokeTransformName;
            smokeObject.transform.name = smokeTransformName;
            Transform smokeTransform = smokeObject.transform;
            smokeTransform.NestToParent(modelBase);
            smokeTransform.localRotation = Quaternion.AngleAxis(90, new Vector3(1, 0, 0));//set it to default pointing downwards, as-per a thrust transform
        }

        #endregion ENDREGION - Initialization

        #region REGION - Model Setup

        /// <summary>
        /// Sets up the actual models for the mount(s), but does not position or scale the models
        /// </summary>
        private void setupMountModels()
        {
            Transform modelBase = part.transform.FindRecursive("model");
            Transform mountBaseTransform = modelBase.FindRecursive(mountTransformName);
            if (mountBaseTransform != null)
            {
                GameObject.DestroyImmediate(mountBaseTransform.gameObject);
            }

            GameObject newMountBaseGO = new GameObject(mountTransformName);
            mountBaseTransform = newMountBaseGO.transform;
            mountBaseTransform.NestToParent(modelBase);
                        
            currentMountData.setupModel(part, mountBaseTransform, ModelOrientation.BOTTOM);
        }

        private void positionMountModels()
        {
            float currentMountScale = getCurrentMountScale();
            float mountY = partTopY + (currentMountScale * currentMountData.modelDefinition.verticalOffset);
            if (currentMountData.model != null)
            {
                Transform mountModel = currentMountData.model.transform;
                mountModel.transform.localPosition = new Vector3(0, mountY, 0);
                mountModel.transform.localRotation = currentMountData.modelDefinition.invertForBottom ? Quaternion.AngleAxis(180, Vector3.forward) : Quaternion.AngleAxis(0, Vector3.up);
                mountModel.transform.localScale = new Vector3(currentMountScale, currentMountScale, currentMountScale);
            }            
            //set up fairing/engine/node positions
            float mountScaledHeight = currentMountData.modelDefinition.height * currentMountScale;
            fairingTopY = partTopY + (currentMountData.modelDefinition.fairingTopOffset * currentMountScale);
            engineMountingY = partTopY + (engineYOffset * engineScale) - mountScaledHeight + editorEngineHeightAdjust;
            fairingBottomY = partTopY - (engineHeight * engineScale) - mountScaledHeight + editorEngineHeightAdjust;          
        }

        /// <summary>
        /// Removes existing engine models and create new models, but does not position or scale them
        /// </summary>
        private void setupEngineModels()
        {
            Transform modelBase = part.transform.FindRecursive("model");
            Transform engineBaseTransform = modelBase.FindRecursive(engineTransformName);
            if (engineBaseTransform != null)
            {
                GameObject.DestroyImmediate(engineBaseTransform.gameObject);
            }

            GameObject baseGO = new GameObject(engineTransformName);
            baseGO.transform.NestToParent(modelBase);
            engineBaseTransform = baseGO.transform;
            SSTUEngineLayout layout = currentEngineLayout.getLayoutData();
            int numberOfEngines = layout.positions.Count;
            
            GameObject enginePrefab = GameDatabase.Instance.GetModelPrefab(engineModelName);
            GameObject engineClone;
            foreach (SSTUEnginePosition position in layout.positions)
            {
                engineClone = (GameObject)GameObject.Instantiate(enginePrefab);
                engineClone.name = enginePrefab.name;
                engineClone.transform.name = enginePrefab.transform.name;
                engineClone.transform.NestToParent(engineBaseTransform);
                engineClone.transform.localScale = new Vector3(engineScale, engineScale, engineScale);
                engineClone.SetActive(true);
            }
            removeStockTransforms();
        }

        /// <summary>
        /// Updates the engine model positions and rotations for the current layout positioning with the given mount vertical offset, engine vertical offset, and user-specified vertical offset
        /// </summary>
        private void positionEngineModels()
        {
            SSTUEngineLayout layout = currentEngineLayout.getLayoutData();

            float posX, posZ, rot;
            GameObject model;
            SSTUEnginePosition position;
            int length = layout.positions.Count;

            float rotateEngines = currentMountData.rotateEngines;

            Transform[] models = part.transform.FindRecursive(engineTransformName).FindChildren(engineModelName);
            for (int i = 0; i < length; i++)
            {
                position = layout.positions[i];
                model = models[i].gameObject;
                posX = position.scaledX(currentEngineSpacing);
                posZ = position.scaledZ(currentEngineSpacing);
                rot = position.rotation;
                rot += rotateEngines;
                model.transform.localPosition = new Vector3(posX, engineMountingY, posZ);
                model.transform.localRotation = Quaternion.AngleAxis(rot, Vector3.up);
            }

            Transform smokeTransform = part.FindModelTransform(smokeTransformName);
            if (smokeTransform != null)
            {
                Vector3 pos = smokeTransform.localPosition;
                pos.y = engineMountingY + (engineScale * smokeTransformOffset);
                smokeTransform.localPosition = pos;
            }            
        }

        private void updateMountTexture()
        {
            if (!currentMountData.isValidTextureSet(currentMountTexture))
            {
                currentMountTexture = currentMountData.modelDefinition.defaultTextureSet;
            }
            currentMountData.enableTextureSet(currentMountTexture);
        }

        #endregion ENDREGION - Model Setup

        #region REGION - Update Methods
                
        private void updatePartCostAndMass()
        {
            SSTUEngineLayout layout = currentEngineLayout.getLayoutData();
            float engineCost = 100;
            if (part.partInfo != null) { engineCost = part.partInfo.cost; }
            modifiedCost = engineCost * (float)layout.positions.Count;                        
            modifiedCost += Mathf.Pow(getCurrentMountScale(), 3.0f) * currentMountData.modelDefinition.cost;

            modifiedMass = part.prefabMass * (float)layout.positions.Count;
            modifiedMass += currentMountData.modelDefinition.mass * Mathf.Pow(getCurrentMountScale(), 3.0f);
        }

        /// <summary>
        /// Restores the editor-adjustment values from the current/persistent tank size data
        /// Should only be called when a new mount is selected, or the part is fist initialized in the editor
        /// </summary>
        private void updateEditorFields()
        {       
            float spacing = currentEngineLayout.getEngineSpacing(engineSpacing, currentMountData);
            prevEngineSpacingAdjust = editorEngineSpacingAdjust = currentEngineSpacing - spacing;
            prevEngineHeightAdjust = editorEngineHeightAdjust = currentEngineVerticalOffset;
            prevMountDiameter = currentMountDiameter;
            guiMountOption = currentMountName;
            guiLayoutOption = currentEngineLayoutName;
        }

        private void updateMountSizeGuiControl(bool forceUpdate, float forceVal = 0)
        {
            bool active = currentMountData.minDiameter != currentMountData.maxDiameter;
            Fields["currentMountDiameter"].guiActiveEditor = active;
            if (active)
            {
                this.updateUIFloatEditControl("currentMountDiameter", currentMountData.minDiameter, currentMountData.maxDiameter, diameterIncrement * 2, diameterIncrement, diameterIncrement * 0.05f, forceUpdate, forceVal);
            }            
            prevMountDiameter = currentMountDiameter;
        }

        private void updateMountOptionsGuiControl(bool forceUpdate)
        {
            string[] optionsArray = SSTUUtils.getNames(currentEngineLayout.mountData, m => m.name);
            this.updateUIChooseOptionControl("guiMountOption", optionsArray, optionsArray, forceUpdate, currentMountName);
        }

        private void updateLayoutOptionsGuiControl(bool forceUpdate)
        {
            string[] optionsArray = SSTUUtils.getNames(engineLayouts, m => m.layoutName);
            this.updateUIChooseOptionControl("guiLayoutOption", optionsArray, optionsArray, forceUpdate, currentEngineLayoutName);
        }

        /// <summary>
        /// Updates the context-menu GUI buttons/etc as the config of the part changes.
        /// </summary>
        private void updateGuiState()
        {
            Events["clearMountEvent"].active = currentEngineLayout.mountData.Length > 1;
            Fields["editorEngineSpacingAdjust"].guiActiveEditor = currentEngineLayout.getLayoutData().positions.Count > 1;
            Events["nextMountTextureEvent"].active = currentMountData.modelDefinition.textureSets.Length > 1;
        }
        
        /// <summary>
        /// Updates the position and enable/disable status of the SSTUNodeFairing (if present). <para/>
        /// </summary>
        private void updateFairing(bool userInput)
        {
            SSTUNodeFairing fairing = part.GetComponent<SSTUNodeFairing>();
            if (fairing == null) { return; }            
            bool enable = !currentMountData.modelDefinition.fairingDisabled;
            fairing.canDisableInEditor = enable;
            FairingUpdateData data = new FairingUpdateData();
            data.setTopY(fairingTopY);
            data.setTopRadius(currentMountDiameter * 0.5f);
            if (userInput)
            {
                data.setBottomRadius(currentMountDiameter * 0.5f);
            }
            data.setEnable(enable);
            fairing.updateExternal(data);
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
                        
            if (!String.IsNullOrEmpty(interstageNodeName))
            {
                float y = partTopY + (currentMountData.modelDefinition.fairingTopOffset * getCurrentMountScale());
                pos = new Vector3(0, y, 0);
                SSTUSelectableNodes.updateNodePosition(part, interstageNodeName, pos);
                AttachNode interstage = part.findAttachNode(interstageNodeName);
                if (interstage != null)
                {
                    Vector3 orientation = new Vector3(0, -1, 0);
                    SSTUAttachNodeUtils.updateAttachNodePosition(part, interstage, pos, orientation, userInput);
                }
            }
        }

        private void updateDragCubes()
        {
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        #endregion ENDREGION - Update Methods

        #region REGION - Utility Methods

        /// <summary>
        /// Re-initializes the engine and gimbal modules from their original config nodes -- this should -hopefully- allow them to grab updated transforms and update FX stuff properly
        /// </summary>
        private void reInitEngineModule()
        {
            MonoBehaviour.print("SSTUModularEngineCluster -- updating external modules (gimbal, engines, constraints, animations, engineConfigs)");
            SSTUEngineLayout layout = currentEngineLayout.getLayoutData();
            StartState state = HighLogic.LoadedSceneIsEditor ? StartState.Editor : HighLogic.LoadedSceneIsFlight ? StartState.Flying : StartState.None;
            ConfigNode partConfig = SSTUStockInterop.getPartConfig(part);
            
            //model constraints need to be updated whenever the number of models (or just the game-objects) are updated
            SSTUModelConstraint constraint = part.GetComponent<SSTUModelConstraint>();
            if (constraint != null)
            {
                constraint.reInitialize();
            }

            //animations need to be updated to find the new animations for the updated models
            SSTUAnimateControlled[] anims = part.GetComponents<SSTUAnimateControlled>();
            foreach (SSTUAnimateControlled controlled in anims)
            {
                controlled.reInitialize();
            }

            SSTUAnimateEngineHeat[] heatAnims = part.GetComponents<SSTUAnimateEngineHeat>();
            foreach (SSTUAnimateEngineHeat heatAnim in heatAnims)
            {
                heatAnim.reInitialize();
            }

            //update the engine module(s), forcing them to to reload their thrust, transforms, and effects.
            ModuleEngines[] engines = part.GetComponents<ModuleEngines>();
            String engineModuleName = engines[0].GetType().Name;
            ConfigNode[] engineNodes = partConfig.GetNodes("MODULE", "name", engineModuleName);
            ConfigNode engineNode;
            float maxThrust, minThrust;
            int positions = layout.positions.Count;
            for (int i = 0; i < engines.Length; i++)
            {
                engineNode = new ConfigNode("MODULE");
                engineNodes[i].CopyTo(engineNode);
                minThrust = engineNode.GetFloatValue("minThrust") * (float)positions;
                maxThrust = engineNode.GetFloatValue("maxThrust") * (float)positions;
                engineNode.SetValue("minThrust", minThrust.ToString(), true);
                engineNode.SetValue("maxThrust", maxThrust.ToString(), true);
                engines[i].propellants.Clear();//as i'm feeding it nodes with propellants, clear the existing ones..
                if (engineNode.HasNode("transformMultipliers"))
                {
                    ConfigNode orig = engineNode.GetNode("transformMultipliers");
                    engineNode.RemoveNode(orig);
                    engineNode.AddNode(getSplitThrustNode(orig, positions));
                }
                engines[i].Load(engineNode);
                engines[i].OnStart(state);
            }
            SSTUModInterop.onEngineConfigChange(part, null, positions);//this forces ModuleEngineConfigs to reload the config, with the # of engines as the 'scale'

            //update the gimbal modules, force them to reload transforms
            ModuleGimbal[] gimbals = part.GetComponents<ModuleGimbal>();
            ConfigNode[] gimbalNodes = partConfig.GetNodes("MODULE", "name", "ModuleGimbal");
            float limit = 0;
            for (int i = 0; i < gimbals.Length; i++)
            {
                limit = gimbals[i].gimbalLimiter; 
                gimbals[i].gimbalTransforms = null;
                gimbals[i].initRots = null;
                gimbals[i].OnStart(state);
                gimbals[i].gimbalLimiter = limit;
            }
        }

        private ConfigNode getSplitThrustNode(ConfigNode original, int positions)
        {
            int numOfTransforms = original.values.Count;
            MonoBehaviour.print("Updating split thrust for " + numOfTransforms + " base transform count");
            ConfigNode output = new ConfigNode("transformMultipliers");
            float[] originalValues = new float[numOfTransforms];
            float newValue;
            for (int i = 0; i < numOfTransforms; i++)
            {
                originalValues[i] = original.GetFloatValue("trf" + i);
            }
            int rawIndex = 0;
            float totalValue = 0;
            for (int i = 0; i < positions; i++)
            {
                for (int k = 0; k < numOfTransforms; k++, rawIndex++)
                {                    
                    newValue = originalValues[k] / (float)positions;
                    totalValue += newValue;
                    output.AddValue("trf" + rawIndex, newValue);
                    MonoBehaviour.print("original thrust for transform: " + rawIndex + " : " + originalValues[i]+" ::adjusted percentag: "+newValue);
                }
            }
            MonoBehaviour.print("Split thrust total: " + totalValue + " (Should == 1)");
            return output;
        }

        /// <summary>
        /// Returns the current mount scale; calculated by the current user-set size and the default size specified in definition file
        /// </summary>
        /// <returns></returns>
        private float getCurrentMountScale()
        {
            return currentMountDiameter / currentMountData.modelDefinition.diameter;
        }

        /// <summary>
        /// Removes the named transforms from the model hierarchy. Removes the entire branch of the tree starting at the named transform.<para/>
        /// This is intended to be used to remove stock ModuleJettison engine fairing transforms,
        /// but may have other use cases as well.  Should function as intended for any model transforms.
        /// </summary>
        private void removeStockTransforms()
        {
            if (String.IsNullOrEmpty(transformsToRemove)) { return; }
            String[] names = SSTUUtils.parseCSV(transformsToRemove);
            SSTUUtils.removeTransforms(part, names);
        }
        
        #endregion ENDREGION - Utility Methods
    }

    public class EngineClusterLayoutData
    {
        public readonly String layoutName;
        public readonly String defaultMount;
        public readonly float engineSpacingOverride = -1f;

        //base layout for positional data
        private readonly SSTUEngineLayout layoutData;

        //available mounts for this layout
        public EngineClusterLayoutMountData[] mountData;

        public EngineClusterLayoutData(SSTUEngineLayout layoutData, ConfigNode node, float moduleEngineSpacing, float moduleMountSize, bool upperMounts, bool lowerMounts)
        {
            layoutName = layoutData.name;
            this.layoutData = layoutData;

            defaultMount = lowerMounts? layoutData.defaultLowerStageMount : layoutData.defaultUpperStageMount;
            engineSpacingOverride = moduleEngineSpacing;

            Dictionary<String, ConfigNode> localMountNodes = new Dictionary<String, ConfigNode>();
            Dictionary<String, SSTUEngineLayoutMountOption> globalMountOptions = new Dictionary<String, SSTUEngineLayoutMountOption>();
            List<ConfigNode> customMounts = new List<ConfigNode>();

            String name;
            ConfigNode mountNode;

            SSTUEngineLayoutMountOption mountOption;
            int len = layoutData.mountOptions.Length;
            for (int i = 0; i < len; i++)
            {
                mountOption = layoutData.mountOptions[i];
                if ((mountOption.upperStage && upperMounts) || (mountOption.lowerStage && lowerMounts))
                {
                    globalMountOptions.Add(mountOption.mountName, mountOption);
                }
            }

            if (node != null)
            {
                //override data from the config node
                defaultMount = node.GetStringValue("defaultMount", defaultMount);
                engineSpacingOverride = node.GetFloatValue("engineSpacing", engineSpacingOverride);
                ConfigNode[] mountNodes = node.GetNodes("MOUNT");
                len = mountNodes.Length;
                for (int i = 0; i < len; i++)
                {
                    mountNode = mountNodes[i];
                    name = mountNode.GetStringValue("name");
                    if (mountNode.GetBoolValue("remove", false))
                    {
                        globalMountOptions.Remove(name);
                        MonoBehaviour.print("Removing mount:" + name + " from layout: " + layoutName);
                    }
                    else
                    {
                        localMountNodes.Add(name, mountNode);
                    }
                    if (!globalMountOptions.ContainsKey(name))
                    {
                        customMounts.Add(mountNode);
                    }
                }
            }

            
            List<EngineClusterLayoutMountData> mountDataTemp = new List<EngineClusterLayoutMountData>();            
            foreach(String key in globalMountOptions.Keys)
            {
                if (localMountNodes.ContainsKey(key))
                {
                    mountNode = localMountNodes[key];
                    //MonoBehaviour.print("Loading manual-sized mount: " + key + " for layout: " + layoutName + "\n" + mountNode);
                }
                else
                {
                    mountNode = getAutoSizeNode(globalMountOptions[key], moduleEngineSpacing, moduleMountSize, 0.625f);
                    //MonoBehaviour.print("Loading auto-sized mount: " + key + " for layout: " + layoutName + "\n" + mountNode);
                }
                mountDataTemp.Add(new EngineClusterLayoutMountData(mountNode));
            }
            foreach (ConfigNode cm in customMounts)
            {
                mountDataTemp.Add(new EngineClusterLayoutMountData(cm));
            }
            mountData = mountDataTemp.ToArray();
        }

        private ConfigNode getAutoSizeNode(SSTUEngineLayoutMountOption option, float engineSpacing, float engineMountSize, float increment)
        {
            
            float sizeDelta = engineSpacing - engineMountSize;
            float minMountingArea = (layoutData.mountSizeMult * engineSpacing) - sizeDelta;
                        
            float minSize = 2.5f, maxSize = 10f, size = 2.5f;

            ModelDefinition mdf = SSTUModelData.getModelDefinition(option.mountName);
            float modelMountArea = mdf.configNode.GetFloatValue("mountingDiameter");
            float modelSize = mdf.diameter;
            float mountPercent = modelMountArea / modelSize;
            float minMountAreaCalc = minMountingArea / mountPercent;
            float whole = minMountAreaCalc / increment;
            whole = (float)Math.Ceiling(whole);
            minMountAreaCalc = whole * increment;
            size = minMountAreaCalc;
            minSize = size - increment;
            //TODO fix this/clean up
            if (size >= increment*4) { minSize -= increment; }
            if (size >= increment*8) { minSize -= increment; }
            if (size >= increment * 12) { minSize -= increment; }

            ConfigNode node = new ConfigNode("MOUNT");
            node.AddValue("name", option.mountName);
            node.AddValue("size", size);
            node.AddValue("minSize", minSize);
            node.AddValue("maxSize", maxSize);
            return node;
        }

        public float getEngineSpacing(float defaultSpacing, EngineClusterLayoutMountData mount)
        {
            if (mount.engineSpacing != -1)
            {
                return mount.engineSpacing;
            }
            return  engineSpacingOverride == -1 ? defaultSpacing : engineSpacingOverride;
        }

        public bool isValidMount(String mountName)
        {
            return Array.Find(mountData, m => m.name == mountName) != null;
        }

        public EngineClusterLayoutMountData getMountData(String mountName)
        {
            return Array.Find(mountData, m => m.name == mountName);
        }

        public SSTUEngineLayout getLayoutData()
        {
            return layoutData;
        }

    }

    public class EngineClusterLayoutMountData : SingleModelData
    {
        public readonly bool canAdjustSize = true;
        public readonly float initialDiameter = 1.25f;
        public readonly float minDiameter;
        public readonly float maxDiameter;
        public readonly float rotateEngines = 0;
        public readonly float engineSpacing = -1;
        
        public EngineClusterLayoutMountData(ConfigNode node) : base(node)
        {
            canAdjustSize = node.GetBoolValue("canAdjustSize", canAdjustSize);
            initialDiameter = node.GetFloatValue("size", initialDiameter);
            minDiameter = node.GetFloatValue("minSize", initialDiameter);
            maxDiameter = node.GetFloatValue("maxSize", initialDiameter);
            rotateEngines = node.GetFloatValue("rotateEngines");
            engineSpacing = node.GetFloatValue("engineSpacing", engineSpacing);
            if (String.IsNullOrEmpty(modelDefinition.modelName)) { canAdjustSize = false; }
        }
    }

}

