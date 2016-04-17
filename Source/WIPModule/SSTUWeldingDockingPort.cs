using System;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUWeldingDockingPort : PartModule, IPartMassModifier, IPartCostModifier
    {        
        [KSPField]
        public String weldNodeName = "bottom";

        [KSPField]
        public float modelDiameter = 2.5f;

        [KSPField]
        public float diameterIncrement = 2.5f;

        [KSPField]
        public float minDiameter = 2.5f;

        [KSPField]
        public float maxDiameter = 2.5f;

        [KSPField]
        public String techLimitSet = "Default";

        [KSPField(guiName = "Diameter +/-", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0f, maxValue = 0.95f, stepIncrement = 0.05f)]
        public float editorDiameterAdjust;

        [KSPField(isPersistant =true, guiActiveEditor = true)]
        public float currentDiameter = 2.5f;

        private float modifiedMass;
        private float modifiedCost;

        private float editorWholeDiameter;        
        private float prevEditorDiameterAdjust;

        private float techLimitMaxDiameter;

        public float prefabCost = 1000f;
        public float prefabMass;

        [KSPEvent(guiName = "Diameter ++", guiActiveEditor =true)]
        public void nextDiameterEvent()
        {
            setDiameterFromEditor(currentDiameter + diameterIncrement, true);
        }

        [KSPEvent(guiName = "Diameter --", guiActiveEditor = true)]
        public void prevDiameterEvent()
        {
            setDiameterFromEditor(currentDiameter + diameterIncrement, true);
        }
        
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

        private void setDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {
            if (newDiameter > techLimitMaxDiameter) { newDiameter = techLimitMaxDiameter; }
            if (newDiameter > maxDiameter) { newDiameter = maxDiameter; }
            if (newDiameter < minDiameter) { newDiameter = minDiameter; }
            currentDiameter = newDiameter;
            updateEditorFields();
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
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { prefabMass = part.mass; }
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            return -defaultMass + modifiedMass;
        }
                
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            return -defaultCost + modifiedCost;
        }
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        public void onEditorVesselModified(ShipConstruct ship)
        {
            if (!HighLogic.LoadedSceneIsEditor) { return; }
            if (prevEditorDiameterAdjust != editorDiameterAdjust)
            {
                prevEditorDiameterAdjust = editorDiameterAdjust;
                float newDiameter = editorWholeDiameter + (editorDiameterAdjust * diameterIncrement);
                setDiameterFromEditor(newDiameter, true);
            }
        }

        private void initialize()
        {
            TechLimit.updateTechLimits(techLimitSet, out techLimitMaxDiameter);
            if (currentDiameter > techLimitMaxDiameter) { currentDiameter = techLimitMaxDiameter; }
            if (currentDiameter > maxDiameter) { currentDiameter = maxDiameter; }            
            if (currentDiameter < minDiameter) { currentDiameter = minDiameter; }            
            updateEditorFields();
            updateModelScale();
            updateDragCubes();
            updatePartCost();
            updatePartMass();
        }

        private void updateEditorFields()
        {
            float div = currentDiameter / diameterIncrement;
            float whole = (int)div;
            float extra = div - whole;
            editorWholeDiameter = whole * diameterIncrement;
            prevEditorDiameterAdjust = editorDiameterAdjust = extra;            
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
            modifiedCost = scaleFactor * scaleFactor * scaleFactor * prefabCost;
        }

        private void updatePartMass()
        {
            float scaleFactor = currentDiameter / modelDiameter;
            modifiedMass = scaleFactor * scaleFactor * scaleFactor * prefabMass;
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
            decoupleFromBase();
            otherPortModule.decoupleFromBase();
            weld.Couple(otherWeld);
            //if you don't de-activate the GUI it will null-ref because the active window belongs to one of the exploding parts below.
            UIPartActionController.Instance.Deactivate();
            //but then we need to re-activate it to make sure that part-right clicking/etc doesn't break
            UIPartActionController.Instance.Activate();
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
            AttachNode weldNode = part.findAttachNode(weldNodeName);
            if (weldNode != null && weldNode.attachedPart != null) { return weldNode.attachedPart; }
            AttachNode srfNode = part.srfAttachNode;
            if (srfNode != null && srfNode.attachedPart != null) { return srfNode.attachedPart; }
            return null;
        }

        private void selfDestruct()
        {
            part.explode();
        }
    }
}

