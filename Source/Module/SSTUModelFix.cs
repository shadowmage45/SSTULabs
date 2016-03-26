using System;
using UnityEngine;
namespace SSTUTools
{
    class SSTUModelFix : PartModule
    {
        [KSPField]
        public string model;
        [KSPField]
        public string parent;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
            {
                Transform modelT = part.transform.FindModel(model);
                Transform parentT = part.transform.FindRecursive(parent);
                MonoBehaviour.print("setting model " + modelT + " parent from: " + modelT.parent + " to: " + parentT);
                modelT.parent = parentT;
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            
            SSTUUtils.recursePrintComponents(part.gameObject, "");
        }
    }
}
