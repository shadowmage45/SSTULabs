using System;
using UnityEngine;
using System.Collections.Generic;

namespace SSTUTools
{
    public class SSTUModularEngineCluster : PartModule, IPartCostModifier, IPartMassModifier, IRecolorable
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
        
        /// <summary>
        /// The mounting diameter of the engine; this is the diameter of the area that the gimbals and fuel-lines occupy and determines the minimum size of available mounts.
        /// </summary>
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

        [KSPField]
        public float minVerticalOffset = -2f;

        [KSPField]
        public float maxVerticalOffset = 2f;

        [KSPField]
        public float minSpacing = -2f;

        [KSPField]
        public float maxSpacing = 2f;

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

        [KSPField]
        public bool adjustMass = true;

        [KSPField]
        public bool adjustCost = true;
        
        #endregion ENDREGION - Standard KSPField variables

        #region REGION - KSP Editor Adjust Fields (Float Sliders) and KSP GUI Fields (visible data)

        /// <summary>
        /// Used for adjusting the inter-engine spacing.  Explicit value, specified in meters.
        /// </summary>
        [KSPField(guiName = "Spacing", guiActive = false, guiActiveEditor = true, isPersistant = true),
         UI_FloatEdit(sigFigs = 2, suppressEditorShipModified = true, minValue = -2, maxValue = 2, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.05f)]
        public float currentEngineSpacing = 0f;

        /// <summary>
        /// Determines the y-position of the engine model and bottom attach node position/fairing position.  Explicit value, specified in meters.
        /// </summary>
        [KSPField(guiName = "Vert. Pos.", guiActive = false, guiActiveEditor = true, isPersistant = true),
         UI_FloatEdit(sigFigs =2, suppressEditorShipModified = true, minValue = -2, maxValue = 2, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.05f)]
        public float currentEngineVerticalOffset = 0f;

        /// <summary>
        /// User selection and persistent value for the current engine layout.
        /// </summary>
        [KSPField(guiName = "Layout", guiActive = false, guiActiveEditor = true, isPersistant = true),
         UI_ChooseOption(display = new string[] { "Single" }, options = new string[] { "Single" }, suppressEditorShipModified = true)]
        public string currentEngineLayoutName = "Single";

        /// <summary>
        /// User selection and persistent value for the current engine mount.
        /// </summary>
        [KSPField(guiName = "Mount", guiActive = false, guiActiveEditor = true, isPersistant = true),
         UI_ChooseOption(display = new string[] { "Mount-None"}, options = new string[] { "Mount-None"}, suppressEditorShipModified = true)]
        public string currentMountName = "Mount-None";

