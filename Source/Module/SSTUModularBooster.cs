using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace SSTUTools
{
    class SSTUModularBooster : PartModule
    {
        #region REGION - KSP Config Variables

        /// <summary>
        /// Models will be added to this transform
        /// </summary>
        [KSPField]
        public string baseTransformName = "SSTU-MRB-BaseTransform";

        /// <summary>
        /// If not empty, a transform by this name will be added to the base part model.<para></para>
        /// These will only be added during the initial part prefab setup.
        /// </summary>
        [KSPField]
        public String thrustTransformName = "SSTU-MRB-ThrustTransform";

        [KSPField]
        public String gimbalTransformName = "SSTU-MRB-GimbalTransform";

        /// <summary>
        /// If true, the engine thrust will be scaled with model changes by the parameters below
        /// </summary>
        [KSPField]
        public bool scaleMotorThrust = true;

        /// <summary>
        /// Defaults to using cubic scaling for engine thrust, to maintain constant burn time for scaling vs. resource quantity.
        /// </summary>
        [KSPField]
        public float thrustScalePower = 3;

        [KSPField]
        public bool scaleMotorMass = true;

        [KSPField]
        public float motorMassScalePower = 3;

        /// <summary>
        /// If true, resources will be scaled with model changes by the parameters below
        /// </summary>
        [KSPField]
        public bool scaleResources = true;

        /// <summary>
        /// Determines the scaling power of resources scaling.  Default is cubic scaling, as this represents how volume scales with diameter changes.
        /// </summary>
        [KSPField]
        public float resourceScalePower = 3;

        [KSPField]
        public float minDiameter = 0.625f;

        [KSPField]
        public float maxDiameter = 10f;

        [KSPField]
        public float diameterIncrement = 0.625f;

        [KSPField]
        public bool useRF = false;

        #endregion REGION - KSP Config Variables

        #region REGION - Persistent Variables

        [KSPField(isPersistant = true)]
        public String currentNoseName;

        [KSPField(isPersistant = true)]
        public String currentNozzleName;

        [KSPField(isPersistant = true)]
        public String currentMainName;
        
        [KSPField(isPersistant = true)]
        public float currentDiameter;
        
        [KSPField(isPersistant = true)]
        public float currentGimbalOffset;

        [KSPField(isPersistant = true)]
        public String currentMainTexture;

        [KSPField(isPersistant = true)]
        public String currentNoseTexture;

        [KSPField(isPersistant = true)]
        public String currentNozzleTexture;

        //do NOT adjust this through config, or you will mess up your resource updates in the editor; you have been warned
        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        #endregion ENDREGION - Persistent variables

        #region REGION - GUI Variables

        [KSPField(guiName = "Diameter", guiActiveEditor = true)]
        public float guiDiameter = 0f;

        [KSPField(guiName = "Height", guiActiveEditor = true)]
        public float guiHeight = 0f;

        [KSPField(guiName = "Dry Mass", guiActiveEditor = true)]
        public float guiDryMass = 0f;

        [KSPField(guiName = "Propellant Mass", guiActiveEditor = true)]
        public float guiPropellantMass;

        [KSPField(guiName = "Thrust", guiActiveEditor = true)]
        public float guiThrust = 0f;

        [KSPField(guiName = "Burn Tme", guiActiveEditor = true)]
        public float guiBurnTime = 0f;

        [KSPField(guiName = "Diameter Adjust", guiActiveEditor = true, guiActive = false), UI_FloatRange(minValue = 0f, maxValue = 0.95f, stepIncrement = 0.05f)]
        public float editorDiameterAdjust = 0f;

        [KSPField(guiName = "Nozzle Angle", guiActiveEditor = true, guiActive = false), UI_FloatRange(minValue = -1f, maxValue = 1f, stepIncrement = 0.1f)]
        public float editorNozzleAdjust = 0f;

        #endregion

        #region REGION - Private working variables

        private bool initialized = false;

        private float editorDiameterWhole;
        private float prevEditorDiameterAdjust;

        private float prevEditorNozzleAdjust;
                
        private FuelTypeData fuelTypeData;

        private MountModelData[] noseModules;
        private SRBNozzleData[] nozzleModules;
        private SRBModelData[] mainModules;

        private MountModelData currentNoseModule;
        private SRBNozzleData currentNozzleModule;
        private SRBModelData currentMainModule;

        private TechLimitDiameter[] techLimits;
        private float techLimitMaxDiameter;
        
        [Persistent]
        public String configNodeData;

        #endregion ENDREGION - Private working variables

        #region REGION - KSP GUI Interaction Methods

        /// <summary>
        /// Called when user presses the decrease diameter button in editor
        /// </summary>
        [KSPEvent(guiName = "Diameter --", guiActiveEditor = true, guiActive = false)]
        public void prevDiameterEvent()
        {
            updateDiameterFromEditor(currentDiameter - diameterIncrement, true);
        }

        /// <summary>
        /// Called when user presses the increase diameter button in editor
        /// </summary>
        [KSPEvent(guiName = "Diameter ++", guiActiveEditor = true, guiActive = false)]
        public void nextDiameterEvent()
        {
            updateDiameterFromEditor(currentDiameter + diameterIncrement, true);
        }

        [KSPEvent(guiName = "Length --", guiActiveEditor = true, guiActive = false)]
        public void prevMainModelEvent()
        {
            SingleModelData d = SSTUUtils.findNext(mainModules, m => m.name == currentMainName, true);
            updateMainModelFromEditor(d.name, true);
        }

        [KSPEvent(guiName = "Length ++", guiActiveEditor = true, guiActive = false)]
        public void nextMainModelEvent()
        {
            SingleModelData d = SSTUUtils.findNext(mainModules, m => m.name == currentMainName, false);
            updateMainModelFromEditor(d.name, true);
        }

        [KSPEvent(guiName = "Prev Nose", guiActiveEditor = true, guiActive = false)]
        public void prevNoseModelEvent()
        {
            SingleModelData d = SSTUUtils.findNext(noseModules, m => m.name == currentNoseName, true);
            updateNoseFromEditor(d.name, true);
        }

        [KSPEvent(guiName = "Next Nose", guiActiveEditor = true, guiActive = false)]
        public void nextNoseModelEvent()
        {
            SingleModelData d = SSTUUtils.findNext(noseModules, m => m.name == currentNoseName, false);
            updateNoseFromEditor(d.name, true);
        }

        [KSPEvent(guiName = "Prev Nozzle", guiActiveEditor = true, guiActive = false)]
        public void prevMountModelEvent()
        {
            SingleModelData d = SSTUUtils.findNext(nozzleModules, m => m.name == currentNozzleName, true);
            updateMountFromEditor(d.name, true);
        }

        [KSPEvent(guiName = "Next Nozzle", guiActiveEditor = true, guiActive = false)]
        public void nextMountModelEvent()
        {
            SingleModelData d = SSTUUtils.findNext(nozzleModules, m => m.name == currentNozzleName, false);
            updateMountFromEditor(d.name, true);
        }

        [KSPEvent(guiName = "Next Nose Texture", guiActiveEditor = true, guiActive = false)]
        public void nextNoseTextureEvent()
        {
            String nextTex = currentNoseModule.getNextTextureSetName(currentNoseTexture, false);
            updateNoseTextureFromEditor(nextTex, true);
        }

        [KSPEvent(guiName = "Next Main Texture", guiActiveEditor = true, guiActive = false)]
        public void nextMainTextureEvent()
        {
            String nextTex = currentMainModule.getNextTextureSetName(currentMainTexture, false);
            updateMainTextureFromEditor(nextTex, true);
        }

        [KSPEvent(guiName = "Next Nozzle Texture", guiActiveEditor = true, guiActive = false)]
        public void nextNozzleTextureEvent()
        {
            String nextTex = currentNozzleModule.getNextTextureSetName(currentNozzleTexture, false);
            updateNozzleTextureFromEditor(nextTex, true);
        }

        /// <summary>
        /// Updates the current model scales from user input in the editor
        /// </summary>
        /// <param name="newDiameter"></param>
        /// <param name="updateSymmetry"></param>
        private void updateDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {
            if (newDiameter > maxDiameter) { newDiameter = maxDiameter; }
            if (newDiameter < minDiameter) { newDiameter = minDiameter; }
            if (newDiameter > techLimitMaxDiameter) { newDiameter = techLimitMaxDiameter; }
            float oldDiameter = currentDiameter;
            currentDiameter = newDiameter;
            updateModelScaleAndPosition();
            updatePartResources();
            updatePartMass();
            updateAttachnodes(true);
            SSTUAttachNodeUtils.updateSurfaceAttachedChildren(part, oldDiameter, currentDiameter);
            updateThrustOutput();
            updateEditorValues();
            updateGui();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularBooster>().updateDiameterFromEditor(newDiameter, false);
                }
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }

        /// <summary>
        /// Updates the main-segment model from user input in the editor
        /// </summary>
        /// <param name="newModel"></param>
        /// <param name="updateSymmetry"></param>
        private void updateMainModelFromEditor(String newModel, bool updateSymmetry)
        {
            SRBModelData mod = Array.Find(mainModules, m => m.name == newModel);
            if (mod != null && mod != currentMainModule)
            {
                currentMainModule.destroyCurrentModel();
                currentMainModule = mod;
                currentMainModule.setupModel(part, part.transform.FindRecursive(baseTransformName), ModelOrientation.CENTRAL);
                currentMainName = currentMainModule.name;
            }
            if (!currentMainModule.isValidTextureSet(currentMainTexture))
            {
                currentMainTexture = currentMainModule.modelDefinition.defaultTextureSet;            
            }
            currentMainModule.enableTextureSet(currentMainTexture);
            updateModelScaleAndPosition();
            updatePartResources();
            updatePartMass();
            updateAttachnodes(true);
            updateThrustOutput();
            updateEditorValues();
            updateGui();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularBooster>().updateMainModelFromEditor(newModel, false);
                }
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }

        /// <summary>
        /// Update the nose module from user input in the editor
        /// </summary>
        /// <param name="newNose"></param>
        /// <param name="updateSymmetry"></param>
        private void updateNoseFromEditor(String newNose, bool updateSymmetry)
        {
            MountModelData mod = Array.Find(noseModules, m => m.name == newNose);
            if (mod != null && mod != currentNoseModule)
            {
                currentNoseModule.destroyCurrentModel();
                mod.setupModel(part, part.transform.FindRecursive(baseTransformName), ModelOrientation.TOP);
                currentNoseModule = mod;
                currentNoseName = currentNoseModule.name;
            }
            if (!currentNoseModule.isValidTextureSet(currentNoseTexture))
            {
                currentNoseTexture = currentNoseModule.modelDefinition.defaultTextureSet;
            }
            currentNoseModule.enableTextureSet(currentNoseTexture);
            updateModelScaleAndPosition();
            updatePartResources();
            updatePartMass();
            updateAttachnodes(true);
            updateEditorValues();
            updateGui();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularBooster>().updateNoseFromEditor(newNose, false);
                }
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }

        /// <summary>
        /// Updates the mount module from user input in the editor
        /// </summary>
        /// <param name="newMount"></param>
        /// <param name="updateSymmetry"></param>
        private void updateMountFromEditor(String newMount, bool updateSymmetry)
        {
            SRBNozzleData mod = Array.Find(nozzleModules, m => m.name == newMount);
            if (mod != null && mod != currentNozzleModule)
            {
                //finally, clear any existing models from prefab, and initialize the currently configured models
                resetTransformParents();

                currentNozzleModule.destroyCurrentModel();
                currentNozzleModule = mod;
                currentNozzleModule.setupModel(part, part.transform.FindRecursive(baseTransformName), ModelOrientation.BOTTOM);
                currentNozzleName = currentNozzleModule.name;
                currentGimbalOffset = 0;                
            }
            if (!currentNozzleModule.isValidTextureSet(currentNozzleTexture))
            {
                currentNozzleTexture = currentNozzleModule.modelDefinition.defaultTextureSet;
            }
            currentNozzleModule.enableTextureSet(currentNozzleTexture);
            updateModelScaleAndPosition();
            updatePartResources();
            updatePartMass();
            updateAttachnodes(true);
            updateThrustOutput();
            currentNozzleModule.setupTransformDefaults(part.transform.FindRecursive(thrustTransformName), part.transform.FindRecursive(gimbalTransformName));
            updateGimbalOffset();
            updateEngineISP();
            updateEditorValues();
            updateGui();

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularBooster>().updateMountFromEditor(newMount, false);
                }
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }

        private void updateGimbalOffsetFromEditor(float newOffset, bool updateSymmetry)
        {
            if (newOffset < -currentNozzleModule.gimbalAdjustmentRange)
            {
                newOffset = -currentNozzleModule.gimbalAdjustmentRange;
            }
            if (newOffset > currentNozzleModule.gimbalAdjustmentRange)
            {
                newOffset = currentNozzleModule.gimbalAdjustmentRange;
            }
            currentGimbalOffset = newOffset;
            currentNozzleModule.updateGimbalRotation(part.transform.forward, currentGimbalOffset);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularBooster>().updateGimbalOffsetFromEditor(newOffset, false);
                }
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }

        private void updateMainTextureFromEditor(String newTex, bool updateSymmetry)
        {
            currentMainTexture = newTex;
            currentMainModule.enableTextureSet(newTex);
            if (updateSymmetry)
            {
                foreach(Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularBooster>().updateMainTextureFromEditor(newTex, false);
                }
            }
        }

        private void updateNoseTextureFromEditor(String newTex, bool updateSymmetry)
        {
            currentNoseTexture = newTex;
            currentNoseModule.enableTextureSet(newTex);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularBooster>().updateNoseTextureFromEditor(newTex, false);
                }
            }
        }

        private void updateNozzleTextureFromEditor(String newTex, bool updateSymmetry)
        {
            currentNozzleTexture = newTex;
            currentNozzleModule.enableTextureSet(newTex);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularBooster>().updateNozzleTextureFromEditor(newTex, false);
                }
            }
        }

        #endregion ENDREGION - KSP GUI Interaction Methods

        #region REGION - Standard KSP Overrides

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
            StartCoroutine(delayedDragUpdate());
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasNode("CAP") || node.HasNode("MAINMODEL") || node.HasNode("FUELTYPE"))
            {
                configNodeData = node.ToString();
            }
            initialize();
        }

        //TODO -- what?
        public void Start()
        {
            updateGimbalOffset();
            updateThrustOutput();
            updateGui();
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
        /// Initializes a delayed update to the drag cube for this part
        /// </summary>
        /// <returns></returns>
        private IEnumerator delayedDragUpdate()
        {
            yield return new WaitForFixedUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        /// <summary>
        /// Event callback for when vessel is modified in the editor.  Used to know when the gui-fields for this module have been updated.
        /// </summary>
        /// <param name="ship"></param>
        public void onEditorVesselModified(ShipConstruct ship)
        {
            if (!HighLogic.LoadedSceneIsEditor) { return; }
            if (prevEditorDiameterAdjust != editorDiameterAdjust)
            {
                prevEditorDiameterAdjust = editorDiameterAdjust;
                float newDiameter = editorDiameterWhole * diameterIncrement + diameterIncrement * editorDiameterAdjust;
                updateDiameterFromEditor(newDiameter, true);
            }
            if (prevEditorNozzleAdjust != editorNozzleAdjust)
            {
                prevEditorNozzleAdjust = editorNozzleAdjust;
                float newOffset = editorNozzleAdjust * currentNozzleModule.gimbalAdjustmentRange;

                updateGimbalOffsetFromEditor(newOffset, true);
            }
        }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Initialization Methods

        /// <summary>
        /// Initializes all modules and variables for this PartModule
        /// </summary>
        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor) { initiaizePrefab(); }//init thrust transforms and/or other persistent models
            loadConfigNodeData();            
            updateModelScaleAndPosition();
            updateAttachnodes(false);
            updateThrustOutput();
            updateEngineISP();
            updateGimbalOffset();
            updateEditorValues();
            if (!initializedResources && (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
            {
                initializedResources = true;
                updatePartResources();
            }
            updateGui();
            updateTextureSets();
        }

        /// <summary>
        /// Update the editor values for whole and partial increments based on the current setup parameters.
        /// Ensures that pressing the ++/-- buttom with a parital increment selected will carry that increment over or zero it out if is out of bounds.
        /// Also allows for non-whole increments to be used for min and max values for the adjusted parameters
        /// </summary>
        private void updateEditorValues()
        {
            float div = currentDiameter / diameterIncrement;
            float whole = (int)div;
            float extra = div - whole;
            editorDiameterWhole = whole;
            editorDiameterAdjust = prevEditorDiameterAdjust = extra;

            float max = currentNozzleModule.gimbalAdjustmentRange;
            float percent = max == 0 ? 0 : currentGimbalOffset / max;
            editorNozzleAdjust = percent;
            prevEditorNozzleAdjust = editorNozzleAdjust;
        }

        /// <summary>
        /// Initializes thrust transforms for the part; should only be called during prefab init.  Transforms will then be cloned into live model as-is.
        /// </summary>
        private void initiaizePrefab()
        {
            Transform modelBase = part.transform.FindRecursive("model");

            GameObject thrustTransformGO = new GameObject(thrustTransformName);
            thrustTransformGO.transform.NestToParent(modelBase.transform);
            thrustTransformGO.SetActive(true);
            MonoBehaviour.print("SSTUModularBooster - Created reference thrust transform during prefab construction: " + thrustTransformGO);

            GameObject gimbalTransformGO = new GameObject(gimbalTransformName);
            gimbalTransformGO.transform.NestToParent(modelBase.transform);
            gimbalTransformGO.SetActive(true);
            MonoBehaviour.print("SSTUModularBooster - Created reference gimbal transform during prefab construction: " + gimbalTransformGO);
        }

        /// <summary>
        /// Loads the current configuration from the cached persistent config node data
        /// </summary>
        private void loadConfigNodeData()
        {
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);

            techLimits = TechLimitDiameter.loadTechLimits(node.GetNodes("TECHLIMIT"));
            TechLimitDiameter.updateTechLimits(techLimits, out techLimitMaxDiameter);
            if (currentDiameter > techLimitMaxDiameter) { currentDiameter = techLimitMaxDiameter; }


            //load singular fuel type data from config node;
            //using a node so that it may have the custom fields defined for mass fraction/etc on a per-part basis
            fuelTypeData = new FuelTypeData(node.GetNode("FUELTYPE"));

            //load all main tank model datas
            mainModules = SRBModelData.parseSRBModels(node.GetNodes("MAINMODEL"));
            currentMainModule = Array.Find(mainModules, m => m.name == currentMainName);
            if (currentMainModule == null)
            {
                currentMainModule = mainModules[0];
                currentMainName = currentMainModule.name;
            }
            if (!currentMainModule.isValidTextureSet(currentMainTexture)) { currentMainTexture = currentMainModule.modelDefinition.defaultTextureSet; }

            //load nose modules from NOSE nodes
            ConfigNode[] noseNodes = node.GetNodes("NOSE");
            ConfigNode noseNode;
            int length = noseNodes.Length;
            List<MountModelData> noseModulesTemp = new List<MountModelData>();
            for (int i = 0; i < length; i++)
            {
                noseNode = noseNodes[i];
                noseModulesTemp.Add(new MountModelData(noseNode));
            }
            this.noseModules = noseModulesTemp.ToArray();
            currentNoseModule = Array.Find(this.noseModules, m => m.name == currentNoseName);
            if (currentNoseModule == null)
            {
                currentNoseModule = this.noseModules[0];//not having a mount defined is an error, at least one mount must be defined, crashing at this point is acceptable
                currentNoseName = currentNoseModule.name;
            }
            if (!currentNoseModule.isValidTextureSet(currentNoseTexture)) { currentNoseTexture = currentNoseModule.modelDefinition.defaultTextureSet; }

            //load nose modules from NOZZLE nodes
            ConfigNode[] nozzleNodes = node.GetNodes("NOZZLE");
            ConfigNode nozzleNode;
            length = nozzleNodes.Length;            
            List<SRBNozzleData> nozzleModulesTemp = new List<SRBNozzleData>();
            for (int i = 0; i < length; i++)
            {
                nozzleNode = nozzleNodes[i];
                nozzleModulesTemp.Add(new SRBNozzleData(nozzleNode));
            }
            this.nozzleModules = nozzleModulesTemp.ToArray();
            currentNozzleModule = Array.Find(this.nozzleModules, m => m.name == currentNozzleName);
            if (currentNozzleModule == null)
            {
                currentNozzleModule = this.nozzleModules[0];//not having a mount defined is an error, at least one mount must be defined, crashing at this point is acceptable
                currentNozzleName = currentNozzleModule.name;
            }
            if (!currentNozzleModule.isValidTextureSet(currentNozzleTexture)) { currentNozzleTexture = currentNozzleModule.modelDefinition.defaultTextureSet; }
            
            //reset existing gimbal/thrust transforms, remove them from the model hierarchy
            resetTransformParents();//this resets the thrust transform parent in case it was changed during prefab; we don't want to delete the thrust transform
            Transform parentTransform = part.transform.FindRecursive("model").FindOrCreate(baseTransformName);
            //finally, clear any existing models from prefab, and initialize the currently configured models
            SSTUUtils.destroyChildren(parentTransform);
            currentNoseModule.setupModel(part, parentTransform, ModelOrientation.TOP);
            currentNozzleModule.setupModel(part, parentTransform, ModelOrientation.BOTTOM);
            currentMainModule.setupModel(part, parentTransform, ModelOrientation.CENTRAL);
            //lastly, re-insert gimbal and thrust transforms into model hierarchy and reset default gimbal rotation offset
            currentNozzleModule.setupTransformDefaults(part.transform.FindRecursive(thrustTransformName), part.transform.FindRecursive(gimbalTransformName));
        }

        /// <summary>
        /// Updates the rendering scale and position for the currently enabled modules
        /// </summary>
        private void updateModelScaleAndPosition()
        {
            currentNoseModule.updateScaleForDiameter(currentDiameter);
            currentMainModule.updateScaleForDiameter(currentDiameter);
            currentNozzleModule.updateScaleForDiameter(currentDiameter);
            currentNoseModule.currentVerticalPosition = currentMainModule.currentHeight * 0.5f;
            currentMainModule.currentVerticalPosition = 0f;
            currentNozzleModule.currentVerticalPosition = -currentMainModule.currentHeight * 0.5f;
            currentNoseModule.updateModel();
            currentMainModule.updateModel();
            currentNozzleModule.updateModel();
        }
               
        /// <summary>
        /// Update the engines min and max thrust values based on the currently selected main tank segment
        /// </summary>
        private void updateThrustOutput()
        {
            ModuleEngines engine = part.GetComponent<ModuleEngines>();
            if (engine != null)
            {
                float minThrust = Mathf.Pow(currentMainModule.currentDiameterScale, thrustScalePower) * currentMainModule.minThrust;
                float maxThrust = Mathf.Pow(currentMainModule.currentDiameterScale, thrustScalePower) * currentMainModule.maxThrust;
                SSTUUtils.updateEngineThrust(engine, minThrust, maxThrust);
            }
        }

        /// <summary>
        /// Updates the current gimbal transform angle and the gimbal modules range values to the values for the current nozzle module
        /// </summary>
        private void updateGimbalOffset()
        {
            //update the transform orientation for the gimbal so that the moduleGimbal gets the correct 'defaultOrientation'
            currentNozzleModule.updateGimbalRotation(part.transform.forward, currentGimbalOffset);            

            //update the ModuleGimbals transform and orientation data
            ModuleGimbal gimbal = part.GetComponent<ModuleGimbal>();
            if (gimbal != null)
            {
                if (gimbal.initRots == null)//gimbal is uninitialized; do nothing...
                {
                    //MonoBehaviour.print("Gimbal module rotations are null; gimbal uninitialized.");
                    gimbal.gimbalTransformName = gimbalTransformName;
                }
                else
                {
                    //MonoBehaviour.print("Updating gimbal module data...");
                    //set gimbal actuation range
                    gimbal.gimbalTransformName = gimbalTransformName;
                    gimbal.gimbalRange = currentNozzleModule.gimbalFlightRange;
                    gimbal.OnStart(StartState.Flying);//forces gimbal to re-init its transform and default orientation data
                }
            }
            else
            {
                MonoBehaviour.print("Could not update gimbal, no module found");
            }
        }

        //TODO
        /// <summary>
        /// Update the engine ISP based on the currently selected nozzle mode (atmo or vacuum specialized)
        /// </summary>
        private void updateEngineISP()
        {
            // pull ISP from nozzle
            // set ISP to engine
            // will need to find out if I can directly adjust the curve (it is probably public)
            // or if I need to create a fake config node and feed that back through the OnLoad setup
            // in theory it -should- accept just chaning of the curve, as I believe the curve is queried directly for the resultant ISP output.
        }

        /// <summary>
        /// Update attach node positions and optionally update the parts attached to those nodes if userInput==true
        /// </summary>
        /// <param name="userInput"></param>
        private void updateAttachnodes(bool userInput)
        {
            Vector3 pos;
            AttachNode topNode = part.findAttachNode("top");
            if (topNode != null)
            {
                pos = new Vector3(0, currentMainModule.currentHeight * 0.5f + currentNoseModule.currentHeight, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, topNode, pos, topNode.orientation, userInput);
            }
            AttachNode bottomNode = part.findAttachNode("bottom");
            if (bottomNode != null)
            {
                pos = new Vector3(0, -currentMainModule.currentHeight * 0.5f - currentNozzleModule.currentHeight, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, bottomNode, pos, bottomNode.orientation, userInput);
            }
            AttachNode surface = part.srfAttachNode;
            if (surface != null)
            {
                pos = new Vector3(currentDiameter * 0.5f, 0, 0);
                Vector3 orientation = new Vector3(1, 0, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, surface, pos, orientation, userInput);
            }            
        }

        /// <summary>
        /// Update the volume of resources that are available in the part, based on the currently selected models and scales
        /// </summary>
        private void updatePartResources()
        {
            if (useRF) { return; }
            float currentVolume = currentMainModule.getModuleVolume();
            currentVolume = fuelTypeData.getUsableVolume(currentVolume);
            SSTUResourceList res = fuelTypeData.getResourceList(currentVolume);
            res.setResourcesToPart(part, HighLogic.LoadedSceneIsEditor);
        }
        
        /// <summary>
        /// Update the mass of the part (and real-fuels/MFT volume) based on the currently selected models and scales
        /// </summary>
        private void updatePartMass()
        {
            float volume = currentMainModule.getModuleVolume();
            if (useRF)
            {
                SSTUModInterop.onPartFuelVolumeUpdate(part, volume);
                return;
            }
            float usableVolume = fuelTypeData.getUsableVolume(volume);
            float dryMass = fuelTypeData.getTankageMass(usableVolume);
            part.mass = dryMass + currentNozzleModule.getModuleMass();            
        }

        /// <summary>
        /// Updates GUI fields for diameter/height/etc
        /// </summary>
        private void updateGui()
        {
            guiDryMass = part.mass;
            guiPropellantMass = fuelTypeData.getResourceMass(fuelTypeData.getUsableVolume(currentMainModule.getModuleVolume()));
            guiHeight = currentMainModule.currentHeight;
            guiDiameter = currentDiameter;

            ModuleEngines engine = part.GetComponent<ModuleEngines>();
            if (engine != null)
            {
                float maxThrust = Mathf.Pow(currentMainModule.currentDiameterScale, thrustScalePower) * currentMainModule.maxThrust;
                guiThrust = maxThrust * engine.thrustPercentage*0.01f;
            }
            else
            {
                guiThrust = 0;
            }
            float isp = 220f;
            float g = 9.81f;
            float flowRate = guiThrust / (g * isp);
            guiBurnTime = (guiPropellantMass / flowRate);
            
            if (noseModules.Length <= 1)
            {
                Events["prevNoseModelEvent"].active = false;
                Events["nextNoseModelEvent"].active = false;
            }
            if (nozzleModules.Length <= 1)
            {
                Events["prevMountModelEvent"].active = false;
                Events["nextMountModelEvent"].active = false;
            }
            if (mainModules.Length <= 1)
            {
                Events["prevMainModelEvent"].active = false;
                Events["nextMainModelEvent"].active = false;
            }
        }

        private void updateTextureSets()
        {
            currentNoseModule.enableTextureSet(currentNoseTexture);
            currentMainModule.enableTextureSet(currentMainTexture);
            currentNozzleModule.enableTextureSet(currentNozzleTexture);
        }

        /// <summary>
        /// Utility method to -temporarily- reset the parent of the thrust transform to the parts base model transform.<para></para>
        /// This should be used before deleting a nozzle/mount model to keep the same thrust transform object in use, 
        /// and the transforms should subsequently be re-parented to thier proper hierarchy after the new/updated model/module is initialized.
        /// </summary>
        /// <param name="modelBase"></param>
        private void resetTransformParents()
        {
            Transform modelBase = part.transform.FindRecursive("model");
            //re-parent the thrust transform so they do not get deleted when clearing the existing models
            Transform gimbal = modelBase.FindRecursive(gimbalTransformName);
            foreach (Transform tr in gimbal) { tr.parent = gimbal.parent; }
            gimbal.parent = modelBase;

            Transform thrust = modelBase.FindRecursive(thrustTransformName);
            foreach (Transform tr in thrust) { tr.parent = thrust.parent; }
            thrust.parent = modelBase;
        }

        #endregion ENDREGION - Initialization Methods
    }

    /// <summary>
    /// Data for the main segment models for the SRB
    /// </summary>
    public class SRBModelData : SingleModelData
    {
        public float minThrust;
        public float maxThrust;
        public SRBModelData(ConfigNode node) : base(node)
        {
            minThrust = node.GetFloatValue("minThrust");
            maxThrust = node.GetFloatValue("maxThrust");
        }

        public static SRBModelData[] parseSRBModels(ConfigNode[] modelNodes)
        {
            int len = modelNodes.Length;
            SRBModelData[] datas = new SRBModelData[len];
            for (int i = 0; i < len; i++)
            {
                datas[i] = new SRBModelData(modelNodes[i]);
            }
            return datas;
        }
    }

    /// <summary>
    /// Data for an srb nozzle, including gimbal adjustment data and ISP curve adjustment.
    /// </summary>
    public class SRBNozzleData : MountModelData
    {        
        public readonly String thrustTransformName;
        public readonly String gimbalTransformName;
        public readonly float gimbalAdjustmentRange;//how far the gimbal can be adjusted from reference while in the editor
        public readonly float gimbalFlightRange;//how far the gimbal may be actuated while in flight from the adjusted reference angle
        public Quaternion gimbalDefaultOrientation;

        public SRBNozzleData(ConfigNode node) : base(node)
        {
            thrustTransformName = node.GetStringValue("thrustTransformName");
            gimbalTransformName = node.GetStringValue("gimbalTransformName");
            gimbalAdjustmentRange = node.GetFloatValue("gimbalAdjustRange", 0);
            gimbalFlightRange = node.GetFloatValue("gimbalFlightRange", 0);
        }

        public Transform getGimbalTransform()
        {
            Transform transform = null;
            if (!String.IsNullOrEmpty(gimbalTransformName))
            {
                transform = model.transform.FindRecursive(gimbalTransformName);
            }
            return transform;
        }

        public Transform getThrustTransform()
        {
            Transform transform = null;
            if (!String.IsNullOrEmpty(thrustTransformName))
            {
                transform = model.transform.FindRecursive(thrustTransformName);
            }
            return transform;
        }

        /// <summary>
        /// Positions the input thrust transform as a child of the models existing gimbal transform
        /// in the same orientation as the models existing thrust transform.
        /// </summary>
        /// <param name="partThrustTransform"></param>
        public void setupTransformDefaults(Transform partThrustTransform, Transform partGimbalTransform)
        {
            Transform modelGimbalTransform = getGimbalTransform();
            Transform modelThrustTransform = getThrustTransform();

            partGimbalTransform.position = modelGimbalTransform.position;
            partGimbalTransform.rotation = modelGimbalTransform.rotation;
            partGimbalTransform.parent = modelGimbalTransform.parent;
            modelGimbalTransform.parent = partGimbalTransform;
            gimbalDefaultOrientation = modelGimbalTransform.localRotation;

            partThrustTransform.position = modelThrustTransform.position;
            partThrustTransform.rotation = modelThrustTransform.rotation;
            partThrustTransform.parent = modelGimbalTransform;
            //MonoBehaviour.print("set up transform default parenting and orientations; default orientation: "+gimbalDefaultOrientation);
        }

        /// <summary>
        /// Resets the gimbal to its default orientation, and then applies newRotation to it as a direct rotation around the input world axis
        /// </summary>
        /// <param name="partGimbalTransform"></param>
        /// <param name="newRotation"></param>
        public void updateGimbalRotation(Vector3 worldAxis, float newRotation)
        {
            //MonoBehaviour.print("updating rotation for angle: " + newRotation);
            Transform modelGimbalTransform = getGimbalTransform();
            modelGimbalTransform.localRotation = gimbalDefaultOrientation;
            modelGimbalTransform.Rotate(worldAxis, -newRotation, Space.World);
        }

    }
}
