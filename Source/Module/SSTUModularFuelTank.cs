using System;
using System.Collections.Generic;
using System.Collections;
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
        public bool canChangeInFlight = false;
        
        [KSPField]
        public String techLimitSet = "Default";

        [KSPField]
        public String topManagedNodeNames = "top, top2, top3, top4";

        [KSPField]
        public String bottomManagedNodeNames = "bottom, bottom2, bottom3, bottom4";

        [KSPField]
        public String interstageNodeName = "interstage";

        [KSPField(guiActiveEditor = true, guiName = "Tank Usable Vol. (m^3)")]
        public float guiTankVolume = 0;

        [KSPField(guiActiveEditor = true, guiName = "Tank Dry Mass")]
        public float guiDryMass = 0;

        [KSPField(guiActiveEditor = true, guiName = "Tank Cost")]
        public float guiTankCost = 0;

        // The 'currentXXX' fields are used in the config to define the default values for initialization purposes; else if they are empty/null, they are set to the first available of the specified type
        [KSPField(isPersistant = true)]
        public String currentFuelType = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Tank"),
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

        [KSPField(isPersistant = true)]
        public String currentTankTexture = String.Empty;

        [KSPField(isPersistant = true)]
        public String currentNoseTexture = String.Empty;

        [KSPField(isPersistant = true)]
        public String currentMountTexture = String.Empty;

        /// <summary>
        /// Used solely to track if resources have been initialized, as this should only happen once on first part creation (regardless of if it is created in flight or in the editor);
        /// Unsure of any cleaner way to track a simple boolean value across the lifetime of a part, seems like the part-persistence data is probably it...
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        [KSPField(isPersistant = true)]
        public bool initializedFairing = false;

        #endregion

        #region REGION - private working variables

        private float prevTankDiameter;
        private float prevTankHeightScale;
        private string prevNose;
        private string prevTank;
        private string prevMount;

        private float currentTankVolume;
        private float currentTankMass;
        private float currentTankCost;
        
        private float techLimitMaxDiameter;
        
        private SingleModelData[] mainTankModules;
        private SingleModelData currentMainTankModule;

        private MountModelData[] noseModules;
        private MountModelData currentNoseModule;

        private MountModelData[] mountModules;
        private MountModelData currentMountModule;

        private FuelTypeData[] fuelTypes;
        private FuelTypeData currentFuelTypeData;
                
        private String[] topNodeNames;
        private String[] bottomNodeNames;

        private string[] noseVariants;
        private string[] mountVariants;

        private bool initialized = false;

        #endregion

        #region REGION - GUI Events/Interaction methods

        [KSPEvent(guiName = "Jettison Contents", guiActive = false, guiActiveEditor = false, guiActiveUnfocused = false)]
        public void jettisonContentsEvent()
        {
            emptyTankContents();
        }

        [KSPEvent(guiName = "Next Fuel Type", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextFuelEvent()
        {
            setFuelTypeFromEditor(SSTUUtils.findNext(fuelTypes, m => m == currentFuelTypeData, false), true);    
        }

        [KSPEvent(guiName = "Next Tank Texture", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextTankTextureEvent()
        {
            setTankTextureFromEditor(currentMainTankModule.getNextTextureSetName(currentTankTexture, false), true);
        }

        [KSPEvent(guiName = "Next Nose Texture", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextNoseTextureEvent()
        {
            setNoseTextureFromEditor(currentNoseModule.getNextTextureSetName(currentNoseTexture, false), true);
        }

        [KSPEvent(guiName = "Next Mount Texture", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextMountTextureEvent()
        {
            setMountTextureFromEditor(currentMountModule.getNextTextureSetName(currentMountTexture, false), true);
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
            Fields["currentNoseType"].guiActiveEditor = noseVariants.Length>1;
            Fields["currentMountType"].guiActiveEditor = mountVariants.Length>1;
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

        private void setFuelTypeFromEditor(FuelTypeData newFuelType, bool updateSymmetry)
        {
            if (!canChangeFuelType()) { return; }
            currentFuelTypeData = newFuelType;
            currentFuelType = currentFuelTypeData.name;
            updateTankStats();
            updatePartResources();
            if (updateSymmetry)
            {
                SSTUModularFuelTank tank = null;
                foreach (Part p in part.symmetryCounterparts)
                {
                    tank = p.GetComponent<SSTUModularFuelTank>();
                    if (tank == null) { continue; }
                    tank.setFuelTypeFromEditor(Array.Find(tank.fuelTypes, m => m.name == currentFuelType), false);
                }
                if (HighLogic.LoadedSceneIsEditor) { GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship); }
            }
            SSTUStockInterop.fireEditorUpdate();
        }

        private void setNoseModuleFromEditor(String newNoseType, bool updateSymmetry)
        {
            MountModelData newModule = Array.Find(noseModules, m => m.name == newNoseType);
            currentNoseModule.destroyCurrentModel();
            currentNoseModule = newModule;
            newModule.setupModel(part, getNoseRootTransform(false), ModelOrientation.TOP);
            currentNoseType = newModule.name;
            if (!currentNoseModule.isValidTextureSet(currentNoseTexture)) { currentNoseTexture = currentNoseModule.modelDefinition.defaultTextureSet; }
            currentNoseModule.enableTextureSet(currentNoseTexture);
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

        private void setMainTankModuleFromEditor(String newMainTank, bool updateSymmetry)
        {
            SingleModelData newModule = Array.Find(mainTankModules, m => m.name == newMainTank);
            currentMainTankModule.destroyCurrentModel();
            currentMainTankModule = newModule;
            currentMainTankModule.setupModel(part, getTankRootTransform(false), ModelOrientation.CENTRAL);
            currentTankType = newModule.name;
            if (!currentMainTankModule.isValidTextureSet(currentTankTexture)) { currentTankTexture = currentMainTankModule.modelDefinition.defaultTextureSet; }
            currentMainTankModule.enableTextureSet(currentTankTexture);

            updateUIScaleControls();
            updateEditorStats(true);

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

        private void setMountModuleFromEditor(String newMountType, bool updateSymmetry)
        {
            MountModelData newModule = Array.Find(mountModules, m => m.name == newMountType);
            currentMountModule.destroyCurrentModel();
            currentMountModule = newModule;
            newModule.setupModel(part, getMountRootTransform(false), ModelOrientation.BOTTOM);
            currentMountType = newModule.name;
            if (!currentMountModule.isValidTextureSet(currentMountTexture)) { currentMountTexture = currentMountModule.modelDefinition.defaultTextureSet; }
            currentMountModule.enableTextureSet(currentMountTexture);

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
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
            float max = techLimitMaxDiameter < maxTankDiameter ? techLimitMaxDiameter : maxTankDiameter;
            string[] names = SSTUUtils.getNames(mainTankModules, m => m.name);
            this.updateUIChooseOptionControl("currentTankType", names, names, true, currentTankType);            
            this.updateUIFloatEditControl("currentTankDiameter", minTankDiameter, max, tankDiameterIncrement*2, tankDiameterIncrement, tankDiameterIncrement*0.05f, true, currentTankDiameter);            
            updateAvailableVariants();
            updateUIScaleControls();
            Fields["currentTankDiameter"].uiControlEditor.onFieldChanged = tankDiameterUpdated;
            Fields["currentTankVerticalScale"].uiControlEditor.onFieldChanged = tankHeightScaleUpdated;
            Fields["currentTankType"].uiControlEditor.onFieldChanged = tankTypeUpdated;
            Fields["currentNoseType"].uiControlEditor.onFieldChanged = noseTypeUpdated;
            Fields["currentMountType"].uiControlEditor.onFieldChanged = mountTypeUpdated;
            if (canChangeInFlight)
            {
                Events["nextFuelEvent"].guiActive = true;
                Events["jettisonContentsEvent"].active = Events["jettisonContentsEvent"].guiActive = true;
            }
            if (SSTUModInterop.isRFInstalled())
            {
                Events["nextFuelEvent"].active = false;
                Fields["guiTankCost"].guiActiveEditor = false;
                Fields["guiDryMass"].guiActiveEditor = false;
                Fields["guiTankVolume"].guiActiveEditor = false;
            }
            if (fuelTypes.Length <= 1)
            {
                Events["nextFuelEvent"].guiActiveEditor = false;
            }
            if (mainTankModules.Length <= 1)
            {
                Fields["currentTankType"].guiActiveEditor = false;
            }
            if (noseModules.Length <= 1)
            {
                Fields["currentNoseType"].guiActiveEditor = false;
            }
            if (mountModules.Length <= 1)
            {
                Fields["currentMountType"].guiActiveEditor = false;
            }
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                if (!initializedFairing)
                {
                    initializedFairing = true;
                    updateFairing();
                }
            }
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
            return -defaultCost + currentTankCost;
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            return -defaultMass + currentTankMass;
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
            updateAvailableVariants();
        }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Initialization

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;

            removeExistingModels();
            loadConfigData();
            TechLimit.updateTechLimits(techLimitSet, out techLimitMaxDiameter);
            if (currentTankDiameter > techLimitMaxDiameter)
            {
                currentTankDiameter = techLimitMaxDiameter;
            }
            updateModuleStats();
            restoreModels();
            updateModels();
            updateTextureSet(false);
            restoreEditorFields();
            updateTankStats();
            updateAttachNodes(false);
                        
            if (!initializedResources && HighLogic.LoadedSceneIsEditor)
            {
                initializedResources = true;
                updatePartResources();
            }
        }

        /// <summary>
        /// Restores ModelData instances from config node data, and populates the 'currentModule' instances with the currently enabled modules.
        /// </summary>
        private void loadConfigData()
        {
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);
            ConfigNode[] tankNodes = node.GetNodes("TANK");
            ConfigNode[] mountNodes = node.GetNodes("CAP");
            ConfigNode[] fuelNodes = node.GetNodes("FUELTYPE");
            ConfigNode[] limitNodes = node.GetNodes("TECHLIMIT");
                        
            mainTankModules = SingleModelData.parseModels(tankNodes);

            int len = mountNodes.Length;
            ConfigNode mountNode;
            List<MountModelData> noses = new List<MountModelData>();
            List<MountModelData> mounts = new List<MountModelData>();
            for (int i = 0; i < len; i++)
            {
                mountNode = mountNodes[i];
                if (mountNode.GetBoolValue("useForNose", true))
                {
                    mountNode.SetValue("nose", "true");
                    noses.Add(new MountModelData(mountNode));
                }
                if (mountNode.GetBoolValue("useForMount", true))
                {
                    mountNode.SetValue("nose", "false");
                    mounts.Add(new MountModelData(mountNode));
                }
            }
            mountModules = mounts.ToArray();
            noseModules = noses.ToArray();
            
            fuelTypes = FuelTypeData.parseFuelTypeData(fuelNodes);
                        
            topNodeNames = SSTUUtils.parseCSV(topManagedNodeNames);
            bottomNodeNames = SSTUUtils.parseCSV(bottomManagedNodeNames);
            
            currentMainTankModule = Array.Find(mainTankModules, m => m.name == currentTankType);
            if (currentMainTankModule == null)
            {
                MonoBehaviour.print("ERROR: Could not locate tank type for: " + currentTankType + ". reverting to first available tank type.");
                currentMainTankModule = mainTankModules[0];
                currentTankType = currentMainTankModule.name;
            }

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

            currentFuelTypeData = Array.Find(fuelTypes, m => m.name == currentFuelType);
            if (currentFuelTypeData == null)
            {
                MonoBehaviour.print("ERROR: Could not locate fuel type for: " + currentFuelType + ". reverting to first available fuel type.");
                FuelTypeData d = fuelTypes[0];
                currentFuelType = d.name;
                currentFuelTypeData = d;
                initializedResources = false;
            }
            if (!currentMainTankModule.isValidTextureSet(currentTankTexture))
            {
                currentTankTexture = currentMainTankModule.modelDefinition.defaultTextureSet;
            }
            if (!currentNoseModule.isValidTextureSet(currentNoseTexture))
            {
                currentNoseTexture = currentNoseModule.modelDefinition.defaultTextureSet;
            }
            if (!currentMountModule.isValidTextureSet(currentMountTexture))
            {
                currentMountTexture = currentMountModule.modelDefinition.defaultTextureSet;
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
            currentMainTankModule.setupModel(part, getTankRootTransform(true), ModelOrientation.CENTRAL);
            currentNoseModule.setupModel(part, getNoseRootTransform(true), ModelOrientation.TOP);
            currentMountModule.setupModel(part, getMountRootTransform(true), ModelOrientation.BOTTOM);
        }

        #endregion ENDREGION - Initialization

        #region REGION - Updating methods

        /// <summary>
        /// Updates the internal cached values for the modules based on the current tank settings for scale/volume/position;
        /// done separately from updating the actual models so that the values can be used without the models even being present
        /// </summary>
        private void updateModuleStats()
        {
            float diameterScale = currentTankDiameter / currentMainTankModule.modelDefinition.diameter;
            currentMainTankModule.updateScale(diameterScale, currentTankVerticalScale*diameterScale);
            currentNoseModule.updateScaleForDiameter(currentTankDiameter);
            currentMountModule.updateScaleForDiameter(currentTankDiameter);

            float totalHeight = currentMainTankModule.currentHeight + currentNoseModule.currentHeight + currentMountModule.currentHeight;
            float startY = totalHeight * 0.5f;//start at the top of the first tank
            startY -= currentNoseModule.currentHeight;

            float offset = currentNoseModule.currentHeightScale * currentNoseModule.modelDefinition.verticalOffset;
            if (currentNoseModule.modelDefinition.invertForTop) { offset = currentNoseModule.currentHeight-offset; }      
            currentNoseModule.currentVerticalPosition = startY + offset;

            startY -= currentMainTankModule.currentHeight * 0.5f;
            currentMainTankModule.currentVerticalPosition = startY;

            startY -= currentMainTankModule.currentHeight * 0.5f;
            offset = currentMountModule.currentHeightScale * currentMountModule.modelDefinition.verticalOffset;
            if (currentMountModule.modelDefinition.invertForBottom) { offset = currentMountModule.currentHeight-offset; }
            currentMountModule.currentVerticalPosition = startY - offset;
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
            if (SSTUModInterop.isRFInstalled())
            {
                SSTUModInterop.onPartFuelVolumeUpdate(part, currentTankVolume);
            }
            else
            {
                currentTankVolume = currentFuelTypeData.getUsableVolume(currentTankVolume);
                currentTankMass = currentFuelTypeData.getTankageMass(currentTankVolume);
                currentTankCost = currentFuelTypeData.getDryCost(currentTankVolume) + currentFuelTypeData.getResourceCost(currentTankVolume);
            }
            updateGuiState();
        }

        private void removeExistingModels()
        {
            //Transform toDelete = part.transform.FindRecursive(rootTransformName);
            //if (toDelete != null)
            //{
            //    GameObject.DestroyImmediate(toDelete.gameObject);
            //}
            //toDelete = part.transform.FindRecursive(rootNoseTransformName);
            //if (toDelete != null)
            //{
            //    GameObject.DestroyImmediate(toDelete.gameObject);
            //}
            //toDelete = part.transform.FindRecursive(rootMountTransformName);
            //if (toDelete != null)
            //{
            //    GameObject.DestroyImmediate(toDelete.gameObject);
            //}
        }

        private bool canChangeFuelType()
        {
            if (SSTUModInterop.isRFInstalled()) { return false; }
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (!canChangeInFlight) { return false; }
                foreach (PartResource res in part.Resources.list)
                {
                    if (res.amount > 0) { return false; }
                }
                return true;
            }
            return true;
        }

        private void emptyTankContents()
        {
            //TODO add in delayed timer, enforce button must be pressed twice in twenty seconds in order to trigger
            foreach (PartResource res in part.Resources.list)
            {
                res.amount = 0;
            }
        }

        private void updatePartResources()
        {
            if (SSTUModInterop.isRFInstalled())
            {
                return;
            }
            SSTUResourceList resourceList = currentFuelTypeData.getResourceList(guiTankVolume);
            resourceList.setResourcesToPart(part, !HighLogic.LoadedSceneIsFlight);
        }

        private void updateEditorStats(bool userInput)
        {
            updateModuleStats();
            updateModels();
            updateTankStats();
            updatePartResources();
            updateTextureSet(false);
            updateAttachNodes(userInput);
            updateFairing();
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

        private void updateGuiState()
        {
            guiDryMass = currentTankMass;
            guiTankCost = currentTankCost;
            guiTankVolume = currentTankVolume;
            Events["nextTankTextureEvent"].guiActiveEditor = currentMainTankModule.modelDefinition.textureSets.Length>1;
            Events["nextNoseTextureEvent"].guiActiveEditor = currentNoseModule.modelDefinition.textureSets.Length > 1;
            Events["nextMountTextureEvent"].guiActiveEditor = currentMountModule.modelDefinition.textureSets.Length > 1;
        }

        private void updateAttachNodes(bool userInput)
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
                AttachNode interstage = part.findAttachNode(interstageNodeName);
                if (interstage != null)
                {
                    Vector3 orientation = new Vector3(0, -1, 0);
                    SSTUAttachNodeUtils.updateAttachNodePosition(part, interstage, pos, orientation, userInput);
                }
            }
        }

        private void updateFairing()
        {
            SSTUNodeFairing fairing = part.GetComponent<SSTUNodeFairing>();
            if (fairing==null) { return; }
            float pos = currentMountModule.currentVerticalPosition + (currentMountModule.currentHeightScale * currentMountModule.modelDefinition.fairingTopOffset);
            FairingUpdateData data = new FairingUpdateData();
            data.setTopY(pos);
            data.setTopRadius(currentTankDiameter * 0.5f);
            if (currentMountModule.modelDefinition.fairingDisabled) { data.setEnable(false); }
            fairing.updateExternal(data);
        }

        private Transform getTankRootTransform(bool recreate)
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

        private Transform getNoseRootTransform(bool recreate)
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

        private Transform getMountRootTransform(bool recreate)
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

        private SingleModelData getNextTankLength(SingleModelData currentModule, bool iterateBackwards)
        {
            return SSTUUtils.findNext(mainTankModules, m => m == currentModule, iterateBackwards);
        }

        private MountModelData getNextCap(MountModelData[] mounts, MountModelData currentMount, String[] nodeNames, bool iterateBackwards)
        {
            return SSTUUtils.findNextEligible<MountModelData>(mounts, m => m == currentMount, l => l.canSwitchTo(part, nodeNames), iterateBackwards);            
        }      
    }    
}

