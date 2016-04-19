using System;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUControlTransform : PartModule
    {

        [KSPField]
        public String transformName;

        [KSPField]
        public String controlActionName = "Control From Here";

        private Transform referenceTransform;
        
        [KSPEvent(guiName = "Control From Here", guiActiveEditor = false, guiActive = true, guiActiveUnfocused = false, guiActiveUncommand = false, externalToEVAOnly = false)]
        public void makeReferenceTransform()
        {
            part.SetReferenceTransform(referenceTransform);
            vessel.SetReferenceTransform(part);
        }

        [KSPEvent(guiName = "Reset Control", guiActiveEditor = false, guiActive = true, guiActiveUnfocused = false, guiActiveUncommand = false, externalToEVAOnly = false)]
        public void resetReferenceTransform()
        {
            part.SetReferenceTransform(part.transform);
            vessel.SetReferenceTransform(part);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Events["makeReferenceTransform"].guiName = controlActionName;
            referenceTransform = part.transform.FindRecursive(transformName);
        }
    }
}
