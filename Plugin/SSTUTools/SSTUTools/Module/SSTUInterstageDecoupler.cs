using System;
using UnityEngine;

namespace SSTUTools.Module
{
    class SSTUInterstageDecoupler : ModuleDecouple, IPartMassModifier, IPartCostModifier
    {
        [KSPField]
        public String modelName = "SSTU/Assets/SC-ENG-ULLAGE-A";

        [KSPField]
        public float defaultModelScale = 5f;

        [KSPField]
        public float resourceVolume = 0.25f;

        [KSPField]
        public float engineMass = 0.15f;

        [KSPField]
        public float engineThrust = 300f;

        [KSPField]
        public bool scaleThrust = true;

        [KSPField]
        public float thrustScalePower = 2f;
        
        [KSPField]
        public float baseCost = 150f;

        [KSPField]
        public float costPerPanelArea = 50f;
        
        [KSPField]
        public float massPerPanelArea = 0.025f;

        [KSPField]
        public String baseTransformName = "InterstageDecouplerRoot";
        
        [KSPField]
        public int cylinderSides = 24;

        [KSPField]
        public int numberOfPanels = 1;

        [KSPField]
        public float wallThickness = 0.05f;

        [KSPField]
        public int numberOfEngines = 4;

        [KSPField]
        public float engineRotationOffset = 90f;

        [KSPField]
        public float engineHeight = 0.8f;

        [KSPField]
        public float engineVerticalOffset = 0.4f;

        [KSPField]
        public float enginePlacementAngleOffset = 45f;

        [KSPField]
        public int engineModuleIndex = 1;

        [KSPField]
        public int upperDecouplerModuleIndex = 2;

        [KSPField]
        public float minDiameter = 0.625f;

        [KSPField]
        public float maxDiameter = 20f;

        [KSPField]
        public float maxHeight = 10f;

        [KSPField]
        public float diameterIncrement = 0.625f;

        [KSPField]
        public float heightIncrement = 1.0f;

        [KSPField]
        public float taperHeightIncrement = 1.0f;

        [KSPField]
        public float autoDecoupleDelay = 4f;
        
        [KSPField]
        public String uvMap = "NodeFairing";

        [KSPField]
        public bool shieldsParts = true;

        [KSPField]
        public string fuelPreset = "Solid";

        [KSPField(isPersistant =true, guiName = "Transparency", guiActiveEditor = true), UI_Toggle(enabledText = "Enabled", disabledText = "Disabled", suppressEditorShipModified = true)]
        public bool editorTransparency = true;

        [KSPField(guiName = "Colliders", guiActiveEditor = true, isPersistant = true), UI_Toggle(enabledText = "Enabled", disabledText = "Disabled", suppressEditorShipModified = true)]
        public bool generateColliders = false;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Height"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentHeight = 1.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Top Diam"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentTopDiameter = 2.5f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Bot. Diam"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentBottomDiameter = 2.5f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Taper Height"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentTaperHeight = 0.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Engine Scale"),
         UI_FloatEdit(sigFigs = 2, incrementLarge = 1f, incrementSmall = 0.25f, incrementSlide = 0.01f, minValue = 0.25f, maxValue = 2f, suppressEditorShipModified = true)]
        public float currentEngineScale = 1f;

        [KSPField(isPersistant = true)]
        public bool initializedResources = false;
        
        [KSPField(guiActiveEditor = true, guiActive = true, guiName = "Total Thrust")]
        public float guiEngineThrust;

        [KSPField(guiActiveEditor = true, guiActive = true, guiName = "Fairing Mass")]
        public float guiFairingMass;

        [KSPField(guiActiveEditor = true, guiActive = true, guiName = "Fairing Cost")]
        public float guiFairingCost;

        [KSPField(isPersistant = true)]
        public bool invertEngines = false;

        [KSPField(isPersistant = true, guiName = "Auto Decouple", guiActive =true, guiActiveEditor =true)]
        public bool autoDecouple = false;

