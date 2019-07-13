using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSPShaderTools;
using static SSTUTools.SSTULog;

namespace SSTUTools
{

    /// <summary>
    /// PartModule that manages multiple models/meshes and accompanying features for model switching - resources, modules, textures, recoloring.<para/>
    /// Includes 5 stack-mounted modules, two rcs modules, and a single solar module.  All modules support model-switching, texture-switching, recoloring.
    /// </summary>
    public class SSTUModularPart : PartModule, IPartCostModifier, IPartMassModifier, IRecolorable, IContainerVolumeContributor, IContractObjectiveModule
    {

        #region REGION - Part Config Fields

        [KSPField]
        public float diameterIncrement = 0.625f;

        [KSPField]
        public float minDiameter = 0.625f;

        [KSPField]
        public float maxDiameter = 10f;

        [KSPField]
        public float volumeScalingPower = 3f;

        [KSPField]
        public float massScalingPower = 3f;

        [KSPField]
        public float thrustScalingPower = 3f;

        [KSPField]
        public float solarScalingPower = 2f;

        [KSPField]
        public bool enableVScale;

        [KSPField]
        public bool useAdapterMass = true;

        [KSPField]
        public bool useAdapterCost = true;

        [KSPField]
        public bool validateNose = true;

        [KSPField]
        public bool validateUpper = true;

        [KSPField]
        public bool validateLower = true;

        [KSPField]
        public bool validateMount = true;

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
        public int noseContainerIndex = 0;

        [KSPField]
        public int upperContainerIndex = 0;

        [KSPField]
        public int coreContainerIndex = 0;

        [KSPField]
        public int lowerContainerIndex = 0;

        [KSPField]
        public int mountContainerIndex = 0;

        [KSPField]
        public int auxContainerSourceIndex = -1;

        [KSPField]
        public int auxContainerTargetIndex = -1;

        [KSPField]
        public float auxContainerMinPercent = 0f;

        [KSPField]
        public float auxContainerMaxPercent = 0f;

        [KSPField]
        public int topFairingIndex = -1;

        [KSPField]
        public int centralFairingIndex = -1;

        [KSPField]
        public int bottomFairingIndex = -1;

        [KSPField]
        public int upperRCSIndex = 0;

        [KSPField]
        public int lowerRCSIndex = 0;

        [KSPField]
        public string upperRCSThrustTransform = "RCSThrustTransform";

        [KSPField]
        public string lowerRCSThrustTransform = "RCSThrustTransform";

        [KSPField]
        public string engineThrustTransform = "thrustTransform";

        [KSPField]
        public string gimbalTransform = "gimbalTransform";

        [KSPField]
        public string noseManagedNodes = string.Empty;

        [KSPField]
        public string upperManagedNodes = string.Empty;

        [KSPField]
        public string coreManagedNodes = string.Empty;

        [KSPField]
        public string lowerManagedNodes = string.Empty;

        [KSPField]
        public string mountManagedNodes = string.Empty;

        /// <summary>
        /// Name of the 'interstage' node; positioned according to upper fairing lower spawn point
        /// </summary>
        [KSPField]
        public string noseInterstageNode = "noseInterstage";

        /// <summary>
        /// Name of the 'interstage' node; positioned according to lower fairing upper spawn point
        /// </summary>
        [KSPField]
        public string mountInterstageNode = "mountInterstage";

        /// <summary>
        /// Which model slots may the solar panels be parented to? <para/>
        /// Only one of RCS or SOLAR may be parented to any given slot at any given time,
        /// unless there is only one valid slot specified for each
        /// </summary>
        [KSPField]
        public string solarParentOptions = "NOSE,UPPER,CORE,LOWER,MOUNT";

        /// <summary>
        /// Which model slots may the upper rcs blocks be parented to? <para/>
        /// Only one of RCS or SOLAR may be parented to any given slot at any given time,
        /// unless there is only one valid slot specified for each
        /// </summary>
        [KSPField]
        public string upperRCSParentOptions = "NOSE,UPPER,CORE,LOWER,MOUNT";

        /// <summary>
        /// Which model slots may the lower rcs blocks be parented to? <para/>
        /// Only one of RCS or SOLAR may be parented to any given slot at any given time,
        /// unless there is only one valid slot specified for each
        /// </summary>
        [KSPField]
        public string lowerRCSParentOptions = "NOSE,UPPER,CORE,LOWER,MOUNT";

        /// <summary>
        /// Determines what module slot should be examined for engine thrust statistics.
        /// </summary>
        [KSPField]
        public string engineThrustSource = "CORE";

        /// <summary>
        /// Determines what module slot should be examined for engine transform and gimbal transform names
        /// </summary>
        [KSPField]
        public string engineTransformSource = "CORE";

        /// <summary>
        /// Determines what module slot should be examined for upper-rcs statistics
        /// </summary>
        [KSPField]
        public string upperRCSFunctionSource = "UPPERRCS";

        /// <summary>
        /// Determines what module slot should be examined for lower-rcs statistics
        /// </summary>
        [KSPField]
        public string lowerRCSFunctionSource = "LOWERRCS";

        /// <summary>
        /// Solar panel display status field.  Updated by solar functions module with occlusion and/or EC generation stats.
        /// </summary>
        [KSPField(isPersistant = false, guiActiveEditor = false, guiActive = true, guiName = "SolarState:")]
        public string solarPanelStatus = string.Empty;

        /// <summary>
        /// The current user selected diamater of the part.  Drives the scaling and positioning of everything else in the model.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Diameter", guiUnits = "m"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentDiameter = 2.5f;

        /// <summary>
        /// Adjustment to the vertical-scale of v-scale compatible models/module-slots.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "V.ScaleAdj"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = -1, maxValue = 1, incrementLarge = 0.25f, incrementSmall = 0.05f, incrementSlide = 0.01f)]
        public float currentVScale = 0f;

        /// <summary>
        /// Percentage of 'core' model volume that is devoted to a secondary container.
        /// Intended to be used to allow for easy configuration of 'support tank' use for RCS/EC propellants alongside a main-fuel tank.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Support", guiUnits = "%"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 15, incrementLarge = 5, incrementSmall = 1, incrementSlide = 0.1f)]
        public float auxContainerPercent = 0f;

        #region REGION - Module persistent data fields

        //------------------------------------------MODEL SELECTION SET PERSISTENCE-----------------------------------------------//

