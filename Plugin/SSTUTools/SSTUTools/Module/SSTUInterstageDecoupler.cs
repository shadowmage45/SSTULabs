using System;
using UnityEngine;
using KSPShaderTools;

namespace SSTUTools
{
    class SSTUInterstageDecoupler : ModuleDecouple, IPartMassModifier, IPartCostModifier, IRecolorable, IContainerVolumeContributor
    {

        #region REGION - Config Fields
        //--------------------------- Engine related fields----------------------------------//

        [KSPField]
        public float thrustScalePower = 2f;

        [KSPField]
        public string engineModelRootName = "InterstageDecouplerEngineRoot";

        [KSPField]
        public int engineModuleIndex = 1;

        [KSPField]
        public string engineThrustTransformName = "SSTU-ISDC-ThrustTransform";

        [KSPField]
        public int engineContainerIndex = 0;

        //--------------------------- Fairing related fields----------------------------------//

        /// <summary>
        /// Name of the transform used for the fairing panels
        /// </summary>
        [KSPField]
        public String baseTransformName = "InterstageDecouplerFairingRoot";

        /// <summary>
        /// The cost per square meter of fairing panel
        /// </summary>
        [KSPField]
        public float costPerPanelArea = 50f;
        
        /// <summary>
        /// The mass per square meter of fairing panel
        /// </summary>
        [KSPField]
        public float massPerPanelArea = 0.025f;
        
        /// <summary>
        /// Number of quads to generate for the fairing cylinder mesh.
        /// </summary>
        [KSPField]
        public int cylinderSides = 24;

        /// <summary>
        /// Number of 'splits' in the fairing mesh.  Should be '1' for an unbroken cylinder fairing.
        /// </summary>
        [KSPField]
        public int numberOfPanels = 1;

        /// <summary>
        /// Thickness of the fairing panel walls.
        /// </summary>
        [KSPField]
        public float wallThickness = 0.05f;

        /// <summary>
        /// The index of the decoupler module for this part.
        /// </summary>
        [KSPField]
        public int upperDecouplerModuleIndex = 2;

        /// <summary>
        /// Minimum fairing diameter.
        /// </summary>
        [KSPField]
        public float minDiameter = 0.625f;

        /// <summary>
        /// Maximum fairing diameter
        /// </summary>
        [KSPField]
        public float maxDiameter = 20f;

        /// <summary>
        /// Maximum fairing height
        /// </summary>
        [KSPField]
        public float maxHeight = 10f;

        /// <summary>
        /// Increment value used for GUI widget for diameter
        /// </summary>
        [KSPField]
        public float diameterIncrement = 0.625f;

        /// <summary>
        /// Increment value used for GUI widget for height
        /// </summary>
        [KSPField]
        public float heightIncrement = 1.0f;

        /// <summary>
        /// Increment value used for GUI widget for taper location
        /// </summary>
        [KSPField]
        public float taperHeightIncrement = 1.0f;

        /// <summary>
        /// Auto-decouple and lighting of engines is delayed by this number of seconds.
        /// </summary>
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

