using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{

    public class SSTUFuelTypes
    {
        public static readonly SSTUFuelTypes INSTANCE = new SSTUFuelTypes();

        private bool loadedDefs = false;

        private Dictionary<String, SSTUFuelType> fuelTypes = new Dictionary<String, SSTUFuelType>();

        private Dictionary<String, float> resourceVolumes = new Dictionary<String, float>();

        private void loadDefs()
        {
            if (loadedDefs) { return; }
            fuelTypes.Clear();
            resourceVolumes.Clear();

            ConfigNode[] configs = GameDatabase.Instance.GetConfigNodes("SSTU_RESOURCEVOLUME");
            foreach (ConfigNode node in configs)
            {
                resourceVolumes.Add(node.GetStringValue("name"), node.GetFloatValue("volume"));
            }

            configs = GameDatabase.Instance.GetConfigNodes("SSTU_FUELTYPE");
            SSTUFuelType fuelType;
            foreach (ConfigNode node in configs)
            {
                fuelType = new SSTUFuelType(node);
                fuelTypes.Add(fuelType.name, fuelType);
            }

            loadedDefs = true;
        }

        public SSTUFuelType getFuelType(String type)
        {
            loadDefs();
            SSTUFuelType t = null;
            fuelTypes.TryGetValue(type, out t);
            return t;
        }

        public SSTUFuelTypeData getFuelTypeData(String type)
        {
            loadDefs();
            return new SSTUFuelTypeData(getFuelType(type));
        }

        public float getResourceVolume(String name)
        {
            float val = 0;
            resourceVolumes.TryGetValue(name, out val);
            return val;
        }
    }
    
        
    public class SSTUFuelTypeData
    {
        public SSTUFuelType fuelType;        
        public float tankageVolumeLoss;
        public float tankageMassFraction;
        public float costPerDryTon;

        public SSTUFuelTypeData(ConfigNode node)
        {
            String name = node.GetStringValue("name");
            fuelType = SSTUFuelTypes.INSTANCE.getFuelType(name);
            tankageVolumeLoss = node.GetFloatValue("tankageVolumeLoss", fuelType.tankageVolumeLoss);
            tankageMassFraction = node.GetFloatValue("tankageMassFraction", fuelType.tankageMassFactor);
            costPerDryTon = node.GetFloatValue("costPerDryTon", fuelType.costPerDryTon);
        }

        public SSTUFuelTypeData(SSTUFuelType type)
        {
            fuelType = type;
            tankageMassFraction = type.tankageMassFactor;
            tankageVolumeLoss = type.tankageVolumeLoss;
            costPerDryTon = type.costPerDryTon;
        }

        public static SSTUFuelTypeData[] parseFuelTypeData(ConfigNode[] nodes)
        {
            int len = nodes.Length;
            SSTUFuelTypeData[] array = new SSTUFuelTypeData[len];
            for (int i = 0; i < len; i++)
            {
                array[i] = new SSTUFuelTypeData(nodes[i]);
            }
            return array;
        }

        public SSTUResourceList addResources(float volume, SSTUResourceList list)
        {
            fuelType.addResources(volume, list);
            return list;
        }

        public SSTUResourceList getResourceList(float volume)
        {
            return fuelType.getResourceList(volume);
        }
    }

    public class SSTUFuelType
    {
        private List<SSTUFuelEntry> fuelEntries = new List<SSTUFuelEntry>();
        public readonly String name;
        public readonly float tankageVolumeLoss;
        public readonly float tankageMassFactor;
        public readonly float costPerDryTon;

        private float litersPerUnit;
        private float costPerUnit;
        private float unitsPerCubicMeter;

        public SSTUFuelType(ConfigNode node)
        {
            name = node.GetStringValue("name");
            tankageVolumeLoss = node.GetFloatValue("tankageVolumeLoss", 0.15f);
            tankageMassFactor = node.GetFloatValue("tankageMassFactor", 0.15f);
            costPerDryTon = node.GetFloatValue("costPerDryTon");
            ConfigNode[] fuelConfigs = node.GetNodes("RESOURCE");
            SSTUFuelEntry e = null;
            foreach (ConfigNode n in fuelConfigs)
            {
                e = new SSTUFuelEntry(n);
                fuelEntries.Add(e);
                litersPerUnit += SSTUFuelTypes.INSTANCE.getResourceVolume(e.resourceName) * e.ratio;
                costPerUnit += PartResourceLibrary.Instance.GetDefinition(e.resourceName).unitCost * e.ratio;
            }
            unitsPerCubicMeter = 1000f / litersPerUnit;            
        }

        public override string ToString()
        {
            return string.Format("[SSTUFuelType: " + name + "]");
        }

        public void setResourcesInPart(Part part, float volume, bool fill)
        {
            part.Resources.list.Clear();
            PartResource[] resources = part.GetComponents<PartResource>();
            int len = resources.Length;
            for (int i = 0; i < len; i++)
            {
                GameObject.Destroy(resources[i]);
            }
            int rawFuelUnits = (int)(volume * unitsPerCubicMeter);
            int units;
            ConfigNode resourceNode;
            foreach (SSTUFuelEntry entry in fuelEntries)
            {
                units = entry.ratio * rawFuelUnits;
                resourceNode = new ConfigNode("RESOURCE");
                resourceNode.AddValue("name", entry.resourceName);
                resourceNode.AddValue("maxAmount", units);
                resourceNode.AddValue("amount", fill ? units : 0);
                part.AddResource(resourceNode);
            }
        }

        public SSTUResourceList getResourceList(float volume)
        {
            SSTUResourceList resourceList = new SSTUResourceList();
            return addResources(volume, resourceList);
        }

        public SSTUResourceList addResources(float volume, SSTUResourceList list)
        {
            int rawFuelUnits = (int)(volume * unitsPerCubicMeter);
            int units;
            foreach (SSTUFuelEntry entry in fuelEntries)
            {
                units = entry.ratio * rawFuelUnits;
                list.addResource(entry.resourceName, units);
            }
            return list;
        }

        public float getResourceCost(float cubicMeters)
        {
            return costPerUnit * cubicMeters * unitsPerCubicMeter;
        }
    }

    public class SSTUFuelEntry
    {
        public String resourceName;
        public int ratio;
        public SSTUFuelEntry(ConfigNode node)
        {
            resourceName = node.GetStringValue("resource");
            ratio = node.GetIntValue("ratio", 1);
        }
    }

    /// <summary>
    /// Will be the base class for manipulating resources in a part, including cross-module manipulation.
    /// </summary>
    public class SSTUResourceList
    {
        private Dictionary<String, float> resourceMap = new Dictionary<string, float>();

        public void addResourceByVolume(String name, float volume)
        {
            float litersPerUnit = SSTUFuelTypes.INSTANCE.getResourceVolume(name);
            addResource(name, (volume * 1000) / litersPerUnit);
        }
        
        public void addResource(String name, float quantity)
        {
            if (resourceMap.ContainsKey(name))
            {
                float val = resourceMap[name];
                val += quantity;
                resourceMap[name] = val;
            }
            else
            {
                resourceMap.Add(name, quantity);
            }
        }

        public void removeResource(String name, float quantity)
        {
            if (resourceMap.ContainsKey(name))
            {
                float val = resourceMap[name];
                val -= quantity;
                if (val <= 0)
                {
                    resourceMap.Remove(name);
                }
                else
                {
                    resourceMap[name] = val;
                }
            }
        }

        public void removeResource(String name)
        {
            resourceMap.Remove(name);
        }

        public void setResourcesToPart(Part part, bool fill)
        {
            part.Resources.list.Clear();
            PartResource[] resources = part.GetComponents<PartResource>();
            int len = resources.Length;
            for (int i = 0; i < len; i++)
            {
                GameObject.Destroy(resources[i]);
            }            
            ConfigNode resourceNode;
            float amt = 0;
            foreach (String name in resourceMap.Keys)
            {
                amt = resourceMap[name];
                resourceNode = new ConfigNode("RESOURCE");
                resourceNode.AddValue("name", name);
                resourceNode.AddValue("maxAmount", amt);
                resourceNode.AddValue("amount", fill ? amt : 0);
                part.AddResource(resourceNode);
            }
        }
    }

}

