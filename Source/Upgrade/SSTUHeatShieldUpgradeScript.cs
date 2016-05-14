using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SaveUpgradePipeline;

namespace SSTUTools
{
    //[UpgradeModule(LoadContext.SFS | LoadContext.Craft, craftNodeUrl = "PART", sfsNodeUrl = "GAME/FLIGHTSTATE/VESSEL/PART")]
    class SSTUHeatShieldUpgradeScript //: UpgradeScript
    {

        //Version earlyCompat = new Version(1, 1, 0);
        //Version target = new Version(1, 1, 2);
        //string name = "SSTUHeatShieldUpgrade";
        //string desc = "SSTUHeatShieldUpgradeDescription";
        //public static bool needsUpdate = true;

        ////protected override bool CheckMinVersion(Version v)
        ////{
        ////    MonoBehaviour.print("sstu upgrade ver check ");
        ////    if (ran) { return true; }
        ////    return true;
        ////}

        //protected override bool CheckMaxVersion(Version v)
        //{
        //    MonoBehaviour.print("sstu upgrade ver check ");
        //    if (needsUpdate) { return true; }
        //    return false;
        //}

        //public override string Description
        //{
        //    get
        //    {
        //        return desc;
        //    }
        //}

        //public override Version EarliestCompatibleVersion
        //{
        //    get
        //    {
        //        return earlyCompat;
        //    }
        //}

        //public override string Name
        //{
        //    get
        //    {
        //        return name;
        //    }
        //}

        //public override Version TargetVersion
        //{
        //    get
        //    {
        //        return target;
        //    }
        //}

        //public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        //{
        //    needsUpdate = false;
        //    MonoBehaviour.print("Save upgrade test: " + node.GetValue("name") + ":" + node.GetValue("part"));
        //    nodeName = NodeUtil.GetPartNodeName(node, loadContext);
        //    ConfigNode hsNode = node.GetNode("MODULE", "name", "SSTUHeatShield");
        //    if (hsNode != null)
        //    {
        //        return TestResult.Upgradeable;
        //    }
        //    return TestResult.Pass;
        //}

        //public override void OnUpgrade(ConfigNode node, LoadContext loadContext)
        //{
        //    MonoBehaviour.print("Save upgrade: " + node);
        //    ConfigNode hsNode = node.GetNode("MODULE", "name", "SSTUHeatShield");
        //    ConfigNode mhsNode = node.GetNode("MODULE", "name", "SSTUModularHeatShield");
        //    if (hsNode != null && mhsNode != null)//old MHS part, only part that had both modules; it really shouldn't need any updating?
        //    {

        //    }
        //    else if (hsNode != null)//old pods, change the module name to the new MHS module, set shield type value dependant upon part?
        //    {
        //        hsNode.SetValue("name", "SSTUModularHeatShield");
        //        string pName = NodeUtil.GetPartNodeName(node, loadContext);
        //        MonoBehaviour.print("Old pod detected: " + pName);
        //        hsNode.SetValue("initializedResources", "true");
        //        if (pName == "SSTU-SC-A-DM")
        //        {
        //            hsNode.AddValue("currentShieldType", "Light");
        //        }
        //        else if (pName == "SSTU-SC-B-CM")
        //        {
        //            hsNode.AddValue("currentShieldType", "Medium");
        //        }
        //        else if (pName == "SSTU-SC-C-CM")
        //        {
        //            hsNode.AddValue("currentShieldType", "Heavy");
        //        }
        //    }
        //    MonoBehaviour.print("Post upgrade node: " + node);
        //}
    }
}
