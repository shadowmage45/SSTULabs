using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{

    public interface IPartGeometryUpdated
    {
        void geometryUpdated(Part part);
    }

    public interface IContainerVolumeContributor
    {
        int[] getContainerIndices();
        float[] getContainerVolumes();
    }

}
