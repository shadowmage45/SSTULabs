using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using KSPShaderTools;

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

        private static List<Action<SSTUVolumeContainer>> containerUpdatedCallbacks = new List<Action<SSTUVolumeContainer>>();

        public static void onContainerUpdated(SSTUVolumeContainer container)
        {
            int len = containerUpdatedCallbacks.Count;
            for (int i = 0; i < len; i++)
            {
                containerUpdatedCallbacks[i].Invoke(container);
            }
        }

        public static void addContainerUpdatedCallback(Action<SSTUVolumeContainer> cb) { containerUpdatedCallbacks.AddUnique(cb); }

        public static void removeContainerUpdatedCallback(Action<SSTUVolumeContainer> cb) { containerUpdatedCallbacks.Remove(cb); }

        public static void updateResourceVolume(Part part)
        {
            SSTULog.debug("Part volume changed...");
            SSTUVolumeContainer vc = part.GetComponent<SSTUVolumeContainer>();
            if (vc != null)
            {
                vc.recalcVolume();
                SSTUResourceBoiloff rb = part.GetComponent<SSTUResourceBoiloff>();
                if (rb != null) { rb.onPartResourcesChanged(); }
            }
            else
            {
                IContainerVolumeContributor[] contributors = part.FindModulesImplementing<IContainerVolumeContributor>().ToArray();
                ContainerContribution[] cts;
                int len = contributors.Length;
                float totalVolume = 0;
                for (int i = 0; i < len; i++)
                {
                    cts = contributors[i].getContainerContributions();
                    int len2 = cts.Length;
                    for (int k = 0; k < len2; k++)
                    {
                        totalVolume += cts[k].containerVolume;
                    }
                }
                realFuelsVolumeUpdate(part, totalVolume);
            }
        }

        //RealFuels ModuleEngineConfigs compatibility for updating the 'scale' value of an engine
        public static void onEngineConfigChange(Part part, String config, float scale)
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
                type.GetMethod("SetConfiguration").Invoke(module, new System.Object[] { config, true});
                MonoBehaviour.print("SSTUModInterop: Updated ModuleEngineConfigs configuration for part: " + part.name+ " for config name: "+config+" for scale: "+scale);
            }
        }

        /// <summary>
        /// Updates part highlight renderer list, sends message to SSTUFlagDecal to update its renderer,
        ///  sends message to FAR to update voxels, or if createDefaultCube==true will re-render the 'default' stock drag cube for the part<para/>
        /// Should be called anytime the model geometry in a part is changed -- either models added/deleted, procedural meshes updated.  Other methods exist for pure drag-cube updating in SSTUStockInterop.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="createDefaultCube"></param>
        public static void onPartGeometryUpdate(Part part, bool createDefaultCube)
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { return; }//noop on prefabs
            //MonoBehaviour.print(System.Environment.StackTrace);
            SSTUStockInterop.updatePartHighlighting(part);
            part.airlock = locateAirlock(part);
            partGeometryUpdate(part);
            if (isFARInstalled())
            {
                SSTUStockInterop.addFarUpdatePart(part);
                //FARdebug(part);
                //part.SendMessage("GeometryPartModuleRebuildMeshData");
            }
            else if (createDefaultCube && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                SSTUStockInterop.addDragUpdatePart(part);
            }
            if (HighLogic.LoadedSceneIsEditor && part.parent==null && part!=EditorLogic.RootPart)//likely the part under the cursor; this fixes problems with modular parts not wanting to attach to stuff
            {
                part.gameObject.SetLayerRecursive(1, 2097152);//1<<21 = Part Triggers get skipped by the relayering (hatches, ladders, ??)
            }
        }

        public static void onPartTextureUpdated(Part part)
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                TextureCallbacks.onTextureSetChanged(part);
            }
        }

        private static void partGeometryUpdate(Part part)
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                TextureCallbacks.onPartModelChanged(part);
            }
        }

        private static Transform locateAirlock(Part part)
        {
            Collider[] componentsInChildren = part.GetComponentsInChildren<Collider>();
            int len = componentsInChildren.Length;
            for (int i = 0; i < len; i++)
            {
                if (componentsInChildren[i].gameObject.tag == "Airlock")
                {
                    return componentsInChildren[i].transform;
                }
            }
            return null;
        }

        private static void FARdebug(Part part)
        {
            MonoBehaviour.print("FAR DEBUG FOR PART: " + part);
            GameObject go;
            Mesh m;
            Mesh m2;
            MeshFilter[] mfs = part.GetComponentsInChildren<MeshFilter>();
            MeshFilter mf;
            int len = mfs.Length;
            for (int i = 0; i < len; i++)
            {
                mf = mfs[i];
                m = mf.mesh;
                m2 = mf.sharedMesh;
                go = mf.gameObject;
                MonoBehaviour.print("FAR debug data || go: " + go + " || mf: " + mf + " || mesh: " + m + " || sharedMesh" + m2);
            }
            MonoBehaviour.print("-------------------------------------------------------------------------");
        }

        private static bool realFuelsVolumeUpdate(Part part, float liters)
        {
            Type moduleFuelTank = null;
            if (isRFInstalled())
            {
                moduleFuelTank = Type.GetType("RealFuels.Tanks.ModuleFuelTanks,RealFuels");
                if (moduleFuelTank == null)
                {
                    MonoBehaviour.print("ERROR: Set to use RealFuels, and RealFuels is installed, but no RealFuels-ModuleFuelTank PartModule found.");
                    return false;
                }
            }
            else if (isMFTInstalled())
            {
                moduleFuelTank = Type.GetType("RealFuels.Tanks.ModuleFuelTanks,modularFuelTanks");
                if (moduleFuelTank == null)
                {
                    MonoBehaviour.print("ERROR: Set to use ModularFuelTanks, and ModularFuelTanks is installed, but no ModularFuelTanks-ModuleFuelTank PartModule found.");
                    return false;
                }
            }
            else
            {
                MonoBehaviour.print("ERROR: Config is for part: "+part+" is set to use RF/MFT, but neither RF nor MFT is installed, cannot update part volumes through them.  Please check your configs and/or patches for errors.");
                return false;
            }
            PartModule pm = (PartModule)part.GetComponent(moduleFuelTank);
            if (pm == null)
            {
                MonoBehaviour.print("ERROR! Could not find ModuleFuelTank in part for RealFuels/MFT for type: "+moduleFuelTank);
                return false;
            }
            MethodInfo mi = moduleFuelTank.GetMethod("ChangeTotalVolume");
            double volumeLiters = liters;
            mi.Invoke(pm, new System.Object[] { volumeLiters, false });            
            MethodInfo mi2 = moduleFuelTank.GetMethod("CalculateMass");
            mi2.Invoke(pm, new System.Object[] { });
            updatePartResourceDisplay(part);
            String message = "SSTUModInterop - Set RF/MFT total tank volume to: " + volumeLiters + " Liters for part: " + part.name;
            MonoBehaviour.print(message);
            return true;
        }

        public static PartModule getModuleFuelTanks(Part part)
        {
            Type moduleFuelTank = null;
            if (isRFInstalled())
            {
                moduleFuelTank = Type.GetType("RealFuels.Tanks.ModuleFuelTanks,RealFuels");
                if (moduleFuelTank == null)
                {
                    MonoBehaviour.print("ERROR: Set to use RealFuels, and RealFuels is installed, but no RealFuels-ModuleFuelTank PartModule found.");
                    return null;
                }
            }
            else if (isMFTInstalled())
            {
                moduleFuelTank = Type.GetType("RealFuels.Tanks.ModuleFuelTanks,modularFuelTanks");
                if (moduleFuelTank == null)
                {
                    MonoBehaviour.print("ERROR: Set to use ModularFuelTanks, and ModularFuelTanks is installed, but no ModularFuelTanks-ModuleFuelTank PartModule found.");
                    return null;
                }
            }
            PartModule pm = (PartModule)part.GetComponent(moduleFuelTank);
            return pm;
        }

        public static bool hasModuleFuelTanks(Part part)
        {
            return getModuleFuelTanks(part) != null;
        }

        public static bool hasModuleEngineConfigs(Part part)
        {
            Type type = Type.GetType("RealFuels.ModuleEngineConfigs,RealFuels");
            PartModule module = null;
            if (type != null)
            {
                module = (PartModule)part.GetComponent(type);
            }
            return module != null;
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

        public static void updatePartResourceDisplay(Part part)
        {
            if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch == null) { return; }
            if (HighLogic.LoadedSceneIsFlight && FlightDriver.fetch == null) { return; }
            try
            {
                if (UIPartActionController.Instance != null)
                {
                    UIPartActionWindow window = UIPartActionController.Instance.GetItem(part);
                    if (window != null) { window.displayDirty = true; }
                }
            }
            catch (Exception e)
            {
                MonoBehaviour.print("ERROR: Caught exception while updating part resource display: " + e.Message);
            }
        }
                
    }
}
