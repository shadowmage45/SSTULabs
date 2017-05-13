﻿using System;
using UnityEngine;

namespace SSTUTools
{
    class SSTUResizableFairing : PartModule, IPartMassModifier, IPartCostModifier
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
        
        /// <summary>
        /// The max fairing diameter at the default base diameter; this setting gets scaled according to the fairing base size
        /// </summary>
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
        [KSPField(isPersistant = true, guiName ="Diameter", guiActiveEditor = true),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentDiameter = 1.25f;

        [KSPField(isPersistant = true, guiName = "Texture", guiActiveEditor = true),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentTextureSet = "Fairings-White";

        [Persistent]
        public string configNodeData = string.Empty;

        private float prevDiameter;
        private ModuleProceduralFairing mpf = null;
        private TextureSet[] textureSets;

        public void onTextureUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentTextureSet)
            {
                updateTexture(currentTextureSet);
                foreach (Part p in part.symmetryCounterparts) { p.GetComponent<SSTUResizableFairing>().updateTexture(currentTextureSet); }
            }
        }

        public void onDiameterUpdated(BaseField field, object obj)
        {
            if (prevDiameter != currentDiameter)
            {
                prevDiameter = currentDiameter;
                onUserSizeChange(currentDiameter, true);
            }
        }

        public void onUserSizeChange(float newDiameter, bool updateSymmetry)
        {
            if (newDiameter > maxDiameter) { newDiameter = maxDiameter; }
            if (newDiameter < minDiameter) { newDiameter = minDiameter; }
            currentDiameter = newDiameter;
            updateModelScale();
            mpf.DeleteFairing();
            updateNodePositions(true);
            updateEditorFields();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts) { p.GetComponent<SSTUResizableFairing>().onUserSizeChange(currentDiameter, false); }
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            mpf = part.GetComponent<ModuleProceduralFairing>();
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            textureSets = TextureSet.loadGlobalTextureSets(node.GetNodes("TEXTURESET"));
            int len = textureSets.Length;
            string[] textureSetNames = new string[len];
            for (int i = 0; i < len; i++)
            {
                textureSetNames[i] = textureSets[i].name;
            }
            this.updateUIChooseOptionControl("currentTextureSet", textureSetNames, textureSetNames, true, currentTextureSet);
            
            updateModelScale();
            updateTexture(currentTextureSet);
            updateNodePositions(false);
            this.updateUIFloatEditControl("currentDiameter", minDiameter, maxDiameter, diameterIncrement*2f, diameterIncrement, diameterIncrement*0.05f, true, currentDiameter);
            Fields["currentDiameter"].uiControlEditor.onFieldChanged = onDiameterUpdated;
            Fields["currentTextureSet"].uiControlEditor.onFieldChanged = onTextureUpdated;
            Fields["currentTextureSet"].guiActiveEditor = textureSets.Length > 1;
            updateEditorFields();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            mpf = part.GetComponent<ModuleProceduralFairing>();
            updateModelScale();//for prefab part...
            updateEditorFields();
        }

        public void Start()
        {
            mpf = part.GetComponent<ModuleProceduralFairing>();
            updateModelScale();//make sure to update the mpf after it is initialized
            updateTexture(currentTextureSet);
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            float scale = currentDiameter / modelDiameter;
            return -defaultMass + defaultMass * Mathf.Pow(scale, 3f);
        }

        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            float scale = currentDiameter / modelDiameter;
            return -defaultCost + defaultCost * Mathf.Pow(scale, 3f);
        }
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        private void updateEditorFields()
        {
            prevDiameter = currentDiameter;
        }        

        private void updateModelScale()
        {
            float scale = currentDiameter / modelDiameter;
            Transform tr = part.transform.FindModel(modelName);
            if (tr != null)
            {
                tr.localScale = new Vector3(scale, scale, scale);
            }
            if (mpf != null)
            {
                mpf.baseRadius = scale * fairingDiameter * 0.5f;
                mpf.maxRadius = scale * defaultMaxDiameter * 0.5f;
            }
        }

        private void updateNodePositions(bool userInput)
        {
            AttachNode topNode = part.FindAttachNode("top");
            AttachNode bottomNode = part.FindAttachNode("bottom");
            float scale = currentDiameter / modelDiameter;
            float topY = topNodePosition * scale;
            float bottomY = bottomNodePosition * scale;
            Vector3 pos = new Vector3(0, topY, 0);
            SSTUAttachNodeUtils.updateAttachNodePosition(part, topNode, pos, topNode.orientation, userInput);
            pos = new Vector3(0, bottomY, 0);
            SSTUAttachNodeUtils.updateAttachNodePosition(part, bottomNode, pos, bottomNode.orientation, userInput);
        }

        private void updateTexture(String name)
        {
            currentTextureSet = name;
            if (mpf != null)
            {
                TextureSet set = Array.Find(textureSets, m => m.name == currentTextureSet);
                if (set != null)
                {
                    TextureSetMaterialData data = set.textureData[0];
                    mpf.TextureURL = data.getPropertyValue("_MainTex");
                    Texture t = SSTUUtils.findTexture(mpf.TextureURL, false);
                    
                    mpf.FairingMaterial.mainTexture = t;
                    foreach (var f in mpf.Panels)
                    {
                        f.mat.mainTexture = t;
                        SSTUUtils.setMainTextureRecursive(f.go.transform, t);                        
                    }
                }
            }
        }
    }
}