        /// <summary>
        /// User selection and persistent value for the engine mount diameter.
        /// </summary>
        [KSPField(isPersistant = true, guiName = "Mount Diam", guiActive = false, guiActiveEditor = true),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentMountDiameter = 5;

        /// <summary>
        /// User selection and persistent value for the current engine mount texture set.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Mount Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentMountTexture = String.Empty;

        #endregion ENDREGION - KSP Editor Adjust Fields (Float Sliders)

        #region REGION - persistent save-data values, should not be edited in config

        [KSPField(isPersistant = true)]
        public string mountModuleData = string.Empty;

        [KSPField(isPersistant = true)]
        public bool fairingInitialized = false;

        [Persistent]
        public string configNodeData = string.Empty;

        #endregion ENDREGION - persistent save-data values, should not be edited in config

        #region REGION - Private working variables

        private ModelModule<EngineClusterLayoutMountData> mountModule;
        
        //private EngineClusterLayoutMountData currentMountData = null;
        private EngineClusterLayoutData[] engineLayouts;     
        private EngineClusterLayoutData currentEngineLayout = null;

        private bool initialized = false;

        private float engineMountingY = 0;
        private float fairingTopY = 0;
        private float fairingBottomY = 0;
        
        private float modifiedCost = -1f;
        private float modifiedMass = -1f;

        // cached thrust values, to remove the need to query the part config for the engine module config node
        // are initialized the first time the engines stats are updated (during Start())
        // this should allow it to catch any 'upgraded' stats for the engines.
        private float[] minThrustBase, maxThrustBase;
        private float[][] trsMults;

        #endregion ENDREGION - Private working variables

        #region REGION - GUI Interaction Methods

        [KSPEvent(guiName = "Select Mount", guiActive = false, guiActiveEditor = true)]
        public void selectMountEvent()
        {
            //TODO figure out best setup for module selection GUI interfacing
            //ModuleSelectionGUI.openGUI(ModelData.getValidSelections(part, currentEngineLayout.mountData, new string[] { "top" }), currentMountDiameter, updateMountFromEditor);
        }

        [KSPEvent(guiName = "Clear Mount Type", guiActive = false, guiActiveEditor = true, active = true)]
        public void clearMountEvent()
        {
            mountModule.modelSelected("Mount-None");
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

            bool useModelSelectionGUI = HighLogic.CurrentGame.Parameters.CustomParams<SSTUGameSettings>().useModelSelectGui;
            Fields[nameof(currentMountName)].guiActiveEditor = !useModelSelectionGUI && currentEngineLayout.mountData.Length > 1;
            Events[nameof(selectMountEvent)].guiActiveEditor = useModelSelectionGUI && currentEngineLayout.mountData.Length > 1;
            Events[nameof(clearMountEvent)].guiActiveEditor = currentEngineLayout.mountData.Length > 1;

            //setup the delegate methods for UI field interaction events/callbacks

            Fields[nameof(currentMountDiameter)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                if (currentMountDiameter < mountModule.model.minDiameter) { currentMountDiameter = mountModule.model.minDiameter; }
                if (currentMountDiameter > mountModule.model.maxDiameter) { currentMountDiameter = mountModule.model.maxDiameter; }
                this.actionWithSymmetry(m => 
                {
                    m.currentMountDiameter = currentMountDiameter;
                    m.mountModule.diameterUpdated(m.Fields[nameof(currentMountDiameter)], b);
                    m.positionMountModel();
                    m.positionEngineModels();
                    m.updateNodePositions(true);
                    m.updateFairing(true);
                    m.updateMountSizeGuiControl(false);
                    m.updatePartCostAndMass();
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentEngineLayoutName)].uiControlEditor.onFieldChanged = delegate(BaseField a, object b) 
            {
                this.actionWithSymmetry(m =>
                {
                    //TODO -- finish setting up for symmetry action handling
                    m.currentEngineLayoutName = currentEngineLayoutName;
                    m.currentEngineLayout = Array.Find(m.engineLayouts, s => s.layoutName == m.currentEngineLayoutName);
                    if (m.currentMountName == "Mount-None" && m.currentEngineLayout.isValidMount(m.currentMountName))
                    {
                        //NOOP
                    }
                    else
                    {
                        m.currentMountName = m.currentEngineLayout.defaultMount;
                    }

                    useModelSelectionGUI = HighLogic.CurrentGame.Parameters.CustomParams<SSTUGameSettings>().useModelSelectGui;
                    m.Fields[nameof(currentMountName)].guiActiveEditor = !useModelSelectionGUI && m.currentEngineLayout.mountData.Length > 1;
                    m.Events[nameof(selectMountEvent)].guiActiveEditor = useModelSelectionGUI && m.currentEngineLayout.mountData.Length > 1;
                    m.mountModule.setup(m.currentEngineLayout.mountData, nameof(m.currentMountName), nameof(m.currentMountTexture), nameof(m.currentMountDiameter));
                    m.mountModule.diameterUpdated(mountModule.model.initialDiameter);
                    m.positionMountModel();
                    m.setupEngineModels();
                    m.positionEngineModels();
                    m.reInitEngineModule();
                    m.updateNodePositions(true);
                    m.updateMountSizeGuiControl(true, m.mountModule.model.initialDiameter);
                    m.updateMountOptionsGuiControl();
                    m.updatePartCostAndMass();
                    m.updateGuiState();
                    m.updateFairing(true);
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentEngineVerticalOffset)].uiControlEditor.onFieldChanged = delegate(BaseField a, object b) 
            {
                this.actionWithSymmetry(m => 
                {
                    m.currentEngineVerticalOffset = currentEngineVerticalOffset;
                    m.positionEngineModels();
                    m.updateNodePositions(true);
                    m.updateFairing(true);
                    m.updateGuiState();
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentEngineSpacing)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b) 
            {
                this.actionWithSymmetry(m =>
                {
                    m.currentEngineSpacing = currentEngineSpacing;
                    m.positionEngineModels();
                    m.updateGuiState();
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentMountTexture)].uiControlEditor.onFieldChanged = mountModule.textureSetSelected;
            Fields[nameof(currentMountName)].uiControlEditor.onFieldChanged = mountModule.modelSelected;

            //TODO wtf is this?
            //currentEngineSpacing = currentEngineSpacing - currentEngineLayout.getEngineSpacing(engineScale, currentMountData);
            this.updateUIFloatEditControl(nameof(currentEngineVerticalOffset), minVerticalOffset, maxVerticalOffset, 0.5f, 0.25f, 0.05f, true, currentEngineVerticalOffset);
            this.updateUIFloatEditControl(nameof(currentEngineSpacing), minSpacing, maxSpacing, 0.5f, 0.25f, 0.05f, true, currentEngineSpacing);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
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
            if (!adjustCost) { return 0; }
            if (currentEngineLayout != null && mountModule!=null && mountModule.model != null)
            {
                modifiedCost = defaultCost * (float)currentEngineLayout.getLayoutData().positions.Count;
                modifiedCost += mountModule.model.getModuleCost();
            }
            else { return 0f; }
            return -defaultCost + modifiedCost;
        }

        //IModuleMassModifier Override
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (!adjustMass) { return 0; }
            if (modifiedMass < 0) { return 0; }
            return -defaultMass + modifiedMass;
        }
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        public string[] getSectionNames()
        {
            return new string[] { "Mount" };
        }

        public Color[] getSectionColors(string section)
        {
            return mountModule.customColors;
        }

        public void setSectionColors(string section, Color color1, Color color2, Color color3)
        {
            mountModule.setSectionColors(new Color[] { color1, color2, color3 });
        }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Initialization

        private void initialize()
        {
            if (initialized) { return; }
            loadConfigNodeData(SSTUConfigNodeUtils.parseConfigNode(configNodeData));
            removeStockTransforms();
            initializeSmokeTransform();
            positionMountModel();
            setupEngineModels();
            positionEngineModels();
            updateNodePositions(false);
            SSTUModInterop.onPartGeometryUpdate(part, true);
            updateLayoutOptionsGuiControl(false);
            updateMountSizeGuiControl(false);
            updateMountOptionsGuiControl();
            updatePartCostAndMass();
            updateGuiState();
        }

        private void initializePrefab(ConfigNode node)
        {
            loadConfigNodeData(node);
            //TODO -- fix all the spacing calcs, they are fubard
            //currentEngineSpacing = currentEngineLayout.getEngineSpacing(engineScale, mountModule.model) + editorEngineSpacingAdjust;
            removeStockTransforms();
            initializeSmokeTransform();
            positionMountModel();
            setupEngineModels();
            positionEngineModels();
            updatePartCostAndMass();
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
                currentEngineSpacing = currentEngineLayout.getEngineSpacing(engineScale, currentEngineLayout.getMountData(currentMountName));
            }
            //TODO
            //mountModule = new ModelModule<EngineClusterLayoutMountData>(part, this, null, Fields[nameof(mountModuleData)]);
            mountModule.getSymmetryModule = delegate (PartModule m) { return ((SSTUModularEngineCluster)m).mountModule; };
            mountModule.setup(new List<EngineClusterLayoutMountData>(currentEngineLayout.mountData), nameof(currentMountName), nameof(currentMountTexture), nameof(currentMountDiameter));
            if (currentMountDiameter > mountModule.model.maxDiameter) { currentMountDiameter = mountModule.model.maxDiameter; }
            if (currentMountDiameter < mountModule.model.minDiameter) { currentMountDiameter = mountModule.model.minDiameter; }
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
                engineLayouts[index] = new EngineClusterLayoutData(allBaseLayouts[key], localLayoutNode, engineScale, engineSpacing, engineMountDiameter, diameterIncrement, upperStageMounts, lowerStageMounts);
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

        //TODO replace this code into the model setup/part init stuff
        ///// <summary>
        ///// Sets up the actual models for the mount(s), but does not position or scale the models
        ///// </summary>
        //private void setupMountModel()
        //{
        //    Transform modelBase = part.transform.FindRecursive("model");
        //    Transform mountBaseTransform = modelBase.FindRecursive(mountTransformName);
        //    if (mountBaseTransform != null)
        //    {
        //        GameObject.DestroyImmediate(mountBaseTransform.gameObject);
        //    }

        //    GameObject newMountBaseGO = new GameObject(mountTransformName);
        //    mountBaseTransform = newMountBaseGO.transform;
        //    mountBaseTransform.NestToParent(modelBase);
                        
        //    currentMountData.setupModel(mountBaseTransform, ModelOrientation.BOTTOM);
        //}

        /// <summary>
        /// Position the mount model according to its current scale and model position/offset parameters.<para/>
        /// Sets model scale according to the local cached 'currentMountScale' field value, but does not calculate that value (it is determined by mount config)
        /// </summary>
        private void positionMountModel()
        {
            EngineClusterLayoutMountData currentMountData = mountModule.model;
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
            engineMountingY = partTopY + (engineYOffset * engineScale) - mountScaledHeight + currentEngineVerticalOffset;
            fairingBottomY = partTopY - (engineHeight * engineScale) - mountScaledHeight + currentEngineVerticalOffset;          
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

            float engineRotation;
            Transform[] models = part.transform.FindRecursive(engineTransformName).FindChildren(engineModelName);
            for (int i = 0; i < length; i++)
            {
                position = layout.positions[i];
                model = models[i].gameObject;
                posX = position.scaledX(currentEngineSpacing);
                posZ = position.scaledZ(currentEngineSpacing);
                rot = position.rotation;
                engineRotation = currentEngineLayout.getEngineRotation(mountModule.model, i);
                rot += engineRotation;
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

        #endregion ENDREGION - Model Setup

        #region REGION - Update Methods
                
        private void updatePartCostAndMass()
        {            
            SSTUEngineLayout layout = currentEngineLayout.getLayoutData();
            modifiedMass = part.prefabMass * (float)layout.positions.Count;
            modifiedMass += mountModule.model.getModuleMass();
        }

        private void updateMountSizeGuiControl(bool forceUpdate, float forceVal = 0)
        {
            bool active = mountModule.model.minDiameter != mountModule.model.maxDiameter;
            Fields[nameof(currentMountDiameter)].guiActiveEditor = active;
            if (active)
            {
                this.updateUIFloatEditControl(nameof(currentMountDiameter), mountModule.model.minDiameter, mountModule.model.maxDiameter, diameterIncrement * 2, diameterIncrement, diameterIncrement * 0.05f, forceUpdate, forceVal);
            }
        }

        private void updateMountOptionsGuiControl()
        {
            string[] optionsArray = SSTUUtils.getNames(currentEngineLayout.mountData, m => m.name);
            this.updateUIChooseOptionControl(nameof(currentMountName), optionsArray, optionsArray, true, currentMountName);
        }

        private void updateLayoutOptionsGuiControl(bool forceUpdate)
        {
            string[] optionsArray = SSTUUtils.getNames(engineLayouts, m => m.layoutName);
            this.updateUIChooseOptionControl(nameof(currentEngineLayoutName), optionsArray, optionsArray, forceUpdate, currentEngineLayoutName);
        }

        /// <summary>
        /// Updates the context-menu GUI buttons/etc as the config of the part changes.
        /// </summary>
        private void updateGuiState()
        {
            Events[nameof(clearMountEvent)].active = currentEngineLayout.mountData.Length > 1;
            Fields[nameof(currentEngineSpacing)].guiActiveEditor = currentEngineLayout.getLayoutData().positions.Count > 1;
        }
        
        /// <summary>
        /// Updates the position and enable/disable status of the SSTUNodeFairing (if present). <para/>
        /// </summary>
        private void updateFairing(bool userInput)
        {
            SSTUNodeFairing fairing = part.GetComponent<SSTUNodeFairing>();
            if (fairing == null) { return; }            
            bool enable = !mountModule.model.modelDefinition.fairingDisabled;
            AttachNode node = part.FindAttachNode("top");
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
            AttachNode bottomNode = part.FindAttachNode("bottom");
            if (bottomNode != null)
            {
                Vector3 pos = bottomNode.position;
                pos.y = fairingBottomY;
                SSTUAttachNodeUtils.updateAttachNodePosition(part, bottomNode, pos, bottomNode.orientation, userInput);
            }
                   
            if (!String.IsNullOrEmpty(interstageNodeName))
            {
                float y = partTopY + (mountModule.model.modelDefinition.fairingTopOffset * getCurrentMountScale());
                Vector3 pos = new Vector3(0, y, 0);
                SSTUSelectableNodes.updateNodePosition(part, interstageNodeName, pos);
                AttachNode interstage = part.FindAttachNode(interstageNodeName);
                if (interstage != null)
                {
                    Vector3 orientation = new Vector3(0, -1, 0);
                    SSTUAttachNodeUtils.updateAttachNodePosition(part, interstage, pos, orientation, userInput);
                }
            }
            AttachNode surface = part.srfAttachNode;
            if (surface != null)
            {
                Vector3 pos = new Vector3(0, partTopY, 0);
                Vector3 rot = new Vector3(0, 1, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, surface, pos, rot, userInput);
            }
        }
        
        #endregion ENDREGION - Update Methods

        #region REGION - Utility Methods

        /// <summary>
        /// Re-initializes the engine and gimbal modules from their original config nodes -- this should -hopefully- allow them to grab updated transforms and update FX stuff properly
        /// </summary>
        private void reInitEngineModule()
        {
            SSTUEngineLayout layout = currentEngineLayout.getLayoutData();
            StartState state = HighLogic.LoadedSceneIsEditor ? StartState.Editor : HighLogic.LoadedSceneIsFlight ? StartState.Flying : StartState.None;            
            
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
            if (minThrustBase == null)
            {
                setupThrustCache(engines);
                setupSplitThrustCache(engines);
            }
            ConfigNode engineNode;
            float maxThrust, minThrust;
            int positions = layout.positions.Count;
            for (int i = 0; i < engines.Length; i++)
            {
                engineNode = new ConfigNode("MODULE");
                minThrust = minThrustBase[i] * (float)positions;
                maxThrust = maxThrustBase[i] * (float)positions;
                engineNode.SetValue("minThrust", minThrust.ToString(), true);
                engineNode.SetValue("maxThrust", maxThrust.ToString(), true);
                if (trsMults[i].Length > 0)
                {
                    engineNode.AddNode(getSplitThrustNode(trsMults[i], positions));
                }
                engines[i].OnLoad(engineNode);//update min/max thrust, ISP/mass-flow, thrust-transform shares
                engines[i].OnStart(state);//re-initialize the effects
            }
            SSTUModInterop.onEngineConfigChange(part, null, positions);//this forces ModuleEngineConfigs to reload the config, with the # of engines as the 'scale'

            //update the gimbal modules, force them to reload transforms
            ModuleGimbal[] gimbals = part.GetComponents<ModuleGimbal>();
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

        private void setupThrustCache(ModuleEngines[] engines)
        {
            int len = engines.Length;
            minThrustBase = new float[len];
            maxThrustBase = new float[len];
            for (int i = 0; i < len; i++)
            {
                minThrustBase[i] = engines[i].minThrust;
                maxThrustBase[i] = engines[i].maxThrust;
            }
        }

        private void setupSplitThrustCache(ModuleEngines[] engines)
        {
            int len = engines.Length;
            int tLen;
            trsMults = new float[len][];
            List<float> mults;
            for (int i = 0; i < len; i++)
            {
                mults = engines[i].thrustTransformMultipliers;
                if (mults == null || mults.Count==0)
                {
                    trsMults[i] = new float[0];
                    continue;
                }
                tLen = mults.Count;
                trsMults[i] = new float[tLen];
                for (int k = 0; k < tLen; k++)
                {
                    trsMults[i][k] = mults[k];
                }
            }
        }

        /// <summary>
        /// Return a config node representing the 'split thrust transform' setup for this engine given the original input thrust-split setup and the number of engines currently in the part/model.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="positions"></param>
        /// <returns></returns>
        private ConfigNode getSplitThrustNode(float[] originalValues, int positions)
        {
            int numOfTransforms = originalValues.Length;            
            float newValue;
            ConfigNode output = new ConfigNode("transformMultipliers");
            int rawIndex = 0;
            float totalValue = 0;
            for (int i = 0; i < positions; i++)
            {
                for (int k = 0; k < numOfTransforms; k++, rawIndex++)
                {                    
                    newValue = originalValues[k] / (float)positions;
                    totalValue += newValue;
                    output.AddValue("trf" + rawIndex, newValue);
                }
            }
            return output;
        }

        /// <summary>
        /// Returns the current mount scale; calculated by the current user-set size and the default size specified in definition file
        /// </summary>
        /// <returns></returns>
        private float getCurrentMountScale()
        {
            return currentMountDiameter / mountModule.model.modelDefinition.diameter;
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
        public readonly float engineSpacing = -1f;
        public readonly float[] engineRotationOverride = new float[] { };
        public readonly float engineScale;

        //base layout for positional data
        private readonly SSTUEngineLayout layoutData;

        //available mounts for this layout
        public EngineClusterLayoutMountData[] mountData;

        public EngineClusterLayoutData(SSTUEngineLayout layoutData, ConfigNode node, float engineScale, float moduleEngineSpacing, float moduleMountSize, float increment, bool upperMounts, bool lowerMounts)
        {
            this.engineScale = engineScale;
            this.layoutData = layoutData;
            layoutName = layoutData.name;

            defaultMount = lowerMounts? layoutData.defaultLowerStageMount : layoutData.defaultUpperStageMount;
            engineSpacing = moduleEngineSpacing;

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
                engineSpacing = node.GetFloatValue("engineSpacing", engineSpacing);
                engineRotationOverride = node.GetFloatValuesCSV("rotateEngines", engineRotationOverride);
                ConfigNode[] mountNodes = node.GetNodes("MOUNT");
                len = mountNodes.Length;
                for (int i = 0; i < len; i++)
                {
                    mountNode = mountNodes[i];
                    name = mountNode.GetStringValue("name");
                    if (mountNode.GetBoolValue("remove", false))
                    {
                        globalMountOptions.Remove(name);
                    }
                    else
                    {
                        if (!globalMountOptions.ContainsKey(name))
                        {
                            customMounts.Add(mountNode);
                        }
                        localMountNodes.Add(name, mountNode);
                    }
                }
            }

            engineSpacing = engineSpacing * engineScale;//pre-scale the engine spacing by the engine scale value; needed for engine positioning and mount-size calculation
            moduleMountSize = moduleMountSize * engineScale;//pre-scale the mount size by the engine scale value; needed for mount-size calculation
            List<EngineClusterLayoutMountData> mountDataTemp = new List<EngineClusterLayoutMountData>();            
            foreach(String key in globalMountOptions.Keys)
            {
                if (localMountNodes.ContainsKey(key))//was specified in the config and was not a simple removal; merge values into global node...
                {
                    mountNode = mergeNodes(getAutoSizeNode(globalMountOptions[key], engineSpacing, moduleMountSize, increment), localMountNodes[key]);
                }
                else
                {
                    mountNode = getAutoSizeNode(globalMountOptions[key], engineSpacing, moduleMountSize, increment);
                }
                mountDataTemp.Add(new EngineClusterLayoutMountData(mountNode));
            }
            foreach (ConfigNode cm in customMounts)
            {
                mountDataTemp.Add(new EngineClusterLayoutMountData(cm));
            }
            mountData = mountDataTemp.ToArray();
        }

        /// <summary>
        /// Calculate engine mount size, minSize, and maxSize, and return a configNode defining those values for the input mount.
        /// </summary>
        /// <param name="option">The mount option as defined in the engine layout</param>
        /// <param name="engineSpacing">Pre-scaled engine spacing value</param>
        /// <param name="engineMountSize">Pre-scaled engine mount area value</param>
        /// <param name="increment">mount diameter increment as specified in the engine module</param>
        /// <returns></returns>
        private ConfigNode getAutoSizeNode(SSTUEngineLayoutMountOption option, float engineSpacing, float engineMountSize, float increment)
        {
            ModelDefinition mdf = SSTUModelData.getModelDefinition(option.mountName);
            float modelMountArea = mdf.configNode.GetFloatValue("mountingDiameter");//TODO clean up the need to cache the config node for a simple use
            float minSize = 2.5f, maxSize = 10f, size = 2.5f;
            calcAutoMountSize(engineSpacing, engineMountSize, mdf.diameter, modelMountArea, layoutData.mountSizeMult, increment, out size, out minSize, out maxSize);
            ConfigNode node = new ConfigNode("MOUNT");
            node.AddValue("name", option.mountName);
            node.AddValue("size", size);
            node.AddValue("minSize", minSize);
            node.AddValue("maxSize", maxSize);
            return node;
        }

        private void calcAutoMountSize(float scaledEngineSpacing, float scaledEngineMountingSize, float mountDiameter, float mountMountingSize, float layoutMultiplier, float increment, out float size, out float min, out float max)
        {
            float diff = scaledEngineSpacing - scaledEngineMountingSize;
            float areaNeeded = (scaledEngineSpacing * layoutMultiplier) - diff;
            float neededRawPercent = (areaNeeded) / mountMountingSize;
            float rawMountSize = mountDiameter * neededRawPercent;
            float wholeIncrements = Mathf.Ceil(rawMountSize / increment);
            size = wholeIncrements * increment;//round the raw calculated size to the next-highest mount-size increment
            float minMaxBonus = 0;
            if (wholeIncrements >= 3)//min = size - (size/4) - increment
            {
                minMaxBonus = (Mathf.Ceil((int)wholeIncrements / 4) + 1) * increment;
            }
            else if (wholeIncrements >= 2)//min = size-increment
            {
                minMaxBonus = increment;
            }
            min = size - minMaxBonus;
            //TODO - better handling of max size specification; needs to be adjustable through config from somewhere
            max = Mathf.Max(10, size + minMaxBonus);//minimum of 10m 'max' size
        }

        /// <summary>
        /// Merges global and local config nodes for an engine layout<para/>
        /// Local node values have priority if they are present; any non-specified local values are defaulted
        /// to the global value
        /// </summary>
        /// <param name="global"></param>
        /// <param name="local"></param>
        /// <returns></returns>
        private ConfigNode mergeNodes(ConfigNode global, ConfigNode local)
        {
            ConfigNode output = new ConfigNode("MOUNT");
            global.CopyTo(output);
            if (local.HasValue("canAdjustSize"))
            {
                output.RemoveValues("canAdjustSize");
                output.AddValue("canAdjustSize", local.GetBoolValue("canAdjustSize"));
            }
            if (local.HasValue("size"))
            {
                output.RemoveValues("size");
                output.AddValue("size", local.GetFloatValue("size"));
            }
            if (local.HasValue("minSize"))
            {
                output.RemoveValues("minSize");
                output.AddValue("minSize", local.GetFloatValue("minSize"));
            }
            if (local.HasValue("maxSize"))
            {
                output.RemoveValues("maxSize");
                output.AddValue("maxSize", local.GetFloatValue("maxSize"));
            }
            if (local.HasValue("engineSpacing"))
            {
                output.RemoveValues("engineSpacing");
                output.AddValue("engineSpacing", local.GetFloatValue("engineSpacing"));
            }
            if (local.HasValue("rotateEngines"))
            {
                output.RemoveValues("rotateEngines");
                output.AddValue("rotateEngines", local.GetStringValue("rotateEngines"));
            }

            return output;
        }

        public float getEngineSpacing(float engineScale, EngineClusterLayoutMountData mount)
        {
            if (mount.engineSpacing != -1)
            {
                return mount.engineSpacing * engineScale;
            }
            return engineSpacing;
        }

        public float getEngineRotation(EngineClusterLayoutMountData mount, int positionIndex)
        {
            float[] vals = mount.rotateEngines.Length > 0 ? mount.rotateEngines : engineRotationOverride;
            float val = 0;
            if (vals.Length > 0)
            {
                if (vals.Length == 1) { val = vals[0]; }
                else { val = vals[positionIndex]; }
            }
            return val;
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
        public readonly float engineSpacing = -1;
        public readonly float[] rotateEngines;
        
        public EngineClusterLayoutMountData(ConfigNode node) : base(node)
        {
            canAdjustSize = node.GetBoolValue("canAdjustSize", canAdjustSize);
            initialDiameter = node.GetFloatValue("size", initialDiameter);
            minDiameter = node.GetFloatValue("minSize", initialDiameter);
            maxDiameter = node.GetFloatValue("maxSize", initialDiameter);
            rotateEngines = node.GetFloatValuesCSV("rotateEngines", new float[] {});
            engineSpacing = node.GetFloatValue("engineSpacing", engineSpacing);
            if (String.IsNullOrEmpty(modelDefinition.modelName)) { canAdjustSize = false; }
        }
    }

}

