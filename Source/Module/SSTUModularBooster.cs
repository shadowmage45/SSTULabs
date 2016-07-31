using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUModularBooster : PartModule, IPartCostModifier, IPartMassModifier
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
        public float diameterForThrustScaling = -1;
        
        [KSPField]
        public String techLimitSet = "Default";

        #endregion REGION - KSP Config Variables

        #region REGION - Persistent Variables

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Body"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentMainName;

        [KSPField(isPersistant = true, guiActiveEditor =true, guiName ="Nose"),
         UI_ChooseOption(suppressEditorShipModified =true)]
        public String currentNoseName;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Nozzle"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentNozzleName;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Diameter"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentDiameter;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Gimbal"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentGimbalOffset;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Nose Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentNoseTexture;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Body Texture"),
         UI_ChooseOption(suppressEditorShipModified =true)]
        public String currentMainTexture;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Nozzle Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentNozzleTexture;

        //do NOT adjust this through config, or you will mess up your resource updates in the editor; you have been warned
        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        [KSPField(isPersistant = true)]
        public string thrustCurveData;

        [KSPField(isPersistant = true)]
        public string presetCurveName;

        #endregion ENDREGION - Persistent variables

        #region REGION - GUI Display Variables

        [KSPField(guiName = "Height", guiActiveEditor = true)]
        public float guiHeight = 0f;

        [KSPField(guiName = "Thrust", guiActiveEditor = true)]
        public float guiThrust = 0f;

        [KSPField(guiName = "Burn Tme", guiActiveEditor = true)]
        public float guiBurnTime = 0f;

        #endregion

        #region REGION - Private working variables

        private bool initialized = false;

        private float prevDiameter;
        private float prevGimbal;
        private string prevNose;
        private string prevBody;
        private string prevNozzle;
                
        private SingleModelData[] noseModules;
        private SRBNozzleData[] nozzleModules;
        private SRBModelData[] mainModules;

        private SingleModelData currentNoseModule;
        private SRBNozzleData currentNozzleModule;
        private SRBModelData currentMainModule;
        
        private float techLimitMaxDiameter;

        private float modifiedCost = -1;
        private float modifiedMass = -1;

        private FloatCurve thrustCurveCache;

        private ModuleEnginesFX engineModule;

        private bool guiOpen = false;

        #endregion ENDREGION - Private working variables

        #region REGION - KSP GUI Interaction Methods
        
        public void onDiameterUpdated(BaseField field, object obj)
        {
            if (currentDiameter != prevDiameter)
            {
                updateDiameterFromEditor(currentDiameter, true);
            }
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }
        
        public void onNoseUpdated(BaseField field, object obj)
        {
            if (currentNoseName != prevNose)
            {
                updateNoseFromEditor(currentNoseName, true);
            }
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        public void onBodyUpdated(BaseField field, object obj)
        {
            if (currentMainName != prevBody)
            {
                updateMainModelFromEditor(currentMainName, true);
            }
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        public void onNozzleUpdated(BaseField field, object obj)
        {
            if (currentNozzleName != prevNozzle)
            {
                updateMountFromEditor(currentNozzleName, true);
            }
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        public void onGimbalUpdated(BaseField field, object obj)
        {
            if (currentGimbalOffset != prevGimbal)
            {
                updateGimbalOffsetFromEditor(currentGimbalOffset, true);
                prevGimbal = currentGimbalOffset;
            }
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        public void onNoseTextureUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentNoseTexture)
            {
                updateNoseTextureFromEditor(currentNoseTexture, true);
            }
        }

        public void onMainTextureUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentMainTexture)
            {
                updateMainTextureFromEditor(currentMainTexture, true);
            }
        }

        public void onNozzleTextureUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentNozzleTexture)
            {
                updateNozzleTextureFromEditor(currentNozzleTexture, true);
            }
        }

        [KSPEvent(guiName = "Adjust Thrust Curve", guiActiveEditor = true, guiActive = false)]
        public void editThrustCurveEvent()
        {
            guiOpen = true;
            EditorLogic editor = EditorLogic.fetch;
            if (editor != null) { editor.Lock(true, true, true, "SSTUThrustCurveEditorLock"); }
            ThrustCurveEditorGUI.openGUI(this, thrustCurveCache);
        }

        public void closeGui(FloatCurve editorCurve, string preset)
        {         
            guiOpen = false;
            EditorLogic editor = EditorLogic.fetch;
            if (editor != null) { editor.Unlock("SSTUThrustCurveEditorLock"); }
            thrustCurveCache = editorCurve;
            presetCurveName = preset;
            updateThrustOutput();
            updateCurvePersistentData();
        }
        
        /// <summary>
        /// Updates the current model scales from user input in the editor
        /// </summary>
        /// <param name="newDiameter"></param>
        /// <param name="updateSymmetry"></param>
        private void updateDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {
            currentDiameter = newDiameter;
            updateModelScaleAndPosition();
            updateEffectsScale();
            updateContainerVolume();
            updatePartMass();
            updatePartCost();
            updateAttachnodes(true);
            SSTUAttachNodeUtils.updateSurfaceAttachedChildren(part, prevDiameter, currentDiameter);
            updateEditorValues();
            updateThrustOutput();
            updateGui();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularBooster>().updateDiameterFromEditor(newDiameter, false);
                }
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
                currentMainModule.setupModel(part.transform.FindRecursive(baseTransformName), ModelOrientation.CENTRAL);
                currentMainName = currentMainModule.name;
            }
            if (!currentMainModule.isValidTextureSet(currentMainTexture))
            {
                currentMainTexture = currentMainModule.getDefaultTextureSet();            
            }
            currentMainModule.enableTextureSet(currentMainTexture);
            currentMainModule.updateTextureUIControl(this, "currentMainTexture", currentMainTexture);
            updateModelScaleAndPosition();
            updateEffectsScale();
            updateContainerVolume();
            updatePartMass();
            updatePartCost();
            updateAttachnodes(true);
            updateEditorValues();
            updateThrustOutput();
            updateGui();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularBooster>().updateMainModelFromEditor(newModel, false);
                }
            }
        }

        /// <summary>
        /// Update the nose module from user input in the editor
        /// </summary>
        /// <param name="newNose"></param>
        /// <param name="updateSymmetry"></param>
        private void updateNoseFromEditor(String newNose, bool updateSymmetry)
        {
            SingleModelData mod = Array.Find(noseModules, m => m.name == newNose);
            if (mod != null && mod != currentNoseModule)
            {
                currentNoseModule.destroyCurrentModel();
                mod.setupModel(part.transform.FindRecursive(baseTransformName), ModelOrientation.TOP);
                currentNoseModule = mod;
                currentNoseName = currentNoseModule.name;
            }
            if (!currentNoseModule.isValidTextureSet(currentNoseTexture))
            {
                currentNoseTexture = currentNoseModule.getDefaultTextureSet();
            }
            currentNoseModule.enableTextureSet(currentNoseTexture);
            currentNoseModule.updateTextureUIControl(this, "currentNoseTexture", currentNoseTexture);
            updateModelScaleAndPosition();
            updateEffectsScale();
            updateContainerVolume();
            updatePartMass();
            updatePartCost();
            updateAttachnodes(true);
            updateEditorValues();
            updateGui();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularBooster>().updateNoseFromEditor(newNose, false);
                }
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
                currentNozzleModule.setupModel(part.transform.FindRecursive(baseTransformName), ModelOrientation.BOTTOM);
                currentNozzleName = currentNozzleModule.name;
                currentGimbalOffset = 0;                
            }
            if (!currentNozzleModule.isValidTextureSet(currentNozzleTexture))
            {
                currentNozzleTexture = currentNozzleModule.getDefaultTextureSet();
            }
            currentNozzleModule.enableTextureSet(currentNozzleTexture);
            currentNozzleModule.updateTextureUIControl(this, "currentNozzleTexture", currentNozzleTexture);
            updateModelScaleAndPosition();
            updateEffectsScale();
            updateContainerVolume();
            updatePartMass();
            updatePartCost();
            updateAttachnodes(true);
            currentNozzleModule.setupTransformDefaults(part.transform.FindRecursive(thrustTransformName), part.transform.FindRecursive(gimbalTransformName));
            updateGimbalOffset();
            updateEngineISP();
            updateEditorValues();
            updateThrustOutput();
            float val = currentNozzleModule.gimbalAdjustmentRange;
            this.updateUIFloatEditControl("currentGimbalOffset", -val, val, 2f, 1f, 0.1f, true, currentGimbalOffset);
            updateGui();

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularBooster>().updateMountFromEditor(newMount, false);
                }
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
            float max = techLimitMaxDiameter < maxDiameter ? techLimitMaxDiameter : maxDiameter;
            if (currentDiameter > max) { currentDiameter = max; }
            this.updateUIFloatEditControl("currentDiameter", minDiameter, max, diameterIncrement*2, diameterIncrement, diameterIncrement*0.05f, true, currentDiameter);
            this.updateUIFloatEditControl("currentGimbalOffset", -currentNozzleModule.gimbalAdjustmentRange, currentNozzleModule.gimbalAdjustmentRange, 2f, 1f, 0.1f, true, currentGimbalOffset);

            currentNoseModule.updateTextureUIControl(this, "currentNoseTexture", currentNoseTexture);
            currentMainModule.updateTextureUIControl(this, "currentMainTexture", currentMainTexture);
            currentNozzleModule.updateTextureUIControl(this, "currentNozzleTexture", currentNozzleTexture);

            string[] names = SSTUUtils.getNames(noseModules, m => m.name);
            this.updateUIChooseOptionControl("currentNoseName", names, names, true, currentNoseName);
            names = SSTUUtils.getNames(mainModules, m => m.name);
            this.updateUIChooseOptionControl("currentMainName", names, names, true, currentMainName);
            names = SSTUUtils.getNames(nozzleModules, m => m.name);
            this.updateUIChooseOptionControl("currentNozzleName", names, names, true, currentNozzleName);

            Fields["currentDiameter"].uiControlEditor.onFieldChanged = onDiameterUpdated;
            Fields["currentGimbalOffset"].uiControlEditor.onFieldChanged = onGimbalUpdated;
            Fields["currentNoseName"].uiControlEditor.onFieldChanged = onNoseUpdated;
            Fields["currentMainName"].uiControlEditor.onFieldChanged = onBodyUpdated;
            Fields["currentNozzleName"].uiControlEditor.onFieldChanged = onNozzleUpdated;

            Fields["currentNoseName"].guiActiveEditor = noseModules.Length>1;
            Fields["currentNozzleName"].guiActiveEditor = nozzleModules.Length > 1;
            Fields["currentMainName"].guiActiveEditor = mainModules.Length > 1;
            Fields["currentGimbalOffset"].guiActiveEditor = currentNozzleModule.gimbalAdjustmentRange > 0;
            Fields["currentDiameter"].guiActiveEditor = maxDiameter > minDiameter;

            Fields["currentNoseTexture"].uiControlEditor.onFieldChanged = onNoseTextureUpdated;
            Fields["currentMainTexture"].uiControlEditor.onFieldChanged = onMainTextureUpdated;
            Fields["currentNozzleTexture"].uiControlEditor.onFieldChanged = onNozzleTextureUpdated;

            SSTUModInterop.onPartGeometryUpdate(part, true);
            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorShipModified));
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            initialize();
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            updateCurvePersistentData();
        }

        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorShipModified));
        }

        public void Start()
        {            
            updateGimbalOffset();
            updateThrustOutput();
            updateGui();
            if (!initializedResources && HighLogic.LoadedSceneIsEditor)
            {
                initializedResources = true;
                updateContainerVolume();
            }
        }

        //IModuleCostModifier Override
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (modifiedCost < 0) { return 0; }
            return modifiedCost;
        }

        //IModuleMassModifier Override
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (modifiedMass < 0) { return 0; }
            return modifiedMass;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        public void onEditorShipModified(ShipConstruct ship)
        {
            updateEngineGuiStats();
        }

        public void OnGUI()
        {
            if (guiOpen)
            {
                ThrustCurveEditorGUI.updateGUI();
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
            updateEditorValues();
            updateModelScaleAndPosition();
            updateEffectsScale();
            updateAttachnodes(false);
            updateEngineISP();
            updateGimbalOffset();
            updatePartCost();
            updatePartMass();
            updateGui();
            updateTextureSets();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        /// <summary>
        /// Update the editor values for whole and partial increments based on the current setup parameters.
        /// Ensures that pressing the ++/-- buttom with a parital increment selected will carry that increment over or zero it out if is out of bounds.
        /// Also allows for non-whole increments to be used for min and max values for the adjusted parameters
        /// </summary>
        private void updateEditorValues()
        {
            prevDiameter = currentDiameter;
            prevNose = currentNoseName;
            prevBody = currentMainName;
            prevNozzle = currentNozzleName;
            prevGimbal = currentGimbalOffset;
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

            GameObject gimbalTransformGO = new GameObject(gimbalTransformName);
            gimbalTransformGO.transform.NestToParent(modelBase.transform);
            gimbalTransformGO.SetActive(true);
        }

        /// <summary>
        /// Loads the current configuration from the cached persistent config node data
        /// </summary>
        private void loadConfigNodeData()
        {
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);
            techLimitMaxDiameter = SSTUStockInterop.getTechLimit(techLimitSet);
            if (currentDiameter > techLimitMaxDiameter) { currentDiameter = techLimitMaxDiameter; }
            
            //load all main tank model datas
            mainModules = SRBModelData.parseSRBModels(node.GetNodes("MAINMODEL"));
            currentMainModule = Array.Find(mainModules, m => m.name == currentMainName);
            if (currentMainModule == null)
            {
                currentMainModule = mainModules[0];
                currentMainName = currentMainModule.name;
            }
            if (!currentMainModule.isValidTextureSet(currentMainTexture))
            {
                currentMainTexture = currentMainModule.getDefaultTextureSet();
            }

            //load nose modules from NOSE nodes
            ConfigNode[] noseNodes = node.GetNodes("NOSE");
            ConfigNode noseNode;
            int length = noseNodes.Length;
            List<SingleModelData> noseModulesTemp = new List<SingleModelData>();
            for (int i = 0; i < length; i++)
            {
                noseNode = noseNodes[i];
                noseModulesTemp.Add(new SingleModelData(noseNode));
            }
            this.noseModules = noseModulesTemp.ToArray();
            currentNoseModule = Array.Find(this.noseModules, m => m.name == currentNoseName);
            if (currentNoseModule == null)
            {
                currentNoseModule = this.noseModules[0];//not having a mount defined is an error, at least one mount must be defined, crashing at this point is acceptable
                currentNoseName = currentNoseModule.name;
            }
            if (!currentNoseModule.isValidTextureSet(currentNoseTexture))
            {
                currentNoseTexture = currentNoseModule.getDefaultTextureSet();
            }

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
            if (!currentNozzleModule.isValidTextureSet(currentNozzleTexture))
            {
                currentNozzleTexture = currentNozzleModule.getDefaultTextureSet();
            }
            
            //reset existing gimbal/thrust transforms, remove them from the model hierarchy
            resetTransformParents();//this resets the thrust transform parent in case it was changed during prefab; we don't want to delete the thrust transform
            Transform parentTransform = part.transform.FindRecursive("model").FindOrCreate(baseTransformName);
            //finally, clear any existing models from prefab, and initialize the currently configured models
            SSTUUtils.destroyChildren(parentTransform);
            currentNoseModule.setupModel(parentTransform, ModelOrientation.TOP);
            currentNozzleModule.setupModel(parentTransform, ModelOrientation.BOTTOM);
            currentMainModule.setupModel(parentTransform, ModelOrientation.CENTRAL);
            //lastly, re-insert gimbal and thrust transforms into model hierarchy and reset default gimbal rotation offset
            currentNozzleModule.setupTransformDefaults(part.transform.FindRecursive(thrustTransformName), part.transform.FindRecursive(gimbalTransformName));

            //if had custom thrust curve data, reload it now (else it will default to whatever is on the engine)
            if (!string.IsNullOrEmpty(thrustCurveData))
            {
                thrustCurveCache = new FloatCurve();
                string[] keySplits = thrustCurveData.Split(':');
                string[] valSplits;
                int len = keySplits.Length;
                float key, value, inTan, outTan;
                for (int i = 0; i < len; i++)
                {
                    valSplits = keySplits[i].Split(',');                    
                    key = float.Parse(valSplits[0]);
                    value = float.Parse(valSplits[1]);
                    inTan = float.Parse(valSplits[2]);
                    outTan = float.Parse(valSplits[3]);
                    thrustCurveCache.Add(key, value, inTan, outTan);
                }
            }
            if (!string.IsNullOrEmpty(presetCurveName))
            {
                ConfigNode[] presetNodes = GameDatabase.Instance.GetConfigNodes("SSTU_THRUSTCURVE");
                int len = presetNodes.Length;
                for (int i = 0; i < len; i++)
                {
                    if (presetNodes[i].GetStringValue("name") == presetCurveName)
                    {
                        thrustCurveCache = presetNodes[i].GetFloatCurve("curve");
                        break;
                    }
                }
            }
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

        #region REGION - Update Methods

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
            float scale = diameterForThrustScaling == -1 ? currentMainModule.currentDiameterScale : (currentDiameter / diameterForThrustScaling);
            scale = Mathf.Pow(scale, thrustScalePower);
            if (engineModule == null) { engineModule = part.GetComponent<ModuleEnginesFX>(); }
            if (engineModule != null)
            {
                float minThrust = scale * currentMainModule.minThrust;
                float maxThrust = scale * currentMainModule.maxThrust;
                if (thrustCurveCache == null) { thrustCurveCache = engineModule.thrustCurve; }
                SSTUStockInterop.updateEngineThrust(engineModule, minThrust, maxThrust);
                engineModule.thrustCurve = thrustCurveCache;
                engineModule.useThrustCurve = thrustCurveCache.Curve.length > 1;
                updateEngineGuiStats();
            }
        }

        private void updateEngineGuiStats()
        {
            if (engineModule != null)
            {
                string prop = engineModule.propellants[0].name;
                PartResource res = part.Resources[prop];
                double propMass = res.info.density * res.amount;
                guiThrust = engineModule.maxThrust * engineModule.thrustPercentage * 0.01f;
                float isp = 220f;
                float g = 9.81f;
                float flowRate = guiThrust / (g * isp);
                guiBurnTime = (float)(propMass / flowRate);
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
                MonoBehaviour.print("ERROR: Could not update gimbal, no module found");
            }
        }

        /// <summary>
        /// Update the persistent representation of the custom thrust curve for this engine
        /// and store it as a string so it will be serialized into the part/craft persistence file
        /// </summary>
        private void updateCurvePersistentData()
        {
            if (thrustCurveCache != null && string.IsNullOrEmpty(presetCurveName))
            {
                string data = "";
                int len = thrustCurveCache.Curve.length;
                Keyframe key;
                for (int i = 0; i < len; i++)
                {
                    key = thrustCurveCache.Curve.keys[i];
                    if (i > 0) { data = data + ":"; }
                    data = data + key.time + "," + key.value + "," + key.inTangent + "," + key.outTangent;
                }
                thrustCurveData = data;
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

        private void updateEffectsScale()
        {
            if (part.fxGroups == null)
            {
                return;
            }
            float diameterScale = currentMainModule.currentDiameterScale;
            foreach (FXGroup group in part.fxGroups)
            {
                if (group.fxEmitters == null)
                {
                    continue;
                }
                foreach (ParticleEmitter fx in group.fxEmitters)
                {
                    fx.transform.localScale = new Vector3(diameterScale, diameterScale, diameterScale);
                }
            }
            if (part.partInfo != null && part.partInfo.partConfig != null)
            {
                ConfigNode effectsNode = part.partInfo.partConfig.GetNode("EFFECTS");
                ConfigNode copiedEffectsNode = new ConfigNode("EFFECTS");
                effectsNode.CopyTo(copiedEffectsNode);
                foreach (ConfigNode innerNode1 in copiedEffectsNode.nodes)
                {
                    foreach (ConfigNode innerNode2 in innerNode1.nodes)
                    {
                        if (innerNode2.HasValue("localPosition"))//common for both stock effects and real-plume effects
                        {
                            Vector3 pos = innerNode2.GetVector3("localPosition");
                            pos *= diameterScale;
                            innerNode2.SetValue("localPosition", (pos.x+", "+pos.y+", "+pos.z), false);
                        }
                        if (innerNode2.HasValue("fixedScale"))//real-plumes scaling
                        {
                            float fixedScaleVal = innerNode2.GetFloatValue("fixedScale");
                            fixedScaleVal *= diameterScale;
                            innerNode2.SetValue("fixedScale", fixedScaleVal.ToString(), false);
                        }
                        else if (innerNode2.HasValue("emission"))//stock effects scaling
                        {
                            String[] emissionVals = innerNode2.GetValues("emission");
                            for (int i = 0; i < emissionVals.Length; i++)
                            {
                                String val = emissionVals[i];
                                String[] splitVals = val.Split(new char[] { ' ' });
                                String replacement = "";
                                int len = splitVals.Length;
                                for (int k = 0; k < len; k++)
                                {                                    
                                    if (k == 1)//the 'value' portion 
                                    {
                                        float emissionValue = SSTUUtils.safeParseFloat(splitVals[k]) * diameterScale;
                                        splitVals[k] = emissionValue.ToString();
                                    }
                                    replacement = replacement + splitVals[k];
                                    if (k < len - 1) { replacement = replacement + " "; }
                                }
                                emissionVals[i] = replacement;
                            }
                            innerNode2.RemoveValues("emission");
                            foreach (String replacementVal in emissionVals)
                            {
                                innerNode2.AddValue("emission", replacementVal);
                            }

                            if (innerNode2.HasValue("speed"))//scale speed along with emission
                            {
                                String[] speedBaseVals = innerNode2.GetValues("speed");
                                int len = speedBaseVals.Length;
                                for (int i = 0; i < len; i++)
                                {
                                    String replacement = "";
                                    String[] speedSplitVals = speedBaseVals[i].Split(new char[] { ' ' });
                                    for (int k = 0; k < speedSplitVals.Length; k++)
                                    {
                                        if (k == 1)
                                        {
                                            float speedVal = SSTUUtils.safeParseFloat(speedSplitVals[k]) * diameterScale;
                                            speedSplitVals[k] = speedVal.ToString();
                                        }
                                        replacement = replacement + speedSplitVals[k];
                                        if (k < len - 1) { replacement = replacement + " "; }
                                    }
                                    speedBaseVals[i] = replacement;
                                }
                                innerNode2.RemoveValues("speed");
                                foreach (String replacementVal in speedBaseVals)
                                {
                                    innerNode2.AddValue("speed", replacementVal);
                                }
                            }
                        }
                    }
                }
                part.Effects.OnLoad(copiedEffectsNode);
            }            
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
        private void updateContainerVolume()
        {
            SSTUModInterop.onPartFuelVolumeUpdate(part, currentMainModule.getModuleVolume() * 1000f);
        }

        /// <summary>
        /// Update the mass of the part (and real-fuels/MFT volume) based on the currently selected models and scales
        /// </summary>
        private void updatePartMass()
        {
            modifiedMass = currentMainModule.getModuleMass() + currentNozzleModule.getModuleMass() + currentNoseModule.getModuleMass();
        }

        private void updatePartCost()
        {
            modifiedCost = currentMainModule.getModuleCost() + currentNoseModule.getModuleCost() + currentNozzleModule.getModuleCost();
        }
        
        /// <summary>
        /// Updates GUI fields for diameter/height/etc
        /// </summary>
        private void updateGui()
        {
            guiHeight = currentMainModule.currentHeight;
            Fields["currentNoseTexture"].guiActiveEditor = currentNoseModule.modelDefinition.textureSets.Length > 1;
            Fields["currentMainTexture"].guiActiveEditor = currentMainModule.modelDefinition.textureSets.Length > 1;
            Fields["currentNozzleTexture"].guiActiveEditor = currentNozzleModule.modelDefinition.textureSets.Length > 1;
        }

        private void updateTextureSets()
        {
            currentNoseModule.enableTextureSet(currentNoseTexture);
            currentMainModule.enableTextureSet(currentMainTexture);
            currentNozzleModule.enableTextureSet(currentNozzleTexture);
        }

        #endregion ENDREGION - Update Methods
    }

    /// <summary>
    /// Data for the main segment models for the SRB
    /// </summary>
    public class SRBModelData : SingleModelData
    {
        public float minThrust;
        public float maxThrust;
        public String engineConfig;
        public SRBModelData(ConfigNode node) : base(node)
        {
            minThrust = node.GetFloatValue("minThrust");
            maxThrust = node.GetFloatValue("maxThrust");
            engineConfig = node.GetStringValue("engineConfig");            
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
    public class SRBNozzleData : SingleModelData
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
