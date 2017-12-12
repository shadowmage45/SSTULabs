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

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = true, guiName = "Snap Angle"),
         UI_FloatEdit(suppressEditorShipModified = true, sigFigs = 3, minValue = 0, maxValue = 360, incrementLarge = 90, incrementSmall = 15, incrementSlide = 1)]
        public float snapAngle = 90f;

        private void onSnapToggled(BaseField field, System.Object obj)
        {
            ModuleDockingNode mdn = part.GetComponent<ModuleDockingNode>();
            mdn.snapOffset = snapAngle;
            mdn.snapRotation = enableSnap;
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
            onSnapToggled(null, null);
        }

    }
}
