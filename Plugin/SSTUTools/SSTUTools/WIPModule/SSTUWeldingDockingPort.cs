using System;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUWeldingDockingPort : PartModule, IPartMassModifier, IPartCostModifier, ISSTUAnimatedModule
    {        
        [KSPField]
        public String weldNodeName = "bottom";

        [KSPField]
        public float modelDiameter = 2.5f;

        [KSPField]
        public float diameterIncrement = 0.625f;

        [KSPField]
        public float minDiameter = 0.625f;

        [KSPField]
        public float maxDiameter = 10f;

        [KSPField]
        public float cost = 1000f;

        [KSPField]
        public float mass = 0.25f;

        [KSPField]
        public string animationID = string.Empty;

        [KSPField(isPersistant =true, guiActiveEditor = true, guiName = "Diam"),
         UI_FloatEdit(suppressEditorShipModified = true, unit = "m", sigFigs = 3)]
        public float currentDiameter = 2.5f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = true, guiName = "SnapAngle"),
         UI_FloatEdit(suppressEditorShipModified = true, sigFigs = 3, minValue = 0, maxValue = 360, incrementLarge = 90, incrementSmall = 15, incrementSlide = 1)]
        public float snapAngle = 90f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = true, guiName = "Snap"),
         UI_Toggle(enabledText = "Enabled", disabledText = "Disabled", suppressEditorShipModified = true)]
        public bool enableSnap = false;

        private float modifiedMass;
        private float modifiedCost;
        private bool initialized = false;
        private SSTUAnimateControlled animation;

        [KSPEvent(guiName = "Weld", guiActive = true)]
        public void weldEvent()
        {
            Part targetPart = getDockPart();
            if (targetPart == null)
            {
                MonoBehaviour.print("no other docking port attached!");
                return;
            }//nothing attached to docking port
            SSTUWeldingDockingPort targetModule = targetPart.GetComponent<SSTUWeldingDockingPort>();
            if (targetModule == null)
            {
                MonoBehaviour.print("no other construction port module found on attached port part");
                return;
            }//no construction port found
            Part baseWeld = getBasePart();
            if (baseWeld == null)
            {
                MonoBehaviour.print("nothing found for base part to do welding!");
                return;
            }//nothing to weld on this part
            Part targetBaseWeld = targetModule.getBasePart();
            if (targetBaseWeld == null)
            {
                MonoBehaviour.print("nothing found for other port base part to do welding!");
                return;
            }//nothing to weld on other part
            doWeld(targetPart, targetModule, targetBaseWeld);
            GameEvents.onVesselWasModified.Fire(part.vessel);
        }

        private void onDiameterChanged(BaseField field, System.Object obj)
        {
            setDiameterFromEditor(currentDiameter, true);
        }

        private void setDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {
            if (newDiameter > maxDiameter) { newDiameter = maxDiameter; }
            if (newDiameter < minDiameter) { newDiameter = minDiameter; }
            currentDiameter = newDiameter;
            updateModelScale();
            updateDragCubes();
            updatePartCost();
            updatePartMass();
            if (updateSymmetry)
            {
                if (updateSymmetry)
                {
                    foreach (Part p in part.symmetryCounterparts)
                    {
                        p.GetComponent<SSTUWeldingDockingPort>().setDiameterFromEditor(newDiameter, false);
                    }
                }
                SSTUStockInterop.fireEditorUpdate();
            }
        }

        private void onSnapToggled(BaseField field, System.Object obj)
        {
            ModuleDockingNode mdn = part.GetComponent<ModuleDockingNode>();
            mdn.snapOffset = snapAngle;
            mdn.snapRotation = enableSnap;
            MonoBehaviour.print("Set docking node module to snap angle: " + snapAngle + " enabled: " + enableSnap);
        }

        private void onSnapChanged(BaseField field, System.Object obj)
        {
            ModuleDockingNode mdn = part.GetComponent<ModuleDockingNode>();
            mdn.snapOffset = snapAngle;
            mdn.snapRotation = enableSnap;
            MonoBehaviour.print("Set docking node module to snap angle: " + snapAngle + " enabled: " + enableSnap);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
            GameEvents.onPartCouple.Add(new EventData<GameEvents.FromToAction<Part, Part>>.OnEvent(onDock));
            this.updateUIFloatEditControl("currentDiameter", minDiameter, maxDiameter, diameterIncrement * 2f, diameterIncrement, diameterIncrement * 0.05f, true, currentDiameter);
            BaseField diameter = Fields["currentDiameter"];
            diameter.uiControlEditor.onFieldChanged = onDiameterChanged;

            BaseField snapAngle = Fields["snapAngle"];
            snapAngle.uiControlEditor.onFieldChanged = onSnapChanged;
            snapAngle.uiControlFlight.onFieldChanged = onSnapChanged;

            BaseField snapToggle = Fields["enableSnap"];
            snapToggle.uiControlEditor.onFieldChanged = onSnapToggled;
            snapToggle.uiControlFlight.onFieldChanged = onSnapToggled;
        }

        public void OnDestroy()
        {
            GameEvents.onPartCouple.Remove(new EventData<GameEvents.FromToAction<Part, Part>>.OnEvent(onDock));
        }

        public void Start()
        {
            if (!string.IsNullOrEmpty(animationID))
            {
                animation = SSTUAnimateControlled.setupAnimationController(part, animationID, this);
            }
            updateGUI();
            ModuleDockingNode mdn = part.GetComponent<ModuleDockingNode>();
            mdn.snapOffset = snapAngle;
            mdn.snapRotation = enableSnap;
            MonoBehaviour.print("Set docking node module to snap angle: " + snapAngle + " enabled: " + enableSnap);
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (modifiedMass == 0) { return 0; }
            return -defaultMass + modifiedMass;
        }
                
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (modifiedCost == 0) { return 0; }
            return -defaultCost + modifiedCost;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        private void onDock(GameEvents.FromToAction<Part, Part> dockEvent)
        {
            updateGUI();
        }

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            if (currentDiameter > maxDiameter) { currentDiameter = maxDiameter; }            
            if (currentDiameter < minDiameter) { currentDiameter = minDiameter; }    
            updateModelScale();
            updateDragCubes();
            updatePartCost();
            updatePartMass();
        }

        private void updateDragCubes()
        {
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        private void updateModelScale()
        {
            Transform modelBase = part.transform.FindRecursive("model");
            float scaleFactor = currentDiameter / modelDiameter;
            Vector3 scale = new Vector3(scaleFactor, 1, scaleFactor);
            foreach (Transform model in modelBase)
            {
                model.localScale = scale;
            }
        }

        private void updatePartCost()
        {
            float scaleFactor = currentDiameter / modelDiameter;
            modifiedCost = scaleFactor * scaleFactor * scaleFactor * cost;
        }

        private void updatePartMass()
        {
            float scaleFactor = currentDiameter / modelDiameter;
            modifiedMass = scaleFactor * scaleFactor * scaleFactor * mass;
        }

        private void decoupleFromBase()
        {
            Part p = getBasePart();
            if (p != null)
            {
                MonoBehaviour.print("Decoupling from base part: " + p);
                if (p == part.parent)//decouple this
                {
                    part.decouple(0);
                }
                else//decouple parent
                {
                    p.decouple(0);
                }
            }
        }

        private void doWeld(Part otherPort, SSTUWeldingDockingPort otherPortModule, Part otherWeld)
        {
            Part weld = getBasePart();
            if (otherPort == null || otherPortModule == null || otherWeld == null || weld == null) { return; }

            AttachNode thisNode = weld.FindAttachNodeByPart(part);
            AttachNode otherNode = otherWeld.FindAttachNodeByPart(otherPort);
            decoupleFromBase();
            otherPortModule.decoupleFromBase();
            weld.Couple(otherWeld);
            setupPartAttachNodes(weld, otherWeld, thisNode, otherNode);

            //cleanup global data from all the changing of parts/etc
            FlightGlobals.ForceSetActiveVessel(weld.vessel);
            //if you don't de-activate the GUI it will null-ref because the active window belongs to one of the exploding parts below.
            UIPartActionController.Instance.Deactivate();
            //but then we need to re-activate it to make sure that part-right clicking/etc doesn't break
            UIPartActionController.Instance.Activate();

            //remove the welding docking ports
            //TODO find non-explosive way to do this?
            selfDestruct();
            otherPortModule.selfDestruct();
        }

        private Part getDockPart()
        {
            ModuleDockingNode mdn = part.GetComponent<ModuleDockingNode>();
            if (mdn != null && mdn.otherNode != null) { return mdn.otherNode.part; }
            return null;
        }

        private Part getBasePart()
        {
            AttachNode weldNode = part.FindAttachNode(weldNodeName);
            if (weldNode != null && weldNode.attachedPart != null) { return weldNode.attachedPart; }
            AttachNode srfNode = part.srfAttachNode;
            if (srfNode != null && srfNode.attachedPart != null) { return srfNode.attachedPart; }
            return null;
        }

        private void setupPartAttachNodes(Part thisWeld, Part otherWeld, AttachNode thisNode, AttachNode otherNode)
        {
            MonoBehaviour.print("weld parentage: " + thisWeld.parent + " :: " + otherWeld.parent);
            thisNode.attachedPart = otherWeld;
            otherNode.attachedPart = thisWeld;

            thisWeld.fuelLookupTargets.AddUnique(otherWeld);
            otherWeld.fuelLookupTargets.AddUnique(thisWeld);
        }

        private void selfDestruct()
        {
            part.explode();
        }

        public void onAnimationStateChange(AnimState newState)
        {
            updateGUI();
        }

        public void onModuleEnableChange(bool moduleEnabled)
        {
            //noop
        }

        private void updateGUI()
        {
            bool enabled = true;
            if (animation != null)
            {
                AnimState state = animation.getAnimationState();
                enabled = state == AnimState.STOPPED_END && HighLogic.LoadedSceneIsFlight;
            }
            ModuleDockingNode mdn = part.GetComponent<ModuleDockingNode>();
            if (mdn == null) { enabled = false; }
            else if(mdn.otherNode == null) { enabled = false; }
            Events["weldEvent"].guiActive = enabled;

            //TODO update animateUsable GUI to disable the 'extend' buttons when docking node is docked
        }
    }
}

