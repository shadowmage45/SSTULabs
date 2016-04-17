using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{ 
    /// <summary>
    /// Container definition type that determines what resources may be loaded into a given part, what container types are usable, and what fuel types are available; 
    /// multiple containers may be used on a part to segregate the available storage types;
    /// </summary>
    public class ContainerDefinition
    {
        public readonly string name = "custom";// user config specified tank name; used to display the name and for ease of MM node patching
        public readonly string[] availableResources;// user config specified resources
        public readonly string[] resourceSets;// user config specified resource sets
        public readonly string[] tankModifierNames;// user config specified tank mods
        public readonly float percentOfTankVolume;// user config specified percent of total volume to use for this container
        public readonly float tankageVolume;// percent of volume lost to tankage
        public readonly float tankageMass;// percent of resource mass or volume to compute as dry mass
        public readonly float costPerDryTon;// default cost per dry ton for this tank; modified by the tank modifier
        public readonly string defaultFuelPreset;// user config specified default fuel preset
        public readonly string defaultResource;
        public readonly string defaultModifier;// the default tank modifier; set to first modifier if it is blank or invalid

        public readonly string[] applicableResources;
        public readonly ContainerVolumeData[] tankData;
        public readonly ContainerModifier[] modifiers;
        public readonly ContainerFuelPreset[] fuelPresets;

        public string currentModifier;
        public string currentFuelPreset;
        public float currentRawVolume;
        public float currentUsableVolume;

        private float currentContainerMass = -1;
        private float currentContainerCost = -1;

        public ContainerDefinition(ConfigNode node, float tankTotalVolume)
        {
            name = node.GetStringValue("name", name);
            availableResources = node.GetStringValues("resource");
            resourceSets = node.GetStringValues("resourceSet");
            tankModifierNames = node.GetStringValues("modifier");
            percentOfTankVolume = node.GetFloatValue("percent", 1);
            tankageVolume = node.GetFloatValue("tankageVolume");
            tankageMass = node.GetFloatValue("tankageMass");
            defaultFuelPreset = node.GetStringValue("defaultFuelPreset");
            defaultResource = node.GetStringValue("defaultResource");
            defaultModifier = node.GetStringValue("defaultModifier", "standard");

            if (availableResources.Length == 0 && resourceSets.Length == 0) { resourceSets = new string[] { "generic" }; }//validate that there is some sort of resource reference; generic is a special type for all pumpable resources
            if (tankModifierNames == null || tankModifierNames.Length == 0) { tankModifierNames = VolumeContainerLoader.getAllModifierNames(); }//validate that there is at least one modifier type            
            if (percentOfTankVolume > 1) { percentOfTankVolume *= 0.01f; }
            
            //load available container modifiers
            modifiers = VolumeContainerLoader.getModifiersByName(tankModifierNames);

            //setup applicable resources
            List<string> resourceNames = new List<string>();
            resourceNames.AddRange(availableResources);
            int len = resourceSets.Length;
            int resLen;
            ContainerResourceSet set;
            for (int i = 0; i < len; i++)
            {
                set = VolumeContainerLoader.getResourceSet(resourceSets[i]);
                if (set == null) { continue; }
                resLen = set.availableResources.Length;
                for (int k = 0; k < resLen; k++)
                {
                    resourceNames.AddUnique(set.availableResources[k]);
                }
            }
            resourceNames.Sort();//basic alpha sort...
            applicableResources = resourceNames.ToArray();

            if (string.IsNullOrEmpty(defaultFuelPreset) && string.IsNullOrEmpty(defaultResource) && applicableResources.Length > 0) { defaultResource = applicableResources[0]; }

            //setup volume data
            len = applicableResources.Length;
            tankData = new ContainerVolumeData[len];
            for (int i = 0; i < len; i++)
            {
                tankData[i] = new ContainerVolumeData(applicableResources[i]);
            }

            //setup preset data
            List<ContainerFuelPreset> usablePresets = new List<ContainerFuelPreset>();
            ContainerFuelPreset[] presets = VolumeContainerLoader.getPresets();
            len = presets.Length;
            for (int i = 0; i < len; i++)
            {
                if (presets[i].applicable(applicableResources)) { usablePresets.Add(presets[i]); }
            }
            fuelPresets = usablePresets.ToArray();
            currentModifier = defaultModifier;
            updateVolume(tankTotalVolume);
            initializeDefaults();
            MonoBehaviour.print("Loaded container with percent volume of: " + percentOfTankVolume);
        }

        /// <summary>
        /// return cached mass value for the dry tank; update first if currently invalid
        /// </summary>
        public float currentMass
        {
            get
            {
                if (currentContainerMass == -1) { updateContainerMass(); }
                return currentContainerMass;
            }
        }

        /// <summary>
        /// return cached cost value for the dry tank; update first if currently invalid
        /// </summary>
        public float currentCost
        {
            get
            {
                if (currentContainerCost == -1) { updateContainerCost(); }
                return currentContainerCost;
            }
        }

        public void load(ConfigNode node)
        {
            currentModifier = node.GetStringValue("modifier");
            currentFuelPreset = node.GetStringValue("preset");
            currentRawVolume = node.GetFloatValue("volume");
            int len = tankData.Length;
            ConfigNode[] dataNodes = node.GetNodes("TANK");
            for (int i = 0; i < len; i++)
            {
                tankData[i].load(dataNodes[i]);
            }
        }

        public void save(ConfigNode node)
        {
            node.AddValue("modifier", currentModifier);
            node.AddValue("preset", currentFuelPreset);
            node.AddValue("volume", currentRawVolume);
            int len = tankData.Length;
            ConfigNode saveData;
            for (int i = 0; i < len; i++)
            {
                saveData = new ConfigNode("TANK");
                tankData[i].save(saveData);
                node.AddNode(saveData);
            }
        }

        public void updateVolume(float tankRawTotal)
        {
            ContainerModifier mod = getModifier(currentModifier);
            currentRawVolume = tankRawTotal * percentOfTankVolume;            
            currentUsableVolume = (currentRawVolume - (currentRawVolume * tankageVolume)) * mod.volumeModifier;
            MonoBehaviour.print("updated current volume: " + currentUsableVolume + " from raw volume: " + tankRawTotal);
            updateTankVolumes();
        }

        public void updateTankParameters()
        {
            updateTankVolumes();
            updateContainerMass();
            updateContainerCost();
        }

        /// <summary>
        /// Reset this container to its default resource ratios from the default fuel type specified in its config
        /// </summary>
        public void resetTankResources()
        {
            clearRatios();
            initializeDefaults();
        }

        /// <summary>
        /// Zeroes any current configuration and sets the tank up for the input fuel preset<para/>
        /// Intended to be used by the 'Next Fuel Type' buttons on the base part GUI
        /// </summary>
        /// <param name="preset"></param>
        public void setPresetRatio(ContainerFuelPreset preset)
        {
            currentFuelPreset = preset.name;
            clearRatios();
            addPresetRatios(preset);
        }

        /// <summary>
        /// -ADDS- the input preset to the current ratios for the tank without adjusting any other resource ratios
        /// </summary>
        /// <param name="preset"></param>
        public void addPresetRatios(ContainerFuelPreset preset)
        {
            int len = preset.resourceRatios.Length;
            ContainerVolumeData tank;
            ContainerResourceRatio ratio;
            for (int i = 0; i < len; i++)
            {
                ratio = preset.resourceRatios[i];
                tank = getVolumeData(ratio.resourceName);
                tank.setRatio(tank.ratio + ratio.resourceRatio);       
            }
            updateTankParameters();
        }

        public void addResources(SSTUResourceList list)
        {
            int len = tankData.Length;
            for (int i = 0; i < len; i++)
            {
                list.addResource(tankData[i].name, tankData[i].units);
            }
        }

        private void updateContainerCost()
        {
            float cost = 0;
            int len = tankData.Length;
            for (int i = 0; i < len; i++)
            {
                cost += tankData[i].getResourceCost();
            }
            ContainerModifier mod = getModifier(currentModifier);
            cost += (costPerDryTon * mod.costModifier) * currentContainerMass;
            currentContainerCost = cost;
        }

        private void updateContainerMass()
        {
            float mass = 0;
            ContainerModifier mod = getModifier(currentModifier);
            if (mod.useVolumeForMass)//should most notably be used for structural tank types; any resource-containing tank should use the max resource mass * mass fraction
            {
                mass = currentUsableVolume;
            }
            else
            {
                mass = getResourceMass();
            }
            mass = mass * mod.dryMassModifier * tankageMass;
            currentContainerMass = mass;
        }

        private float getResourceMass()
        {
            float mass = 0;
            int len = tankData.Length;
            for (int i = 0; i < len; i++)
            {
                mass += tankData[i].getResourceMass();
            }
            return mass;
        }

        private void updateTankVolumes()
        {
            float total = tankData.Sum(m => m.ratio);
            if (total == 0)
            {
                int len = tankData.Length;
                for (int i = 0; i < len; i++)
                {
                    tankData[i].volume = 0;
                    tankData[i].units = 0;
                }
            }
            else
            {
                int len = tankData.Length;
                for (int i = 0; i < len; i++)
                {
                    tankData[i].volume = ((float)tankData[i].ratio / total) * currentUsableVolume;
                    tankData[i].units = tankData[i].volume / tankData[i].def.volume;
                }
            }
        }

        private void initializeDefaults()
        {
            if (!string.IsNullOrEmpty(defaultFuelPreset))
            {
                addPresetRatios(getPreset(defaultFuelPreset));
            }
            else//use default resource
            {
                Array.Find(tankData, m => m.name == defaultResource).ratio = 1;
                updateTankParameters();
            }
        }

        private void clearRatios()
        {
            int len = tankData.Length;
            for (int i = 0; i < len; i++) { tankData[i].ratio = 0; }
        }

        private ContainerVolumeData getVolumeData(string resourceName) { return Array.Find(tankData, m => m.name == resourceName); }

        private ContainerFuelPreset getPreset(string name) { return Array.Find(fuelPresets, m => m.name == name); }

        private ContainerModifier getModifier(string name) { return Array.Find(modifiers, m => m.name == name); }

    }

    /// <summary>
    /// Read-only data class for a set of modifiers to a tank based on different types of containers;
    /// </summary>
    public class ContainerModifier
    {
        public readonly string name;
        public readonly string title;
        public readonly string description;
        public readonly float volumeModifier = 0.85f;//applied before any resource volumes are calculated
        public readonly float dryMassModifier = 0.15f;//applied after dry mass for tank is tallied from resource mass fraction values
        public readonly float costModifier = 1f;
        public readonly float impactModifier = 1f;
        public readonly float heatModifier = 1f;
        public readonly float boiloffModifier = 1f;//in case of a 'semi' insulated container type this may be any value from 0-1
        public readonly float boiloffECConsumption = 1f;//modifier to the amount of EC needed to prevent boiloff
        public readonly bool useVolumeForMass = false;//special flag for structural tank type, to denote that dry mass is derived from raw volume rather than resource mass
        public ContainerModifier(ConfigNode node)
        {
            name = node.GetStringValue("name");
            title = node.GetStringValue("title");
            description = node.GetStringValue("description");
            volumeModifier = node.GetFloatValue("volumeModifier", volumeModifier);
            dryMassModifier = node.GetFloatValue("massModifier", dryMassModifier);
            costModifier = node.GetFloatValue("costModifier", costModifier);
            impactModifier = node.GetFloatValue("impactModifier", impactModifier);
            heatModifier = node.GetFloatValue("heatModifier", heatModifier);
            boiloffModifier = node.GetFloatValue("boiloffModifier", boiloffModifier);
            boiloffECConsumption = node.GetFloatValue("boiloffECModifier", boiloffECConsumption);
            useVolumeForMass = node.GetBoolValue("useVolumeForMass", useVolumeForMass);
        }
    }
    
    /// <summary>
    /// Persistent data storage class for a single resource for a single container
    /// </summary>
    public class ContainerVolumeData
    {
        public readonly string name;//resource name
        public readonly PartResourceDefinition def;//resource definition
        public int ratio;//dimensionless ratio value        
        public float volume;//actual volume computed for this resource container from the total ratio
        public float units;//units of resource for current volume

        public ContainerVolumeData(String name)
        {
            this.name = name;
            def = PartResourceLibrary.Instance.resourceDefinitions[name];
            MonoBehaviour.print("loaded volume container for resource: " + name + " :: " + def);
            if (def == null) { throw new NullReferenceException("Resource definition was null for name: " + name); }
        }

        public void load(ConfigNode node)
        {
            ratio = node.GetIntValue("ratio");
        }

        public void save(ConfigNode node)
        {
            node.AddValue("ratio", ratio);
        }

        public float getResourceCost()
        {
            if (units <= 0) { updateUnits(volume); }
            return units * def.unitCost;
        }

        public float getResourceMass()
        {
            if (units <= 0) { updateUnits(volume); }
            return units * def.density;
        }

        public void updateUnits(float volume)
        {
            float unitVolume = def.volume;
            units = volume / unitVolume;
        }

        public void setRatio(int newRatio)
        {
            MonoBehaviour.print("updating ratio for: " + name + " to :" + newRatio);
            ratio = newRatio;
        }
    }

    /// <summary>
    /// Simple wrapper around a set of resources to simplify the setup of custom container types for often-used groups of resources.
    /// </summary>
    public class ContainerResourceSet
    {
        public readonly string name;
        public readonly string[] availableResources;
        public readonly bool generic;//special flag for all pumpable resources
        public ContainerResourceSet(ConfigNode node)
        {
            name = node.GetStringValue("name");
            availableResources = node.GetStringValues("resource");
            generic = node.GetBoolValue("generic");
        }
    }

    /// <summary>
    /// Read-only data regarding a single fuel preset type.  A container may have multiple applicable preset types.<para/>
    /// These are used as a user-convenience setup option to simplify resource setup for often used fuel mixes.
    /// </summary>
    public class ContainerFuelPreset
    {
        public readonly string name;
        public readonly ContainerResourceRatio[] resourceRatios;
        public ContainerFuelPreset(ConfigNode node)
        {
            name = node.GetStringValue("name");
            ConfigNode[] ratioNodes = node.GetNodes("RESOURCE");
            int len = ratioNodes.Length;
            resourceRatios = new ContainerResourceRatio[len];
            for (int i = 0; i < len; i++)
            {
                resourceRatios[i] = new ContainerResourceRatio(ratioNodes[i]);
            }
        }

        /// <summary>
        /// Return true if this ContainerFuelPreset is applicable for the input list of resource names<para/>
        /// Return false if this fuel preset contains any resources that are not present in the input list of resource names<para/>
        /// Return false if this fuel preset contains no resource entries (empty/structural)
        /// </summary>
        /// <param name="availableResources"></param>
        /// <returns></returns>
        public bool applicable(string[] availableResources)
        {
            if (resourceRatios.Length == 0) { return false; }//structural, no preset button...
            bool valid = true;
            int len = resourceRatios.Length;
            for (int i = 0; i < len; i++)
            {
                if (!availableResources.Contains(resourceRatios[i].resourceName))
                {
                    valid = false;
                    break;
                }
            }
            return valid;
        }
    }

    /// <summary>
    /// Read-only data class for a ContainerFuelPreset ratio data for a single resource
    /// </summary>
    public class ContainerResourceRatio
    {
        public readonly string resourceName;
        public readonly int resourceRatio;
        public ContainerResourceRatio(ConfigNode node)
        {
            resourceName = node.GetStringValue("resource");
            resourceRatio = node.GetIntValue("ratio", 1);
        }
    }

    /// <summary>
    /// static class for utility methods related to loading container and resource data for volume-based containers
    /// </summary>
    public static class VolumeContainerLoader
    {
        private static ContainerModifier[] containerModifiers;
        private static ContainerResourceSet[] containerDefs;
        private static ContainerFuelPreset[] containerPresets;

        public static void loadConfigs()
        {
            ConfigNode[] typeNodes = GameDatabase.Instance.GetConfigNodes("SSTU_CONTAINERTYPE");
            int len = typeNodes.Length;
            containerModifiers = new ContainerModifier[len];
            for (int i = 0; i < len; i++) { containerModifiers[i] = new ContainerModifier(typeNodes[i]); }

            ConfigNode[] defNodes = GameDatabase.Instance.GetConfigNodes("SSTU_RESOURCESET");
            len = defNodes.Length;
            containerDefs = new ContainerResourceSet[len];
            for (int i = 0; i < len; i++) { containerDefs[i] = new ContainerResourceSet(defNodes[i]); }

            ConfigNode[] presetNodes = GameDatabase.Instance.GetConfigNodes("SSTU_FUELTYPE");
            len = presetNodes.Length;
            containerPresets = new ContainerFuelPreset[len];
            for (int i = 0; i < len; i++) { containerPresets[i] = new ContainerFuelPreset(presetNodes[i]); }
        }

        public static string[] getAllModifierNames()
        {
            int len = containerModifiers.Length;
            string[] names = new string[len];
            for (int i = 0; i < len; i++) { names[i] = containerModifiers[i].name; }
            return names;
        }

        public static ContainerModifier[] getModifiersByName(string[] names)
        {
            List<ContainerModifier> mods = new List<ContainerModifier>();
            int len = containerModifiers.Length;
            for (int i = 0; i < len; i++)
            {
                if (names.Contains(containerModifiers[i].name))
                {
                    mods.Add(containerModifiers[i]);
                }
            }
            return mods.ToArray();
        }

        public static ContainerModifier[] getConainerTypes() { return containerModifiers; }

        public static ContainerModifier getContainerType(String name) { return Array.Find(containerModifiers, m => m.name == name); }

        public static ContainerResourceSet getResourceSet(String name) { return Array.Find(containerDefs, m => m.name == name); }

        public static ContainerFuelPreset[] getPresets() { return containerPresets; }

        public static ContainerFuelPreset getPreset(String name) { return Array.Find(containerPresets, m => m.name == name); }

    }
}
