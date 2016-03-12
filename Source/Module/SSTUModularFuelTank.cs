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
        public float tankDiameterIncrement = 0.625f;

        [KSPField]
        public float minTankDiameter = 0.625f;

        [KSPField]
        public float maxTankDiameter = 10f;

        [KSPField]
        public bool canChangeInFlight = false;

        [KSPField]
        public bool useRF = false;

        [KSPField]
        public String techLimitSet = "Default";

        [KSPField]
        public String topManagedNodeNames = "top, top2, top3, top4";

        [KSPField]
        public String bottomManagedNodeNames = "bottom, bottom2, bottom3, bottom4";

        [KSPField]
        public String interstageNodeName = "interstage";

        [KSPField(guiName = "Tank Diameter +/-", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0f, maxValue = 0.95f, stepIncrement = 0.05f)]
        public float editorTankDiameterAdjust;

        [KSPField(guiName = "Tank Height +/-", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = -1f, maxValue = 1f, stepIncrement = 0.05f)]
        public float editorTankHeightAdjust;

        [KSPField(guiActiveEditor = true, guiName = "Tank Diameter (m)")]
        public float guiTankDiameter = 0;

        [KSPField(guiActiveEditor = true, guiName = "Tank Height (m)")]
        public float guiTankHeight = 0;

        [KSPField(guiActiveEditor = true, guiName = "Tank Usable Vol. (m^3)")]
        public float guiTankVolume = 0;

        [KSPField(guiActiveEditor = true, guiName = "Tank Dry Mass")]
        public float guiDryMass = 0;

        [KSPField(guiActiveEditor = true, guiName = "Tank Cost")]
        public float guiTankCost = 0;

        // The 'currentXXX' fields are used in the config to define the default values for initialization purposes; else if they are empty/null, they are set to the first available of the specified type

        [KSPField(isPersistant = true)]
        public String currentFuelType = String.Empty;

        [KSPField(isPersistant = true)]
        public String currentTankType = String.Empty;

        [KSPField(isPersistant = true)]
        public String currentNoseType = String.Empty;

        [KSPField(isPersistant = true)]
        public String currentMountType = String.Empty;

        [KSPField(isPersistant = true)]
        public float currentTankDiameter = 2.5f;

        [KSPField(isPersistant = true)]
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

        /// <summary>
        /// Tracks whether -this- part module has been initialized in the editor or flight scenes; this check determines if certain update functions run, mostly related to inter-module interaction
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool initializedModule = false;

        #endregion

        #region REGION - private working variables
        
        private float editorTankWholeDiameter;
        private float editorPrevTankDiameterAdjust;

        private float editorPrevTankHeightAdjust;

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
            if (HighLogic.LoadedSceneIsEditor) { GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship); }            
        }

        [KSPEvent(guiName = "Tank Length -", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void prevTankEvent()
        {
            setMainTankModuleFromEditor(getNextTankLength(currentMainTankModule, true), true);
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        [KSPEvent(guiName = "Tank Length +", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextTankEvent()
        {
            setMainTankModuleFromEditor(getNextTankLength(currentMainTankModule, false), true);
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        [KSPEvent(guiName = "Tank Diameter --", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void prevTankDiameterEvent()
        {
            setTankDiameterFromEditor(currentTankDiameter - tankDiameterIncrement, true);
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }
        
        [KSPEvent(guiName = "Tank Diameter ++", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextTankDiameterEvent()
        {
            setTankDiameterFromEditor(currentTankDiameter + tankDiameterIncrement, true);
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        [KSPEvent(guiName = "Prev Nose Type", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void prevTopEvent()
        {
            setNoseModuleFromEditor(getNextCap(noseModules, currentNoseModule, topNodeNames, true), true);
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        [KSPEvent(guiName = "Next Nose Type", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextTopEvent()
        {
            setNoseModuleFromEditor(getNextCap(noseModules, currentNoseModule, topNodeNames, false), true);
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        [KSPEvent(guiName = "Prev Mount Type", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void prevBottomEvent()
        {
            setMountModuleFromEditor(getNextCap(mountModules, currentMountModule, bottomNodeNames, true), true);
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        [KSPEvent(guiName = "Next Mount Type", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextBottomEvent()
        {
            setMountModuleFromEditor(getNextCap(mountModules, currentMountModule, bottomNodeNames, false), true);
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
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
            }
        }

        private void setNoseModuleFromEditor(MountModelData newModule, bool updateSymmetry)
        {
            currentNoseModule.destroyCurrentModel();
            currentNoseModule = newModule;
            newModule.setupModel(part, part.transform.FindRecursive("model"), ModelOrientation.TOP);
            currentNoseType = newModule.name;
            if (!currentNoseModule.isValidTextureSet(currentNoseTexture)) { currentNoseTexture = currentNoseModule.modelDefinition.defaultTextureSet; }
            currentNoseModule.enableTextureSet(currentNoseTexture);

            updateEditorStats(true);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    SSTUModularFuelTank mft = p.GetComponent<SSTUModularFuelTank>();
                    MountModelData mt = Array.Find(mft.noseModules, t => t.name == newModule.name);
                    mft.setNoseModuleFromEditor(mt, false);
                }
            }
        }

        private void setMainTankModuleFromEditor(SingleModelData newModule, bool updateSymmetry)
        {
            currentMainTankModule.destroyCurrentModel();
            currentMainTankModule = newModule;
            currentMainTankModule.setupModel(part, part.transform.FindRecursive("model"), ModelOrientation.CENTRAL);
            currentTankType = newModule.name;
            if (!currentMainTankModule.isValidTextureSet(currentTankTexture)) { currentTankTexture = currentMainTankModule.modelDefinition.defaultTextureSet; }
            currentMainTankModule.enableTextureSet(currentTankTexture);

            updateEditorStats(true);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    SSTUModularFuelTank mft = p.GetComponent<SSTUModularFuelTank>();
                    SingleModelData mt = Array.Find(mft.mainTankModules, t => t.name == newModule.name);
                    mft.setMainTankModuleFromEditor(mt, false);
                }
            }
        }

        private void setMountModuleFromEditor(MountModelData newModule, bool updateSymmetry)
        {
            currentMountModule.destroyCurrentModel();
            currentMountModule = newModule;
            newModule.setupModel(part, part.transform.FindRecursive("model"), ModelOrientation.BOTTOM);
            currentMountType = newModule.name;
            if (!currentMountModule.isValidTextureSet(currentMountTexture)) { currentMountTexture = currentMountModule.modelDefinition.defaultTextureSet; }
            currentMountModule.enableTextureSet(currentMountTexture);

            updateEditorStats(true);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    SSTUModularFuelTank mft = p.GetComponent<SSTUModularFuelTank>();
                    MountModelData mt = Array.Find(mft.mountModules, t => t.name == newModule.name);
                    mft.setMountModuleFromEditor(mt, false);
                }
            }
        }

        private void setTankDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {
            if (newDiameter < minTankDiameter) { newDiameter = minTankDiameter; }
            if (newDiameter > maxTankDiameter) { newDiameter = maxTankDiameter; }
            if (SSTUUtils.isResearchGame() && newDiameter > techLimitMaxDiameter) { newDiameter = techLimitMaxDiameter; }
            float oldDiameter = currentTankDiameter;
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
        }

        private void setTankScaleFromEditor(float editorScaleValue, bool updateSymmetry)
        {
            float newScale = 1;
            float maxDelta = 0f;
            if (editorScaleValue < 0)
            {
                maxDelta = 1.0f - currentMainTankModule.minVerticalScale;
            }
            else
            {
                maxDelta = currentMainTankModule.maxVerticalScale - 1.0f;
            }            
            newScale = 1.0f + (maxDelta * editorScaleValue);
            currentTankVerticalScale = newScale;

            restoreEditorFields();
            updateEditorStats(true);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setTankScaleFromEditor(editorScaleValue, false);
                }
            }
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
            if (canChangeInFlight)
            {
                Events["nextFuelEvent"].guiActive = true;
                Events["jettisonContentsEvent"].active = Events["jettisonContentsEvent"].guiActive = true;
            }
            if (useRF)
            {
                Events["nextFuelEvent"].active = false;
                Fields["guiTankCost"].guiActiveEditor = false;
                Fields["guiDryMass"].guiActiveEditor = false;
                Fields["guiTankVolume"].guiActiveEditor = false;
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
            StartCoroutine(delayedDragUpdate());
        }

        private IEnumerator delayedDragUpdate()
        {
            yield return new WaitForFixedUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        public void Start()
        {
            if (!initializedModule && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                initializedModule = true;
                updateFairing();
            }
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
        public float GetModuleCost(float defaultCost)
        {
            return currentTankCost;
        }

        public float GetModuleMass(float defaultMass)
        {
            return -defaultMass + currentTankMass;
        }
        
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
            if (!HighLogic.LoadedSceneIsEditor) { return; }
            if (editorPrevTankDiameterAdjust != editorTankDiameterAdjust)
            {
                editorPrevTankDiameterAdjust = editorTankDiameterAdjust;
                float newDiameter = editorTankWholeDiameter + (editorTankDiameterAdjust * tankDiameterIncrement);
                setTankDiameterFromEditor(newDiameter, true);
            }
            else if (editorPrevTankHeightAdjust != editorTankHeightAdjust)
            {
                editorPrevTankHeightAdjust = editorTankHeightAdjust;                
                setTankScaleFromEditor(editorTankHeightAdjust, true);
            }
            else
            {
                updateAttachNodes(true);
            }
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
            updateTextureSet(true);
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
            float div = currentTankDiameter / tankDiameterIncrement;
            float whole = (int)div;
            float extra = div - whole;
            editorTankWholeDiameter = whole * tankDiameterIncrement;
            editorPrevTankDiameterAdjust = editorTankDiameterAdjust = extra;

            editorPrevTankHeightAdjust = editorTankHeightAdjust;
        }

        /// <summary>
        /// Loads or builds the models for the currently selected modules.
        /// </summary>
        private void restoreModels()
        {
            Transform modelBase = new GameObject(rootTransformName).transform;
            modelBase.NestToParent(part.transform.FindRecursive("model"));
            currentMainTankModule.setupModel(part, modelBase, ModelOrientation.CENTRAL);
            currentNoseModule.setupModel(part, modelBase, ModelOrientation.TOP);
            currentMountModule.setupModel(part, modelBase, ModelOrientation.BOTTOM);
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
            //float tankHeight = currentMainTankModule.modelDefinition.height * currentTankVerticalScale * diameterScale;
            //MonoBehaviour.print("new tank height: " + tankHeight);
            //currentMainTankModule.updateScaleForHeightAndDiameter(tankHeight, currentTankDiameter);

            //currentMainTankModule.updateScaleForDiameter(currentTankDiameter);
            currentNoseModule.updateScaleForDiameter(currentTankDiameter);
            currentMountModule.updateScaleForDiameter(currentTankDiameter);
            float totalHeight = currentMainTankModule.currentHeight + currentNoseModule.currentHeight + currentMountModule.currentHeight;
            float startY = totalHeight * 0.5f;
            startY -= currentNoseModule.currentHeight;
            currentNoseModule.currentVerticalPosition = startY + currentNoseModule.currentHeightScale * currentNoseModule.modelDefinition.verticalOffset;
            startY -= currentMainTankModule.currentHeight * 0.5f;
            currentMainTankModule.currentVerticalPosition = startY;
            startY -= currentMainTankModule.currentHeight * 0.5f;
            currentMountModule.currentVerticalPosition = startY + currentMountModule.currentHeightScale * currentMountModule.modelDefinition.verticalOffset;
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
            if (useRF)
            {
                SSTUModInterop.onPartFuelVolumeUpdate(part, currentTankVolume);
            }
            else
            {
                currentTankVolume = currentFuelTypeData.getUsableVolume(currentTankVolume);
                currentTankMass = currentFuelTypeData.getTankageMass(currentTankVolume);
                currentTankCost = currentFuelTypeData.getDryCost(currentTankVolume) + currentFuelTypeData.getResourceCost(currentTankVolume);
                part.mass = currentTankMass;
            }
            updateGuiState();
        }

        private void removeExistingModels()
        {
            Transform toDelete = part.transform.FindRecursive(rootTransformName);
            if (toDelete != null)
            {
                GameObject.DestroyImmediate(toDelete.gameObject);
            }
        }

        private bool canChangeFuelType()
        {
            if (useRF) { return false; }
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
            if (useRF)
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
        }

        private void updateTextureSet(bool updateSymmetry)
        {
            updateFlagTransform();

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

        private void updateFlagTransform()
        {
            SSTUFlagDecal[] decals = part.GetComponents<SSTUFlagDecal>();
            foreach (SSTUFlagDecal decal in decals) { decal.updateFlagTransform(); }
        }

        private void updateGuiState()
        {
            guiDryMass = currentTankMass;
            guiTankCost = currentTankCost;
            guiTankVolume = currentTankVolume;
            guiTankDiameter = currentTankDiameter;
            guiTankHeight = currentMainTankModule.currentHeight + currentNoseModule.currentHeight + currentMountModule.currentHeight;
            Fields["editorTankHeightAdjust"].guiActiveEditor = currentMainTankModule.minVerticalScale != 1 || currentMainTankModule.maxVerticalScale != 1;
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
            AttachNode interstage = part.findAttachNode(interstageNodeName);
            if (interstage != null)
            {
                float y = currentMountModule.currentVerticalPosition + (currentMountModule.modelDefinition.fairingTopOffset * currentMountModule.currentHeightScale);
                Vector3 pos = new Vector3(0, y, 0);
                Vector3 orientation = new Vector3(0, -1, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, interstage, pos, orientation, userInput);
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

