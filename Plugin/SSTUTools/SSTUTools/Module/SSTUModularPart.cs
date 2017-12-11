using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSPShaderTools;

namespace SSTUTools
{
    public class SSTUModularPart : PartModule, IPartCostModifier, IPartMassModifier
    {
        #region REGION - Standard Part Config Fields

        [KSPField]
        public float diameterIncrement = 0.625f;

        [KSPField]
        public float minDiameter = 0.625f;

        [KSPField]
        public float maxDiameter = 10f;

        [KSPField]
        public float fairingRatio = 1.0f;

        [KSPField]
        public bool useAdapterVolume = false;

        [KSPField]
        public bool useAdapterMass = false;

        [KSPField]
        public bool useAdapterCost = false;

        [KSPField]
        public int solarAnimationLayer = 1;

        [KSPField]
        public int noseAnimationLayer = 3;

        [KSPField]
        public int coreAnimationLayer = 5;

        [KSPField]
        public int mountAnimationLayer = 7;

        [KSPField]
        public string rcsThrustTransformName = "RCSThrustTransform";

        [KSPField]
        public string topManagedNodes = "top1, top2";

        [KSPField]
        public string bottomManagedNodes = "bottom1, bottom2";

        /// <summary>
        /// Name of the 'interstage' node; positioned depending on mount interstage location (CB) / bottom of the upper tank (ST).
        /// </summary>
        [KSPField]
        public String noseInterstageNode = "noseInterstage";

        /// <summary>
        /// Name of the 'interstage' node; positioned depending on mount interstage location (CB) / bottom of the upper tank (ST).
        /// </summary>
        [KSPField]
        public String mountInterstageNode = "mountInterstage";

        //persistent config fields for module selections
        //also GUI controls for module selection

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Diameter"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentDiameter = 2.5f;

