using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    /// <summary>
    /// Responsible for config-based simple lighting animation.
    /// Cannot/does not move transforms (hanlded through link to SSTUAnimateControlled).
    /// Intended handle float-curve based animations for emissive and light-transform setups.
    /// Loop animation will play from front-back-front-back, etc; front (start) should be the same state as the end as the on animation (not enforced...)
    /// </summary>
    public class SSTUAnimateLight : PartModule
    {
        private enum LightAnimationState
        {
            OFF,
            TURNING_ON,
            ON,//non-looped 'on' state
            LOOPING_FORWARD,
            LOOPING_BACKWARD,
            TURNING_OFF_LOOP,
            TURNING_OFF
        }

        [KSPField]
        public FloatCurve emissiveOnRedCurve;
        [KSPField]
        public FloatCurve emissiveOnBlueCurve;
        [KSPField]
        public FloatCurve emissiveOnGreenCurve;
        [KSPField]
        public FloatCurve emissiveLoopRedCurve;
        [KSPField]
        public FloatCurve emissiveLoopBlueCurve;
        [KSPField]
        public FloatCurve emissiveLoopGreenCurve;
        [KSPField]
        public FloatCurve lightOnRedCurve;
        [KSPField]
        public FloatCurve lightOnBlueCurve;
        [KSPField]
        public FloatCurve lightOnGreenCurve;
        [KSPField]
        public FloatCurve lightLoopRedCurve;
        [KSPField]
        public FloatCurve lightLoopBlueCurve;
        [KSPField]
        public FloatCurve lightLoopGreenCurve;

        //length of the 'lights-on' animation
        [KSPField]
        public float animationOnTime = 1;

        //length of the 'loop' animation, 0 for non-looping (will play last frame of 'on' animation)
        [KSPField]
        public float animationLoopTime = 0;

        [KSPField]
        public String actionName = "Lights";

        [KSPField]
        public String resourceToUse = "ElectricCharge";

        [KSPField]
        public float resourceUse = 0f;

        [KSPField(isPersistant = true)]
        public float progress = 0;

        [KSPField(isPersistant = true)]
        public String statePersistence = LightAnimationState.OFF.ToString();

        [KSPField]
        public int animationID = -1;

        [Persistent]
        public string configNodeData = string.Empty;

        private bool initialized = false;
        private LightAnimationState state = LightAnimationState.OFF;
        private SSTUAnimateControlled animationController;
        private List<Renderer> emissiveRenderers = new List<Renderer>();
        private LightData[] lightTransforms;
        private int shaderEmissiveID;

        private MaterialPropertyBlock matProps;

        [KSPAction("Toggle Lights", KSPActionGroup.Light)]
        public void enableLightsAction(KSPActionParam param)
        {
            if ((param.type == KSPActionType.Deactivate && state == LightAnimationState.OFF) || (param.type==KSPActionType.Activate && state!=LightAnimationState.OFF)) { return; }
            enableLightsEvent();
        }

        [KSPEvent(guiName = "Enable Lights", guiActive = true, guiActiveEditor = true)]
        public void enableLightsEvent()
        {
            switch (state)
            {
                case LightAnimationState.OFF:
                    setState(LightAnimationState.TURNING_ON);
                    break;
                case LightAnimationState.TURNING_ON:
                    setState(LightAnimationState.TURNING_OFF);
                    break;
                case LightAnimationState.ON:
                    setState(LightAnimationState.TURNING_OFF);
                    break;
                case LightAnimationState.LOOPING_FORWARD:
                    setState(LightAnimationState.TURNING_OFF_LOOP);
                    break;
                case LightAnimationState.LOOPING_BACKWARD:
                    setState(LightAnimationState.TURNING_OFF_LOOP);
                    break;
                case LightAnimationState.TURNING_OFF_LOOP:
                    setState(LightAnimationState.LOOPING_FORWARD);
                    break;
                case LightAnimationState.TURNING_OFF:
                    setState(LightAnimationState.TURNING_ON);
                    break;
                default:
                    break;
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        public void Start()
        {
            if (animationID >= 0)
            {
                animationController = SSTUAnimateControlled.locateAnimationController(part, animationID, OnAnimationStatusCallback);
            }
            updateAnimationControllerState();
        }

        public override void OnAwake()
        {
            base.OnAwake();
            //set up the default curves, for if none are specified in the config
            emissiveOnRedCurve = new FloatCurve();
            emissiveOnRedCurve.Add(0, 0);
            emissiveOnRedCurve.Add(1, 1);
            emissiveOnBlueCurve = new FloatCurve();
            emissiveOnBlueCurve.Add(0, 0);
            emissiveOnBlueCurve.Add(1, 1);
            emissiveOnGreenCurve = new FloatCurve();
            emissiveOnGreenCurve.Add(0, 0);
            emissiveOnGreenCurve.Add(1, 1);

            lightOnRedCurve = new FloatCurve();
            lightOnRedCurve.Add(0, 0);
            lightOnRedCurve.Add(1, 1);
            lightOnBlueCurve = new FloatCurve();
            lightOnBlueCurve.Add(0, 0);
            lightOnBlueCurve.Add(1, 1);
            lightOnGreenCurve = new FloatCurve();
            lightOnGreenCurve.Add(0, 0);
            lightOnGreenCurve.Add(1, 1);

            emissiveLoopRedCurve = new FloatCurve();
            emissiveLoopRedCurve.Add(0, 0);
            emissiveLoopRedCurve.Add(1, 1);
            emissiveLoopBlueCurve = new FloatCurve();
            emissiveLoopBlueCurve.Add(0, 0);
            emissiveLoopBlueCurve.Add(1, 1);
            emissiveLoopGreenCurve = new FloatCurve();
            emissiveLoopGreenCurve.Add(0, 0);
            emissiveLoopGreenCurve.Add(1, 1);

            lightLoopRedCurve = new FloatCurve();
            lightLoopRedCurve.Add(0, 0);
            lightLoopRedCurve.Add(1, 1);
            lightLoopBlueCurve = new FloatCurve();
            lightLoopBlueCurve.Add(0, 0);
            lightLoopBlueCurve.Add(1, 1);
            lightLoopGreenCurve = new FloatCurve();
            lightLoopGreenCurve.Add(0, 0);
            lightLoopGreenCurve.Add(1, 1);

            shaderEmissiveID = Shader.PropertyToID("_EmissiveColor");
            matProps = new MaterialPropertyBlock();
        }

        public void OnAnimationStatusCallback(AnimState newState)
        {
            //NOOP for this module, light state is controlled independently from animation state
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { return; }//only operate flight/editor scenes
            switch (state)
            {
                case LightAnimationState.OFF:
                    break;
                case LightAnimationState.TURNING_ON:
                    progress += TimeWarp.fixedDeltaTime;
                    if (progress >= animationOnTime)
                    {
                        if (animationLoopTime > 0)
                        {
                            progress = 0;
                            setState(LightAnimationState.LOOPING_FORWARD);
                        }
                        else
                        {
                            progress = 1;
                            setState(LightAnimationState.ON);
                        }
                    }
                    else
                    {
                        updateMeshEmissives(progress, false);
                        updateLights(progress, false);
                    }
                    break;
                case LightAnimationState.ON:
                    break;
                case LightAnimationState.LOOPING_FORWARD:
                    progress += TimeWarp.fixedDeltaTime;
                    if (progress >= animationLoopTime)
                    {
                        progress = animationLoopTime;
                        setState(LightAnimationState.LOOPING_BACKWARD);
                    }
                    else
                    {
                        updateMeshEmissives(progress, true);
                        updateLights(progress, true);
                    }
                    break;
                case LightAnimationState.LOOPING_BACKWARD:
                    progress -= TimeWarp.fixedDeltaTime;
                    if (progress <= 0)
                    {
                        progress = 0;
                        setState(LightAnimationState.LOOPING_FORWARD);
                    }
                    else
                    {
                        updateMeshEmissives(progress, true);
                        updateLights(progress, true);
                    }
                    break;
                case LightAnimationState.TURNING_OFF_LOOP:
                    if (animationLoopTime > 0)
                    {
                        progress -= TimeWarp.fixedDeltaTime;
                        if (progress <= 0)
                        {
                            progress = 0;
                            setState(LightAnimationState.TURNING_OFF);
                        }
                        else
                        {
                            updateMeshEmissives(progress, true);
                            updateLights(progress, true);
                        }
                    }
                    break;
                case LightAnimationState.TURNING_OFF:
                    progress -= TimeWarp.fixedDeltaTime;
                    if (progress <= 0)
                    {
                        progress = 0;
                        setState(LightAnimationState.OFF);
                    }
                    else
                    {
                        updateMeshEmissives(progress, false);
                        updateLights(progress, false);
                    }
                    break;
            }
        }

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            loadConfigData(SSTUConfigNodeUtils.parseConfigNode(configNodeData));
            setState(state);
        }

        private void loadConfigData(ConfigNode node)
        {
            ConfigNode[] emissiveNodes = node.GetNodes("EMISSIVE");
            ConfigNode[] lightNodes = node.GetNodes("LIGHT");
            emissiveRenderers.Clear();
            Renderer render;
            foreach(ConfigNode enode in emissiveNodes)
            {
                Transform[] trs1 = part.transform.FindChildren(enode.GetStringValue("name"));
                foreach (Transform tr in trs1)
                {
                    render = tr.GetComponent<Renderer>();
                    if (render != null)
                    {
                        emissiveRenderers.Add(render);
                    }
                }
            }
            lightTransforms = new LightData[lightNodes.Length];
            for (int i = 0; i < lightNodes.Length; i++)
            {
                lightTransforms[i] = new LightData(lightNodes[i], part);
            }            
            state = (LightAnimationState)Enum.Parse(typeof(LightAnimationState), statePersistence);
        }

        private void setState(LightAnimationState state)
        {
            this.state = state;
            this.statePersistence = state.ToString();
            String guiPrefix = String.Empty;
            switch (state)
            {
                case LightAnimationState.OFF:
                    progress = 0;
                    updateMeshEmissives(progress, false);
                    updateLights(progress, false);
                    enableLights(false);
                    guiPrefix = "Enable";
                    break;
                case LightAnimationState.TURNING_ON:
                    progress = 0;
                    updateMeshEmissives(progress, false);
                    updateLights(progress, false);
                    enableLights(true);
                    guiPrefix = "Disable";
                    break;
                case LightAnimationState.ON:
                    progress = animationOnTime;
                    updateMeshEmissives(progress, false);
                    updateLights(progress, false);
                    enableLights(true);
                    guiPrefix = "Disable";
                    break;
                case LightAnimationState.LOOPING_FORWARD:
                    progress = 0;
                    updateMeshEmissives(progress, true);
                    updateLights(progress, true);
                    enableLights(true);
                    guiPrefix = "Disable";
                    break;
                case LightAnimationState.LOOPING_BACKWARD:
                    progress = animationLoopTime;
                    updateMeshEmissives(progress, true);
                    updateLights(progress, true);
                    enableLights(true);
                    guiPrefix = "Disable";
                    break;
                case LightAnimationState.TURNING_OFF_LOOP:
                    updateMeshEmissives(progress, true);
                    updateLights(progress, true);
                    enableLights(true);
                    guiPrefix = "Enable";
                    break;
                case LightAnimationState.TURNING_OFF:                    
                    updateMeshEmissives(progress, false);
                    updateLights(progress, false);
                    enableLights(true);
                    guiPrefix = "Enable";
                    break;
                default:
                    break;
            }
            updateAnimationControllerState();
            Events["enableLightsEvent"].guiName = guiPrefix+ " " + actionName;
        }

        private Color color = new Color(0, 0, 0);
        private void updateMeshEmissives(float progress, bool useLoop)
        {
            float p = progress / (useLoop ? animationLoopTime : animationOnTime);
            FloatCurve rCurve = useLoop?emissiveOnRedCurve : emissiveLoopRedCurve, bCurve = useLoop?emissiveOnBlueCurve:emissiveLoopBlueCurve, gCurve=useLoop?emissiveOnGreenCurve:emissiveOnBlueCurve;
            color.r = rCurve.Evaluate(p);
            color.b = bCurve.Evaluate(p);
            color.g = gCurve.Evaluate(p);
            matProps.SetColor(shaderEmissiveID, color);
            int len = emissiveRenderers.Count;
            for (int i = 0; i < len; i++)
            {
                if (emissiveRenderers[i] != null)
                {
                    emissiveRenderers[i].SetPropertyBlock(matProps);
                    //emissiveRenderers[i].sharedMaterial.SetColor(shaderEmissiveID, color);
                }
            }
        }

        private void updateLights(float progress, bool useLoop)
        {
            float p = progress / (useLoop ? animationLoopTime : animationOnTime);
            if (float.IsNaN(p)) { p = 0; }
            FloatCurve rCurve = useLoop ? lightLoopRedCurve : lightOnRedCurve, bCurve = useLoop ? lightLoopBlueCurve : lightOnBlueCurve, gCurve = useLoop ? lightLoopGreenCurve : lightOnGreenCurve;
            color.r = rCurve.Evaluate(p);
            color.b = bCurve.Evaluate(p);
            color.g = gCurve.Evaluate(p);
            foreach (LightData tr in lightTransforms)
            {
                tr.setColor(color);
            }
        }

        private void enableLights(bool enable)
        {
            foreach (LightData data in lightTransforms)
            {
                if (enable) { data.enableLight(); }
                else { data.disableLight(); }
            }
        }

        private void updateAnimationControllerState()
        {
            if (animationController == null) { return; }
            AnimState newState = AnimState.STOPPED_START;
            switch (state)
            {
                case LightAnimationState.OFF:
                    newState = AnimState.STOPPED_START;
                    break;
                case LightAnimationState.TURNING_ON:
                    newState = AnimState.PLAYING_FORWARD;
                    break;
                case LightAnimationState.ON:
                    newState = AnimState.STOPPED_END;
                    break;
                case LightAnimationState.LOOPING_FORWARD:
                    newState = AnimState.STOPPED_END;
                    break;
                case LightAnimationState.LOOPING_BACKWARD:
                    newState = AnimState.STOPPED_END;
                    break;
                case LightAnimationState.TURNING_OFF_LOOP:
                    newState = AnimState.STOPPED_END;
                    break;
                case LightAnimationState.TURNING_OFF:
                    newState = AnimState.PLAYING_BACKWARD;
                    break;
                default:
                    break;
            }
            animationController.setToState(newState);
        }

    }

    public class LightData
    {
        public String name;//read
        public float intensity;//read
        public float range;//read
        public float angle;//read
        public LightType type;//read

        public Transform transform;
        public Light light;

        public LightData(ConfigNode node, Part part)
        {
            name = node.GetStringValue("name");
            intensity = node.GetFloatValue("intensity");
            range = node.GetFloatValue("range");
            angle = node.GetFloatValue("angle");
            type = (LightType)Enum.Parse(typeof(LightType), node.GetStringValue("type", LightType.Point.ToString()));

            transform = part.transform.FindRecursive(name);
            light = transform.GetComponent<Light>();
            if (light == null)
            {
                light = transform.gameObject.AddComponent<Light>();//add it if it does not exist                
            }

            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.type = type;
        }

        public void setColor(Color color)
        {
            light.color = color;
        }

        public void enableLight()
        {
            light.enabled = true;
        }

        public void disableLight()
        {
            light.enabled = false;
        }
    }
}
