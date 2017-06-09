using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSTUTools.WIPModule
{
    public class SSTULinearMultiDock : PartModule
    {
        [KSPField]
        public string animationID = string.Empty;

        [KSPField]
        public string portName = "Linear Dock";

        [KSPField]
        public float dock1Snap = 0f;

        [KSPField]
        public float dock2Snap = 0f;

        [KSPField]
        public int dock1Index = 0;

        [KSPField]
        public int dock2Index = 1;

        [KSPField(guiName = "Magnetics", guiActive = true, guiActiveEditor = true, isPersistant = true),
          UI_Toggle(enabledText = "Enabled", disabledText = "Disabled", suppressEditorShipModified = true)]
        public bool magneticsEnabled = true;

        private ModuleAnimateGeneric animation;
        private ModuleDockingNode dock1;
        private ModuleDockingNode dock2;

        public void undockEvent()
        {

        }

        public void decoupleEvent()
        {

        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Fields[nameof(magneticsEnabled)].uiControlFlight.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                this.actionWithSymmetry(m => 
                {
                    m.magneticsEnabled = magneticsEnabled;
                    animation.SetScalar(m.magneticsEnabled ? 1 : 0);
                });
            };
        }

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);
            animation = part.Modules.GetScalarModule(animationID) as ModuleAnimateGeneric;
            if (animation != null)
            {

            }
            SSTUMultiDockingPort.updateDockingModuleFieldNames(dock1, portName + "-1");
            SSTUMultiDockingPort.updateDockingModuleFieldNames(dock2, portName + "-2");
        }

    }
}
