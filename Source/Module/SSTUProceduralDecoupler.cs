using UnityEngine;
using System;
namespace SSTUTools
{
    public class SSTUProceduralDecoupler : ModuleDecouple, IPartCostModifier, IPartMassModifier
    {
        #region fields

        [KSPField]
        public bool canAdjustDiameter = false;

        [KSPField]
        public bool canAdjustThickness = false;

        [KSPField]
        public bool canAdjustHeight = false;

        [KSPField(guiActiveEditor = true, guiName = "Diameter Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
        public float diameterExtra;

        [KSPField(guiActiveEditor = true, guiName = "Height Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
        public float heightExtra;

        [KSPField(guiActiveEditor = true, guiName = "Thick Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
        public float thicknessExtra;
        
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

        [KSPField(isPersistant = true, guiName = "Diameter", guiActiveEditor = true)]
        public float diameter = 1.25f;

        [KSPField(isPersistant = true, guiName = "Height", guiActiveEditor = true)]
        public float height = 0.1f;

        [KSPField(isPersistant = true)]
        public float thickness = 0.1f;

        [KSPField(isPersistant = true, guiName = "Texture Set", guiActiveEditor = true)]
        public String currentTextureSet = String.Empty;

        private float modifiedMass = 0;
        private float modifiedCost = 0;
        private float volume = 0;

        private ProceduralCylinderModel model;

        private float editorDiameter;
        private float editorHeight;
        private float editorThickness;
        private float prevDiameterExtra;
        private float prevHeightExtra;
        private float prevThicknessExtra;

        private TextureSet currentTextureSetData;
        private TextureSet[] textureSetData;

        private TechLimitDiameter[] techLimits;
        private float techLimitMaxDiameter;

        private UVArea outsideUV = new UVArea(2, 2, 2+252, 2+60, 256);
        private UVArea insideUV = new UVArea(2, 66, 2+252, 66+60, 256);
        
        private UVArea topUV = new UVArea(0, 0.5f, 0.5f, 1f);
        private UVArea bottomUV = new UVArea(0.5f, 0.5f, 1f, 1f);

        [Persistent]
        public String configNodeData; 

        #endregion

        #region KSP GUI Actions/Events

        [KSPEvent(guiName = "Diameter ++", guiActiveEditor = true)]
        public void increaseDiameter()
        {
            setDiameterFromEditor(diameter + diameterIncrement, true);
        }

        [KSPEvent(guiName = "Diameter --", guiActiveEditor = true)]
        public void decreaseDiameter()
        {
            setDiameterFromEditor( diameter - diameterIncrement, true);
        }

        [KSPEvent(guiName = "Height +", guiActiveEditor = true)]
        public void increaseHeight()
        {
            setHeightFromEditor(height + heightIncrement, true);
        }

        [KSPEvent(guiName = "Height -", guiActiveEditor = true)]
        public void decreaseHeight()
        {
            setHeightFromEditor(height - heightIncrement, true);
        }

        [KSPEvent(guiName = "Thickness +", guiActiveEditor = true)]
        public void increaseThickness()
        {
            setThicknessFromEditor(thickness + thicknessIncrement, true);
        }

        [KSPEvent(guiName = "Thickness -", guiActiveEditor = true)]
        public void decreaseThickness()
        {
            setThicknessFromEditor(thickness - thicknessIncrement, true);
        }

        [KSPEvent(guiName = "Next Texture", guiActiveEditor = true)]
        public void nextTextureEvent()
        {
            TextureSet next = SSTUUtils.findNext(textureSetData, m => m.setName == currentTextureSet, false);
            setTextureFromEditor(next == null ? null : next.setName, true);
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
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
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
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
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
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
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
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }

        #endregion

        #region KSP Lifecycle and KSP Overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasValue("radius"))
            {
                diameter = node.GetFloatValue("radius") * 2f;
            }
            if (node.HasNode("UVMAP"))
            {
                configNodeData = node.ToString();
            }
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
            {
                loadConfigData();
                updateEditorFields();
                prepModel();
            }
        }

        public override string GetInfo()
        {
            model.destroyModel();
            SSTUUtils.destroyChildren(part.FindModelTransform("model"));//remove the original empty proxy model and any created models
            model = null;
            return "This part has configurable diameter, height, thickness, and ejection force.";
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            loadConfigData();
            updateEditorFields();
            prepModel();
            updateGuiState();
            Fields["ejectionForce"].guiName = "Ejection Force";
            Fields["ejectionForce"].guiActiveEditor = true;
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        private void loadConfigData()
        {
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            ConfigNode insideUVNode = node.GetNode("UVMAP", "name", "inside");
            ConfigNode outsideUVNode = node.GetNode("UVMAP", "name", "outside");
            ConfigNode topNode = node.GetNode("UVMAP", "name", "top");
            ConfigNode bottomNode = node.GetNode("UVMAP", "name", "bottom");
            insideUV = new UVArea(insideUVNode);
            outsideUV = new UVArea(outsideUVNode);
            topUV = new UVArea(topNode);
            bottomUV = new UVArea(bottomNode);

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

            techLimits = TechLimitDiameter.loadTechLimits(node.GetNodes("TECHLIMIT"));
            TechLimitDiameter.updateTechLimits(techLimits, out techLimitMaxDiameter);
            if (diameter * 2 > techLimitMaxDiameter)
            {
                diameter = techLimitMaxDiameter * 0.5f;
            }
        }

        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        public void onEditorVesselModified(ShipConstruct ship)
        {
            if (prevDiameterExtra != diameterExtra)
            {
                prevDiameterExtra = diameterExtra;
                setDiameterFromEditor(editorDiameter + diameterExtra * diameterIncrement, true);
            }
            if (prevHeightExtra != heightExtra)
            {
                prevHeightExtra = heightExtra;
                setHeightFromEditor(editorHeight + heightExtra * heightIncrement, true);
            }
            if (prevThicknessExtra != thicknessExtra)
            {
                prevThicknessExtra = thicknessExtra;
                setThicknessFromEditor(editorThickness + thicknessExtra * thicknessIncrement, true);
            }
        }

        public float GetModuleCost(float defaultCost)
        {
            return modifiedCost;
        }

        public float GetModuleMass(float defaultMass)
        {
            return -defaultMass + modifiedMass;
        }

        #endregion

        #region model updating/generation/regeneration

        public void updateEditorFields()
        {
            float div = diameter / diameterIncrement;
            float whole = (int)div;
            float extra = div - whole;
            editorDiameter = whole * diameterIncrement;
            diameterExtra = extra;
            prevDiameterExtra = diameterExtra;

            div = height / heightIncrement;
            whole = (int)div;
            extra = div - whole;
            editorHeight = whole * heightIncrement;
            heightExtra = extra;
            prevHeightExtra = heightExtra;

            div = thickness / thicknessIncrement;
            whole = (int)div;
            extra = div - whole;
            editorThickness = whole * thicknessIncrement;
            thicknessExtra = extra;
            prevThicknessExtra = thicknessExtra;
        }

        public void prepModel()
        {
            if (model != null)
            {
                return;
            }
            Transform tr = part.transform.FindRecursive("model");
            SSTUUtils.destroyChildren(tr);//remove the original empty proxy model, and any models that may have been attached during prefab init
            model = new ProceduralCylinderModel();
            model.outsideUV = outsideUV;
            model.insideUV = insideUV;
            model.topUV = topUV;
            model.bottomUV = bottomUV;
            updateModelParameters();
            setModelParameters();
            TextureData data = currentTextureSetData.textureDatas[0];
            model.setMaterial(SSTUUtils.loadMaterial(data.diffuseTextureName, data.normalTextureName));
            model.setMeshColliderStatus(true, false);
            model.createModel();
            model.setParent(tr);
            updatePhysicalAttributes();
            updateDecouplerForce();
            updateDragCube();
            SSTUUtils.updatePartHighlighting(part);
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        public void recreateModel()
        {
            updateModelParameters();
            setModelParameters();
            model.recreateModel();
            updatePhysicalAttributes();
            updateDecouplerForce();
            updateDragCube();
            SSTUUtils.updatePartHighlighting(part);
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        private void updateModelParameters()
        {
            prevDiameterExtra = diameterExtra;
            prevHeightExtra = heightExtra;
            prevThicknessExtra = thicknessExtra;
            diameter = editorDiameter + (diameterExtra * diameterIncrement);
            height = editorHeight + (heightExtra * heightIncrement);
            thickness = editorThickness + (thicknessExtra * thicknessIncrement);
        }

        private void setModelParameters()
        {
            model.setModelParameters(diameter * 0.5f, height, thickness, cylinderSides);
        }

        public void updateGuiState()
        {
            Events["increaseDiameter"].guiActiveEditor = Events["increaseDiameter"].guiActiveEditor = canAdjustDiameter;
            Events["increaseHeight"].guiActiveEditor = Events["decreaseHeight"].guiActiveEditor = canAdjustHeight;
            Events["increaseThickness"].guiActiveEditor = Events["decreaseThickness"].guiActiveEditor = canAdjustThickness;
            Fields["diameterExtra"].guiActiveEditor = canAdjustDiameter;
            Fields["heightExtra"].guiActiveEditor = canAdjustHeight;
            Fields["thicknessExtra"].guiActiveEditor = canAdjustThickness;
        }

        public void updateAttachNodePositions(bool userInput)
        {
            float h = height * 0.5f;
            MonoBehaviour.print("setting pdc for node height: " + h);
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
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }
    
        public void updateDragCube()
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;//NOOP except in flight
            }
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        private void updateDecouplerForce()
        {
            ejectionForce = forcePerKg * (modifiedMass * 1000f);
        }

        #endregion
    }
}