        //non-persistent value; initialized to whatever the currently selected core model definition is at time of loading
        //allows for variant names to be updated in the part-config without breaking everything....
        [KSPField(isPersistant =true, guiName = "Variant", guiActiveEditor = true, guiActive = false),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentVariant = "Default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Top"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentNose = "Mount-None";
        
        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Upper"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpper = "Mount-None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Core"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCore = "Mount-None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Lower"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLower = "Mount-None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Mount"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentMount = "Mount-None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Solar V.Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentSolarOffset = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Solar"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentSolar = "Solar-None";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Solar Layout"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentSolarLayout = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Solar Parent"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentSolarParent = "CORE";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Upper RCS"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperRCS = "MUS-RCS1";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Upper RCS Layout"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperRCSLayout = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Upper RCS Parent"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperRCSParent = "CORE";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Upper RCS Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentUpperRCSOffset = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Lower RCS"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerRCS = "MUS-RCS1";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Lower RCS Layout"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerRCSLayout = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Lower RCS Parent"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerRCSParent = "CORE";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Lower RCS Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentLowerRCSOffset = 0f;

        //------------------------------------------TEXTURE SET PERSISTENCE-----------------------------------------------//

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Nose Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentNoseTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Upper Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Core Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCoreTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Lower Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Mount Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentMountTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Upper RCS Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperRCSTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Lower RCS Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerRCSTexture = "default";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Solar Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentSolarTexture = "default";

        //------------------------------------------RECOLORING PERSISTENCE-----------------------------------------------//

        //persistent data for modules; stores colors
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

        //------------------------------------------ANIMATION PERSISTENCE-----------------------------------------------//

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Nose Deploy Limit"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float noseAnimationDeployLimit = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Top Deploy Limit"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float upperAnimationDeployLimit = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Core Deploy Limit"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float coreAnimationDeployLimit = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Bottom Deploy Limit"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float lowerAnimationDeployLimit = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Mount Deploy Limit"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float mountAnimationDeployLimit = 1f;
        
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
        
        [KSPField(isPersistant = true)]
        public string solarAnimationPersistentData = string.Empty;

        [KSPField(isPersistant = true)]
        public string solarRotationPersistentData = string.Empty;

        #endregion ENDREGION - Module persistent data fields

        //tracks if default textures and resource volumes have been initialized; only occurs once during the parts' first Start() call
        [KSPField(isPersistant = true)]
        public bool initializedDefaults = false;

        /// <summary>
        /// Nominal output of the solar panels; 100% thermal efficiency at Kerbin orbit distance from sun (1 KAU).  This value is set when
        /// the part is initialied and updated any time solar panel layout is changed.  Can be queried in the editor or flight scene to
        /// determine the current -nominal- EC output of solar panels.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float nominalSolarOutput = 0f;

        [KSPField]
        public bool subtractMass = true;

        [KSPField]
        public bool subtractCost = true;

        #endregion REGION - Part Config Fields

        #region REGION - Private working vars

        /// <summary>
        /// Standard work-around for lack of config-node data being passed consistently and lack of support for mod-added serializable classes.
        /// </summary>
        [Persistent]
        public string configNodeData = string.Empty;

        /// <summary>
        /// Has initialization been run?  Set to true the first time init methods are run (OnLoad/OnStart), and ensures that init is only run a single time.
        /// </summary>
        private bool initialized = false;

        /// <summary>
        /// The adjusted modified mass for this part.
        /// </summary>
        private float modifiedMass = -1;

        /// <summary>
        /// The adjusted modified cost for this part.
        /// </summary>
        private float modifiedCost = -1;

        /// <summary>
        /// Radius values used for positioning of the solar and RCS module slots.  These values are updated in the 'udpatePositions' method.
        /// </summary>
        private float lowerRCSRad;
        private float upperRCSRad;
        private float solarRad;

        /// <summary>
        /// Previous diameter value, used for surface attach position updates.
        /// </summary>
        private float prevDiameter = -1;

        private string[] noseNodeNames;
        private string[] upperNodeNames;
        private string[] coreNodeNames;
        private string[] lowerNodeNames;
        private string[] mountNodeNames;

        //Main module slots for nose/upper/core/lower/mount
        private ModelModule<SSTUModularPart> noseModule;
        private ModelModule<SSTUModularPart> upperModule;
        private ModelModule<SSTUModularPart> coreModule;
        private ModelModule<SSTUModularPart> lowerModule;
        private ModelModule<SSTUModularPart> mountModule;
        //Acessory module slots for solar panels, lower and upper RCS
        private ModelModule<SSTUModularPart> solarModule;
        private ModelModule<SSTUModularPart> lowerRcsModule;
        private ModelModule<SSTUModularPart> upperRcsModule;

        /// <summary>
        /// Ref to the solar module that updates solar panel functions -- ec gen, tracking (animations handled through built-in animation handling)
        /// </summary>
        private SolarModule solarFunctionsModule;

        /// <summary>
        /// Mapping of all of the variant sets available for this part.  When variant list length > 0, an additional 'variant' UI slider is added to allow for switching between variants.
        /// </summary>
        private Dictionary<string, ModelDefinitionVariantSet> variantSets = new Dictionary<string, ModelDefinitionVariantSet>();

        /// <summary>
        /// Helper method to get or create a variant set for the input variant name.  If no set currently exists, a new set is empty set is created and returned.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private ModelDefinitionVariantSet getVariantSet(string name)
        {
            ModelDefinitionVariantSet set = null;
            if (!variantSets.TryGetValue(name, out set))
            {
                set = new ModelDefinitionVariantSet(name);
                variantSets.Add(name, set);
            }
            return set;
        }

        /// <summary>
        /// Helper method to find the variant set for the input model definition.  Will nullref/error if no variant set is found.  Will NOT create a new set if not found.
        /// </summary>
        /// <param name="def"></param>
        /// <returns></returns>
        private ModelDefinitionVariantSet getVariantSet(ModelDefinitionLayoutOptions def)
        {
            //returns the first variant set out of all variants where the variants definitions contains the input definition
            return variantSets.Values.Where((a, b) => { return a.definitions.Contains(def); }).First();
        }

        #endregion ENDREGION - Private working vars

        #region REGION - UI Events and Actions for Animations (buttons and action groups)

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

        [KSPAction(guiName = "Toggle Nose Animation")]
        public void noseToggleAction(KSPActionParam param) { noseModule.animationModule.onToggleAction(param); }

        [KSPAction(guiName = "Toggle Top Animation")]
        public void topToggleAction(KSPActionParam param) { upperModule.animationModule.onToggleAction(param); }

        [KSPAction(guiName = "Toggle Core Animation")]
        public void coreToggleAction(KSPActionParam param) { coreModule.animationModule.onToggleAction(param); }

        [KSPAction(guiName = "Toggle Bottom Animation")]
        public void bottomToggleAction(KSPActionParam param) { lowerModule.animationModule.onToggleAction(param); }

        [KSPAction(guiName = "Toggle Mount Animation")]
        public void mountToggleAction(KSPActionParam param) { mountModule.animationModule.onToggleAction(param); }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Deploy Solar Panels")]
        public void solarDeployEvent() { solarModule.animationModule.onDeployEvent(); }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Retract Solar Panels")]
        public void solarRetractEvent() { solarFunctionsModule.onRetractEvent(); }

        [KSPAction(guiName = "Toggle Solar Deployment")]
        public void solarToggleAction(KSPActionParam param) { solarModule.animationModule.onToggleAction(param); }

        #endregion ENDREGION - UI Events and Actions (buttons and action groups)

        #region REGION - Standard KSP Overrides

        //standard KSP lifecyle override
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
        }

