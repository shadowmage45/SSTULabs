using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSTUTools
{
    public class SSTUModuleSwitch : PartModule, IPartMassModifier, IPartCostModifier
    {

        /// <summary>
        /// Persistent data storage, saves state regarding which modules are enabled and/or disabled.
        /// </summary>
        [KSPField(isPersistant =true)]
        public String persistentData = String.Empty;

        //active managed modules
        private List<ManagedModule> managedModules = new List<ManagedModule>();
        private float modifiedCost;
        private float modifiedMass;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            int len = managedModules.Count;
            for (int i = 0; i < len; i++)
            {
                managedModules[i].OnLoad(node);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            int len = managedModules.Count;
            for (int i = 0; i < len; i++)
            {
                managedModules[i].OnSave(node);
            }
        }

        public float GetModuleCost(float defaultCost)
        {
            throw new NotImplementedException();
        }

        public float GetModuleMass(float defaultMass)
        {
            throw new NotImplementedException();
        }
    }

    public class ManagedModule
    {
        public PartModule module;

        public int indexInConfigs;//what index this module has in the list of all managed module configs

        public void OnSave(ConfigNode node)
        {
            node.SetValue("MM_DYNAMIC", true.ToString(), true);//flag it as a dynamic module, so MM can scrub it from the base save data; I'll be manually saving the data to specially formatted sub-nodes of the SSTUModuleSwitch save-data...
            module.OnSave(node);
        }

        public void OnLoad(ConfigNode node)
        {
            module.OnLoad(node);
        }
    }
}
