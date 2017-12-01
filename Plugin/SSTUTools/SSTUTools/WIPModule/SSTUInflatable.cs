using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace SSTUTools
{
    public class SSTUInflatable : PartModule, IPartMassModifier, IPartCostModifier
    {
        [KSPField]
        public string animationID = string.Empty;

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

        private bool initialized = false;
        private SSTUAnimateControlled animation;
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
                updateResourceAmounts(1.0d / deflationMult);
                if (animation != null) { animation.setToState(AnimState.PLAYING_FORWARD); }
                updateCrewCapacity(inflatedCrew);
                inflated = true;

                BaseEvent evt = Events["inflateEvent"];
                evt.guiActive = evt.guiActiveEditor = !inflated;
                evt = Events["deflateEvent"];
                evt.guiActiveEditor = true;
                evt.guiActive = canDeflate;
            }
        }

        [KSPEvent(guiName = "Deflate", guiActiveEditor = true)]
        public void deflateEvent()
        {
            if (!inflated) { return; }
            updateResourceAmounts(deflationMult);
            if (animation != null) { animation.setToState(AnimState.PLAYING_BACKWARD); }
            updateCrewCapacity(deflatedCrew);
            inflated = false;

            appliedMass = 0;
            updateRequiredMass();

            BaseEvent evt = Events["inflateEvent"];
            evt.guiActive = evt.guiActiveEditor = true;

            evt = Events["deflateEvent"];
            evt.guiActive = evt.guiActiveEditor = false;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
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

        public void Start()
        {
            if (!inflated && !initializedDefualts)
            {
                updateResourceAmounts(deflationMult);
            }
            initializedDefualts = true;
            SSTUModInterop.addContainerUpdatedCallback(onContainerUpdated);
        }

        public void OnDestroy()
        {
            SSTUModInterop.removeContainerUpdatedCallback(onContainerUpdated);
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
            animation = SSTUAnimateControlled.locateAnimationController(part, animationID);
            if (animation != null)
            {
                AnimState state = inflated ? AnimState.STOPPED_END : AnimState.STOPPED_START;
                animation.setToState(state);
            }

            BaseEvent evt = Events["inflateEvent"];
            evt.guiActive = evt.guiActiveEditor = !inflated;
            
            evt = Events["deflateEvent"];
            evt.guiActiveEditor = inflated;
            evt.guiActive = inflated && (HighLogic.LoadedSceneIsEditor || canDeflate);

            resourceDef = PartResourceLibrary.Instance.GetDefinition(resourceName);
            if (resourceDef == null)
            {
                MonoBehaviour.print("ERROR: Could not locate resource for name: " + resourceName + " for " + this.name);
            }
            updateRequiredMass();
        }

        private void onContainerUpdated(SSTUVolumeContainer vc)
        {
            if (vc.part == part && !inflated)
            {
                updateResourceAmounts(deflationMult);
            }            
        }

        private void updateResourceAmounts(double mult)
        {
            int len = part.Resources.Count;
            for (int i = 0; i < len; i++)
            {
                part.Resources[i].maxAmount *= mult;
            }
            SSTUModInterop.updatePartResourceDisplay(part);
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

        /// <summary>
        /// most functioniality derived from TweakScale ScaleCrewCapacity method
        /// https://github.com/pellinor0/TweakScale/blob/master/Scale.cs#L279
        /// </summary>
        /// <param name="capacity"></param>
        private void updateCrewCapacity(int capacity)
        {
            MonoBehaviour.print("Setting crew capacity to: " + capacity + " from current: " + part.CrewCapacity);
            part.CrewCapacity = capacity;

            //if (!HighLogic.LoadedSceneIsEditor) { return; }//only run the following block in the editor; it updates the crew-assignment GUI
            //if (EditorLogic.fetch.editorScreen == EditorScreen.Crew)
            //{
            //    EditorLogic.fetch.SelectPanelParts();
            //    //EditorLogic.fetch.SelectPanelCrew(); //TODO toggle back to crew select? -- NOPE, causes KSP to explode as it tries to render both GUIs simultaneously
            //}

            //VesselCrewManifest vcm = ShipConstruction.ShipManifest;
            //if (vcm == null) { return; }
            //PartCrewManifest pcm = vcm.GetPartCrewManifest(part.craftID);
            //if (pcm == null) { return; }
            //int len = pcm.partCrew.Length;
            //for (int i = 0; i < len; i++)
            //{
            //    pcm.RemoveCrewFromSeat(i);
            //}
            //pcm.partCrew = new string[capacity];
            //for (int i = 0; i < capacity; i++)
            //{
            //    pcm.partCrew[i] = "";
            //}
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
