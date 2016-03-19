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

    public class ControlTransformData
    {
        String name;
        String transformName;
        ControlTransformRCSData[] rcsData;

        public ControlTransformData(ConfigNode node)
        {

        }
    }

    public class ControlTransformRCSData
    {
        int moduleIndex;
        bool yaw;
        bool pitch;
        bool roll;
        bool x;
        bool y;
        bool z;

        public ControlTransformRCSData(ConfigNode node)
        {
            pitch = node.GetBoolValue("enablePitch", false);
            yaw = node.GetBoolValue("enableYaw", false);            
            roll = node.GetBoolValue("enableRoll", false);
            x = node.GetBoolValue("enableX");
            y = node.GetBoolValue("enableY");
            z = node.GetBoolValue("enableZ");
        }

        public void enable(Part part)
        {
            ModuleRCS[] rcsMmodules = part.GetComponents<ModuleRCS>();
            ModuleRCS module = rcsMmodules[moduleIndex];
            module.enablePitch = pitch;
            module.enableYaw = yaw;
            module.enableRoll = roll;
            module.enableX = x;
            module.enableY = y;
            module.enableZ = z;
        }
    }
}