        [KSPField(isPersistant = true, guiName = "Texture Set", guiActiveEditor = true),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentTextureSet = String.Empty;

        [Persistent]
        public string configNodeData = string.Empty;

        private float remainingDelay;

        private float minHeight;

        private float modifiedMass;
        private float modifiedCost;

        private TextureSet currentTextureSetData;
        private TextureSet[] textureSetData;
        private Material fairingMaterial;
        private InterstageDecouplerModel fairingBase;
        private InterstageDecouplerEngine[] engineModels;

        private ContainerFuelPreset fuelType;

        private bool initialized = false;

        private ModuleEngines engineModule;
        private ModuleDecouple decoupler;

        #region REGION - GUI Interaction

        [KSPEvent(guiName = "Invert Engines", guiActiveEditor = true)]
        public void invertEnginesEvent()
        {
            invertEngines = !invertEngines;
            updateEnginePositionAndScale();
            this.forEachSymmetryCounterpart(module =>
            {
                module.invertEngines = this.invertEngines;
                module.updateEnginePositionAndScale();
            });
            SSTUStockInterop.fireEditorUpdate();
        }

        [KSPEvent(guiName = "Toggle Auto Decouple", guiActiveEditor = true)]
        public void toggleAutoDecoupleEvent()
        {
            autoDecouple = !autoDecouple;
            this.forEachSymmetryCounterpart(module => module.autoDecouple = this.autoDecouple);
        }

        [KSPEvent(guiName = "Next Texture", guiActiveEditor = true)]
        public void nextTextureEvent()
        {
            TextureSet next = SSTUUtils.findNext(textureSetData, m => m.name == currentTextureSet, false);
            currentTextureSet = next.name;
            onTextureUpdated(null, null);
        }

        public void onTextureUpdated(BaseField field, object obj)
        {
            textureWasUpdated();
            this.forEachSymmetryCounterpart(module =>
            {
                module.currentTextureSet = this.currentTextureSet;
                module.textureWasUpdated();
            });
        }
        
        public void onTopDiameterUpdated(BaseField field, object obj)
        {
            dimensionsWereUpdated();
            this.forEachSymmetryCounterpart(module =>
            {
                module.currentTopDiameter = this.currentTopDiameter;
                module.dimensionsWereUpdated();
            });
        }

        public void onBottomDiameterUpdated(BaseField field, object obj)
        {
            dimensionsWereUpdatedWithEngineRecalc();
            this.forEachSymmetryCounterpart(module =>
            {
                module.currentBottomDiameter = this.currentBottomDiameter;
                module.dimensionsWereUpdatedWithEngineRecalc();
            });
        }

        public void onHeightUpdated(BaseField field, object obj)
        {
            dimensionsWereUpdated();
            this.forEachSymmetryCounterpart(module =>
            {
                module.currentHeight = this.currentHeight;
                module.dimensionsWereUpdated();
            });
        }

        public void onStraightUpdated(BaseField field, object obj)
        {
            dimensionsWereUpdated();
            this.forEachSymmetryCounterpart(module =>
            {
                module.currentTaperHeight = this.currentTaperHeight;
                module.dimensionsWereUpdated();
            });
        }

        public void onEngineScaleUpdated(BaseField field, object obj)
        {
            dimensionsWereUpdatedWithEngineRecalc();
            this.forEachSymmetryCounterpart(module =>
            {
                module.currentEngineScale = this.currentEngineScale;
                module.dimensionsWereUpdatedWithEngineRecalc();
            });
        }

        public void onTransparencyUpdated(BaseField field, object obj)
        {
            fairingBase.setOpacity(editorTransparency ? 0.25f : 1);
        }

        public void onCollidersUpdated(BaseField field, object obj)
        {
            if (fairingBase.generateColliders != this.generateColliders)
            {
                fairingBase.generateColliders = this.generateColliders;
                buildFairing();
            }
        }

        private void dimensionsWereUpdated()
        {
            updateEditorFields();
            buildFairing();
            updateNodePositions(true);
            updatePartMass();
            updateShielding();
            updateDragCubes();
        }

        private void dimensionsWereUpdatedWithEngineRecalc()
        {
            updateEditorFields();
            buildFairing();
            updateNodePositions(true);
            updateResources();
            updatePartMass();
            updateEngineThrust();
            updateShielding();
            updateDragCubes();
        }

        private void textureWasUpdated()
        {
            currentTextureSetData = Array.Find(textureSetData, m => m.name == currentTextureSet);
            if (currentTextureSetData == null)
            {
                currentTextureSetData = textureSetData[0];
                currentTextureSet = currentTextureSetData.name;
            }
            currentTextureSetData.enable(fairingBase.rootObject, Color.clear);
        }

        #endregion ENDREGION - GUI Interaction

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
            this.updateUIFloatEditControl(nameof(currentTopDiameter), minDiameter, maxDiameter, diameterIncrement*2, diameterIncrement, diameterIncrement*0.05f, true, currentTopDiameter);
            this.updateUIFloatEditControl(nameof(currentBottomDiameter), minDiameter, maxDiameter, diameterIncrement*2, diameterIncrement, diameterIncrement * 0.05f, true, currentBottomDiameter);
            this.updateUIFloatEditControl(nameof(currentHeight), minHeight, maxHeight, heightIncrement*2, heightIncrement, heightIncrement*0.05f, true, currentHeight);
            this.updateUIFloatEditControl(nameof(currentTaperHeight), minHeight, maxHeight, heightIncrement*2, heightIncrement, heightIncrement * 0.05f, true, currentTaperHeight);
            Fields[nameof(currentTopDiameter)].uiControlEditor.onFieldChanged = onTopDiameterUpdated;
            Fields[nameof(currentBottomDiameter)].uiControlEditor.onFieldChanged = onBottomDiameterUpdated;
            Fields[nameof(currentHeight)].uiControlEditor.onFieldChanged = onHeightUpdated;
            Fields[nameof(currentTaperHeight)].uiControlEditor.onFieldChanged = onStraightUpdated;
            Fields[nameof(editorTransparency)].uiControlEditor.onFieldChanged = onTransparencyUpdated;
            Fields[nameof(generateColliders)].uiControlEditor.onFieldChanged = onCollidersUpdated;
            Fields[nameof(currentTextureSet)].uiControlEditor.onFieldChanged = onTextureUpdated;
            Fields[nameof(currentTextureSet)].guiActiveEditor = textureSetData.Length > 1;
            Fields[nameof(currentEngineScale)].uiControlEditor.onFieldChanged = onEngineScaleUpdated;
            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorShipModified));
        }

        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorShipModified));
        }

        public void onEditorShipModified(ShipConstruct ship)
        {
            if (!HighLogic.LoadedSceneIsEditor) { return; }
            fairingBase.setOpacity(editorTransparency ? 0.25f : 1);
        }

        public void Start()
        {
            engineModule = part.GetComponent<ModuleEngines>();
            ModuleDecouple[] decouplers = part.GetComponents<ModuleDecouple>();
            foreach (ModuleDecouple dc in decouplers)
            {
                if (dc != this) { decoupler = dc; break; }
            }
            updateEngineThrust();
            updateShielding();
            Events[nameof(ToggleStaging)].advancedTweakable = false;
            decoupler.Events[nameof(ToggleStaging)].advancedTweakable = false;
        }

        public void FixedUpdate()
        {
            if (remainingDelay > 0)
            {
                remainingDelay -= TimeWarp.fixedDeltaTime;
                if (remainingDelay <= 0)
                {
                    if (!isDecoupled) { Decouple(); }
                    if (!decoupler.isDecoupled) { decoupler.Decouple(); }
                }
            }
            else if (autoDecouple && engineModule.flameout)
            {
                if (autoDecoupleDelay > 0)
                {
                    remainingDelay = autoDecoupleDelay;
                }
                else
                {
                    if (!isDecoupled) { Decouple(); }
                    if (!decoupler.isDecoupled) { decoupler.Decouple(); }
                }
            }
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            return -defaultMass + modifiedMass;
        }

        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            return -defaultCost + modifiedCost;
        }
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            ConfigNode[] textureNodes = node.GetNodes("TEXTURESET");
            textureSetData = TextureSet.loadGlobalTextureSets(textureNodes);
            currentTextureSetData = Array.Find(textureSetData, m => m.name == currentTextureSet);
            if (currentTextureSetData == null)
            {
                currentTextureSetData = textureSetData[0];
                currentTextureSet = currentTextureSetData.name;
            }
            int len = textureSetData.Length;
            string[] textureSetNames = new string[len];
            for (int i = 0; i < len; i++)
            {
                textureSetNames[i] = textureSetData[i].name;
            }
            this.updateUIChooseOptionControl("currentTextureSet", textureSetNames, textureSetNames, true, currentTextureSet);

            TextureSetMaterialData data = currentTextureSetData.textureData[0];
            fairingMaterial = data.createMaterial("SSTUFairingMaterial");

            fuelType = VolumeContainerLoader.getPreset(fuelPreset);

            Transform modelBase = part.transform.FindRecursive("model");
            setupEngineModels(modelBase);
            minHeight = engineHeight * getEngineScale();
            Transform root = modelBase.FindOrCreate(baseTransformName);
            Transform collider = modelBase.FindOrCreate("InterstageFairingBaseCollider");
            fairingBase = new InterstageDecouplerModel(root.gameObject, collider.gameObject, 0.25f, cylinderSides, numberOfPanels, wallThickness);
            updateEditorFields();
            buildFairing();
            updateNodePositions(false);
            if (!initializedResources && (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
            {
                initializedResources = true;
                updateResources();
            }
            updatePartMass();
            updateEngineThrust();
        }

        private void updateEditorFields()
        {
            minHeight = engineHeight * getEngineScale();
            if (currentTaperHeight < minHeight)
            {
                currentTaperHeight = minHeight;
            }
            else if (currentTaperHeight > currentHeight)
            {
                currentTaperHeight = currentHeight;
            }
            this.updateUIFloatEditControl("currentTaperHeight", minHeight, currentHeight, heightIncrement*2, heightIncrement, heightIncrement*0.05f, true, currentTaperHeight);
            if (currentHeight < minHeight)
            {
                currentHeight = minHeight;
            }
            this.updateUIFloatEditControl("currentHeight", minHeight, maxHeight, heightIncrement * 2, heightIncrement, heightIncrement * 0.05f, true, currentHeight);
        }

        private void setupEngineModels(Transform modelBase)
        {
            engineModels = new InterstageDecouplerEngine[numberOfEngines];
            float anglePerEngine = 360f / (float)numberOfEngines;
            float startAngle = enginePlacementAngleOffset;
            Transform modelTransform;
            String fullName;
            float placementAngle;
            for (int i = 0; i < numberOfEngines; i++)
            {
                placementAngle = startAngle + ((float)i * anglePerEngine);
                fullName = modelName + "-" + i;
                modelTransform = modelBase.FindRecursive(fullName);
                if (modelTransform == null)
                {
                    modelTransform = SSTUUtils.cloneModel(modelName).transform;
                    modelTransform.name = fullName;
                    modelTransform.gameObject.name = fullName;
                }
                modelTransform.parent = modelBase;
                engineModels[i] = new InterstageDecouplerEngine(modelTransform, placementAngle, engineRotationOffset);
            }
        }

        private void buildFairing()
        {
            fairingBase.clearProfile();
            
            UVMap uvs = UVMap.GetUVMapGlobal(uvMap);
            fairingBase.outsideUV = uvs.getArea("outside");
            fairingBase.insideUV = uvs.getArea("inside");
            fairingBase.edgesUV = uvs.getArea("edges");

            float halfHeight = currentHeight * 0.5f;

            fairingBase.addRing(-halfHeight, currentBottomDiameter * 0.5f);
            if (currentTopDiameter != currentBottomDiameter)
            {
                fairingBase.addRing(-halfHeight + currentTaperHeight, currentBottomDiameter * 0.5f);
            }
            if (currentHeight != currentTaperHeight || currentTopDiameter == currentBottomDiameter)
            {
                fairingBase.addRing(halfHeight, currentTopDiameter * 0.5f);
            }
            fairingBase.generateColliders = this.generateColliders;
            fairingBase.generateFairing();
            fairingBase.setMaterial(fairingMaterial);
            fairingBase.setOpacity(HighLogic.LoadedSceneIsEditor && editorTransparency ? 0.25f : 1.0f);

            updateEnginePositionAndScale();
            SSTUModInterop.onPartGeometryUpdate(part, true);
            SSTUStockInterop.fireEditorUpdate();
        }

        private void updateEnginePositionAndScale()
        {
            float newScale = getEngineScale();
            float yPos = -(currentHeight * 0.5f) + (newScale * engineVerticalOffset);
            foreach(InterstageDecouplerEngine engine in engineModels)
            {
                engine.reposition(newScale, currentBottomDiameter * 0.5f, yPos, invertEngines);
            }
        }

        private void updateResources()
        {
            float scale = Mathf.Pow(getEngineScale(), thrustScalePower);
            float volume = resourceVolume * scale * numberOfEngines;
            if (!SSTUModInterop.onPartFuelVolumeUpdate(part, volume*1000))
            {
                SSTUResourceList resources = new SSTUResourceList();
                fuelType.addResources(resources, volume);
                resources.setResourcesToPart(part);
            }
        }

        private void updatePartMass()
        {
            float avgDiameter = currentBottomDiameter + (currentTopDiameter - currentBottomDiameter) * 0.5f;
            float panelArea = avgDiameter * Mathf.PI * currentHeight;//circumference * height = area

            float scale = getEngineScale();
            scale = Mathf.Pow(scale, 3);

            float escale = Mathf.Pow(getEngineScale(), thrustScalePower);
            float volume = resourceVolume * escale * numberOfEngines;

            float engineScaledMass = engineMass * scale;
            float panelMass = massPerPanelArea * panelArea;
            modifiedMass = engineScaledMass + panelMass;

            float engineScaledCost = baseCost * scale;
            float panelCost = costPerPanelArea * panelArea;
            modifiedCost = engineScaledCost + panelCost + fuelType.getResourceCost(volume);

            guiFairingCost = panelCost;
            guiFairingMass = panelMass;
        }

        private void updateEngineThrust()
        {
            ModuleEngines engine = part.GetComponent<ModuleEngines>();
            if (engine != null)
            {
                float scale = getEngineScale();
                float thrustScalar = Mathf.Pow(scale, thrustScalePower);
                float thrustPerEngine = engineThrust * thrustScalar;
                float totalThrust = thrustPerEngine * numberOfEngines;
                guiEngineThrust = totalThrust;
                SSTUStockInterop.updateEngineThrust(engine, engine.minThrust, totalThrust);
            }
            else
            {
                print("Cannot update engine thrust -- no engine module found!");
                guiEngineThrust = 0;
            }
        }

        private void updateNodePositions(bool userInput)
        {
            float h = currentHeight * 0.5f;
            SSTUAttachNodeUtils.updateAttachNodePosition(part, part.FindAttachNode("top"), new Vector3(0, h, 0), Vector3.up, userInput);
            SSTUAttachNodeUtils.updateAttachNodePosition(part, part.FindAttachNode("bottom"), new Vector3(0, -h, 0), Vector3.down, userInput);
        }

        private void updateDragCubes()
        {
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        private void updateShielding()
        {
            if (!shieldsParts) { return; }
            SSTUAirstreamShield shield = part.GetComponent<SSTUAirstreamShield>();
            if (shield != null)
            {
                shield.addShieldArea("ISDC-Shielding", currentTopDiameter * 0.5f, currentTopDiameter * 0.5f, currentHeight * 0.5f, -currentHeight * 0.5f, true, true);
            }
        }

        private float getEngineScale()
        {
            return currentBottomDiameter / defaultModelScale * currentEngineScale;
        }
        
        private class InterstageDecouplerEngine
        {
            Transform model;
            float angle;
            float offsetAngle;

            public InterstageDecouplerEngine(Transform model, float placementAngle, float rotationOffset)
            {
                this.model = model;
                this.angle = placementAngle;
                this.offsetAngle = rotationOffset;
            }
            
            public void reposition(float scale, float radius, float yPos, bool invert)
            {
                model.transform.rotation = model.transform.parent.rotation;
                float x = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
                float z = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
                model.transform.localPosition = new Vector3(x, yPos, z);
                model.Rotate(Vector3.up, angle + offsetAngle, Space.Self);
                if (invert)
                {
                    model.Rotate(Vector3.left, 180f, Space.Self);//x-axis is default for rcs blocks -- TODO add config to use other axis
                }
                model.transform.localScale = new Vector3(scale, scale, scale);
            }            
        }

        private class InterstageDecouplerModel : FairingContainer
        {
            private GameObject collider;
            private float colliderHeight;
            
            public InterstageDecouplerModel(GameObject root, GameObject collider, float colliderHeight, int cylinderFaces, int numberOfPanels, float thickness) : base(root, cylinderFaces, numberOfPanels, thickness)
            {
                this.collider = collider;
                this.colliderHeight = colliderHeight;
            }

            public override void generateFairing()
            {
                base.generateFairing();
                rebuildCollider();
            }

            //TODO
            private void rebuildCollider()
            {
                //throw new NotImplementedException();
            }
        }

    }
}
