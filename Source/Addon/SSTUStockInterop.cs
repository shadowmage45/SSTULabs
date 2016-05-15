using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class SSTUStockInterop : MonoBehaviour
    {
        private static Dictionary<String, ConfigNode> partConfigNodes = new Dictionary<string, ConfigNode>();

        private static List<Part> dragCubeUpdateParts = new List<Part>();
        private static List<Part> delayedUpdateDragCubeParts = new List<Part>();
        private static Dictionary<string, float> techLimitCache = new Dictionary<string, float>();

        private static bool fireEditorEvent = false;

        public static SSTUStockInterop INSTANCE;
        
        public void Start()
        {
            INSTANCE = this;
            GameObject.DontDestroyOnLoad(this);
            MonoBehaviour.print("SSTUStockInterop Start");
            GameEvents.onGameStateLoad.Add(new EventData<ConfigNode>.OnEvent(onGameLoad));
            GameEvents.onLevelWasLoadedGUIReady.Add(new EventData<GameScenes>.OnEvent(onSceneLoaded));
        }

        public void OnDestroy()
        {
            MonoBehaviour.print("SSTUStockInterop Destroy");
            GameEvents.onGameStateLoad.Remove(new EventData<ConfigNode>.OnEvent(onGameLoad));
            GameEvents.onLevelWasLoadedGUIReady.Remove(new EventData<GameScenes>.OnEvent(onSceneLoaded));
        }

        public void onSceneLoaded(GameScenes scene)
        {
            if (scene == GameScenes.SPACECENTER || scene==GameScenes.EDITOR || scene == GameScenes.FLIGHT)
            {
                //MonoBehaviour.print("Onscene loaded: " + scene);
                //MonoBehaviour.print("RD: " + ResearchAndDevelopment.Instance);
                updateTechLimitCache();
            }
        }

        public void onGameLoad(ConfigNode node)
        {
            //MonoBehaviour.print("onGameLoad!");
            //MonoBehaviour.print("RD: " + ResearchAndDevelopment.Instance);
            techLimitCache.Clear();
        }

        public static void addDragUpdatePart(Part part)
        {            
            if(part!=null && HighLogic.LoadedSceneIsFlight && !dragCubeUpdateParts.Contains(part))
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
                    updatePartDragCube(p);
                    if(p.collider== null) { seatFirstCollider(p); }
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
            MonoBehaviour.print("SSTU -- Creating Part Config cache.");
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
            MonoBehaviour.print("SSTU -- Reloading config databases (fuel types, model data, etc...)");
            FuelTypes.INSTANCE.reloadData();
            SSTUModelData.reloadData();
            VolumeContainerLoader.loadConfigs();
            SSTUDatabase.reloadDatabase();
        }

        private static void updateTechLimitCache()
        {
            techLimitCache.Clear();
            ConfigNode[] techLimitSets = GameDatabase.Instance.GetConfigNodes("TECHLIMITSET");
            ConfigNode setNode;
            ConfigNode[] techLimitNodes;
            ConfigNode limitNode;
            string setName;
            string techName;
            float techLimit;
            float setTechLimit;
            int len = techLimitSets.Length;
            int len2;
            bool researchGame = SSTUUtils.isResearchGame();
            for (int i = 0; i < len; i++)
            {
                setNode = techLimitSets[i];
                setName = setNode.GetStringValue("name");
                if (techLimitCache.ContainsKey(setName)) { continue; }
                setTechLimit = float.PositiveInfinity;
                if (researchGame)
                {
                    setTechLimit = 0;
                    techLimitNodes = setNode.GetNodes("TECHLIMIT");
                    len2 = techLimitNodes.Length;
                    for (int k = 0; k < len2; k++)
                    {
                        limitNode = techLimitNodes[k];
                        techName = limitNode.GetStringValue("name");
                        techLimit = limitNode.GetFloatValue("diameter");
                        if (techLimit > setTechLimit && SSTUUtils.isTechUnlocked(techName))
                        {
                            setTechLimit = techLimit;
                        }
                    }
                }
                //MonoBehaviour.print("Loaded tech limit of: " + setName + " :: " + setTechLimit);
                techLimitCache.Add(setName, setTechLimit);
            }
        }

        private static void seatFirstCollider(Part part)
        {
            Collider[] colliders = part.gameObject.GetComponentsInChildren<Collider>();
            int len = colliders.Length;
            for (int i = 0; i < len; i++)
            {
                if (colliders[i].isTrigger) { continue; }
                if (colliders[i].GetType() == typeof(WheelCollider)) { continue; }
                part.collider = colliders[i];
                break;
            }
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

        public static float getTechLimit(string name)
        {
            float limit = float.PositiveInfinity;
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { return limit; }//for prefab parts....            
            if (!techLimitCache.TryGetValue(name, out limit)) { return float.PositiveInfinity; }//for uninitialized cache or invalid key, return max value
            //MonoBehaviour.print("found tech limit for set name: " + name + " :: " + limit);
            return limit;
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
            if (name.IndexOf('(') > 0)
            {
                string sName = name.Substring(0, name.IndexOf('(')).Trim();
                if (partConfigNodes.ContainsKey(sName)) { name = sName; }
            }
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
