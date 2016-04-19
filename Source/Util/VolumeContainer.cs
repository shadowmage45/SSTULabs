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
        public readonly string name = "Main";// config specified tank name; used to display the name and for ease of MM node patching
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
        public readonly ContainerFuelPreset[] fuelPresets;
        public readonly ContainerModifier[] modifiers;

        private SubContainerDefinition[] subContainerData;
        private Dictionary<string, SubContainerDefinition> subContainersByName = new Dictionary<string, SubContainerDefinition>();

        private ContainerModifier currentModifier;
        private string currentFuelPreset;
        private float currentRawVolume;
        private float currentUsableVolume;
        private string currentModifierName;
        private float currentResourceMass;
        private float currentResourceCost;
        private float currentContainerMass;
        private float currentContainerCost;
        private int currentTotalRatio;
        private bool resourcesDirty = false;

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
            subContainerData = new SubContainerDefinition[len];
            for (int i = 0; i < len; i++)
            {
                subContainerData[i] = new SubContainerDefinition(this, applicableResources[i]);
                subContainersByName.Add(subContainerData[i].name, subContainerData[i]);
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
            currentModifierName = defaultModifier;
            currentModifier = internalGetModifier(currentModifierName);
            currentRawVolume = tankTotalVolume * percentOfTankVolume;
            internalInitializeDefaults();
            MonoBehaviour.print("Loaded container with percent volume of: " + percentOfTankVolume);
        }

        public void load(ConfigNode node)
        {
            currentModifierName = node.GetStringValue("modifier");
            currentFuelPreset = node.GetStringValue("preset");
            currentRawVolume = node.GetFloatValue("volume");
            int len = subContainerData.Length;
            ConfigNode[] dataNodes = node.GetNodes("TANK");
            for (int i = 0; i < len; i++)
            {
                subContainerData[i].load(dataNodes[i]);
            }
        }

        public void save(ConfigNode node)
        {
            node.AddValue("modifier", currentModifierName);
            node.AddValue("preset", currentFuelPreset);
            node.AddValue("volume", currentRawVolume);
            int len = subContainerData.Length;
            ConfigNode saveData;
            for (int i = 0; i < len; i++)
            {
                saveData = new ConfigNode("TANK");
                subContainerData[i].save(saveData);
                node.AddNode(saveData);
            }
        }

        public int totalRatio { get { return currentTotalRatio; } }

        public float containerMass{ get { return currentContainerMass; } }
        
        public float containerCost{ get { return currentContainerCost; } }

        public float resourceMass { get { return currentResourceMass; } }

        public float resourceCost { get { return currentResourceCost; } }

        public int getResourceRatio(string name) { return internalGetVolumeData(name).unitRatio; }

        public float getResourceVolume(string name) { return internalGetVolumeData(name).resourceVolume; }

        public float getResourceUnits(string name) { return internalGetVolumeData(name).resourceUnits; }

        public float rawVolume { get { return currentRawVolume; } }

        public float usableVolume { get { return currentUsableVolume; } }

        public string fuelPreset { get { return currentFuelPreset; } }

        public int Length { get { return subContainerData.Length; } }

        public bool isDirty { get { return resourcesDirty; } }

        public void clearDirty() { resourcesDirty = false; }

        public ContainerModifier getCurrentModifier() { return currentModifier; }
        
        public string[] getResourceNames()
        {
            int len = subContainerData.Length;
            string[] names = new string[len];
            for (int i = 0; i < len; i++)
            {
                names[i] = subContainerData[i].name;
            }
            return names;
        }

        public void getResources(SSTUResourceList list)
        {
            int len = subContainerData.Length;
            for (int i = 0; i < len; i++)
            {
                list.addResource(subContainerData[i].name, subContainerData[i].resourceUnits);
            }
        }

        public void setModifier(ContainerModifier mod)
        {
            currentModifierName = mod.name;
            currentModifier = mod;
            internalUpdateVolumeUnits();
            internalUpdateMassAndCost();
            resourcesDirty = true;
        }

        public void setResourceRatio(string name, int newRatio)
        {
            internalGetVolumeData(name).setRatio(newRatio);
            MonoBehaviour.print("Updated ratio for: " + name + " to: " + newRatio);
            internalUpdateTotalRatio();
            internalUpdateVolumeUnits();
            internalUpdateMassAndCost();
            resourcesDirty = true;
        }

        public void setContainerVolume(float partRawVolume)
        {
            currentRawVolume = partRawVolume * percentOfTankVolume;
            internalUpdateVolumeUnits();
            internalUpdateMassAndCost();
            resourcesDirty = true;
            MonoBehaviour.print("updated current volume: " + currentUsableVolume + " from raw volume: " + partRawVolume);
        }

        /// <summary>
        /// Zeroes any current configuration and sets the tank up for the input fuel preset<para/>
        /// Intended to be used by the 'Next Fuel Type' buttons on the base part GUI
        /// </summary>
        /// <param name="preset"></param>
        public void setFuelPreset(ContainerFuelPreset preset)
        {
            currentFuelPreset = preset.name;
            internalClearRatios();
            addPresetRatios(preset);
        }

        /// <summary>
        /// -ADDS- the input preset to the current ratios for the tank without adjusting any other resource ratios
        /// </summary>
        /// <param name="preset"></param>
        public void addPresetRatios(ContainerFuelPreset preset)
        {
            int len = preset.resourceRatios.Length;
            ContainerResourceRatio ratio;
            for (int i = 0; i < len; i++)
            {
                ratio = preset.resourceRatios[i];
                internalGetVolumeData(ratio.resourceName).addRatio(ratio.resourceRatio);
            }
            internalUpdateTotalRatio();
            internalUpdateVolumeUnits();
            internalUpdateMassAndCost();
            resourcesDirty = true;
        }

        private void internalUpdateMassAndCost()
        {
            MonoBehaviour.print("internalUpdateMassAndCost");
            currentResourceMass = 0;
            int len = subContainerData.Length;
            for (int i = 0; i < len; i++)
            {
                currentResourceMass += subContainerData[i].resourceMass;
            }
                        
            currentContainerMass = 0;
            float tempMass;
            if (currentModifier.useVolumeForMass)//should most notably be used for structural tank types; any resource-containing tank should use the max resource mass * mass fraction
            {
                tempMass = currentUsableVolume;
            }
            else
            {
                tempMass = currentResourceMass;
            }
            currentContainerMass = tempMass * currentModifier.dryMassModifier * tankageMass;

            currentResourceCost = 0;
            for (int i = 0; i < len; i++)
            {
                currentResourceCost += subContainerData[i].resourceCost;
            }
            currentContainerCost = (costPerDryTon * currentModifier.costModifier) * currentResourceCost;
        }

        private void internalUpdateVolumeUnits()
        {
            MonoBehaviour.print("internalUpdateVolumeUnits!");
            ContainerModifier mod = internalGetModifier(currentModifierName);
            currentUsableVolume = (currentRawVolume - (currentRawVolume * tankageVolume)) * mod.volumeModifier;
            MonoBehaviour.print("Usable volume: " + currentUsableVolume);
            int total = currentTotalRatio;
            MonoBehaviour.print("total ratio: " + currentTotalRatio);
            int len = subContainerData.Length;
            float vol=0;
            for (int i = 0; i < len; i++)
            {
                if (total > 0)
                {
                    vol = (float)subContainerData[i].unitRatio / (float)total;
                    vol *= currentUsableVolume;                    
                }
                subContainerData[i].setVolume(vol);
                MonoBehaviour.print("setting volume for: " + subContainerData[i].name + "  to: " + vol);
            }
        }

        private void internalUpdateTotalRatio()
        {
            currentTotalRatio = 0;
            int len = subContainerData.Length;
            for (int i = 0; i < len; i++) { currentTotalRatio += subContainerData[i].unitRatio; }
            MonoBehaviour.print("internalUpdateTotalRatio :: "+currentTotalRatio);
        }

        /// <summary>
        /// UNSAFE; MUST MANUALLY UPDATE AFTER CALLING THIS METHOD...
        /// </summary>
        private void internalClearRatios()
        {
            MonoBehaviour.print("internalClearRatios");
            int len = subContainerData.Length;
            for (int i = 0; i < len; i++) { subContainerData[i].setRatio(0); }
        }

        private void internalInitializeDefaults()
        {
            MonoBehaviour.print("internalInitializeDefaults");
            if (!string.IsNullOrEmpty(defaultFuelPreset))
            {
                currentFuelPreset = defaultFuelPreset;
                setFuelPreset(internalGetFuelPreset(currentFuelPreset));
            }
            else//use default resource
            {
                internalClearRatios();
                currentFuelPreset = "custom";
                internalGetVolumeData(defaultResource).setRatio(1);
                internalUpdateTotalRatio();
                internalUpdateVolumeUnits();
                internalUpdateMassAndCost();
            }
            resourcesDirty = true;
        }

        private SubContainerDefinition internalGetVolumeData(string resourceName) { return subContainersByName[resourceName]; }

        private ContainerFuelPreset internalGetFuelPreset(string name) { return Array.Find(fuelPresets, m => m.name == name); }

        private ContainerModifier internalGetModifier(string name) { return Array.Find(modifiers, m => m.name == name); }

    }

    /// <summary>
    /// Persistent data storage class for a single resource for a single container
    /// </summary>
    public class SubContainerDefinition
    {
        private readonly ContainerDefinition container;
        public readonly string name;//resource name
        private readonly PartResourceDefinition def;//resource definition
        private int ratio;//dimensionless ratio value        
        private float volume;//actual volume computed for this resource container from the total ratio

        public SubContainerDefinition(ContainerDefinition container, String name)
        {
            this.container = container;
            this.name = name;
            if (name != "structural")
            {
                def = PartResourceLibrary.Instance.resourceDefinitions[name];
                if (def == null) { throw new NullReferenceException("Resource definition was null for name: " + name); }
            }
        }

        public float resourceMass { get { return resourceUnits * unitMass; } }

        public float resourceCost { get { return resourceUnits * unitCost; } }

        public float resourceVolume { get { return volume; } }

        public float resourceUnits { get { return volume / unitVolume; } }

        public int unitRatio { get { return ratio; } }

        public float unitVolume { get { return def == null ? 1 : def.volume; } }

        public float unitMass { get { return def == null ? 0.001f : def.density; } }

        public float unitCost { get { return def == null ? 100 : def.unitCost; } }

        public void load(ConfigNode node)
        {
            ratio = node.GetIntValue("ratio");
        }

        public void save(ConfigNode node)
        {
            node.AddValue("ratio", ratio);
        }

        public void addRatio(int addRatio)
        {
            setRatio(ratio + addRatio);
        }

        public void setVolume(float volume)
        {
            this.volume = volume;
            MonoBehaviour.print("set container to volume: " + volume + " for resource: " + name);
        }

        public void setRatio(int ratio)
        {
            this.ratio = ratio;
            if (ratio == 0) { volume = 0; }
            MonoBehaviour.print("set container ratio to: " + ratio + " for resource: " + name);
        }

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
