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
            initialize();
        }

        private void initialize()
        {
            if (initialized) { return; }
            if (defaultOrientations != null) { return; }//already initialized? should not have
            gimbalModule = part.GetComponent<ModuleGimbal>();
            if (gimbalModule == null)//no module to update, may be due to ordering in part config
            {
                SSTULog.debug("Skipping gimbal offset external init - no gimbal module");
                return;
            }
            if (gimbalModule.gimbalTransforms == null || gimbalModule.gimbalTransforms.Count <= 0)//gimbal not loaded
            {
                SSTULog.debug("Skipping gimbal offset external init - no gimbal transforms");
                return;
            }
            else if (gimbalModule.initRots == null || gimbalModule.initRots.Count <= 0)//gimbal invalid?
            {
                SSTULog.debug("Skipping gimbal offset external init - no gimbal initRots");
                return;
            }
            //gimbal is present, and appears to be valid, set to initialized and get default orientations array
            initialized = true;
            defaultOrientations = gimbalModule.initRots.ToArray();
            SSTULog.debug("Initialized default gimbal values: "+defaultOrientations.Length);
            //update gimbal rotations for the current values
            SSTULog.debug("Updating for current/persistent values: "+gimbalOffsetX+","+gimbalOffsetZ+"  and ranges: "+gimbalXRange+","+gimbalZRange);
            updateGimbalOffset();
            
        }

        private void updateGimbalOffset()
        {
            SSTULog.debug("Updating gimbal offsets");
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
                SSTULog.debug("Updating tr: " + i + " : " + tr.localRotation);
                rot = defaultOrientations[i] * Quaternion.AngleAxis(gimbalOffsetX * gimbalXRange, Vector3.right);
                tr.localRotation = rot * Quaternion.AngleAxis(gimbalOffsetZ * gimbalZRange, Vector3.up);//gimbals use Z+ = 'down', so we actually rotate around Y+ as 'forward'
                gimbalModule.initRots[i] = tr.localRotation;
                SSTULog.debug("New rot: " + tr.localRotation);
            }
        }

    }

}
