using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{

    
    public class SSTUModularParachute : PartModule, IMultipleDragCube
    {
        private enum ChuteState
        {
            RETRACTED,
            DEPLOYING_SEMI,
            SEMI_DEPLOYED,
            DEPLOYING_FULL,
            FULL_DEPLOYED,
            CUT
        }
        
        [KSPField]
        public String baseTransformName = "SSTUModularParachuteBaseTransform";

        [KSPField]
        public Vector3 drogueRetractedScale = new Vector3(0.005f, 0.005f, 0.005f);

        [KSPField]
        public Vector3 drogueSemiDeployedScale = new Vector3(0.2f, 1.0f, 0.2f);

        [KSPField]
        public Vector3 drogueFullDeployedScale = new Vector3(1.0f, 1.0f, 1.0f);

        [KSPField]
        public float drogueSemiDeploySpeed = 2f;

        [KSPField]
        public float drogueFullDeploySpeed = 2f;

        [KSPField]
        public Vector3 mainRetractedScale = new Vector3(0.005f, 0.005f, 0.005f);

        [KSPField]
        public Vector3 mainSemiDeployedScale = new Vector3(0.2f, 1.0f, 0.2f);

        [KSPField]
        public Vector3 mainFullDeployedScale = new Vector3(1.0f, 1.0f, 1.0f);
        
        [KSPField]
        public float mainSemiDeploySpeed = 2f;

        [KSPField]
        public float mainFullDeploySpeed = 2f;        
        
        [KSPField(isPersistant = true)]
        public bool hasDrogueBeenDeployed = false;

        [KSPField(isPersistant = true)]
        public String drogueChutePersistence = ChuteState.RETRACTED.ToString();

        [KSPField(isPersistant = true)]
        public String mainChutePersistence = ChuteState.RETRACTED.ToString();

        [Persistent]
        public String configNodeData = String.Empty;

        private ChuteState mainChuteState = ChuteState.RETRACTED;
        private ChuteState drogueChuteState = ChuteState.RETRACTED;

        private DragCube dragCubeRetracted;
        private DragCube dragCubeDrogueSemi;
        private DragCube dragCubeDrogueFull;
        private DragCube dragCubeMainSemi;
        private DragCube dragCubeMainFull;

        private ParachuteModelData[] mainChuteModules;
        private ParachuteModelData[] drogueChuteModules;

        private float deployTime = 0f;
        private bool hasDrogeChute = false;
        private bool initialized = false;
        private bool animating = false;

        [KSPEvent(guiName = "Deploy Chute", guiActive = true, guiActiveEditor = true)]
        public void deployChuteEvent()
        {
            if (hasDrogeChute)
            {

            }
            else
            {

            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { configNodeData = node.ToString(); }
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        public void FixedUpdate()
        {
            if (animating) { updateDeploymentStatus(); }
        }

        #region initialization methods

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;            
            loadConfigData(SSTUNodeUtils.parseConfigNode(configNodeData));
            initializeModels();
            loadDragCubes();
            updateDeploymentStatus();
        }

        private void loadConfigData(ConfigNode node)
        {
            ConfigNode[] drogueNodes = node.GetNodes("DROGUECHUTE");
            ConfigNode[] mainNodes = node.GetNodes("MAINCHUTE");
            int len = drogueNodes.Length;
            drogueChuteModules = new ParachuteModelData[len];
            for (int i = 0; i < len; i++){ drogueChuteModules[i] = new ParachuteModelData(drogueNodes[i]); }
            len = mainNodes.Length;
            mainChuteModules = new ParachuteModelData[len];
            for (int i = 0; i < len; i++){ mainChuteModules[i] = new ParachuteModelData(mainNodes[i]); }
        }
        
        /// <summary>
        /// create the models, and restore them to their saved/default state
        /// </summary>
        private void initializeModels()
        {
            Transform baseTransform = part.transform.FindRecursive(baseTransformName);
            if (baseTransform == null)
            {
                Transform modelBase = part.transform.FindRecursive("model");
                GameObject baseObject = new GameObject(baseTransformName);
                baseTransform = baseObject.transform;
                baseTransform.name = baseTransformName;
                baseTransform.NestToParent(modelBase);
            }
            SSTUUtils.destroyChildren(baseTransform);
            foreach (ParachuteModelData droge in drogueChuteModules) { droge.setupModel(part, baseTransform); }
            foreach (ParachuteModelData main in mainChuteModules) { main.setupModel(part, baseTransform); }
            hasDrogeChute = drogueChuteModules.Length > 0;            
        }

        #endregion

        #region drag cube handling

        /// <summary>
        /// Restore the local drag cube references from the database
        /// </summary>
        private void loadDragCubes()
        {
            if (part.partInfo == null) { return; }
        }

        //TODO
        private void updatePartDragCube(DragCube a, DragCube b, float progress)
        {
            if (a == null || b == null) { return; }
        }

        public string[] GetDragCubeNames()
        {
            return new string[] { "RETRACTED","DROGUESEMI","DROGUEFULL","MAINSEMI","MAINFULL"};
        }

        //TODO
        /// <summary>
        /// Used by drag cube system to render the pre-computed drag cubes.  Should update model into the position intended for the input drag cube name.  The name will be one of those returned by GetDragCubeNames
        /// </summary>
        /// <param name="name"></param>
        public void AssumeDragCubePosition(string name)
        {
            //TODO
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

        private void updateDeploymentStatus()
        {
            if (deployTime > 0) { deployTime -= TimeWarp.fixedDeltaTime; }
            if (deployTime < 0) { deployTime = 0; }
            
            switch (mainChuteState)
            {
                case ChuteState.CUT:
                    {
                        foreach (ParachuteModelData module in mainChuteModules)
                        {
                            SSTUUtils.enableRenderRecursive(module.baseModel.transform, false);
                        }
                        break;
                    }
                case ChuteState.RETRACTED:
                    {
                        updateDeploymentState(mainChuteModules, mainRetractedScale, ChuteState.RETRACTED);
                        break;
                    }
                case ChuteState.DEPLOYING_SEMI:
                    {
                        updateDeploymentState(mainChuteModules, Vector3.Lerp(mainRetractedScale, mainSemiDeployedScale, deployTime/mainSemiDeploySpeed), ChuteState.DEPLOYING_SEMI);
                        break;
                    }
                case ChuteState.SEMI_DEPLOYED:
                    {
                        updateDeploymentState(mainChuteModules, mainSemiDeployedScale, ChuteState.RETRACTED);
                        break;
                    }
                case ChuteState.DEPLOYING_FULL:
                    {
                        updateDeploymentState(mainChuteModules, Vector3.Lerp(mainSemiDeployedScale, mainFullDeployedScale, deployTime / mainFullDeploySpeed), ChuteState.DEPLOYING_FULL);
                        break;
                    }
                case ChuteState.FULL_DEPLOYED:
                    {
                        updateDeploymentState(mainChuteModules, mainFullDeployedScale, ChuteState.RETRACTED);
                        break;
                    }
            }
            
            switch (drogueChuteState)
            {
                case ChuteState.CUT:
                    {
                        foreach (ParachuteModelData module in drogueChuteModules)
                        {
                            SSTUUtils.enableRenderRecursive(module.baseModel.transform, false);
                        }
                        break;
                    }
                case ChuteState.RETRACTED:
                    {
                        updateDeploymentState(drogueChuteModules, drogueRetractedScale, ChuteState.RETRACTED);
                        break;
                    }
                case ChuteState.DEPLOYING_SEMI:
                    {
                        updateDeploymentState(drogueChuteModules, Vector3.Lerp(drogueRetractedScale, drogueSemiDeployedScale, deployTime / drogueSemiDeploySpeed), ChuteState.DEPLOYING_SEMI);
                        break;
                    }
                case ChuteState.SEMI_DEPLOYED:
                    {
                        updateDeploymentState(drogueChuteModules, drogueSemiDeployedScale, ChuteState.RETRACTED);
                        break;
                    }
                case ChuteState.DEPLOYING_FULL:
                    {
                        updateDeploymentState(drogueChuteModules, Vector3.Lerp(drogueSemiDeployedScale, drogueFullDeployedScale, deployTime / drogueFullDeploySpeed), ChuteState.DEPLOYING_FULL);
                        break;
                    }
                case ChuteState.FULL_DEPLOYED:
                    {
                        updateDeploymentState(drogueChuteModules, drogueFullDeployedScale, ChuteState.RETRACTED);
                        break;
                    }
            }
        }

        private void updateDeploymentState(ParachuteModelData[] modules, Vector3 scale, ChuteState state)
        {
            foreach (ParachuteModelData data in modules) { updateAnimationState(data, scale, (state!=ChuteState.CUT && state!=ChuteState.RETRACTED)); }
        }

        private void updateAnimationState(ParachuteModelData data, Vector3 localScale, bool wobble)
        {
            data.baseModel.transform.localScale = localScale;
        }

        #endregion
        
    }

    public class ParachuteModelData : ModelData
    {
        SSTUParachuteDefinition definition;
        public Quaternion defaultOrientation;
        public GameObject baseModel;
        public GameObject capModel;
        public GameObject lineModel;
        public Vector3 localPosition;
        public float semiDeploySpeed;
        public float fullDeploySpeed;
        public float lineVerticalScale;
        public float capVerticalScale;
        public float capHorizontalScale;
        public ParachuteModelData(ConfigNode node) : base(node)
        {            
            diameter = definition.capDiameter;
            height = definition.capHeight + definition.lineHeight;
        }
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

        }

    }
}
