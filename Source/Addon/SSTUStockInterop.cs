using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    [KSPAddon(KSPAddon.Startup.Instantly|KSPAddon.Startup.EveryScene, false)]
    public class SSTUStockInterop : MonoBehaviour
    {
        private static Dictionary<String, ConfigNode> partConfigNodes = new Dictionary<string, ConfigNode>();

        private static List<Part> dragCubeUpdateParts = new List<Part>();
        private static List<Part> delayedUpdateDragCubeParts = new List<Part>();

        private static bool fireEditorEvent = false;

        public static SSTUStockInterop INSTANCE;
        
        public void Start()
        {
            INSTANCE = this;
            //TODO investigate why it gets destroyed even when flagged for 'once' type setup
            //GameObject.DontDestroyOnLoad(this);
            //MonoBehaviour.print("SSTUStockInterop Start"); 
        }

        public void OnDestroy()
        {
            //MonoBehaviour.print("SSTUStockInterop Destroy");
        }

        public static void addDragUpdatePart(Part part)
        {            
            if(part!=null && !dragCubeUpdateParts.Contains(part))
            {
                dragCubeUpdateParts.Add(part); 
            }          
        }

        public static void fireEditorUpdate()
        {
            fireEditorEvent = HighLogic.LoadedSceneIsEditor;
        }

        public void FixedUpdate()
        {
            int len = dragCubeUpdateParts.Count;
            if (len>0 && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                Part p;                
                for (int i = 0; i < len; i++)
                {
                    p = dragCubeUpdateParts[i];
                    if (p == null) { continue; }
                    MonoBehaviour.print("Updating procedural drag cube for: " + p);
                    updatePartDragCube(p);
                }
                dragCubeUpdateParts.Clear();               
            }
        }

        public void LateUpdate()
        {
            if (HighLogic.LoadedSceneIsEditor && fireEditorEvent)
            {
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
            fireEditorEvent = false;
        }
        
        public void ModuleManagerPostLoad()
        {
            MonoBehaviour.print("SSTU Creating Part Config cache.");
            partConfigNodes.Clear();
            ConfigNode[] partNodes = GameDatabase.Instance.GetConfigNodes("PART");
            String name;
            foreach (ConfigNode node in partNodes)
            {
                name = node.GetStringValue("name");
                name = name.Replace('_', '.');
                if (partConfigNodes.ContainsKey(name)) { continue; }
                partConfigNodes.Add(name, node);
            }
            FuelTypes.INSTANCE.reloadData();
            SSTUModelData.reloadData();
            VolumeContainerLoader.loadConfigs();
        }

        private static void updatePartDragCube(Part part)
        {
            DragCube newDefaultCube = DragCubeSystem.Instance.RenderProceduralDragCube(part);
            newDefaultCube.Weight = 1f;
            newDefaultCube.Name = "Default";
            part.DragCubes.ClearCubes();
            part.DragCubes.Cubes.Add(newDefaultCube);
            part.DragCubes.ResetCubeWeights();
        }

        public static ConfigNode getPartModuleConfig(PartModule module)
        {
            return getPartModuleConfig(module.part, module);
        }

        public static ConfigNode getPartModuleConfig(Part p, int moduleIndex)
        {
            return getPartConfig(p).GetNodes("MODULE")[moduleIndex];
        }

        public static ConfigNode getPartModuleConfig(Part p, String moduleName)
        {
            return getPartConfig(p).GetNode("MODULE", "name", moduleName);
        }

        public static ConfigNode getPartModuleConfig(Part p, PartModule m)
        {
            ConfigNode partNode = getPartConfig(p);
            ConfigNode[] moduleNodes = partNode.GetNodes("MODULE");
            int index = p.Modules.IndexOf(m);
            if (index >= moduleNodes.Length)
            {
                MonoBehaviour.print("Module index was out of range: " + index + " : " + moduleNodes.Length);
                return null;
            }
            String type = m.GetType().Name;
            ConfigNode moduleNode = moduleNodes[index];
            if (moduleNode.GetStringValue("name") == type){ return moduleNode; }
            MonoBehaviour.print("Could not find matching index for module: " + type + " :: " + m + " returning first module by name.");
            return getPartModuleConfig(p, type);
        }

        public static ConfigNode getPartConfig(Part p)
        {
            String name = p.name;
            if (partConfigNodes.ContainsKey(name)){return partConfigNodes[name];}            
            MonoBehaviour.print("MINOR ERROR: Could not locate part config from cached database for part: "+name+" (This may be recoverable)");
            if (p.partInfo != null && p.partInfo.partConfig != null) { return p.partInfo.partConfig; }
            MonoBehaviour.print("MAJOR ERROR: Could not locate part config from part.partInfo.partConfig for part: " + name);
            if (p.partInfo != null) { return PartLoader.Instance.GetDatabaseConfig(p); }
            MonoBehaviour.print("SEVERE ERROR: Could not locate part config from PartLoader.Instance.GetDatabaseConfig() for part: " + name+"  Things are about to crash :)");
            return null;
        }
        
        public static void updateEngineThrust(ModuleEngines engine, float minThrust, float maxThrust)
        {
            engine.minThrust = minThrust;
            engine.maxThrust = maxThrust;
            ConfigNode updateNode = new ConfigNode("MODULE");
            updateNode.AddValue("maxThrust", engine.maxThrust);
            updateNode.AddValue("minThrust", engine.minThrust);
            engine.Load(updateNode);
        }
    }
}