        [KSPField(isPersistant = true, guiName = "Engine Model", guiActiveEditor = false),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentEngineModel = string.Empty;

        [KSPField(isPersistant = true, guiName = "Engine Layout", guiActiveEditor = false),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentEngineLayout = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Engine Scale"),
         UI_FloatEdit(sigFigs = 2, incrementLarge = 1f, incrementSmall = 0.25f, incrementSlide = 0.01f, minValue = 0.25f, maxValue = 2f, suppressEditorShipModified = true)]
        public float currentEngineScale = 1f;

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

        /// <summary>
        /// Fairing texture set
        /// </summary>
        [KSPField(isPersistant = true, guiName = "Texture Set", guiActiveEditor = true),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentTextureSet = String.Empty;

        /// <summary>
        /// Engine texture set
        /// </summary>
        [KSPField(isPersistant = true, guiName = "Engine Texture Set", guiActiveEditor = true),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentEngineTextureSet = String.Empty;

        #endregion ENDREGION - Config Fields

        #region REGION - Private Working Vars

        /// <summary>
        /// Persistent data for fairing recoloring
        /// </summary>
        [KSPField(isPersistant = true)]
        public String customColorData = string.Empty;

        /// <summary>
        /// Persistent data for engine recoloring
        /// </summary>
        [KSPField(isPersistant = true)]
        public String customEngineColorData = string.Empty;

        [KSPField(isPersistant = true)]
        public bool initializedColors = false;

        [Persistent]
        public string configNodeData = string.Empty;

        private float remainingDelay;

        private float minHeight;

        private float modifiedMass;
        private float modifiedCost;
        private FairingContainer fairingBase;
        private ModelModule<SSTUInterstageDecoupler> engineModels;
        private Transform engineModelRoot;

        /// <summary>
        /// Recoloring handler for fairing recoloring
        /// </summary>
        private RecoloringHandler recolorHandler;

        private ContainerFuelPreset fuelType;

        private bool initialized = false;

        private ModuleEngines engineModule;
        private ModuleDecouple decoupler;

        #endregion ENDREGION - Private Working Vars

        #region REGION - GUI Interaction

        [KSPEvent(guiName = "Invert Engines", guiActiveEditor = true)]
        public void invertEnginesEvent()
        {
            invertEngines = !invertEngines;
            this.actionWithSymmetry(m => 
            {
                m.invertEngines = invertEngines;
                m.updateEnginePositionAndScale();
            });
            SSTUStockInterop.fireEditorUpdate();
        }

        [KSPEvent(guiName = "Toggle Auto Decouple", guiActiveEditor = true)]
        public void toggleAutoDecoupleEvent()
        {
            autoDecouple = !autoDecouple;
            this.forEachSymmetryCounterpart(module => module.autoDecouple = this.autoDecouple);
        }

        #endregion ENDREGION - GUI Interaction

        #region REGION - KSP Lifecycle and Overrides

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

            Action<SSTUInterstageDecoupler> rebuild = delegate (SSTUInterstageDecoupler m)
            {
                m.updateEditorFields();
                m.buildFairing();
                m.updateEnginePositionAndScale();
                m.updateNodePositions(true);
                m.updatePartMass();
                m.updateShielding();
                m.updateDragCubes();
                m.updateFairingTextureSet(false);
            };
            Fields[nameof(currentTopDiameter)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m => 
                {
                    if (m != this) { m.currentTopDiameter = this.currentTopDiameter; }
                    rebuild(m);
                });
            };
            Fields[nameof(currentBottomDiameter)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentBottomDiameter = this.currentBottomDiameter; }
                    rebuild(m);
                });
            };
            Fields[nameof(currentHeight)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentHeight = this.currentHeight; }
                    rebuild(m);
                });
            };
            Fields[nameof(currentTaperHeight)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentTaperHeight = this.currentTaperHeight; }
                    rebuild(m);
                });
            };
            Fields[nameof(editorTransparency)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.editorTransparency = this.editorTransparency; }
                    m.fairingBase.setOpacity(m.editorTransparency ? 0.25f : 1);
                });
            };
            Fields[nameof(generateColliders)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.generateColliders = this.generateColliders; }
                    if (m.fairingBase.generateColliders != m.generateColliders)
                    {
                        m.fairingBase.generateColliders = m.generateColliders;
                        m.buildFairing();
                        m.updateFairingTextureSet(false);
                    }
                });
            };

            Fields[nameof(currentTextureSet)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.currentTextureSet = currentTextureSet;
                    m.updateFairingTextureSet(!SSTUGameSettings.persistRecolor());
                });
            };

            Fields[nameof(currentEngineModel)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                engineModels.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    //model selected action sets vars on symmetry parts
                    rebuild(m);
                    m.reInitEngineModule();
                    SSTUModInterop.updateResourceVolume(m.part);
                });
            };
            Fields[nameof(currentEngineScale)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentEngineScale = this.currentEngineScale; }
                    rebuild(m);
                    SSTUModInterop.updateResourceVolume(m.part);
                });
            };
            Fields[nameof(currentEngineLayout)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                engineModels.layoutSelected(a, b);
                this.actionWithSymmetry(m => 
                {
                    m.reInitEngineModule();
                    m.updatePartMass();
                    m.updateDragCubes();
                    SSTUModInterop.updateResourceVolume(m.part);
                });
            };
            Fields[nameof(currentEngineTextureSet)].uiControlEditor.onFieldChanged = engineModels.textureSetSelected;
            Fields[nameof(currentEngineTextureSet)].guiActiveEditor = engineModels.definition.textureSets.Length > 1;

            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorShipModified));
            SSTUModInterop.onPartGeometryUpdate(part, true);
            SSTUModInterop.updateResourceVolume(part);
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
            reInitEngineModule();
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

        public void LateUpdate()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                fairingBase.setOpacity(editorTransparency ? 0.25f : 1);
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

        //IRecolorable override
        public string[] getSectionNames()
        {
            return new string[] { "Decoupler" , "Engines"};
        }

        //IRecolorable override
        public RecoloringData[] getSectionColors(string name)
        {
            switch (name)
            {
                case "Decoupler":
                    return recolorHandler.getColorData();
                case "Engines":
                    return engineModels.recoloringData;
                default:
                    return recolorHandler.getColorData();
            }
        }

        //IRecolorable override
        public void setSectionColors(string name, RecoloringData[] colors)
        {
            switch (name)
            {
                case "Decoupler":
                    recolorHandler.setColorData(colors);
                    updateFairingTextureSet(false);
                    break;
                case "Engines":
                    engineModels.setSectionColors(colors);
                    break;
                default:
                    break;
            }
        }

        //IRecolorable override
        public TextureSet getSectionTexture(string section)
        {
            switch (name)
            {
                case "Decoupler":
                    return TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
                case "Engines":
                    return engineModels.textureSet;
                default:
                    return TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
            }
        }

        //IContainerVolumeContributor override
        public ContainerContribution[] getContainerContributions()
        {
            ContainerContribution ctBlock = new ContainerContribution("isdc", engineContainerIndex, engineModels.moduleVolume*1000f);
            ContainerContribution[] cts = new ContainerContribution[] { ctBlock };
            return cts;
        }

        #endregion ENDREGION - KSP LifeCycle and overrides

        #region REGION - Init Methods

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
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
            this.updateUIChooseOptionControl(nameof(currentTextureSet), names, titles, true, currentTextureSet);
            Fields[nameof(currentTextureSet)].guiActiveEditor = names.Length > 1;

            fuelType = VolumeContainerLoader.getPreset(fuelPreset);

            Transform modelBase = part.transform.FindRecursive("model");

            //Set up the engine models container
            ConfigNode[] modelNodes = node.GetNodes("MODEL");
            engineModelRoot = modelBase.FindOrCreate(engineModelRootName);
            ModelDefinitionLayoutOptions[] models = SSTUModelData.getModelDefinitions(modelNodes);
            engineModels = new ModelModule<SSTUInterstageDecoupler>(part, this, engineModelRoot, ModelOrientation.CENTRAL, nameof(currentEngineModel), nameof(currentEngineLayout), nameof(currentEngineTextureSet), nameof(customEngineColorData), null, null, null, null);
            engineModels.getSymmetryModule = m => m.engineModels;
            engineModels.getValidOptions = () => models;
            engineModels.getLayoutPositionScalar = () => currentBottomDiameter * 0.5f;
            //engineModels.getLayoutScaleScalar = () => currentEngineScale;
            engineModels.setupModelList(models);
            engineModels.setupModel();
            engineModels.updateSelections();
            updateEnginePositionAndScale();

            //set up the fairing container
            minHeight = engineModels.moduleHeight;
            Transform fairingContainerRoot = modelBase.FindOrCreate(baseTransformName);
            fairingBase = new FairingContainer(fairingContainerRoot.gameObject, cylinderSides, numberOfPanels, wallThickness);
            updateEditorFields();
            buildFairing();
            updateEnginePositionAndScale();
            updateFairingTextureSet(false);
            updateNodePositions(false);
            updatePartMass();
        }

        #endregion ENDREGION - Init methods

        /// <summary>
        /// Updates the UI fields for taper and height, including updating for min height from engines.
        /// </summary>
        private void updateEditorFields()
        {
            minHeight = engineModels.moduleHeight;
            if (currentTaperHeight < minHeight)
            {
                currentTaperHeight = minHeight;
            }
            else if (currentTaperHeight > currentHeight)
            {
                currentTaperHeight = currentHeight;
            }
            this.updateUIFloatEditControl(nameof(currentTaperHeight), minHeight, currentHeight, heightIncrement*2, heightIncrement, heightIncrement*0.05f, true, currentTaperHeight);
            if (currentHeight < minHeight)
            {
                currentHeight = minHeight;
            }
            this.updateUIFloatEditControl(nameof(currentHeight), minHeight, maxHeight, heightIncrement * 2, heightIncrement, heightIncrement * 0.05f, true, currentHeight);
        }

        /// <summary>
        /// Rebuild fairing for current parameters
        /// </summary>
        private void buildFairing()
        {
            MonoBehaviour.print("Rebuilding fairing.  Top: " + currentTopDiameter + " bot: " + currentBottomDiameter + " taper: " + currentTaperHeight + " height: " + currentHeight);
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
            fairingBase.setOpacity(HighLogic.LoadedSceneIsEditor && editorTransparency ? 0.25f : 1.0f);

            SSTUModInterop.onPartGeometryUpdate(part, true);
            SSTUStockInterop.fireEditorUpdate();
        }

        /// <summary>
        /// Update the engine scale and position for scale.
        /// </summary>
        private void updateEnginePositionAndScale()
        {
            engineModels.setScale(currentEngineScale);
            engineModels.root.localRotation = this.invertEngines ? Quaternion.Euler(180, 0, 0) : Quaternion.identity;
            engineModels.setPosition(-currentHeight * 0.5f + engineModels.moduleHeight * 0.5f);
            engineModels.updateModelMeshes();
            engineModels.renameEngineThrustTransforms(engineThrustTransformName);
        }

        private void updatePartMass()
        {
            float avgDiameter = currentBottomDiameter + (currentTopDiameter - currentBottomDiameter) * 0.5f;
            float panelArea = avgDiameter * Mathf.PI * currentHeight;//circumference * height = area
            
            float volume = engineModels.moduleVolume;

            float engineScaledMass = engineModels.moduleMass;
            float panelMass = massPerPanelArea * panelArea;
            modifiedMass = engineScaledMass + panelMass;

            float engineScaledCost = engineModels.moduleCost;
            float panelCost = costPerPanelArea * panelArea;
            modifiedCost = engineScaledCost + panelCost + fuelType.getResourceCost(volume);

            guiFairingCost = panelCost;
            guiFairingMass = panelMass;
        }

        private void reInitEngineModule()
        {
            engineModels.renameEngineThrustTransforms(engineThrustTransformName);
            ModuleEngines engine = part.GetComponent<ModuleEngines>();
            if (engine != null)
            {
                engine.thrustVectorTransformName = engineThrustTransformName;
                engineModels.updateEngineModuleThrust(engine, thrustScalePower);
                engine.OnStart(HighLogic.LoadedSceneIsEditor ? StartState.Editor : HighLogic.LoadedSceneIsFlight ? StartState.Flying : StartState.None);
                guiEngineThrust = engine.maxThrust;
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

        private void updateFairingTextureSet(bool useDefaults)
        {
            TextureSet s = TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
            RecoloringData[] colors = useDefaults ? s.maskColors : getSectionColors("Decoupler");
            fairingBase.enableTextureSet(currentTextureSet, colors);
            if (useDefaults)
            {
                recolorHandler.setColorData(colors);
            }
            SSTUModInterop.onPartTextureUpdated(part);
        }

    }
}
