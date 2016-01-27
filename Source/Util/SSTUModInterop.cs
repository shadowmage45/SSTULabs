using System;
using UnityEngine;
using System.Reflection;

namespace SSTUTools
{ 
    public static class SSTUModInterop
    {
        //TODO move these to a map/set of maps
        private static bool checkedFar = false;
        private static bool checkedRF = false;
        private static bool checkedMFT = false;
        private static bool installedFAR = false;
        private static bool installedRF = false;
        private static bool installedMFT = false;

        public static void onPartGeometryUpdate(Part part, bool createDefaultCube)
        {
            if (isFARInstalled())
            {
                part.SendMessage("GeometryPartModuleRebuildMeshData");
            }
            else if (createDefaultCube && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            { 
                DragCube newDefaultCube =DragCubeSystem.Instance.RenderProceduralDragCube(part);
                newDefaultCube.Weight = 1f;
                newDefaultCube.Name = "Default";
                part.DragCubes.ClearCubes();
                part.DragCubes.Cubes.Add(newDefaultCube);
                part.DragCubes.ResetCubeWeights();
            }
        }

        public static void onPartFuelVolumeUpdate(Part part, float cubicMeters)
        {
            if (!isRFInstalled() && !isMFTInstalled())
            {
                MonoBehaviour.print("Neither RF nor MFT is installed, cannot update part volumes through them.");
                return;
            }
            Type moduleFuelTank = Type.GetType("RealFuels.Tanks.ModuleFuelTanks,RealFuels");
            if (moduleFuelTank == null)
            {
                moduleFuelTank = Type.GetType("RealFuels.Tanks.ModuleFuelTanks,modularFuelTanks");
                if (moduleFuelTank == null)
                {
                    MonoBehaviour.print("Fuel tank is set to use RF, but neither RF nor MFT are installed!!");
                    return;
                }
            }
            PartModule pm = (PartModule)part.GetComponent(moduleFuelTank);
            if (pm == null)
            {
                MonoBehaviour.print("ERROR! could not find fuel tank module in part for RealFuels");
                return;
            }
            MethodInfo mi = moduleFuelTank.GetMethod("ChangeTotalVolume");
            double val = cubicMeters * 1000f;
            mi.Invoke(pm, new System.Object[] { val, false });
            MonoBehaviour.print("set RF total tank volume to: " + val);
            MethodInfo mi2 = moduleFuelTank.GetMethod("CalculateMass");
            mi2.Invoke(pm, new System.Object[] { });
        }

        public static bool isFARInstalled()
        {
            if (!checkedFar)
            {
                checkedFar = true;
                installedFAR = isAssemblyLoaded("FerramAerospaceResearch");
            }
            return installedFAR;
        }

        public static bool isRFInstalled()
        {
            if (!checkedRF)
            {
                checkedRF = true;
                installedRF = isAssemblyLoaded("RealFuels");
            }
            return installedRF;    
        }

        public static bool isMFTInstalled()
        {
            if (!checkedMFT)
            {
                checkedMFT = true;
                installedMFT = isAssemblyLoaded("modularFuelTanks");               
            }
            return installedMFT;
        }

        private static bool isAssemblyLoaded(String name)
        {
            for (int i = 0; i < AssemblyLoader.loadedAssemblies.Count; i++)
            {
                AssemblyLoader.LoadedAssembly assembly = AssemblyLoader.loadedAssemblies[i];
                if (assembly.name == name)
                {
                    return true;
                }
            }
            return false;
        }
                
    }
}
