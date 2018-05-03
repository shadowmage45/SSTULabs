using System;
using UnityEngine;
using KSPShaderTools;
using System.Collections.Generic;

namespace SSTUTools
{
    class SSTUResizableFairing : PartModule, IPartMassModifier, IPartCostModifier, IRecolorable
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

        [KSPField(isPersistant = true)]
        public string customColorData = string.Empty;

        [KSPField(isPersistant = true)]
        public bool initializedColors = false;

        [Persistent]
        public string configNodeData = string.Empty;

        private bool initialized = false;
        private float prevDiameter;
        private ModuleProceduralFairing mpf = null;
        private RecoloringHandler recolorHandler;

        public void onTextureUpdated(BaseField field, object obj)
        {
            this.actionWithSymmetry(m => 
            {
                m.currentTextureSet = currentTextureSet;
                m.updateTextureSet(!SSTUGameSettings.persistRecolor());
            });
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
            initialize();
            updateModelScale();
            updateTextureSet(false);
            updateNodePositions(false);
            this.updateUIFloatEditControl(nameof(currentDiameter), minDiameter, maxDiameter, diameterIncrement*2f, diameterIncrement, diameterIncrement*0.05f, true, currentDiameter);
            Fields[nameof(currentDiameter)].uiControlEditor.onFieldChanged = onDiameterUpdated;
            Fields[nameof(currentTextureSet)].uiControlEditor.onFieldChanged = onTextureUpdated;
            updateEditorFields();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            if (node.HasValue("customColor1"))
            {
                Color c1 = node.GetColorFromFloatCSV("customColor1");
                Color c2 = node.GetColorFromFloatCSV("customColor2");
                Color c3 = node.GetColorFromFloatCSV("customColor3");
                string colorData = c1.r + "," + c1.g + "," + c1.b + "," + c1.a + ",0;";
                colorData = colorData + c2.r + "," + c2.g + "," + c2.b + "," + c2.a + ",0;";
                colorData = colorData + c3.r + "," + c3.g + "," + c3.b + "," + c3.a + ",0";
                customColorData = colorData;
            }
            initialize();
            updateModelScale();//for prefab part...
            updateEditorFields();
        }

        public void Start()
        {
            mpf = part.GetComponent<ModuleProceduralFairing>();
            updateModelScale();//make sure to update the mpf after it is initialized
            updateTextureSet(false);
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

        public string[] getSectionNames()
        {
            return new string[] { "Fairing" };
        }

        public RecoloringData[] getSectionColors(string name)
        {
            return recolorHandler.getColorData();
        }

        public void setSectionColors(string name, RecoloringData[] colors)
        {
            recolorHandler.setColorData(colors);
            updateTextureSet(false);
        }

        //IRecolorable override
        public TextureSet getSectionTexture(string section)
        {
            return TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
        }

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;

            recolorHandler = new RecoloringHandler(Fields[nameof(customColorData)]);

            mpf = part.GetComponent<ModuleProceduralFairing>();
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            string[] names = node.GetStringValues("textureSet");
            string[] titles = SSTUUtils.getNames(TexturesUnlimitedLoader.getTextureSets(names), m => m.title);
            TextureSet t = TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
            if (t == null)
            {
                currentTextureSet = names[0];
                t = TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
                initializedColors = false;
            }
            if (!initializedColors)
            {
                initializedColors = true;
                recolorHandler.setColorData(t.maskColors);
            }
            this.updateUIChooseOptionControl(nameof(currentTextureSet), names, titles, true, currentTextureSet);
            Fields[nameof(currentTextureSet)].guiActiveEditor = names.Length > 1;
        }

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

        private void updateTextureSet(bool useDefaults)
        {
            if (mpf == null) { return; }
            TextureSet s = TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
            RecoloringData[] colors = useDefaults ? s.maskColors : getSectionColors(string.Empty);
            Material fm = mpf.FairingMaterial;
            s.textureData[0].apply(fm);//TODO -- bit of an ugly hack; should at least pull a ref to whatever index that slot goes to
            s.textureData[0].apply(mpf.FairingMaterial);
            s.textureData[0].applyRecoloring(mpf.FairingMaterial, colors);
            s.textureData[0].apply(mpf.FairingConeMaterial);
            s.textureData[0].applyRecoloring(mpf.FairingConeMaterial, colors);
            List<Transform> trs = new List<Transform>();
            foreach (ProceduralFairings.FairingPanel fp in mpf.Panels)
            {
                s.enable(fp.go.transform, colors);
            }
            if (useDefaults)
            {
                recolorHandler.setColorData(colors);
            }
            SSTUModInterop.onPartTextureUpdated(part);
        }

    }
}