        //standard KSP lifecyle override
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
            initializeUI();
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
                updateFairing(false);
            }
            initializedDefaults = true;
            updateRCSModule();
            updateEngineModule();
            updateDragCubes();
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
            //the model-module update function handles updating of animations
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
            //the solar module fixed update function handles solar panel resource manipulation
            solarFunctionsModule.FixedUpdate();
        }

        //KSP editor modified event callback
        private void onEditorVesselModified(ShipConstruct ship)
        {
            //update available variants for attach node changes
            updateAvailableVariants();
        }

        //IPartMass/CostModifier override
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        //IPartMass/CostModifier override
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        //IPartMass/CostModifier override
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (modifiedMass == -1)
            {
                return 0;
            }
            return (subtractMass ? -defaultMass : 0) + modifiedMass;
        }

        //IPartMass/CostModifier override
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (modifiedCost == -1) { return 0; }
            return (subtractCost? -defaultCost : 0) + modifiedCost;
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

        //IContainerVolumeContributor override
        public ContainerContribution[] getContainerContributions()
        {
            ContainerContribution[] cts;
            float auxVol = 0;
            ContainerContribution ct0 = getCC("nose", noseContainerIndex, noseModule.moduleVolume * 1000f, ref auxVol);
            ContainerContribution ct1 = getCC("upper", upperContainerIndex, upperModule.moduleVolume * 1000f, ref auxVol);
            ContainerContribution ct2 = getCC("core", coreContainerIndex, coreModule.moduleVolume * 1000f, ref auxVol);
            ContainerContribution ct3 = getCC("lower", lowerContainerIndex, lowerModule.moduleVolume * 1000f, ref auxVol);
            ContainerContribution ct4 = getCC("mount", mountContainerIndex, mountModule.moduleVolume * 1000f, ref auxVol);
            ContainerContribution ct5 = new ContainerContribution("aux", auxContainerTargetIndex, auxVol);
            cts = new ContainerContribution[6] { ct0, ct1, ct2, ct3, ct4, ct5 };
            return cts;
        }

        private ContainerContribution getCC(string name, int index, float vol, ref float auxVol)
        {
            float ap = auxContainerPercent * 0.01f;
            float contVol = vol;
            if (index == auxContainerSourceIndex && auxContainerTargetIndex >= 0)
            {
                auxVol += vol * ap;
                contVol = (1 - ap) * vol;
            }
            return new ContainerContribution(name, index, contVol);
        }

        //IContractObjectiveModule override
        public string GetContractObjectiveType() { return "Generator"; }
        //IContractObjectiveModule override
        public bool CheckContractObjectiveValidity() { return solarFunctionsModule != null && solarFunctionsModule.standardPotentialOutput > 0; }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Custom Update Methods

        /// <summary>
        /// Initialization method.  Sets up model modules, loads their configs from the input config node.  Does all initial linking of part-modules.<para/>
        /// Does NOT set up their UI interaction -- that is all handled during OnStart()
        /// </summary>
        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;

            prevDiameter = currentDiameter;

            noseNodeNames = SSTUUtils.parseCSV(noseManagedNodes);
            upperNodeNames = SSTUUtils.parseCSV(upperManagedNodes);
            coreNodeNames = SSTUUtils.parseCSV(coreManagedNodes);
            lowerNodeNames = SSTUUtils.parseCSV(lowerManagedNodes);
            mountNodeNames = SSTUUtils.parseCSV(mountManagedNodes);

            //model-module setup/initialization
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);

            //list of CORE model nodes from config
            //each one may contain multiple 'model=modelDefinitionName' entries
            //but must contain no more than a single 'variant' entry.
            //if no variant is specified, they are added to the 'Default' variant.
            ConfigNode[] coreDefNodes = node.GetNodes("CORE");
            ModelDefinitionLayoutOptions[] coreDefs;
            List<ModelDefinitionLayoutOptions> coreDefList = new List<ModelDefinitionLayoutOptions>();
            int coreDefLen = coreDefNodes.Length;
            for (int i = 0; i < coreDefLen; i++)
            {
                string variantName = coreDefNodes[i].GetStringValue("variant", "Default");
                coreDefs = SSTUModelData.getModelDefinitionLayouts(coreDefNodes[i].GetStringValues("model"));
                coreDefList.AddUniqueRange(coreDefs);
                ModelDefinitionVariantSet mdvs = getVariantSet(variantName);
                mdvs.addModels(coreDefs);
            }
            coreDefs = coreDefList.ToArray();

            //model defs - brought here so we can capture the array rather than the config node+method call
            ModelDefinitionLayoutOptions[] noseDefs = SSTUModelData.getModelDefinitions(node.GetNodes("NOSE"));
            ModelDefinitionLayoutOptions[] upperDefs = SSTUModelData.getModelDefinitions(node.GetNodes("UPPER"));
            ModelDefinitionLayoutOptions[] lowerDefs = SSTUModelData.getModelDefinitions(node.GetNodes("LOWER"));
            ModelDefinitionLayoutOptions[] mountDefs = SSTUModelData.getModelDefinitions(node.GetNodes("MOUNT"));
            ModelDefinitionLayoutOptions[] solarDefs = SSTUModelData.getModelDefinitions(node.GetNodes("SOLAR"));
            ModelDefinitionLayoutOptions[] rcsUpDefs = SSTUModelData.getModelDefinitions(node.GetNodes("UPPERRCS"));
            ModelDefinitionLayoutOptions[] rcsDnDefs = SSTUModelData.getModelDefinitions(node.GetNodes("LOWERRCS"));

            noseModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-NOSE"), ModelOrientation.TOP, nameof(currentNose), null, nameof(currentNoseTexture), nameof(noseModulePersistentData), nameof(noseAnimationPersistentData), nameof(noseAnimationDeployLimit), nameof(noseDeployEvent), nameof(noseRetractEvent));
            noseModule.name = "ModularPart-Nose";
            noseModule.getSymmetryModule = m => m.noseModule;
            if (validateNose) { noseModule.getValidOptions =  () => upperModule.getValidUpperModels(noseDefs, noseModule.orientation, noseNodeNames, string.Empty); }
            else { noseModule.getValidOptions = () => noseDefs; }

            upperModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-UPPER"), ModelOrientation.TOP, nameof(currentUpper), null, nameof(currentUpperTexture), nameof(upperModulePersistentData), nameof(upperAnimationPersistentData), nameof(upperAnimationDeployLimit), nameof(upperDeployEvent), nameof(upperRetractEvent));
            upperModule.name = "ModularPart-Upper";
            upperModule.getSymmetryModule = m => m.upperModule;
            if (validateUpper) { upperModule.getValidOptions = () => coreModule.getValidUpperModels(upperDefs, upperModule.orientation, upperNodeNames, string.Empty); }
            else { upperModule.getValidOptions = () => upperDefs; }

            coreModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-CORE"), ModelOrientation.CENTRAL, nameof(currentCore), null, nameof(currentCoreTexture), nameof(coreModulePersistentData), nameof(coreAnimationPersistentData), nameof(coreAnimationDeployLimit), nameof(coreDeployEvent), nameof(coreRetractEvent));
            coreModule.name = "ModularPart-Core";
            coreModule.getSymmetryModule = m => m.coreModule;
            coreModule.getValidOptions = () => getVariantSet(currentVariant).definitions;

            lowerModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-LOWER"), ModelOrientation.BOTTOM, nameof(currentLower), null, nameof(currentLowerTexture), nameof(lowerModulePersistentData), nameof(lowerAnimationPersistentData), nameof(lowerAnimationDeployLimit), nameof(lowerDeployEvent), nameof(lowerRetractEvent));
            lowerModule.name = "ModularPart-Lower";
            lowerModule.getSymmetryModule = m => m.lowerModule;
            if (validateLower) { lowerModule.getValidOptions = () => coreModule.getValidLowerModels(lowerDefs, lowerModule.orientation, lowerNodeNames, string.Empty); }
            else { lowerModule.getValidOptions = () => lowerDefs; }

            mountModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-MOUNT"), ModelOrientation.BOTTOM, nameof(currentMount), null, nameof(currentMountTexture), nameof(mountModulePersistentData), nameof(mountAnimationPersistentData), nameof(mountAnimationDeployLimit), nameof(mountDeployEvent), nameof(mountRetractEvent));
            mountModule.name = "ModularPart-Mount";
            mountModule.getSymmetryModule = m => m.mountModule;
            if (validateMount) { mountModule.getValidOptions = () => lowerModule.getValidLowerModels(mountDefs, mountModule.orientation, mountNodeNames, "bottom"); }
            else { mountModule.getValidOptions = () => mountDefs; }

            solarModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-SOLAR"), ModelOrientation.CENTRAL, nameof(currentSolar), nameof(currentSolarLayout), nameof(currentSolarTexture), nameof(solarModulePersistentData), nameof(solarAnimationPersistentData), null, nameof(solarDeployEvent), nameof(solarRetractEvent));
            solarModule.name = "ModularPart-Solar";
            solarModule.getSymmetryModule = m => m.solarModule;
            solarModule.getValidOptions = () => solarDefs;
            solarModule.getLayoutPositionScalar = () => solarRad;
            solarModule.getLayoutScaleScalar = () => 1f;

            upperRcsModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-UPPERRCS"), ModelOrientation.CENTRAL, nameof(currentUpperRCS), nameof(currentUpperRCSLayout), nameof(currentUpperRCSTexture), nameof(upperRCSModulePersistentData), null, null, null, null);
            upperRcsModule.name = "ModularPart-UpperRCS";
            upperRcsModule.getSymmetryModule = m => m.upperRcsModule;
            upperRcsModule.getValidOptions = () => rcsUpDefs;
            upperRcsModule.getLayoutPositionScalar = () => upperRCSRad;
            upperRcsModule.getLayoutScaleScalar = () => 1f;

            lowerRcsModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-LOWERRCS"), ModelOrientation.CENTRAL, nameof(currentLowerRCS), nameof(currentLowerRCSLayout), nameof(currentLowerRCSTexture), nameof(lowerRCSModulePersistentData), null, null, null, null);
            lowerRcsModule.name = "ModularPart-LowerRCS";
            lowerRcsModule.getSymmetryModule = m => m.lowerRcsModule;
            lowerRcsModule.getValidOptions = () => rcsDnDefs;
            lowerRcsModule.getLayoutPositionScalar = () => lowerRCSRad;
            lowerRcsModule.getLayoutScaleScalar = () => 1f;

            noseModule.massScalar = massScalingPower;
            upperModule.massScalar = massScalingPower;
            coreModule.massScalar = massScalingPower;
            lowerModule.massScalar = massScalingPower;
            mountModule.massScalar = massScalingPower;
            solarModule.massScalar = massScalingPower;
            upperRcsModule.massScalar = massScalingPower;
            lowerRcsModule.massScalar = massScalingPower;

            noseModule.volumeScalar = volumeScalingPower;
            upperModule.volumeScalar = volumeScalingPower;
            coreModule.volumeScalar = volumeScalingPower;
            lowerModule.volumeScalar = volumeScalingPower;
            mountModule.volumeScalar = volumeScalingPower;
            solarModule.volumeScalar = volumeScalingPower;
            upperRcsModule.volumeScalar = volumeScalingPower;
            lowerRcsModule.volumeScalar = volumeScalingPower;

            //set up the model lists and load the currently selected model
            noseModule.setupModelList(noseDefs);
            upperModule.setupModelList(upperDefs);
            coreModule.setupModelList(coreDefs);
            lowerModule.setupModelList(lowerDefs);
            mountModule.setupModelList(mountDefs);
            upperRcsModule.setupModelList(rcsUpDefs);
            lowerRcsModule.setupModelList(rcsDnDefs);
            solarModule.setupModelList(solarDefs);
            coreModule.setupModel();
            upperModule.setupModel();
            noseModule.setupModel();
            lowerModule.setupModel();
            mountModule.setupModel();
            upperRcsModule.setupModel();
            lowerRcsModule.setupModel();
            solarModule.setupModel();

            //initialize RCS thrust transforms
            getModuleByName(upperRCSFunctionSource).renameRCSThrustTransforms(upperRCSThrustTransform);
            getModuleByName(lowerRCSFunctionSource).renameRCSThrustTransforms(lowerRCSThrustTransform);

            //TODO handle engine transform and gimbal transform initial renaming
            getModuleByName(engineTransformSource).renameEngineThrustTransforms(engineThrustTransform);
            getModuleByName(engineTransformSource).renameGimbalTransforms(gimbalTransform);

            //solar panel animation and solar panel UI controls
            solarFunctionsModule = new SolarModule(part, this, solarModule.animationModule, Fields[nameof(solarRotationPersistentData)], Fields[nameof(solarPanelStatus)]);
            solarFunctionsModule.getSymmetryModule = m => ((SSTUModularPart)m).solarFunctionsModule;
            solarFunctionsModule.setupSolarPanelData(solarModule.getSolarData(), solarModule.moduleModelTransforms);

            validateModules();
            updateModulePositions();
            updateMassAndCost();
            updateAttachNodes(false);
            updateAvailableVariants();
            SSTUStockInterop.updatePartHighlighting(part);
            SSTUModInterop.updateResourceVolume(part);
            nominalSolarOutput = solarFunctionsModule.standardPotentialOutput;
        }
        
        /// <summary>
        /// Initialize the UI controls, including default values, and specifying delegates for their 'onClick' methods.<para/>
        /// All UI based interaction code will be defined/run through these delegates.
        /// </summary>
        private void initializeUI()
        {
            Action<SSTUModularPart> modelChangedAction = (m) =>
            {
                m.validateModules();
                m.updateModulePositions();
                m.updateMassAndCost();
                m.updateAttachNodes(true);
                m.updateFairing(true);
                m.updateAvailableVariants();
                m.updateDragCubes();
                m.updateRCSModule();
                m.updateEngineModule();
                SSTUModInterop.updateResourceVolume(m.part);
            };

            //set up the core variant UI control
            string[] variantNames = SSTUUtils.getNames(variantSets.Values, m => m.variantName);
            this.updateUIChooseOptionControl(nameof(currentVariant), variantNames, variantNames, true, currentVariant);
            Fields[nameof(currentVariant)].guiActiveEditor = variantSets.Count > 1;

            Fields[nameof(currentVariant)].uiControlEditor.onFieldChanged = (a, b) => 
            {
                //TODO find variant set for the currently enabled core model
                //query the index from that variant set
                ModelDefinitionVariantSet prevMdvs = getVariantSet(coreModule.definition.name);
                //this is the index of the currently selected model within its variant set
                int previousIndex = prevMdvs.indexOf(coreModule.layoutOptions);
                //grab ref to the current/new variant set
                ModelDefinitionVariantSet mdvs = getVariantSet(currentVariant);
                //and a reference to the model from same index out of the new set ([] call does validation internally for IAOOBE)
                ModelDefinitionLayoutOptions newCoreDef = mdvs[previousIndex];
                //now, call model-selected on the core model to update for the changes, including symmetry counterpart updating.
                this.actionWithSymmetry(m => 
                {
                    m.currentVariant = currentVariant;
                    m.coreModule.modelSelected(newCoreDef.definition.name);
                    modelChangedAction(m);
                });
            };

            Fields[nameof(currentDiameter)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentDiameter = this.currentDiameter; }
                    modelChangedAction(m);
                    m.prevDiameter = m.currentDiameter;
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentVScale)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentVScale = this.currentVScale; }
                    modelChangedAction(m);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentNose)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                noseModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentUpper)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                upperModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentCore)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                coreModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentLower)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                lowerModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentMount)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                mountModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                SSTUStockInterop.fireEditorUpdate();
            };

            //------------------SOLAR MODULE UI INIT---------------------//
            Fields[nameof(currentSolar)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                solarModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    modelChangedAction(m);
                    m.solarFunctionsModule.setupSolarPanelData(m.solarModule.getSolarData(), m.solarModule.moduleModelTransforms);
                });
                SSTUStockInterop.fireEditorUpdate();
                nominalSolarOutput = solarFunctionsModule.standardPotentialOutput;
            };

            Fields[nameof(currentSolarLayout)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                solarModule.layoutSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    modelChangedAction(m);
                    m.solarFunctionsModule.setupSolarPanelData(m.solarModule.getSolarData(), m.solarModule.moduleModelTransforms);
                });
                nominalSolarOutput = solarFunctionsModule.standardPotentialOutput;
            };

            Fields[nameof(currentSolarParent)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentSolarParent = currentSolarParent; }
                    m.updateModulePositions();
                    m.updateDragCubes();
                });
            };

            Fields[nameof(currentSolarOffset)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentSolarOffset = currentSolarOffset; }
                    m.updateModulePositions();
                    m.updateDragCubes();
                });
            };

            //------------------UPPER RCS MODULE UI INIT---------------------//
            Fields[nameof(currentUpperRCS)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                upperRcsModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.upperRcsModule.renameRCSThrustTransforms(m.upperRCSThrustTransform);
                    modelChangedAction(m);
                    m.updateRCSModule();
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentUpperRCSLayout)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                upperRcsModule.layoutSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
            };

            Fields[nameof(currentUpperRCSParent)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentUpperRCSParent = currentUpperRCSParent; }
                    m.updateModulePositions();
                    m.updateDragCubes();
                });
            };

            Fields[nameof(currentUpperRCSOffset)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentUpperRCSOffset = this.currentUpperRCSOffset; }
                    m.updateModulePositions();
                    m.updateDragCubes();
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            //------------------LOWER RCS MODULE UI INIT---------------------//
            Fields[nameof(currentLowerRCS)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                lowerRcsModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.lowerRcsModule.renameRCSThrustTransforms(m.lowerRCSThrustTransform);
                    modelChangedAction(m);
                    m.updateRCSModule();
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentLowerRCSLayout)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                lowerRcsModule.layoutSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
            };

            Fields[nameof(currentLowerRCSParent)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentLowerRCSParent = currentLowerRCSParent; }
                    m.updateModulePositions();
                    m.updateDragCubes();
                });
            };

            Fields[nameof(currentLowerRCSOffset)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentLowerRCSOffset = this.currentLowerRCSOffset; }
                    m.updateModulePositions();
                    m.updateDragCubes();
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            //------------------MODEL DIAMETER SWITCH UI INIT---------------------//
            if (maxDiameter == minDiameter)
            {
                Fields[nameof(currentDiameter)].guiActiveEditor = false;
            }
            else
            {
                this.updateUIFloatEditControl(nameof(currentDiameter), minDiameter, maxDiameter, diameterIncrement * 2, diameterIncrement, diameterIncrement * 0.05f, true, currentDiameter);
            }
            Fields[nameof(currentVScale)].guiActiveEditor = enableVScale;

            //------------------AUX CONTAINER SWITCH UI INIT---------------------//
            Fields[nameof(auxContainerPercent)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.auxContainerPercent = this.auxContainerPercent; }
                    SSTUModInterop.updateResourceVolume(m.part);
                    SSTUStockInterop.fireEditorUpdate();
                });
            };
            if (auxContainerMinPercent == auxContainerMaxPercent || auxContainerSourceIndex < 0 || auxContainerTargetIndex < 0)
            {
                Fields[nameof(auxContainerPercent)].guiActiveEditor = false;
            }
            else
            {
                this.updateUIFloatEditControl(nameof(auxContainerPercent), auxContainerMinPercent, auxContainerMaxPercent, 5f, 1f, 0.1f, false, auxContainerPercent);
            }

            //------------------MODULE TEXTURE SWITCH UI INIT---------------------//
            Fields[nameof(currentNoseTexture)].uiControlEditor.onFieldChanged = noseModule.textureSetSelected;
            Fields[nameof(currentUpperTexture)].uiControlEditor.onFieldChanged = upperModule.textureSetSelected;
            Fields[nameof(currentCoreTexture)].uiControlEditor.onFieldChanged = coreModule.textureSetSelected;
            Fields[nameof(currentLowerTexture)].uiControlEditor.onFieldChanged = lowerModule.textureSetSelected;
            Fields[nameof(currentMountTexture)].uiControlEditor.onFieldChanged = mountModule.textureSetSelected;
            Fields[nameof(currentSolarTexture)].uiControlEditor.onFieldChanged = solarModule.textureSetSelected;
            Fields[nameof(currentUpperRCSTexture)].uiControlEditor.onFieldChanged = upperRcsModule.textureSetSelected;
            Fields[nameof(currentLowerRCSTexture)].uiControlEditor.onFieldChanged = lowerRcsModule.textureSetSelected;

            //------------------MODULE PARENT SWITCH UI INIT---------------------//
            string[] upperRCSOptions = upperRCSParentOptions.Split(',');
            this.updateUIChooseOptionControl(nameof(currentUpperRCSParent), upperRCSOptions, upperRCSOptions, true, currentUpperRCSParent);
            string[] lowerRCSOptions = lowerRCSParentOptions.Split(',');
            this.updateUIChooseOptionControl(nameof(currentLowerRCSParent), lowerRCSOptions, lowerRCSOptions, true, currentLowerRCSParent);
            string[] solarOptions = solarParentOptions.Split(',');
            this.updateUIChooseOptionControl(nameof(currentSolarParent), solarOptions, solarOptions, true, currentSolarParent);

            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        //TODO - rcs/solar validation
        /// <summary>
        /// Validate the currently selected models, and select update any that are found to be invalid by setting to the first usable option form their model list.<para/>
        /// Does not validate CORE, but updates other models from core outward.  Includes validating of solar and RCS options, setting to empty model if current parent slot cannot support RCS.
        /// </summary>
        private void validateModules()
        {
            //core module is automatically 'valid' -- don't touch it.
            //but do need to validate upper+nose, and lower+mount
            //as well as validating the solar/RCS (parent position + enabled/disbled status)

            //validate upper model
            if (validateUpper && !coreModule.isValidUpper(upperModule))
            {
                ModelDefinition def = coreModule.findFirstValidUpper(upperModule);
                if (def == null) { error("Could not locate valid definition for UPPER"); }
                upperModule.modelSelected(def.name);
            }
            //validate nose model regardless of if upper changed or not
            if (validateNose && !upperModule.isValidUpper(noseModule))
            {
                ModelDefinition def = upperModule.findFirstValidUpper(noseModule);
                if (def == null) { error("Could not locate valid definition for NOSE"); }
                noseModule.modelSelected(def.name);
            }
            //validate lower model
            if (validateLower && !coreModule.isValidLower(lowerModule))
            {
                ModelDefinition def = coreModule.findFirstValidLower(lowerModule);
                if (def == null) { error("Could not locate valid definition for LOWER"); }
                lowerModule.modelSelected(def.name);
            }
            //validate mount model
            if (validateMount && !lowerModule.isValidLower(mountModule))
            {
                ModelDefinition def = lowerModule.findFirstValidLower(mountModule);
                if (def == null) { error("Could not locate valid definition for MOUNT"); }
                mountModule.modelSelected(def.name);
            }
            //TODO validate solar/RCS selections (model and parent)
            //what determines valid RCS/solar options for a given module?
            //what determines valid parent options for a given configuration?
            ModelModule<SSTUModularPart> p = getModuleByName(currentUpperRCSParent);
            if (!p.rcsParentEnabled)
            {
                //TODO -- disable RCS module (set to inactive model), and/or move it to a valid parent slot.
            }
            p = getModuleByName(currentLowerRCSParent);
            if (!p.rcsParentEnabled)
            {
                //TODO -- disable RCS module (set to inactive model), and/or move it to a valid parent slot.
            }
            p = getModuleByName(currentSolarParent);
            if (!p.rcsParentEnabled)//TODO -- how to determine if a model is valid for mounting of solar panels, and -where- to mount them on the model?
            {
                //TODO -- disable solar module (set to inactive model), and/or move it to a valid parent slot
            }
        }
        
        /// <summary>
        /// Update the scale and position values for all currently configured models.  Does no validation, only updates positions.<para/>
        /// After calling this method, all models will be scaled and positioned according to their internal position/scale values and the orientations/offsets defined in the models.
        /// </summary>
        private void updateModulePositions()
        {
            //scales for modules depend on the module above/below them
            //first set the scale for the core module -- this depends directly on the UI specified 'diameter' value.
            coreModule.setScaleForDiameter(currentDiameter, currentVScale);

            //next, set upper, and then nose scale values
            upperModule.setDiameterFromBelow(coreModule.moduleUpperDiameter, currentVScale);
            noseModule.setDiameterFromBelow(upperModule.moduleUpperDiameter, currentVScale);

            //finally, set lower and mount scale values
            lowerModule.setDiameterFromAbove(coreModule.moduleLowerDiameter, currentVScale);
            mountModule.setDiameterFromAbove(lowerModule.moduleLowerDiameter, currentVScale);

            //total height of the part is determined by the sum of the heights of the modules at their current scale
            float totalHeight = noseModule.moduleHeight;
            totalHeight += upperModule.moduleHeight;
            totalHeight += coreModule.moduleHeight;
            totalHeight += lowerModule.moduleHeight;
            totalHeight += mountModule.moduleHeight;

            //position of each module is set such that the vertical center of the models is at part origin/COM
            float pos = totalHeight * 0.5f;//abs top of model
            pos -= noseModule.moduleHeight;//bottom of nose model
            noseModule.setPosition(pos);
            pos -= upperModule.moduleHeight;//bottom of upper model
            upperModule.setPosition(pos);
            pos -= coreModule.moduleHeight * 0.5f;//center of 'core' model
            coreModule.setPosition(pos);
            pos -= coreModule.moduleHeight * 0.5f;//bottom of 'core' model
            lowerModule.setPosition(pos);
            pos -= lowerModule.moduleHeight;//bottom of 'lower' model
            mountModule.setPosition(pos);

            //update actual model positions and scales
            noseModule.updateModelMeshes();
            upperModule.updateModelMeshes();
            coreModule.updateModelMeshes();
            lowerModule.updateModelMeshes();
            mountModule.updateModelMeshes();
            
            //scale and position of RCS and solar models handled a bit differently
            float coreScale = coreModule.moduleHorizontalScale;

            ModelModule<SSTUModularPart> module = getModuleByName(currentSolarParent);
            module.getSolarMountingValues(currentSolarOffset, out solarRad, out pos);
            solarModule.setScale(coreScale);
            solarModule.setPosition(pos);
            solarModule.updateModelMeshes();
            solarFunctionsModule.powerScalar = Mathf.Pow(coreScale, solarScalingPower);

            module = getModuleByName(currentUpperRCSParent);
            module.getRCSMountingValues(currentUpperRCSOffset, true, out upperRCSRad, out pos);
            upperRcsModule.setScale(coreScale);
            upperRcsModule.setPosition(pos);
            upperRcsModule.updateModelMeshes(); 

            module = getModuleByName(currentLowerRCSParent);
            module.getRCSMountingValues(currentLowerRCSOffset, false, out lowerRCSRad, out pos);
            lowerRcsModule.setScale(coreScale);
            lowerRcsModule.setPosition(pos);
            lowerRcsModule.updateModelMeshes();

            module = getModuleByName(upperRCSFunctionSource);
            module.renameRCSThrustTransforms(upperRCSThrustTransform);
            module = getModuleByName(lowerRCSFunctionSource);
            module.renameRCSThrustTransforms(lowerRCSThrustTransform);
        }

        /// <summary>
        /// Update the cached modifiedMass and modifiedCost field values.  Used with stock cost/mass modifier interface.<para/>
        /// Optionally includes adapter mass/cost if enabled in config.
        /// </summary>
        private void updateMassAndCost()
        {
            modifiedMass = coreModule.moduleMass;
            modifiedMass += solarModule.moduleMass;
            modifiedMass += upperRcsModule.moduleMass;
            modifiedMass += lowerRcsModule.moduleMass;
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
        
        /// <summary>
        /// Update the ModuleRCSXX with the current stats for the current configuration (thrust, ISP, fuel type)
        /// </summary>
        private void updateRCSModule()
        {
            ModuleRCS[] rcsModules = part.GetComponents<ModuleRCS>();
            if (rcsModules == null || rcsModules.Length == 0) { return; }//nothing to update
            ModelModule<SSTUModularPart> upperRCSSource = getModuleByName(upperRCSFunctionSource);
            ModelModule<SSTUModularPart> lowerRCSSource = getModuleByName(lowerRCSFunctionSource);
            //if only a single index used (e.g. StationCore part setups, where upper/lower are used simply for two different layouts)
            //use the data from model in the 'upper' slot
            if (upperRCSIndex == lowerRCSIndex && upperRCSIndex >= 0 && upperRCSIndex < rcsModules.Length)
            {
                upperRCSSource.updateRCSModule(rcsModules[upperRCSIndex], 3);
            }
            else
            {
                //else treat each module individually, if indexes are valid
                if (upperRCSIndex < rcsModules.Length && upperRCSIndex >= 0)
                {
                    upperRCSSource.updateRCSModule(rcsModules[upperRCSIndex], thrustScalingPower);
                }
                if (lowerRCSIndex < rcsModules.Length && lowerRCSIndex >= 0)
                {
                    lowerRCSSource.updateRCSModule(rcsModules[lowerRCSIndex], thrustScalingPower);
                }
            }
        }

        /// <summary>
        /// Update the ModuleEnginesXX with the current stats for the current configuration.
        /// </summary>
        private void updateEngineModule()
        {
            ModelModule<SSTUModularPart> engineTransformSource = getModuleByName(this.engineTransformSource);
            engineTransformSource.renameEngineThrustTransforms(engineThrustTransform);
            engineTransformSource.renameGimbalTransforms(gimbalTransform);
            ModuleEngines engine = part.GetComponent<ModuleEngines>();
            if (engine != null)
            {
                ModelModule<SSTUModularPart> engineThrustSource = getModuleByName(this.engineThrustSource);
                engineThrustSource.updateEngineModuleThrust(engine, thrustScalingPower);
            }

            //re-init gimbal module
            ModuleGimbal gimbal = part.GetComponent<ModuleGimbal>();
            if (gimbal != null)
            {
                //check to see that gimbal was already initialized
                if ((HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight) && gimbal.gimbalTransforms!=null)
                {
                    float range = 0;
                    gimbal.OnStart(StartState.Flying);
                    if (engineTransformSource.definition.engineTransformData != null) { range = engineTransformSource.definition.engineTransformData.gimbalFlightRange; }
                    gimbal.gimbalRange = range;

                    //re-init gimbal offset module if it exists
                    SSTUGimbalOffset gOffset = part.GetComponent<SSTUGimbalOffset>();
                    if (gOffset != null)
                    {
                        range = 0;
                        if (engineTransformSource.definition.engineTransformData != null)
                        {
                            range = engineTransformSource.definition.engineTransformData.gimbalAdjustmentRange;
                        }
                        gOffset.gimbalXRange = range;
                        gOffset.gimbalZRange = range;
                        gOffset.reInitialize();
                    }
                }
            }

            SSTUAnimateEngineHeat engineHeat = part.GetComponent<SSTUAnimateEngineHeat>();
            if (engineHeat != null)
            {
                engineHeat.reInitialize();
            }

            SSTUModelConstraint constraints = part.GetComponent<SSTUModelConstraint>();
            if (constraints!=null)
            {
                ConfigNode constraintNode;
                if (engineTransformSource.definition.constraintData != null)
                {
                    constraintNode = engineTransformSource.definition.constraintData.constraintNode;
                }
                else
                {
                    constraintNode = new ConfigNode("CONSTRAINT");
                }
                constraints.loadExternalData(constraintNode);
            }
            updateEffectsScale();
        }

        /// <summary>
        /// Rescales the values in the EFFECTS node from the prefab part for the current model scale, and then reloads the effects
        /// </summary>
        private void updateEffectsScale()
        {
            float diameterScale = coreModule.moduleHorizontalScale;
            if (part.partInfo != null && part.partInfo.partConfig != null && part.partInfo.partConfig.HasNode("EFFECTS"))
            {
                //get the base EFFECTS node from the part config
                ConfigNode effectsNode = part.partInfo.partConfig.GetNode("EFFECTS");
                //create a copy of it, so as to not adjust the original node
                ConfigNode copiedEffectsNode = new ConfigNode("EFFECTS");
                effectsNode.CopyTo(copiedEffectsNode);
                //TODO clean up foreach
                foreach (ConfigNode innerNode1 in copiedEffectsNode.nodes)
                {
                    //TODO clean up foreach
                    foreach (ConfigNode innerNode2 in innerNode1.nodes)
                    {
                        //local position offset is common for both stock effects and real-plume effects
                        if (innerNode2.HasValue("localPosition"))
                        {
                            Vector3 pos = innerNode2.GetVector3("localPosition");
                            pos *= diameterScale;
                            innerNode2.SetValue("localPosition", (pos.x + ", " + pos.y + ", " + pos.z), false);
                        }
                        //TODO expand this to explicitly test for RealPlume somehow?
                        //TODO also include additional paramaters that may need scaling (how to determine scaling factors?)
                        //fixedScale is only used by RealPlume, but handles pretty much all scaling effects more or less properly
                        if (innerNode2.HasValue("fixedScale"))//real-plumes scaling
                        {
                            float fixedScaleVal = innerNode2.GetFloatValue("fixedScale");
                            fixedScaleVal *= diameterScale;
                            innerNode2.SetValue("fixedScale", fixedScaleVal.ToString(), false);
                        }
                        else if (innerNode2.HasValue("emission"))//stock effects scaling
                        {
                            //TODO -- stock has some strange scaling values applied to the effects, inverse of model transform scale
                            //possibly in an attempt to keep the effect the same 'scale' regardless of transform scale
                            //... but it looks so terrible...
                            String[] emissionVals = innerNode2.GetValues("emission");
                            for (int i = 0; i < emissionVals.Length; i++)
                            {
                                String val = emissionVals[i];
                                String[] splitVals = val.Split(new char[] { ' ' });
                                String replacement = "";
                                int len = splitVals.Length;
                                for (int k = 0; k < len; k++)
                                {
                                    if (k == 1)//the 'value' portion 
                                    {
                                        float emissionValue = SSTUUtils.safeParseFloat(splitVals[k]) * diameterScale;
                                        splitVals[k] = emissionValue.ToString();
                                    }
                                    replacement = replacement + splitVals[k];
                                    if (k < len - 1) { replacement = replacement + " "; }
                                }
                                emissionVals[i] = replacement;
                            }
                            innerNode2.RemoveValues("emission");
                            foreach (String replacementVal in emissionVals)
                            {
                                innerNode2.AddValue("emission", replacementVal);
                            }

                            //scale speed along with emission
                            if (innerNode2.HasValue("speed"))
                            {
                                String[] speedBaseVals = innerNode2.GetValues("speed");
                                int len = speedBaseVals.Length;
                                for (int i = 0; i < len; i++)
                                {
                                    String replacement = "";
                                    String[] speedSplitVals = speedBaseVals[i].Split(new char[] { ' ' });
                                    for (int k = 0; k < speedSplitVals.Length; k++)
                                    {
                                        if (k == 1)
                                        {
                                            float speedVal = SSTUUtils.safeParseFloat(speedSplitVals[k]) * diameterScale;
                                            speedSplitVals[k] = speedVal.ToString();
                                        }
                                        replacement = replacement + speedSplitVals[k];
                                        if (k < len - 1) { replacement = replacement + " "; }
                                    }
                                    speedBaseVals[i] = replacement;
                                }
                                innerNode2.RemoveValues("speed");
                                foreach (String replacementVal in speedBaseVals)
                                {
                                    innerNode2.AddValue("speed", replacementVal);
                                }
                            }
                            //do stock effects support any kind of 'size' manipulation?
                        }
                    }
                }
                part.Effects.OnLoad(copiedEffectsNode);
            }
        }

        /// <summary>
        /// Update the attach nodes for the current model-module configuration. 
        /// The 'nose' module is responsible for updating of upper attach nodes, while the 'mount' module is responsible for lower attach nodes.
        /// Also includes updating of 'interstage' nose/mount attach nodes.
        /// Also includes updating of surface-attach node position.
        /// Also includes updating of any parts that are surface attached to this part.
        /// </summary>
        /// <param name="userInput"></param>
        private void updateAttachNodes(bool userInput)
        {
            //update the standard top and bottom attach nodes, using the node position(s) defined in the nose and mount modules
            noseModule.updateAttachNodeTop("top", userInput);
            mountModule.updateAttachNodeBottom("bottom", userInput);

            //update the model-module specific attach nodes, using the per-module node definitions from the part
            noseModule.updateAttachNodeBody(noseNodeNames, userInput);
            upperModule.updateAttachNodeBody(upperNodeNames, userInput);
            coreModule.updateAttachNodeBody(coreNodeNames, userInput);
            lowerModule.updateAttachNodeBody(lowerNodeNames, userInput);
            mountModule.updateAttachNodeBody(mountNodeNames, userInput);

            //update the nose interstage node, using the node position as specified by the nose module's fairing offset parameter
            ModelModule<SSTUModularPart> nodeModule = getUpperFairingModelModule();
            Vector3 pos = new Vector3(0, nodeModule.fairingBottom, 0);
            SSTUSelectableNodes.updateNodePosition(part, noseInterstageNode, pos);
            AttachNode noseInterstage = part.FindAttachNode(noseInterstageNode);
            if (noseInterstage != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, noseInterstage, pos, Vector3.up, userInput);
            }

            //update the nose interstage node, using the node position as specified by the nose module's fairing offset parameter
            nodeModule = getLowerFairingModelModule();
            pos = new Vector3(0, nodeModule.fairingTop, 0);
            SSTUSelectableNodes.updateNodePosition(part, mountInterstageNode, pos);
            AttachNode mountInterstage = part.FindAttachNode(mountInterstageNode);
            if (mountInterstage != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, mountInterstage, pos, Vector3.down, userInput);
            }

            //update surface attach node position, part position, and any surface attached children
            //TODO -- how to determine how far to offset/move surface attached children?
            AttachNode surfaceNode = part.srfAttachNode;
            if (surfaceNode != null)
            {
                coreModule.updateSurfaceAttachNode(surfaceNode, prevDiameter, userInput);
            }
        }
        
        /// <summary>
        /// Update the current fairing modules (top, centra, and bottom) for the current model-module configuration (diameters, positions).
        /// </summary>
        /// <param name="userInput"></param>
        private void updateFairing(bool userInput)
        {
            SSTUNodeFairing[] modules = part.GetComponents<SSTUNodeFairing>();
            if (centralFairingIndex >= 0 && centralFairingIndex < modules.Length)
            {
                bool enabled = coreModule.fairingEnabled;
                SSTUNodeFairing coreFairing = modules[centralFairingIndex];
                float top = coreModule.fairingTop;
                float bot = coreModule.fairingBottom;
                FairingUpdateData data = new FairingUpdateData();
                data.setTopY(top);
                data.setTopRadius(currentDiameter * 0.5f);
                data.setBottomY(bot);
                data.setBottomRadius(currentDiameter * 0.5f);
                data.setEnable(enabled);
                coreFairing.updateExternal(data);
            }
            if (topFairingIndex >= 0 && topFairingIndex < modules.Length)
            {
                ModelModule<SSTUModularPart> moduleForUpperFiaring = getUpperFairingModelModule();
                bool enabled = moduleForUpperFiaring.fairingEnabled;
                SSTUNodeFairing topFairing = modules[topFairingIndex];
                float topFairingBottomY = moduleForUpperFiaring.fairingBottom;
                FairingUpdateData data = new FairingUpdateData();
                data.setTopY(getPartTopY());
                data.setBottomY(topFairingBottomY);
                data.setBottomRadius(currentDiameter * 0.5f);
                data.setEnable(enabled);
                if (userInput) { data.setTopRadius(currentDiameter * 0.5f); }
                topFairing.updateExternal(data);
            }
            if (bottomFairingIndex >= 0 && bottomFairingIndex < modules.Length)
            {
                ModelModule<SSTUModularPart> moduleForLowerFairing = getLowerFairingModelModule();
                bool enabled = moduleForLowerFairing.fairingEnabled;
                SSTUNodeFairing bottomFairing = modules[bottomFairingIndex];
                float bottomFairingTopY = moduleForLowerFairing.fairingTop;
                FairingUpdateData data = new FairingUpdateData();
                data.setTopRadius(currentDiameter * 0.5f);
                data.setTopY(bottomFairingTopY);
                data.setEnable(enabled);
                if (userInput) { data.setBottomRadius(currentDiameter * 0.5f); }
                bottomFairing.updateExternal(data);
            }
        }

        /// <summary>
        /// Return the total height of this part in its current configuration.  This will be the distance from the bottom attach node to the top attach node, and may not include any 'extra' structure.
        /// </summary>
        /// <returns></returns>
        private float getTotalHeight()
        {
            float totalHeight = noseModule.moduleHeight;
            totalHeight += upperModule.moduleHeight;
            totalHeight += coreModule.moduleHeight;
            totalHeight += lowerModule.moduleHeight;
            totalHeight += mountModule.moduleHeight;
            return totalHeight;
        }

        /// <summary>
        /// Return the topmost position in the models relative to the part's origin.
        /// </summary>
        /// <returns></returns>
        private float getPartTopY()
        {
            return getTotalHeight() * 0.5f;
        }

        /// <summary>
        /// Return the ModelModule slot responsible for upper attach point of lower fairing module
        /// </summary>
        /// <returns></returns>
        private ModelModule<SSTUModularPart> getLowerFairingModelModule()
        {
            float coreBaseDiam = coreModule.moduleDiameter;
            if (coreModule.moduleLowerDiameter < coreBaseDiam) { return coreModule; }
            if (lowerModule.moduleLowerDiameter < coreBaseDiam) { return lowerModule; }
            return mountModule;
        }

        /// <summary>
        /// Return the ModelModule slot responsible for lower attach point of the upper fairing module
        /// </summary>
        /// <returns></returns>
        private ModelModule<SSTUModularPart> getUpperFairingModelModule()
        {
            float coreBaseDiam = coreModule.moduleDiameter;
            if (coreModule.moduleUpperDiameter < coreBaseDiam) { return coreModule; }
            if (upperModule.moduleUpperDiameter < coreBaseDiam) { return upperModule; }
            return noseModule;
        }

        /// <summary>
        /// Update the UI visibility for the currently available selections.<para/>
        /// Will hide/remove UI fields for slots with only a single option (models, textures, layouts).
        /// </summary>
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

            ModelModule<SSTUModularPart> lowerParent, upperParent, solarParent;
            lowerParent = getModuleByName(currentLowerRCSParent);
            upperParent = getModuleByName(currentUpperRCSParent);
            solarParent = getModuleByName(currentSolarParent);

            bool lowerRCSControlsEnabled = lowerRcsModule.rcsModuleEnabled;
            bool upperRCSControlsEnabled = upperRcsModule.rcsModuleEnabled;
            bool solarControlsEnabled = solarModule.solarEnabled;

            Fields[nameof(currentLowerRCSParent)].guiActiveEditor = lowerRCSControlsEnabled && lowerRCSParentOptions.Split(',').Length > 1;
            Fields[nameof(currentLowerRCSOffset)].guiActiveEditor = lowerRCSControlsEnabled && lowerParent.getRCSMountingRange(false) > 0;
            Fields[nameof(currentLowerRCSLayout)].guiActiveEditor = lowerRCSControlsEnabled && lowerRcsModule.layoutOptions.layouts.Count() > 1;

            Fields[nameof(currentUpperRCSParent)].guiActiveEditor = upperRCSControlsEnabled && upperRCSParentOptions.Split(',').Length > 1;
            Fields[nameof(currentUpperRCSOffset)].guiActiveEditor = upperRCSControlsEnabled && upperParent.getRCSMountingRange(true) > 0;
            Fields[nameof(currentUpperRCSLayout)].guiActiveEditor = upperRCSControlsEnabled && upperRcsModule.layoutOptions.layouts.Count() > 1;

            Fields[nameof(currentSolarParent)].guiActiveEditor = solarControlsEnabled && solarParentOptions.Split(',').Length > 1;
            Fields[nameof(currentSolarOffset)].guiActiveEditor = solarControlsEnabled && solarParent.definition.solarPositionData!=null && solarParent.definition.solarPositionData.range > 0;
            Fields[nameof(currentSolarLayout)].guiActiveEditor = solarControlsEnabled && solarModule.layoutOptions.layouts.Count() > 1;
        }

        /// <summary>
        /// Calls the generic SSTU procedural drag-cube updating routines.  Will update the drag cubes for whatever the current model state is.
        /// </summary>
        private void updateDragCubes()
        {
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }
        
        /// <summary>
        /// Return the root transform for the specified name.  If does not exist, will create it and parent it to the parts' 'model' transform.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="recreate"></param>
        /// <returns></returns>
        private Transform getRootTransform(string name)
        {
            Transform root = part.transform.FindRecursive(name);
            if (root != null)
            {
                GameObject.DestroyImmediate(root.gameObject);
                root = null;
            }
            root = new GameObject(name).transform;
            root.NestToParent(part.transform.FindRecursive("model"));
            return root;
        }

        /// <summary>
        /// Return the model-module corresponding to the input slot name.  Valid slot names are: NOSE,UPPER,CORE,LOWER,MOUNT
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private ModelModule<SSTUModularPart> getModuleByName(string name)
        {
            switch (name)
            {
                case "NOSE":
                    return noseModule;
                case "UPPER":
                    return upperModule;
                case "CORE":
                    return coreModule;
                case "LOWER":
                    return lowerModule;
                case "MOUNT":
                    return mountModule;
                case "NONE":
                    return null;
                case "SOLAR":
                    return solarModule;
                case "UPPERRCS":
                    return upperRcsModule;
                case "LOWERRCS":
                    return lowerRcsModule;
                default:
                    return null;
            }
        }

        #endregion ENDREGION - Custom Update Methods

        #region REGION - Mod Interop

        public Bounds getModuleBounds(string moduleName)
        {
            switch (moduleName)
            {
                case "NOSE":
                    return noseModule.getModelBounds();
                case "UPPER":
                    return upperModule.getModelBounds();
                case "CORE":
                    return coreModule.getModelBounds();
                case "LOWER":
                    return lowerModule.getModelBounds();
                case "MOUNT":
                    return mountModule.getModelBounds();
                case "NONE":
                    return new Bounds();
                case "SOLAR":
                    return solarModule.getModelBounds();
                case "UPPERRCS":
                    return upperRcsModule.getModelBounds();
                case "LOWERRCS":
                    return lowerRcsModule.getModelBounds();
                default:
                    return new Bounds();
            }

        }

        public Bounds getModuleBounds(string[] moduleNames)
        {
            Bounds bounds = new Bounds();
            int len = moduleNames.Length;
            for (int i = 0; i < len; i++)
            {
                bounds.Encapsulate(getModuleBounds(moduleNames[i]));
            }
            return bounds;
        }

        #endregion

    }

    /// <summary>
    /// Data storage for a group of model definitions that share the same 'variant' type.  Used by modular-part in variant-defined configurations.
    /// </summary>
    public class ModelDefinitionVariantSet
    {
        public readonly string variantName;

        public ModelDefinitionLayoutOptions[] definitions = new ModelDefinitionLayoutOptions[0];
        
        public ModelDefinitionLayoutOptions this[int index]
        {
            get
            {
                if (index < 0) { index = 0; }
                if (index >= definitions.Length) { index = definitions.Length - 1; }
                return definitions[index];
            }
        }

        public ModelDefinitionVariantSet(string name)
        {
            this.variantName = name;
        }

        public void addModels(ModelDefinitionLayoutOptions[] defs)
        {
            List<ModelDefinitionLayoutOptions> allDefs = new List<ModelDefinitionLayoutOptions>();
            allDefs.AddRange(definitions);
            allDefs.AddUniqueRange(defs);
            definitions = allDefs.ToArray();
        }

        public int indexOf(ModelDefinitionLayoutOptions def)
        {
            return definitions.IndexOf(def);
        }

    }

}
