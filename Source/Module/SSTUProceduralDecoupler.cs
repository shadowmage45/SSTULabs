using UnityEngine;
using System;
namespace SSTUTools
{
    public class SSTUProceduralDecoupler : PartModule, IPartCostModifier, IPartMassModifier
    {
        #region fields
                
        [KSPField]
        public int cylinderSides = 24;

        [KSPField]
        public float diameterIncrement = 0.625f;

        [KSPField]
        public float heightIncrement = 0.1f;

        [KSPField]
        public float thicknessIncrement = 0.1f;

        [KSPField]
        public float minDiameter = 0.3125f;

        [KSPField]
        public float maxDiameter = 5f;

        [KSPField]
        public float minThickness = 0.1f;

        [KSPField]
        public float maxThickness = 5f;

        [KSPField]
        public float minHeight = 0.1f;

        [KSPField]
        public float maxHeight = 0.5f;

        [KSPField]
        public float massPerCubicMeter = 0.4f;

        [KSPField]
        public float costPerCubicMeter = 5000f;

        [KSPField]
        public float forcePerKg = 0.75f;

        [KSPField]
        public String techLimitSet = "Default";

        [KSPField]
        public String uvMap = "NodeFairing";

        [KSPField(isPersistant = true, guiName = "Diameter", guiActiveEditor = true),
         UI_FloatEdit(sigFigs = 2, suppressEditorShipModified =true)]
        public float diameter = 1.25f;

        [KSPField(isPersistant = true, guiName = "Height", guiActiveEditor = true),
         UI_FloatEdit(sigFigs = 2, suppressEditorShipModified = true)]
        public float height = 0.1f;

        [KSPField(isPersistant = true, guiName = "Thickness", guiActiveEditor = true),
         UI_FloatEdit(sigFigs = 2, suppressEditorShipModified = true)]
        public float thickness = 0.1f;

        [KSPField(isPersistant = true, guiName = "Hollow Collider", guiActiveEditor = true), UI_Toggle(disabledText ="Disabled", enabledText ="Enabled")]
        public bool hollowCollider = false;

        [KSPField(isPersistant = true, guiName = "Texture", guiActiveEditor = true),
         UI_ChooseOption(suppressEditorShipModified =true)]
        public String currentTextureSet = String.Empty;

        private float modifiedMass = 0;
        private float modifiedCost = 0;
        private float volume = 0;

        private ProceduralCylinderModel model;
        
        private float prevDiameter;
        private float prevHeight;
        private float prevThickness;
        private bool prevCollider;

        private TextureSet currentTextureSetData;
        private TextureSet[] textureSetData;
        
        private float techLimitMaxDiameter;
                
        #endregion

        #region KSP GUI Actions/Events

