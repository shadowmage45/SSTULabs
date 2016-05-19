using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools.WIPModule
{
    public class SSTUModularCargoBay : PartModule
    {
        [KSPField]
        public string baseTransformName;

        [KSPField]
        public string currentModelName;

        private bool initialized = false;
        //private SingleModelData[] modelDatas;

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
            if (initialized) { return; }
            initialized = true;
            ConfigNode moduleNode = SSTUStockInterop.getPartModuleConfig(part, this);
            ConfigNode[] modelNodes = moduleNode.GetNodes("MODEL");
        }

    }
}
