using System;
using UnityEngine;

namespace SSTUTools
{
    public class SSTURCSFuelSelection : PartModule
    {

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Fuel Type"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentFuelType = string.Empty;

        [Persistent]
        public string configNodeData = string.Empty;
        
        private ContainerFuelPreset[] fuelTypes;
        private ContainerFuelPreset fuelType;
        private bool initialized = false;

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

            Fields[nameof(currentFuelType)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentFuelType = currentFuelType; }
                    m.fuelType = Array.Find(m.fuelTypes, s => s.name == m.currentFuelType);
                    m.updateRCSFuelType();
                });
            };

            Fields[nameof(currentFuelType)].guiActiveEditor = fuelTypes.Length > 1;
            string[] names = SSTUUtils.getNames(fuelTypes, m => m.name);
            this.updateUIChooseOptionControl(nameof(currentFuelType), names, names, true, currentFuelType);            
        }

        public void Start()
        {
            updateRCSFuelType();
        }

        private void init()
        {
            if (initialized) { return; }
            initialized = true;
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);

            ConfigNode[] fuelTypeNodes = node.GetNodes("FUELTYPE");
            int len = fuelTypeNodes.Length;
            fuelTypes = new ContainerFuelPreset[len];
            for (int i = 0; i < len; i++)
            {
                fuelTypes[i] = VolumeContainerLoader.getPreset(fuelTypeNodes[i].GetValue("name"));
            }
            fuelType = Array.Find(fuelTypes, m => m.name == currentFuelType);
            if (fuelType == null && (fuelTypes != null && fuelTypes.Length > 0))
            {
                MonoBehaviour.print("ERROR: SSTUModularRCS - currentFuelType was null for value: " + currentFuelType);
                fuelType = fuelTypes[0];
                currentFuelType = fuelType.name;
                MonoBehaviour.print("Assigned default fuel type of: " + currentFuelType + ".  This is likely a config error that needs to be corrected.");
            }
            else if (fuelTypes == null || fuelTypes.Length < 1)
            {
                //TODO -- handle cases of disabled fuel switching
                MonoBehaviour.print("ERROR: SSTUModularRCS - No fuel type definitions found.");
            }
        }

        private void updateRCSFuelType()
        {
            updateRCSFuelType(fuelType, part);
        }

        public static void updateRCSFuelType(string fuelType, Part part)
        {
            ContainerFuelPreset fuelTypeData = VolumeContainerLoader.getPreset(fuelType);
            if (fuelTypeData != null)
            {
                updateRCSFuelType(fuelTypeData, part);
            }
        }

        public static void updateRCSFuelType(ContainerFuelPreset fuelType, Part part)
        {
            ModuleRCS[] modules = part.GetComponents<ModuleRCS>();
            int len = modules.Length;
            ModuleRCS rcsModule;
            for (int i = 0; i < len; i++)
            {
                rcsModule = modules[i];
                rcsModule.propellants.Clear();
                ConfigNode pNode = fuelType.getPropellantNode(ResourceFlowMode.ALL_VESSEL_BALANCE);
                rcsModule.OnLoad(pNode);
            }
        }

    }
}
