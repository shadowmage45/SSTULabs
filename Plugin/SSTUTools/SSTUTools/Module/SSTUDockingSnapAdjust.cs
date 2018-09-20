using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUDockingSnapAdjust : PartModule
    {

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = true, guiName = "Docking Port Snap"),
         UI_Toggle(enabledText = "Enabled", disabledText = "Disabled", suppressEditorShipModified = true)]
        public bool enableSnap = false;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = true, guiName = "Snap Increment", guiUnits = "deg"),
         UI_FloatEdit(suppressEditorShipModified = true, sigFigs = 3, minValue = 0, maxValue = 360, incrementLarge = 90, incrementSmall = 15, incrementSlide = 1)]
        public float snapAngle = 90f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = true, guiName = "Snap Tolerance", guiUnits ="deg"),
         UI_FloatEdit(suppressEditorShipModified = true, sigFigs = 4, minValue = 0, maxValue = 360, incrementLarge = 90, incrementSmall = 15, incrementSlide = 0.5f)]
        public float tolerance = 0.5f;

        private void onSnapToggled(BaseField field, System.Object obj)
        {
            ModuleDockingNode mdn = part.GetComponent<ModuleDockingNode>();
            mdn.snapOffset = snapAngle;
            mdn.snapRotation = enableSnap;
            float minDot = 1 - (tolerance / 360f);//gives a 0 <-> 1 range
            minDot *= 2;//convert to a 0 <-> 2 range
            minDot -= 1;//offset into -1 <-> 1 range
            mdn.captureMinRollDot = enableSnap? minDot : -3.402e38f;

            BaseField snapAngleField = Fields[nameof(snapAngle)];
            snapAngleField.guiActive = snapAngleField.guiActiveEditor = enableSnap;
            BaseField toleranceField = Fields[nameof(tolerance)];
            toleranceField.guiActive = toleranceField.guiActiveEditor = enableSnap;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            BaseField snapAngle = Fields[nameof(snapAngle)];
            snapAngle.uiControlEditor.onFieldChanged = onSnapToggled;
            snapAngle.uiControlFlight.onFieldChanged = onSnapToggled;

            BaseField snapToggle = Fields[nameof(enableSnap)];
            snapToggle.uiControlEditor.onFieldChanged = onSnapToggled;
            snapToggle.uiControlFlight.onFieldChanged = onSnapToggled;
        }

        public void Start()
        {
            //update UI and set docking port to currently configured state for this module (fields are non-persistent in docking port)
            onSnapToggled(null, null);
        }

    }
}
