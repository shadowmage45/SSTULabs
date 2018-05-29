using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{

    public class SSTUGimbalOffset : PartModule
    {

        public float gimbalZRange;

        public float gimbalXRange;

        [KSPField(isPersistant = true)]
        public float gimbalOffsetX = 0f;

        [KSPField(isPersistant = true)]
        public float gimbalOffsetZ = 0f;

        private Quaternion[] defaultOrientations;

        private bool initialized = false;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
        }

        public void initialize()
        {
            if (initialized) { return; }
            initialized = true;

        }

    }

}
