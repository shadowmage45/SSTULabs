using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace SSTUTools
{    
    public class SSTUModularParachuteOld : SSTUPartModuleConfigEnabled, IMultipleDragCube
    {

        private enum ChuteDragCube
        {
            RETRACTED,
            DROGUESEMI,
            DROGUEFULL,
            MAINSEMI,
            MAINFULL
        }

        private Dictionary<ChuteDragCube, String> cubeToNameMap = new Dictionary<ChuteDragCube, String>();

        private static int TERRAIN_MASK = 1 << 15;

        /// <summary>
        /// Custom name for the full-retracted drag-cube
        /// </summary>
        [KSPField]
        public String retractedDragCubeName = ChuteDragCube.RETRACTED.ToString();

        /// <summary>
        /// Custom name for the drogue semi-deployed drag cube
        /// </summary>
        [KSPField]
        public String drogueSemiDragCubeName = ChuteDragCube.DROGUESEMI.ToString();

        /// <summary>
        /// Custom name for the drogue full deployed drag cube
        /// </summary>
        [KSPField]
        public String drogueFullDragCubeName = ChuteDragCube.DROGUEFULL.ToString();

        /// <summary>
        /// Custom name for the main semi-deployed drag cube
        /// </summary>
        [KSPField]
        public String mainSemiDragCubeName = ChuteDragCube.MAINSEMI.ToString();

        /// <summary>
        /// Custom name for the main full deployed drag cube
        /// </summary>
        [KSPField]
        public String mainFullDragCubeName = ChuteDragCube.MAINFULL.ToString();

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
            if (String.IsNullOrEmpty(part.buoyancyUseCubeNamed))
            {
                part.buoyancyUseCubeNamed = retractedDragCubeName;
            }

            StartCoroutine(delayedDragUpdate());
        }

        private IEnumerator delayedDragUpdate()
        {
            yield return new WaitForFixedUpdate();
            setChuteState(chuteState);
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
            printDragCubes();
        }

        private void printDragCubes()
        {
            if (part != null && part.DragCubes != null && part.DragCubes.Cubes != null)
            {
                foreach (DragCube cube in part.DragCubes.Cubes)
                {
                    print("Cube: "+cube.Name+ " weight: "+cube.Weight + " :: "+cube.SaveToString());
                }
            }
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
            cubeToNameMap.Clear();
            cubeToNameMap.Add(ChuteDragCube.RETRACTED, retractedDragCubeName);
            cubeToNameMap.Add(ChuteDragCube.DROGUESEMI, drogueSemiDragCubeName);
            cubeToNameMap.Add(ChuteDragCube.DROGUEFULL, drogueFullDragCubeName);
            cubeToNameMap.Add(ChuteDragCube.MAINSEMI, mainSemiDragCubeName);
            cubeToNameMap.Add(ChuteDragCube.MAINFULL, mainFullDragCubeName);
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

        #region drag cube handling
        
        private void updatePartDragCube(ChuteDragCube a, ChuteDragCube b, float progress)
        {
            foreach (ChuteDragCube c in Enum.GetValues(typeof(ChuteDragCube)))
            {
                if (c != a && c != b)//not one of the active cubes
                {
                    part.DragCubes.SetCubeWeight(cubeToNameMap[c], 0);
                }
            }
            bool chuteCutOrRetracted = false;
            if (a == b)// a and b are the same, all others deactivated already, so just set a to full
            {
                part.DragCubes.SetCubeWeight(cubeToNameMap[a], 1.0f);
                if (a == ChuteDragCube.RETRACTED) { chuteCutOrRetracted = true; }
            }
            else//lerp between A and B by _progress_
            {
                chuteCutOrRetracted = false;
                part.DragCubes.SetCubeWeight(cubeToNameMap[a], 1f - progress);
                part.DragCubes.SetCubeWeight(cubeToNameMap[b], progress);
            }
            part.DragCubes.SetOcclusionMultiplier(chuteCutOrRetracted ? 1.0f : 0f);
            part.bodyLiftMultiplier = chuteCutOrRetracted ? defaultBodyLiftMultiplier : 0f;
        }

        public string[] GetDragCubeNames()
        {
            return new string[] { retractedDragCubeName, drogueSemiDragCubeName, drogueFullDragCubeName, mainSemiDragCubeName, mainFullDragCubeName };
        }
        
        
        /// <summary>
        /// Used by drag cube system to render the pre-computed drag cubes.  Should update model into the position intended for the input drag cube name.  The name will be one of those returned by GetDragCubeNames
        /// </summary>
        /// <param name="name"></param>
        public void AssumeDragCubePosition(string name)
        {
            forceReloadConfig();//forces loadConfig(ConfigNode) to be called again, really the only hacky use for that method, need to find a proper setup for it
            if (!initialized)
            {
                initializeModels();
                initialized = true;
            }
            updateParachuteTargets(part.transform.up);
            switch (name)
            {
                case "RETRACTED":
                    {
                        enableChuteRenders(false, false);
                        updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 1.0f, 0, 0);
                        updateDeployAnimation(drogueChuteModules, ChuteAnimationState.RETRACTED, 1.0f, 0, 0);
                        break;
                    }
                case "DROGUESEMI":
                    {
                        enableChuteRenders(true, false);
                        updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 1.0f, 0, 0);
                        updateDeployAnimation(drogueChuteModules, ChuteAnimationState.SEMI_DEPLOYED, 1.0f, 0, 0);
                        break;
                    }
                case "DROGUEFULL":
                    {
                        enableChuteRenders(true, false);
                        updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 1.0f, 0, 0);
                        updateDeployAnimation(drogueChuteModules, ChuteAnimationState.FULL_DEPLOYED, 1.0f, 0, 0);
                        break;
                    }
                case "MAINSEMI":
                    {
                        enableChuteRenders(false, true);
                        updateDeployAnimation(mainChuteModules, ChuteAnimationState.SEMI_DEPLOYED, 1.0f, 0, 0);
                        updateDeployAnimation(drogueChuteModules, ChuteAnimationState.RETRACTED, 1.0f, 0, 0);
                        break;
                    }
                case "MAINFULL":
                    {
                        enableChuteRenders(false, true);
                        updateDeployAnimation(mainChuteModules, ChuteAnimationState.FULL_DEPLOYED, 1.0f, 0, 0);
                        updateDeployAnimation(drogueChuteModules, ChuteAnimationState.RETRACTED, 1.0f, 0, 0);
                        break;
                    }
            }
            //print("render bounds: "+SSTUUtils.getRendererBoundsRecursive(part.gameObject));
        }

        /// <summary>
        /// Return true if the drag cube should be constantly re-rendered by the drag-cube system.  This implementation returns false, as it uses pre-computed drag-cubes.
        /// </summary>
        /// <returns></returns>
        public bool UsesProceduralDragCubes()
        {
            return false;
        }

        #endregion

        #region update methods

        private void updateFlight()
        {
            updateParachuteStats();
            bool noAtmo = atmoDensity <= 0;
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
                    if (shouldAutoCutDrogue() || shouldAutoCutAtmoOrSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    }
                    else if (shouldDeployMains())
                    {
                        setChuteState(ChuteState.MAIN_DEPLOYING_SEMI);
                    }
                    else
                    {
                        deployTime -= TimeWarp.fixedDeltaTime;
                        if (deployTime <= 0)
                        {
                            deployTime = 0;
                            setChuteState(ChuteState.DROGUE_FULL_DEPLOYED);
                        }
                        else
                        {
                            updateParachuteTargets(-part.dragVectorDir);
                            float progress = 1f - deployTime / drogueSemiDeploySpeed;
                            updateDeployAnimation(drogueChuteModules, ChuteAnimationState.DEPLOYING_SEMI, progress);
                            updatePartDragCube(ChuteDragCube.RETRACTED, ChuteDragCube.DROGUESEMI, progress);
                        }
                    }
                    //status text set from setState
                    break;
                    
                case ChuteState.DROGUE_SEMI_DEPLOYED:
                    if (shouldAutoCutDrogue() || shouldAutoCutAtmoOrSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    }
                    else if (shouldDeployMains())
                    {
                        setChuteState(ChuteState.MAIN_DEPLOYING_SEMI);
                    }
                    else
                    {
                        updateParachuteTargets(-part.dragVectorDir);
                        updateDeployAnimation(drogueChuteModules, ChuteAnimationState.SEMI_DEPLOYED, 1f);
                    }
                    //status text set from setState
                    //drag cubes set from setState
                    break;
                    
                case ChuteState.DROGUE_DEPLOYING_FULL:
                    if (shouldAutoCutDrogue() || shouldAutoCutAtmoOrSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    }
                    else if (shouldDeployMains())
                    {
                        setChuteState(ChuteState.MAIN_DEPLOYING_SEMI);
                    }
                    else
                    {
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
                            updatePartDragCube(ChuteDragCube.DROGUESEMI, ChuteDragCube.DROGUEFULL, p);
                        }
                    }
                    //status text set from setState
                    break;
                    
                case ChuteState.DROGUE_FULL_DEPLOYED:
                    if (shouldAutoCutDrogue() || shouldAutoCutAtmoOrSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    } 
                    else if (shouldDeployMains())
                    {
                        setChuteState(ChuteState.MAIN_DEPLOYING_SEMI);
                    }
                    else
                    {
                        updateParachuteTargets(-part.dragVectorDir);
                        updateDeployAnimation(drogueChuteModules, ChuteAnimationState.FULL_DEPLOYED, 1f);
                    }
                    //status text set from setState
                    //drag cubes set from setState
                    break;
                    
                case ChuteState.MAIN_DEPLOYING_SEMI:
                    if (shouldAutoCutMain() || shouldAutoCutAtmoOrSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    }
                    else
                    {
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
                            updatePartDragCube(ChuteDragCube.RETRACTED, ChuteDragCube.MAINSEMI, 1f - deployTime / mainSemiDeploySpeed);
                        }
                    }
                    //status text set from setState
                    break;
                    
                case ChuteState.MAIN_SEMI_DEPLOYED:
                    if (shouldAutoCutMain() || shouldAutoCutAtmoOrSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    }
                    else
                    {                        
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
                            updatePartDragCube(ChuteDragCube.MAINSEMI, ChuteDragCube.MAINFULL, 1f - deployTime / mainFullDeploySpeed);
                        }
                    }
                    //status text set from setState
                    break;
                    
                case ChuteState.MAIN_FULL_DEPLOYED:
                    if (shouldAutoCutMain() || shouldAutoCutAtmoOrSpeed())
                    {
                        setChuteState(ChuteState.CUT);
                    }
                    else
                    {
                        updateParachuteTargets(-part.dragVectorDir);
                        updateDeployAnimation(mainChuteModules, ChuteAnimationState.FULL_DEPLOYED, 1f);
                    }
                    //status text set from setState
                    //drag cubes set from setState
                    break;
            }
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
                    updatePartDragCube(ChuteDragCube.RETRACTED, ChuteDragCube.RETRACTED, 1f);
                    break;

                case ChuteState.RETRACTED:
                    enableChuteRenders(false, false);
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0);
                    updatePartDragCube(ChuteDragCube.RETRACTED, ChuteDragCube.RETRACTED, 1f);
                    //status text set during update tick
                    break;

                case ChuteState.ARMED:
                    enableChuteRenders(false, false);
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0);
                    updatePartDragCube(ChuteDragCube.RETRACTED, ChuteDragCube.RETRACTED, 1f);
                    //status text set during update tick
                    break;

                case ChuteState.DROGUE_DEPLOYING_SEMI:
                    enableChuteRenders(true, false);
                    deployTime = drogueSemiDeploySpeed;
                    chuteStatusText = "Drogue Semi-Deploying";
                    updatePartDragCube(ChuteDragCube.RETRACTED, ChuteDragCube.DROGUESEMI, 0f);
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.DEPLOYING_SEMI, 0, 0, 0); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0);
                    break;

                case ChuteState.DROGUE_SEMI_DEPLOYED:
                    enableChuteRenders(true, false);
                    chuteStatusText = "Drogue Semi-Deployed";
                    updatePartDragCube(ChuteDragCube.RETRACTED, ChuteDragCube.DROGUESEMI, 1f);
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.SEMI_DEPLOYED, 1, wobbleMultiplier, lerpDegreePerSecond); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0);
                    break;

                case ChuteState.DROGUE_DEPLOYING_FULL:
                    enableChuteRenders(true, false);
                    deployTime = drogueFullDeploySpeed;
                    chuteStatusText = "Drogue Full-Deploying";
                    updatePartDragCube(ChuteDragCube.DROGUESEMI, ChuteDragCube.DROGUEFULL, 0f);
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.DEPLOYING_FULL, 0, wobbleMultiplier, lerpDegreePerSecond); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0);
                    break;

                case ChuteState.DROGUE_FULL_DEPLOYED:
                    enableChuteRenders(true, false);
                    chuteStatusText = "Drogue Full-Deployed";
                    updatePartDragCube(ChuteDragCube.DROGUESEMI, ChuteDragCube.DROGUEFULL, 1f);
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.FULL_DEPLOYED, 1, wobbleMultiplier, lerpDegreePerSecond); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.RETRACTED, 0, 0, 0);
                    break;

                case ChuteState.MAIN_DEPLOYING_SEMI:
                    enableChuteRenders(false, true);
                    deployTime = mainSemiDeploySpeed;
                    chuteStatusText = "Main Semi-Deploying";
                    updatePartDragCube(ChuteDragCube.RETRACTED, ChuteDragCube.MAINSEMI, 0f);
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.CUT, 0, 0, 0); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.DEPLOYING_SEMI, 0, 0, 0);
                    break;

                case ChuteState.MAIN_SEMI_DEPLOYED:
                    enableChuteRenders(false, true);
                    chuteStatusText = "Main Semi-Deployed";
                    updatePartDragCube(ChuteDragCube.RETRACTED, ChuteDragCube.MAINSEMI, 1f);
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.CUT, 0, 0, 0); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.SEMI_DEPLOYED, 1, wobbleMultiplier, lerpDegreePerSecond);
                    break;

                case ChuteState.MAIN_DEPLOYING_FULL:
                    enableChuteRenders(false, true);
                    deployTime = mainFullDeploySpeed;
                    chuteStatusText = "Main Full-Deploying";
                    updatePartDragCube(ChuteDragCube.MAINSEMI, ChuteDragCube.MAINFULL, 0f);
                    if (hasDrogueChute) { updateDeployAnimation(drogueChuteModules, ChuteAnimationState.CUT, 0, 0, 0); }
                    updateDeployAnimation(mainChuteModules, ChuteAnimationState.DEPLOYING_FULL, 0, wobbleMultiplier, lerpDegreePerSecond);
                    break;

                case ChuteState.MAIN_FULL_DEPLOYED:
                    enableChuteRenders(false, true);
                    chuteStatusText = "Main Full-Deployed";
                    updatePartDragCube(ChuteDragCube.MAINSEMI, ChuteDragCube.MAINFULL, 1f);
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
            if (!hasDrogueChute || isDrogueChuteDeployed() || chuteState==ChuteState.CUT)
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
            return atmoDensity >= drogueMinAtm*0.75f;
        }

        private bool canMainDeploy()
        {
            return atmoDensity >= mainMinAtm*0.75f;
        }

        private bool shouldAutoCutMain()
        {
            return dynamicPressure > mainMaxQ || externalTemp > mainMaxTemp;
        }

        private bool shouldAutoCutDrogue()
        {
            return dynamicPressure > drogueMaxQ || externalTemp > drogueMaxTemp;
        }

        private bool shouldAutoCutAtmoOrSpeed()
        {
            return atmoDensity <= 0 || vessel.LandedOrSplashed && squareVelocity < autoCutSpeed * autoCutSpeed;
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

        private Vector3 getDragForce()
        {
            Vector3 dragVector = part.dragVector;
                        
            float area = calcDragArea();
            float drag = area * (float)atmoDensity * (float)squareVelocity;

            return dragVector * drag;
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
            return Mathf.Lerp(min, max, progress);
        }

        #endregion        
    }
}