        public void onTextureUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentTextureSet)
            {
                setTextureFromEditor(currentTextureSet, true);
            }
        }
        
        public void onHeightUpdated(BaseField field, object obj)
        {
            if (prevHeight != height)
            {
                prevHeight = height;
                setHeightFromEditor(height, true);
            }
        }

        public void onDiameterUpdated(BaseField field, object obj)
        {
            if (prevDiameter != diameter)
            {
                prevDiameter = diameter;
                setDiameterFromEditor(diameter, true);
            }
        }

        public void onThicknessUpdated(BaseField field, object obj)
        {
            if (prevThickness != thickness)
            {
                prevThickness = thickness;
                setThicknessFromEditor(thickness, true);
            }
        }

        public void onColliderUpdated(BaseField field, object obj)
        {
            if (prevCollider != hollowCollider)
            {
                prevCollider = hollowCollider;
                recreateModel();
            }
        }

        private void setDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {
            if (newDiameter > maxDiameter) { newDiameter = maxDiameter; }
            if (newDiameter > techLimitMaxDiameter) { newDiameter = techLimitMaxDiameter; }
            if (newDiameter < minDiameter) { newDiameter = minDiameter; }
            diameter = newDiameter;
            updateEditorFields();
            recreateModel();
            updateAttachNodePositions(true);
            if (updateSymmetry)
            {
                SSTUProceduralDecoupler dc;
                foreach (Part p in part.symmetryCounterparts)
                {
                    dc = p.GetComponent<SSTUProceduralDecoupler>();
                    dc.setDiameterFromEditor(newDiameter, false);
                }
            }
        }

        private void setHeightFromEditor(float newHeight, bool updateSymmetry)
        {
            if (newHeight > maxHeight) { newHeight = maxHeight; }
            if (newHeight < minHeight) { newHeight = minHeight; }
            height = newHeight;
            updateEditorFields();
            recreateModel();
            updateAttachNodePositions(true);
            if (updateSymmetry)
            {
                SSTUProceduralDecoupler dc;
                foreach (Part p in part.symmetryCounterparts)
                {
                    dc = p.GetComponent<SSTUProceduralDecoupler>();
                    dc.setHeightFromEditor(newHeight, false);
                }
            }
        }

        private void setThicknessFromEditor(float newThickness, bool updateSymmetry)
        {
            if (newThickness > maxThickness) { newThickness = maxThickness; }
            if (newThickness > diameter) { newThickness = diameter; }
            if (newThickness < minThickness) { newThickness = minThickness; }
            thickness = newThickness;
            updateEditorFields();
            recreateModel();
            if (updateSymmetry)
            {
                SSTUProceduralDecoupler dc;
                foreach (Part p in part.symmetryCounterparts)
                {
                    dc = p.GetComponent<SSTUProceduralDecoupler>();
                    dc.setThicknessFromEditor(newThickness, false);
                }
            }
        }

        private void setTextureFromEditor(String newTexture, bool updateSymmetry)
        {
            currentTextureSet = newTexture;
            currentTextureSetData = Array.Find(textureSetData, m => m.setName == newTexture);
            if (currentTextureSetData == null)
            {
                currentTextureSetData = textureSetData[0];
                currentTextureSet = currentTextureSetData.setName;
                newTexture = currentTextureSet;
            }
            TextureData data = currentTextureSetData.textureDatas[0];
            model.setMainTexture(data.diffuseTextureName);
            model.setNormalTexture(data.normalTextureName);
            if (updateSymmetry)
            {
                SSTUProceduralDecoupler dc;
                foreach (Part p in part.symmetryCounterparts)
                {
                    dc = p.GetComponent<SSTUProceduralDecoupler>();
                    dc.setTextureFromEditor(newTexture, false);
                }
            }
        }

        #endregion

        #region KSP Lifecycle and KSP Overrides
        
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            //this preps the model that will be displayed for the prefab part / editor part icon
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
            {
                loadConfigData();
                updateEditorFields();
                prepModel();
            }
        }

        public override string GetInfo()
        {
            //destroy the model after the prefab part/icon part has been created
            model.destroyModel();            
            model = null;
            return "This part has configurable diameter, height, thickness, and ejection force.";
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            loadConfigData();
            float max = techLimitMaxDiameter < maxDiameter ? techLimitMaxDiameter : maxDiameter;
            this.updateUIFloatEditControl("height", minHeight, maxHeight, heightIncrement*2f, heightIncrement, heightIncrement*0.05f, true, height);
            this.updateUIFloatEditControl("diameter", minDiameter, max, diameterIncrement*2f, diameterIncrement, diameterIncrement*0.05f, true, diameter);
            this.updateUIFloatEditControl("thickness", minThickness, maxThickness, thicknessIncrement*2f, thicknessIncrement, thicknessIncrement*0.05f, true, thickness);
            updateEditorFields();
            prepModel();
            Fields["height"].uiControlEditor.onFieldChanged = onHeightUpdated;
            Fields["diameter"].uiControlEditor.onFieldChanged = onDiameterUpdated;
            Fields["thickness"].uiControlEditor.onFieldChanged = onThicknessUpdated;
            Fields["hollowCollider"].uiControlEditor.onFieldChanged = onColliderUpdated;
            Fields["currentTextureSet"].uiControlEditor.onFieldChanged = onTextureUpdated;
            Fields["currentTextureSet"].guiActiveEditor = textureSetData.Length > 1;
        }

        private void loadConfigData()
        {
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);

            ConfigNode[] textureNodes = node.GetNodes("TEXTURESET");
            int len = textureNodes.Length;
            textureSetData = new TextureSet[len];
            for (int i = 0; i < len; i++)
            {
                textureSetData[i] = new TextureSet(textureNodes[i]);
            }
            currentTextureSetData = Array.Find(textureSetData, m => m.setName == currentTextureSet);
            if (currentTextureSetData == null)
            {
                currentTextureSetData = textureSetData[0];
                currentTextureSet = currentTextureSetData.setName;
            }
            string[] textureSetNames = new string[len];
            for (int i = 0; i < len; i++)
            {
                textureSetNames[i] = textureSetData[i].setName;
            }
            this.updateUIChooseOptionControl("currentTextureSet", textureSetNames, textureSetNames, true, currentTextureSet);

            techLimitMaxDiameter = SSTUStockInterop.getTechLimit(techLimitSet);
            if (diameter > techLimitMaxDiameter)
            {
                diameter = techLimitMaxDiameter;
            }
        }

        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            return -defaultCost + modifiedCost;
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            return -defaultMass + modifiedMass;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        public void Start()
        {
            updateDecouplerForce();
        }

        #endregion

        #region model updating/generation/regeneration

        public void updateEditorFields()
        {
            prevHeight = height;
            prevThickness = thickness;
            prevDiameter = diameter;
            prevCollider = hollowCollider;
        }

        public void prepModel()
        {
            if (model != null)
            {
                return;
            }

            String transformName = "ProcDecouplerRoot";
            Transform modelBase = part.transform.FindRecursive(transformName);
            if (modelBase != null)
            {
                GameObject.DestroyImmediate(modelBase.gameObject);
            }
            modelBase = new GameObject(transformName).transform;
            modelBase.NestToParent(part.transform.FindRecursive("model"));
            model = new ProceduralCylinderModel();

            UVMap uvs = UVMap.GetUVMapGlobal(uvMap);
            model.outsideUV = uvs.getArea("outside");
            model.insideUV = uvs.getArea("inside");
            model.topUV = uvs.getArea("top");
            model.bottomUV = uvs.getArea("top");
            setModelParameters();
            TextureData data = currentTextureSetData.textureDatas[0];
            model.setMaterial(SSTUUtils.loadMaterial(data.diffuseTextureName, data.normalTextureName));
            model.createModel();
            model.setParent(modelBase);
            updatePhysicalAttributes();
            updateDecouplerForce();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        public void recreateModel()
        {
            setModelParameters();
            model.recreateModel();
            updatePhysicalAttributes();
            updateDecouplerForce();
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }
        
        private void setModelParameters()
        {
            model.setModelParameters(diameter * 0.5f, height, thickness, cylinderSides, !hollowCollider);
        }
        
        public void updateAttachNodePositions(bool userInput)
        {
            float h = height * 0.5f;
            AttachNode topNode = part.findAttachNode("top");
            if (topNode != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, topNode, new Vector3(topNode.position.x, h, topNode.position.z), topNode.orientation, userInput);
            }
            AttachNode bottomNode = part.findAttachNode("bottom");
            if (bottomNode != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, bottomNode, new Vector3(bottomNode.position.x, -h, bottomNode.position.z), bottomNode.orientation, userInput);
            }
        }

        public void updatePhysicalAttributes()
        {
            float r = diameter * 0.5f;
            float h = height;
            float t = thickness;
            float innerCylVolume = 0;
            float outerCylVolume = 0;
            float innerCylRadius = (r) - (t);
            float outerCylRadius = (r);
            innerCylVolume = (float)Math.PI * innerCylRadius * innerCylRadius * h;
            outerCylVolume = (float)Math.PI * outerCylRadius * outerCylRadius * h;
            volume = outerCylVolume - innerCylVolume;
            modifiedMass = volume * massPerCubicMeter;
            modifiedCost = volume * costPerCubicMeter;
            part.mass = modifiedMass;
            SSTUStockInterop.fireEditorUpdate();
        }
    
        private void updateDecouplerForce()
        {
            ModuleDecouple dc = part.GetComponent<ModuleDecouple>();
            if (dc != null)
            {
                dc.ejectionForce = forcePerKg * (modifiedMass * 1000f);
                dc.Fields["ejectionForce"].guiName = "Ejection Force";
                dc.Fields["ejectionForce"].guiActiveEditor = true;
            }
        }

        #endregion
    }
}

