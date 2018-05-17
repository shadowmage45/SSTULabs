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

        public readonly string containerName;
        public readonly int containerIndex;
        public readonly float containerVolume;
        public ContainerContribution(string name, int index, float volumeLiters)
        {
            containerName = name;
            containerIndex = index;
            containerVolume = volumeLiters;
        }

        public override string ToString()
        {
            return "CC[" + containerName + "]-" + containerIndex + "-" + containerVolume;
        }

    }

    public interface ISSTUAnimatedModule
    {
        AnimState getAnimationState();
        void setAnimationState(AnimState newState);
    }


}
