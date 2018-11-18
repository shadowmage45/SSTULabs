using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SSTUTools
{

    public class FuelTypes
    {
        public static readonly FuelTypes INSTANCE = new FuelTypes();
        private Dictionary<string, float> resourceVolumes = new Dictionary<string, float>();
        private Dictionary<string, float> zeroMassResourceMasses = new Dictionary<string, float>();
        private Dictionary<string, float> zeroCostResourceCosts = new Dictionary<string, float>();
        private Dictionary<string, BoiloffData> boiloffResourceValues = new Dictionary<string, BoiloffData>();

        public void loadConfigData()
        {
            loadDefs();
        }

        private void loadDefs()
        {
            resourceVolumes.Clear();
            zeroMassResourceMasses.Clear();
            zeroCostResourceCosts.Clear();
            boiloffResourceValues.Clear();

            ConfigNode[] configs = GameDatabase.Instance.GetConfigNodes("SSTU_RESOURCEVOLUME");
            string name;
            foreach (ConfigNode node in configs)
            {
                name = node.GetStringValue("name");
                if (resourceVolumes.ContainsKey(name))
                {
                    MonoBehaviour.print("ERROR: Found duplicate resource volume definition for: " + name);
                    continue;
                }
                resourceVolumes.Add(name, node.GetFloatValue("volume"));
            }

            configs = GameDatabase.Instance.GetConfigNodes("SSTU_ZEROMASSRESOURCE");
            foreach (ConfigNode node in configs)
            {
                name = node.GetStringValue("name");
                if (zeroMassResourceMasses.ContainsKey(name))
                {
                    MonoBehaviour.print("ERROR: Found duplicate zero mass resource mass definition for: " + name);
                    continue;
                }
                zeroMassResourceMasses.Add(name, node.GetFloatValue("mass"));
            }

            configs = GameDatabase.Instance.GetConfigNodes("SSTU_ZEROCOSTRESOURCE");
            foreach (ConfigNode node in configs)
            {
                name = node.GetStringValue("name");
                if (zeroCostResourceCosts.ContainsKey(name))
                {
                    MonoBehaviour.print("ERROR: Found duplicate zero volume resource volume definition for: " + name);
                    continue;
                }
                zeroCostResourceCosts.Add(name, node.GetFloatValue("cost"));
            }

            configs = GameDatabase.Instance.GetConfigNodes("SSTU_RESOURCEBOILOFF");
            foreach (ConfigNode node in configs)
            {
                name = node.GetStringValue("name");
                float val = node.GetFloatValue("value");
                float cost = node.GetFloatValue("cost");
                if (boiloffResourceValues.ContainsKey(name))
                {
                    MonoBehaviour.print("ERROR: Found duplicate resource boiloff definition for: " + name);
                    continue;
                }
                boiloffResourceValues.Add(name, new BoiloffData(name, val, cost));
            }
        }

        public float getResourceVolume(String name)
        {
            return getResourceVolume(name, PartResourceLibrary.Instance.GetDefinition(name));
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

    /// <summary>
    /// Will be the base class for manipulating resources in a part, including cross-module manipulation.
    /// </summary>
    public class SSTUResourceList
    {
        private Dictionary<string, ResourceListEntry> resourceList = new Dictionary<string, ResourceListEntry>();

        /// <summary>
        /// Add a resource, by volume, setting/adjusting fill and max units by the volume specified
        /// </summary>
        /// <param name="name"></param>
        /// <param name="cubicMetersTotalFilled"></param>
        public void addResourceByVolume(String name, float cubicMetersTotalFilled)
        {
            addResourceByVolume(name, cubicMetersTotalFilled, cubicMetersTotalFilled);
        }

        /// <summary>
        /// Add a resource, by volume, setting/adjusting fill and max units by the volumes specified
        /// </summary>
        /// <param name="name"></param>
        /// <param name="cubicMetersFilled"></param>
        /// <param name="totalCubicMeters"></param>
        public void addResourceByVolume(string name, float cubicMetersFilled, float totalCubicMeters)
        {
            float litersPerUnit = FuelTypes.INSTANCE.getResourceVolume(name);
            float unitsFilled = (cubicMetersFilled * 1000) / litersPerUnit;
            float unitsMax = (totalCubicMeters * 1000) / litersPerUnit;
            addResource(name, unitsFilled, unitsMax);
        }

        /// <summary>
        /// Add a resource, by units, setting/adjusting fill and max units by the units specified
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fillMaxUnits"></param>
        public void addResource(String name, float fillMaxUnits)
        {
            addResource(name, fillMaxUnits, fillMaxUnits);
        }

        /// <summary>
        /// Add a resource, by units, setting/adjusting fill and max units by the units specified
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fillMaxUnits"></param>
        public void addResource(string name, float fill, float max)
        {
            if (resourceList.ContainsKey(name))
            {
                ResourceListEntry entry = resourceList[name];
                entry.max += max;
                entry.fill += fill;
                if (entry.fill > entry.max) { entry.fill = entry.max; }
            }
            else
            {
                if (fill > max) { fill = max; }
                ResourceListEntry entry = new ResourceListEntry(name, fill, max);
                resourceList.Add(entry.name, entry);
            }
        }

        /// <summary>
        /// Remove a resource from the list entirely
        /// </summary>
        /// <param name="name"></param>
        public void removeResource(String name)
        {
            resourceList.Remove(name);
        }

        /// <summary>
        /// Remove a specified number of units from the resource maximum; if fill>max after this, fill will be set to max.  If max==0, resource will be removed from list entirely
        /// </summary>
        /// <param name="name"></param>
        /// <param name="removeFromMax"></param>
        public void removeResource(String name, float removeFromMax)
        {
            removeResource(name, 0, removeFromMax);
        }

        /// <summary>
        /// Remove a specified number of units from the resource fill and maximum values; if fill>max after this, fill will be set to max.  If max==0, resource will be removed from list entirely
        /// </summary>
        /// <param name="name"></param>
        /// <param name="removeFromMax"></param>
        public void removeResource(String name, float removeFromFill, float removeFromMax)
        {
            if (resourceList.ContainsKey(name))
            {
                ResourceListEntry entry = resourceList[name];
                entry.fill -= removeFromFill;
                entry.max -= removeFromMax;
                if (entry.fill > entry.max)
                {
                    entry.fill = entry.max;
                }
                if (entry.fill < 0)
                {
                    entry.fill = 0;
                }
                if (entry.max <= 0)
                {
                    removeResource(name);
                }
            }
        }

        /// <summary>
        /// Blindly set the quanity of a resource.  Adds new resource if not present, removes resource if present and max==0.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fill"></param>
        /// <param name="max"></param>
        public void setResource(string name, float fill, float max)
        {
            if (max == 0) { removeResource(name); return; }
            if (fill > max) { fill = max; }
            if (resourceList.ContainsKey(name))
            {
                ResourceListEntry entry = resourceList[name];
                entry.fill = fill;
                entry.max = max;
            }
            else
            {
                ResourceListEntry entry = new ResourceListEntry(name, fill, max);
                resourceList.Add(entry.name, entry);
            }
        }

        /// <summary>
        /// Actually set the resources from this list to the input part; if the current part resources match this list exactly they will be updated in-place,
        /// else all resources from the part will be cleared and the new list of resources added.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="fill"></param>
        public void setResourcesToPart(Part part, float modifier, bool keepExisting)
        {
            removeUnusedResources(part);
            int len = resourceList.Count;
            foreach(ResourceListEntry rle in resourceList.Values)
            {
                rle.applyToPart(part, modifier, keepExisting);
            }
            SSTUModInterop.updatePartResourceDisplay(part);
            //GameEvents.onPartResourceListChange.Fire(part);
        }

        private void removeUnusedResources(Part part)
        {
            int len = part.Resources.Count;
            PartResource pr;
            for (int i = len-1; i >=0; i--)
            {
                pr = part.Resources[i];
                if (!contains(pr))
                {
                    part.Resources.Remove(pr);
                }
            }
        }

        private bool contains(PartResource pr)
        {
            return resourceList.Keys.Contains(pr.resourceName);
        }

        public static void partResourceDebug(Part part)
        {
            int len = part.Resources.Count;
            for (int i = 0; i < len; i++)
            {
                MonoBehaviour.print(part.Resources[i].resourceName + " : " + part.Resources[i].amount+"/"+part.Resources[i].maxAmount);
            }
        }

    }

    public class ResourceListEntry
    {
        public readonly string name;
        public float fill;
        public float max;
        public ResourceListEntry(string name, float fill, float max)
        {
            this.name = name;
            this.fill = fill;
            this.max = max;
        }

        public void applyToPart(Part part, float modifier, bool keepExistingAmount)
        {
            PartResource pr = part.Resources[name];
            if (pr != null)
            {
                pr.maxAmount = max * modifier;
                if (keepExistingAmount)
                {
                    pr.amount = Math.Min(pr.amount, pr.maxAmount);
                }
                else
                {
                    pr.amount = fill * modifier;
                }                
            }
            else
            {
                ConfigNode resourceNode;
                resourceNode = new ConfigNode("RESOURCE");
                resourceNode.AddValue("name", name);
                resourceNode.AddValue("maxAmount", max * modifier);
                resourceNode.AddValue("amount", fill * modifier);
                pr = part.AddResource(resourceNode);
            }

            //handle stock delta-v simulation resource setup
            //SR resource code adapted from B9 code by @blowfishpro
            //https://github.com/blowfishpro/B9PartSwitch/pull/110/files
            PartResource sr = part.SimulationResources[name];
            if (sr != null)
            {

                sr.maxAmount = max * modifier;
                if (keepExistingAmount)
                {
                    sr.amount = Math.Min(pr.amount, pr.maxAmount);
                }
                else
                {
                    sr.amount = fill * modifier;
                }
            }
            else
            {
                sr = new PartResource(pr);
                sr.simulationResource = true;
                part.SimulationResources.dict.Add(name.GetHashCode(), sr);
            }
        }

        public bool equals(string resource) { return name == resource; }

        public override string ToString()
        {
            return "ResourceListEntry: " + name + "-" + fill + "/" + max;
        }

    }

}

