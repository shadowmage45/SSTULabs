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
        public string engineThrustTransform = "thrustTransform";

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

        private ModelModule<SSTUModularPart> noseModule;
        private ModelModule<SSTUModularPart> upperModule;
        private ModelModule<SSTUModularPart> coreModule;
        private ModelModule<SSTUModularPart> lowerModule;
        private ModelModule<SSTUModularPart> mountModule;

        private ModelModule<SSTUModularPart> solarModule;
        private ModelModule<SSTUModularPart> lowerRcsModule;
        private ModelModule<SSTUModularPart> upperRcsModule;

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
        public void upperDeployEvent() { upperModule.animationModule.onDeployEvent(); }

        [KSPEvent]
        public void coreDeployEvent() { coreModule.animationModule.onDeployEvent(); }

        [KSPEvent]
        public void lowerDeployEvent() { lowerModule.animationModule.onDeployEvent(); }

        [KSPEvent]
        public void mountDeployEvent() { mountModule.animationModule.onDeployEvent(); }

        [KSPEvent]
        public void noseRetractEvent() { noseModule.animationModule.onRetractEvent(); }

        [KSPEvent]
        public void upperRetractEvent() { upperModule.animationModule.onRetractEvent(); }

        [KSPEvent]
        public void coreRetractEvent() { coreModule.animationModule.onRetractEvent(); }

        [KSPEvent]
        public void lowerRetractEvent() { lowerModule.animationModule.onRetractEvent(); }

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
                m.updateAvailableVariants();
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

            Fields[nameof(currentNose)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                noseModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
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

            Fields[nameof(currentMount)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                mountModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentSolar)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                solarModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    modelChangedAction(m);
                    m.solarFunctionsModule.setupSolarPanelData(m.solarModule.getSolarData(), m.solarModule.moduleModelTransforms);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentUpperRCS)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                upperRcsModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.upperRcsModule.renameRCSThrustTransforms(upperRCSThrustTransform);
                    modelChangedAction(m);
                    m.updateRCSModule();
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentLowerRCS)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                lowerRcsModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.lowerRcsModule.renameRCSThrustTransforms(lowerRCSThrustTransform);
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

            Fields[nameof(currentLowerRCSOffset)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
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

            Fields[nameof(currentNoseTexture)].uiControlEditor.onFieldChanged = noseModule.textureSetSelected;
            Fields[nameof(currentUpperTexture)].uiControlEditor.onFieldChanged = upperModule.textureSetSelected;
            Fields[nameof(currentCoreTexture)].uiControlEditor.onFieldChanged = coreModule.textureSetSelected;
            Fields[nameof(currentLowerTexture)].uiControlEditor.onFieldChanged = lowerModule.textureSetSelected;
            Fields[nameof(currentMountTexture)].uiControlEditor.onFieldChanged = mountModule.textureSetSelected;

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
            noseModule.Update();
            upperModule.Update();
            coreModule.Update();
            lowerModule.Update();
            mountModule.Update();
            solarModule.Update();
            solarFunctionsModule.Update();
            upperRcsModule.Update();
            lowerRcsModule.Update();
        }

        //standard Unity lifecyle override
        public void FixedUpdate()
        {
            solarFunctionsModule.FixedUpdate();
        }

        //KSP editor modified event callback
        private void onEditorVesselModified(ShipConstruct ship)
        {
            updateAvailableVariants();
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
            return new string[] { "Nose", "Upper", "Core", "Lower", "Mount", "UpperRCS", "LowerRCS", "Solar" };
        }

        //IRecolorable override
        public RecoloringData[] getSectionColors(string section)
        {
            if (section == "Nose")
            {
                return noseModule.recoloringData;
            }
            else if (section == "Upper")
            {
                return upperModule.recoloringData;
            }
            else if (section == "Core")
            {
                return coreModule.recoloringData;
            }
            else if (section == "Lower")
            {
                return lowerModule.recoloringData;
            }
            else if (section == "Mount")
            {
                return mountModule.recoloringData;
            }
            else if (section == "UpperRCS")
            {
                return upperRcsModule.recoloringData;
            }
            else if (section == "LowerRCS")
            {
                return lowerRcsModule.recoloringData;
            }
            else if (section == "Solar")
            {
                return solarModule.recoloringData;
            }
            return coreModule.recoloringData;
        }

        //IRecolorable override
        public void setSectionColors(string section, RecoloringData[] colors)
        {
            if (section == "Nose")
            {
                noseModule.setSectionColors(colors);
            }
            else if (section == "Upper")
            {
                upperModule.setSectionColors(colors);
            }
            else if (section == "Core")
            {
                coreModule.setSectionColors(colors);
            }
            else if (section == "Lower")
            {
                lowerModule.setSectionColors(colors);
            }
            else if (section == "Mount")
            {
                mountModule.setSectionColors(colors);
            }
            else if (section == "UpperRCS")
            {
                upperRcsModule.setSectionColors(colors);
            }
            else if (section == "LowerRCS")
            {
                lowerRcsModule.setSectionColors(colors);
            }
            else if (section == "Solar")
            {
                solarModule.setSectionColors(colors);
            }
        }

        //IRecolorable override
        public TextureSet getSectionTexture(string section)
        {
            if (section == "Nose")
            {
                return noseModule.textureSet;
            }
            else if (section == "Upper")
            {
                return upperModule.textureSet;
            }
            else if (section == "Core")
            {
                return coreModule.textureSet;
            }
            else if (section == "Lower")
            {
                return lowerModule.textureSet;
            }
            else if (section == "Mount")
            {
                return mountModule.textureSet;
            }
            else if (section == "UpperRCS")
            {
                return upperRcsModule.textureSet;
            }
            else if (section == "LowerRCS")
            {
                return lowerRcsModule.textureSet;
            }
            else if (section == "Solar")
            {
                return solarModule.textureSet;
            }
            return coreModule.textureSet;
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

            noseModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-NOSE", true), ModelOrientation.TOP, nameof(currentNose), nameof(currentNoseTexture), nameof(noseModulePersistentData), nameof(noseAnimationPersistentData), nameof(noseAnimationDeployLimit), nameof(noseDeployEvent), nameof(noseRetractEvent));
            noseModule.getSymmetryModule = m => m.noseModule;
            noseModule.getParentModule = m => m.upperModule;
            noseModule.getValidOptions = upperModule.getUpperOptions;

            upperModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-UPPER", true), ModelOrientation.TOP, nameof(currentUpper), nameof(currentUpperTexture), nameof(upperModulePersistentData), nameof(upperAnimationPersistentData), nameof(upperAnimationDeployLimit), nameof(upperDeployEvent), nameof(upperRetractEvent));
            upperModule.getSymmetryModule = m => m.upperModule;
            upperModule.getParentModule = m => m.coreModule;
            upperModule.getValidOptions = coreModule.getUpperOptions;

            coreModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-CORE", true), ModelOrientation.TOP, nameof(currentCore), nameof(currentCoreTexture), nameof(coreModulePersistentData), nameof(coreAnimationPersistentData), nameof(coreAnimationDeployLimit), nameof(coreDeployEvent), nameof(coreRetractEvent));
            coreModule.getSymmetryModule = m => m.coreModule;
            solarModule.getParentModule = m => null;
            coreModule.getValidOptions = () => SSTUModelData.getModelDefinitions(node.GetNodes("CORE"));
            coreModule.setupModelList(SSTUModelData.getModelDefinitions(node.GetNodes("CORE")));

            lowerModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-LOWER", true), ModelOrientation.BOTTOM, nameof(currentLower), nameof(currentLowerTexture), nameof(lowerModulePersistentData), nameof(lowerAnimationPersistentData), nameof(lowerAnimationDeployLimit), nameof(lowerDeployEvent), nameof(lowerRetractEvent));
            lowerModule.getSymmetryModule = m => m.lowerModule;
            lowerModule.getParentModule = m => m.coreModule;
            lowerModule.getValidOptions = coreModule.getLowerOptions;

            mountModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-MOUNT", true), ModelOrientation.BOTTOM, nameof(currentMount), nameof(currentMountTexture), nameof(mountModulePersistentData), nameof(mountAnimationPersistentData), nameof(mountAnimationDeployLimit), nameof(mountDeployEvent), nameof(mountRetractEvent));
            mountModule.getSymmetryModule = m => m.mountModule;
            mountModule.getParentModule = m => m.lowerModule;
            mountModule.getValidOptions = lowerModule.getLowerOptions;

            solarModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-SOLAR", true), ModelOrientation.CENTRAL, nameof(currentSolar), nameof(currentSolarTexture), nameof(solarModulePersistentData), nameof(solarAnimationPersistentData), null, nameof(solarDeployEvent), nameof(solarRetractEvent));
            solarModule.getSymmetryModule = m => m.solarModule;
            solarModule.getParentModule = m => null;

            upperRcsModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-UPPERRCS", true), ModelOrientation.CENTRAL, nameof(currentUpperRCS), null, null, null, null, null, null);
            upperRcsModule.getSymmetryModule = m => m.upperRcsModule;

            coreModule.setScaleForDiameter(currentDiameter);

            //model-module model-creation
            noseModule.setupModel();
            upperModule.setupModel();            
            coreModule.setupModel();            
            lowerModule.setupModel();
            mountModule.setupModel();
            solarModule.setupModel();
            upperRcsModule.setupModel();
            lowerRcsModule.setupModel();

            upperRcsModule.renameRCSThrustTransforms(upperRCSThrustTransform);

            //solar panel animation and solar panel UI controls
            solarFunctionsModule = new SolarModule(part, this, solarModule.animationModule, Fields[nameof(solarRotationPersistentData)], Fields[nameof(solarPanelStatus)]);
            solarFunctionsModule.getSymmetryModule = m => ((SSTUModularPart)m).solarFunctionsModule;
            solarFunctionsModule.setupSolarPanelData(solarModule.getSolarData(), solarModule.moduleModelTransforms);

            updateModulePositions();
            updateMassAndCost();
            updateAttachNodes(false);
            SSTUStockInterop.updatePartHighlighting(part);
        }

        private void updateModulePositions()
        {
            //update for model scale
            upperModule.setScaleForDiameter(currentDiameter);
            coreModule.setScaleForDiameter(currentDiameter);
            lowerModule.setScaleForDiameter(currentDiameter);
            float coreScale = coreModule.moduleHorizontalScale;
            solarModule.setScale(coreScale);
            upperRcsModule.setScale(coreScale);

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
            upperRcsModule.setPosition(coreY);

            //update actual model positions and scales
            noseModule.updateModelMeshes();
            upperModule.updateModelMeshes();
            coreModule.updateModelMeshes();
            lowerModule.updateModelMeshes();
            mountModule.updateModelMeshes();
            solarModule.updateModelMeshes();
            upperRcsModule.updateModelMeshes();
            lowerRcsModule.updateModelMeshes();
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

        //TODO
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
                float thrust = rcsThrust * Mathf.Pow(coreModule.moduleHorizontalScale, 2);
                MonoBehaviour.print("TODO -- Adjust RCS thrust/enabled/disabled status from SSTUModularPart");
                //SSTUModularRCS.updateRCSModules(part, !upperRcsModule.model.dummyModel, thrust, true, true, true, true, true, true);
            }
        }

        private void updateAttachNodes(bool userInput)
        {
            upperModule.updateAttachNodes(topNodeNames, userInput);
            lowerModule.updateAttachNodes( bottomNodeNames, userInput);

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
            return 0f;
        }

        private float getTopFairingBottomY()
        {
            return 0f;
        }

        private float getBottomFairingTopY()
        {
            return 0f;
        }

        private void updateAvailableVariants()
        {
            noseModule.updateSelections();
            upperModule.updateSelections();
            coreModule.updateSelections();
            lowerModule.updateSelections();
            mountModule.updateSelections();
            solarModule.updateSelections();
            upperRcsModule.updateSelections();
            lowerRcsModule.updateSelections();
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

    }
}
