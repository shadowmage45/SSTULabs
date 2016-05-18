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
        private static bool checkedKIS = false;
        private static bool installedFAR = false;
        private static bool installedRF = false;
        private static bool installedMFT = false;
        private static bool installedKIS = false;

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
                MonoBehaviour.print("Updated ModuleEngineConfigs configuration for part: " + part.name+ " for config name: "+config+" for scale: "+scale);
            }
        }

        public static void onPartGeometryUpdate(Part part, bool createDefaultCube)
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { return; }//noop on prefabs
            //MonoBehaviour.print(System.Environment.StackTrace);
            part.HighlightRenderers = null;//force refresh of part highlighting
            part.SendMessage("onPartGeometryChanged", part);//used by SSTUFlagDecal and potentially others in the future
            if (isFARInstalled())
            {
                part.SendMessage("GeometryPartModuleRebuildMeshData");
            }
            else if (createDefaultCube && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                SSTUStockInterop.addDragUpdatePart(part);
            }
        }

        public static bool onPartFuelVolumeUpdate(Part part, float liters)
        {
            SSTUVolumeContainer vc = part.GetComponent<SSTUVolumeContainer>();
            if (vc != null)
            {
                vc.onVolumeUpdated(liters);
                return true;
            }
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
                MonoBehaviour.print("Config is for part: "+part+" is set to use RF/MFT, but neither RF nor MFT is installed, cannot update part volumes through them.  Please check your configs and/or patches for errors.");
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
            String message = "SSTU - Set RF/MFT total tank volume to: " + volumeLiters + " Liters for part: " + part.name;
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

        public static void onPartKISVolumeUpdated(Part part, float liters)
        {
            if (!isKISInstalled())
            {
                MonoBehaviour.print("KIS is not installed, cannot update KIS volume..");
                return;
            }
            string typeName = "KIS.ModuleKISInventory,KIS";
            Type kisModuleType = Type.GetType(typeName);
            if (kisModuleType == null)
            {
                MonoBehaviour.print("ERROR: Could not locate KIS module for name: "+typeName);
            }
            PartModule pm = (PartModule)part.GetComponent(kisModuleType);
            if(pm == null)
            {
                if (liters != 0)
                {
                    MonoBehaviour.print("ERROR: Could not locate KIS module on part for type: " + part + " :: " + kisModuleType);
                }
                return;
            }
            FieldInfo fi = kisModuleType.GetField("maxVolume");
            fi.SetValue(pm, liters);
            BaseEvent inventoryEvent = pm.Events["ShowInventory"];
            inventoryEvent.guiActive = inventoryEvent.guiActiveEditor = liters > 0;
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

        public static bool isKISInstalled()
        {
            if (!checkedKIS)
            {
                checkedKIS = true;
                installedKIS = isAssemblyLoaded("KIS");
            }
            return installedKIS;
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
            if (UIPartActionController.Instance.resourcesShown.Count > 0)
            {
                UIPartActionWindow window = UIPartActionController.Instance.GetItem(part);
                if (window != null) { window.displayDirty = true; }
            }
        }
                
    }
}
