using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{    
    public class SSTUModularParachute : PartModule, IMultipleDragCube
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

        [KSPField]
        public String retractedDragCubeName = ChuteDragCube.RETRACTED.ToString();

        [KSPField]
        public String drogueSemiDragCubeName = ChuteDragCube.DROGUESEMI.ToString();

        [KSPField]
        public String drogueFullDragCubeName = ChuteDragCube.DROGUEFULL.ToString();

        [KSPField]
        public String mainSemiDragCubeName = ChuteDragCube.MAINSEMI.ToString();

        [KSPField]
        public String mainFullDragCubeName = ChuteDragCube.MAINFULL.ToString();

        [KSPField]
        public float wobbleMultiplier = 10f;

        [KSPField]
        public float lerpDegreePerSecond = 45f;

        [KSPField]
        public float autoCutSpeed = 0.5f;

        [KSPField]
        public float mainSemiDeploySpeed = 2f;

        [KSPField]
        public float mainFullDeploySpeed = 2f;

        [KSPField]
        public float mainSemiMinAtm = 0.01f;

        [KSPField]
        public float mainSemiAutoAlt = 8000f;

        [KSPField]
        public float mainFullMinAtm = 0.2f;

        [KSPField]
        public float mainFullAutoAlt = 750f;

        [KSPField]
        public float mainMaxTemp = 800f;

        [KSPField]
        public float drogueSemiMinAtm = 0.001f;

        [KSPField]
        public float drogueSemiAutoAlt = 25000f;

        [KSPField]
        public float drogueFullMinAtm = 0.005f;

        [KSPField]
        public float drogueFullAutoAlt = 17000f;

        [KSPField]
        public float drogueMaxTemp = 1400f;

        [KSPField]
        public float drogueSemiDeploySpeed = 2f;

        [KSPField]
        public float drogueFullDeploySpeed = 2f;

        [KSPField]
        public String drogueCapName = "DrogueCap";

        [KSPField]
        public String mainCapName = "MainCap";

        [KSPField]
        public String baseTransformName = "SSTUModularParachuteBaseTransform";

        [KSPField]
        public String removableDrogueCapName = String.Empty;

        [KSPField]
        public String removableMainCapName = String.Empty;

        [KSPField]
        public Vector3 drogueRetractedScale = new Vector3(0.005f, 0.005f, 0.005f);

        [KSPField]
        public Vector3 drogueSemiDeployedScale = new Vector3(0.2f, 1.0f, 0.2f);

        [KSPField]
        public Vector3 drogueFullDeployedScale = new Vector3(1.0f, 1.0f, 1.0f);

        [KSPField]
        public Vector3 mainRetractedScale = new Vector3(0.005f, 0.005f, 0.005f);

        [KSPField]
        public Vector3 mainSemiDeployedScale = new Vector3(0.2f, 1.0f, 0.2f);

        [KSPField]
        public Vector3 mainFullDeployedScale = new Vector3(1.0f, 1.0f, 1.0f);        

        [KSPField(isPersistant = true)]
        public bool hasDrogueBeenDeployed = false;

        [KSPField(isPersistant = true)]
        public String drogueChutePersistence = ChuteState.RETRACTED.ToString();

        [KSPField(isPersistant = true)]
        public String mainChutePersistence = ChuteState.RETRACTED.ToString();

        [KSPField(isPersistant = true)]
        public bool hasJettisonedMainCap = false;
        [KSPField(isPersistant = true)]
        public bool hasJettisonedDrogueCap = false;

        #region private working variables

        [Persistent]
        public String configNodeData = String.Empty;

        private ChuteState mainChuteState = ChuteState.RETRACTED;
        private ChuteState drogueChuteState = ChuteState.RETRACTED;
                
        private ParachuteModelData[] mainChuteModules;
        private ParachuteModelData[] drogueChuteModules;

        private float deployTime = 0f;
        private bool hasDrogeChute = false;
        private bool initialized = false;
        private Transform baseTransform;
        private Transform rotatorTransform;

        #endregion

        #region GUI events and actions
        [KSPField(guiName = "Safe For Chutes", guiActive = true, guiActiveEditor = false)]
        public String safeToDeploy = "Unknown";

        [KSPEvent(guiName = "Deploy Chute", guiActive = true, guiActiveEditor = false)]
        public void deployChuteEvent()
        {            
            if (hasDrogeChute)
            {
                if (drogueChuteState == ChuteState.RETRACTED)
                {
                    setChuteStates(ChuteState.RETRACTED, ChuteState.DEPLOYING_SEMI);
                }                
            }
            else if(mainChuteState == ChuteState.RETRACTED)//has no drogue, main is retracted and ready to deploy
            {
                setChuteStates(ChuteState.DEPLOYING_SEMI, ChuteState.CUT);
            }
        }

        [KSPEvent(guiName = "Cut Chute", guiActive = true, guiActiveEditor = false)]
        public void cutChuteEvent()
        {
            if (drogueChuteState != ChuteState.RETRACTED && drogueChuteState != ChuteState.CUT)
            {
                setChuteStates(mainChuteState, ChuteState.CUT);
            }
            if (mainChuteState != ChuteState.RETRACTED && mainChuteState != ChuteState.CUT)
            {
                setChuteStates(ChuteState.CUT, drogueChuteState);
            }
        }

        #endregion

        #region standard KSP overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { configNodeData = node.ToString(); }
            mainChuteState = (ChuteState)Enum.Parse(typeof(ChuteState), mainChutePersistence);
            drogueChuteState = (ChuteState)Enum.Parse(typeof(ChuteState), drogueChutePersistence);
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
            GameEvents.onVesselGoOffRails.Add(new EventData<Vessel>.OnEvent(onUnpackEvent));
        }

        public void OnDestroy()
        {
            GameEvents.onVesselGoOffRails.Remove(new EventData<Vessel>.OnEvent(onUnpackEvent));
        }

        public void onUnpackEvent(Vessel v)
        {
            setChuteStates(mainChuteState, drogueChuteState);
        }

        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (mainChuteState != ChuteState.CUT && mainChuteState != ChuteState.RETRACTED)
                {
                    if (updateAutoCut(true))
                    {
                        setChuteStates(ChuteState.CUT, ChuteState.CUT);
                    }
                    else
                    {
                        updateParachuteTargets(-part.dragVectorDir);
                        if (deployTime > 0) { deployTime -= TimeWarp.fixedDeltaTime; }
                        if (deployTime < 0) { deployTime = 0; }
                        updateAnimationStatus(true);
                    }
                }
                else if (drogueChuteState != ChuteState.CUT && drogueChuteState != ChuteState.RETRACTED)
                {
                    if (updateAutoCut(false))
                    {                        
                        setChuteStates(ChuteState.DEPLOYING_SEMI, ChuteState.CUT);
                    }
                    else
                    {
                        updateParachuteTargets(-part.dragVectorDir);
                        if (deployTime > 0) { deployTime -= TimeWarp.fixedDeltaTime; }
                        if (deployTime < 0) { deployTime = 0; }
                        updateAnimationStatus(false);
                    }
                }
            }
        }

        #endregion

        #region initialization methods

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;            
            loadConfigData(SSTUNodeUtils.parseConfigNode(configNodeData));
            initializeModels();
            setChuteStates(mainChuteState, drogueChuteState);
            print("SSTUModularParachute initialized");
            print("hashcode: " + GetHashCode());
            printDragCubes();
        }

        private void printDragCubes()
        {
            if (part != null && part.DragCubes != null && part.DragCubes.Cubes != null)
            {
                foreach (DragCube cube in part.DragCubes.Cubes)
                {
                    print("Cube: "+cube.Name+ " weight: "+cube.Weight);
                }
            }
        }

        private void loadConfigData(ConfigNode node)
        {
            ConfigNode[] drogueNodes = node.GetNodes("DROGUECHUTE");
            ConfigNode[] mainNodes = node.GetNodes("MAINCHUTE");
            int len = drogueNodes.Length;
            drogueChuteModules = new ParachuteModelData[len];
            for (int i = 0; i < len; i++){ drogueChuteModules[i] = new ParachuteModelData(drogueNodes[i], drogueRetractedScale, drogueSemiDeployedScale, drogueFullDeployedScale); }
            len = mainNodes.Length;
            mainChuteModules = new ParachuteModelData[len];
            for (int i = 0; i < len; i++){ mainChuteModules[i] = new ParachuteModelData(mainNodes[i], mainRetractedScale, mainSemiDeployedScale, mainFullDeployedScale); }
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
            baseTransform = part.transform.FindOrCreate(baseTransformName);
            SSTUUtils.destroyChildren(baseTransform);
            GameObject targetRotator = new GameObject("ParachuteTargetRotator");
            rotatorTransform = targetRotator.transform;
            targetRotator.transform.NestToParent(baseTransform);
            targetRotator.transform.rotation = Quaternion.LookRotation(part.transform.up, part.transform.forward);
            foreach (ParachuteModelData droge in drogueChuteModules) { droge.setupModel(part, baseTransform, rotatorTransform); }
            foreach (ParachuteModelData main in mainChuteModules) { main.setupModel(part, baseTransform, rotatorTransform); }
            hasDrogeChute = drogueChuteModules.Length > 0;            
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
                    print("setting drag cube: " + c + " to: " + 0);
                }
            }
            if (a == b)// a and b are the same, all others deactivated already, so just set a to full
            {
                part.DragCubes.SetCubeWeight(cubeToNameMap[a], 1.0f);
                print("set single drag cube to full progress: " + a);
            }
            else//lerp between A and B by _progress_
            {
                part.DragCubes.SetCubeWeight(cubeToNameMap[a], 1f - progress);
                part.DragCubes.SetCubeWeight(cubeToNameMap[b], progress);
                print("setting drag cubes to: " + a + " : " + b + "  at progress: " + progress);
            }
        }

        public string[] GetDragCubeNames()
        {
            return new string[] { "RETRACTED","DROGUESEMI","DROGUEFULL","MAINSEMI", "MAINFULL" };
        }
        
        /// <summary>
        /// Used by drag cube system to render the pre-computed drag cubes.  Should update model into the position intended for the input drag cube name.  The name will be one of those returned by GetDragCubeNames
        /// </summary>
        /// <param name="name"></param>
        public void AssumeDragCubePosition(string name)
        {
            initialize();
            updateParachuteTargets(part.transform.up);
            switch (name)
            {
                case "RETRACTED":
                    {
                        updateDeployAnimation(mainChuteModules, ChuteState.RETRACTED, 1.0f, 0, 0);
                        updateDeployAnimation(drogueChuteModules, ChuteState.RETRACTED, 1.0f, 0, 0);
                        break;
                    }
                case "DROGUESEMI":
                    {
                        updateDeployAnimation(mainChuteModules, ChuteState.RETRACTED, 1.0f, 0, 0);
                        updateDeployAnimation(drogueChuteModules, ChuteState.SEMI_DEPLOYED, 1.0f, 0, 0);
                        break;
                    }
                case "DROGUEFULL":
                    {
                        updateDeployAnimation(mainChuteModules, ChuteState.RETRACTED, 1.0f, 0, 0);
                        updateDeployAnimation(drogueChuteModules, ChuteState.FULL_DEPLOYED, 1.0f, 0, 0);
                        break;
                    }
                case "MAINSEMI":
                    {
                        updateDeployAnimation(mainChuteModules, ChuteState.SEMI_DEPLOYED, 1.0f, 0, 0);
                        updateDeployAnimation(drogueChuteModules, ChuteState.RETRACTED, 1.0f, 0, 0);
                        break;
                    }
                case "MAINFULL":
                    {
                        updateDeployAnimation(mainChuteModules, ChuteState.FULL_DEPLOYED, 1.0f, 0, 0);
                        updateDeployAnimation(drogueChuteModules, ChuteState.RETRACTED, 1.0f, 0, 0);
                        break;
                    }
            }
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

        private bool updateAutoCut(bool main)
        {
            //TODO calc chute temp differently from base vessel temp, as a simple formula of area and speed (whichever that is), cut if exceeds max for that chute type
            if (part.atmDensity <= 0)
            {
                return true;
            }
            if ((Krakensbane.GetFrameVelocity() + part.rigidbody.velocity).sqrMagnitude < (autoCutSpeed * autoCutSpeed))
            {
                return vessel.LandedOrSplashed;
            }
            return false;
        }

        private void updateSafeToDeploy(bool main)
        {

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

        /// <summary>
        /// Updates the animation state and drag cubes for the modules current deployed state for whichever chute is input
        /// assumes state for other chute is set properly, and that the state was initiated properly through setState
        /// Will transition to next state if animation final state is met
        /// </summary>
        private void updateAnimationStatus(bool main)
        {
            ChuteDragCube dragCubeA = ChuteDragCube.RETRACTED, dragCubeB = ChuteDragCube.RETRACTED;
            bool updateDragCube = false;
            bool updateAnim = false;
            float progress = 0;
            ChuteState state = main ? mainChuteState : drogueChuteState;
            switch (state)
            {
                case ChuteState.DEPLOYING_SEMI:
                    {
                        progress = 1f - deployTime / (main? mainSemiDeploySpeed : drogueSemiDeploySpeed);
                        if (deployTime <= 0)
                        {
                            if (main) { setChuteStates(ChuteState.SEMI_DEPLOYED, drogueChuteState); }
                            else { setChuteStates(mainChuteState, ChuteState.SEMI_DEPLOYED); }
                            print("toggling to next state from semi deploying");
                            updateDragCube = false;
                            updateAnim = false;
                        }
                        else
                        {
                            dragCubeA = ChuteDragCube.RETRACTED;
                            dragCubeB = main ? ChuteDragCube.MAINSEMI : ChuteDragCube.DROGUESEMI;
                            updateDragCube = true;
                            updateAnim = true;
                        }
                        break;
                    }
                case ChuteState.DEPLOYING_FULL:
                    {
                        progress = 1f - deployTime / (main? mainFullDeploySpeed : drogueFullDeploySpeed);
                        if (deployTime <= 0)
                        {
                            if (main) { setChuteStates(ChuteState.FULL_DEPLOYED, drogueChuteState); }
                            else { setChuteStates(mainChuteState, ChuteState.FULL_DEPLOYED); }
                            print("toggling to next state from full deploying");
                            updateDragCube = false;
                            updateAnim = false;
                        }
                        else
                        {
                            dragCubeA = main ? ChuteDragCube.MAINSEMI : ChuteDragCube.DROGUESEMI;
                            dragCubeB = main ? ChuteDragCube.DROGUEFULL : ChuteDragCube.DROGUEFULL;
                            updateDragCube = true;
                            updateAnim = true;
                        }
                        break;
                    }
                case ChuteState.SEMI_DEPLOYED:
                    {
                        progress = 1f;
                        updateAnim = true;
                        updateDragCube = false;
                        double alt = FlightGlobals.getAltitudeAtPos(part.transform.position);
                        float nextAlt = main ? mainFullAutoAlt : drogueFullAutoAlt;
                        if (alt < nextAlt)
                        {
                            updateAnim = false;
                            if (main)
                            {
                                print("toggling to next state from semi deployed");
                                setChuteStates(ChuteState.DEPLOYING_FULL, drogueChuteState);
                            }
                            else
                            {
                                print("toggling to next state from semi deployed");
                                setChuteStates(mainChuteState, ChuteState.DEPLOYING_FULL);
                            }
                        }
                        //TODO check if should full deploy
                        break;
                    }
                case ChuteState.FULL_DEPLOYED:
                    {
                        progress = 1f;
                        updateAnim = true;
                        updateDragCube = false;
                        if (!main)
                        {
                            double alt = FlightGlobals.getAltitudeAtPos(part.transform.position);
                            //TODO check if should auto-deploy main
                            float nextAlt = mainSemiAutoAlt;
                            if (alt < nextAlt)
                            {
                                print("toggling to main deploy state from drogue deployed");
                                setChuteStates(ChuteState.DEPLOYING_SEMI, ChuteState.CUT);
                                updateAnim = false;
                            }
                        }
                        break;
                    }
            }
            if (updateAnim)
            {
                updateDeployAnimation(main ? mainChuteModules : drogueChuteModules, main ? mainChuteState : drogueChuteState, progress);
            }
            if (updateDragCube)
            {
                updatePartDragCube(dragCubeA, dragCubeB, progress);
            }
        }

        private void setChuteStates(ChuteState newMainState, ChuteState newDrogueState)
        {
            this.mainChuteState = newMainState;
            this.mainChutePersistence = mainChuteState.ToString();
            this.drogueChuteState = newDrogueState;
            this.drogueChutePersistence = drogueChuteState.ToString();
            
            bool updateDrogueCubes = true;
            bool removeMainCap = newMainState!=ChuteState.RETRACTED;
            bool removeDrogueCap = removeMainCap || newDrogueState!=ChuteState.RETRACTED;
            
            ChuteDragCube a = ChuteDragCube.RETRACTED, b = ChuteDragCube.RETRACTED;
            float progress = 0;
            
            if (newMainState == ChuteState.CUT || newMainState == ChuteState.RETRACTED)
            {
                //disable renders
                foreach (ParachuteModelData module in mainChuteModules)
                {
                    SSTUUtils.enableRenderRecursive(module.baseModel.transform, false);
                }
                //dont worry about animation status, renders are disabled
                //let drogue update drag cube
                updateDeployAnimation(mainChuteModules, mainChuteState, 0, 0, 0);
                if (newMainState != ChuteState.RETRACTED) { removeMainCap = true;  removeDrogueCap = true; }
            }
            else
            {
                updateDrogueCubes = false;
                //enable renders, in case they were disabled by cut/retracted states
                foreach (ParachuteModelData module in mainChuteModules)
                {
                    SSTUUtils.enableRenderRecursive(module.baseModel.transform, true);
                }
                if (newMainState == ChuteState.DEPLOYING_SEMI)
                {
                    a = ChuteDragCube.RETRACTED;
                    b = ChuteDragCube.MAINSEMI;
                    progress = 0;
                    deployTime = mainSemiDeploySpeed;
                    updateDeployAnimation(mainChuteModules, mainChuteState, 0, 0, 0);
                    removeMainCap = true;
                    removeDrogueCap = true;
                }
                else if (newMainState == ChuteState.DEPLOYING_FULL)
                {
                    a = ChuteDragCube.MAINSEMI;
                    b = ChuteDragCube.MAINFULL;
                    progress = 0;
                    deployTime = mainFullDeploySpeed;
                    updateDeployAnimation(mainChuteModules, mainChuteState, 0, wobbleMultiplier, lerpDegreePerSecond);
                    removeMainCap = true;
                    removeDrogueCap = true;
                }
                else if (newMainState == ChuteState.SEMI_DEPLOYED)
                {
                    a = ChuteDragCube.MAINSEMI;
                    b = ChuteDragCube.MAINSEMI;
                    progress = 1;
                    deployTime = 0;
                    updateDeployAnimation(mainChuteModules, mainChuteState, 1, wobbleMultiplier, lerpDegreePerSecond);
                    removeMainCap = true;
                    removeDrogueCap = true;
                }
                else if (newMainState == ChuteState.FULL_DEPLOYED)
                {
                    a = ChuteDragCube.MAINFULL;
                    b = ChuteDragCube.MAINFULL;
                    progress = 1;
                    deployTime = 0;
                    updateDeployAnimation(mainChuteModules, mainChuteState, 1, wobbleMultiplier, lerpDegreePerSecond);
                    removeMainCap = true;
                    removeDrogueCap = true;
                }
            }

            if (newDrogueState == ChuteState.CUT || newDrogueState == ChuteState.RETRACTED)
            {
                //disable renders
                foreach (ParachuteModelData module in drogueChuteModules)
                {
                    SSTUUtils.enableRenderRecursive(module.baseModel.transform, false);
                }
                updateDeployAnimation(drogueChuteModules, drogueChuteState, 0, wobbleMultiplier, 0);
                if (updateDrogueCubes)
                {
                    a = ChuteDragCube.RETRACTED;
                    b = ChuteDragCube.RETRACTED;
                    progress = 0;
                    deployTime = 0;
                }
            }
            else
            {
                foreach (ParachuteModelData module in drogueChuteModules)
                {
                    SSTUUtils.enableRenderRecursive(module.baseModel.transform, true);
                }
                if (newDrogueState == ChuteState.DEPLOYING_SEMI)
                {
                    updateDeployAnimation(drogueChuteModules, drogueChuteState, 0, wobbleMultiplier, 0);
                    if (updateDrogueCubes)
                    {
                        a = ChuteDragCube.RETRACTED;
                        b = ChuteDragCube.DROGUESEMI;
                        progress = 0;
                        deployTime = drogueSemiDeploySpeed;
                    }
                }
                else if (newDrogueState == ChuteState.DEPLOYING_FULL)
                {
                    updateDeployAnimation(drogueChuteModules, drogueChuteState, 0, wobbleMultiplier, lerpDegreePerSecond);
                    if (updateDrogueCubes)
                    {
                        a = ChuteDragCube.DROGUESEMI;
                        b = ChuteDragCube.DROGUEFULL;
                        progress = 0;
                        deployTime = drogueFullDeploySpeed;
                    }
                }
                else if (newDrogueState == ChuteState.SEMI_DEPLOYED)
                {
                    updateDeployAnimation(drogueChuteModules, drogueChuteState, 1, wobbleMultiplier, lerpDegreePerSecond);
                    if (updateDrogueCubes)
                    {
                        a = ChuteDragCube.DROGUESEMI;
                        b = ChuteDragCube.DROGUESEMI;
                        progress = 1;
                        deployTime = 0;
                    }
                }
                else if (newDrogueState == ChuteState.FULL_DEPLOYED)
                {
                    updateDeployAnimation(drogueChuteModules, drogueChuteState, 1, wobbleMultiplier, lerpDegreePerSecond);
                    if (updateDrogueCubes)
                    {
                        a = ChuteDragCube.DROGUEFULL;
                        b = ChuteDragCube.DROGUEFULL;
                        progress = 1;
                        deployTime = 0;
                    }
                }
            }
            
            if (removeMainCap)
            {
                removeParachuteCap(mainCapName, HighLogic.LoadedSceneIsFlight && !hasJettisonedMainCap);
                hasJettisonedMainCap = true;
            }
            if (removeDrogueCap)
            {
                removeParachuteCap(drogueCapName, HighLogic.LoadedSceneIsFlight && !hasJettisonedDrogueCap);
                hasJettisonedDrogueCap = true;
            }

            updatePartDragCube(a, b, progress);

            bool canDeploy = (hasDrogeChute && (drogueChuteState==ChuteState.RETRACTED || (drogueChuteState==ChuteState.CUT && mainChuteState==ChuteState.RETRACTED)))
                             ||
                             (!hasDrogeChute && (mainChuteState==ChuteState.RETRACTED));
            bool canCut = (drogueChuteState!=ChuteState.RETRACTED && drogueChuteState!=ChuteState.CUT) 
                          ||
                          (mainChuteState!=ChuteState.RETRACTED && mainChuteState!=ChuteState.CUT);
            updateGuiState(canDeploy, canCut);
        }

        private void updateDeployAnimation(ParachuteModelData[] modules, ChuteState state, float progress)
        {
            updateDeployAnimation(modules, state, progress, wobbleMultiplier, lerpDegreePerSecond);
        }

        private void updateDeployAnimation(ParachuteModelData[] modules, ChuteState state, float progress, float randomization, float lerpDps)
        {
            foreach (ParachuteModelData data in modules) { data.updateDeployAnimation(state, progress, randomization, lerpDps); }
        }

        private void updateParachuteTargets(Vector3 wind)
        {
            Vector3 windLocal = part.transform.InverseTransformDirection(wind);
            Vector3 scale = new Vector3(1, 1, 1);
            if (windLocal.y <= 0) { scale.x = -scale.x; }
            rotatorTransform.rotation = Quaternion.LookRotation(wind, part.transform.forward);
            rotatorTransform.localScale = scale;
        }

        private void updateGuiState(bool deploy, bool cut)
        {
            Events["deployChuteEvent"].guiActive = deploy;
            Events["cutChuteEvent"].guiActive = cut;
            Fields["safeToDeploy"].guiActive = deploy;
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

        public bool debug = false;
        
        public Vector3 retractedScale = Vector3.zero;
        public Vector3 semiDeployedScale = new Vector3(0.2f, 1, 0.2f);
        public Vector3 fullDeployedScale = new Vector3(0.2f, 1, 0.2f);
        public float animationProgress = 0f;

        private Transform partTransform;
        private GameObject retractedTarget;
        private GameObject semiDeployedTarget;
        private GameObject fullDeployedTarget;
        private Quaternion prevWind;

        public ParachuteModelData(ConfigNode node, Vector3 retracted, Vector3 semi, Vector3 full)
        {
            name = node.GetStringValue("name");
            definition = SSTUParachuteDefinitions.getDefinition(name);
            modelName = definition.modelName;
            localPosition = node.GetVector3("localPosition");
            retractedUpVector = node.GetVector3("retractedUpVector");
            semiDeployedUpVector = node.GetVector3("semiDeployedUpVector");
            fullDeployedUpVector = node.GetVector3("fullDeployedUpVector");
            debug = node.GetBoolValue("debug");
            retractedScale = retracted;
            semiDeployedScale = semi;
            fullDeployedScale = full;
        }

        public void setupModel(Part part, Transform parent, Transform targetRotator)
        {
            this.partTransform = part.transform;
            
            retractedTarget = new GameObject("ParachuteRetractedTarget");
            retractedTarget.transform.NestToParent(targetRotator);
            retractedTarget.transform.localPosition = new Vector3(-retractedUpVector.x, -retractedUpVector.z, retractedUpVector.y);
            
            semiDeployedTarget = new GameObject("ParachuteSemiDeployTarget");
            semiDeployedTarget.transform.NestToParent(targetRotator);
            semiDeployedTarget.transform.localPosition = new Vector3(-semiDeployedUpVector.x, -semiDeployedUpVector.z, semiDeployedUpVector.y);
            
            fullDeployedTarget = new GameObject("ParachuteFullDeployTarget");
            fullDeployedTarget.transform.NestToParent(targetRotator);
            fullDeployedTarget.transform.localPosition = new Vector3(-fullDeployedUpVector.x, -fullDeployedUpVector.z, fullDeployedUpVector.y);
            
            parachutePivot = new GameObject("ParachuteSpacingPivot");
            parachutePivot.transform.NestToParent(parent);
            parachutePivot.transform.localPosition = new Vector3(localPosition.x, localPosition.y, -localPosition.z);//TODO -- why does this need to be inverted?
                        
            baseModel = SSTUUtils.cloneModel(modelName);
            baseModel.transform.NestToParent(parachutePivot.transform);
            
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
            
            if (debug)
            {
                GameObject debug1 = SSTUUtils.cloneModel("SSTU/Assets/DEBUG_MODEL"); 
                debug1.transform.NestToParent(parachutePivot.transform);
                GameObject debug2 = SSTUUtils.cloneModel("SSTU/Assets/DEBUG_MODEL");
                debug2.transform.NestToParent(parachutePivot.transform);
                debug2.transform.localPosition = new Vector3(0, 0, 2);

                GameObject debug3 = SSTUUtils.cloneModel("SSTU/Assets/DEBUG_MODEL");
                debug3.transform.NestToParent(baseModel.transform);
                GameObject debug4 = SSTUUtils.cloneModel("SSTU/Assets/DEBUG_MODEL");
                debug4.transform.NestToParent(baseModel.transform);
                debug4.transform.localPosition = new Vector3(0, 0, 2);

                GameObject debug5 = SSTUUtils.cloneModel("SSTU/Assets/DEBUG_MODEL");
                debug5.transform.NestToParent(retractedTarget.transform);
                GameObject debug6 = SSTUUtils.cloneModel("SSTU/Assets/DEBUG_MODEL");
                debug6.transform.NestToParent(semiDeployedTarget.transform);
                GameObject debug7 = SSTUUtils.cloneModel("SSTU/Assets/DEBUG_MODEL");
                debug7.transform.NestToParent(fullDeployedTarget.transform);
            }
        }
        
        public void updateDeployAnimation(ChuteState state, float progress, float randomization, float lerpDPS)
        {
            Vector3 targetA = Vector3.zero, targetB = Vector3.zero, scaleA = Vector3.one, scaleB = Vector3.one;
            switch (state)
            {
                case ChuteState.CUT:                    
                case ChuteState.RETRACTED:
                    targetA = retractedTarget.transform.position;
                    targetB = retractedTarget.transform.position;
                    scaleA = retractedScale;
                    scaleB = retractedScale;
                    break;
                case ChuteState.DEPLOYING_SEMI:
                    targetA = retractedTarget.transform.position;
                    targetB = semiDeployedTarget.transform.position;
                    scaleA = retractedScale;
                    scaleB = semiDeployedScale;
                    break;
                case ChuteState.SEMI_DEPLOYED:
                    targetA = semiDeployedTarget.transform.position;
                    targetB = semiDeployedTarget.transform.position;
                    scaleA = semiDeployedScale;
                    scaleB = semiDeployedScale;
                    break;
                case ChuteState.DEPLOYING_FULL:
                    targetA = semiDeployedTarget.transform.position;
                    targetB = fullDeployedTarget.transform.position;
                    scaleA = semiDeployedScale;
                    scaleB = fullDeployedScale;
                    break;
                case ChuteState.FULL_DEPLOYED:
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
            if (lerpDPS>0)
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
