using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUModuleControl : PartModule
    {
        private Dictionary<int, IControlledModule> modulesByID = new Dictionary<int, IControlledModule>();

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        private void initialize()
        {
            modulesByID.Clear();
            IControlledModule[] cms = SSTUUtils.getComponentsImplementing<IControlledModule>(part.gameObject);
            int id;
            foreach (IControlledModule cm in cms)
            {
                id = cm.getControlID();
                if (id >= 0)
                {
                    if (modulesByID.ContainsKey(id))
                    {
                        print("Found duplicate control ID when setting up SSTUModuleControl.  Duplicate ID: " + id + " for module: " + cm.GetType());
                    }
                    else
                    {
                        modulesByID.Add(id, cm);
                    }
                }
            }
        }

        public void enableControlledModule(int id)
        {
            IControlledModule cm = getControlledModule(id);
            if (cm != null && !cm.isControlEnabled())
            {
                cm.enableModule();
            }
            else if (cm == null)
            {
                print("ERROR, no module to control for id: " + id);
            }
        }

        public void disableControlledModule(int id)
        {
            IControlledModule cm = getControlledModule(id);
            if (cm != null && cm.isControlEnabled())
            {
                cm.disableModule();
            }
            else if (cm == null)
            {
                print("ERROR, no module to control for id: " + id);
            }
        }

        public IControlledModule getControlledModule(int id)
        {
            IControlledModule cm;
            modulesByID.TryGetValue(id, out cm);
            return cm;
        }
    }
}

