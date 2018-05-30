using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSTUTools
{

    /// <summary>
    /// Part-module responsible for managing of custom thrust curves for SRBs.<para/>
    /// Should be placed in the part -after- any ModuleEngines that are to be manipulated.
    /// </summary>
    public class SSTUEngineThrustCurveGUI : PartModule
    {

        /// <summary>
        /// The index of the engine module this module manipulates.  this index is specified as the index into the array of modules as returned by part.getcomponents()
        /// </summary>
        [KSPField]
        public int engineModuleIndex = 0;

        /// <summary>
        /// Current curve preset name.  Will be either the name of a preset curve or 'custom' if a user-defined curve is in use.
        /// </summary>
        [KSPField(isPersistant =true)]
        public string presetCurveName = "Constant";

        /// <summary>
        /// True/false if using a preset or custom curve.
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool usePresetCurve = true;

        /// <summary>
        /// Serialized text version of the current user-custom curve.  Only populated if a preset curve is not in use.
        /// </summary>
        [KSPField(isPersistant = true)]
        private string customCurveData = string.Empty;

        /// <summary>
        /// The actual curve data in use.  This will be a copy of a preset curve, or the run-time representation of a user-defined curve.
        /// </summary>
        private FloatCurve currentCurve;

        /// <summary>
        /// Tracks if init has been done on this part
        /// </summary>
        private bool initialized = false;

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Open Thrust Curve Editor")]
        public void openThrustCurveGUI()
        {
            ThrustCurveEditorGUI.openGUI(this, presetCurveName, currentCurve);
        }
        
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        public void Start()
        {
            updateEngineCurve();
        }

        /// <summary>
        /// Initializes this module.  Loads custom curve data from persistence if necessary, and updates the engine module with the currently loaded curve.
        /// </summary>
        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            if (!string.IsNullOrEmpty(customCurveData))
            {
                //load currentCurve from customCurveData
                currentCurve = new FloatCurve();
                currentCurve.loadSingleLine(customCurveData);
            }
            else if (usePresetCurve && !string.IsNullOrEmpty(presetCurveName))
            {
                //load currentCurve from PresetCurve data
                loadPresetCurve(presetCurveName);
                customCurveData = "";
            }
            else
            {
                //uninitialized module; no custom or preset curve specified, and at least one of the two is mandatory
                //init to 'linear' curve type
                usePresetCurve = true;
                presetCurveName = "Constant";
                customCurveData = "";
                //load currentCurve from PresetCurve data
                loadPresetCurve(presetCurveName);
            }
            updateEngineCurve();
        }

        public void thrustCurveGuiClosed(string preset, FloatCurve curve)
        {
            //update the persistent curve data from
            currentCurve = curve;
            presetCurveName = preset;
            usePresetCurve = !string.IsNullOrEmpty(presetCurveName);
            if (!usePresetCurve)
            {
                customCurveData = currentCurve.ToStringSingleLine();
            }
            SSTULog.debug("Updating engine thrust cuve data.  Use preset: " + usePresetCurve);
            updateEngineCurve();
        }

        /// <summary>
        /// Applies the 'currentCurve' to the engine module as its active thrust curve.
        /// </summary>
        private void updateEngineCurve()
        {
            ModuleEngines[] engines = part.GetComponents<ModuleEngines>();
            if (engineModuleIndex < 0) { return; }//config error
            if (engineModuleIndex >= engines.Length) { return; }//config error
            if (currentCurve == null) { return; }//code error
            SSTULog.debug("Updating ModuleEngine's thrust-curve");
            engines[engineModuleIndex].thrustCurve = currentCurve;
        }

        private void loadPresetCurve(string presetName)
        {
            ConfigNode[] presetNodes = GameDatabase.Instance.GetConfigNodes("SSTU_THRUSTCURVE");
            ThrustCurvePreset preset;
            int len = presetNodes.Length;
            for (int i = 0; i < len; i++)
            {
                if (presetNodes[i].GetStringValue("name") == presetName)
                {
                    preset = new ThrustCurvePreset(presetNodes[i]);
                    currentCurve = preset.curve;
                    break;
                }
            }
        }
    }
}
