using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{

    public class SSTUModularFuelTank : PartModule, IPartCostModifier, IPartMassModifier
    {

        #region Config Fields   
        [KSPField]
        public String defaultTankType = String.Empty;

        [KSPField]
        public String defaultNoseName = String.Empty;

        [KSPField]
        public String defaultMountName = String.Empty;

        [KSPField]
        public String defaultFuelType = String.Empty;

        [KSPField]
        public String defaultNoseTexture = String.Empty;

        [KSPField]
        public String defaultTankTexture = String.Empty;

        [KSPField]
        public String defaultMountTexture = String.Empty;

        [KSPField]
        public float defaultTankDiameter = 2.5f;
        
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
        public String topManagedNodeNames = "top, top2, top3, top4";

        [KSPField]
        public String bottomManagedNodeNames = "bottom, bottom2, bottom3, bottom4";

        [KSPField]
        public String interstageNodeName = "interstage";

        [KSPField(guiName = "Tank Diameter +/-", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0f, maxValue = 0.95f, stepIncrement = 0.05f)]
        public float editorTankDiameterAdjust;

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

        [KSPField(isPersistant = true)]
        public String currentFuelType = String.Empty;

        [KSPField(isPersistant = true)]
        public String currentTankType = String.Empty;

        [KSPField(isPersistant = true)]
        public String currentNoseType = String.Empty;

        [KSPField(isPersistant = true)]
        public String currentMountType = String.Empty;

        [KSPField(isPersistant = true)]
        public float currentTankDiameter = 0;

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

        #region working variable fields
        [Persistent]
        public String configNodeData = String.Empty;

        private float editorTankWholeDiameter;
        private float editorPrevTankDiameterAdjust;

        private float currentTankVolume;
        private float currentTankMass;
        private float currentTankCost;

        private float techLimitMaxHeight;
        private float techLimitMaxDiameter;
        
        private SingleModelData[] mainTankModules;
        private SingleModelData currentMainTankModule;
        private CustomFuelTankMount[] noseModules;
        private CustomFuelTankMount currentNoseModule;
        private CustomFuelTankMount[] mountModules;
        private CustomFuelTankMount currentMountModule;
        private FuelTypeData[] fuelTypes;
        private FuelTypeData currentFuelTypeData;

        private TechLimitDiameterHeight[] techLimits;

        private TextureSet[] textureSets;

        private String[] topNodeNames;
        private String[] bottomNodeNames;

        private bool initialized = false;

        #endregion

        #region GUI Events

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
            setTankTextureFromEditor(SSTUUtils.findNext(textureSets, m => m.setName == currentTankTexture, false), true);
        }

        [KSPEvent(guiName = "Next Nose Texture", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextNoseTextureEvent()
        {
            setNoseTextureFromEditor(SSTUUtils.findNext(textureSets, m => m.setName == currentTankTexture, false), true);
        }

        [KSPEvent(guiName = "Next Mount Texture", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextMountTextureEvent()
        {
            setMountTextureFromEditor(SSTUUtils.findNext(textureSets, m => m.setName == currentTankTexture, false), true);
        }

        #endregion

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasNode("TANK"))//only prefab instance config node should contain this data...but whatever, grab it whenever it is present
            {
                configNodeData = node.ToString();
            }
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
            if (textureSets.Length <= 1)
            {
                Events["nextTankTextureEvent"].active = false;
                Events["nextNoseTextureEvent"].active = false;
                Events["nextMountTextureEvent"].active = false;
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        public void Start()
        {
            updateTechLimits();
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
            currentTankType = String.Empty;
            currentFuelType = String.Empty;
            currentMountType = String.Empty;
            currentNoseType = String.Empty;
            currentTankDiameter = 0;
            removeExistingModels();
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
            else
            {
                updateAttachNodes(true);
            }
        }

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;

            if (String.IsNullOrEmpty(currentTankType))
            {
                currentTankType = defaultTankType;
                currentFuelType = defaultFuelType;
                currentMountType = defaultMountName;
                currentNoseType = defaultNoseName;
                currentTankDiameter = defaultTankDiameter;
                currentTankTexture = defaultTankTexture;
                currentNoseTexture = defaultNoseTexture;
                currentMountTexture = defaultMountTexture;
            }

            removeExistingModels();
            loadConfigData();
            updateTechLimits();

            if (currentTankDiameter > techLimitMaxDiameter)
            {
                currentTankDiameter = techLimitMaxDiameter;
            }
            SingleModelData data = Array.Find(mainTankModules, m => m.name == currentTankType);
            if (data.height > techLimitMaxHeight)
            {
                data = getNextTankLength(data, true);
                currentTankType = data.name;
            }

            loadTankModules();
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
        /// Restores the editor-only diameter and height-adjustment values;
        /// </summary>
        private void restoreEditorFields()
        {
            float div = currentTankDiameter / tankDiameterIncrement;
            float whole = (int)div;
            float extra = div - whole;
            editorTankWholeDiameter = whole * tankDiameterIncrement;
            editorPrevTankDiameterAdjust = editorTankDiameterAdjust = extra;
        }
                
        /// <summary>
        /// If tank is uninitialized (no current tank type), will load the default values for tank type/diameter/nose/mount/fuel type into the 'current' slots.
        /// Will populate the 'currentModuleX' slot with the module for the loaded current-name
        /// </summary>
        private void loadTankModules()
        {
            currentMainTankModule = Array.Find(mainTankModules, m => m.name == currentTankType);
            currentNoseModule = Array.Find(noseModules, m => m.name == currentNoseType);
            currentMountModule = Array.Find(mountModules, m => m.name == currentMountType);
            currentFuelTypeData = Array.Find(fuelTypes, m => m.name == currentFuelType);
        }
        
        /// <summary>
        /// Restores SSTUCustomFuelTankPart instances from config node data, and populates the 'currentModule' instances with the currently selected module.
        /// </summary>
        private void loadConfigData()
        {
            ConfigNode node = SSTUNodeUtils.parseConfigNode(configNodeData);
            ConfigNode[] tankNodes = node.GetNodes("TANK");
            ConfigNode[] mountNodes = node.GetNodes("CAP");
            ConfigNode[] fuelNodes = node.GetNodes("FUELTYPE");
            ConfigNode[] limitNodes = node.GetNodes("TECHLIMIT");
            ConfigNode[] textureNodes = node.GetNodes("TEXTURESET");

            int len = tankNodes.Length;
            mainTankModules = new SingleModelData[len];
            for (int i = 0; i < len; i++) { mainTankModules[i] = new SingleModelData(tankNodes[i]); }
            
            len = mountNodes.Length;
            ConfigNode mountNode;
            List<CustomFuelTankMount> noses = new List<CustomFuelTankMount>();
            List<CustomFuelTankMount> mounts = new List<CustomFuelTankMount>();
            for (int i = 0; i < len; i++)
            {
                mountNode = mountNodes[i];
                if (mountNode.GetBoolValue("useForNose", true))
                {
                    noses.Add(new CustomFuelTankMount(mountNode, true));
                }
                if (mountNode.GetBoolValue("useForMount", true))
                {
                    mounts.Add(new CustomFuelTankMount(mountNode, false));
                }
            }
            mountModules = mounts.ToArray();
            noseModules = noses.ToArray();

            len = fuelNodes.Length;
            fuelTypes = new FuelTypeData[len];
            for (int i = 0; i < len; i++) { fuelTypes[i] = new FuelTypeData(fuelNodes[i]); }

            len = limitNodes.Length;
            techLimits = new TechLimitDiameterHeight[len];
            for (int i = 0; i < len; i++) { techLimits[i] = new TechLimitDiameterHeight(limitNodes[i]); }

            len = textureNodes.Length;
            textureSets = new TextureSet[len];
            for (int i = 0; i < len; i++)
            {
                textureSets[i] = TextureSets.INSTANCE.getTextureSet(textureNodes[i].GetStringValue("name"));
            }

            topNodeNames = SSTUUtils.parseCSV(topManagedNodeNames);
            bottomNodeNames = SSTUUtils.parseCSV(bottomManagedNodeNames);
        }

        /// <summary>
        /// Loads or builds the models for the currently selected modules.
        /// </summary>
        private void restoreModels()
        {
            Transform modelBase = part.transform.FindRecursive("model");
            currentMainTankModule.setupModel(part, modelBase);            
            currentNoseModule.setupModel(part, modelBase);            
            currentMountModule.setupModel(part, modelBase);
        }
        
        /// <summary>
        /// Updates the internal cached values for the modules based on the current tank settings for scale/volume/position;
        /// done separately from updating the actual models so that the values can be used without the models even being present
        /// </summary>
        private void updateModuleStats()
        {
            currentMainTankModule.updateScaleForDiameter(currentTankDiameter);
            currentNoseModule.updateScaleForDiameter(currentTankDiameter);
            currentMountModule.updateScaleForDiameter(currentTankDiameter);
            float totalHeight = currentMainTankModule.currentHeight + currentNoseModule.currentHeight + currentMountModule.currentHeight;
            float startY = totalHeight * 0.5f;
            startY -= currentNoseModule.currentHeight;
            currentNoseModule.currentVerticalPosition = startY;
            startY -= currentMainTankModule.currentHeight * 0.5f;
            currentMainTankModule.currentVerticalPosition = startY;
            startY -= currentMainTankModule.currentHeight * 0.5f;
            currentMountModule.currentVerticalPosition = startY;
        }

        private void updateModels()
        {
            currentMainTankModule.updateModel();
            currentNoseModule.updateModel();
            currentMountModule.updateModel();
        }

        private void updateTankStats()
        {
            currentTankVolume = currentMainTankModule.getModuleVolume() + currentNoseModule.getModuleVolume() + currentMountModule.getModuleVolume();
            if (useRF)
            {
                SSTUUtils.updateRealFuelsPartVolume(part, currentTankVolume);
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
            Transform tr = part.transform.FindRecursive("model");
            SSTUUtils.destroyChildren(tr);
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

        private void updateDragCube()
        {
            DragCube newCube = DragCubeSystem.Instance.RenderProceduralDragCube(part);
            newCube.Name = "Default";
            part.DragCubes.ClearCubes();
            part.DragCubes.Cubes.Add(newCube);
            part.DragCubes.ResetCubeWeights();
            part.DragCubes.SetCubeWeight("Default", 1f);
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
                    tank.setFuelTypeFromEditor(Array.Find(tank.fuelTypes, m => m.name==currentFuelType), false);
                }
            }
        }
        
        private void setNoseModuleFromEditor(CustomFuelTankMount newModule, bool updateSymmetry)
        {
            currentNoseModule.destroyCurrentModel();
            currentNoseModule = newModule;
            newModule.setupModel(part, part.transform.FindRecursive("model"));
            currentNoseType = newModule.name;

            updateEditorStats(true);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setNoseModuleFromEditor(newModule, false);
                }
            }
        }
        
        private void setMainTankModuleFromEditor(SingleModelData newModule, bool updateSymmetry)
        {
            currentMainTankModule.destroyCurrentModel();
            currentMainTankModule = newModule;
            currentMainTankModule.setupModel(part, part.transform.FindRecursive("model"));
            currentTankType = newModule.name;

            updateEditorStats(true);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setMainTankModuleFromEditor(newModule, false);
                }
            }
        }
        
        private void setMountModuleFromEditor(CustomFuelTankMount newModule, bool updateSymmetry)
        {
            currentMountModule.destroyCurrentModel();
            currentMountModule = newModule;
            newModule.setupModel(part, part.transform.FindRecursive("model"));
            currentMountType = newModule.name;
            
            updateEditorStats(true);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setMountModuleFromEditor(newModule, false);
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

            SSTUUtils.updateSurfaceAttachedChildren(part, oldDiameter, newDiameter);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setTankDiameterFromEditor(newDiameter, false);
                }
            }
        }

        private void updateEditorStats(bool userInput)
        {
            updateModuleStats();
            updateModels();
            updateTankStats();
            updatePartResources();
            updateTextureSet(false);
            updateAttachNodes(userInput);
            updateDragCube();
            updateFairing();
        }

        private void setNoseTextureFromEditor(TextureSet set, bool updateSymmetry)
        {
            currentNoseTexture = set.setName;
            updateModuleTexture(currentNoseModule, set);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setNoseTextureFromEditor(set, false);
                }
            }
        }

        private void setTankTextureFromEditor(TextureSet set, bool updateSymmetry)
        {
            currentTankTexture = set.setName;
            updateModuleTexture(currentMainTankModule, set);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setTankTextureFromEditor(set, false);
                }
            }
        }

        private void setMountTextureFromEditor(TextureSet set, bool updateSymmetry)
        {
            currentMountTexture = set.setName;
            updateModuleTexture(currentMountModule, set);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularFuelTank>().setMountTextureFromEditor(set, false);
                }
            }
        }

        private void updateModuleTexture(SingleModelData module, TextureSet set)
        {
            if (module.model == null) { return; }//model may be null if the module has no model (e.g. 'None' mount type)
            set.enable(module.model.transform);
        }

        private void updateTextureSet(bool updateSymmetry)
        {
            updateFlagTransform();
            updateModuleTexture(currentNoseModule, Array.Find(textureSets, m=>m.setName==currentNoseTexture));
            updateModuleTexture(currentMainTankModule, Array.Find(textureSets, m => m.setName == currentTankTexture));
            updateModuleTexture(currentMountModule, Array.Find(textureSets, m => m.setName == currentMountTexture));

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts) { p.GetComponent<SSTUModularFuelTank>().updateTextureSet(false); }
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
        }

        private void updateAttachNodes(bool userInput)
        {
            currentNoseModule.updateAttachNodes(part, topNodeNames, userInput);
            currentMountModule.updateAttachNodes(part, bottomNodeNames, userInput);
            AttachNode surface = part.srfAttachNode;
            if (surface != null)
            {
                Vector3 pos = new Vector3(currentTankDiameter * 0.5f, 0, 0);
                Vector3 orientation = new Vector3(1, 0, 0);
                SSTUUtils.updateAttachNodePosition(part, surface, pos, orientation, userInput);
            }
            AttachNode interstage = part.findAttachNode(interstageNodeName);
            if (interstage != null)
            {
                float y = currentMountModule.currentVerticalPosition + (currentMountModule.mountDefinition.fairingTopOffset * currentMountModule.currentHeightScale);
                Vector3 pos = new Vector3(0, y, 0);
                Vector3 orientation = new Vector3(0, -1, 0);
                SSTUUtils.updateAttachNodePosition(part, interstage, pos, orientation, userInput);
            }
        }

        private void updateFairing()
        {
            SSTUNodeFairing fairing = part.GetComponent<SSTUNodeFairing>(); 
            if (!fairing.initialized()) { return; }
            float pos = currentMountModule.currentVerticalPosition + (currentMountModule.currentHeightScale * currentMountModule.mountDefinition.fairingTopOffset);
            fairing.setFairingTopY(pos);
            fairing.setFairingTopRadius(currentTankDiameter * 0.5f);
            if (currentMountModule.mountDefinition.fairingDisabled) { fairing.enableFairingFromEditor(false); }
        }

        private void updateTechLimits()
        {
            techLimitMaxDiameter = float.PositiveInfinity;
            techLimitMaxHeight = float.PositiveInfinity;
            if (!SSTUUtils.isResearchGame()) { return; }
            if (HighLogic.CurrentGame == null) { return; }
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX) { return; }
            techLimitMaxDiameter = 0;
            techLimitMaxHeight = 0;
            foreach (TechLimitDiameterHeight limit in techLimits)
            {
                if (limit.isUnlocked())
                {
                    if (limit.maxDiameter > techLimitMaxDiameter) { techLimitMaxDiameter = limit.maxDiameter; }
                    if (limit.maxHeight > techLimitMaxHeight) { techLimitMaxHeight = limit.maxHeight; }
                }
            }
        }

        private SingleModelData getNextTankLength(SingleModelData currentModule, bool iterateBackwards)
        {
            if (!SSTUUtils.isResearchGame())
            {
                return SSTUUtils.findNext(mainTankModules, m => m == currentModule, iterateBackwards);
            }
            return SSTUUtils.findNextEligible<SingleModelData>(mainTankModules, m => m == currentMainTankModule, l => l.height <= techLimitMaxHeight, iterateBackwards);            
        }

        private CustomFuelTankMount getNextCap(CustomFuelTankMount[] mounts, CustomFuelTankMount currentMount, String[] nodeNames, bool iterateBackwards)
        {
            return SSTUUtils.findNextEligible<CustomFuelTankMount>(mounts, m => m == currentMount, l => l.canSwitchTo(part, nodeNames), iterateBackwards);            
        }
        
    }

    public class CustomFuelTankMount : MountModelData
    {
        public bool useForNose = true;
        public bool useForMount = true;

        public CustomFuelTankMount(ConfigNode node, bool isNose) : base(node, isNose)
        {

        }

        public void updateAttachNodes(Part part, String[] nodeNames, bool userInput)
        {
            Vector3 basePos = new Vector3(0, currentVerticalPosition, 0);
            AttachNode node = null;
            int len = nodeNames.Length;

            Vector3 pos = Vector3.zero;
            Vector3 orient = Vector3.up;
            int size = 2;
            for (int i = 0; i < len; i++)
            {
                node = part.findAttachNode(nodeNames[i]);                
                if (i < mountDefinition.nodePositions.Count)
                {
                    size = nodePositions[i].size;
                    pos = nodePositions[i].position * currentHeightScale;
                    pos.y += currentVerticalPosition;
                    orient = nodePositions[i].orientation;
                    if (node == null)//create it
                    {
                        SSTUUtils.createAttachNode(part, nodeNames[i], pos, orient, size);
                    }
                    else//update its position
                    {
                        SSTUUtils.updateAttachNodePosition(part, node, pos, orient, userInput);
                    }
                }                
                else//extra node, destroy
                {
                    if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
                    {
                        SSTUUtils.destroyAttachNode(part, node);
                    }
                }
            }
        }

        /// <summary>
        /// Determine if the number of parts attached to the part will prevent this mount from being applied;
        /// if any node that has a part attached would be deleted, return false
        /// </summary>
        /// <param name="part"></param>
        /// <param name="nodeNames"></param>
        /// <returns></returns>
        public bool canSwitchTo(Part part, String[] nodeNames)
        {
            AttachNode node;
            int len = nodeNames.Length;
            for (int i = 0; i < len; i++)
            {
                if (i < nodePositions.Count) { continue; }//don't care about those nodes, they will be present
                node = part.findAttachNode(nodeNames[i]);//this is a node that would be disabled
                if (node == null) { continue; }//already disabled, and that is just fine
                else if (node.attachedPart != null) { return false; }//drat, this node is scheduled for deletion, but has a part attached; cannot delete it, so cannot switch to this mount
            }
            return true;//and if all node checks go okay, return true by default...
        }
    }
    
}

