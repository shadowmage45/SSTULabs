using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{

    public class SSTUModularFuelTank : PartModule, IPartCostModifier, IPartMassModifier
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
        
        [Persistent]
        public string configNodeData = string.Empty;

        #endregion

        #region REGION - private working variables

        private float prevTankDiameter;
        private float prevTankHeightScale;
        private string prevNose;
        private string prevTank;
        private string prevMount;

        private float currentTankVolume;
        private float currentTankMass=-1;
        private float currentTankCost=-1;
        
        private TankSet[] tankSets;
        private TankSet currentTankSetModule;
        
        protected TankModelData[] mainTankModules;
        protected TankModelData currentMainTankModule;

        protected SingleModelData[] noseModules;
        protected SingleModelData currentNoseModule;

        protected SingleModelData[] mountModules;
        protected SingleModelData currentMountModule;
                
        private String[] topNodeNames;
        private String[] bottomNodeNames;

        private string[] noseVariants;
        private string[] mountVariants;

        //populated during init to the variant type of the currently selected model data
        private string lastSelectedVariant;

        protected bool initialized = false;

        #endregion

        #region REGION - GUI Events/Interaction methods

        [KSPEvent(guiName = "Select Nose", guiActive = false, guiActiveEditor = true)]
        public void selectNoseEvent()
        {
            ModuleSelectionGUI.openGUI(ModelData.getValidSelections(part, noseModules, topNodeNames), currentTankDiameter, setNoseModuleFromEditor);
        }

        [KSPEvent(guiName = "Select Mount", guiActive = false, guiActiveEditor = true)]
        public void selectMountEvent()
        {
            ModuleSelectionGUI.openGUI(ModelData.getValidSelections(part, mountModules, bottomNodeNames), currentTankDiameter, setMountModuleFromEditor);
        }

        public void tankDiameterUpdated(BaseField field, object obj)
        {
            if (prevTankDiameter != currentTankDiameter)
            {
                setTankDiameterFromEditor(currentTankDiameter, true);
            }
        }

        public void tankHeightScaleUpdated(BaseField field, object obj)
        {
            if (prevTankHeightScale != currentTankVerticalScale)
            {
                setTankScaleFromEditor(currentTankVerticalScale, true);
            }
        }

        public void noseTypeUpdated(BaseField field, object obj)
        {
            if (prevNose != currentNoseType || currentNoseModule.name!=currentNoseType)
            {
                setNoseModuleFromEditor(currentNoseType, true);
            }
        }

        public void tankSetUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentTankSet)
            {
                setTankSetFromEditor(currentTankSet, true);
            }
        }

        public void tankTypeUpdated(BaseField field, object obj)
        {
            if (prevTank != currentTankType || currentMainTankModule.name!=currentTankType)
            {
                setMainTankModuleFromEditor(currentTankType, true);
            }
        }

        public void mountTypeUpdated(BaseField field, object obj)
        {
            if (prevMount != currentMountType || currentMountModule.name!=currentMountType)
            {
                setMountModuleFromEditor(currentMountType, true);
            }
        }

        public void onNoseTextureUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentNoseTexture)
            {
                setNoseTextureFromEditor(currentNoseTexture, true);
            }
        }

        public void onTankTextureUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentTankTexture)
            {
                setTankTextureFromEditor(currentTankTexture, true);
            }
        }

        public void onMountTextureUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentMountTexture)
            {
                setMountTextureFromEditor(currentMountTexture, true);
            }
        }

        private void updateAvailableVariants()
        {
            List<String> availNoseTypes = new List<string>();
            List<String> availMountTypes = new List<string>();
            int len = noseModules.Length;
            for (int i = 0; i < len; i++)
            {
                if (noseModules[i].canSwitchTo(part, topNodeNames)) { availNoseTypes.Add(noseModules[i].name); }
            }
            len = mountModules.Length;
            for (int i = 0; i < len; i++)
            {
                if (mountModules[i].canSwitchTo(part, bottomNodeNames)) { availMountTypes.Add(mountModules[i].name); }
            }
            noseVariants = availNoseTypes.ToArray();
            mountVariants = availMountTypes.ToArray();
            this.updateUIChooseOptionControl("currentNoseType", noseVariants, noseVariants, true, currentNoseType);
            this.updateUIChooseOptionControl("currentMountType", mountVariants, mountVariants, true, currentMountType);

            bool useModelSelectionGUI = HighLogic.CurrentGame.Parameters.CustomParams<SSTUGameSettings>().useModelSelectGui;
            Fields["currentNoseType"].guiActiveEditor = !useModelSelectionGUI && noseVariants.Length>1;
            Fields["currentMountType"].guiActiveEditor = !useModelSelectionGUI && mountVariants.Length>1;
        }

        private void updateUIScaleControls()
        {
            float min = 0.5f;
            float max = 1.5f;
            min = currentMainTankModule.minVerticalScale;
            max = currentMainTankModule.maxVerticalScale;
            float diff = max - min;
            if (diff > 0)
            {
                this.updateUIFloatEditControl("currentTankVerticalScale", min, max, diff*0.5f, diff*0.25f, diff*0.05f, true, currentTankVerticalScale);
            }
            Fields["currentTankVerticalScale"].guiActiveEditor = currentMainTankModule.minVerticalScale != 1 || currentMainTankModule.maxVerticalScale != 1;
        }

        protected virtual void setNoseModuleFromEditor(String newNoseType, bool updateSymmetry)
        {
            currentNoseType = newNoseType;
            SingleModelData newModule = Array.Find(noseModules, m => m.name == newNoseType);
            currentNoseModule.destroyCurrentModel();
            currentNoseModule = newModule;
            newModule.setupModel(getNoseRootTransform(false), ModelOrientation.TOP);
            currentNoseType = newModule.name;
            if (!currentNoseModule.isValidTextureSet(currentNoseTexture)) { currentNoseTexture = currentNoseModule.getDefaultTextureSet(); }
            currentNoseModule.enableTextureSet(currentNoseTexture);
            currentNoseModule.updateTextureUIControl(this, "currentNoseTexture", currentNoseTexture);
            updateEditorStats(true);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setNoseModuleFromEditor(newNoseType, false);
                }
            }
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        protected virtual void setTankSetFromEditor(String newTankSet, bool updateSymmetry)
        {
            TankSet newSet = Array.Find(tankSets, m => m.name == newTankSet);
            currentTankSetModule = newSet;
            string variant = lastSelectedVariant;
            string newTankName = newSet.getDefaultModel(lastSelectedVariant);
            this.updateUIChooseOptionControl("currentTankType", newSet.getModelNames(), newSet.getTankDescriptions(), true, newTankName);
            setMainTankModuleFromEditor(newTankName, false);
            Fields["currentTankType"].guiActiveEditor = newSet.Length > 1;
            //re-seat this if it was changed in the 'setMainTankModuleFromEditor' method
            //will allow for user-initiated main-tank changes to still change the 'last variant' but will
            //persist the variant if the newly selected set did not contain the selected variant
            //so that it will persist to the next set selection, OR be reseated on the next user-tank selection within the current set
            if (!currentTankSetModule.hasVariant(variant)) { lastSelectedVariant = variant; }
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setTankSetFromEditor(newTankSet, false);
                }
            }
        }

        protected virtual void setMainTankModuleFromEditor(String newMainTank, bool updateSymmetry)
        {
            TankModelData newModule = Array.Find(mainTankModules, m => m.name == newMainTank);
            currentMainTankModule.destroyCurrentModel();
            currentMainTankModule = newModule;
            currentMainTankModule.setupModel(getTankRootTransform(false), ModelOrientation.CENTRAL);
            currentTankType = newModule.name;
            if (!currentMainTankModule.isValidTextureSet(currentTankTexture)) { currentTankTexture = currentMainTankModule.getDefaultTextureSet(); }
            currentMainTankModule.enableTextureSet(currentTankTexture);
            currentMainTankModule.updateTextureUIControl(this, "currentTankTexture", currentTankTexture);
            updateUIScaleControls();
            updateEditorStats(true);
            lastSelectedVariant = currentMainTankModule.variantName;
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setMainTankModuleFromEditor(newMainTank, false);
                }
            }
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);

        }

        protected virtual void setMountModuleFromEditor(String newMountType, bool updateSymmetry)
        {
            currentMountType = newMountType;
            SingleModelData newModule = Array.Find(mountModules, m => m.name == newMountType);
            currentMountModule.destroyCurrentModel();
            currentMountModule = newModule;
            newModule.setupModel(getMountRootTransform(false), ModelOrientation.BOTTOM);
            currentMountType = newModule.name;
            if (!currentMountModule.isValidTextureSet(currentMountTexture)) { currentMountTexture = currentMountModule.getDefaultTextureSet(); }
            currentMountModule.enableTextureSet(currentMountTexture);
            currentMountModule.updateTextureUIControl(this, "currentMountTexture", currentMountTexture);
            updateEditorStats(true);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setMountModuleFromEditor(newMountType, false);
                }
            }
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        private void setTankDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {
            float oldDiameter = prevTankDiameter;
            currentTankDiameter = newDiameter;
            restoreEditorFields();
            updateEditorStats(true);
            SSTUAttachNodeUtils.updateSurfaceAttachedChildren(part, oldDiameter, newDiameter);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setTankDiameterFromEditor(newDiameter, false);
                }
            }
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        private void setTankScaleFromEditor(float editorScaleValue, bool updateSymmetry)
        {      
            currentTankVerticalScale = editorScaleValue;

            restoreEditorFields();
            updateEditorStats(true);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setTankScaleFromEditor(editorScaleValue, false);
                }
            }
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }
        
        private void setNoseTextureFromEditor(String newSet, bool updateSymmetry)
        {
            currentNoseTexture = newSet;
            currentNoseModule.enableTextureSet(newSet);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setNoseTextureFromEditor(newSet, false);
                }
            }
        }
        
        private void setTankTextureFromEditor(String newSet, bool updateSymmetry)
        {
            currentTankTexture = newSet;
            currentMainTankModule.enableTextureSet(newSet);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setTankTextureFromEditor(newSet, false);
                }
            }
        }
        
        private void setMountTextureFromEditor(String newSet, bool updateSymmetry)
        {
            currentMountTexture = newSet;
            currentMountModule.enableTextureSet(newSet);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                   p.GetComponent<SSTUModularFuelTank>().setMountTextureFromEditor(newSet, false);
                }
            }
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
                Fields["currentTankDiameter"].guiActiveEditor = false;
            }
            else
            {
                this.updateUIFloatEditControl("currentTankDiameter", minTankDiameter, maxTankDiameter, tankDiameterIncrement * 2, tankDiameterIncrement, tankDiameterIncrement * 0.05f, true, currentTankDiameter);
            }
            updateAvailableVariants();
            updateUIScaleControls();

            currentNoseModule.updateTextureUIControl(this, "currentNoseTexture", currentNoseTexture);
            currentMainTankModule.updateTextureUIControl(this, "currentTankTexture", currentTankTexture);
            currentMountModule.updateTextureUIControl(this, "currentMountTexture", currentMountTexture);

            bool useModelSelectionGUI = HighLogic.CurrentGame.Parameters.CustomParams<SSTUGameSettings>().useModelSelectGui;

            Events["selectNoseEvent"].guiActiveEditor = useModelSelectionGUI;
            Events["selectMountEvent"].guiActiveEditor = useModelSelectionGUI;

            Fields["currentTankDiameter"].uiControlEditor.onFieldChanged = tankDiameterUpdated;
            Fields["currentTankVerticalScale"].uiControlEditor.onFieldChanged = tankHeightScaleUpdated;
            Fields["currentTankSet"].uiControlEditor.onFieldChanged = tankSetUpdated;
            Fields["currentTankType"].uiControlEditor.onFieldChanged = tankTypeUpdated;
            Fields["currentNoseType"].uiControlEditor.onFieldChanged = noseTypeUpdated;
            Fields["currentMountType"].uiControlEditor.onFieldChanged = mountTypeUpdated;

            Fields["currentNoseTexture"].uiControlEditor.onFieldChanged = onNoseTextureUpdated;
            Fields["currentTankTexture"].uiControlEditor.onFieldChanged = onTankTextureUpdated;
            Fields["currentMountTexture"].uiControlEditor.onFieldChanged = onMountTextureUpdated;

            Fields["currentTankSet"].guiActiveEditor = tankSets.Length > 1;
            Fields["currentTankType"].guiActiveEditor = currentTankSetModule.Length > 1;
            Fields["currentNoseType"].guiActiveEditor = !useModelSelectionGUI && noseModules.Length > 1;
            Fields["currentMountType"].guiActiveEditor = !useModelSelectionGUI && mountModules.Length > 1;

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
            updateGuiState();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }
        
        /// <summary>
        /// Cleans up the 'default' assignments from the prefab part (put here due to lack of multi-pass loading).  
        /// Also adds a quick blurb to the right-click menu regarding possible additional part functionality.
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
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

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Initialization

        protected virtual void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            
            loadConfigData();
            updateModuleStats();
            restoreModels();
            updateModels();
            updateTextureSet(false);
            restoreEditorFields();
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
            ConfigNode[] limitNodes = node.GetNodes("TECHLIMIT");

            tankSets = TankSet.parseSets(tankSetsNodes);
            //if no sets exist, initialize a default set to add all models to
            if (tankSets.Length == 0)
            {
                tankSets = new TankSet[1];
                ConfigNode defaultSetNode = new ConfigNode("TANKSET");
                defaultSetNode.AddValue("name", "default");
                tankSets[0] = new TankSet(defaultSetNode);
            }
            mainTankModules = ModelData.parseModels<TankModelData>(tankNodes, m => new TankModelData(m));

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
            mountModules = mounts.ToArray();
            noseModules = noses.ToArray();

            topNodeNames = SSTUUtils.parseCSV(topManagedNodeNames);
            bottomNodeNames = SSTUUtils.parseCSV(bottomManagedNodeNames);
            
            currentMainTankModule = Array.Find(mainTankModules, m => m.name == currentTankType);
            if (currentMainTankModule == null)
            {
                MonoBehaviour.print("ERROR: Could not locate tank type for: " + currentTankType + ". reverting to first available tank type.");
                currentMainTankModule = mainTankModules[0];
                currentTankType = currentMainTankModule.name;
            }

            currentTankSetModule = Array.Find(tankSets, m => m.name == currentMainTankModule.setName);
            currentTankSet = currentTankSetModule.name;
            lastSelectedVariant = currentMainTankModule.variantName;

            currentNoseModule = Array.Find(noseModules, m => m.name == currentNoseType);
            if (currentNoseModule == null)
            {
                MonoBehaviour.print("ERROR: Could not locate nose type for: " + currentNoseType + ". reverting to first available nose type.");
                currentNoseModule = noseModules[0];
                currentNoseType = currentNoseModule.name;
            }

            currentMountModule = Array.Find(mountModules, m => m.name == currentMountType);
            if (currentMountModule == null)
            {
                MonoBehaviour.print("ERROR: Could not locate mount type for: " + currentMountType + ". reverting to first available mount type.");
                currentMountModule = mountModules[0];
                currentMountType = currentMountModule.name;
            }
            if (!currentMainTankModule.isValidTextureSet(currentTankTexture))
            {
                currentTankTexture = currentMainTankModule.getDefaultTextureSet();
            }
            if (!currentNoseModule.isValidTextureSet(currentNoseTexture))
            {
                currentNoseTexture = currentNoseModule.getDefaultTextureSet();
            }
            if (!currentMountModule.isValidTextureSet(currentMountTexture))
            {
                currentMountTexture = currentMountModule.getDefaultTextureSet();
            }
        }

        /// <summary>
        /// Restores the editor-only diameter and height-adjustment values;
        /// </summary>
        private void restoreEditorFields()
        {
            prevTankDiameter = currentTankDiameter;
            prevTankHeightScale = currentTankVerticalScale;
            prevNose = currentNoseType;
            prevMount = currentMountType;
            prevTank = currentTankType;
        }

        /// <summary>
        /// Loads or builds the models for the currently selected modules.
        /// </summary>
        private void restoreModels()
        {
            currentMainTankModule.setupModel(getTankRootTransform(true), ModelOrientation.CENTRAL);
            currentNoseModule.setupModel(getNoseRootTransform(true), ModelOrientation.TOP);
            currentMountModule.setupModel(getMountRootTransform(true), ModelOrientation.BOTTOM);
        }

        #endregion ENDREGION - Initialization

        #region REGION - Updating methods

        /// <summary>
        /// Updates the internal cached values for the modules based on the current tank settings for scale/volume/position;
        /// done separately from updating the actual models so that the values can be used without the models even being present
        /// </summary>
        private void updateModuleStats()
        {
            if (currentTankVerticalScale < currentMainTankModule.minVerticalScale)
            {
                currentTankVerticalScale = currentMainTankModule.minVerticalScale;
                this.updateUIFloatEditControl("currentTankVerticalScale", currentTankVerticalScale);
            }
            else if (currentTankVerticalScale > currentMainTankModule.maxVerticalScale)
            {
                currentTankVerticalScale = currentMainTankModule.maxVerticalScale;
                this.updateUIFloatEditControl("currentTankVerticalScale", currentTankVerticalScale);
            }
            float diameterScale = currentTankDiameter / currentMainTankModule.modelDefinition.diameter;
            currentMainTankModule.updateScale(diameterScale, currentTankVerticalScale * diameterScale);
            currentNoseModule.updateScaleForDiameter(currentTankDiameter * topAdapterRatio);
            currentMountModule.updateScaleForDiameter(currentTankDiameter * bottomAdapterRatio);

            float totalHeight = currentMainTankModule.currentHeight + currentNoseModule.currentHeight + currentMountModule.currentHeight;
            float startY = totalHeight * 0.5f;//start at the top of the first tank
            startY -= currentNoseModule.currentHeight;
            currentNoseModule.setPosition(startY, ModelOrientation.TOP);

            startY -= currentMainTankModule.currentHeight * 0.5f;
            currentMainTankModule.currentVerticalPosition = startY;

            startY -= currentMainTankModule.currentHeight * 0.5f;
            currentMountModule.setPosition(startY, ModelOrientation.BOTTOM);
        }

        private void updateModels()
        {
            currentMainTankModule.updateModel();
            currentNoseModule.updateModel();
            currentMountModule.updateModel();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        private void updateTankStats()
        {
            currentTankVolume = currentMainTankModule.getModuleVolume() + currentNoseModule.getModuleVolume() + currentMountModule.getModuleVolume();            
            currentTankMass = currentNoseModule.getModuleMass() + currentMainTankModule.getModuleMass() + currentMountModule.getModuleMass(); ;
            currentTankCost = currentNoseModule.getModuleCost() + currentMainTankModule.getModuleCost() + currentMountModule.getModuleCost(); ;
            updateGuiState();
        }

        private void updateEditorStats(bool userInput)
        {
            updateModuleStats();
            updateModels();
            updateTankStats();
            updateContainerVolume();
            updateTextureSet(false);
            updateAttachNodes(userInput);
            updateGuiState();
        }

        private void updateTextureSet(bool updateSymmetry)
        {
            currentNoseModule.enableTextureSet(currentNoseTexture);
            currentMainTankModule.enableTextureSet(currentTankTexture);
            currentMountModule.enableTextureSet(currentMountTexture);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().updateTextureSet(false);
                }
            }
        }

        private void updateContainerVolume()
        {
            SSTUModInterop.onPartFuelVolumeUpdate(part, currentTankVolume * 1000f);
        }

        private void updateGuiState()
        {
            Fields["currentNoseTexture"].guiActiveEditor = currentNoseModule.modelDefinition.textureSets.Length > 1;
            Fields["currentTankTexture"].guiActiveEditor = currentMainTankModule.modelDefinition.textureSets.Length > 1;
            Fields["currentMountTexture"].guiActiveEditor = currentMountModule.modelDefinition.textureSets.Length > 1;
        }

        protected virtual void updateAttachNodes(bool userInput)
        {
            currentNoseModule.updateAttachNodes(part, topNodeNames, userInput, ModelOrientation.TOP);
            currentMountModule.updateAttachNodes(part, bottomNodeNames, userInput, ModelOrientation.BOTTOM);
            AttachNode surface = part.srfAttachNode;
            if (surface != null)
            {
                Vector3 pos = currentMainTankModule.modelDefinition.surfaceNode.position * currentMainTankModule.currentDiameterScale;
                Vector3 rot = currentMainTankModule.modelDefinition.surfaceNode.orientation;
                SSTUAttachNodeUtils.updateAttachNodePosition(part, surface, pos, rot, userInput);                
            }            
            
            if (!String.IsNullOrEmpty(interstageNodeName))
            {
                float y = currentMountModule.currentVerticalPosition + (currentMountModule.modelDefinition.fairingTopOffset * currentMountModule.currentHeightScale);
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

        protected Transform getTankRootTransform(bool recreate)
        {
            Transform root = part.transform.FindRecursive(rootTransformName);
            if (recreate && root!=null)
            {
                GameObject.DestroyImmediate(root.gameObject);
                root = null;
            }
            if (root == null)
            {
                root = new GameObject(rootTransformName).transform;
            }
            root.NestToParent(part.transform.FindRecursive("model"));
            return root;
        }

        protected Transform getNoseRootTransform(bool recreate)
        {
            Transform root = part.transform.FindRecursive(rootNoseTransformName);
            if (recreate && root != null)
            {
                GameObject.DestroyImmediate(root.gameObject);
                root = null;
            }
            if (root == null)
            {
                root = new GameObject(rootNoseTransformName).transform;
            }
            root.NestToParent(part.transform.FindRecursive("model"));
            return root;
        }

        protected Transform getMountRootTransform(bool recreate)
        {
            Transform root = part.transform.FindRecursive(rootMountTransformName);
            if (recreate && root != null)
            {
                GameObject.DestroyImmediate(root.gameObject);
                root = null;
            }
            if (root == null)
            {
                root = new GameObject(rootMountTransformName).transform;
            }
            root.NestToParent(part.transform.FindRecursive("model"));
            return root;
        }

        #endregion ENDREGION - Updating methods
   
    }

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

    public class TankModelData : SingleModelData
    {
        public string setName;
        public string variantName;
        public TankModelData(ConfigNode node) : base(node)
        {
            setName = node.GetStringValue("setName", "default");
            variantName = node.GetStringValue("variantName", name);
        }
    }

}

