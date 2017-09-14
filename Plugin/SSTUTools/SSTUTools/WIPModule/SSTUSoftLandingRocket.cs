using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSTUTools.WIPModule
{
    /// <summary>
    /// Retro-rocket module to perform a rocket-asisted powered soft landing.
    /// Needs to calculate the time at which to start the engines in order to land at the specified speed.
    /// </summary>
    public class SSTUSoftLandingRocket : PartModule
    {

        /// <summary>
        /// The height of the parts reference transform above its lowest point (positive value).  This value gets subtracted from the altitude returned by querying the parts transform location.
        /// </summary>
        [KSPField]
        public float groundHeightOffset = 0f;

        /// <summary>
        /// The desired speed at touchdown.
        /// </summary>
        [KSPField]
        public float landingSpeed = 1f;

        /// <summary>
        /// How much deltaV the system is capable of nominally
        /// </summary>
        [KSPField]
        public float deltaV = 0f;

        /// <summary>
        /// The duration of the burn over which the deltaV is produced
        /// </summary>
        [KSPField]
        public float burnTime = 0f;

        /// <summary>
        /// Engines will only ignite when below this height, regardless of speed.
        /// Too high of a speed, and the rockets will not be able to reduce velocity enough.
        /// </summary>
        [KSPField]
        public float maxStartHeight = 20f;

        [KSPField(isPersistant =true)]
        public bool fired = false;

        private double prevAlt;
        private float prevAccel;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) { return; }
            if (vessel == null) { return; }
            if (fired) { return; }
            if (vessel.radarAltitude < maxStartHeight)
            {

                prevAlt = vessel.radarAltitude;
            }
        }

    }
}
