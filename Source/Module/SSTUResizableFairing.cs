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

        [KSPField]
        public String techLimitSet = "Default";

        /// <summary>
        /// Persistent scale value, whatever value is here/in the config will be the 'start diameter' for parts in the editor/etc
        /// </summary>
        [KSPField(isPersistant = true, guiName ="Fairing Diameter")]
        public float currentDiameter = 1.25f;

        [KSPField(isPersistant = true, guiName = "Texture Set")]
        public String currentTextureSet = "Fairings-SLS";
        
        private float techLimitMaxDiameter;

        private ModuleProceduralFairing mpf = null;

        private TextureSet[] textureSets;
        
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

        [KSPEvent(guiName = "Next Texture Set", guiActiveEditor = true)]
        public void nextTextureSet()
        {
            TextureSet s = SSTUUtils.findNext(textureSets, m=>m.setName==currentTextureSet, false);
            currentTextureSet = s.setName;
            updateTexture();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            mpf = part.GetComponent<ModuleProceduralFairing>();
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);
            textureSets = TextureSet.loadTextureSets(node.GetNodes("TEXTURESET"));
            TechLimit.updateTechLimits(techLimitSet, out techLimitMaxDiameter);                        
            if (currentDiameter > techLimitMaxDiameter)
            {
                currentDiameter = techLimitMaxDiameter;
            }
            updateModelScale();
            updateTexture();
            updateNodePositions(false);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            mpf = part.GetComponent<ModuleProceduralFairing>();
            updateModelScale();//for prefab part...
            updateTexture();
        }

        public void Start()
        {
            mpf = part.GetComponent<ModuleProceduralFairing>();
            updateModelScale();//make sure to update the mpf after it is initialized
            updateTexture();
        }

        //TODO update symmetry counterparts
        public void onUserSizeChange()
        {
            if (currentDiameter > maxDiameter) { currentDiameter = maxDiameter; }
            if (currentDiameter > techLimitMaxDiameter) { currentDiameter = techLimitMaxDiameter; }
            if (currentDiameter < minDiameter) { currentDiameter = minDiameter; }
            updateModelScale();
            mpf.DeleteFairing();
            updateNodePositions(true);
            //TODO update symmetry counterparts
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
            SSTUAttachNodeUtils.updateAttachNodePosition(part, topNode, pos, topNode.orientation, userInput);
            pos = new Vector3(0, bottomY, 0);
            SSTUAttachNodeUtils.updateAttachNodePosition(part, bottomNode, pos, bottomNode.orientation, userInput);
        }

        private void updateTexture()
        {
            if (mpf != null)
            {
                //print("Updating mpf: " + mpf);
                TextureSet set = getCurrentTextureSet();
                if (set != null)
                {
                    //print("updating texture set: " + set.setName);
                    TextureData data = set.textureDatas[0];//TODO cleanup this hack
                    mpf.TextureURL = data.diffuseTextureName;
                    //print("set mpf texture to: " + mpf.TextureURL);
                    Texture t = SSTUUtils.findTexture(data.diffuseTextureName, false);
                    mpf.FairingMaterial.mainTexture = t;
                    foreach (var f in mpf.Panels)
                    {
                        f.go.renderer.material.mainTexture = t;
                    }
                }
            }
        }

        private TextureSet getCurrentTextureSet()
        {
            if (textureSets == null) { return null; }
            return Array.Find(textureSets, m => m.setName == currentTextureSet);
        }
    }
}
