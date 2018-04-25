using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{

    public interface IContainerVolumeContributor
    {
        /// <summary>
        /// Return the indices of the containers that this module will adjust
        /// </summary>
        /// <returns></returns>
        int[] getContainerIndices();

        /// <summary>
        /// Return the volume of the containers in liters
        /// </summary>
        /// <returns></returns>
        float[] getContainerVolumes();
    }
    
    public interface ISSTUAnimatedModule
    {
        AnimState getAnimationState();
        void setAnimationState(AnimState newState);
    }


}
