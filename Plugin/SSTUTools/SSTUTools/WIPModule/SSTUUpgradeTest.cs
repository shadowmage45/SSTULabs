using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUUpgradeTest : PartModule
    {
        public override void OnInitialize()
        {
            base.OnInitialize();
            MonoBehaviour.print("OnInitialize");
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            MonoBehaviour.print("OnLoad \n"+node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            MonoBehaviour.print("OnStart");
        }

        //public override void ApplyUpgradeNode(ConfigNode node, bool doLoad)
        //{
        //    base.ApplyUpgradeNode(node, doLoad);
        //    MonoBehaviour.print("ApplyUpgradeNode: \n"+node);
        //}

        public override bool ApplyUpgrades(StartState state)
        {
            MonoBehaviour.print("ApplyUpgrades: " + state);
            return base.ApplyUpgrades(state);
        }

        public override void LoadUpgrades(ConfigNode node)
        {
            base.LoadUpgrades(node);
            MonoBehaviour.print("LoadUpgrades: \n" + node);
        }

        public override bool FindUpgrades(bool fillApplied, ConfigNode node = null)
        {
            MonoBehaviour.print("FindUpgrades: " + fillApplied);
            return base.FindUpgrades(fillApplied, node);
        }

    }
}
