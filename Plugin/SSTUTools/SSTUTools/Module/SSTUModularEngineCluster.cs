using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KSPShaderTools;

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
        /// This field determines how much vertical offset should be given to the engine model, to correct for the default-COM-based positioning of stock-configured engine models in their model file.
        /// A positive value will move the model up, a negative value moves it down.
        /// COM Offset is handled through the partTopY config value.
        /// Should be the value of the distance between part origin and the top mounting plane of the part, as a negative value (as you are moving the engine model downward to place the mounting plane at COM/origin)
        /// </summary>
        [KSPField]
        public float engineYOffset = 0f;

        /// <summary>
        /// Should this engine load and use the upper stage mounts.
        /// </summary>
        [KSPField]
        public bool upperStageMounts = true;

        /// <summary>
        /// Should this engine load and use the lower stage mounts.
        /// </summary>
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
        
        /// <summary>
        /// The engine mount model will be childed to a transform of this name.  The transform will be created by the plugin on the prefab model.
        /// </summary>
        [KSPField]
        public String mountTransformName = "SSTEngineClusterMounts";

        /// <summary>
        /// The engine models will be be childed to a transform of this name.  The transform will be created by the plugin on the prefab model.
        /// </summary>
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
         UI_FloatEdit(sigFigs =2, suppressEditorShipModified = true, minValue = -2, maxValue = 2, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentEngineVerticalOffset = 0f;

        /// <summary>
        /// User selection and persistent value for the current engine layout.
        /// </summary>
        [KSPField(guiName = "Layout", guiActive = false, guiActiveEditor = true, isPersistant = true),
         UI_ChooseOption(display = new string[] { "Single" }, options = new string[] { "Single" }, suppressEditorShipModified = true)]
        public string currentEngineLayoutName = "Single";

        /// <summary>
        /// User selection and persistent value for the engine rotation offset
        /// </summary>
        [KSPField(isPersistant = true, guiName = "Engine Rotation", guiActive = false, guiActiveEditor = true),
         UI_FloatEdit(sigFigs = 1, suppressEditorShipModified = true, minValue = -180, maxValue = 180, incrementSlide = 0.5f, incrementLarge = 90f, incrementSmall = 45f)]
        public float currentEngineRotation = 0;

        /// <summary>
        /// User selection and persistent value for the current engine mount.
        /// </summary>
        [KSPField(guiName = "Mount", guiActive = false, guiActiveEditor = true, isPersistant = true),
         UI_ChooseOption(display = new string[] { "Mount-None"}, options = new string[] { "Mount-None" }, suppressEditorShipModified = true)]
        public string currentMountName = string.Empty;

        /// <summary>
        /// User selection and persistent value for the engine mount diameter.
        /// </summary>
        [KSPField(isPersistant = true, guiName = "Mount Diam", guiActive = false, guiActiveEditor = true),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentMountDiameter = 5;

        /// <summary>
        /// User selection and persistent value for the current engine mount texture set.
        /// If left blank in config file, initialized to the default texture set for the current mount.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Mount Texture"),
         UI_ChooseOption(display = new string[] { "default" }, options = new string[] { "default" }, suppressEditorShipModified = true)]
        public String currentMountTexture = String.Empty;

        #endregion ENDREGION - KSP Editor Adjust Fields (Float Sliders)

        #region REGION - persistent save-data values, should not be edited in config

        /// <summary>
        /// Persistent data for the mount module.  Used to store custom color data or any other module-specific data that needs to be persistent.
        /// Should be passed to the module upon initialization, and it will handle updating the field internally.
        /// </summary>
        [KSPField(isPersistant = true)]
        public string mountModuleData = string.Empty;

        /// <summary>
        /// //TODO -- move this into the fairing module as an 'extInitialized' field.
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool fairingInitialized = false;

        /// <summary>
        /// Used to persist the custom config node data from the modules' config node from the prefab parts into the child parts.  
        /// </summary>
        [Persistent]
        public string configNodeData = string.Empty;

        #endregion ENDREGION - persistent save-data values, should not be edited in config

        #region REGION - Private working variables

        private ModelModule<SSTUModularEngineCluster> mountModule;
        private EngineClusterLayoutData[] engineLayouts;
        private EngineClusterLayoutData currentEngineLayout = null;
        private EngineClusterLayoutMountData mountData
        {
            get { return currentEngineLayout.getMountData(currentMountName); }
        }

        private bool initialized = false;

        //cached posiion values used to update attach nodes and fairing parameters.
        private float engineMountingY = 0;
        private float partBottomY = 0;

        //cached modifiedCost/modifiedMass values, updated on initialization and whenever engine layout changes
        //both of these use cubic scaling, and use the config-specified mass/cost as the base values
        private float modifiedCost = 0;
        private float modifiedMass = 0;
        private int positions = 1;//used as a multiplier to the default cost and mass, set during layout updating

        // cached thrust values, to remove the need to query the part config for the engine module config node
        // are initialized the first time the engines stats are updated (during Start())
        // this should allow it to catch any 'upgraded' stats for the engines.
        /// <summary>
        /// Used to track if the cached engine thrust values can be trusted, or need to be rebuilt (as Unity serialization does not support null)
        /// </summary>
        public bool engineInitialized = false;
        public float[] minThrustBase;
        public float[] maxThrustBase;
        public float[] trsMults;
        public int[] trsMultInd;

        #endregion ENDREGION - Private working variables

        #region REGION - GUI Interaction Methods

        [KSPEvent(guiName = "Clear Mount Type", guiActive = false, guiActiveEditor = true, active = true)]
        public void clearMountEvent()
        {
            this.actionWithSymmetry(m => 
            {
                m.mountModule.modelSelected("Mount-None");
                m.updateEditorStats(true);
                m.updateMountSizeGuiControl(true, m.mountData.initialDiameter);
                MonoUtilities.RefreshContextWindows(m.part);
            });
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
            
            //setup the delegate methods for UI field interaction events/callbacks

            Fields[nameof(currentMountName)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b) 
            {
                mountModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    if (m.currentMountDiameter < mountData.minDiameter) { m.currentMountDiameter = mountData.minDiameter; }
                    if (m.currentMountDiameter > mountData.maxDiameter) { m.currentMountDiameter = mountData.maxDiameter; }
                    m.updateMountSizeGuiControl(true, m.currentMountDiameter);
                    m.updateEditorStats(true);
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentMountDiameter)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.currentMountDiameter = currentMountDiameter;
                    if (m.currentMountDiameter < m.mountData.minDiameter) { m.currentMountDiameter = m.mountData.minDiameter; }
                    if (m.currentMountDiameter > m.mountData.maxDiameter) { m.currentMountDiameter = m.mountData.maxDiameter; }
                    m.updateEditorStats(true);
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentEngineRotation)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.currentEngineRotation = currentEngineRotation;
                    m.positionEngineModels();
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
                    m.Fields[nameof(currentMountName)].guiActiveEditor = m.currentEngineLayout.mountData.Length > 1;                  
                    m.setupMountModel();
                    m.currentMountDiameter = m.mountData.initialDiameter;
                    m.setupEngineModels();
                    m.updateEditorStats(true);
                    m.reInitEngineModule();
                    m.updateMountSizeGuiControl(true, m.mountData.initialDiameter);
                    m.updateGuiState();
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentEngineVerticalOffset)].uiControlEditor.onFieldChanged = delegate(BaseField a, object b) 
            {
                this.actionWithSymmetry(m => 
                {
                    m.currentEngineVerticalOffset = currentEngineVerticalOffset;
                    m.positionMountModel();//updates engine mounting position and fairing position including the new engine offset...method should probably be renamed... or functions split off...
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

            //mapped directly to the modules update fields
            Fields[nameof(currentMountTexture)].uiControlEditor.onFieldChanged = mountModule.textureSetSelected;

            //update config specified min/max on the spacing and offset controls
            //TODO -- clamp current to min/max
            //TODO -- if min/max == same, disable UI field
            this.updateUIFloatEditControl(nameof(currentEngineVerticalOffset), minVerticalOffset, maxVerticalOffset, 0.5f, 0.25f, 0.01f, true, currentEngineVerticalOffset);
            this.updateUIFloatEditControl(nameof(currentEngineSpacing), minSpacing, maxSpacing, 0.5f, 0.25f, 0.05f, true, currentEngineSpacing);

            string[] optionsArray = SSTUUtils.getNames(engineLayouts, m => m.layoutName);
            this.updateUIChooseOptionControl(nameof(currentEngineLayoutName), optionsArray, optionsArray, false);

            Fields[nameof(currentMountName)].guiActiveEditor = currentEngineLayout.mountData.Length > 1;
            Events[nameof(clearMountEvent)].guiActiveEditor = currentEngineLayout.mountData.Length > 1;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
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

        public override string GetInfo()
        {
            //TODO -- add list of layouts and mounting options
            //TODO -- how to specify custom module title; IModuleInfo interface or etc? -- Check KSPWheels
            return "This part may have multiple mount variants in the editor, right click for more info.";
        }

        //IModuleCostModifier Override
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (!adjustCost) { return 0; }
            return -defaultCost + positions * defaultCost + modifiedCost;
        }

        //IModuleMassModifier Override
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (!adjustMass) { return 0; }
            return -defaultMass + positions * defaultMass + modifiedMass;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        //IRecolorable interaction methods.
        public string[] getSectionNames()
        {
            return new string[] { "Mount" };
        }

        public RecoloringData[] getSectionColors(string section)
        {
            return mountModule.recoloringData;
        }

        public void setSectionColors(string section, RecoloringData[] colors)
        {
            mountModule.setSectionColors(colors);
        }

        //IRecolorable override
        public TextureSet getSectionTexture(string section)
        {
            return mountModule.textureSet;
        }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Initialization

        private void initialize()
        {
            if (initialized) { return; }

            ConfigNode[] layoutNodes = SSTUConfigNodeUtils.parseConfigNode(configNodeData).GetNodes("LAYOUT");
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
            }
            initializeSmokeTransform();
            setupMountModule();
            setupMountModel();
            setupEngineModels();
            updateEditorStats(false);
            updateGuiState();
            SSTUModInterop.onPartGeometryUpdate(part, true);
            //outputMountInfo();
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
            Transform smokeTransform = modelBase.FindOrCreate(smokeTransformName);
            smokeTransform.NestToParent(modelBase);
            smokeTransform.localRotation = Quaternion.AngleAxis(90, new Vector3(1, 0, 0));//set it to default pointing downwards, as-per a thrust transform
        }
        
        /// <summary>
        /// Initialization method.
        /// Sets up the mount module and mount transform
        /// </summary>
        private void setupMountModule()
        {
            Transform mountTransform = part.transform.FindRecursive("model").FindRecursive(mountTransformName);
            if (mountTransform == null)
            {
                mountTransform = new GameObject(mountTransformName).transform;
                mountTransform.NestToParent(part.transform.FindRecursive("model"));
            }
            mountModule = new ModelModule<SSTUModularEngineCluster>(part, this, mountTransform, ModelOrientation.BOTTOM, nameof(currentMountName), null, nameof(currentMountTexture), nameof(mountModuleData), null, null, null, null);
            mountModule.getSymmetryModule = m => m.mountModule;
            mountModule.getValidOptions = () => currentEngineLayout.getMountModelDefinitions();
        }

        private void outputMountInfo()
        {
            MonoBehaviour.print("-------------------------------");
            MonoBehaviour.print("Mount config for: " + part);
            MonoBehaviour.print("Mounting size: " + engineMountDiameter);
            MonoBehaviour.print("Spacing: " + engineSpacing);
            int len = engineLayouts.Length;
            for (int i = 0; i < len; i++)
            {
                EngineClusterLayoutData ecld = engineLayouts[i];
                int len2 = ecld.mountData.Length;
                string menuBar = "BaseSz - MntAr  - Min    - Max    - Default - Layout -       Mount";
                MonoBehaviour.print(menuBar);
                for (int k = 0; k < len2; k++)
                {
                    EngineClusterLayoutMountData eclmd = ecld.mountData[k];
                    float bse = eclmd.modelDefinition.diameter;
                    float bmd = eclmd.modelDefinition.lowerDiameter;
                    float min = eclmd.minDiameter;
                    float max = eclmd.maxDiameter;
                    float def = eclmd.initialDiameter;
                    string output = string.Format("{0,4:#,0.00} - {1,4:#,0.00} - {2,4:#,0.00} - {3,4:#,0.00} - {4,4:#,0.00} - {5,15:#} - {6:#}", bse, bmd, min, max, def, ecld.layoutName, eclmd.modelDefinition.name);
                    MonoBehaviour.print(output);
                }
            }
            MonoBehaviour.print("-------------------------------");
        }

        #endregion ENDREGION - Initialization

        #region REGION - Model Setup and Updating

        /// <summary>
        /// Sets up the mount model and update GUI controls for min/max size.
        /// </summary>
        private void setupMountModel()
        {
            mountModule.setupModelList(currentEngineLayout.getMountModelDefinitions());
            updateMountSizeGuiControl(false);
            if (currentMountDiameter > mountData.maxDiameter) { currentMountDiameter = mountData.maxDiameter; }
            if (currentMountDiameter < mountData.minDiameter) { currentMountDiameter = mountData.minDiameter; }
            mountModule.setupModel();
            mountModule.updateSelections();
        }

        /// <summary>
        /// Position the mount model according to its current scale and model position/offset parameters.<para/>
        /// Sets model scale according to the local cached 'currentMountScale' field value, but does not calculate that value (it is determined by mount config)
        /// </summary>
        private void positionMountModel()
        {
            mountModule.setPosition(partTopY);
            mountModule.setScaleForDiameter(currentMountDiameter);
            mountModule.updateModelMeshes();
            //set up fairing/engine/node positions
            float mountScaledHeight = mountModule.moduleHeight;
            engineMountingY = partTopY + (engineYOffset * engineScale) - mountScaledHeight + currentEngineVerticalOffset;
            partBottomY = partTopY - (engineHeight * engineScale) - mountScaledHeight + currentEngineVerticalOffset;
        }

        /// <summary>
        /// Removes existing engine models and create new models, but does not position or rotate them
        /// </summary>
        private void setupEngineModels()
        {
            Transform modelBase = part.transform.FindRecursive("model");
            Transform engineBaseTransform = modelBase.FindRecursive(engineTransformName);
            if (engineBaseTransform != null)
            {
                //using destroy immediate as otherwise it leaves ghost transforms that will be picked up by the stock engine/gimbal/effects modules when they initialize
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

            //remove stock transforms
            if (!String.IsNullOrEmpty(transformsToRemove))
            {
                String[] names = SSTUUtils.parseCSV(transformsToRemove);
                SSTUUtils.removeTransforms(part, names);
            }
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
            float currentEngineSpacing = currentEngineLayout.getEngineSpacing(engineScale, mountData) + this.currentEngineSpacing;
            for (int i = 0; i < length; i++)
            {
                position = layout.positions[i];
                model = models[i].gameObject;
                posX = position.scaledX(currentEngineSpacing);
                posZ = position.scaledZ(currentEngineSpacing);
                rot = position.rotation;
                engineRotation = currentEngineLayout.getEngineRotation(mountData, i);
                rot += engineRotation + (currentEngineRotation * position.rotationDirection);
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

        private void updateEditorStats(bool userInput)
        {
            positionMountModel();
            positionEngineModels();
            updatePartCostAndMass();
            updateFairing(userInput);
            updateNodePositions(userInput);
        }

        /// <summary>
        /// Updates the cached positions, mass, and cost values for the current configuration
        /// </summary>
        private void updatePartCostAndMass()
        {
            positions = currentEngineLayout.getLayoutData().positions.Count;
            modifiedMass = mountModule.moduleMass;
            modifiedCost = mountModule.moduleCost;
        }

        /// <summary>
        /// Updates the min/max value for the mount diameter GUI control for the current engine layout
        /// </summary>
        /// <param name="forceUpdate"></param>
        /// <param name="forceVal"></param>
        private void updateMountSizeGuiControl(bool forceUpdate, float forceVal = 0)
        {
            bool active = mountData.minDiameter < mountData.maxDiameter;
            Fields[nameof(currentMountDiameter)].guiActiveEditor = active;
            if (active)
            {
                this.updateUIFloatEditControl(nameof(currentMountDiameter), mountData.minDiameter, mountData.maxDiameter, diameterIncrement * 2, diameterIncrement, diameterIncrement * 0.05f, forceUpdate, forceVal);
            }
        }

        /// <summary>
        /// Updates the clear mount and engine spacing enable/disable states based on mount availability and engine count
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
            bool enable = mountModule.fairingEnabled;
            fairing.canDisableInEditor = enable;
            FairingUpdateData data = new FairingUpdateData();
            data.setTopY(mountModule.fairingTop);
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
            AttachNode topNode = part.FindAttachNode("top");
            if (topNode != null)
            {
                Vector3 pos = new Vector3(0, mountModule.moduleTop, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, topNode, pos, topNode.orientation, userInput);
                //mountModule.updateAttachNodes(new string[] { "top" }, userInput);//won't work because mounts are defined with two attach nodes.... and we're only using one of them
                //it might be incorrectly grabbing the 2nd one due to KSPs current config node value order problems
            }

            AttachNode bottomNode = part.FindAttachNode("bottom");
            if (bottomNode != null)
            {
                Vector3 pos = bottomNode.position;
                pos.y = partBottomY;
                SSTUAttachNodeUtils.updateAttachNodePosition(part, bottomNode, pos, bottomNode.orientation, userInput);
            }
                   
            if (!String.IsNullOrEmpty(interstageNodeName))
            {
                float y = mountModule.fairingTop;
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
            SSTUDeployableEngine deployable = part.GetComponent<SSTUDeployableEngine>();
            if (deployable != null)
            {
                deployable.reInitialize();
            }

            SSTUAnimateEngineHeat heatAnim = part.GetComponent<SSTUAnimateEngineHeat>();
            if (heatAnim != null)
            {
                heatAnim.reInitialize();
            }

            //update the engine module(s), forcing them to to reload their thrust, transforms, and effects.
            ModuleEngines[] engines = part.GetComponents<ModuleEngines>();
            if (!engineInitialized)
            {
                engineInitialized = true;
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
                float[] trsMults = getSplitThrustCache(i);
                if (trsMults.Length > 0)
                {
                    engineNode.AddNode(getSplitThrustNode(trsMults, positions));
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
            trsMultInd = new int[len];
            List<float> mults;
            List<float> output = new List<float>();
            for (int i = 0; i < len; i++)
            {
                mults = engines[i].thrustTransformMultipliers;
                if (mults == null || mults.Count==0)
                {
                    trsMultInd[i] = 0;
                    continue;
                }
                tLen = mults.Count;
                trsMultInd[i] = tLen;
                output.AddRange(mults);
            }
            trsMults = output.ToArray();
        }

        private float[] getSplitThrustCache(int engineIndex)
        {
            int start = 0;
            for (int i = 0; i < engineIndex; i++)
            {
                start += trsMultInd[i];
            }
            int length = trsMultInd[engineIndex];
            float[] cache = new float[length];
            for (int i = 0, k = start; i < length; i++, k++)
            {
                cache[i] = trsMults[k];
            }
            return cache;
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
            return currentMountDiameter / mountModule.definition.diameter;
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
        public readonly EngineClusterLayoutMountData[] mountData;

        private ModelDefinitionLayoutOptions[] mountDefCache;

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
            float modelMountArea = mdf.lowerDiameter;
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

        public ModelDefinitionLayoutOptions[] getMountModelDefinitions()
        {
            if (mountDefCache == null)
            {
                string[] names = SSTUUtils.getNames(mountData, m => m.name);
                mountDefCache = SSTUModelData.getModelDefinitionLayouts(names);
            }
            return mountDefCache;
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

    public class EngineClusterLayoutMountData
    {
        public readonly string name;
        public readonly bool canAdjustSize = true;
        public readonly float initialDiameter = 1.25f;
        public readonly float minDiameter;
        public readonly float maxDiameter;
        public readonly float engineSpacing = -1;
        public readonly float[] rotateEngines;

        public ModelDefinition modelDefinition { get { return SSTUModelData.getModelDefinition(name); } }
        
        public EngineClusterLayoutMountData(ConfigNode node)
        {
            name = node.GetStringValue("name");
            canAdjustSize = node.GetBoolValue("canAdjustSize", canAdjustSize);
            initialDiameter = node.GetFloatValue("size", initialDiameter);
            minDiameter = node.GetFloatValue("minSize", initialDiameter);
            maxDiameter = node.GetFloatValue("maxSize", initialDiameter);
            rotateEngines = node.GetFloatValuesCSV("rotateEngines", new float[] {});
            engineSpacing = node.GetFloatValue("engineSpacing", engineSpacing);
        }
    }

}

