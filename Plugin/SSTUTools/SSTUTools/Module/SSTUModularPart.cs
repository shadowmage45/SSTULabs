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
        public int upperAnimationLayer = 5;

        [KSPField]
        public int coreAnimationLayer = 7;

        [KSPField]
        public int lowerAnimationLayer = 9;

        [KSPField]
        public int mountAnimationLayer = 11;

        [KSPField]
        public string upperRCSThrustTransform = "RCSThrustTransform";

        [KSPField]
        public string lowerRCSThrustTransform = "RCSThrustTransform";

        [KSPField]
        public string topManagedNodes = "top1, top2";

        [KSPField]
        public string bottomManagedNodes = "bottom1, bottom2";

        /// <summary>
        /// Name of the 'interstage' node; positioned depending on mount interstage location (CB) / bottom of the upper tank (ST).
        /// </summary>
        [KSPField]
        public string noseInterstageNode = "noseInterstage";

        /// <summary>
        /// Name of the 'interstage' node; positioned depending on mount interstage location (CB) / bottom of the upper tank (ST).
        /// </summary>
        [KSPField]
        public string mountInterstageNode = "mountInterstage";

        //persistent config fields for module selections
        //also GUI controls for module selection

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Diameter"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentDiameter = 2.5f;

        //solar panel GUI status field
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "SolarState:")]
        public string solarPanelStatus = string.Empty;

        #region REGION - Module persistent data fields

        //persistence and UI controls for module/model selection (top, core, bottom, solar, RCS)
        [KSPField(isPersistant = true, guiName = "Top"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentNose = "Mount-None";
        
        [KSPField(isPersistant = true, guiName = "Upper"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpper = "Mount-None";

        [KSPField(isPersistant = true, guiName = "Core"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCore = "Mount-None";

        [KSPField(isPersistant = true, guiName = "Lower"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLower = "Mount-None";

        [KSPField(isPersistant = true, guiName = "Mount"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentMount = "Mount-None";

        [KSPField(isPersistant = true, guiName = "Solar"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentSolar = "Solar-None";

        [KSPField(isPersistant = true, guiName = "Upper RCS"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperRCS = "MUS-RCS1";

        [KSPField(isPersistant = true, guiName = "Lower RCS"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerRCS = "MUS-RCS1";

        //persistent config fields for module texture sets
        //also GUI controls for texture selection
        [KSPField(isPersistant = true, guiName = "Nose Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentNoseTexture = "default";

        [KSPField(isPersistant = true, guiName = "Upper Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperTexture = "default";

        [KSPField(isPersistant = true, guiName = "Core Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCoreTexture = "default";

        [KSPField(isPersistant = true, guiName = "Lower Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerTexture = "default";

        [KSPField(isPersistant = true, guiName = "Mount Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentMountTexture = "default";

        [KSPField(isPersistant = true, guiName = "Upper RCS Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperRCSTexture = "default";

        [KSPField(isPersistant = true, guiName = "Lower RCS Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerRCSTexture = "default";

        [KSPField(isPersistant = true, guiName = "Solar Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentSolarTexture = "default";

        //persistent config fields and UI controls for deploy limits for nose, core, and mount animation modules
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Nose Deploy Limit"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float noseAnimationDeployLimit = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Top Deploy Limit"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float upperAnimationDeployLimit = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Core Deploy Limit"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float coreAnimationDeployLimit = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Bottom Deploy Limit"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float lowerAnimationDeployLimit = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Mount Deploy Limit"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float mountAnimationDeployLimit = 1f;

        //vertical offset control for RCS models
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "RCS V.Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentUpperRCSOffset = 0f;

        //vertical offset control for RCS models
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "RCS V.Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentLowerRCSOffset = 0f;

        //persistent data for modules; stores colors and other per-module data
        [KSPField(isPersistant = true)]
        public string noseModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string upperModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string coreModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string lowerModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string mountModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string solarModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string upperRCSModulePersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string lowerRCSModulePersistentData = string.Empty;

        //persistence data for animation modules, stores animation state
        [KSPField(isPersistant = true)]
        public string noseAnimationPersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string upperAnimationPersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string coreAnimationPersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string lowerAnimationPersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string mountAnimationPersistentData = string.Empty;

        //persistence data for solar module, stores animation state and rotation cache
        [KSPField(isPersistant = true)]
        public string solarAnimationPersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string solarRotationPersistentData = string.Empty;

        #endregion ENDREGION - Module persistent data fields

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

        private ModelModule<SingleModelData, SSTUModularPart> noseModule;
        private ModelModule<SingleModelData, SSTUModularPart> upperModule;
        private ModelModule<SingleModelData, SSTUModularPart> coreModule;
        private ModelModule<SingleModelData, SSTUModularPart> lowerModule;
        private ModelModule<SingleModelData, SSTUModularPart> mountModule;

        private ModelModule<SolarModelData, SSTUModularPart> solarModule;
        private ModelModule<ServiceModuleRCSModelData, SSTUModularPart> lowerRcsModule;
        private ModelModule<ServiceModuleRCSModelData, SSTUModularPart> upperRcsModule;

        private SolarModule solarFunctionsModule;
        
        /// <summary>
        /// ref to the ModularRCS module that updates fuel type and thrust for RCS
        /// </summary>
        private SSTURCSFuelSelection rcsFuelControl;

        #endregion ENDREGION - Private working vars

        #region REGION - UI Controls

        [KSPEvent]
        public void noseDeployEvent() { noseModule.animationModule.onDeployEvent(); }

        [KSPEvent]
        public void topDeployEvent() { upperModule.animationModule.onDeployEvent(); }

        [KSPEvent]
        public void coreDeployEvent() { coreModule.animationModule.onDeployEvent(); }

        [KSPEvent]
        public void bottomDeployEvent() { lowerModule.animationModule.onDeployEvent(); }

        [KSPEvent]
        public void mountDeployEvent() { mountModule.animationModule.onDeployEvent(); }

        [KSPEvent]
        public void noseRetractEvent() { noseModule.animationModule.onRetractEvent(); }

        [KSPEvent]
        public void topRetractEvent() { upperModule.animationModule.onRetractEvent(); }

        [KSPEvent]
        public void coreRetractEvent() { coreModule.animationModule.onRetractEvent(); }

        [KSPEvent]
        public void bottomRetractEvent() { lowerModule.animationModule.onRetractEvent(); }

        [KSPEvent]
        public void mountRetractEvent() { mountModule.animationModule.onRetractEvent(); }

        [KSPAction]
        public void topToggleAction(KSPActionParam param) { upperModule.animationModule.onToggleAction(param); }

        [KSPAction]
        public void coreToggleAction(KSPActionParam param) { coreModule.animationModule.onToggleAction(param); }

        [KSPAction]
        public void bottomToggleAction(KSPActionParam param) { lowerModule.animationModule.onToggleAction(param); }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Deploy Solar Panels")]
        public void solarDeployEvent() { solarModule.animationModule.onDeployEvent(); }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Retract Solar Panels")]
        public void solarRetractEvent() { solarFunctionsModule.onRetractEvent(); }

        [KSPAction]
        public void solarToggleAction(KSPActionParam param) { solarModule.animationModule.onToggleAction(param); }

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

            Fields[nameof(currentUpper)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                upperModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentCore)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                coreModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                //TODO validate solar, adapters, etc
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentLower)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                lowerModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentSolar)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                solarModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    modelChangedAction(m);
                    m.solarFunctionsModule.setupSolarPanelData(m.solarModule.model.getSolarData(), m.solarModule.root);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentUpperRCS)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                upperRcsModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.upperRcsModule.model.renameThrustTransforms(lowerRCSThrustTransform);
                    modelChangedAction(m);
                    m.updateRCSModule();
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentUpperRCSOffset)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
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

            Fields[nameof(currentUpperTexture)].uiControlEditor.onFieldChanged = upperModule.textureSetSelected;
            Fields[nameof(currentCoreTexture)].uiControlEditor.onFieldChanged = coreModule.textureSetSelected;
            Fields[nameof(currentLowerTexture)].uiControlEditor.onFieldChanged = lowerModule.textureSetSelected;

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
            if (solarFunctionsModule != null)
            {
                solarFunctionsModule.updateSolarPersistence();
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
                return upperModule.customColors;
            }
            else if (section == "Body")
            {
                return coreModule.customColors;
            }
            else if (section == "Bottom")
            {
                return lowerModule.customColors;
            }
            return coreModule.customColors;
        }

        //IRecolorable override
        public void setSectionColors(string section, RecoloringData[] colors)
        {
            if (section == "Top")
            {
                upperModule.setSectionColors(colors);
            }
            else if (section == "Body")
            {
                coreModule.setSectionColors(colors);
            }
            else if (section == "Bottom")
            {
                lowerModule.setSectionColors(colors);
            }
        }

        //IRecolorable override
        public TextureSet getSectionTexture(string section)
        {
            if (section == "Top")
            {
                return upperModule.currentTextureSet;
            }
            else if (section == "Body")
            {
                return coreModule.currentTextureSet;
            }
            else if (section == "Bottom")
            {
                return lowerModule.currentTextureSet;
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

            upperModule = new ModelModule<SingleModelData, SSTUModularPart>(part, this, getRootTransform("MSC-TOP", true), ModelOrientation.TOP, nameof(currentUpper), nameof(currentUpperTexture), nameof(upperModulePersistentData));
            upperModule.getSymmetryModule = m => m.upperModule;
            upperModule.getValidSelections = m => upperModule.models.FindAll(s => s.canSwitchTo(part, topNodeNames));

            coreModule = new ModelModule<SingleModelData, SSTUModularPart>(part, this, getRootTransform("MSC-CORE", true), ModelOrientation.TOP, nameof(currentCore), nameof(currentCoreTexture), nameof(coreModulePersistentData), nameof(coreAnimationPersistentData), nameof(coreAnimationDeployLimit), nameof(coreDeployEvent), nameof(coreRetractEvent));
            coreModule.getSymmetryModule = m => m.coreModule;
            coreModule.setupModelList(ModelData.parseModels(node.GetNodes("CORE"), m => new ServiceModuleCoreModel(m)));

            lowerModule = new ModelModule<SingleModelData, SSTUModularPart>(part, this, getRootTransform("MSC-BOTTOM", true), ModelOrientation.BOTTOM, nameof(currentLower), nameof(currentLowerTexture), nameof(lowerModulePersistentData));
            lowerModule.getSymmetryModule = m => m.lowerModule;
            lowerModule.getValidSelections = m => lowerModule.models.FindAll(s => s.canSwitchTo(part, bottomNodeNames));

            solarModule = new ModelModule<SolarModelData, SSTUModularPart>(part, this, getRootTransform("MSC-Solar", true), ModelOrientation.CENTRAL, nameof(currentSolar), nameof(currentSolarTexture), nameof(solarModulePersistentData));
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

            upperRcsModule = new ModelModule<ServiceModuleRCSModelData, SSTUModularPart>(part, this, getRootTransform("MSC-Rcs", true), ModelOrientation.CENTRAL, nameof(currentUpperRCS), null, null);
            upperRcsModule.getSymmetryModule = m => m.upperRcsModule;
            upperRcsModule.setupModelList(ModelData.parseModels(node.GetNodes("RCS"), m => new ServiceModuleRCSModelData(m)));
            upperRcsModule.getValidSelections = m => upperRcsModule.models.FindAll(s => s.isAvailable(upgradesApplied));

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
            upperModule.setupModelList(SingleModelData.parseModels(tops.ToArray()));
            lowerModule.setupModelList(SingleModelData.parseModels(bottoms.ToArray()));
            tops.Clear();
            bottoms.Clear();

            //model-module model-creation
            upperModule.setupModel();
            coreModule.setupModel();
            coreModule.model.updateScaleForDiameter(currentDiameter);
            lowerModule.setupModel();
            solarModule.setupModel();
            upperRcsModule.setupModel();
            upperRcsModule.model.renameThrustTransforms(lowerRCSThrustTransform);

            //solar panel animation and solar panel UI controls
            solarFunctionsModule = new SolarModule(part, this, solarModule.animationModule, Fields[nameof(solarRotationPersistentData)], Fields[nameof(solarPanelStatus)]);
            solarFunctionsModule.getSymmetryModule = m => ((SSTUModularPart)m).solarFunctionsModule;
            solarFunctionsModule.setupSolarPanelData(solarModule.model.getSolarData(), solarModule.root);

            updateModulePositions();
            updateMassAndCost();
            updateAttachNodes(false);
            SSTUStockInterop.updatePartHighlighting(part);
        }

        private void updateModulePositions()
        {
            //update for model scale
            upperModule.model.updateScaleForDiameter(currentDiameter);
            coreModule.model.updateScaleForDiameter(currentDiameter);
            lowerModule.model.updateScaleForDiameter(currentDiameter);
            float coreScale = coreModule.model.currentDiameterScale;
            solarModule.model.updateScale(coreScale);
            upperRcsModule.model.updateScale(coreScale);

            //calc positions
            float yPos = upperModule.moduleHeight + (coreModule.moduleHeight * 0.5f);
            yPos -= upperModule.moduleHeight;
            float topY = yPos;
            yPos -= coreModule.moduleHeight;
            float coreY = yPos;
            float bottomY = coreY;
            yPos -= lowerModule.moduleHeight;
            float bottomDockY = yPos;

            //update internal ref of position
            upperModule.setPosition(topY);
            coreModule.setPosition(coreY);
            solarModule.setPosition(coreY);
            lowerModule.setPosition(bottomY, ModelOrientation.BOTTOM);
            upperRcsModule.setPosition(coreY + (coreScale * currentUpperRCSOffset * coreModule.model.rcsOffsetRange) + (coreScale * coreModule.model.rcsPosition));

            //update actual model positions and scales
            upperModule.updateModel();
            coreModule.updateModel();
            lowerModule.updateModel();
            solarModule.updateModel();
            upperRcsModule.model.currentHorizontalPosition = coreModule.model.currentDiameterScale * coreModule.model.modelDefinition.rcsHorizontalPosition;
            upperRcsModule.updateModel();
        }

        private void updateResourceVolume()
        {
            float volume = coreModule.moduleVolume;
            if (useAdapterVolume)
            {
                volume += noseModule.moduleVolume;
                volume += upperModule.moduleVolume;
                volume += lowerModule.moduleVolume;
                volume += mountModule.moduleVolume;
            }
            SSTUModInterop.onPartFuelVolumeUpdate(part, volume * 1000f);
        }

        private void updateMassAndCost()
        {
            modifiedMass = coreModule.moduleMass;
            modifiedMass += solarModule.moduleMass;
            modifiedMass += upperRcsModule.moduleMass;
            if (useAdapterMass)
            {
                modifiedMass += noseModule.moduleMass;
                modifiedMass += upperModule.moduleMass;
                modifiedMass += lowerModule.moduleMass;
                modifiedMass += mountModule.moduleMass;
            }

            modifiedCost = coreModule.moduleCost;
            modifiedCost += solarModule.moduleCost;
            modifiedCost += upperRcsModule.moduleCost;
            modifiedCost += lowerRcsModule.moduleCost;
            if (useAdapterCost)
            {
                modifiedCost += noseModule.moduleCost;
                modifiedCost += upperModule.moduleCost;
                modifiedCost += lowerModule.moduleCost;
                modifiedCost += mountModule.moduleCost;
            }
        }

        private void updateRCSModule()
        {
            //TODO
            //rcsFuelControl = part.GetComponent<SSTUModularRCS>();
            //if (rcsFuelControl != null)
            //{
            //    rcsFuelControl.Start();
            //}
            //TODO
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
                SSTUModularRCS.updateRCSModules(part, !upperRcsModule.model.dummyModel, thrust, true, true, true, true, true, true);
            }
        }

        private void updateAttachNodes(bool userInput)
        {
            upperModule.model.updateAttachNodes(part, topNodeNames, userInput, ModelOrientation.TOP);
            lowerModule.model.updateAttachNodes(part, bottomNodeNames, userInput, ModelOrientation.BOTTOM);

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
            return coreModule.model.currentHeight * 0.5f + upperModule.model.currentHeight;
        }

        private float getTopFairingBottomY()
        {
            return upperModule.model.getPosition(ModelOrientation.TOP) + upperModule.model.getFairingOffset();
        }

        private float getBottomFairingTopY()
        {
            if (!coreModule.model.modelDefinition.fairingDisabled)
            {
                return coreModule.model.getPosition(ModelOrientation.TOP) + coreModule.model.getFairingOffset();
            }
            return lowerModule.model.getPosition(ModelOrientation.BOTTOM) - lowerModule.model.getFairingOffset();
        }

        private void updateGUI()
        {
            upperModule.updateSelections();
            lowerModule.updateSelections();
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
