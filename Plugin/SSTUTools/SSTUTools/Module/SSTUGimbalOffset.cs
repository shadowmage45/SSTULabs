using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{

    public class SSTUGimbalOffset : PartModule
    {

        [KSPField]
        public float gimbalXRangeEditor;

        [KSPField]
        public float gimbalZRangeEditor;

        /// <summary>
        /// Gimbal adjustment range on X-axis while in flight.
        /// </summary>
        [KSPField]
        public float gimbalXRange;

        /// <summary>
        /// Gimbal adjustment range on Z-axis while in flight.
        /// </summary>
        [KSPField]
        public float gimbalZRange;

        [KSPField(isPersistant = true)]
        public float gimbalOffsetX = 0f;

        [KSPField(isPersistant = true)]
        public float gimbalOffsetZ = 0f;

        private Quaternion[] defaultOrientations;
        private Transform[] gimbalTransforms;

        private bool initialized = false;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        public void initialize()
        {
            if (initialized) { return; }
            initialized = true;

            Fields[nameof(gimbalOffsetX)].uiControlEditor.onFieldChanged = (a, b) => 
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.gimbalOffsetX = this.gimbalOffsetX; }
                });
            };

            Fields[nameof(gimbalOffsetZ)].uiControlEditor.onFieldChanged = (a, b) =>
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.gimbalOffsetZ = this.gimbalOffsetZ; }
                });
            };

            ModuleGimbal gimbal = part.GetComponent<ModuleGimbal>();
            if (gimbal == null) { SSTULog.error("No gimbal module located; cannot manipulate gimbal range or offset"); }

            gimbalTransforms = gimbal.gimbalTransforms.ToArray();
            defaultOrientations = gimbal.initRots.ToArray();
        }

        public void reInitialize()
        {

        }

        /// <summary>
        /// Resets the gimbal to its default orientation, and then applies newRotation to it as a direct rotation around the input world axis.
        /// After this method is used, the ModuleGimbal's internal 'default orientation' values need to be updated appropriately.
        /// </summary>
        /// <param name="partGimbalTransform"></param>
        /// <param name="newRotation"></param>
        public void updateGimbalRotation(Vector3 worldAxis, float newRotation)
        {
            int len = gimbalTransforms.Length;
            for (int i = 0; i < len; i++)
            {
                gimbalTransforms[i].localRotation = defaultOrientations[i];
                gimbalTransforms[i].Rotate(worldAxis, -newRotation, Space.World);
            }
        }

    }

}
