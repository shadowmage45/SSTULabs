using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    /// <summary>
    /// Responsible for config-based lighting animations.
    /// Can be linked into existing animations on the part to force simultaneous playing
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
        public int animationLayer = 0;

        [KSPField]
        public String resourceToUse = "ElectricCharge";

        [KSPField]
        public float resourceUse = 0f;

        [KSPField(isPersistant = true)]
        public string animationPersistentData = AnimState.STOPPED_START.ToString();

        [Persistent]
        public string configNodeData = string.Empty;

        [Persistent]
        public bool prefabSetup = false;

        private bool initialized = false;

        private AnimationModule animationController;

        #region REGION - UI interaction methods

        [KSPAction("Toggle Lights", KSPActionGroup.Light)]
        public void toggleLightsAction(KSPActionParam param)
        {
            animationController.onToggleAction(param);
        }

        [KSPEvent(guiName = "Enable Lights", guiActive = true, guiActiveEditor = true)]
        public void enableLightsEvent()
        {
            animationController.onDeployEvent();
        }

        [KSPEvent(guiName = "Disable Lights", guiActive = true, guiActiveEditor = true)]
        public void disableLightsEvent()
        {
            animationController.onRetractEvent();
        }

        #endregion ENDREGION - UI interaction methods

        #region REGION - KSP Overrides

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

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { return; }//only operate flight/editor scenes
            animationController.Update();
        }

        public void FixedUpdate()
        {
            if (resourceUse > 0 && animationController.animState != AnimState.STOPPED_START)
            {

            }
        }

        #endregion ENDREGION - KSP Overrides

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            loadConfigData(SSTUConfigNodeUtils.parseConfigNode(configNodeData));
        }

        private void loadConfigData(ConfigNode node)
        {
            //should only run the first time the part-module is initialized, 
            // which had better be on the prefab part (or bad things will happen)
            // only tracked so as to not add duplicate animations/clips
            if (!prefabSetup)
            {
                prefabSetup = true;
                Transform root = part.transform.FindRecursive("model");

                ConfigNode[] emissiveNodes = node.GetNodes("EMISSIVE");
                int len = emissiveNodes.Length;
                EmissiveData[] emissiveDatas = new EmissiveData[len];
                for (int i = 0; i < len; i++)
                {
                    emissiveDatas[i] = new EmissiveData(emissiveNodes[i]);
                    emissiveDatas[i].createAnimationClips(root);
                }
                
                ConfigNode[] lightNodes = node.GetNodes("LIGHT");
                len = lightNodes.Length;
                LightData[] lightDatas = new LightData[lightNodes.Length];
                for (int i = 0; i < len; i++)
                {
                    lightDatas[i] = new LightData(lightNodes[i]);
                    lightDatas[i].createAnimationClips(root);
                }
            }

            //will not function without animation data being loaded/present
            // the EMISSIVE and LIGHT blocks -build- new animations that can be
            // referenced inside of the ANIMATION data blocks
            AnimationData animData = new AnimationData(node.GetNode("ANIMATIONDATA"));

            //setup animation control module.  limit/deploy/retract events passed as null, as UI visibility/updating handled externally to ensure syncing to light animation state
            animationController = new AnimationModule(part, this, nameof(animationPersistentData), null, nameof(enableLightsEvent), nameof(disableLightsEvent));
            animationController.getSymmetryModule = m => ((SSTUAnimateLight)m).animationController;
            animationController.setupAnimations(animData, part.transform.FindRecursive("model"), animationLayer);
        }

        public static void createEmissiveAnimation(string clipName, Transform transform, FloatCurve r, FloatCurve g, FloatCurve b)
        {
            Type type = typeof(Material);
            string propName = "_EmissiveColor";
            Animation animationComponent = transform.GetComponent<Animation>();
            if (animationComponent == null)
            {
                animationComponent = transform.gameObject.AddComponent<Animation>();
                animationComponent.playAutomatically = false;
            }

            AnimationClip clip = new AnimationClip();
            clip.name = clipName;
            clip.legacy = true;
            clip.SetCurve("", type, propName + ".r", r.Curve);
            clip.SetCurve("", type, propName + ".g", g.Curve);
            clip.SetCurve("", type, propName + ".b", b.Curve);
            clip.SetCurve("", type, propName + ".a", AnimationCurve.Linear(0, 1, 1, 1));
            animationComponent.AddClip(clip, clip.name);
        }

        public static void createLightAnimation(string clipName, Transform transform, FloatCurve r, FloatCurve g, FloatCurve b)
        {
            Type type = typeof(Light);
            string propName = "m_color";

            Animation animationComponent = transform.GetComponent<Animation>();
            if (animationComponent == null)
            {
                animationComponent = transform.gameObject.AddComponent<Animation>();
                animationComponent.playAutomatically = false;
            }

            AnimationClip clip = new AnimationClip();
            clip.name = clipName;
            clip.legacy = true;
            clip.SetCurve("", type, propName + ".r", r.Curve);
            clip.SetCurve("", type, propName + ".g", g.Curve);
            clip.SetCurve("", type, propName + ".b", b.Curve);
            clip.SetCurve("", type, "m_Enabled", AnimationCurve.Linear(0, 0, 0.001f, 1));
            animationComponent.AddClip(clip, clip.name);
        }
        
        public static FloatCurve createDefaultCurve()
        {
            FloatCurve fc = new FloatCurve();
            fc.Add(0, 0);
            fc.Add(1, 1);
            return fc;
        }

    }

    public class EmissiveData
    {
        public readonly string name;
        public readonly string transformName;
        public readonly FloatCurve redCurve;
        public readonly FloatCurve greenCurve;
        public readonly FloatCurve blueCurve;
        
        public EmissiveData(ConfigNode node)
        {
            this.name = node.GetStringValue("name", "emissiveAnimation");
            this.transformName = node.GetStringValue("transformName");
            redCurve = node.HasNode("redCurve") ? node.GetFloatCurve("redCurve") : SSTUAnimateLight.createDefaultCurve();
            greenCurve = node.HasNode("greenCurve") ? node.GetFloatCurve("greenCurve") : SSTUAnimateLight.createDefaultCurve();
            blueCurve = node.HasNode("blueCurve") ? node.GetFloatCurve("blueCurve") : SSTUAnimateLight.createDefaultCurve();
        }

        public void createAnimationClips(Transform root)
        {
            Transform[] transforms = root.FindChildren(transformName);
            int len = transforms.Length;
            for (int i = 0; i < len; i++)
            {
                SSTUAnimateLight.createEmissiveAnimation(name, transforms[i], redCurve, greenCurve, blueCurve);
            }            
        }

    }

    public class LightData
    {
        public string name;//read
        public string transformName;
        public float intensity;//read
        public float range;//read
        public float angle;//read
        public LightType type;//read
        public readonly FloatCurve redCurve;
        public readonly FloatCurve greenCurve;
        public readonly FloatCurve blueCurve;

        public LightData(ConfigNode node)
        {
            name = node.GetStringValue("name", "lightAnimation");
            transformName = node.GetStringValue("transformName");
            intensity = node.GetFloatValue("intensity");
            range = node.GetFloatValue("range");
            angle = node.GetFloatValue("angle");
            type = (LightType)Enum.Parse(typeof(LightType), node.GetStringValue("type", LightType.Point.ToString()));
            redCurve = node.HasNode("redCurve") ? node.GetFloatCurve("redCurve") : SSTUAnimateLight.createDefaultCurve();
            greenCurve = node.HasNode("greenCurve") ? node.GetFloatCurve("greenCurve") : SSTUAnimateLight.createDefaultCurve();
            blueCurve = node.HasNode("blueCurve") ? node.GetFloatCurve("blueCurve") : SSTUAnimateLight.createDefaultCurve();
        }

        public void createAnimationClips(Transform root)
        {
            Transform[] transforms = root.FindChildren(transformName);
            int len = transforms.Length;
            
            Transform transform;
            Light light;
            for (int i = 0; i < len; i++)
            {
                transform = transforms[i];
                light = transform.GetComponent<Light>();
                if (light == null)
                {
                    light = transform.gameObject.AddComponent<Light>();//add it if it does not exist                
                }

                //set light params to the config specified parameters
                light.intensity = intensity;
                light.range = range;
                light.spotAngle = angle;
                light.type = type;
                light.cullingMask = light.cullingMask & ~(1 << 10);//flip the layer 10 bit to ignore scaled scenery, keep existing mask except for layer 10
                SSTUAnimateLight.createLightAnimation(name, transforms[i], redCurve, greenCurve, blueCurve);
            }
        }

    }

}
