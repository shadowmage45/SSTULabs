using UnityEngine;

namespace SSTUTools
{
    public class SSTUMultiDockingPort : PartModule
    {
        [KSPField]
        public string portName = "Port 1";

        [KSPField]
        public int dockingModuleIndex = 0;

        public void Start()
        {
            ModuleDockingNode[] dockModules = part.GetComponents<ModuleDockingNode>();
            if (dockingModuleIndex >= dockModules.Length)
            {
                MonoBehaviour.print("ERROR: Could not locate docking port by index: " + dockingModuleIndex + " only found: " + dockModules.Length + " docking modules on part.  Please check your part configuration for errors.");
                return;
            }
            ModuleDockingNode dockModule = dockModules[dockingModuleIndex];
            updateDockingModuleFieldNames(dockModule, portName);
        }

        public static void updateDockingModuleFieldNames(ModuleDockingNode dockModule, string portName)
        {
            dockModule.Events["Undock"].guiName = "Undock " + portName;
            dockModule.Events["UndockSameVessel"].guiName = "Undock" + portName;
            dockModule.Events["Decouple"].guiName = "Decouple " + portName;

            dockModule.Events["SetAsTarget"].guiName = "Set " + portName + " as Target";
            dockModule.Events["MakeReferenceTransform"].guiName = "Control from " + portName;

            dockModule.Events["DisableXFeed"].guiName = "Disable " + portName + " Crossfeed";
            dockModule.Events["EnableXFeed"].guiName = "Enable " + portName + " Crossfeed";

            dockModule.Actions["DecoupleAction"].guiName = "Decouple " + portName;
        }
    }
}

