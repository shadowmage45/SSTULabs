using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{

    public class SSTUGimbalOffset : PartModule
    {

        /// <summary>
        /// Gimbal adjustment range on X-axis while in editor.
        /// </summary>
        [KSPField]
        public float gimbalXRange;

        /// <summary>
        /// Gimbal adjustment range on Z-axis while in editor.
        /// </summary>
        [KSPField]
        public float gimbalZRange;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Gimbal X"),
         UI_FloatRange(suppressEditorShipModified =true, minValue = -1, maxValue = 1, stepIncrement = 0.01f)]
        public float gimbalOffsetX = 0f;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Gimbal Z"),
         UI_FloatRange(suppressEditorShipModified = true, minValue = -1, maxValue = 1, stepIncrement = 0.01f)]
        public float gimbalOffsetZ = 0f;

        private ModuleGimbal gimbalModule;
        //the actual default orientation of the transforms
        private Quaternion[] defaultOrientations;

        private bool initialized = false;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();

            //init field callback methods
            Fields[nameof(gimbalOffsetX)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.gimbalOffsetX = this.gimbalOffsetX; }
                    m.updateGimbalOffset();
                });
            };

            Fields[nameof(gimbalOffsetZ)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.gimbalOffsetZ = this.gimbalOffsetZ; }
                    m.updateGimbalOffset();
                });
            };
        }

        public void Start()
        {
            initialize();
        }

        /// <summary>
        /// Recreates the default orientation array, and re-applies the X and Z rotation offsets.<para/>
        /// This method relies on the ModuleGimbal's transform and rotations arrays, and should only be called after the gimbal has been initialized.<para/>
        /// Assumes that the new gimbal transforms are currently in their default orientation.
        /// </summary>
        public void reInitialize()
        {
            initialized = false;
            defaultOrientations = null;
            initialize();
        }

        private void initialize()
        {
            if (initialized) { return; }
            if (defaultOrientations != null) { return; }//already initialized? should not have
            gimbalModule = part.GetComponent<ModuleGimbal>();
            if (gimbalModule == null)//no module to update, may be due to ordering in part config
            {
                return;
            }
            if (gimbalModule.gimbalTransforms == null || gimbalModule.gimbalTransforms.Count <= 0)//gimbal not loaded
            {
                return;
            }
            else if (gimbalModule.initRots == null || gimbalModule.initRots.Count <= 0)//gimbal invalid?
            {
                return;
            }
            //gimbal is present, and appears to be valid, set to initialized and get default orientations array
            initialized = true;
            defaultOrientations = gimbalModule.initRots.ToArray();
            updateGimbalOffset();
            
        }

        private void updateGimbalOffset()
        {
            int len = gimbalModule.gimbalTransforms.Count();
            Transform tr;
            Quaternion rot;
            for (int i = 0; i < len; i++)
            {
                tr = gimbalModule.gimbalTransforms[i];
                if (tr == null)
                {
                    SSTULog.error("NULL gimbal transform detected in ModuleGimbal's transform list!");
                    continue;
                }
                //use the parts 'right' and 'fwd' vectors for part-local axis rotations
                Vector3 vesselRight = part.transform.right;
                Vector3 vesselFwd = part.transform.forward;
                vesselRight = tr.InverseTransformDirection(vesselRight);
                vesselFwd = tr.InverseTransformDirection(vesselFwd);
                rot = defaultOrientations[i] * Quaternion.AngleAxis(gimbalOffsetX * gimbalXRange, vesselRight);
                tr.localRotation = rot * Quaternion.AngleAxis(gimbalOffsetZ * gimbalZRange, vesselFwd);
                gimbalModule.initRots[i] = tr.localRotation;
            }
        }

    }

}
