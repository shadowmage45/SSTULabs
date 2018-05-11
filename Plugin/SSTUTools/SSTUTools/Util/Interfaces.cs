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
        /// Return an array of container contributions.
        /// </summary>
        /// <returns></returns>
        ContainerContribution[] getContainerContributions();
    }

    public struct ContainerContribution
    {
        public readonly int containerIndex;
        public readonly float containerVolume;
        public ContainerContribution(int index, float volume)
        {
            containerIndex = index;
            containerVolume = volume;
        }
    }

    public interface ISSTUAnimatedModule
    {
        AnimState getAnimationState();
        void setAnimationState(AnimState newState);
    }


}
