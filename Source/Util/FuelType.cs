using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{

    public class FuelTypes
    {
        public static readonly FuelTypes INSTANCE = new FuelTypes();

        private bool loadedDefs = false;

        private Dictionary<string, FuelType> fuelTypes = new Dictionary<string, FuelType>();

        private Dictionary<string, float> resourceVolumes = new Dictionary<string, float>();

        private Dictionary<string, float> zeroMassResourceMasses = new Dictionary<string, float>();

        private Dictionary<string, float> zeroCostResourceCosts = new Dictionary<string, float>();

        private Dictionary<string, BoiloffData> boiloffResourceValues = new Dictionary<string, BoiloffData>();

        public void reloadData()
        {
            loadedDefs = false;
            loadDefs();
        }

        private void loadDefs()
        {
            if (loadedDefs) { return; }
            fuelTypes.Clear();
            resourceVolumes.Clear();
            zeroMassResourceMasses.Clear();
            zeroCostResourceCosts.Clear();
            boiloffResourceValues.Clear();

            ConfigNode[] configs = GameDatabase.Instance.GetConfigNodes("SSTU_RESOURCEVOLUME");
            foreach (ConfigNode node in configs)
            {
                resourceVolumes.Add(node.GetStringValue("name"), node.GetFloatValue("volume"));
            }

            configs = GameDatabase.Instance.GetConfigNodes("SSTU_ZEROMASSRESOURCE");
            foreach (ConfigNode node in configs)
            {
                zeroMassResourceMasses.Add(node.GetStringValue("name"), node.GetFloatValue("mass"));
            }

            configs = GameDatabase.Instance.GetConfigNodes("SSTU_ZEROCOSTRESOURCE");
            foreach (ConfigNode node in configs)
            {
                zeroCostResourceCosts.Add(node.GetStringValue("name"), node.GetFloatValue("cost"));
            }

            configs = GameDatabase.Instance.GetConfigNodes("SSTU_RESOURCEBOILOFF");
            foreach (ConfigNode node in configs)
            {
                string name = node.GetStringValue("name");
                float val = node.GetFloatValue("value");
                float cost = node.GetFloatValue("cost");
                MonoBehaviour.print("Loading boiloff data for resource: " + name + " : " + val + " : " + cost);
                boiloffResourceValues.Add(name, new BoiloffData(name, val, cost));
            }
            
            configs = GameDatabase.Instance.GetConfigNodes("SSTU_FUELTYPE");
            FuelType fuelType;
            foreach (ConfigNode node in configs)
            {
                fuelType = new FuelType(node);
                if (fuelType.isValid)//kind of hacky, but workable method to determine if the fuel type was missing any resources
                {
                    fuelTypes.Add(fuelType.name, fuelType);
                }
            }

            loadedDefs = true;
        }

        public FuelType getFuelType(String type)
        {
            loadDefs();
            FuelType t = null;
            fuelTypes.TryGetValue(type, out t);
            return t;
        }

        public FuelType[] getFuelTypes()
        {
            loadDefs();
            FuelType[] types = new FuelType[fuelTypes.Count];            
            int i = 0;
            foreach (FuelType t in fuelTypes.Values)
            {
                types[i] = t;
                i++;
            }
            Array.Sort(types, delegate (FuelType x, FuelType y) { return String.Compare(x.name,y.name); });
            return types;
        }

        public FuelTypeData getFuelTypeData(String type)
        {
            loadDefs();
            return new FuelTypeData(getFuelType(type));
        }

        public float getResourceVolume(String name)
        {
            PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(name);
            return getResourceVolume(name, def);
        }

        public float getResourceVolume(String name, PartResourceDefinition def)
        {
            float val = 0;
            if (!resourceVolumes.TryGetValue(name, out val))
            {
                val = def == null ? 5.0f : def.volume;
            }
            return val;
        }

        public float getZeroMassResourceMass(string name)
        {
            float val = 0;
            if (!zeroMassResourceMasses.TryGetValue(name, out val)) { return 0; }
            return val;
        }

        public float getZeroCostResourceCost(string name)
        {
            float val = 0;
            if (!zeroCostResourceCosts.TryGetValue(name, out val)) { return 0; }
            return val;
        }

        public BoiloffData getResourceBoiloffValue(string name)
        {
            BoiloffData val = null;
            if (!boiloffResourceValues.TryGetValue(name, out val)) { return null; }
            return val;
        }

        public BoiloffData[] getResourceBoiloffData(string[] resources)
        {
            List<BoiloffData> data = new List<BoiloffData>();
            BoiloffData d;
            int len = resources.Length;
            for (int i = 0; i < len; i++)
            {
                d = getResourceBoiloffValue(resources[i]);
                if (d != null) { data.Add(d); }
            }
            return data.ToArray();
        }
    }

    public class BoiloffData
    {
        public readonly string name;
        public readonly float value;
        public readonly float cost;
        public BoiloffData(string name, float value, float cost)
        {
            this.name = name;
            this.value = value;
            this.cost = cost;
        }
    }
            
    public class FuelTypeData
    {
        public readonly FuelType fuelType;
        public readonly String name;
        private float tankageVolumeLoss;
        private float tankageMassFraction;
        private float costPerDryTon;

        public FuelTypeData(ConfigNode node)
        {
            name = node.GetStringValue("name");
            fuelType = FuelTypes.INSTANCE.getFuelType(name);
            if (fuelType == null) { throw new NullReferenceException("Fuel type was null for fuel name: " + name); }
            tankageVolumeLoss = node.GetFloatValue("tankageVolumeLoss", fuelType.tankageVolumeLoss);
            tankageMassFraction = node.GetFloatValue("tankageMassFraction", fuelType.tankageMassFactor);
            costPerDryTon = node.GetFloatValue("costPerDryTon", fuelType.costPerDryTon);
        }

        public FuelTypeData(FuelType type)
        {
            fuelType = type;
            name = type.name;
            tankageMassFraction = type.tankageMassFactor;
            tankageVolumeLoss = type.tankageVolumeLoss;
            costPerDryTon = type.costPerDryTon;
        }

        public float getTankageVolumeLoss() { return tankageVolumeLoss; }

        public float getTankageMassFraction() { return tankageMassFraction; }

        public float getDryCost(float usableVolume) { return getTankageMass(usableVolume) * costPerDryTon; }

        public float getResourceCost(float usableVolume) { return fuelType.getResourceCost(usableVolume); }

        public float getUsableVolume(float rawVolume) { return rawVolume * (1.0f - tankageVolumeLoss); }

        public float getResourceMass(float usableVolume)
        {
            return fuelType.tonsPerCubicMeter * usableVolume;
        }

        public float getTankageMass(float usableVolume)
        {            
            return usableVolume * fuelType.tonsPerCubicMeter * tankageMassFraction;
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

        public static FuelTypeData[] parseFuelTypeData(ConfigNode[] nodes)
        {
            int len = nodes.Length;
            FuelTypeData[] array = new FuelTypeData[len];
            for (int i = 0; i < len; i++)
            {
                array[i] = new FuelTypeData(nodes[i]);
            }
            return array;
        }

        public static FuelTypeData createFuelTypeData(String typeName)
        {
            return new FuelTypeData(FuelTypes.INSTANCE.getFuelType(typeName));
        }
    }

    public class FuelType
    {
        public readonly List<SSTUFuelEntry> fuelEntries = new List<SSTUFuelEntry>();
        public readonly String name;
        public readonly float tankageVolumeLoss;
        public readonly float tankageMassFactor;
        public readonly float costPerDryTon;
        public readonly float tonsPerCubicMeter;
        public readonly float litersPerUnit;
        public readonly float costPerUnit;
        public readonly float unitsPerCubicMeter;
        public bool isValid = true;
        
        public FuelType(ConfigNode node)
        {
            name = node.GetStringValue("name");
            tankageVolumeLoss = node.GetFloatValue("tankageVolumeLoss", 0.15f);
            tankageMassFactor = node.GetFloatValue("tankageMassFactor", 0.15f);
            costPerDryTon = node.GetFloatValue("costPerDryTon");
            ConfigNode[] fuelConfigs = node.GetNodes("RESOURCE");
            if (fuelConfigs == null || fuelConfigs.Length == 0)
            {
                litersPerUnit = 1;
                tonsPerCubicMeter = tankageMassFactor;
                costPerUnit = costPerDryTon * 0.001f;
                unitsPerCubicMeter = 1000f;// 1 liter fake units
            }
            else
            {
                SSTUFuelEntry e = null;
                PartResourceDefinition def;
                float massPerUnit = 0;
                foreach (ConfigNode n in fuelConfigs)
                {
                    e = new SSTUFuelEntry(n);
                    fuelEntries.Add(e);
                    litersPerUnit += FuelTypes.INSTANCE.getResourceVolume(e.resourceName) * e.ratio;
                    def = PartResourceLibrary.Instance.GetDefinition(e.resourceName);
                    if (def == null)
                    {
                        MonoBehaviour.print("Could not locate resource definition for: " + e.resourceName + " :: Fuel type: " + name + " will be unavailable.");
                        isValid = false;
                        return;
                    }
                    costPerUnit += def.unitCost * e.ratio;
                    massPerUnit += def.density * e.ratio;
                }
                unitsPerCubicMeter = 1000f / litersPerUnit;
                tonsPerCubicMeter = massPerUnit * unitsPerCubicMeter;
            }
        }

        public string[] getResourceTypes()
        {
            int len = fuelEntries.Count;
            string[] names = new string[len];
            for (int i = 0; i < len; i++) { names[i] = fuelEntries[i].resourceName; }
            return names;
        }

        public List<SSTUFuelEntry> getFuelEntries()
        {
            return fuelEntries;
        }

        public override string ToString()
        {
            return string.Format("[SSTUFuelType: " + name + "]");
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
        public readonly String resourceName;
        public readonly int ratio;
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
        private Dictionary<String, float> resourceMax = new Dictionary<string, float>();

        public void addResourceByVolume(String name, float volume)
        {
            float litersPerUnit = FuelTypes.INSTANCE.getResourceVolume(name);
            addResource(name, (volume * 1000) / litersPerUnit);
        }
        
        public void addResource(String name, float quantity)
        {
            if (resourceMax.ContainsKey(name))
            {
                float val = resourceMax[name];
                val += quantity;
                resourceMax[name] = val;
            }
            else
            {
                resourceMax.Add(name, quantity);
            }
        }

        public void removeResource(String name, float quantity)
        {
            if (resourceMax.ContainsKey(name))
            {
                float val = resourceMax[name];
                val -= quantity;
                if (val <= 0)
                {
                    resourceMax.Remove(name);
                }
                else
                {
                    resourceMax[name] = val;
                }
            }
        }

        public void removeResource(String name)
        {
            resourceMax.Remove(name);
        }

        public void setResourcesToPart(Part part, bool fill)
        {
            int len = part.Resources.Count;
            float amt;
            if (len == resourceMax.Count)//potentially the same resources exist as we are trying to setup
            {
                bool foundAll = true;                         
                foreach (String name in resourceMax.Keys)
                {
                    if (part.Resources.Contains(name))//go ahead and set them as found; if not all are found we'll delete them anyway...
                    {
                        amt = resourceMax[name];
                        PartResource pr = part.Resources[name];
                        pr.maxAmount = amt;
                        pr.amount = fill ? amt : 0;
                    }
                    else
                    {
                        foundAll = false;
                        break;
                    }
                }
                if (foundAll)
                {
                    updatePartResourceGui(part);
                    //TODO update min/max for existing resources
                    return;
                }
            }
            part.Resources.list.Clear();
            PartResource[] resources = part.GetComponents<PartResource>();
            len = resources.Length;
            for (int i = 0; i < len; i++)
            {
                GameObject.Destroy(resources[i]);
            }            
            ConfigNode resourceNode;
            foreach (String name in resourceMax.Keys)
            {
                amt = resourceMax[name];
                resourceNode = new ConfigNode("RESOURCE");
                resourceNode.AddValue("name", name);
                resourceNode.AddValue("maxAmount", amt);
                resourceNode.AddValue("amount", fill ? amt : 0);
                part.AddResource(resourceNode);
            }
            updatePartResourceGui(part);
        }

        private void updatePartResourceGui(Part part)
        {
            if (UIPartActionController.Instance != null && UIPartActionController.Instance.resourcesShown.Count > 0)
            {
                UIPartActionWindow window = UIPartActionController.Instance.GetItem(part);
                if (window != null) { window.displayDirty = true; }
            }
        }
    }

}