        //persistence and UI controls for module/model selection (top, core, bottom, solar, RCS)
        [KSPField(isPersistant = true, guiName = "Top"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentTop = "Mount-None";

        [KSPField(isPersistant = true, guiName = "Core"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCore = "Mount-None";

        [KSPField(isPersistant = true, guiName = "Bottom"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentBottom = "Mount-None";

        [KSPField(isPersistant = true, guiName = "Solar"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentSolar = "Solar-None";

        [KSPField(isPersistant = true, guiName = "RCS"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentRCS = "MUS-RCS1";

        //vertical offset control for RCS models
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "RCS V.Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentRCSOffset = 0f;

        //persistent config fields for module texture sets
        //also GUI controls for texture selection
        [KSPField(isPersistant = true, guiName = "Top Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentTopTexture = "default";

        [KSPField(isPersistant = true, guiName = "Core Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCoreTexture = "default";

        [KSPField(isPersistant = true, guiName = "Bottom Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentBottomTexture = "default";

        //persistent config fields and UI controls for deploy limits for nose, core, and mount animation modules
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Top Deploy Limit"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float topAnimationDeployLimit = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Core Deploy Limit"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float coreAnimationDeployLimit = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Bottom Deploy Limit"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float bottomAnimationDeployLimit = 1f;

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Solar:")]
        public string solarPanelStatus = string.Empty;

        //persistent data for modules; stores colors and other per-module data
        [KSPField(isPersistant = true)]
        public string topModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string coreModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string bottomModulePersistentData = string.Empty;

        //persistence data for animation modules, stores animation state
        [KSPField(isPersistant = true)]
        public string topAnimationPersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string coreAnimationPersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string bottomAnimationPersistentData = string.Empty;

        //persistence data for solar module, stores animation state and rotation cache
        [KSPField(isPersistant = true)]
        public string solarAnimationPersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string solarRotationPersistentData = string.Empty;

        //tracks if default textures and resource volumes have been initialized; only occurs once during the parts' first Start() call
        [KSPField(isPersistant = true)]
        public bool initializedDefaults = false;

        //standard work-around for lack of config-node data being passed consistently and lack of support for mod-added serializable classes
        [Persistent]
        public string configNodeData = string.Empty;

        #endregion REGION - Standard Part Config Fields

        #region REGION - Private working vars

        private bool initialized = false;
        private float rcsThrust = -1;
        private float modifiedMass = 0;
        private float modifiedCost = 0;
        private string[] topNodeNames;
        private string[] bottomNodeNames;

        ModelModule<SingleModelData, SSTUModularPart> topModule;
        ModelModule<ServiceModuleCoreModel, SSTUModularPart> coreModule;
        ModelModule<SingleModelData, SSTUModularPart> bottomModule;
        ModelModule<SolarModelData, SSTUModularPart> solarModule;
        ModelModule<ServiceModuleRCSModelData, SSTUModularPart> rcsModule;

        AnimationModule<SSTUModularPart> topAnimationModule;
        AnimationModule<SSTUModularPart> coreAnimationModule;
        AnimationModule<SSTUModularPart> bottomAnimationModule;

        SolarModule<SSTUModularPart> solarPanelModule;

        /// <summary>
        /// ref to the ModularRCS module that updates fuel type and thrust for RCS
        /// </summary>
        private SSTUModularRCS modularRCSControl;

        #endregion ENDREGION - Private working vars

        #region REGION - UI Controls

        [KSPEvent]
        public void topDeployEvent() { topAnimationModule.onDeployEvent(); }

        [KSPEvent]
        public void coreDeployEvent() { coreAnimationModule.onDeployEvent(); }

        [KSPEvent]
        public void bottomDeployEvent() { bottomAnimationModule.onDeployEvent(); }

        [KSPEvent]
        public void topRetractEvent() { topAnimationModule.onRetractEvent(); }

        [KSPEvent]
        public void coreRetractEvent() { coreAnimationModule.onRetractEvent(); }

        [KSPEvent]
        public void bottomRetractEvent() { bottomAnimationModule.onRetractEvent(); }

        [KSPAction]
        public void topToggleAction(KSPActionParam param) { topAnimationModule.onToggleAction(param); }

        [KSPAction]
        public void coreToggleAction(KSPActionParam param) { coreAnimationModule.onToggleAction(param); }

        [KSPAction]
        public void bottomToggleAction(KSPActionParam param) { bottomAnimationModule.onToggleAction(param); }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Deploy Solar Panels")]
        public void solarDeployEvent() { solarPanelModule.onDeployEvent(); }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Retract Solar Panels")]
        public void solarRetractEvent() { solarPanelModule.onRetractEvent(); }

        [KSPAction]
        public void solarToggleAction(KSPActionParam param) { solarPanelModule.onToggleAction(param); }

        #endregion ENDREGION - UI Controls

        #region REGION - Standard KSP Overrides

        //standard KSP lifecyle override
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize(false);
        }

        //standard KSP lifecyle override
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize(true);

            Action<SSTUModularPart> modelChangedAction = delegate (SSTUModularPart m)
            {
                m.updateModulePositions();
                m.updateMassAndCost();
                m.updateAttachNodes(true);
                m.updateDragCubes();
                m.updateResourceVolume();
                m.updateFairing(true);
                m.updateGUI();
                SSTUModInterop.onPartGeometryUpdate(m.part, true);
            };

            Fields[nameof(currentDiameter)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                this.actionWithSymmetry(m =>
                {
                    modelChangedAction(m);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentTop)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                topModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                this.actionWithSymmetry(m =>
                {
                    m.topAnimationModule.setupAnimations(m.topModule.animationData, m.topModule.root, noseAnimationLayer);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentCore)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                coreModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                if (!coreModule.model.isValidSolarOption(currentSolar, coreModule.model.currentDiameterScale))
                {
                    this.actionWithSymmetry(m =>
                    {
                        m.currentSolar = m.coreModule.model.getAvailableSolarVariants(coreModule.model.currentDiameterScale)[0];
                        m.solarModule.modelSelected(m.currentSolar);
                        modelChangedAction(m);
                        m.solarPanelModule.setupAnimations(m.solarModule.animationData, m.solarModule.root, solarAnimationLayer);
                        m.solarPanelModule.setupSolarPanelData(m.solarModule.model.getSolarData(), m.solarModule.root);
                    });
                }
                this.actionWithSymmetry(m =>
                {
                    m.coreAnimationModule.setupAnimations(m.coreModule.animationData, m.coreModule.root, m.coreAnimationLayer);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentBottom)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                bottomModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                this.actionWithSymmetry(m =>
                {
                    m.bottomAnimationModule.setupAnimations(m.bottomModule.animationData, m.bottomModule.root, m.mountAnimationLayer);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentSolar)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                solarModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    modelChangedAction(m);
                    m.solarPanelModule.setupAnimations(m.solarModule.animationData, m.solarModule.root, solarAnimationLayer);
                    m.solarPanelModule.setupSolarPanelData(m.solarModule.model.getSolarData(), m.solarModule.root);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentRCS)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                rcsModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.rcsModule.model.renameThrustTransforms(rcsThrustTransformName);
                    modelChangedAction(m);
                    m.updateRCSModule();
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentRCSOffset)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    modelChangedAction(m);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            if (maxDiameter == minDiameter)
            {
                Fields[nameof(currentDiameter)].guiActiveEditor = false;
            }
            else
            {
                this.updateUIFloatEditControl(nameof(currentDiameter), minDiameter, maxDiameter, diameterIncrement * 2, diameterIncrement, diameterIncrement * 0.05f, true, currentDiameter);
            }

            Fields[nameof(currentTopTexture)].uiControlEditor.onFieldChanged = topModule.textureSetSelected;
            Fields[nameof(currentCoreTexture)].uiControlEditor.onFieldChanged = coreModule.textureSetSelected;
            Fields[nameof(currentBottomTexture)].uiControlEditor.onFieldChanged = bottomModule.textureSetSelected;

            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
            updateDragCubes();
        }

        //standard KSP lifecycle override
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            //animation persistence is updated on state change
            //but rotations are updated every frame, so it is not feasible to update string-based persistence data (without excessive garbage generation)
            if (solarPanelModule != null)
            {
                solarPanelModule.updateSolarPersistence();
                node.SetValue(nameof(solarRotationPersistentData), solarRotationPersistentData, true);
            }
        }

        //standard Unity lifecyle override
        public void Start()
        {
            if (!initializedDefaults)
            {
                updateResourceVolume();
                updateFairing(false);
            }
            initializedDefaults = true;
            updateRCSModule();
        }

        //standard Unity lifecyle override
        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        //standard Unity lifecyle override
        public void Update()
        {
            //TODO -- call Update() on ModelModules
        }

        //standard Unity lifecyle override
        public void FixedUpdate()
        {
            //TODO -- call FixedUpdate() on ModelModules
        }

        //KSP editor modified event callback
        private void onEditorVesselModified(ShipConstruct ship)
        {
            //TODO -- update UI for change in available options based on occupied attach nodes
            updateGUI();
        }

        //IPartMass/CostModifier override
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        //IPartMass/CostModifier override
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        //IPartMass/CostModifier override
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (modifiedMass == 0) { return 0; }
            return -defaultMass + modifiedMass;
        }

        //IPartMass/CostModifier override
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (modifiedCost == 0) { return 0; }
            return -defaultCost + modifiedCost;
        }

        //IRecolorable override
        public string[] getSectionNames()
        {
            return new string[] { "Top", "Body", "Bottom" };
        }

        //IRecolorable override
        public RecoloringData[] getSectionColors(string section)
        {
            if (section == "Top")
            {
                return topModule.customColors;
            }
            else if (section == "Body")
            {
                return coreModule.customColors;
            }
            else if (section == "Bottom")
            {
                return bottomModule.customColors;
            }
            return coreModule.customColors;
        }

        //IRecolorable override
        public void setSectionColors(string section, RecoloringData[] colors)
        {
            if (section == "Top")
            {
                topModule.setSectionColors(colors);
            }
            else if (section == "Body")
            {
                coreModule.setSectionColors(colors);
            }
            else if (section == "Bottom")
            {
                bottomModule.setSectionColors(colors);
            }
        }

        //IRecolorable override
        public TextureSet getSectionTexture(string section)
        {
            if (section == "Top")
            {
                return topModule.currentTextureSet;
            }
            else if (section == "Body")
            {
                return coreModule.currentTextureSet;
            }
            else if (section == "Bottom")
            {
                return bottomModule.currentTextureSet;
            }
            return coreModule.currentTextureSet;
        }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Custom Update Methods

        private void initialize(bool start)
        {
            if (initialized) { return; }
            initialized = true;

            //model-module setup/initialization
            topNodeNames = SSTUUtils.parseCSV(topManagedNodes);
            bottomNodeNames = SSTUUtils.parseCSV(bottomManagedNodes);

            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);

            coreModule = new ModelModule<ServiceModuleCoreModel, SSTUModularPart>(part, this, getRootTransform("MSC-CORE", true), ModelOrientation.TOP, nameof(coreModulePersistentData), nameof(currentCore), nameof(currentCoreTexture));
            coreModule.getSymmetryModule = m => m.coreModule;
            coreModule.setupModelList(ModelData.parseModels(node.GetNodes("CORE"), m => new ServiceModuleCoreModel(m)));

            topModule = new ModelModule<SingleModelData, SSTUModularPart>(part, this, getRootTransform("MSC-TOP", true), ModelOrientation.TOP, nameof(topModulePersistentData), nameof(currentTop), nameof(currentTopTexture));
            topModule.getSymmetryModule = m => m.topModule;
            topModule.getValidSelections = m => topModule.models.FindAll(s => s.canSwitchTo(part, topNodeNames));

            bottomModule = new ModelModule<SingleModelData, SSTUModularPart>(part, this, getRootTransform("MSC-BOTTOM", true), ModelOrientation.BOTTOM, nameof(bottomModulePersistentData), nameof(currentBottom), nameof(currentBottomTexture));
            bottomModule.getSymmetryModule = m => m.bottomModule;
            bottomModule.getValidSelections = m => bottomModule.models.FindAll(s => s.canSwitchTo(part, bottomNodeNames));

            solarModule = new ModelModule<SolarModelData, SSTUModularPart>(part, this, getRootTransform("MSC-Solar", true), ModelOrientation.CENTRAL, null, nameof(currentSolar), null);
            solarModule.getSymmetryModule = m => m.solarModule;
            solarModule.setupModelList(ModelData.parseModels(node.GetNodes("SOLAR"), m => new SolarModelData(m)));
            solarModule.getValidSelections = delegate (IEnumerable<SolarModelData> all)
            {
                //System.Linq.Enumerable.Where(all, s => s.isAvailable(upgradesApplied));
                float scale = coreModule.model.currentDiameterScale;
                //find all solar panels that are unlocked via upgrades/tech-tree
                List<SolarModelData> unlocked = solarModule.models.FindAll(s => s.isAvailable(upgradesApplied));
                //filter those to find only the ones available for the current
                List<SolarModelData> availableByScale = unlocked.FindAll(s => coreModule.model.isValidSolarOption(s.name, scale));
                return availableByScale;
            };
            solarModule.preModelSetup = delegate (SolarModelData d)
            {
                d.positions = coreModule.model.getPanelConfiguration(d.name).positions;
            };

            rcsModule = new ModelModule<ServiceModuleRCSModelData, SSTUModularPart>(part, this, getRootTransform("MSC-Rcs", true), ModelOrientation.CENTRAL, null, nameof(currentRCS), null);
            rcsModule.getSymmetryModule = m => m.rcsModule;
            rcsModule.setupModelList(ModelData.parseModels(node.GetNodes("RCS"), m => new ServiceModuleRCSModelData(m)));
            rcsModule.getValidSelections = m => rcsModule.models.FindAll(s => s.isAvailable(upgradesApplied));

            List<ConfigNode> tops = new List<ConfigNode>();
            List<ConfigNode> bottoms = new List<ConfigNode>();
            ConfigNode[] mNodes = node.GetNodes("CAP");
            ConfigNode mNode;
            int len = mNodes.Length;
            for (int i = 0; i < len; i++)
            {
                mNode = mNodes[i];
                if (mNode.GetBoolValue("useForTop", true)) { tops.Add(mNode); }
                if (mNode.GetBoolValue("useForBottom", true)) { bottoms.Add(mNode); }
            }
            topModule.setupModelList(SingleModelData.parseModels(tops.ToArray()));
            bottomModule.setupModelList(SingleModelData.parseModels(bottoms.ToArray()));
            tops.Clear();
            bottoms.Clear();

            //model-module model-creation
            topModule.setupModel();
            coreModule.setupModel();
            coreModule.model.updateScaleForDiameter(currentDiameter);
            bottomModule.setupModel();
            solarModule.setupModel();
            rcsModule.setupModel();
            rcsModule.model.renameThrustTransforms(rcsThrustTransformName);

            //animation-module setup/initialization
            topAnimationModule = new AnimationModule<SSTUModularPart>(part, this, Fields[nameof(topAnimationPersistentData)], Fields[nameof(topAnimationDeployLimit)], Events[nameof(topDeployEvent)], Events[nameof(topRetractEvent)]);
            topAnimationModule.getSymmetryModule = m => m.topAnimationModule;
            topAnimationModule.setupAnimations(topModule.animationData, topModule.root, noseAnimationLayer);

            coreAnimationModule = new AnimationModule<SSTUModularPart>(part, this, Fields[nameof(coreAnimationPersistentData)], Fields[nameof(coreAnimationDeployLimit)], Events[nameof(coreDeployEvent)], Events[nameof(coreRetractEvent)]);
            coreAnimationModule.getSymmetryModule = m => m.coreAnimationModule;
            coreAnimationModule.setupAnimations(coreModule.animationData, coreModule.root, coreAnimationLayer);

            bottomAnimationModule = new AnimationModule<SSTUModularPart>(part, this, Fields[nameof(bottomAnimationPersistentData)], Fields[nameof(bottomAnimationDeployLimit)], Events[nameof(bottomDeployEvent)], Events[nameof(bottomRetractEvent)]);
            bottomAnimationModule.getSymmetryModule = m => m.bottomAnimationModule;
            bottomAnimationModule.setupAnimations(bottomModule.animationData, bottomModule.root, mountAnimationLayer);

            //solar panel animation and solar panel UI controls
            solarPanelModule = new SolarModule<SSTUModularPart>(part, this, Fields[nameof(solarAnimationPersistentData)], Fields[nameof(solarRotationPersistentData)], Fields[nameof(solarPanelStatus)], Events[nameof(solarDeployEvent)], Events[nameof(solarRetractEvent)]);
            solarPanelModule.getSymmetryModule = m => m.solarPanelModule;
            solarPanelModule.setupAnimations(solarModule.animationData, solarModule.root, solarAnimationLayer);
            solarPanelModule.setupSolarPanelData(solarModule.model.getSolarData(), solarModule.root);

            updateModulePositions();
            updateMassAndCost();
            updateAttachNodes(false);
            SSTUStockInterop.updatePartHighlighting(part);
        }

        private void updateModulePositions()
        {
            //update for model scale
            topModule.model.updateScaleForDiameter(currentDiameter * coreModule.model.topRatio);
            coreModule.model.updateScaleForDiameter(currentDiameter);
            bottomModule.model.updateScaleForDiameter(currentDiameter * coreModule.model.bottomRatio);
            float coreScale = coreModule.model.currentDiameterScale;
            solarModule.model.updateScale(coreScale);
            rcsModule.model.updateScale(coreScale);

            //calc positions
            float yPos = topModule.moduleHeight + (coreModule.moduleHeight * 0.5f);
            yPos -= topModule.moduleHeight;
            float topY = yPos;
            yPos -= coreModule.moduleHeight;
            float coreY = yPos;
            float bottomY = coreY;
            yPos -= bottomModule.moduleHeight;
            float bottomDockY = yPos;

            //update internal ref of position
            topModule.setPosition(topY);
            coreModule.setPosition(coreY);
            solarModule.setPosition(coreY);
            bottomModule.setPosition(bottomY, ModelOrientation.BOTTOM);
            rcsModule.setPosition(coreY + (coreScale * currentRCSOffset * coreModule.model.rcsOffsetRange) + (coreScale * coreModule.model.rcsPosition));

            //update actual model positions and scales
            topModule.updateModel();
            coreModule.updateModel();
            bottomModule.updateModel();
            solarModule.updateModel();
            rcsModule.model.currentHorizontalPosition = coreModule.model.currentDiameterScale * coreModule.model.modelDefinition.rcsHorizontalPosition;
            rcsModule.updateModel();
        }

        private void updateResourceVolume()
        {
            float volume = coreModule.moduleVolume;
            if (useAdapterVolume)
            {
                volume += topModule.moduleVolume;
                volume += bottomModule.moduleVolume;
            }
            SSTUModInterop.onPartFuelVolumeUpdate(part, volume * 1000f);
        }

        private void updateMassAndCost()
        {
            modifiedMass = coreModule.moduleMass;
            modifiedMass += solarModule.moduleMass;
            modifiedMass += rcsModule.moduleMass;
            if (useAdapterMass)
            {
                modifiedMass += topModule.moduleMass;
                modifiedMass += bottomModule.moduleMass;
            }

            modifiedCost = coreModule.moduleCost;
            modifiedCost += solarModule.moduleCost;
            modifiedCost += rcsModule.moduleCost;
            if (useAdapterCost)
            {
                modifiedCost += topModule.moduleCost;
                modifiedCost += bottomModule.moduleCost;
            }
        }

        private void updateRCSModule()
        {
            modularRCSControl = part.GetComponent<SSTUModularRCS>();
            if (modularRCSControl != null)
            {
                modularRCSControl.Start();
            }
            if (rcsThrust < 0)
            {
                ModuleRCS rcs = part.GetComponent<ModuleRCS>();
                if (rcs != null)
                {
                    rcsThrust = rcs.thrusterPower;
                }
            }
            if (rcsThrust >= 0)
            {
                float thrust = rcsThrust * Mathf.Pow(coreModule.model.currentDiameterScale, 2);
                SSTUModularRCS.updateRCSModules(part, !rcsModule.model.dummyModel, thrust, true, true, true, true, true, true);
            }
        }

        private void updateAttachNodes(bool userInput)
        {
            topModule.model.updateAttachNodes(part, topNodeNames, userInput, ModelOrientation.TOP);
            bottomModule.model.updateAttachNodes(part, bottomNodeNames, userInput, ModelOrientation.BOTTOM);

            Vector3 pos = new Vector3(0, getTopFairingBottomY(), 0);
            SSTUSelectableNodes.updateNodePosition(part, noseInterstageNode, pos);
            AttachNode noseInterstage = part.FindAttachNode(noseInterstageNode);
            if (noseInterstage != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, noseInterstage, pos, Vector3.up, userInput);
            }

            float bottomFairingTopY = getBottomFairingTopY();
            pos = new Vector3(0, bottomFairingTopY, 0);
            SSTUSelectableNodes.updateNodePosition(part, mountInterstageNode, pos);
            AttachNode mountInterstage = part.FindAttachNode(mountInterstageNode);
            if (mountInterstage != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, mountInterstage, pos, Vector3.down, userInput);
            }
        }

        private void updateFairing(bool userInput)
        {
            SSTUNodeFairing[] modules = part.GetComponents<SSTUNodeFairing>();
            if (modules == null || modules.Length < 2)
            {
                MonoBehaviour.print("ERROR: Could not locate both fairing modules for part: " + part.name);
                return;
            }
            SSTUNodeFairing topFairing = modules[0];
            if (topFairing != null)
            {
                float topFairingBottomY = getTopFairingBottomY();
                FairingUpdateData data = new FairingUpdateData();
                data.setTopY(getPartTopY());
                data.setBottomY(topFairingBottomY);
                data.setBottomRadius(currentDiameter * 0.5f);
                if (userInput) { data.setTopRadius(currentDiameter * 0.5f); }
                topFairing.updateExternal(data);
            }
            SSTUNodeFairing bottomFairing = modules[1];
            if (bottomFairing != null)
            {
                float bottomFairingTopY = getBottomFairingTopY();
                FairingUpdateData data = new FairingUpdateData();
                data.setTopRadius(currentDiameter * 0.5f);
                data.setTopY(bottomFairingTopY);
                if (userInput) { data.setBottomRadius(currentDiameter * 0.5f); }
                bottomFairing.updateExternal(data);
            }
        }

        private float getPartTopY()
        {
            return coreModule.model.currentHeight * 0.5f + topModule.model.currentHeight;
        }

        private float getTopFairingBottomY()
        {
            return topModule.model.getPosition(ModelOrientation.TOP) + topModule.model.getFairingOffset();
        }

        private float getBottomFairingTopY()
        {
            if (!coreModule.model.modelDefinition.fairingDisabled)
            {
                return coreModule.model.getPosition(ModelOrientation.TOP) + coreModule.model.getFairingOffset();
            }
            return bottomModule.model.getPosition(ModelOrientation.BOTTOM) - bottomModule.model.getFairingOffset();
        }

        private void updateGUI()
        {
            topModule.updateSelections();
            bottomModule.updateSelections();
            solarModule.updateSelections();
        }

        private void updateDragCubes()
        {
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        private Transform getRootTransform(string name, bool recreate)
        {
            Transform root = part.transform.FindRecursive(name);
            if (recreate && root != null)
            {
                GameObject.DestroyImmediate(root.gameObject);
                root = null;
            }
            if (root == null)
            {
                root = new GameObject(name).transform;
            }
            root.NestToParent(part.transform.FindRecursive("model"));
            return root;
        }

        #endregion ENDREGION - Custom Update Methods

        #region REGION - Utility methods

        public static T getSymmetryModule<T>(PartModule m) where T : PartModule
        {
            return (T)m;
        }

        #endregion ENDREGION

    }
}
