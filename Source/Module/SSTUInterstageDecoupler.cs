using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools.Module
{
    class SSTUInterstageDecoupler : PartModule
    {
        [KSPField]
        public int cylinderSides = 24;

        [KSPField]
        public int numberOfPanels = 1;

        public float currentHeight;

        public float currentTopDiameter;

        public float currentBottomDiameter;

        public float currentStraightHeight;

        private FairingContainer fairingBase;

        public int lowerDecouplerModuleIndex = 1;
        public int upperDecouplerModuleIndex = 2;
    }
}
