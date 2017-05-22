using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{

    public class SSTUModularFuelTank : PartModule, IPartCostModifier, IPartMassModifier, IRecolorable
    {

        #region REGION - Config Fields

        [KSPField]
        public String rootTransformName = "SSTUModularFuelTankRoot";

        [KSPField]
        public String rootNoseTransformName = "SSTUModularFuelTankNoseRoot";

        [KSPField]
        public String rootMountTransformName = "SSTUModularFuelTankMountRoot";

        [KSPField]
        public float tankDiameterIncrement = 0.625f;

        [KSPField]
        public float minTankDiameter = 0.625f;

        [KSPField]
        public float maxTankDiameter = 10f;

        [KSPField]
        public float topAdapterRatio = 1f;

        [KSPField]
        public float bottomAdapterRatio = 1f;

        [KSPField]
        public String topManagedNodeNames = "top, top2, top3, top4";

        [KSPField]
        public String bottomManagedNodeNames = "bottom, bottom2, bottom3, bottom4";

        [KSPField]
        public String interstageNodeName = "interstage";

        [KSPField]
        public bool subtractMass = false;

        [KSPField]
        public bool subtractCost = false;

        [KSPField]
        public string noseAnimationID = string.Empty;

        [KSPField]
        public string bodyAnimationID = string.Empty;

        [KSPField]
        public string mountAnimationID = string.Empty;

        // The 'currentXXX' fields are used in the config to define the default values for initialization purposes; else if they are empty/null, they are set to the first available of the specified type
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Body Length"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentTankSet = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Body Variant"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentTankType = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Nose"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentNoseType = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Mount"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentMountType = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Diameter"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentTankDiameter = 2.5f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "V.Scale"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentTankVerticalScale = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Nose Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentNoseTexture = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Body Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentTankTexture = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Mount Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentMountTexture = String.Empty;

        /// <summary>
        /// Used solely to track if volumeContainer has been initialized with the volume for this MFT; 
        /// checked and updated during OnStart, and should generally only run in the editor the first
        /// time the part is ever initialized
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        [KSPField(isPersistant = true)]
        public string noseModuleData = string.Empty;

        [KSPField(isPersistant = true)]
        public string bodyModuleData = string.Empty;

        [KSPField(isPersistant = true)]
        public string mountModuleData = string.Empty;
        
        [Persistent]
        public string configNodeData = string.Empty;

        #endregion

        #region REGION - private working variables

        private float prevTankDiameter;
        private float prevTankHeightScale;

        private float currentTankVolume;
        private float currentTankMass = -1;
        private float currentTankCost = -1;
        
        private TankSet[] tankSets;
        private TankSet currentTankSetModule;

        protected ModelModule<TankModelData, SSTUModularFuelTank> tankModule;
        protected ModelModule<SingleModelData, SSTUModularFuelTank> noseModule;
        protected ModelModule<SingleModelData, SSTUModularFuelTank> mountModule;

        protected String[] topNodeNames;
        protected String[] bottomNodeNames;

        //populated during init to the variant type of the currently selected model data
        private string lastSelectedVariant;

        protected bool initialized = false;

        #endregion

        #region REGION - GUI Events/Interaction methods

        [KSPEvent(guiName = "Select Nose", guiActive = false, guiActiveEditor = true)]
        public void selectNoseEvent()
        {
            ModuleSelectionGUI.openGUI(ModelData.getValidSelections(part, noseModule.models, topNodeNames), currentTankDiameter, delegate (string a, bool b)
            {
                noseModule.modelSelected(a);
                this.actionWithSymmetry(m =>
                {
                    m.updateEditorStats(true);
                });
            });
        }

        [KSPEvent(guiName = "Select Mount", guiActive = false, guiActiveEditor = true)]
        public void selectMountEvent()
        {
            ModuleSelectionGUI.openGUI(ModelData.getValidSelections(part, mountModule.models, bottomNodeNames), currentTankDiameter, delegate (string a, bool b)
            {
                mountModule.modelSelected(a);
                this.actionWithSymmetry(m =>
                {
                    m.updateEditorStats(true);
                });
            });
        }

        private void updateAvailableVariants()
        {
            noseModule.updateSelections();
            mountModule.updateSelections();
            bool useModelSelectionGUI = HighLogic.CurrentGame.Parameters.CustomParams<SSTUGameSettings>().useModelSelectGui;
            Fields[nameof(currentNoseType)].guiActiveEditor = !useModelSelectionGUI && noseModule.models.Count > 1;
            Fields[nameof(currentMountType)].guiActiveEditor = !useModelSelectionGUI && mountModule.models.Count > 1;
        }

        private void updateUIScaleControls()
        {
            float min = 0.5f;
            float max = 1.5f;
            min = tankModule.model.minVerticalScale;
            max = tankModule.model.maxVerticalScale;
            float diff = max - min;
            if (diff > 0)
            {
                this.updateUIFloatEditControl(nameof(currentTankVerticalScale), min, max, diff * 0.5f, diff * 0.25f, diff * 0.01f, true, currentTankVerticalScale);
            }
            Fields[nameof(currentTankVerticalScale)].guiActiveEditor = min != 1 || max != 1;
        }

        #endregion

        #region REGION - Standard KSP Overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();

            string[] groupNames = TankSet.getSetNames(tankSets);
            this.updateUIChooseOptionControl("currentTankSet", groupNames, groupNames, true, currentTankSet);

            string[] names = currentTankSetModule.getModelNames();
            string[] descs = currentTankSetModule.getTankDescriptions();
            this.updateUIChooseOptionControl("currentTankType", names, descs, true, currentTankType);

            if (maxTankDiameter == minTankDiameter)
            {
                Fields[nameof(currentTankDiameter)].guiActiveEditor = false;
            }
            else
            {
                this.updateUIFloatEditControl(nameof(currentTankDiameter), minTankDiameter, maxTankDiameter, tankDiameterIncrement * 2, tankDiameterIncrement, tankDiameterIncrement * 0.05f, true, currentTankDiameter);
            }
            updateAvailableVariants();
            updateUIScaleControls();

            bool useModelSelectionGUI = HighLogic.CurrentGame.Parameters.CustomParams<SSTUGameSettings>().useModelSelectGui;

            Events[nameof(selectNoseEvent)].guiActiveEditor = useModelSelectionGUI && noseModule.models.Count > 0;
            Events[nameof(selectMountEvent)].guiActiveEditor = useModelSelectionGUI && mountModule.models.Count > 0;

            Fields[nameof(currentTankDiameter)].uiControlEditor.onFieldChanged = delegate(BaseField a, object b) 
            {
                this.actionWithSymmetry(m =>
                {
                    m.updateEditorStats(true);
                    SSTUAttachNodeUtils.updateSurfaceAttachedChildren(m.part, m.prevTankDiameter, m.currentTankDiameter);
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentTankVerticalScale)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b) 
            {
                this.actionWithSymmetry(m =>
                {
                    m.updateEditorStats(true);
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentTankSet)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                this.actionWithSymmetry(m =>
                {
                    TankSet newSet = Array.Find(m.tankSets, s => s.name == m.currentTankSet);
                    m.currentTankSetModule = newSet;
                    string variant = lastSelectedVariant;
                    m.currentTankType = newSet.getDefaultModel(lastSelectedVariant);
                    m.tankModule.updateSelections();
                    m.tankModule.modelSelected(m.currentTankType);
                    m.Fields[nameof(currentTankType)].guiActiveEditor = newSet.Length > 1;
                    //re-seat this if it was changed in the 'setMainTankModuleFromEditor' method
                    //will allow for user-initiated main-tank changes to still change the 'last variant' but will
                    //persist the variant if the newly selected set did not contain the selected variant
                    //so that it will persist to the next set selection, OR be reseated on the next user-tank selection within the current set
                    if (!currentTankSetModule.hasVariant(variant)) { lastSelectedVariant = variant; }
                    m.updateEditorStats(true);
                    m.updateUIScaleControls();
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentNoseType)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                noseModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.updateEditorStats(true);
                    m.updateAnimationControl(m.noseAnimationID, m.noseModule.model, 1);
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentTankType)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                tankModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.updateEditorStats(true);
                    m.lastSelectedVariant = tankModule.model.variantName;
                    m.updateAnimationControl(m.bodyAnimationID, m.tankModule.model, 3);
                    m.updateUIScaleControls();
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentMountType)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                mountModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.updateEditorStats(true);
                    m.updateAnimationControl(m.mountAnimationID, m.mountModule.model, 5);
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentNoseTexture)].uiControlEditor.onFieldChanged = noseModule.textureSetSelected;
            Fields[nameof(currentTankTexture)].uiControlEditor.onFieldChanged = tankModule.textureSetSelected;
            Fields[nameof(currentMountTexture)].uiControlEditor.onFieldChanged = mountModule.textureSetSelected;

            Fields[nameof(currentTankSet)].guiActiveEditor = tankSets.Length > 1;
            Fields[nameof(currentTankType)].guiActiveEditor = currentTankSetModule.Length > 1;
            Fields[nameof(currentNoseType)].guiActiveEditor = !useModelSelectionGUI && noseModule.models.Count > 1;
            Fields[nameof(currentMountType)].guiActiveEditor = !useModelSelectionGUI && mountModule.models.Count > 1;

            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        public void Start()
        {
            if (!initializedResources && HighLogic.LoadedSceneIsEditor)
            {
                initializedResources = true;
                updateContainerVolume();
            }
            updateAnimationControl(noseAnimationID, noseModule.model, 1);
            updateAnimationControl(bodyAnimationID, tankModule.model, 3);
            updateAnimationControl(mountAnimationID, mountModule.model, 5);
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }
        
        /// <summary>
        /// Adds a quick blurb to the right-click menu regarding possible additional part functionality.
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
            //Shader maskedIconShader = SSTUDatabase.getShader("SSTU/MaskedIcon");
            //Transform modelMesh;
            //Transform modelRoot;
            //Renderer iconRenderer;
            //Renderer modelRenderer;
            //if (part.partInfo != null && part.partInfo.iconPrefab != null)
            //{
            //    modelRoot = part.transform.FindRecursive("model");
            //    Renderer[] rs = part.partInfo.iconPrefab.GetComponentsInChildren<Renderer>();
            //    int len = rs.Length;
            //    for (int i = 0; i < len; i++)
            //    {
            //        iconRenderer = rs[i];
            //        modelMesh = modelRoot.FindRecursive(iconRenderer.name);
            //        if (modelMesh != null)
            //        {
            //            modelRenderer = modelMesh.GetComponent<Renderer>();
            //            if (modelRenderer.sharedMaterial.shader.name.Equals("SSTU/Masked"))
            //            {
            //                iconRenderer.sharedMaterial.shader = maskedIconShader;
            //            }
            //        }                    
            //    }
            //}
            return "This fuel tank has configurable height, diameter, mount, and nosecone.";
        }

        /// <summary>
        /// Return the adjusted cost for the part based on current tank setup
        /// </summary>
        /// <param name="defaultCost"></param>
        /// <returns></returns>
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (currentTankCost < 0) { return 0; }
            return subtractCost ? -defaultCost + currentTankCost : currentTankCost;
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (currentTankMass < 0) { return 0; }
            return subtractMass ? -defaultMass + currentTankMass : currentTankMass;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        /// <summary>
        /// Overriden/defined in order to remove the on-editor-ship-modified event from the game-event callback queue
        /// </summary>
        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        /// <summary>
        /// Event callback for when vessel is modified in the editor.  Used to know when the gui-fields for this module have been updated.
        /// </summary>
        /// <param name="ship"></param>
        public void onEditorVesselModified(ShipConstruct ship)
        {
            // really we only care about parts being attached to the nodes on the mount/nose
            // but have to update available variants regardless of whatever caused the editor event callback
            updateAvailableVariants();
        }
        
        public string[] getSectionNames()
        {
            return new string[] { "Top", "Body", "Bottom" };
        }
        
        public Color[] getSectionColors(string section)
        {
            if (section == "Top")
            {
                return noseModule.customColors;
            }
            else if (section == "Body")
            {
                return tankModule.customColors;
            }
            else if (section == "Bottom")
            {
                return mountModule.customColors;
            }
            return tankModule.customColors;
        }

        public void setSectionColors(string section, Color[] colors)
        {
            if (section == "Top")
            {
                noseModule.setSectionColors(colors);
            }
            else if (section == "Body")
            {
                tankModule.setSectionColors(colors);
            }
            else if (section == "Bottom")
            {
                mountModule.setSectionColors(colors);
            }
        }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Initialization

        protected virtual void initialize()
        {
            if (initialized) { return; }
            initialized = true;            
            loadConfigData();
            updateModuleStats();
            updateModels();
            updatePreviousValueFields();
            updateTankStats();
            updateAttachNodes(false);
        }

        /// <summary>
        /// Restores ModelData instances from config node data, and populates the 'currentModule' instances with the currently enabled modules.
        /// </summary>
        private void loadConfigData()
        {
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            ConfigNode[] tankSetsNodes = node.GetNodes("TANKSET");
            ConfigNode[] tankNodes = node.GetNodes("TANK");
            ConfigNode[] mountNodes = node.GetNodes("CAP");

            tankSets = TankSet.parseSets(tankSetsNodes);
            //if no sets exist, initialize a default set to add all models to
            if (tankSets.Length == 0)
            {
                tankSets = new TankSet[1];
                ConfigNode defaultSetNode = new ConfigNode("TANKSET");
                defaultSetNode.AddValue("name", "default");
                tankSets[0] = new TankSet(defaultSetNode);
            }
            TankModelData[] mainTankModules = ModelData.parseModels<TankModelData>(tankNodes, m => new TankModelData(m));

            int len = mainTankModules.Length;
            TankSet set;
            for (int i = 0; i < len; i++)
            {
                set = Array.Find(tankSets, m => m.name == mainTankModules[i].setName);
                //if set is not found by name, add it to the first set which is guaranteed to exist due to the default-set-adding code above.
                if (set == null)
                {
                    set = tankSets[0];
                }
                set.addModel(mainTankModules[i]);
            }

            topNodeNames = SSTUUtils.parseCSV(topManagedNodeNames);
            bottomNodeNames = SSTUUtils.parseCSV(bottomManagedNodeNames);

            tankModule = new ModelModule<TankModelData, SSTUModularFuelTank>(part, this, getRootTransform(rootTransformName, true), ModelOrientation.CENTRAL, nameof(bodyModuleData), nameof(currentTankType), nameof(currentTankTexture));
            tankModule.getSymmetryModule = m => m.tankModule;
            tankModule.setupModelList(mainTankModules);

            currentTankSetModule = Array.Find(tankSets, m => m.name == tankModule.model.setName);
            currentTankSet = currentTankSetModule.name;
            lastSelectedVariant = tankModule.model.variantName;

            tankModule.getValidSelections = delegate (IEnumerable<TankModelData> data) { return System.Linq.Enumerable.Where(data, s => s.setName == currentTankSet); };
            tankModule.updateSelections();
            tankModule.setupModel();

            len = mountNodes.Length;
            ConfigNode mountNode;
            List<SingleModelData> noses = new List<SingleModelData>();
            List<SingleModelData> mounts = new List<SingleModelData>();
            for (int i = 0; i < len; i++)
            {
                mountNode = mountNodes[i];
                if (mountNode.GetBoolValue("useForNose", true))
                {
                    mountNode.SetValue("nose", "true");
                    noses.Add(new SingleModelData(mountNode));
                }
                if (mountNode.GetBoolValue("useForMount", true))
                {
                    mountNode.SetValue("nose", "false");
                    mounts.Add(new SingleModelData(mountNode));
                }
            }
            
            noseModule = new ModelModule<SingleModelData, SSTUModularFuelTank>(part, this, getRootTransform(rootNoseTransformName, true), ModelOrientation.TOP, nameof(noseModuleData), nameof(currentNoseType), nameof(currentNoseTexture));
            noseModule.getSymmetryModule = m => m.noseModule;
            noseModule.getValidSelections = delegate (IEnumerable<SingleModelData> data) { return System.Linq.Enumerable.Where(data, m => m.canSwitchTo(part, topNodeNames)); };
            noseModule.setupModelList(noses);
            noseModule.setupModel();

            mountModule = new ModelModule<SingleModelData, SSTUModularFuelTank>(part, this, getRootTransform(rootMountTransformName, true), ModelOrientation.BOTTOM, nameof(mountModuleData), nameof(currentMountType), nameof(currentMountTexture));
            mountModule.getSymmetryModule = m => m.mountModule;
            mountModule.getValidSelections = delegate (IEnumerable<SingleModelData> data) { return System.Linq.Enumerable.Where(data, m => m.canSwitchTo(part, bottomNodeNames)); };
            mountModule.setupModelList(mounts);
            mountModule.setupModel();
        }

        /// <summary>
        /// Restores the editor-only diameter and height-adjustment values;
        /// </summary>
        private void updatePreviousValueFields()
        {
            prevTankDiameter = currentTankDiameter;
            prevTankHeightScale = currentTankVerticalScale;
        }

        #endregion ENDREGION - Initialization

        #region REGION - Updating methods

        /// <summary>
        /// Updates the cached values for the modules positions and scales based on the current tank settings for scale/position.
        /// Done separately from updating the actual models so that the values can be used without the models even being present.
        /// </summary>
        private void updateModuleStats()
        {
            if (currentTankVerticalScale < tankModule.model.minVerticalScale)
            {
                currentTankVerticalScale = tankModule.model.minVerticalScale;
                this.updateUIFloatEditControl(nameof(currentTankVerticalScale), currentTankVerticalScale);
            }
            else if (currentTankVerticalScale > tankModule.model.maxVerticalScale)
            {
                currentTankVerticalScale = tankModule.model.maxVerticalScale;
                this.updateUIFloatEditControl(nameof(currentTankVerticalScale), currentTankVerticalScale);
            }
            float diameterScale = currentTankDiameter / tankModule.model.modelDefinition.diameter;
            tankModule.model.updateScale(diameterScale, currentTankVerticalScale * diameterScale);
            noseModule.model.updateScaleForDiameter(currentTankDiameter * topAdapterRatio);
            mountModule.model.updateScaleForDiameter(currentTankDiameter * bottomAdapterRatio);

            float totalHeight = tankModule.model.currentHeight + noseModule.model.currentHeight + mountModule.model.currentHeight;
            float nosePosition = totalHeight * 0.5f;//start at the top of the first tank
            nosePosition -= noseModule.model.currentHeight;
            noseModule.model.setPosition(nosePosition, ModelOrientation.TOP);

            float tankPosition = nosePosition - tankModule.model.currentHeight;
            tankModule.model.setPosition(tankPosition, tankModule.model.modelDefinition.orientation);

            //mount uses same position as tank, as it uses 'bottom' orientation setup for positioning
            mountModule.model.setPosition(tankPosition, ModelOrientation.BOTTOM);
        }

        /// <summary>
        /// Apply the scale and position changes to the actual models
        /// </summary>
        private void updateModels()
        {
            tankModule.model.updateModel();
            noseModule.model.updateModel();
            mountModule.model.updateModel();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        /// <summary>
        /// Updates the cached volume, mass, and cost fields
        /// </summary>
        private void updateTankStats()
        {
            currentTankVolume = noseModule.moduleVolume + tankModule.moduleVolume + mountModule.moduleVolume;
            currentTankMass = noseModule.moduleMass + tankModule.moduleMass + mountModule.moduleMass;
            currentTankCost = noseModule.moduleCost + tankModule.moduleCost + mountModule.moduleCost;
        }

        /// <summary>
        /// Group method for updating tank volume/mass/cost, model scales and positions, VolumeContainer volume, and attach nodes.
        /// </summary>
        /// <param name="userInput"></param>
        private void updateEditorStats(bool userInput)
        {
            updateModuleStats();
            updateModels();
            updateTankStats();
            updateContainerVolume();
            updateAttachNodes(userInput);
            updatePreviousValueFields();
        }

        /// <summary>
        /// Fires an event to update the VolumeContainer volume (or optionally RF/MFT if those modules are installed and in use)
        /// </summary>
        private void updateContainerVolume()
        {
            SSTUModInterop.onPartFuelVolumeUpdate(part, currentTankVolume * 1000f);
        }

        protected virtual void updateAttachNodes(bool userInput)
        {
            noseModule.model.updateAttachNodes(part, topNodeNames, userInput, ModelOrientation.TOP);
            mountModule.model.updateAttachNodes(part, bottomNodeNames, userInput, ModelOrientation.BOTTOM);
            TankModelData currentMainTankModule = tankModule.model;
            AttachNode surface = part.srfAttachNode;
            if (surface != null)
            {
                Vector3 pos = currentMainTankModule.modelDefinition.surfaceNode.position * currentMainTankModule.currentDiameterScale;
                Vector3 rot = currentMainTankModule.modelDefinition.surfaceNode.orientation;
                SSTUAttachNodeUtils.updateAttachNodePosition(part, surface, pos, rot, userInput);                
            }
            
            if (!String.IsNullOrEmpty(interstageNodeName))
            {
                float y = mountModule.model.currentVerticalPosition + (mountModule.model.modelDefinition.fairingTopOffset * mountModule.model.currentHeightScale);
                Vector3 pos = new Vector3(0, y, 0);
                SSTUSelectableNodes.updateNodePosition(part, interstageNodeName, pos);
                AttachNode interstage = part.FindAttachNode(interstageNodeName);
                if (interstage != null)
                {
                    Vector3 orientation = new Vector3(0, -1, 0);
                    SSTUAttachNodeUtils.updateAttachNodePosition(part, interstage, pos, orientation, userInput);
                }
            }
        }

        protected Transform getRootTransform(string name, bool recreate)
        {
            Transform root = part.transform.FindRecursive(name);
            if (recreate && root != null)
            {
                GameObject.DestroyImmediate(root.gameObject);
                root = null;
            }
            if (root == null)
            {
                root = new GameObject(name).transform;
            }
            root.NestToParent(part.transform.FindRecursive("model"));
            return root;
        }

        private void updateAnimationControl(string id, SingleModelData model, int layer)
        {
            if (string.IsNullOrEmpty(id)) { return; }
            SSTUAnimateControlled module = SSTUAnimateControlled.locateAnimationController(part, id);
            if (module != null)
            {
                module.initializeExternal(model.getAnimationData(model.model.transform, layer));
            }
        }

        #endregion ENDREGION - Updating methods
   
    }

    /// <summary>
    /// A set of tanks of the same length.  Used to group up tanks by length and variant type.
    /// </summary>
    public class TankSet
    {
        public readonly string name;
        private List<TankModelData> modelData = new List<TankModelData>();

        public TankSet(ConfigNode node)
        {
            name = node.GetStringValue("name");
        }

        public int Length { get { return modelData.Count; } }

        public void addModel(TankModelData data)
        {
            modelData.AddUnique(data);
        }

        public string getDefaultModel(string preferredVariant)
        {
            int len = modelData.Count;
            for (int i = 0; i < len; i++)
            {
                if (modelData[i].variantName == preferredVariant) { return modelData[i].name; }
            }
            return modelData[0].name;
        }

        public String[] getModelNames()
        {
            int len = modelData.Count;
            string[] names = new string[len];
            for (int i = 0; i < len; i++)
            {
                names[i] = modelData[i].name;
            }
            return names;
        }

        public string[] getTankDescriptions()
        {
            int len = modelData.Count;
            string[] names = new string[len];
            for (int i = 0; i < len; i++)
            {
                names[i] = String.IsNullOrEmpty(modelData[i].variantName) ? modelData[i].name : modelData[i].variantName;
            }
            return names;
        }

        public bool hasVariant(string name)
        {
            int len = modelData.Count;
            for (int i = 0; i < len; i++)
            {
                if (modelData[i].variantName == name) { return true; }
            }
            return false;
        }

        public static string[] getSetNames(TankSet[] sets, bool includeEmpty = false)
        {
            List<string> names = new List<string>();
            int len = sets.Length;
            for (int i = 0; i < len; i++)
            {
                if (sets[i].Length > 0 || includeEmpty) { names.Add(sets[i].name); }
            }
            return names.ToArray();
        }

        public static TankSet[] parseSets(ConfigNode[] nodes)
        {
            int len = nodes.Length;
            TankSet[] sets = new TankSet[len];
            for (int i = 0; i < len; i++)
            {
                sets[i] = new TankSet(nodes[i]);
            }
            return sets;
        }

        public override string ToString()
        {
            return "TankSet: " + name;
        }
    }

    /// <summary>
    /// Wrapper around SingleModelData to include a 'variant' (hydrolox/kerolox/etc) type and 'setName' (length -- 1.0x, 1.5x 2.0x, etc)
    /// </summary>
    public class TankModelData : SingleModelData
    {

        /// <summary>
        /// The 'set name' of the tank.  This is basically its length in a standardized format.
        /// </summary>
        public string setName;

        /// <summary>
        /// The 'variant name' of the tank.  E.G. Kerlox, Hydrolox, Cryo, Framed, whatever
        /// </summary>
        public string variantName;

        public TankModelData(ConfigNode node) : base(node)
        {
            setName = node.GetStringValue("setName", "default");
            variantName = node.GetStringValue("variantName", name);
        }

    }

}