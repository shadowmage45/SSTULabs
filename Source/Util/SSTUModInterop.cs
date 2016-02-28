using System;
using UnityEngine;
using System.Reflection;

namespace SSTUTools
{ 
    public static class SSTUModInterop
    {
        private static bool checkedFar = false;
        private static bool checkedRF = false;
        private static bool checkedMFT = false;
        private static bool installedFAR = false;
        private static bool installedRF = false;
        private static bool installedMFT = false;

        public static void onEngineConfigChange(Part part, float scale)
        {
            if (isRFInstalled())
            {
                Type type = Type.GetType("RealFuels.ModuleEngineConfigs,RealFuels");
                if (type == null)
                {
                    MonoBehaviour.print("ERROR: Could not locate ModuleEngineConfigs by type!");
                }
                PartModule module = (PartModule)part.GetComponent(type);
                if (module == null)
                {
                    MonoBehaviour.print("ERROR: Could not locate ModuleEngineConfigs for part: "+part.name+" while updating engine stats, this may be a configuration error.");
                    return;
                }
                type.GetField("scale").SetValue(module, scale);
                type.GetMethod("SetConfiguration").Invoke(module, new System.Object[] {null, true});
                MonoBehaviour.print("Updated ModuleEngineConfigs configuration for part: " + part.name);
            }
        }

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
            bool mfTank = false;
            Type moduleFuelTank = null;
            if (isRFInstalled())
            {
                moduleFuelTank = Type.GetType("RealFuels.Tanks.ModuleFuelTanks,RealFuels");
                if (moduleFuelTank == null)
                {
                    MonoBehaviour.print("ERROR: Set to use RealFuels, and RealFuels is installed, but no RealFuels-ModuleFuelTank PartModule found.");
                    return;
                }
            }
            else if (isMFTInstalled())
            {
                moduleFuelTank = Type.GetType("RealFuels.Tanks.ModuleFuelTanks,modularFuelTanks");
                if (moduleFuelTank == null)
                {
                    MonoBehaviour.print("ERROR: Set to use ModularFuelTanks, and ModularFuelTanks is installed, but no ModularFuelTanks-ModuleFuelTank PartModule found.");
                    return;
                }
                mfTank = true;
            }
            else
            {
                MonoBehaviour.print("Config is for part: "+part+" is set to use RF/MFT, but neither RF nor MFT is installed, cannot update part volumes through them.  Please check your configs and/or patches for errors.");
                return;
            }
            PartModule pm = (PartModule)part.GetComponent(moduleFuelTank);
            if (pm == null)
            {
                MonoBehaviour.print("ERROR! Could not find ModuleFuelTank in part for RealFuels/MFT for type: "+moduleFuelTank);
                return;
            }
            MethodInfo mi = moduleFuelTank.GetMethod("ChangeTotalVolume");
            double volumeLiters = cubicMeters * 1000f;
            if (mfTank)
            {
                volumeLiters *= 0.2f;//convert liters into stock units....
            }
            mi.Invoke(pm, new System.Object[] { volumeLiters, false });
            String message = "SSTU - Set RF/MFT total tank volume to: " + volumeLiters + (mfTank? " Units": " Liters for part: "+part.name);
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
