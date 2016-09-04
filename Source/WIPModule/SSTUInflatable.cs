using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace SSTUTools
{
    public class SSTUInflatable : PartModule, IPartMassModifier, IPartCostModifier
    {
        [KSPField]
        public int animationID = 0;

        [KSPField]
        public float deflationMult = 0.1f;

        [KSPField]
        public int inflatedCrew = 4;

        [KSPField]
        public int deflatedCrew = 0;

        [KSPField]
        public float inflationMass = 5f;

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

        [KSPField(guiName = "RocketParts reqd", guiActive = true, guiActiveEditor =true, guiUnits = " tons")]
        public float requiredMass = 0f;

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

            }
            BaseEvent evt = Events["inflateEvent"];
            evt.guiActive = evt.guiActiveEditor = false;

            evt = Events["deflateEvent"];
            evt.guiActiveEditor = true;
            evt.guiActive = canDeflate;
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
            if (inflated || resourceDef == null) { return 0; }
            float cost = (inflationMass / resourceDef.density) * resourceDef.unitCost;
            return inflated ? 0 : -cost;
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
            animation = SSTUAnimateControlled.locateAnimationController(part, animationID, onAnimStateChange);
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

        private void onAnimStateChange(AnimState newState)
        {
            //NOOP
        }

        private void onContainerUpdated(SSTUVolumeContainer vc)
        {
            if (!inflated)
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
            MonoBehaviour.print("Set crew capacity to: " + capacity+" current: "+part.CrewCapacity);
            part.CrewCapacity = capacity;
            List<PartCrewManifest> manifests = ShipConstruction.ShipManifest.GetCrewableParts();
            if (manifests == null || manifests.Count == 0) { return; }
            int len = manifests.Count;
            PartCrewManifest crewManifest = null;
            PartCrewManifest temp;
            for (int i = 0; i < len; i++)
            {
                temp = manifests[i];
                if (temp.PartID == part.craftID)
                {
                    crewManifest = temp;
                    break;
                }
            }
            if(crewManifest == null) { return; }
            //force the part-selection panel to be active, else the crew-selection UI will not be updated properly
            if (EditorLogic.fetch.editorScreen == EditorScreen.Crew)
            {
                EditorLogic.fetch.SelectPanelParts();
                //EditorLogic.fetch.SelectPanelCrew(); //TODO toggle back to crew select?
            }
            //clear existing crew from part before updating the crew manifest size
            len = crewManifest.GetPartCrew().Length;
            for (int i = 0; i < len; i++)
            {
                if (crewManifest.GetPartCrew()[i] != null) { crewManifest.RemoveCrewFromSeat(i); }
            }
            //TODO bug SQUAD devs about a public way to alter crew capacity, without having to resort to reflection hacks
            FieldInfo crewField = typeof(PartCrewManifest).GetField("partCrew", BindingFlags.NonPublic | BindingFlags.Instance);
            if (crewField != null) { crewField.SetValue(crewManifest, new string[capacity]); }
            ShipConstruction.ShipManifest.SetPartManifest(part.craftID, crewManifest);
        }

        private void updateRequiredMass()
        {
            requiredMass = inflationMass - appliedMass;
            BaseField fld = Fields["requiredMass"];
            fld.guiActive = fld.guiActiveEditor = requiredMass > 0;
            fld.guiName = resourceName + " reqd";
        }

    }
}
