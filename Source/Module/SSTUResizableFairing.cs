using System;
using UnityEngine;

namespace SSTUTools
{
    class SSTUResizableFairing : PartModule
    {

        /// <summary>
        /// Minimum diameter of the model that can be selected by user
        /// </summary>
        [KSPField]
        public float minDiameter = 0.625f;

        /// <summary>
        /// Maximum diameter of the model that can be selected by user
        /// </summary>
        [KSPField]
        public float maxDiameter = 10;
        
        [KSPField]
        public float diameterIncrement = 0.625f;
        
        [KSPField]
        public float topNodePosition = 1f;

        [KSPField]
        public float bottomNodePosition = -0.25f;

        /// <summary>
        /// Default diameter of the model
        /// </summary>
        [KSPField]
        public float modelDiameter = 5f;

        /// <summary>
        /// Default/config diameter of the fairing, in case it differs from model diameter; model scale is applied to this to maintain correct scaling
        /// </summary>
        [KSPField]
        public float fairingDiameter = 5f;

        [KSPField]
        public float defaultMaxDiameter = 5f;

        /// <summary>
        /// root transform of the model, for scaling
        /// </summary>
        [KSPField]
        public String modelName = "SSTU/Assets/SC-GEN-FR";

        /// <summary>
        /// Persistent scale value, whatever value is here/in the config will be the 'start diameter' for parts in the editor/etc
        /// </summary>
        [KSPField(isPersistant = true, guiName ="Fairing Diameter")]
        public float currentDiameter = 1.25f;
                
        private ModuleProceduralFairing mpf = null;
        
        [KSPEvent(guiName ="Prev Fairing Diameter", guiActiveEditor =true)]
        public void prevDiameter()
        {
            currentDiameter -= diameterIncrement;
            onUserSizeChange();
        }

        [KSPEvent(guiName = "Next Fairing Diameter", guiActiveEditor = true)]
        public void nextDiameter()
        {
            currentDiameter += diameterIncrement;
            onUserSizeChange();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            updateModelScale();
            updateNodePositions(false);            
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            updateModelScale();//for prefab part...
        }

        public void Start()
        {
            mpf = part.GetComponent<ModuleProceduralFairing>();
            updateModelScale();//make sure to updat the mpf after it is linked
        }

        public void onUserSizeChange()
        {
            if (currentDiameter < minDiameter) { currentDiameter = minDiameter; }
            if (currentDiameter > maxDiameter) { currentDiameter = maxDiameter; }
            updateModelScale();
            mpf.DeleteFairing();
            updateNodePositions(true);
        }

        private void updateModelScale()
        {
            float scale = currentDiameter / modelDiameter;

            Transform tr = part.transform.FindModel(modelName);

            if (tr != null)
            {
                tr.localScale = new Vector3(scale, scale, scale);
            }
            else
            {
                print("could not locate transform for name: " + modelName);
                SSTUUtils.recursePrintComponents(part.gameObject, "");
            }

            if (mpf != null)
            {
                mpf.baseRadius = scale * fairingDiameter * 0.5f;
                mpf.maxRadius = scale * defaultMaxDiameter * 0.5f;
            }
        }

        private void updateNodePositions(bool userInput)
        {
            AttachNode topNode = part.findAttachNode("top");
            AttachNode bottomNode = part.findAttachNode("bottom");
            float scale = currentDiameter / modelDiameter;
            float topY = topNodePosition * scale;
            float bottomY = bottomNodePosition * scale;
            Vector3 pos = new Vector3(0, topY, 0);
            print("set topnode pos to: " + topY);
            print("set bottomnode pos to: " + bottomY);
            SSTUUtils.updateAttachNodePosition(part, topNode, pos, topNode.orientation, userInput);
            pos = new Vector3(0, bottomY, 0);
            SSTUUtils.updateAttachNodePosition(part, bottomNode, pos, bottomNode.orientation, userInput);
        }
    }
}
