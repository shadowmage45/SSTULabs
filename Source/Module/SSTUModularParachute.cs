using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace SSTUTools
{    
    public class SSTUModularParachute : SSTUPartModuleConfigEnabled
    {
        
        private static int TERRAIN_MASK = 1 << 15;
               
        /// <summary>
        /// How much random wobble should be applied to the parachtues?  This is an arbitrary scalar factor; 0=no wobble, +infinity = 100% random orientation; general usable range should be from 0-100
        /// </summary>
        [KSPField]
        public float wobbleMultiplier = 10f;

        /// <summary>
        /// How fast should the parachutes track changes in direction of drag?
        /// </summary>
        [KSPField]
        public float lerpDegreePerSecond = 45f;

        /// <summary>
        /// Below this speed (in m/s), the parachutes will auto-cut if the vessel is in a landed or splashed state.
        /// </summary>
        [KSPField]
        public float autoCutSpeed = 0.5f;

        [KSPField]
        public float dragCoef = 1.0f;
        
        /// <summary>
        /// The base transform name to be created; all parachute models and objects will be parented to this transform (so they are not scattered all over the "model" transform direct children)
        /// </summary>
        [KSPField]
        public String baseTransformName = "SSTUMPBaseTransform";

        [KSPField]
        public String targetTransformName = "SSTUMPTargetTransform";

        //mains config data

        /// <summary>
        /// The name of the mesh within the model that should be jettisoned when main parachutes are activated.  May be a separate mesh from the drogue mesh, and will stay attached until mains are activated.
        /// </summary>
        [KSPField]
        public String mainCapName = "MainCap";

        /// <summary>
        /// The maximum temperature that the mains parachute may experience before burning up.  Beyond this temp and the parachute will be cut (burn up).  'Current' temp is calculated as a simple factor of surface area vs mach factor * exterior heat value
        /// </summary>
        [KSPField]
        public float mainMaxTemp = 800f;

        /// <summary>
        /// Max dynamic pressure for main chute safety
        /// </summary>
        [KSPField]
        public float mainMaxQ = 4000;

        /// <summary>
        /// Minimum atmospheric pressure for main chute deployment
        /// </summary>
        [KSPField]
        public float mainMinAtm = 0.25f;

        /// <summary>
        /// The scale factor to use for the mains model when in retracted state.  Rendering is disabled when in this state, so any 'small' value should be usable; when deployment starts, this scale will be set and rendering will be enabled.
        /// </summary>
        [KSPField]
        public Vector3 mainRetractedScale = new Vector3(0.005f, 0.005f, 0.005f);

        /// <summary>
        /// The scale factor to use for the mains model when in semi-deployed state
        /// </summary>
        [KSPField]
        public Vector3 mainSemiDeployedScale = new Vector3(0.2f, 1.0f, 0.2f);

        /// <summary>
        /// The number of seconds it takes the mains to go from retracted to semi-deployed state
        /// </summary>
        [KSPField]
        public float mainSemiDeploySpeed = 2f;
        
        /// <summary>
        /// The scale of the model to use when the mains are at full deployment
        /// </summary>
        [KSPField]
        public Vector3 mainFullDeployedScale = new Vector3(1.0f, 1.0f, 1.0f);

        /// <summary>
        /// The number of seconds it takes the mains to go from semi to full deployed
        /// </summary>
        [KSPField]
        public float mainFullDeploySpeed = 2f;

        [KSPField]
        public float drogueSemiDeployArea = 5f;

        [KSPField]
        public float drogueFullDeployArea = 5f;

        [KSPField]
        public float mainSemiDeployArea = 5f;

        [KSPField]
        public float mainFullDeployArea = 5f;

        //drogues config data

        [KSPField]
        public String drogueCapName = "DrogueCap";

        [KSPField]
        public float drogueMaxTemp = 1800f;

        [KSPField]
        public float drogueMinAtm = 0.005f;

        [KSPField]
        public float drogueMaxQ = 24000;
        
        [KSPField]
        public Vector3 drogueRetractedScale = new Vector3(0.005f, 0.005f, 0.005f);

        [KSPField]
        public Vector3 drogueSemiDeployedScale = new Vector3(0.2f, 1.0f, 0.2f);

        [KSPField]
        public float drogueSemiDeploySpeed = 2f;
        
        [KSPField]
        public Vector3 drogueFullDeployedScale = new Vector3(1.0f, 1.0f, 1.0f);        

        [KSPField]
        public float drogueFullDeploySpeed = 2f;

        [KSPField]
        public float defaultBodyLiftMultiplier = 1f;


        /// <summary>
        /// Used to track the ChuteState variable for the main chutes; restored during OnLoad
        /// </summary>
        [KSPField(isPersistant = true)]
        public String chutePersistence = ChuteState.RETRACTED.ToString();

        /// <summary>
        /// Used to track if the mains jettison cap has already been jettisoned (to prevent re-jettisoning it on part-reload if it was previously removed)
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool hasJettisonedMainCap = false;

        /// <summary>
        /// Used to track if the d jettison cap has already been jettisoned (to prevent re-jettisoning it on part-reload if it was previously removed)
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool hasJettisonedDrogueCap = false;

        #region private working variables
        
        private ChuteState chuteState = ChuteState.RETRACTED;
                
        private ParachuteModelData[] mainChuteModules;
        private ParachuteModelData[] drogueChuteModules;
        private Vector3 mainAttachPos = Vector3.zero;
        private Vector3 drogueAttachPos = Vector3.zero;

        private Transform targetTransform;

        private float deployTime = 0f;
        private bool hasDrogueChute = false;
        private bool initialized = false;
        private bool configLoaded = false;

        private double atmoDensity;
        private double squareVelocity;
        private double dynamicPressure;
        private double externalTemp;

        #endregion

        #region GUI fields, events, actions
        
        [KSPField(isPersistant = true, guiName = "Drogue Deploy Alt", guiActive = true, guiActiveEditor =true ), UI_FloatRange(minValue = 2000f, stepIncrement = 100f, maxValue = 12000f)]
        public float drogueSafetyAlt = 7500f;

        [KSPField(isPersistant =true, guiName ="Main Deploy Alt", guiActive = true, guiActiveEditor = true), UI_FloatRange(minValue = 500f, stepIncrement = 100f, maxValue = 5500f)]
        public float mainSafetyAlt = 1200f;

        [KSPField(guiName = "Chute", guiActive = true, guiActiveEditor = false)]
        public String chuteStatusText = "Unknown";

        [KSPAction("Deploy Chute")]
        public void deployChuteAction(KSPActionParam param)
        {
            deployChuteEvent();
        }

        [KSPAction("Cut Chute")]
        public void cutChuteAction(KSPActionParam param)
        {
            cutChuteEvent();
        }

        [KSPEvent(guiName = "Deploy Chute", guiActive = true, guiActiveEditor = false)]
        public void deployChuteEvent()
        {
            if (chuteState == ChuteState.RETRACTED)
            {
                setChuteState(ChuteState.ARMED);
                //TODO print output message to screen
            }            
        }

        [KSPEvent(guiName = "Cut Chute", guiActive = true, guiActiveEditor = false)]
        public void cutChuteEvent()
        {
            if (isDrogueChuteDeployed() || isMainChuteDeployed())
            {
                setChuteState(ChuteState.CUT);
                //TODO print output message to screen
            }
        }

        #endregion

        #region standard KSP overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            chuteState = (ChuteState)Enum.Parse(typeof(ChuteState), chutePersistence);
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
            if (!hasDrogueChute)
            {
                BaseField f = Fields["drogueSafetyAlt"];
                f.guiActive = f.guiActiveEditor = false;
            }            
        }

        public override void OnActive()
        {
            base.OnActive();
            deployChuteEvent();
        }

        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                updateFlight();
            }
        }        

        #endregion

        #region initialization methods

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;            
            initializeModels();
            setChuteState(chuteState);
        }

        protected override void loadConfigData(ConfigNode node)
        {
            if (configLoaded) { return; }
            configLoaded = true;
            ConfigNode[] drogueNodes = node.GetNodes("DROGUECHUTE");
            ConfigNode[] mainNodes = node.GetNodes("MAINCHUTE");
            int len = drogueNodes.Length;
            drogueChuteModules = new ParachuteModelData[len];
            for (int i = 0; i < len; i++){ drogueChuteModules[i] = new ParachuteModelData(drogueNodes[i], drogueRetractedScale, drogueSemiDeployedScale, drogueFullDeployedScale, i, false); }
            len = mainNodes.Length;
            mainChuteModules = new ParachuteModelData[len];
            for (int i = 0; i < len; i++){ mainChuteModules[i] = new ParachuteModelData(mainNodes[i], mainRetractedScale, mainSemiDeployedScale, mainFullDeployedScale, i, true); }

            mainAttachPos = Vector3.zero;
            foreach(ParachuteModelData pmd in mainChuteModules) { mainAttachPos += pmd.localPosition; }
            mainAttachPos.z = -mainAttachPos.z;//TODO -- fix inverted Z on these things...
            drogueAttachPos = Vector3.zero;
            foreach (ParachuteModelData pmd in drogueChuteModules) { drogueAttachPos += pmd.localPosition; }
            drogueAttachPos.z = -drogueAttachPos.z;//TODO -- fix inverted Z on these things...
        }
        
        /// <summary>
        /// create the models, and restore them to their saved/default state
        /// </summary>
        private void initializeModels()
        {
            Transform baseTransform = part.transform.FindRecursive("model").FindOrCreate(baseTransformName);
            baseTransform.NestToParent(part.transform.FindRecursive("model"));

            targetTransform = baseTransform.FindOrCreate(targetTransformName);
            targetTransform.NestToParent(baseTransform);
            targetTransform.rotation = Quaternion.LookRotation(part.transform.up, part.transform.forward);
                        
            foreach (ParachuteModelData droge in drogueChuteModules) { droge.setupModel(part, baseTransform, targetTransform); }
            foreach (ParachuteModelData main in mainChuteModules) { main.setupModel(part, baseTransform, targetTransform); }
            hasDrogueChute = drogueChuteModules.Length > 0;
        }

        #endregion

        #region update methods

        private void updateFlight()
        {
            updateParachuteStats();
            bool noAtmo = atmoDensity <= 0;
            bool applyDrag = false;
            switch (chuteState)
            {
                case ChuteState.CUT:
                    //NOOP
                    //status text set from setState
                    break;

                case ChuteState.RETRACTED:
                    chuteStatusText = "Packed-";
                    if (noAtmo) { chuteStatusText += "No Atmo"; }
                    else if (hasDrogueChute){chuteStatusText += shouldAutoCutDrogue() ? "Unsafe" : "Safe";}
                    else { chuteStatusText += shouldAutoCutMain() ? "Unsafe" : "Safe"; }
                    break;
                
                case ChuteState.ARMED:
                    if (shouldDeployMains())
                    {
                        setChuteState(ChuteState.MAIN_DEPLOYING_SEMI);
                    }
                    else if (shouldDeployDrogue())
                    {
                        setChuteState(ChuteState.DROGUE_DEPLOYING_SEMI);
                    }
                    else
                    {
                        chuteStatusText = "Armed-";
                        if (noAtmo) { chuteStatusText += "No Atmo"; }
                        else { chuteStatusText += "Unsafe"; }
                    }                    
                    break;
                    
                case ChuteState.DROGUE_DEPLOYING_SEMI:
                    applyDrag = true;
                    if (shouldAutoCutDrogue() || shouldAutoCutLandedSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    }
                    else if (shouldDeployMains())
                    {
                        applyDrag = true;
                        setChuteState(ChuteState.MAIN_DEPLOYING_SEMI);
                    }
                    else
                    {
                        applyDrag = true;
                        deployTime -= TimeWarp.fixedDeltaTime;
                        if (deployTime <= 0)
                        {
                            deployTime = 0;
                            setChuteState(ChuteState.DROGUE_SEMI_DEPLOYED);
                        }
                        else
                        {
                            updateParachuteTargets(-part.dragVectorDir);
                            float progress = 1f - deployTime / drogueSemiDeploySpeed;
                            updateDeployAnimation(drogueChuteModules, ChuteAnimationState.DEPLOYING_SEMI, progress);
                        }
                    }
                    //status text set from setState
                    break;
                    
                case ChuteState.DROGUE_SEMI_DEPLOYED:
                    if (shouldAutoCutDrogue() || shouldAutoCutLandedSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    }
                    else if (shouldDeployMains())
                    {
                        applyDrag = true;
                        setChuteState(ChuteState.MAIN_DEPLOYING_SEMI);
                    }
                    else if (FlightGlobals.getAltitudeAtPos(part.transform.position) < drogueSafetyAlt * 0.75f)
                    {
                        //transition to drogue-full-deploying
                        applyDrag = true;
                        setChuteState(ChuteState.DROGUE_DEPLOYING_FULL);
                    }
                    else
                    {
                        applyDrag = true;
                        updateParachuteTargets(-part.dragVectorDir);
                        updateDeployAnimation(drogueChuteModules, ChuteAnimationState.SEMI_DEPLOYED, 1f);
                    }
                    //status text set from setState
                    //drag cubes set from setState
                    break;
                    
                case ChuteState.DROGUE_DEPLOYING_FULL:
                    if (shouldAutoCutDrogue() || shouldAutoCutLandedSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    }
                    else if (shouldDeployMains())
                    {
                        applyDrag = true;
                        setChuteState(ChuteState.MAIN_DEPLOYING_SEMI);
                    }
                    else
                    {
                        applyDrag = true;
                        deployTime -= TimeWarp.fixedDeltaTime;
                        if (deployTime <= 0)
                        {
                            deployTime = 0;
                            setChuteState(ChuteState.DROGUE_FULL_DEPLOYED);
                        }
                        else
                        {
                            updateParachuteTargets(-part.dragVectorDir);
                            float p = 1f - deployTime / drogueFullDeploySpeed;
                            updateDeployAnimation(drogueChuteModules, ChuteAnimationState.DEPLOYING_FULL, p);
                        }
                    }
                    //status text set from setState
                    break;
                    
                case ChuteState.DROGUE_FULL_DEPLOYED:
                    applyPhysicsDrag();
                    if (shouldAutoCutDrogue() || shouldAutoCutLandedSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    } 
                    else if (shouldDeployMains())
                    {
                        applyDrag = true;
                        setChuteState(ChuteState.MAIN_DEPLOYING_SEMI);
                    }
                    else
                    {
                        applyDrag = true;
                        updateParachuteTargets(-part.dragVectorDir);
                        updateDeployAnimation(drogueChuteModules, ChuteAnimationState.FULL_DEPLOYED, 1f);
                    }
                    //status text set from setState
                    //drag cubes set from setState
                    break;
                    
                case ChuteState.MAIN_DEPLOYING_SEMI:
                    if (shouldAutoCutMain() || shouldAutoCutLandedSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    }
                    else
                    {
                        applyDrag = true;
                        deployTime -= TimeWarp.fixedDeltaTime;
                        if (deployTime <= 0)
                        {
                            deployTime = 0;
                            setChuteState(ChuteState.MAIN_SEMI_DEPLOYED);
                        }
                        else
                        {
                            updateParachuteTargets(-part.dragVectorDir);
                            updateDeployAnimation(mainChuteModules, ChuteAnimationState.DEPLOYING_SEMI, 1f - deployTime / mainSemiDeploySpeed);
                        }
                    }
                    //status text set from setState
                    break;
                    
                case ChuteState.MAIN_SEMI_DEPLOYED:
                    if (shouldAutoCutMain() || shouldAutoCutLandedSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    }
                    else
                    {
                        applyDrag = true;
                        //check for if should full-deploy based on main full deploy altitude * 0.75f (possibly config scalar in the future)
                        float tAlt = mainSafetyAlt * 0.75f;
                        if (shouldDeployRadarAlt(tAlt))
                        {
                            setChuteState(ChuteState.MAIN_DEPLOYING_FULL);
                        }
                        else if (FlightGlobals.getAltitudeAtPos(part.transform.position) < tAlt)
                        {
                            setChuteState(ChuteState.MAIN_DEPLOYING_FULL);
                        }
                        else
                        {
                            updateParachuteTargets(-part.dragVectorDir);
                            updateDeployAnimation(mainChuteModules, ChuteAnimationState.SEMI_DEPLOYED, 1f);
                        }
                    }
                    //status text set from setState
                    //drag cubes set from setState
                    break;
                    
                case ChuteState.MAIN_DEPLOYING_FULL:
                    if (shouldAutoCutMain())
                    {
                        setChuteState(ChuteState.CUT);
                    }
                    else
                    {
                        applyDrag = true;
                        deployTime -= TimeWarp.fixedDeltaTime;
                        if (deployTime <= 0)
                        {
                            deployTime = 0;
                            setChuteState(ChuteState.MAIN_FULL_DEPLOYED);
                        }
                        else
                        {
                            updateParachuteTargets(-part.dragVectorDir);
                            updateDeployAnimation(mainChuteModules, ChuteAnimationState.DEPLOYING_FULL, 1f - deployTime / mainFullDeploySpeed);
                        }
                    }
                    //status text set from setState
                    break;
                    
                case ChuteState.MAIN_FULL_DEPLOYED:
                    if (shouldAutoCutMain() || shouldAutoCutLandedSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    }
                    else
                    {
                        applyDrag = true;
                        updateParachuteTargets(-part.dragVectorDir);
                        updateDeployAnimation(mainChuteModules, ChuteAnimationState.FULL_DEPLOYED, 1f);
                    }
                    //status text set from setState
                    //drag cubes set from setState
                    break;
            }
            if (applyDrag) { applyPhysicsDrag(); }
        }
        
        private void setChuteState(ChuteState newState)
        {
            print("setting to chute state: " + newState);
            chuteState = newState;
            this.chutePersistence = chuteState.ToString();
            
            switch (chuteState)
            {
                case ChuteState.CUT:
                    enableChuteRenders(false, false);
                    chuteStatusText = "Cut";
                    break;

                case ChuteState.RETRACTED:
                    enableChuteRenders(false, false);
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0);
                    //status text set during update tick
                    break;

                case ChuteState.ARMED:
                    enableChuteRenders(false, false);
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0);
                    //status text set during update tick
                    break;

                case ChuteState.DROGUE_DEPLOYING_SEMI:
                    enableChuteRenders(true, false);
                    deployTime = drogueSemiDeploySpeed;
                    chuteStatusText = "Drogue Semi-Deploying";
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.DEPLOYING_SEMI, 0, 0, 0); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0);
                    break;

                case ChuteState.DROGUE_SEMI_DEPLOYED:
                    enableChuteRenders(true, false);
                    chuteStatusText = "Drogue Semi-Deployed";
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.SEMI_DEPLOYED, 1, wobbleMultiplier, lerpDegreePerSecond); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0);
                    break;

                case ChuteState.DROGUE_DEPLOYING_FULL:
                    enableChuteRenders(true, false);
                    deployTime = drogueFullDeploySpeed;
                    chuteStatusText = "Drogue Full-Deploying";
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.DEPLOYING_FULL, 0, wobbleMultiplier, lerpDegreePerSecond); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0);
                    break;

                case ChuteState.DROGUE_FULL_DEPLOYED:
                    enableChuteRenders(true, false);
                    chuteStatusText = "Drogue Full-Deployed";
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.FULL_DEPLOYED, 1, wobbleMultiplier, lerpDegreePerSecond); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0);
                    break;

                case ChuteState.MAIN_DEPLOYING_SEMI:
                    enableChuteRenders(false, true);
                    deployTime = mainSemiDeploySpeed;
                    chuteStatusText = "Main Semi-Deploying";
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.CUT, 0, 0, 0); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.DEPLOYING_SEMI, 0, 0, 0);
                    break;

                case ChuteState.MAIN_SEMI_DEPLOYED:
                    enableChuteRenders(false, true);
                    chuteStatusText = "Main Semi-Deployed";
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.CUT, 0, 0, 0); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.SEMI_DEPLOYED, 1, wobbleMultiplier, lerpDegreePerSecond);
                    break;

                case ChuteState.MAIN_DEPLOYING_FULL:
                    enableChuteRenders(false, true);
                    deployTime = mainFullDeploySpeed;
                    chuteStatusText = "Main Full-Deploying";
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.CUT, 0, 0, 0); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.DEPLOYING_FULL, 0, wobbleMultiplier, lerpDegreePerSecond);
                    break;

                case ChuteState.MAIN_FULL_DEPLOYED:
                    enableChuteRenders(false, true);
                    chuteStatusText = "Main Full-Deployed";
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.CUT, 0, 0, 0); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.FULL_DEPLOYED, 1, wobbleMultiplier, lerpDegreePerSecond);
                    break;
            }
            
            if (isMainChuteDeployed())
            {
                removeParachuteCap(mainCapName, HighLogic.LoadedSceneIsFlight && !hasJettisonedMainCap);
                removeParachuteCap(drogueCapName, HighLogic.LoadedSceneIsFlight && !hasJettisonedDrogueCap);
                hasJettisonedMainCap = true;
                hasJettisonedDrogueCap = true;
            }
            else if (isDrogueChuteDeployed())
            {
                removeParachuteCap(drogueCapName, HighLogic.LoadedSceneIsFlight && !hasJettisonedDrogueCap);
                hasJettisonedDrogueCap = true;
            }
            updateGuiState();            
        }

        private void updateDeployAnimation(ParachuteModelData[] modules, ChuteAnimationState state, float progress)
        {
            updateDeployAnimation(modules, state, progress, wobbleMultiplier, lerpDegreePerSecond);
        }

        private void updateDeployAnimation(ParachuteModelData[] modules, ChuteAnimationState state, float progress, float randomization, float lerpDps)
        {
            foreach (ParachuteModelData data in modules) { data.updateDeployAnimation(state, progress, randomization, lerpDps); }
        }

        private void updateParachuteTargets(Vector3 wind)
        {
            Vector3 windLocal = part.transform.InverseTransformDirection(wind);
            Vector3 scale = new Vector3(1, 1, 1);
            if (windLocal.y <= 0) { scale.x = -scale.x; }
            targetTransform.rotation = Quaternion.LookRotation(wind, part.transform.forward);
            targetTransform.localScale = scale;
        }

        private void updateGuiState()
        {
            bool deploy = hasDrogueChute ? isDrogueReady() : isMainReady();
            bool cut = isDrogueChuteDeployed() || isMainChuteDeployed();
            Events["deployChuteEvent"].guiActive = deploy;
            Events["cutChuteEvent"].guiActive = cut;
            if (isMainChuteDeployed() || chuteState==ChuteState.CUT)
            {
                BaseField f = Fields["mainSafetyAlt"];
                f.guiActive = f.guiActiveEditor = false;
            }
            if (!hasDrogueChute || isDrogueChuteDeployed() || isMainChuteDeployed() || chuteState==ChuteState.CUT)
            {
                BaseField f = Fields["drogueSafetyAlt"];
                f.guiActive = f.guiActiveEditor = false;
            }
        }

        /// <summary>
        /// Updates internal cached vars for external physical state -- velocity, dynamic pressure, etc.
        /// </summary>
        private void updateParachuteStats()
        {
            if (part == null || part.rigidbody == null || vessel == null)
            {
                atmoDensity = 0.2;
                squareVelocity = 1;
                externalTemp = 350;
                dynamicPressure = atmoDensity * squareVelocity * 0.5d;
                return;
            }
            atmoDensity = part.atmDensity;
            squareVelocity = Krakensbane.GetFrameVelocity().sqrMagnitude + part.rigidbody.velocity.sqrMagnitude;
            externalTemp = vessel.externalTemperature;
            dynamicPressure = atmoDensity * squareVelocity * 0.5d;
            //print("dens: " + atmoDensity);
            //print("sqvel: " + squareVelocity);
            //print("extemp:" + externalTemp);
            //print("dynpres:" + dynamicPressure);
            //print("part.. bucn" + part.buoyancyUseCubeNamed);
            //print("part.. cob" + part.CenterOfBuoyancy);
            //print("part.. bus" + part.buoyancyUseSine);
            //print("part.. cod" + part.CenterOfDisplacement);
            //print("land: " + vessel.LandedOrSplashed);
            //print("alt: " + vessel.altitude);
            //print("alt2: " + vessel.terrainAltitude);
            //print("alt3: " + vessel.heightFromSurface);
            //print("alt4: " + vessel.heightFromTerrain);
        }

        private void removeParachuteCap(String name, bool jettison)
        {
            if (String.IsNullOrEmpty(name)) { return; }
            Transform tr = part.transform.FindRecursive(name);
            if (tr == null) { return; }
            if (jettison)
            {
                Vector3 force = (part.transform.up * 100) + (part.transform.forward * 10);
                GameObject jettisoned = SSTUUtils.createJettisonedObject(tr.gameObject, part.rigidbody.velocity, force, 0.15f);
                //umm..nothing left to do?
            }
            else
            {
                GameObject.Destroy(tr.gameObject);
            }
        }

        private void enableChuteRenders(bool drogue, bool main)
        {
            foreach (ParachuteModelData module in drogueChuteModules)
            {
                SSTUUtils.enableRenderRecursive(module.baseModel.transform, drogue);
            }
            foreach (ParachuteModelData module in mainChuteModules)
            {
                SSTUUtils.enableRenderRecursive(module.baseModel.transform, main);
            }
        }

        private bool isMainChuteDeployed()
        {
            return chuteState == ChuteState.MAIN_DEPLOYING_FULL || chuteState == ChuteState.MAIN_DEPLOYING_SEMI || chuteState == ChuteState.MAIN_FULL_DEPLOYED || chuteState == ChuteState.MAIN_SEMI_DEPLOYED;
        }

        private bool isDrogueChuteDeployed()
        {
            return chuteState == ChuteState.DROGUE_DEPLOYING_FULL || chuteState == ChuteState.DROGUE_DEPLOYING_SEMI || chuteState == ChuteState.DROGUE_FULL_DEPLOYED || chuteState == ChuteState.DROGUE_SEMI_DEPLOYED;
        }

        private bool isDrogueCut()
        {
            return hasDrogueChute && (chuteState == ChuteState.MAIN_DEPLOYING_FULL || chuteState == ChuteState.MAIN_DEPLOYING_SEMI || chuteState == ChuteState.MAIN_FULL_DEPLOYED || chuteState == ChuteState.MAIN_SEMI_DEPLOYED || chuteState == ChuteState.CUT);
        }

        private bool isDrogueReady()
        {
            return hasDrogueChute && chuteState == ChuteState.RETRACTED;
        }

        private bool isMainReady()
        {
            return !hasDrogueChute && chuteState == ChuteState.RETRACTED;
        }

        private bool canDrogueDeploy()
        {
            return atmoDensity >= drogueMinAtm;
        }

        private bool canMainDeploy()
        {
            return atmoDensity >= mainMinAtm;
        }

        private bool shouldAutoCutMain()
        {
            return dynamicPressure > mainMaxQ || externalTemp > mainMaxTemp || atmoDensity < mainMinAtm;
        }

        private bool shouldAutoCutDrogue()
        {
            return dynamicPressure > drogueMaxQ || externalTemp > drogueMaxTemp || atmoDensity < drogueMinAtm;
        }

        private bool shouldAutoCutLandedSpeed()
        {
            return vessel.LandedOrSplashed && squareVelocity < autoCutSpeed * autoCutSpeed;
        }

        private bool shouldDeployDrogue()
        {
            if (!hasDrogueChute) { return false; }
            if (shouldAutoCutDrogue()) { return false; }
            return shouldDeploySeaLevelAlt(drogueSafetyAlt);
        }

        private bool shouldDeployMains()
        {
            if (shouldAutoCutMain()) { return false;}
            if (shouldDeploySeaLevelAlt(mainSafetyAlt)) { return true; }
            return shouldDeployRadarAlt(mainSafetyAlt);
        }

        private bool shouldDeploy(float height, bool ray)
        {
            return ray ? shouldDeployRadarAlt(height) : shouldDeploySeaLevelAlt(height);
        }

        private bool shouldDeployRadarAlt(float height)
        {
            return Physics.Raycast(part.transform.position, -FlightGlobals.getUpAxis(part.transform.position), height, TERRAIN_MASK);
        }

        private bool shouldDeploySeaLevelAlt(float height)
        {
            return height >= FlightGlobals.getAltitudeAtPos(part.transform.position);
        }

        private void applyPhysicsDrag()
        {
            Vector3 dragForce = getDragForce();
            Vector3 dragPoint = getDragPoint();
            Vector3 dragDirLocal = part.dragVectorDirLocal;
            dragPoint = Vector3.Lerp(dragPoint, dragDirLocal, 0.5f);//lerp between these to stabalize the oscilations
            Vector3 dragPointWorld = part.transform.TransformPoint(dragPoint);
            part.rigidbody.AddForceAtPosition(dragForce, dragPointWorld, ForceMode.Force);
        }

        private Vector3 getDragForce()
        {
            Vector3 dragVector = -(part.dragVectorDir);            
            float area = calcDragArea();
            float dragForce = area * (float)dynamicPressure * dragCoef;//should be in kg-m2/s2, newtons;
            dragForce *= 0.001f;//convert N into kN for KSP (everything is based on tons/1000kg units)
            return dragVector * dragForce;
        }

        /// <summary>
        /// Returns part relative drag-point, drag will be applied at this point on the part (relative to model origin), in the direction of the global drag vector
        /// </summary>
        /// <returns></returns>
        private Vector3 getDragPoint()
        {
            Vector3 pos = Vector3.zero;
            if (isDrogueChuteDeployed())
            {
                pos = drogueAttachPos;
            }
            else if (isMainChuteDeployed())
            {
                pos = mainAttachPos;
            }
            return pos;
        }

        private float calcDragArea()
        {
            float min = 0, max = 0, progress = 0;
            switch (chuteState)
            {
                case ChuteState.RETRACTED:
                    break;
                case ChuteState.ARMED:
                    break;
                case ChuteState.DROGUE_DEPLOYING_SEMI:
                    min = 0;
                    max = drogueSemiDeployArea;
                    progress = deployTime / drogueSemiDeploySpeed;
                    break;
                case ChuteState.DROGUE_SEMI_DEPLOYED:
                    min = drogueSemiDeployArea;
                    max = drogueSemiDeployArea;
                    progress = 0;
                    break;
                case ChuteState.DROGUE_DEPLOYING_FULL:
                    min = drogueSemiDeployArea;
                    max = drogueFullDeployArea;
                    progress = deployTime / drogueFullDeploySpeed;
                    break;
                case ChuteState.DROGUE_FULL_DEPLOYED:
                    min = drogueFullDeployArea;
                    max = drogueFullDeployArea;
                    progress = 0;
                    break;
                case ChuteState.MAIN_DEPLOYING_SEMI:
                    min = 0;
                    max = mainSemiDeployArea;
                    progress = deployTime / mainSemiDeploySpeed;
                    break;
                case ChuteState.MAIN_SEMI_DEPLOYED:
                    min = mainSemiDeployArea;
                    max = mainSemiDeployArea;
                    progress = 0;
                    break;
                case ChuteState.MAIN_DEPLOYING_FULL:
                    min = mainSemiDeployArea;
                    max = mainFullDeployArea;
                    progress = deployTime / mainFullDeploySpeed;
                    break;
                case ChuteState.MAIN_FULL_DEPLOYED:
                    min = mainFullDeployArea;
                    max = mainFullDeployArea;
                    progress = 0;
                    break;
                case ChuteState.CUT:
                    break;
                default:
                    break;
            }
            return Mathf.Lerp(min, max, 1.0f - progress);
        }

        #endregion        
    }


    public class ParachuteModelData
    {
        private SSTUParachuteDefinition definition;
        public String name;
        public String modelName;
        public Quaternion defaultOrientation;
        public GameObject parachutePivot;
        public GameObject baseModel;//the base transform of the actual model
        public GameObject capModel;//the upper cap of the parachute
        public GameObject lineModel;//the lower line segment of the parachute
        public Vector3 localPosition;//the position of the base of the line in the model, at the models default origin (does CoMOffset effect this, for stock-added models?)
        public Vector3 retractedUpVector;
        public Vector3 semiDeployedUpVector;
        public Vector3 fullDeployedUpVector;
        public String texture = String.Empty;

        public bool debug = false;

        public Vector3 retractedScale = Vector3.zero;
        public Vector3 semiDeployedScale = new Vector3(0.2f, 1, 0.2f);
        public Vector3 fullDeployedScale = new Vector3(0.2f, 1, 0.2f);

        private Transform partTransform;
        private GameObject retractedTarget;
        private GameObject semiDeployedTarget;
        private GameObject fullDeployedTarget;
        private Quaternion prevWind;
        private int index;
        private bool main;

        public ParachuteModelData(ConfigNode node, Vector3 retracted, Vector3 semi, Vector3 full, int index, bool main)
        {
            name = node.GetStringValue("name");
            definition = SSTUParachuteDefinitions.getDefinition(name);
            modelName = definition.modelName;
            localPosition = node.GetVector3("localPosition");
            retractedUpVector = node.GetVector3("retractedUpVector");
            semiDeployedUpVector = node.GetVector3("semiDeployedUpVector");
            fullDeployedUpVector = node.GetVector3("fullDeployedUpVector");
            texture = node.GetStringValue("texture", texture);
            debug = node.GetBoolValue("debug");
            retractedScale = retracted;
            semiDeployedScale = semi;
            fullDeployedScale = full;
            this.index = index;
            this.main = main;
        }

        public void setupModel(Part part, Transform baseTransform, Transform targetRotator)
        {
            this.partTransform = part.transform;

            String suffix = (main ? "-main-" : "-drogue-") + index;

            retractedTarget = targetRotator.FindOrCreate("ParachuteRetractedTarget" + suffix).gameObject;
            retractedTarget.transform.NestToParent(targetRotator);
            retractedTarget.transform.localPosition = new Vector3(-retractedUpVector.x, -retractedUpVector.z, retractedUpVector.y);

            semiDeployedTarget = targetRotator.FindOrCreate("ParachuteSemiDeployTarget" + suffix).gameObject;
            semiDeployedTarget.transform.NestToParent(targetRotator);
            semiDeployedTarget.transform.localPosition = new Vector3(-semiDeployedUpVector.x, -semiDeployedUpVector.z, semiDeployedUpVector.y);

            fullDeployedTarget = targetRotator.FindOrCreate("ParachuteFullDeployTarget" + suffix).gameObject;
            fullDeployedTarget.transform.NestToParent(targetRotator);
            fullDeployedTarget.transform.localPosition = new Vector3(-fullDeployedUpVector.x, -fullDeployedUpVector.z, fullDeployedUpVector.y);

            parachutePivot = baseTransform.FindOrCreate("ParachuteSpacingPivot" + suffix).gameObject;
            parachutePivot.transform.NestToParent(baseTransform);
            parachutePivot.transform.localPosition = new Vector3(localPosition.x, localPosition.y, -localPosition.z);//TODO -- why does this need to be inverted?

            Transform modelTransform = parachutePivot.transform.FindRecursive(modelName);
            if (modelTransform == null)
            {
                baseModel = SSTUUtils.cloneModel(modelName);
                MonoBehaviour.print("cloning new parachute model...");
            }
            else
            {
                baseModel = modelTransform.gameObject;
                MonoBehaviour.print("re-using existing model...");
            }
            baseModel.transform.NestToParent(parachutePivot.transform);
            if (!String.IsNullOrEmpty(texture))
            {
                SSTUUtils.setMainTextureRecursive(baseModel.transform, GameDatabase.Instance.GetTexture(texture, false));
            }

            Transform tr = baseModel.transform.FindRecursive(definition.capName);
            if (tr == null) { MonoBehaviour.print("ERROR: Could not locate transform for cap name: " + definition.capName); }
            capModel = tr.gameObject;
            tr = baseModel.transform.FindRecursive(definition.lineName);
            if (tr == null) { MonoBehaviour.print("ERROR: Could not locate transform for line name: " + definition.lineName); }
            lineModel = tr.gameObject;

            Vector3 lookDir = (-(parachutePivot.transform.position - semiDeployedTarget.transform.position));
            lookDir.Normalize();
            parachutePivot.transform.rotation = Quaternion.LookRotation(lookDir, part.transform.forward);
            prevWind = parachutePivot.transform.rotation;
            baseModel.transform.localRotation = Quaternion.AngleAxis(-90, Vector3.left);
        }

        public void updateDeployAnimation(ChuteAnimationState state, float progress, float randomization, float lerpDPS)
        {
            Vector3 targetA = Vector3.zero, targetB = Vector3.zero, scaleA = Vector3.one, scaleB = Vector3.one;
            switch (state)
            {
                case ChuteAnimationState.CUT:
                case ChuteAnimationState.RETRACTED:
                    targetA = retractedTarget.transform.position;
                    targetB = retractedTarget.transform.position;
                    scaleA = retractedScale;
                    scaleB = retractedScale;
                    break;
                case ChuteAnimationState.DEPLOYING_SEMI:
                    targetA = retractedTarget.transform.position;
                    targetB = semiDeployedTarget.transform.position;
                    scaleA = retractedScale;
                    scaleB = semiDeployedScale;
                    break;
                case ChuteAnimationState.SEMI_DEPLOYED:
                    targetA = semiDeployedTarget.transform.position;
                    targetB = semiDeployedTarget.transform.position;
                    scaleA = semiDeployedScale;
                    scaleB = semiDeployedScale;
                    break;
                case ChuteAnimationState.DEPLOYING_FULL:
                    targetA = semiDeployedTarget.transform.position;
                    targetB = fullDeployedTarget.transform.position;
                    scaleA = semiDeployedScale;
                    scaleB = fullDeployedScale;
                    break;
                case ChuteAnimationState.FULL_DEPLOYED:
                    targetA = fullDeployedTarget.transform.position;
                    targetB = fullDeployedTarget.transform.position;
                    scaleA = fullDeployedScale;
                    scaleB = fullDeployedScale;
                    break;
            }
            Vector3 target = Vector3.Lerp(targetA, targetB, progress);
            Vector3 lookDir = (-(parachutePivot.transform.position - target));
            lookDir.Normalize();
            parachutePivot.transform.rotation = Quaternion.LookRotation(lookDir, partTransform.forward);
            if (randomization > 0)
            {
                float rand = Time.time + (GetHashCode() % 32);//should be different per parachute-cap instance
                float rx = (Mathf.PerlinNoise(rand, 0) - 0.5f) * randomization;
                float ry = (Mathf.PerlinNoise(rand, 4) - 0.5f) * randomization;
                float rz = (Mathf.PerlinNoise(rand, 8) - 0.5f) * randomization;
                parachutePivot.transform.Rotate(new Vector3(rx, ry, rz));
            }
            if (lerpDPS > 0)
            {
                parachutePivot.transform.rotation = Quaternion.RotateTowards(prevWind, parachutePivot.transform.rotation, lerpDPS * TimeWarp.fixedDeltaTime);
            }
            baseModel.transform.localScale = Vector3.Lerp(scaleA, scaleB, progress);
            prevWind = parachutePivot.transform.rotation;
        }

    }

    public enum ChuteState
    {
        RETRACTED,
        ARMED,
        DROGUE_DEPLOYING_SEMI,
        DROGUE_SEMI_DEPLOYED,
        DROGUE_DEPLOYING_FULL,
        DROGUE_FULL_DEPLOYED,
        MAIN_DEPLOYING_SEMI,
        MAIN_SEMI_DEPLOYED,
        MAIN_DEPLOYING_FULL,
        MAIN_FULL_DEPLOYED,
        CUT
    }

    public enum ChuteAnimationState
    {
        RETRACTED,
        DEPLOYING_SEMI,
        SEMI_DEPLOYED,
        DEPLOYING_FULL,
        FULL_DEPLOYED,
        CUT
    }

    public class SSTUParachuteDefinition
    {
        public String name;
        public String modelName;
        public String capName;
        public String lineName;
        public float capHeight;
        public float capDiameter;
        public float lineHeight;
        public SSTUParachuteDefinition(ConfigNode node)
        {
            name = node.GetStringValue("name");
            modelName = node.GetStringValue("modelName");
            capName = node.GetStringValue("capName");
            lineName = node.GetStringValue("lineName");
            capHeight = node.GetFloatValue("capHeight");
            capDiameter = node.GetFloatValue("capDiameter");
            lineHeight = node.GetFloatValue("lineHeight");
        }
    }

    public class SSTUParachuteDefinitions
    {
        private static Dictionary<String, SSTUParachuteDefinition> definitions = new Dictionary<string, SSTUParachuteDefinition>();
        private static bool loaded = false;
        private static void loadMap()
        {
            definitions.Clear();
            ConfigNode[] parNodes = GameDatabase.Instance.GetConfigNodes("SSTU_PARACHUTE");
            foreach (ConfigNode node in parNodes)
            {
                SSTUParachuteDefinition def = new SSTUParachuteDefinition(node);
                definitions.Add(def.name, def);
            }
            loaded = true;
        }
        public static SSTUParachuteDefinition getDefinition(String name)
        {
            if (!loaded) { loadMap(); }
            SSTUParachuteDefinition def = null;
            definitions.TryGetValue(name, out def);
            return def;
        }
    }

}
