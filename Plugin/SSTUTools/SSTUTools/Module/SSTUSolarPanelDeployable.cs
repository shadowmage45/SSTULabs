using System;
using System.Collections.Generic;
using UnityEngine;
namespace SSTUTools
{

    //Multi-panel solar panel module, each with own suncatcher and pivot and occlusion checks
    //Animation code based from stock, Near-Future, and Firespitter code
    //Solar panel code based from Near-Future code originally, but has been vastly changed since the original implementation
    //solar pivots rotate around localY, to make localZ face the sun
    //e.g. y+ should point towards origin, z+ should point towards the panel solar input direction
    public class SSTUSolarPanelDeployable : PartModule, IContractObjectiveModule
    {

        [KSPField]
        public String resourceName = "ElectricCharge";
        
        [KSPField]
        public bool canDeployShrouded = false;
        
        [KSPField]
        public FloatCurve temperatureEfficCurve;

        [KSPField]
        public int animationLayer = 1;

        //[KSPField]
        //public bool canLockPanels = true;

        //[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Panel Rotation"),
        // UI_Toggle(suppressEditorShipModified = true, enabledText = "Locked", disabledText = "Tracking")]
        //public bool userLock = false;

        //[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Panel Rotation"),
        // UI_FloatEdit(suppressEditorShipModified = true, minValue = -180f, maxValue = 180f, incrementLarge =90f, incrementSmall = 45, incrementSlide = 1f)]
        //public float userRotation = 0f;

        //BELOW HERE ARE NON-CONFIG EDITABLE FIELDS

        //used purely to persist rough estimate of animation state; if it is retracting/extending when reloaded, it will default to the start of that animation transition
        //defaults to retracted state for any new/uninitialized parts

        //Status displayed for panel state, includes animation state and energy state;  Using in place of the three-line output from stock panels
        [KSPField(guiName = "S.P.", guiActive = true)]
        public String guiStatus = String.Empty;

        /// <summary>
        /// Public field that can be used by external mods/etc to query for the last updated power output.<para/>
        /// This value is given in EC/s, and reflects the total calculated output from the last update tick.
        /// </summary>
        [KSPField]
        public float ECOutput = 0f;

        /// <summary>
        /// Animation persistent data
        /// </summary>
        [KSPField(isPersistant = true)]
        public String persistentState = string.Empty;

        /// <summary>
        /// Solar panel rotation persistent data
        /// </summary>
        [KSPField(isPersistant = true)]
        public string solarPersistentData = string.Empty;

        /// <summary>
        /// Nominal output of the solar panels; 100% thermal efficiency at Kerbin orbit distance from sun (1 KAU).  This value is set when
        /// the part is initialied and updated any time solar panel layout is changed.  Can be queried in the editor or flight scene to
        /// determine the current -nominal- EC output of solar panels.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float nominalSolarOutput = 0f;

        [Persistent]
        public string configNodeData = string.Empty;

        private AnimationModule animationModule;
        private SolarModule solarModule;

        private bool initialized = false;

        //KSP Action Group 'Extend Panels' action, will only trigger when panels are actually retracted/ing
        [KSPAction("Extend Solar Panels")]
        public void extendAction(KSPActionParam param)
        {
            extendEvent();
        }

        //KSP Action Group 'Retract Panels' action, will only trigger when panels are actually extended/ing
        [KSPAction("Retract Solar Panels")]
        public void retractAction(KSPActionParam param)
        {
            retractEvent();
        }

        //KSP Action Group 'Toggle Panels' action, will operate regardless of current panel status (except broken)
        [KSPAction("Toggle Solar Panels")]
        public void toggleAction(KSPActionParam param)
        {
            animationModule.onToggleAction(param);
        }

        [KSPEvent(name = "extendEvent", guiName = "Extend Solar Panels", guiActiveUnfocused = true, externalToEVAOnly = true, guiActive = true, unfocusedRange = 4f, guiActiveEditor = true)]
        public void extendEvent()
        {
            animationModule.onDeployEvent();
        }

        [KSPEvent(name = "retractEvent", guiName = "Retract Solar Panels", guiActiveUnfocused = true, externalToEVAOnly = true, guiActive = true, unfocusedRange = 4f, guiActiveEditor = true)]
        public void retractEvent()
        {
            solarModule.onRetractEvent();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }
        
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            //animation persistence is updated on state change
            //but rotations are updated every frame, so it is not feasible to update string-based persistence data (without excessive garbage generation)
            if (solarModule != null)
            {
                solarModule.updateSolarPersistence();
                node.SetValue(nameof(solarPersistentData), solarPersistentData, true);
            }
        }

        public override string GetInfo()
        {
            if (moduleIsEnabled)
            {
                //TODO
                MonoBehaviour.print("TODO -- SSTUSolarPanelDeployable - GetInfo()");
            }
            return base.GetInfo();
        }

        public void FixedUpdate()
        {
            ECOutput = 0f;
            if (!moduleIsEnabled) { return; }
            solarModule.FixedUpdate();
            ECOutput = solarModule.totalOutput;
        }

        public void Update()
        {
            if (!moduleIsEnabled) { return; }
            animationModule.Update();
            solarModule.Update();
        }

        /// <summary>
        /// To be called by external modules when the module has been enabled/disabled.
        /// Reloads config and/or disables GUI depending on the current moduleIsEnabled status.
        /// </summary>
        public void reInitialize()
        {
            initialized = false;
            initialize();
        }

        private void initialize()
        {
            if (!moduleIsEnabled)
            {
                //TODO -- UI disabling
                return;
            }
            if (initialized) { return; }
            initialized = true;
            AnimationData animData = null;
            ModelSolarData msd = null;
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            if (node.HasValue("modelDefinition"))
            {
                ModelDefinition def = SSTUModelData.getModelDefinition(node.GetStringValue("modelDefinition"));
                if (def == null)
                {
                    MonoBehaviour.print("Could not locate model definition: " + node.GetStringValue("modelDefinition") + " for solar data");
                }
                else
                {
                    animData = def.animationData;
                    msd = def.solarModuleData;
                }
            }
            //allow local override of animation data
            if (node.HasNode("ANIMATIONDATA"))
            {
                animData = new AnimationData(node.GetNode("ANIMATIONDATA"));
            }
            if (node.HasNode("SOLARDATA"))
            {
                msd = new ModelSolarData(node.GetNode("SOLARDATA"));
            }

            animationModule = new AnimationModule(part, this, nameof(persistentState), null, nameof(extendEvent), nameof(retractEvent));
            animationModule.getSymmetryModule = m => ((SSTUSolarPanelDeployable)m).animationModule;
            animationModule.setupAnimations(animData, part.transform.FindRecursive("model"), animationLayer);

            solarModule = new SolarModule(part, this, animationModule, Fields[nameof(solarPersistentData)], Fields[nameof(guiStatus)]);
            solarModule.getSymmetryModule = m => ((SSTUSolarPanelDeployable)m).solarModule;
            solarModule.setupSolarPanelData(new ModelSolarData[] { msd }, new Transform[] { part.transform.FindRecursive("model") });
            nominalSolarOutput = solarModule.standardPotentialOutput;
        }
        
        //TODO
        private void updateGuiData()
        {
            //Fields[nameof(userLock)].guiActive = Fields[nameof(userLock)].guiActiveEditor = canLockPanels;
            //Fields[nameof(userRotation)].guiActive = Fields[nameof(userRotation)].guiActiveEditor = userLock;
        }

        public string GetContractObjectiveType()
        {
            return "Generator";
        }

        public bool CheckContractObjectiveValidity()
        {
            return moduleIsEnabled;
        }

    }

}

