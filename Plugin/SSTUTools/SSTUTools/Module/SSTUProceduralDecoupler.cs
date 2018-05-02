using UnityEngine;
using System;
using KSPShaderTools;

namespace SSTUTools
{
    public class SSTUProceduralDecoupler : PartModule, IPartCostModifier, IPartMassModifier, IRecolorable
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

        [KSPField(isPersistant = true)]
        public string customColorData = string.Empty;

        [KSPField(isPersistant = true)]
        public bool initializedColors = false;

        [Persistent]
        public string configNodeData = string.Empty;

        private float modifiedMass = 0;
        private float modifiedCost = 0;
        private float volume = 0;

        private ProceduralCylinderModel model;
        private RecoloringHandler recolorHandler;
        
        private float prevDiameter;
        private float prevHeight;
        private float prevThickness;
        private bool prevCollider;
                
        #endregion

        #region KSP GUI Actions/Events

        public void onTextureUpdated(BaseField field, object obj)
        {
            this.actionWithSymmetry(m =>
            {
                m.currentTextureSet = currentTextureSet;
                m.updateTextureSet(!SSTUGameSettings.persistRecolor());
            });
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

        #endregion

        #region KSP Lifecycle and KSP Overrides
        
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
            //this preps the model that will be displayed for the prefab part / editor part icon
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
            {
                loadConfigData();
                updateEditorFields();
                prepModel();
                updateTextureSet(true);
            }
        }

        public override string GetInfo()
        {
            if (model != null)
            {
                //destroy the model after the prefab part/icon part has been created
                model.destroyModel();
                model = null;
            }
            return "This part has configurable diameter, height, thickness, and ejection force.";
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            loadConfigData();
            this.updateUIFloatEditControl("height", minHeight, maxHeight, heightIncrement*2f, heightIncrement, heightIncrement*0.05f, true, height);
            this.updateUIFloatEditControl("diameter", minDiameter, maxDiameter, diameterIncrement*2f, diameterIncrement, diameterIncrement*0.05f, true, diameter);
            this.updateUIFloatEditControl("thickness", minThickness, maxThickness, thicknessIncrement*2f, thicknessIncrement, thicknessIncrement*0.05f, true, thickness);
            updateEditorFields();
            prepModel();
            Fields["height"].uiControlEditor.onFieldChanged = onHeightUpdated;
            Fields["diameter"].uiControlEditor.onFieldChanged = onDiameterUpdated;
            Fields["thickness"].uiControlEditor.onFieldChanged = onThicknessUpdated;
            Fields["hollowCollider"].uiControlEditor.onFieldChanged = onColliderUpdated;
            Fields["currentTextureSet"].uiControlEditor.onFieldChanged = onTextureUpdated;
            updateTextureSet(false);
        }

        private void loadConfigData()
        {
            recolorHandler = new RecoloringHandler(Fields[nameof(customColorData)]);
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            string[] names = node.GetStringValues("textureSet");
            string[] titles = SSTUUtils.getNames(TexturesUnlimitedLoader.getTextureSets(names), m => m.title);
            TextureSet currentTextureSetData = TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
            if (currentTextureSetData == null)
            {
                currentTextureSet = names[0];
                currentTextureSetData = TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
                initializedColors = false;
            }
            if (!initializedColors)
            {
                initializedColors = true;
                recolorHandler.setColorData(currentTextureSetData.maskColors);
            }
            Fields["currentTextureSet"].guiActiveEditor = names.Length > 1;
            this.updateUIChooseOptionControl("currentTextureSet", names, titles, true, currentTextureSet);
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

        public string[] getSectionNames()
        {
            return new string[] { "Decoupler" };
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
            model.bottomUV = uvs.getArea("bottom");
            setModelParameters();            
            model.createModel();
            model.setParent(modelBase);
            updatePhysicalAttributes();
            updateDecouplerForce();
            updateTextureSet(false);
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        public void recreateModel()
        {
            setModelParameters();
            model.recreateModel();
            updatePhysicalAttributes();
            updateDecouplerForce();
            updateTextureSet(false);
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
            AttachNode topNode = part.FindAttachNode("top");
            if (topNode != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, topNode, new Vector3(topNode.position.x, h, topNode.position.z), topNode.orientation, userInput);
            }
            AttachNode bottomNode = part.FindAttachNode("bottom");
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

        private void updateTextureSet(bool useDefaults)
        {
            TextureSet s = TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
            RecoloringData[] colors = useDefaults ? s.maskColors : getSectionColors(string.Empty);
            model.enableTextureSet(currentTextureSet, colors);
            if (useDefaults)
            {
                recolorHandler.setColorData(colors);
            }
            SSTUModInterop.onPartTextureUpdated(part);
        }

        #endregion
    }
}

