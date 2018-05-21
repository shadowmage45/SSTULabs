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
        public string presetCurveName = "linear";

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

        [KSPEvent(guiActive = false, guiActiveEditor = true)]
        public void openThrustCurveGUI()
        {
            ThrustCurveEditorGUI.openGUI(this, currentCurve);
        }
        
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
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
            engines[engineModuleIndex].thrustCurve = currentCurve;
        }
    }
}
