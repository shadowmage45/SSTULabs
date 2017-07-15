using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUModularBooster : PartModule, IPartCostModifier, IPartMassModifier, IRecolorable
    {

        #region REGION - KSP Config Variables

        /// <summary>
        /// Models will be added to this transform
        /// </summary>
        [KSPField]
        public string baseTransformName = "SSTU-MSRB-BaseTransform";

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

        #endregion REGION - KSP Config Variables

        #region REGION - Persistent Variables
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Variant"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentVariantName = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Length"),
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

        [KSPField(isPersistant = true)]
        public string noseModuleData = string.Empty;

        [KSPField(isPersistant = true)]
        public string bodyModuleData = string.Empty;

        [KSPField(isPersistant = true)]
        public string mountModuleData = string.Empty;

        //do NOT adjust this through config, or you will mess up your resource updates in the editor; you have been warned
        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        [KSPField(isPersistant = true)]
        public string thrustCurveData;

        [KSPField(isPersistant = true)]
        public string presetCurveName;
        
        [Persistent]
        public string configNodeData = string.Empty;

        #endregion ENDREGION - Persistent variables

        #region REGION - GUI Display Variables

        [KSPField(guiName = "Thrust", guiActiveEditor = true)]
        public float guiThrust = 0f;

        [KSPField(guiName = "Burn Tme", guiActiveEditor = true)]
        public float guiBurnTime = 0f;

        #endregion

        #region REGION - Private working variables

        private bool initialized = false;

        private string[] variantNames;
        private ModelModule<SingleModelData, SSTUModularBooster> noseModule;
        private ModelModule<SRBModelData, SSTUModularBooster> bodyModule;
        private ModelModule<SRBNozzleData, SSTUModularBooster> mountModule;

        private float modifiedCost = -1;
        private float modifiedMass = -1;

        private FloatCurve thrustCurveCache;

        private ModuleEnginesFX engineModule;

        private bool guiOpen = false;

        private float baseRCSThrust = -1;

        #endregion ENDREGION - Private working variables

        #region REGION - KSP GUI Interaction Methods

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
            this.actionWithSymmetry(m =>
            {
                m.thrustCurveCache = editorCurve;
                m.presetCurveName = preset;
                m.updateThrustOutput();
                m.updateCurvePersistentData();
            });
        }

        #endregion ENDREGION - KSP GUI Interaction Methods

        #region REGION - Standard KSP Overrides

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();

            Fields[nameof(currentDiameter)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.currentDiameter = currentDiameter;
                    m.updateEditorStats(true);
                    m.updateThrustOutput();
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentGimbalOffset)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.currentGimbalOffset = Mathf.Clamp(currentGimbalOffset, -m.mountModule.model.gimbalAdjustmentRange, m.mountModule.model.gimbalAdjustmentRange);
                    m.mountModule.model.updateGimbalRotation(m.part.transform.forward, m.currentGimbalOffset);
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentNoseName)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                noseModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.updateEditorStats(true);
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentMainName)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                bodyModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.updateEditorStats(true);
                    m.updateThrustOutput();
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentNozzleName)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.resetTransformParents();
                });
                mountModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.updateEditorStats(true);
                    m.mountModule.model.setupTransformDefaults(m.part.transform.FindRecursive(m.thrustTransformName), m.part.transform.FindRecursive(m.gimbalTransformName));
                    m.updateGimbalOffset();
                    m.updateThrustOutput();
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentNoseTexture)].uiControlEditor.onFieldChanged = noseModule.textureSetSelected;
            Fields[nameof(currentMainTexture)].uiControlEditor.onFieldChanged = bodyModule.textureSetSelected;
            Fields[nameof(currentNozzleTexture)].uiControlEditor.onFieldChanged = mountModule.textureSetSelected;

            this.updateUIFloatEditControl(nameof(currentDiameter), minDiameter, maxDiameter, diameterIncrement * 2, diameterIncrement, diameterIncrement * 0.05f, true, currentDiameter);
            this.updateUIFloatEditControl(nameof(currentGimbalOffset), -mountModule.model.gimbalAdjustmentRange, mountModule.model.gimbalAdjustmentRange, 2f, 1f, 0.1f, true, currentGimbalOffset);
            Fields[nameof(currentGimbalOffset)].guiActiveEditor = mountModule.model.gimbalAdjustmentRange > 0;
            Fields[nameof(currentDiameter)].guiActiveEditor = maxDiameter > minDiameter;

            variantNames = getVariantNames();
            this.updateUIChooseOptionControl(nameof(currentVariantName), variantNames, variantNames, true, currentVariantName);
            Fields[nameof(currentVariantName)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                string newModel = getNewModel(currentVariantName, bodyModule.model.length);
                currentMainName = newModel;
                this.actionWithSymmetry(m =>
                {
                    m.currentMainName = currentMainName;
                    m.currentVariantName = currentVariantName;
                    m.bodyModule.updateSelections();
                });
                bodyModule.modelSelected(newModel);
                this.actionWithSymmetry(m =>
                {
                    m.updateEditorStats(true);
                    m.updateThrustOutput();
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            SSTUModInterop.onPartGeometryUpdate(part, true);
            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorShipModified));
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
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

        //IRecolorable interface methods
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
                return bodyModule.customColors;
            }
            else if (section == "Bottom")
            {
                return mountModule.customColors;
            }
            return bodyModule.customColors;
        }

        public void setSectionColors(string section, Color[] colors)
        {
            if (section == "Top")
            {
                noseModule.setSectionColors(colors);
            }
            else if (section == "Body")
            {
                bodyModule.setSectionColors(colors);
            }
            else if (section == "Bottom")
            {
                mountModule.setSectionColors(colors);
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
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
            {
                //init thrust transforms and/or other persistent models
                //these transforms need to be present on the part at all times or the stock modules will error out during loading
                //as such these transform are left on the model even when resetting all the other model-module data
                initiaizePrefab();
            }
            loadConfigNodeData();
            updateEditorStats(false);
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        private void updateEditorStats(bool userInput)
        {
            updateModelScaleAndPosition();
            updateEffectsScale();
            updateAttachnodes(userInput);
            updatePartCostAndMass();
            if (userInput)
            {
                updateContainerVolume();
            }
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
            //reset existing gimbal/thrust transforms, remove them from the model hierarchy so they do not get deleted when setting up models
            //this resets the thrust transform parent in case it was changed during prefab; we don't want to delete the thrust transform
            resetTransformParents();

            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);

            //load all main modules from MAINMODEL nodes
            bodyModule = new ModelModule<SRBModelData, SSTUModularBooster>(part, this, createRootTransform(baseTransformName + "Root"), ModelOrientation.CENTRAL, nameof(bodyModuleData), nameof(currentMainName), nameof(currentMainTexture));
            bodyModule.getSymmetryModule = m => m.bodyModule;
            bodyModule.getValidSelections = m => bodyModule.models.FindAll(s => s.variant == currentVariantName);
            bodyModule.setupModelList(SingleModelData.parseModels<SRBModelData>(node.GetNodes("MAINMODEL"), m => new SRBModelData(m)));
            bodyModule.setupModel();

            //load nose modules from NOSE nodes
            noseModule = new ModelModule<SingleModelData, SSTUModularBooster>(part, this, createRootTransform(baseTransformName + "Nose"), ModelOrientation.TOP, nameof(noseModuleData), nameof(currentNoseName), nameof(currentNoseTexture));
            noseModule.getSymmetryModule = m => m.noseModule;
            noseModule.setupModelList(SingleModelData.parseModels(node.GetNodes("NOSE")));
            noseModule.setupModel();

            //load nose modules from NOZZLE nodes
            mountModule = new ModelModule<SRBNozzleData, SSTUModularBooster>(part, this, createRootTransform(baseTransformName + "Mount"), ModelOrientation.BOTTOM, nameof(mountModuleData), nameof(currentNozzleName), nameof(currentNozzleTexture));
            mountModule.getSymmetryModule = m => m.mountModule;
            mountModule.setupModelList(SingleModelData.parseModels<SRBNozzleData>(node.GetNodes("NOZZLE"), m => new SRBNozzleData(m)));
            mountModule.setupModel();

            //lastly, re-insert gimbal and thrust transforms into model hierarchy and reset default gimbal rotation offset
            mountModule.model.setupTransformDefaults(part.transform.FindRecursive(thrustTransformName), part.transform.FindRecursive(gimbalTransformName));

            int len;
            //if had custom thrust curve data, reload it now (else it will default to whatever is on the engine)
            if (!string.IsNullOrEmpty(thrustCurveData))
            {
                thrustCurveCache = new FloatCurve();
                string[] keySplits = thrustCurveData.Split(':');
                string[] valSplits;
                len = keySplits.Length;
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
                len = presetNodes.Length;
                for (int i = 0; i < len; i++)
                {
                    if (presetNodes[i].GetStringValue("name") == presetCurveName)
                    {
                        thrustCurveCache = presetNodes[i].GetFloatCurve("curve");
                        break;
                    }
                }
            }
            List<string> variantNames = new List<string>();
            len = bodyModule.models.Count;
            for (int i = 0; i < len; i++)
            {
                variantNames.AddUnique(bodyModule.models[i].variant);
            }
            if (string.IsNullOrEmpty(currentVariantName) || !variantNames.Contains(currentVariantName))
            {
                currentVariantName = bodyModule.model.variant;
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
            noseModule.model.updateScaleForDiameter(currentDiameter);
            bodyModule.model.updateScaleForDiameter(currentDiameter);
            mountModule.model.updateScaleForDiameter(currentDiameter);

            noseModule.model.currentVerticalPosition = bodyModule.model.currentHeight * 0.5f;
            bodyModule.model.currentVerticalPosition = 0f;
            mountModule.model.currentVerticalPosition = -bodyModule.model.currentHeight * 0.5f;

            noseModule.model.updateModel();
            bodyModule.model.updateModel();
            mountModule.model.updateModel();
        }

        /// <summary>
        /// Update the engines min and max thrust values based on the currently selected main tank segment
        /// </summary>
        private void updateThrustOutput()
        {
            float scale = diameterForThrustScaling == -1 ? bodyModule.model.currentDiameterScale : (currentDiameter / diameterForThrustScaling);
            scale = Mathf.Pow(scale, thrustScalePower);
            if (engineModule == null) { engineModule = part.GetComponent<ModuleEnginesFX>(); }
            if (engineModule != null)
            {
                float minThrust = scale * bodyModule.model.minThrust;
                float maxThrust = scale * bodyModule.model.maxThrust;
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
                float delta = engineModule.maxThrust - engineModule.minThrust;
                float limiter = engineModule.thrustPercentage * 0.01f;
                guiThrust = engineModule.minThrust + delta * limiter;
                float limit = guiThrust / engineModule.maxThrust;
                guiBurnTime = (float)(propMass / engineModule.maxFuelFlow) / limit;
            }
        }

        /// <summary>
        /// Updates the current gimbal transform angle and the gimbal modules range values to the values for the current nozzle module
        /// </summary>
        private void updateGimbalOffset()
        {
            //update the transform orientation for the gimbal so that the moduleGimbal gets the correct 'defaultOrientation'
            mountModule.model.updateGimbalRotation(part.transform.forward, currentGimbalOffset);

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
                    gimbal.gimbalRange = mountModule.model.gimbalFlightRange;
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

        private void updateEffectsScale()
        {
            if (part.fxGroups == null)
            {
                return;
            }
            float diameterScale = bodyModule.model.currentDiameterScale;
            //foreach (FXGroup group in part.fxGroups)
            //{
            //    if (group.fxEmitters == null)
            //    {
            //        continue;
            //    }
            //    foreach (ParticleEmitter fx in group.fxEmitters)
            //    {
            //        fx.transform.localScale = new Vector3(diameterScale, diameterScale, diameterScale);
            //    }
            //}
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

        private void updateRCSModule()
        {
            ModuleRCS rcs = part.GetComponent<ModuleRCS>();
            if (rcs == null) { return; }
            if (baseRCSThrust < 0) { baseRCSThrust = rcs.thrusterPower; }

            float thrust = bodyModule.model.currentDiameterScale * baseRCSThrust;
            rcs.thrusterPower = thrust;
        }

        /// <summary>
        /// Update attach node positions and optionally update the parts attached to those nodes if userInput==true
        /// </summary>
        /// <param name="userInput"></param>
        private void updateAttachnodes(bool userInput)
        {
            noseModule.model.updateAttachNodes(part, new string[] { "top" }, userInput, ModelOrientation.TOP);
            mountModule.model.updateAttachNodes(part, new string[] { "bottom" }, userInput, ModelOrientation.BOTTOM);
            AttachNode surface = part.srfAttachNode;
            if (surface != null)
            {
                Vector3 pos = bodyModule.model.modelDefinition.surfaceNode.position * bodyModule.model.currentDiameterScale;
                Vector3 rot = bodyModule.model.modelDefinition.surfaceNode.orientation;
                SSTUAttachNodeUtils.updateAttachNodePosition(part, surface, pos, rot, userInput);
            }
        }

        /// <summary>
        /// Update the volume of resources that are available in the part, based on the currently selected models and scales
        /// </summary>
        private void updateContainerVolume()
        {
            SSTUModInterop.onPartFuelVolumeUpdate(part, bodyModule.model.getModuleVolume() * 1000f);
        }

        private void updatePartCostAndMass()
        {
            modifiedCost = bodyModule.moduleCost + noseModule.moduleCost + mountModule.moduleCost;
            modifiedMass = bodyModule.moduleMass + noseModule.moduleMass + mountModule.moduleMass;
        }

        private Transform createRootTransform(string name)
        {
            Transform tr = part.transform.FindRecursive(name);
            if (tr != null) { GameObject.DestroyImmediate(tr.gameObject); }
            tr = new GameObject(name).transform;
            tr.NestToParent(part.transform.FindRecursive("model"));
            return tr;
        }

        private string[] getVariantNames()
        {
            List<string> names = new List<string>();
            int len = bodyModule.models.Count;
            for (int i = 0; i < len; i++)
            {
                names.AddUnique(bodyModule.models[i].variant);
            }
            return names.ToArray();
        }

        private string getNewModel(string newVariant, string length)
        {
            string name = string.Empty;
            int len = bodyModule.models.Count;
            //attempt to find selection of 'newVariant' that has same 'length' as input
            for (int i = 0; i < len; i++)
            {
                if (bodyModule.models[i].variant == newVariant && bodyModule.models[i].length == length)
                {
                    name = bodyModule.models[i].name;
                    break;
                }
            }
            //fallback for if not found -- select first selection of 'newVariant' type
            if (string.IsNullOrEmpty(name))
            {
                for (int i = 0; i < len; i++)
                {
                    if (bodyModule.models[i].variant == newVariant)
                    {
                        name = bodyModule.models[i].name;
                        break;
                    }
                }
            }
            return name;
        }

        #endregion ENDREGION - Update Methods
    }

    /// <summary>
    /// Data for the main segment models for the SRB
    /// </summary>
    public class SRBModelData : SingleModelData
    {
        public string variant;
        public string length;
        public float minThrust;
        public float maxThrust;
        public String engineConfig;
        public SRBModelData(ConfigNode node) : base(node)
        {
            variant = node.GetStringValue("variant", "default");
            length = node.GetStringValue("length", "1x");
            minThrust = node.GetFloatValue("minThrust");
            maxThrust = node.GetFloatValue("maxThrust");
            engineConfig = node.GetStringValue("engineConfig");            
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
