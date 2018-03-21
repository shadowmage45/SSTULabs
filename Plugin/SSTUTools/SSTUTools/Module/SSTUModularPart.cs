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

        /**
        * UI Layout for part right-click menu (Editor)
        * Line     Feature
        * 1        Diameter Adjust Control
        * 2        Nose Selection
        * 3        Upper Selection
        * 4        Core Selection
        * 5        Lower Selection
        * 6        Mount Selection
        * 7        Upper RCS Mounting Choice (nose/upper/core)
        * 8        Upper RCS Selection
        * 9        Upper RCS Offset
        * 10       Upper RCS Layout
        * 11       Lower RCS Mounting Choice (core/lower/mount)
        * 12       Lower RCS Selection
        * 13       Lower RCS Offset
        * 14       Lower RCS Layout
        * 15       Solar Panel Mouting Choice (upper/core/lower)
        * 16       Solar Panel Selection
        * 17       Solar Panel Layout
        * 18       Nose Texture
        * 19       Upper Texture
        * 20       Core Texture
        * 21       Lower Texture
        * 22       Mount Texture
        * 23       Upper RCS Texture
        * 24       Lower RCS Texture
        * 25       Solar Panel Texture
        * 26       Nose Animation Toggle
        * 27       Nose Animation Deploy Limit
        * 28       Upper Animation Toggle
        * 29       Upper Animation Deploy Limit
        * 30       Core Animation Toggle
        * 31       Core Animation Deploy Limit
        * 32       Lower Animation Toggle
        * 33       Lower Animation Deploy Limit
        * 34       Mount Animation Toggle
        * 35       Mount Animation Deploy Limit
        * 36       Open Volume Container GUI
        * 37       Open Recoloring GUI
        * 38+++    Stock RCS, gimbal, ReactionWheel toggles, resources (lots more lines)
        **/

        /**
        * UI Layout for part right-click menu (Flight)
        * Line     Feature
        * 1        Solar Panel Status
        * 2        Nose Animation Toggle
        * 3        Nose Animation Deploy Limit
        * 4        Upper Animation Toggle
        * 5        Upper Animation Deploy Limit
        * 6        Core Animation Toggle
        * 7        Core Animation Deploy Limit
        * 8        Lower Animation Toggle
        * 9        Lower Animation Deploy Limit
        * 10       Mount Animation Toggle
        * 11       Mount Animation Deploy Limit
        * 12+++    Stock RCS, gimbal, ReactionWheel toggles, resources (lots more lines)
        **/

        #region REGION - Standard Part Config Fields

        [KSPField]
        public float diameterIncrement = 0.625f;

        [KSPField]
        public float minDiameter = 0.625f;

        [KSPField]
        public float maxDiameter = 10f;

        [KSPField]
        public bool useAdapterVolume = true;

        [KSPField]
        public bool useAdapterMass = true;

        [KSPField]
        public bool useAdapterCost = true;

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
        public string disabledRCSModelName = "RCS-None";

        [KSPField]
        public string disabledSolarName = "Solar-None";

        [KSPField]
        public string engineThrustModule = "NONE";

        [KSPField]
        public string engineTransformModule = "NONE";

        [KSPField]
        public string engineGimbalModule = "NONE";

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
        /// Solar panel display status field.  Updated by solar functions module with occlusion and/or EC generation stats.
        /// </summary>
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "SolarState:")]
        public string solarPanelStatus = string.Empty;

        /// <summary>
        /// The current user selected diamater of the part.  Drives the scaling and positioning of everything else in the model.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Diameter"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentDiameter = 2.5f;

        #region REGION - Module persistent data fields

        //------------------------------------------MODEL SELECTION SET PERSISTENCE-----------------------------------------------//

        //non-persistent value; initialized to whatever the currently selected core model definition is at time of loading
        //allows for variant names to be updated in the part-config without breaking everything....
        [KSPField(guiName = "Variant"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentVariant = "Default";

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

        [KSPField(isPersistant = true, guiName = "Solar Layout"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentSolarLayout = "default";

        [KSPField(isPersistant = true, guiName = "Solar Parent"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentSolarParent = "CORE";

        [KSPField(isPersistant = true, guiName = "Upper RCS"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperRCS = "MUS-RCS1";

        [KSPField(isPersistant = true, guiName = "Upper RCS Layout"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperRCSLayout = "default";

        [KSPField(isPersistant = true, guiName = "Upper RCS Parent"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentUpperRCSParent = "CORE";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "RCS V.Offset1"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentUpperRCSOffset = 0f;

        [KSPField(isPersistant = true, guiName = "Lower RCS"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerRCS = "MUS-RCS1";

        [KSPField(isPersistant = true, guiName = "Lower RCS Layout"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerRCSLayout = "default";

        [KSPField(isPersistant = true, guiName = "Lower RCS Parent"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentLowerRCSParent = "CORE";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "RCS V.Offset2"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentLowerRCSOffset = 0f;

        //------------------------------------------TEXTURE SET PERSISTENCE-----------------------------------------------//

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

        //standard work-around for lack of config-node data being passed consistently and lack of support for mod-added serializable classes
        [Persistent]
        public string configNodeData = string.Empty;

        #endregion REGION - Standard Part Config Fields

        #region REGION - Private working vars

        /// <summary>
        /// Has initialization been run?  Set to true the first time init methods are run (OnLoad/OnStart), and ensures that init is only run a single time.
        /// </summary>
        private bool initialized = false;

        /// <summary>
        /// Cache of the base/config thrust for the RCS module(s)
        /// TODO -- add differentiation for upper/lower rcs base thrusts
        /// </summary>
        private float rcsThrust = -1;

        /// <summary>
        /// The adjusted modified mass for this part.
        /// </summary>
        private float modifiedMass = 0;

        /// <summary>
        /// The adjusted modified cost for this part.
        /// </summary>
        private float modifiedCost = 0;

        /// <summary>
        /// The list of attach node names that the 'nose' module is responsible for.
        /// </summary>
        private string[] topNodeNames;

        /// <summary>
        /// The list of attach node names that the 'mount' module is responsible for
        /// </summary>
        private string[] bottomNodeNames;

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
        /// ref to the ModularRCS module that updates fuel type and thrust for RCS
        /// </summary>
        private SSTURCSFuelSelection rcsFuelControl;

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
        public void noseToggleAction(KSPActionParam param) { noseModule.animationModule.onToggleAction(param); }

        [KSPAction]
        public void topToggleAction(KSPActionParam param) { upperModule.animationModule.onToggleAction(param); }

        [KSPAction]
        public void coreToggleAction(KSPActionParam param) { coreModule.animationModule.onToggleAction(param); }

        [KSPAction]
        public void bottomToggleAction(KSPActionParam param) { lowerModule.animationModule.onToggleAction(param); }

        [KSPAction]
        public void mountToggleAction(KSPActionParam param) { mountModule.animationModule.onToggleAction(param); }

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
                updateResourceVolume();
                updateFairing(false);
            }
            initializedDefaults = true;
            updateRCSModule();
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

        /// <summary>
        /// Initialization method.  Sets up model modules, loads their configs from the input config node.  Does all initial linking of part-modules.<para/>
        /// Does NOT set up their UI interaction -- that is all handled during OnStart()
        /// </summary>
        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;

            //attach node names, parsed from CSVs in config into standard arrays
            topNodeNames = SSTUUtils.parseCSV(topManagedNodes);
            bottomNodeNames = SSTUUtils.parseCSV(bottomManagedNodes);

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
                MonoBehaviour.print("Loading models for variant: " + variantName);
                coreDefs = SSTUModelData.getModelDefinitionLayouts(coreDefNodes[i].GetStringValues("model"));
                int l2 = coreDefs.Length;
                for (int k = 0; k < l2; k++)
                {
                    //coreDefList.AddUnique(coreDefs[l2]);
                    MonoBehaviour.print("Loading model: " + coreDefs[k]);
                }
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
            MonoBehaviour.print("noses: " + noseDefs.Length);
            MonoBehaviour.print("uppers: " + upperDefs.Length);
            MonoBehaviour.print("cores: " + coreDefs.Length);
            MonoBehaviour.print("lowers: " + lowerDefs.Length);
            MonoBehaviour.print("mounts: " + mountDefs.Length);
            MonoBehaviour.print("solars: " + solarDefs.Length);
            MonoBehaviour.print("rcsUp: " + rcsUpDefs.Length);
            MonoBehaviour.print("rcsdn: " + rcsDnDefs.Length);

            noseModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-NOSE"), ModelOrientation.TOP, nameof(currentNose), null, nameof(currentNoseTexture), nameof(noseModulePersistentData), nameof(noseAnimationPersistentData), nameof(noseAnimationDeployLimit), nameof(noseDeployEvent), nameof(noseRetractEvent));
            noseModule.name = "ModularPart-Nose";
            noseModule.getSymmetryModule = m => m.noseModule;
            noseModule.getValidOptions = () => upperModule.getValidUpperModels(noseDefs, noseModule.orientation);

            upperModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-UPPER"), ModelOrientation.TOP, nameof(currentUpper), null, nameof(currentUpperTexture), nameof(upperModulePersistentData), nameof(upperAnimationPersistentData), nameof(upperAnimationDeployLimit), nameof(upperDeployEvent), nameof(upperRetractEvent));
            upperModule.name = "ModularPart-Upper";
            upperModule.getSymmetryModule = m => m.upperModule;
            upperModule.getValidOptions = () => coreModule.getValidUpperModels(upperDefs, upperModule.orientation);

            coreModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-CORE"), ModelOrientation.CENTRAL, nameof(currentCore), null, nameof(currentCoreTexture), nameof(coreModulePersistentData), nameof(coreAnimationPersistentData), nameof(coreAnimationDeployLimit), nameof(coreDeployEvent), nameof(coreRetractEvent));
            coreModule.name = "ModularPart-Core";
            coreModule.getSymmetryModule = m => m.coreModule;
            coreModule.getValidOptions = () => getVariantSet(currentVariant).definitions;

            lowerModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-LOWER"), ModelOrientation.BOTTOM, nameof(currentLower), null, nameof(currentLowerTexture), nameof(lowerModulePersistentData), nameof(lowerAnimationPersistentData), nameof(lowerAnimationDeployLimit), nameof(lowerDeployEvent), nameof(lowerRetractEvent));
            lowerModule.name = "ModularPart-Lower";
            lowerModule.getSymmetryModule = m => m.lowerModule;
            lowerModule.getValidOptions = () => coreModule.getValidLowerModels(lowerDefs, lowerModule.orientation);

            mountModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-MOUNT"), ModelOrientation.BOTTOM, nameof(currentMount), null, nameof(currentMountTexture), nameof(mountModulePersistentData), nameof(mountAnimationPersistentData), nameof(mountAnimationDeployLimit), nameof(mountDeployEvent), nameof(mountRetractEvent));
            mountModule.name = "ModularPart-Mount";
            mountModule.getSymmetryModule = m => m.mountModule;
            mountModule.getValidOptions = () => lowerModule.getValidLowerModels(mountDefs, mountModule.orientation);

            solarModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-SOLAR"), ModelOrientation.CENTRAL, nameof(currentSolar), nameof(currentSolarLayout), nameof(currentSolarTexture), nameof(solarModulePersistentData), nameof(solarAnimationPersistentData), null, nameof(solarDeployEvent), nameof(solarRetractEvent));
            solarModule.name = "ModularPart-Solar";
            solarModule.getSymmetryModule = m => m.solarModule;
            solarModule.getValidOptions = () => solarDefs;
            solarModule.getLayoutPositionScalar = () => coreModule.moduleDiameter * 0.5f;
            solarModule.getLayoutScaleScalar = () => coreModule.moduleHorizontalScale;

            upperRcsModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-UPPERRCS"), ModelOrientation.CENTRAL, nameof(currentUpperRCS), nameof(currentUpperRCSLayout), nameof(currentUpperRCSTexture), nameof(upperRCSModulePersistentData), null, null, null, null);
            upperRcsModule.name = "ModularPart-UpperRCS";
            upperRcsModule.getSymmetryModule = m => m.upperRcsModule;
            upperRcsModule.getValidOptions = () => rcsUpDefs;
            upperRcsModule.getLayoutPositionScalar = () => coreModule.moduleDiameter * 0.5f;
            upperRcsModule.getLayoutScaleScalar = () => coreModule.moduleHorizontalScale;

            lowerRcsModule = new ModelModule<SSTUModularPart>(part, this, getRootTransform("ModularPart-LOWERRCS"), ModelOrientation.CENTRAL, nameof(currentLowerRCS), nameof(currentLowerRCSLayout), nameof(currentLowerRCSTexture), nameof(lowerRCSModulePersistentData), null, null, null, null);
            lowerRcsModule.name = "ModularPart-LowerRCS";
            lowerRcsModule.getSymmetryModule = m => m.lowerRcsModule;
            lowerRcsModule.getValidOptions = () => rcsDnDefs;
            lowerRcsModule.getLayoutPositionScalar = () => coreModule.moduleDiameter * 0.5f;
            lowerRcsModule.getLayoutScaleScalar = () => coreModule.moduleHorizontalScale;

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
            upperRcsModule.renameRCSThrustTransforms(upperRCSThrustTransform);
            lowerRcsModule.renameRCSThrustTransforms(lowerRCSThrustTransform);

            //TODO handle engine transform and gimbal transform initial renaming

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
        }
        
        //TODO - controls for rcs vertical position functions
        //TODO - controls for layout change functions
        private void initializeUI()
        {

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
                });
            };

            Action<SSTUModularPart> modelChangedAction = (m) =>
            {
                m.validateModules();
                m.updateModulePositions();
                m.updateMassAndCost();
                m.updateAttachNodes(true);
                m.updateDragCubes();
                m.updateResourceVolume();
                m.updateFairing(true);
                m.updateAvailableVariants();
                SSTUModInterop.onPartGeometryUpdate(m.part, true);
            };

            Fields[nameof(currentDiameter)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
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

            Fields[nameof(currentSolar)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                solarModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    modelChangedAction(m);
                    m.solarFunctionsModule.setupSolarPanelData(m.solarModule.getSolarData(), m.solarModule.moduleModelTransforms);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

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

            Fields[nameof(currentUpperRCSOffset)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
                    modelChangedAction(m);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentLowerRCSOffset)].uiControlEditor.onFieldChanged = (a, b) =>
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
            Fields[nameof(currentSolarTexture)].uiControlEditor.onFieldChanged = solarModule.textureSetSelected;
            Fields[nameof(currentUpperRCSTexture)].uiControlEditor.onFieldChanged = upperRcsModule.textureSetSelected;
            Fields[nameof(currentLowerRCSTexture)].uiControlEditor.onFieldChanged = lowerRcsModule.textureSetSelected;

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
            if (!coreModule.isValidUpper(upperModule))
            {
                ModelDefinition def = coreModule.findFirstValidUpper(upperModule);
                if (def == null) { }//TODO throw error...
                upperModule.modelSelected(def.name);
            }

            //validate nose model regardless of if upper changed or not
            if (!upperModule.isValidUpper(noseModule))
            {
                ModelDefinition def = upperModule.findFirstValidUpper(noseModule);
                if (def == null) { }//TODO throw error...
                noseModule.modelSelected(def.name);
            }

            //validate lower model
            if (!coreModule.isValidLower(lowerModule))
            {
                ModelDefinition def = coreModule.findFirstValidLower(lowerModule);
                if (def == null) { }//TODO throw error...
                lowerModule.modelSelected(def.name);
            }

            //validate mount model
            if (!lowerModule.isValidLower(mountModule))
            {
                ModelDefinition def = lowerModule.findFirstValidLower(mountModule);
                if (def == null) { }//TODO throw error...
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
        
        //TODO -- update solar/RCS positions
        private void updateModulePositions()
        {
            //scales for modules depend on the module above/below them
            //first set the scale for the core module -- this depends directly on the UI specified 'diameter' value.
            coreModule.setScaleForDiameter(currentDiameter);

            //next, set upper, and then nose scale values
            upperModule.setDiameterFromBelow(coreModule.moduleUpperDiameter);
            noseModule.setDiameterFromBelow(upperModule.moduleUpperDiameter);

            //finally, set lower and mount scale values
            lowerModule.setDiameterFromAbove(coreModule.moduleLowerDiameter);
            mountModule.setDiameterFromAbove(lowerModule.moduleLowerDiameter);

            //total height of the part is determined by the sum of the heights of the modules at their current scale
            float totalHeight = noseModule.moduleHeight;
            totalHeight += upperModule.moduleHeight;
            totalHeight += coreModule.moduleHeight;
            totalHeight += lowerModule.moduleHeight;
            totalHeight += mountModule.moduleHeight;

            //position of each module is set such that the vertical center of the models is at part origin/COM
            float pos = totalHeight * 0.5f;
            pos -= noseModule.moduleHeight;
            noseModule.setPosition(pos);
            pos -= upperModule.moduleHeight;
            upperModule.setPosition(pos);
            pos -= coreModule.moduleHeight * 0.5f;
            coreModule.setPosition(pos);
            pos -= coreModule.moduleHeight * 0.5f;
            lowerModule.setPosition(pos);
            pos -= lowerModule.moduleHeight;
            mountModule.setPosition(pos);

            //update actual model positions and scales
            noseModule.updateModelMeshes();
            upperModule.updateModelMeshes();
            coreModule.updateModelMeshes();
            lowerModule.updateModelMeshes();
            mountModule.updateModelMeshes();
            
            //scale and position of RCS and solar models handled a bit differently
            float coreScale = coreModule.moduleHorizontalScale;
            solarModule.setScale(1);
            upperRcsModule.setScale(1);
            lowerRcsModule.setScale(1);

            //TODO -- these positions need to depend on what the current 'parent' module is for the add-on
            // as well as, at least for RCS, the currently configured 'offset'
            // -- need an easy way to track what the parent module is for each of these
            solarModule.setPosition(getModuleByName(currentSolarParent).modulePosition);
            upperRcsModule.setPosition(getModuleByName(currentUpperRCSParent).modulePosition);
            lowerRcsModule.setPosition(getModuleByName(currentLowerRCSParent).modulePosition);

            solarModule.updateModelMeshes();
            upperRcsModule.updateModelMeshes();
            lowerRcsModule.updateModelMeshes();
        }

        /// <summary>
        /// Update VolumeContainer resource volume from the currently configured model selections.<para/>
        /// Optionally includes volume from adapters if specified in config.
        /// </summary>
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

        /// <summary>
        /// Update the cached modifiedMass and modifiedCost field values.  Used with stock cost/mass modifier interface.<para/>
        /// Optionally includes adapter mass/cost if enabled in config.
        /// </summary>
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
        /// <summary>
        /// Update the ModuleRCSXX with the current stats for the current configuration (thrust, ISP, fuel type)
        /// </summary>
        private void updateRCSModule()
        {
            //initialize the external RCS fuel-type control module
            //TODO -- need more modules for upper/lower differentiation?
            rcsFuelControl = part.GetComponent<SSTURCSFuelSelection>();
            if (rcsFuelControl != null)
            {
                rcsFuelControl.Start();
            }

            //initialize the thrust value from the RCS module
            if (rcsThrust < 0)
            {
                ModuleRCS rcs = part.GetComponent<ModuleRCS>();
                if (rcs != null)
                {
                    rcsThrust = rcs.thrusterPower;
                }
            }

            //if we have a valid thrust output rating, update the stock RCS module with that thrust value.
            if (rcsThrust >= 0)
            {
                float thrust = rcsThrust * Mathf.Pow(coreModule.moduleHorizontalScale, 2);
                MonoBehaviour.print("TODO -- Adjust RCS thrust/enabled/disabled status from SSTUModularPart");
                //SSTUModularRCS.updateRCSModules(part, !upperRcsModule.model.dummyModel, thrust, true, true, true, true, true, true);
            }
        }

        //TODO
        /// <summary>
        /// Update the ModuleEnginesXX with the current stats for the current configuration.
        /// </summary>
        private void updateEngineModule()
        {

        }

        //TODO
        /// <summary>
        /// Update the ModuleGimbal with the current stats for the current configuration.
        /// </summary>
        private void updateGimbalModule()
        {

        }

        //TODO -- surface attach handling -- both internal and external.
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

        /// <summary>
        /// Update the current fairing modules (top and bottom) for the current model-module configuration (diameters, positions).
        /// </summary>
        /// <param name="userInput"></param>
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
        /// Return the Y position(model space) of the bottom edge of the upper fairing.  Should first check if the fairing is enabled before using this value.
        /// </summary>
        /// <returns></returns>
        private float getTopFairingBottomY()
        {
            return getPartTopY() + noseModule.moduleFairingOffset;
        }

        /// <summary>
        /// Return the Y position(model spae) of the upper edge of the lower fairing.  Should first check if the fairing is enabled before using this value.
        /// </summary>
        /// <returns></returns>
        private float getBottomFairingTopY()
        {
            return getPartTopY() - getTotalHeight() + mountModule.moduleFairingOffset; ;
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
            Fields[nameof(currentLowerRCSParent)].guiActiveEditor = lowerRcsModule.layout.positions.Length >= 1;
            Fields[nameof(currentUpperRCSParent)].guiActiveEditor = upperRcsModule.layout.positions.Length >= 1;
            Fields[nameof(currentSolarParent)].guiActiveEditor = solarModule.layout.positions.Length >= 1;
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
                default:
                    return null;
            }
        }

        #endregion ENDREGION - Custom Update Methods

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
