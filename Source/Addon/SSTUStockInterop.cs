using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class SSTUStockInterop : MonoBehaviour
    {
        private static Dictionary<String, ConfigNode> partConfigNodes = new Dictionary<string, ConfigNode>();
        
        public void ModuleManagerPostLoad()
        {
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
            return getPartModuleConfig(p, p.Modules.IndexOf(m));
        }

        public static ConfigNode getPartConfig(Part p)
        {
            if (partConfigNodes.ContainsKey(p.name)){return partConfigNodes[p.name];}            
            MonoBehaviour.print("Could not locate part config from cached database for part: "+p.name);
            if (p.partInfo != null && p.partInfo.partConfig != null) { return p.partInfo.partConfig; }
            MonoBehaviour.print("Could not locate part config from part.partInfo.partConfig for part: " + p.name);
            if (p.partInfo != null) { return PartLoader.Instance.GetDatabaseConfig(p); }
            MonoBehaviour.print("Could not locate part config from PartLoader.Instance.GetDatabaseConfig() for part: " + p.name);
            return null;
        }
        
        public static void updateEngineThrust(ModuleEngines engine, float minThrust, float maxThrust)
        {
            //MonoBehaviour.print("updating engine thrust: " + minThrust + "::" + maxThrust);
            engine.minThrust = minThrust;
            engine.maxThrust = maxThrust;
            ConfigNode updateNode = new ConfigNode("MODULE");
            updateNode.AddValue("maxThrust", engine.maxThrust);
            updateNode.AddValue("minThrust", engine.minThrust);
            engine.Load(updateNode);
        }
    }
}
