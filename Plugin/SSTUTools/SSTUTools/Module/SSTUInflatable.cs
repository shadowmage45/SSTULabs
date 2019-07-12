using UnityEngine;

namespace SSTUTools
{
    public class SSTUInflatable : PartModule, IPartMassModifier, IPartCostModifier
    {

        [KSPField]
        public float deflationMult = 0.1f;

        [KSPField]
        public int inflatedCrew = 4;

        [KSPField]
        public int deflatedCrew = 0;

        [KSPField]
        public float inflationMass = 5f;

        [KSPField]
        public float inflationCost = 0f;

        [KSPField]
        public string resourceName = "RocketParts";

        [KSPField]
        public bool canDeflate = false;

        [KSPField(isPersistant = true)]
        public float appliedMass = 0f;

        [KSPField(isPersistant = true)]
        public bool inflated = false;

        [KSPField(isPersistant = true)]
        public bool initializedDefualts = false;

        [KSPField(guiName = "Infl. Resource Req'd", guiActiveEditor = true, guiActive = true)]
        public string requiredResourceDisplay = string.Empty;

        [KSPField(guiName = "Required Amount", guiActiveEditor = true, guiActive = true)]
        public string requiredResourceAmount = string.Empty;

        [KSPField(isPersistant = true)]
        public string persistentState = AnimState.STOPPED_START.ToString();

        [Persistent]
        public string configNodeData = string.Empty;

        private bool initialized = false;
        private AnimationModule animationModule;
        private SSTUAnimateRotation rotationModule;
        private PartResourceDefinition resourceDef;
        
        [KSPEvent(guiName = "Inflate", guiActive = true, guiActiveEditor = true)]
        public void inflateEvent()
        {
            if (inflated) { return; }
            if (HighLogic.LoadedSceneIsFlight)
            {
                consumeResources();
            }
            else
            {
                appliedMass = inflationMass;
            }
            updateRequiredMass();
            if (appliedMass >= inflationMass)
            {
                animationModule.onDeployEvent();
                updateResourceAmounts(1.0f);
                updateCrewCapacity(inflatedCrew);
                inflated = true;
            }
        }

        [KSPEvent(guiName = "Deflate", guiActiveEditor = true)]
        public void deflateEvent()
        {
            if (!inflated) { return; }
            updateResourceAmounts(deflationMult);
            animationModule.onRetractEvent();
            if (rotationModule != null)
            {
                //force-send the retract event to the rotation module to trigger stopping of rotation during retract animation
                rotationModule.onAnimationStateChange(AnimState.STOPPED_START);
            }
            updateCrewCapacity(deflatedCrew);
            inflated = false;
            appliedMass = 0;
            updateRequiredMass();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            init();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            init();
        }

        public override string GetInfo()
        {
            string info = "This module requires " + inflationMass + " tons of " + resourceName + " in order to be brought online if launched in the deflated/packed state.";
            return info;
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { return; }
            animationModule.Update();
        }

        public void Start()
        {
            if (!initializedDefualts)
            {
                updateResourceAmounts(inflated? 1.0f : deflationMult);
            }
            if (rotationModule == null)
            {
                rotationModule = part.GetComponent<SSTUAnimateRotation>();
                if (rotationModule != null)
                {
                    setupRotationModule(rotationModule);
                }
            }
            initializedDefualts = true;
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            return inflated? 0 : -inflationMass;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen()
        {
            return ModifierChangeWhen.CONSTANTLY;
        }

        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            return inflated? 0 : -inflationCost;
        }

        public ModifierChangeWhen GetModuleCostChangeWhen()
        {
            return ModifierChangeWhen.CONSTANTLY;
        }

        private void init()
        {
            if (initialized) { return; }
            initialized = true;
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                updateCrewCapacity(inflated ? inflatedCrew : deflatedCrew);
            }

            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            AnimationData animData = new AnimationData(node.GetNode("ANIMATIONDATA"));

            animationModule = new AnimationModule(part, this, nameof(persistentState), null, nameof(inflateEvent), nameof(deflateEvent));
            animationModule.getSymmetryModule = m => ((SSTUInflatable)m).animationModule;
            animationModule.setupAnimations(animData, part.transform.FindRecursive("model"), 0);
            animationModule.onAnimStateChangeCallback = onAnimationStateChange;

            resourceDef = PartResourceLibrary.Instance.GetDefinition(resourceName);
            if (resourceDef == null)
            {
                MonoBehaviour.print("ERROR: Could not locate resource for name: " + resourceName + " for " + this.name);
            }
            updateRequiredMass();
        }

        /// <summary>
        /// To be called from Start() method on either/both this module and from SSTUAnimateRotation.  Ensures synced status between the deploy animation, and the rotation.
        /// </summary>
        /// <param name="module"></param>
        public void setupRotationModule(SSTUAnimateRotation module)
        {
            //this module was not yet initialized
            if (animationModule == null) { return; }
            rotationModule = module;
            rotationModule.initializeRotationModule(animationModule.animState);
        }

        private void onAnimationStateChange(AnimState newState)
        {
            if (rotationModule != null)
            {
                rotationModule.onAnimationStateChange(newState);
            }
        }

        private void updateResourceAmounts(float mult)
        {
            SSTUVolumeContainer vc = part.GetComponent<SSTUVolumeContainer>();
            if (vc != null)
            {
                vc.inflationMultiplier = mult;
            }
            //std call that will update volume container and/or realfuels interop
            SSTUModInterop.updateResourceVolume(part);
        }

        private void consumeResources()
        {
            if (resourceDef == null)
            {
                MonoBehaviour.print("ERROR: Could not locate resource definition for name: " + resourceName + " to consume for inflatable module.  This is a configuration error and should be corrected.");
                return;
            }
            double unitsNeeded = (inflationMass - appliedMass) / resourceDef.density;
            double unitsUsed = part.RequestResource(resourceName, unitsNeeded);
            appliedMass += (float) unitsUsed * resourceDef.density;
        }

        private void updateCrewCapacity(int capacity)
        {
            part.CrewCapacity = capacity;
        }

        private void updateRequiredMass()
        {
            float requiredMass = inflationMass - appliedMass;
            bool active = requiredMass > 0;

            string resourceName = this.resourceName;

            string resourceAmount = requiredMass.ToString();

            if (resourceDef != null)
            {
                resourceName = resourceDef.displayName;
                float units = requiredMass / resourceDef.density;
                resourceAmount = units + "u / " + requiredMass + "t";
            }

            BaseField fld = Fields[nameof(requiredResourceDisplay)];
            fld.guiActive = fld.guiActiveEditor = active;
            requiredResourceDisplay = resourceName;

            fld = Fields[nameof(requiredResourceAmount)];
            fld.guiActive = fld.guiActiveEditor = active;
            requiredResourceAmount = resourceAmount;
        }

    }
}
