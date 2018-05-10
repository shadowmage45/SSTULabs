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
        //Config fields - loaded from config node; arrays are loaded as key=value pairs
        public readonly string name = "Main";// config specified tank name; used to display the name and for ease of MM node patching
        public readonly string[] availableResources;// user config specified resources; resource=XXX
        public readonly string[] resourceSets;// user config specified resource sets; resourceSet=XXX
        public readonly string[] tankModifierNames;// user config specified tank mods; modifer=XXX
        public readonly float tankageVolume;// percent of volume lost to tankage
        public readonly float tankageMass;// percent of resource mass or volume to compute as dry mass
        public readonly float costPerDryTon;// default cost per dry ton for this tank; modified by the tank modifier
        public readonly float massPerEmptyCubicMeter;// how much does this tank weigh per m^3 when it is empty or for unused volume (not yet supported)
        public readonly string defaultFuelPreset;// user config specified default fuel preset
        public readonly string defaultResources;//CSV resource,ratio,resource,ratio...
        public readonly string defaultModifier;// the default tank modifier; set to first modifier if it is blank or invalid
        public readonly bool ecHasMass = true;
        public readonly bool ecHasCost = true;
        public readonly bool guiAvailable = true;
        public readonly bool useStaticVolume = false; //if the config volume should always be included regardless of external module manipulation
        public readonly float configVolume; // base/static volume from the original config
        public readonly SSTUVolumeContainer module;
        //runtime accessible data
        public readonly string[] applicableResources;
        public readonly ContainerFuelPreset[] fuelPresets;
        public readonly ContainerModifier[] modifiers;

        //private vars
        private SubContainerDefinition[] subContainerData;
        private Dictionary<string, SubContainerDefinition> subContainersByName = new Dictionary<string, SubContainerDefinition>();
        
        //cached values
        private ContainerModifier cachedModifier;
        private string currentFuelPreset;
        private float volume; // volume of this container; may be adjusted by other modules
        private float currentUsableVolume;
        private string currentModifierName;
        private float currentResourceMass;
        private float currentResourceCost;
        private float currentContainerMass;
        private float currentContainerCost;
        private int currentTotalUnitRatio;
        private float currentTotalVolumeRatio;
        private bool resourcesDirty = false;

        public ContainerDefinition(SSTUVolumeContainer module, ConfigNode node)
        {
            this.module = module;
            name = node.GetStringValue("name", name);
            availableResources = node.GetStringValues("resource");
            resourceSets = node.GetStringValues("resourceSet");
            tankModifierNames = node.GetStringValues("modifier");
            configVolume = volume = node.GetFloatValue("volume", 0);
            tankageVolume = node.GetFloatValue("tankageVolume");
            tankageMass = node.GetFloatValue("tankageMass");
            costPerDryTon = node.GetFloatValue("dryCost", 700f);
            massPerEmptyCubicMeter = node.GetFloatValue("emptyMass", 0.05f);
            defaultFuelPreset = node.GetStringValue("defaultFuelPreset");
            defaultResources = node.GetStringValue("defaultResources");
            defaultModifier = node.GetStringValue("defaultModifier", "standard");
            ecHasMass = node.GetBoolValue("ecHasMass", ecHasMass);
            ecHasCost = node.GetBoolValue("ecHasCost", ecHasCost);
            guiAvailable = node.GetBoolValue("guiAvailable", guiAvailable);
            useStaticVolume = node.GetBoolValue("useStaticVolume", useStaticVolume);

            if (availableResources.Length == 0 && resourceSets.Length == 0) { resourceSets = new string[] { "generic" }; }//validate that there is some sort of resource reference; generic is a special type for all pumpable resources
            if (tankModifierNames == null || tankModifierNames.Length == 0) { tankModifierNames = VolumeContainerLoader.getAllModifierNames(); }//validate that there is at least one modifier type            
            
            //load available container modifiers
            modifiers = VolumeContainerLoader.getModifiersByName(tankModifierNames);

            //setup applicable resources
            List<string> resourceNames = new List<string>();
            int len = availableResources.Length;
            for (int i = 0; i < len; i++)
            {
                if (!resourceNames.Contains(availableResources[i]))
                {
                    resourceNames.Add(availableResources[i]);
                }
                else
                {
                    MonoBehaviour.print("ERROR:  Duplicate resource detected for name: " + availableResources[i] + " while loading data for part: " + module.part);
                }
            }
            len = resourceSets.Length;
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
            
            //validate vs part resource library, make sure they are all valid resource entries
            PartResourceLibrary prl = PartResourceLibrary.Instance;
            PartResourceDefinition prd;
            len = resourceNames.Count;
            for (int i = len-1; i >= 0; i--)//inverted loop to allow for removal by index while traversing
            {
                prd = prl.GetDefinition(resourceNames[i]);
                if (prd == null)
                {
                    MonoBehaviour.print("ERROR: Could not find resource definition for: " + resourceNames[i] + " while loading data for part: " + module.part+" -- resource removed from VolumeContainer");
                    resourceNames.RemoveAt(i);
                }
            }

            //sort and turn into an array
            resourceNames.Sort();//basic alpha sort...
            applicableResources = resourceNames.ToArray();

            if (string.IsNullOrEmpty(defaultFuelPreset) && string.IsNullOrEmpty(defaultResources) && applicableResources.Length > 0) { defaultResources = applicableResources[0]+",1"; }
            
            //setup volume data
            len = applicableResources.Length;
            subContainerData = new SubContainerDefinition[len];
            for (int i = 0; i < len; i++)
            {
                subContainerData[i] = new SubContainerDefinition(this, applicableResources[i]);
                if (subContainersByName.ContainsKey(subContainerData[i].name))
                {
                    MonoBehaviour.print("ERROR:  Duplicate resoruce detected for name: " + subContainerData[i].name + " while loading data for part: " + module.part);
                }
                else
                {
                    subContainersByName.Add(subContainerData[i].name, subContainerData[i]);
                }
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
            internalInitializeDefaults();
        }

        public void loadPersistenData(string data)
        {
            int len = subContainerData.Length;
            //zero out any ratios set from default tank values
            for (int i = 0; i < len; i++)
            {
                subContainerData[i].setRatio(0);
            }
            string[] vals = data.Split(',');
            currentModifierName = vals[0];
            currentFuelPreset = vals[1];
            volume = float.Parse(vals[2]);
            len = subContainerData.Length;
            int len2 = vals.Length;
            int testVal;
            if (int.TryParse(vals[3], out testVal))//test if the first value is a name or a number...//TODO remove this code after a few releases
            {
                for (int i = 0, k = 3; i < len && k < len2; i++, k++)
                {
                    subContainerData[i].setRatio(int.Parse(vals[k]));
                }
            }
            else
            {
                string name;
                int ratio;
                for (int i = 3; i < len2; i+=2)
                {
                    name = vals[i];
                    ratio = int.Parse(vals[i + 1]);
                    if (subContainersByName.ContainsKey(name))
                    {
                        subContainersByName[name].setRatio(ratio);
                    }
                }
            }
            internalUpdateTotalRatio();
            internalUpdateVolumeUnits();
            internalUpdateMassAndCost();
        }

        public string getPersistentData()
        {
            string data = currentModifierName + "," + currentFuelPreset + ","+volume;
            int len = subContainerData.Length;
            for (int i = 0; i < len; i++)
            {
                if (subContainerData[i].unitRatio > 0)
                {
                    data = data + "," + subContainerData[i].name + "," + subContainerData[i].unitRatio;
                }
            }
            return data;
        }

        public int totalUnitRatio { get { return currentTotalUnitRatio; } }

        public float totalVolumeRatio { get { return currentTotalVolumeRatio; } }

        public float containerMass{ get { return currentContainerMass; } }
        
        public float containerCost{ get { return currentContainerCost; } }

        public float resourceMass { get { return currentResourceMass; } }

        public float resourceCost { get { return currentResourceCost; } }

        public int getResourceUnitRatio(string name) { return internalGetVolumeData(name).unitRatio; }

        public float getResourceVolumeRatio(string name) { return internalGetVolumeData(name).volumeRatio; }

        public float getResourceVolume(string name) { return internalGetVolumeData(name).resourceVolume; }

        public float getResourceUnits(string name) { return internalGetVolumeData(name).resourceUnits; }

        public float getResourceMass(string name) { return internalGetVolumeData(name).resourceMass; }

        public float getResourceCost(string name) { return internalGetVolumeData(name).resourceCost; }

        public float getResourceFillPercent(string name) { return internalGetVolumeData(name).fillPercentage; }

        public float rawVolume { get { return volume; } }

        public float usableVolume { get { return currentUsableVolume; } }

        public string fuelPreset { get { return currentFuelPreset; } }

        public int Length { get { return subContainerData.Length; } }

        public bool isDirty { get { return resourcesDirty; } }

        public bool contains(string name) { return subContainersByName.ContainsKey(name); }

        public void clearDirty() { resourcesDirty = false; }

        public ContainerModifier currentModifier
        {
            get
            {
                if (cachedModifier == null || cachedModifier.name != currentModifierName) { cachedModifier = internalGetModifier(currentModifierName); }
                return cachedModifier;
            }
        }
                
        //TODO can this just return applicableResources?
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
            float unitsMax;
            float unitsFill;
            for (int i = 0; i < len; i++)
            {
                if (subContainerData[i].unitRatio > 0)
                {
                    unitsMax = subContainerData[i].resourceUnits;
                    unitsFill = unitsMax * subContainerData[i].fillPercentage;
                    list.addResource(subContainerData[i].name, unitsFill, unitsMax);
                }
            }
        }

        public void setModifier(ContainerModifier mod)
        {
            currentModifierName = mod.name;
            internalUpdateVolumeUnits();
            internalUpdateMassAndCost();
            resourcesDirty = true;
        }

        public void setResourceRatio(string name, int newRatio)
        {
            internalGetVolumeData(name).setRatio(newRatio);
            internalUpdateTotalRatio();
            internalUpdateVolumeUnits();
            internalUpdateMassAndCost();
            resourcesDirty = true;
        }

        public void setResourceFillPercent(string name, float newPercent)
        {
            internalGetVolumeData(name).setFillPercent(newPercent);
            internalUpdateMassAndCost();
            resourcesDirty = true;
        }

        public void setContainerVolume(float containerRawVolume)
        {
            volume = containerRawVolume;
            if (useStaticVolume) { volume += configVolume; }
            internalUpdateVolumeUnits();
            internalUpdateMassAndCost();
            resourcesDirty = true;
        }

        /// <summary>
        /// Zeroes any current configuration and sets the tank up for the input fuel preset; must not be null<para/>
        /// Intended to be used by the 'Next Fuel Type' buttons on the base part GUI (or other method to set a valid fuel type)
        /// </summary>
        /// <param name="preset"></param>
        public void setFuelPreset(ContainerFuelPreset preset)
        {
            if (preset == null) { throw new NullReferenceException("Fuel preset was null when calling setFuelPreset for part: "+module.part); }
            currentFuelPreset = preset.name;
            internalClearRatios();
            internalAddPresetRatios(preset);
        }

        /// <summary>
        /// -ADDS- the input preset to the current ratios for the tank without adjusting any other resource ratios<para/>
        /// this forces a 'custom' fuel preset type on the tank
        /// </summary>
        /// <param name="preset"></param>
        public void addPresetRatios(ContainerFuelPreset preset)
        {
            internalAddPresetRatios(preset);
            currentFuelPreset = "custom";
        }

        public void subtractPresetRatios(ContainerFuelPreset preset)
        {
            internalAddPresetRatios(preset, true);
            currentFuelPreset = "custom";
        }

        private void internalAddPresetRatios(ContainerFuelPreset preset, bool subtract=false)
        {
            int len = preset.resourceRatios.Length;
            ContainerResourceRatio ratio;
            for (int i = 0; i < len; i++)
            {
                ratio = preset.resourceRatios[i];
                internalGetVolumeData(ratio.resourceName).addRatio(subtract? -ratio.resourceRatio : ratio.resourceRatio);
            }
            internalUpdateTotalRatio();
            internalUpdateVolumeUnits();
            internalUpdateMassAndCost();
            resourcesDirty = true;
        }

        private void internalUpdateMassAndCost()
        {
            currentResourceMass = 0;
            int len = subContainerData.Length;
            float tempMass;
            float zeroMassResourceMass = 0;//if that name doesn't make sense... well...
            for (int i = 0; i < len; i++)
            {
                tempMass = subContainerData[i].resourceMass;
                if (tempMass == 0 && ecHasMass && subContainerData[i].resourceUnits > 0)//zero mass resource; fake it for the purposes of
                {                    
                    zeroMassResourceMass += FuelTypes.INSTANCE.getZeroMassResourceMass(subContainerData[i].name) * subContainerData[i].resourceUnits;
                }
                currentResourceMass += tempMass;
            }
                        
            currentContainerMass = 0;
            if (currentModifier.useVolumeForMass)//should most notably be used for structural tank types; any resource-containing tank should use the max resource mass * mass fraction
            {
                tempMass = volume * 0.001f;//based on volume in m^3
                currentContainerMass = tempMass * currentModifier.dryMassModifier * tankageMass;
            }
            else if (totalUnitRatio == 0)//empty tank, use config specified empty tank mass
            {
                currentContainerMass = (currentUsableVolume * 0.001f) * massPerEmptyCubicMeter * currentModifier.dryMassModifier;
            }
            else//standard mass-fraction based calculation
            {
                tempMass = currentResourceMass;
                currentContainerMass = tempMass * currentModifier.dryMassModifier * tankageMass;
            }

            currentResourceCost = 0;
            float tempCost;
            float zeroCostResourceCosts = 0;
            for (int i = 0; i < len; i++)
            {
                tempCost = subContainerData[i].resourceCost;
                if (tempCost == 0 && ecHasCost && subContainerData[i].resourceUnits > 0)
                {
                    zeroCostResourceCosts += FuelTypes.INSTANCE.getZeroCostResourceCost(subContainerData[i].name) * subContainerData[i].resourceUnits;
                }
                currentResourceCost += tempCost * subContainerData[i].fillPercentage;
            }
            currentContainerCost = costPerDryTon * currentModifier.costModifier * currentContainerMass;
            currentContainerCost += zeroCostResourceCosts;

            currentContainerMass += zeroMassResourceMass;
        }

        private void internalUpdateVolumeUnits()
        {
            currentUsableVolume = volume - (volume * tankageVolume * currentModifier.tankageVolumeModifier);
            currentUsableVolume *= currentModifier.volumeModifier;//basically used only for structural type to zero out avaialble volume
            if (currentUsableVolume > volume) { currentUsableVolume = volume; }
            float total = currentTotalVolumeRatio;
            int len = subContainerData.Length;
            float vol;
            for (int i = 0; i < len; i++)
            {
                vol = 0;
                if (total > 0)
                {
                    vol = subContainerData[i].volumeRatio / total;
                    vol *= currentUsableVolume;                    
                }
                subContainerData[i].setVolume(vol);
            }
        }

        private void internalUpdateTotalRatio()
        {
            currentTotalUnitRatio = 0;
            currentTotalVolumeRatio = 0;
            int len = subContainerData.Length;
            for (int i = 0; i < len; i++)
            {
                currentTotalUnitRatio += subContainerData[i].unitRatio;
                currentTotalVolumeRatio += subContainerData[i].volumeRatio;
            }
        }

        /// <summary>
        /// UNSAFE; MUST MANUALLY UPDATE AFTER CALLING THIS METHOD...
        /// </summary>
        private void internalClearRatios()
        {
            int len = subContainerData.Length;
            for (int i = 0; i < len; i++) { subContainerData[i].setRatio(0); }
        }

        private void internalInitializeDefaults()
        {
            if (!string.IsNullOrEmpty(defaultFuelPreset))
            {
                currentFuelPreset = defaultFuelPreset;
                setFuelPreset(internalGetFuelPreset(currentFuelPreset));
            }
            else//use default resource
            {
                internalClearRatios();
                currentFuelPreset = "custom";
                string[] splitResources = defaultResources.Split(';');
                string[] resourceValues;
                int len = splitResources.Length;
                string name;
                int ratio;
                float percent;
                for (int i = 0; i < len; i++)
                {
                    resourceValues = splitResources[i].Split(',');
                    name = resourceValues[0];
                    if (resourceValues.Length >= 2) { ratio = int.Parse(resourceValues[1]); }
                    else { ratio = 1; }
                    if (resourceValues.Length >= 3) { percent = float.Parse(resourceValues[2]); }
                    else { percent = 1; }
                    internalGetVolumeData(name).setRatio(ratio);
                    internalGetVolumeData(name).setFillPercent(percent);
                }
                internalUpdateTotalRatio();
                internalUpdateVolumeUnits();
                internalUpdateMassAndCost();
            }
            resourcesDirty = true;
        }

        public SubContainerDefinition internalGetVolumeData(string resourceName) { return subContainersByName[resourceName]; }

        public ContainerFuelPreset internalGetFuelPreset(string name) { return Array.Find(fuelPresets, m => m.name == name); }

        public ContainerModifier internalGetModifier(string name) { return Array.Find(modifiers, m => m.name == name); }

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
        private float fillPercent = 1f;//the amount of the resource compared to max to fill the part with

        public SubContainerDefinition(ContainerDefinition container, String name)
        {
            this.container = container;
            this.name = name;
            def = PartResourceLibrary.Instance.resourceDefinitions[name];
            if (def == null)
            {
                throw new NullReferenceException("Resource definition was null for name: " + name+" while loading resources for part: "+container.module.part);
            }
        }

        public float resourceMass { get { return resourceUnits * unitMass; } }

        public float resourceCost { get { return resourceUnits * unitCost; } }

        public float fillPercentage { get { return fillPercent; } }

        /// <summary>
        /// Current cached usable volume occupied by this resource
        /// </summary>
        public float resourceVolume { get { return volume; } }

        /// <summary>
        /// Current number of units (volume/unitVolume) this subcontainer can hold
        /// </summary>
        public float resourceUnits { get { return volume / unitVolume; } }

        /// <summary>
        /// Current user or config specified unit ratio for this resource
        /// </summary>
        public int unitRatio { get { return ratio; } }

        /// <summary>
        /// Volume weighted ratio for this resource
        /// </summary>
        public float volumeRatio {get { return (float)ratio * unitVolume; }}

        /// <summary>
        /// Volume per-unit for this resource
        /// </summary>
        public float unitVolume { get { return FuelTypes.INSTANCE.getResourceVolume(name, def); } }

        /// <summary>
        /// Mass per-unit for this resource
        /// </summary>
        public float unitMass { get { return def.density; } }

        public float unitCost { get { return def.unitCost; } }
        
        public void addRatio(int addRatio)
        {
            setRatio(ratio + addRatio);
        }

        public void setVolume(float volume)
        {
            this.volume = volume;
        }

        public void setRatio(int ratio)
        {
            if (ratio < 0) { ratio = 0; }
            this.ratio = ratio;
            if (ratio == 0) { volume = 0; }
        }

        public void setFillPercent(float percent)
        {
            this.fillPercent = percent;
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
        public readonly float volumeModifier = 1f;
        public readonly float tankageVolumeModifier = 1f;//applied before any resource volumes are calculated
        public readonly float dryMassModifier = 1f;//applied after dry mass for tank is tallied from resource mass fraction values
        public readonly float costModifier = 1f;
        public readonly float impactModifier = 1f;
        public readonly float heatModifier = 1f;
        public readonly bool useVolumeForMass = false;//special flag for structural tank type, to denote that dry mass is derived from raw volume rather than resource mass        
        public readonly float boiloffModifier = 1f;
        public readonly float activeInsulationPercent = 0f;
        public readonly float activeECCost = 0f;
        public readonly float activeInsulationPrevention = 1f;
        public readonly float inactiveInsulationPrevention = 0f;
        public readonly float passiveInsulationPrevention = 0f;
        public readonly string unlockName = string.Empty;

        public ContainerModifier(ConfigNode node)
        {
            name = node.GetStringValue("name");
            title = node.GetStringValue("title");
            description = node.GetStringValue("description");
            volumeModifier = node.GetFloatValue("volumeModifier", volumeModifier);
            tankageVolumeModifier = node.GetFloatValue("tankageModifier", tankageVolumeModifier);
            dryMassModifier = node.GetFloatValue("massModifier", dryMassModifier);
            costModifier = node.GetFloatValue("costModifier", costModifier);
            impactModifier = node.GetFloatValue("impactModifier", impactModifier);
            heatModifier = node.GetFloatValue("heatModifier", heatModifier);
            useVolumeForMass = node.GetBoolValue("useVolumeForMass", useVolumeForMass);

            boiloffModifier = node.GetFloatValue("boiloffModifier", boiloffModifier);
            activeInsulationPercent = node.GetFloatValue("activeInsulationPercent", activeInsulationPercent);
            activeInsulationPrevention = node.GetFloatValue("activeInsulationPrevention", activeInsulationPrevention);
            inactiveInsulationPrevention = node.GetFloatValue("inactiveInsulationPrevention", inactiveInsulationPrevention);
            passiveInsulationPrevention = node.GetFloatValue("passiveInsulationPrevention", passiveInsulationPrevention);
            activeECCost = node.GetFloatValue("activeECCost", activeECCost);

            unlockName = node.GetStringValue("upgradeUnlock");
        }

        public bool isAvailable(SSTUVolumeContainer module)
        {
            if (string.IsNullOrEmpty(unlockName)) { return true; }
            return PartUpgradeManager.Handler.IsUnlocked(unlockName);
            //return string.IsNullOrEmpty(unlockName)? true : module.upgradesApplied.Contains(unlockName);
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
        public readonly float totalVolumeRatio;
        public readonly ContainerResourceRatio[] resourceRatios;
        public ContainerFuelPreset(ConfigNode node)
        {
            name = node.GetStringValue("name");
            ConfigNode[] ratioNodes = node.GetNodes("RESOURCE");
            int len = ratioNodes.Length;
            resourceRatios = new ContainerResourceRatio[len];
            ContainerResourceRatio ratio;
            for (int i = 0; i < len; i++)
            {
                ratio = new ContainerResourceRatio(ratioNodes[i]);
                resourceRatios[i] = ratio;
                totalVolumeRatio += ratio.resourceRatio * ratio.resourceVolume;
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

        public void addResources(SSTUResourceList list, float cubicMeters)
        {
            float resourceVolumeRatio;
            float resourcePercent;
            float resourceVolume;
            int len = resourceRatios.Length;
            ContainerResourceRatio ratio;
            for (int i = 0; i < len; i++)
            {
                ratio = resourceRatios[i];
                resourceVolumeRatio = ratio.resourceRatio * ratio.resourceVolume;
                resourcePercent = resourceVolumeRatio / totalVolumeRatio;
                resourceVolume = cubicMeters * resourcePercent;
                list.addResourceByVolume(ratio.resourceName, resourceVolume);
            }
        }

        public float getResourceCost(float cubicMeters)
        {
            float resourceVolumeRatio;
            float resourcePercent;
            float resourceVolume;
            float resourceUnits;
            float totalCost = 0f;
            int len = resourceRatios.Length;
            ContainerResourceRatio ratio;
            for (int i = 0; i < len; i++)
            {
                ratio = resourceRatios[i];
                resourceVolumeRatio = ratio.resourceRatio * ratio.resourceVolume;
                resourcePercent = resourceVolumeRatio / totalVolumeRatio;
                resourceVolume = cubicMeters * resourcePercent;
                PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(ratio.resourceName);
                resourceUnits = (resourceVolume * 1000f) / def.volume;
                totalCost += def.unitCost * resourceUnits;
            }
            return totalCost;
        }

        public ConfigNode getPropellantNode(ResourceFlowMode flowMode)
        {
            ConfigNode node = new ConfigNode();
            int len = resourceRatios.Length;
            for (int i = 0; i < len; i++)
            {
                ConfigNode propNode = new ConfigNode("PROPELLANT");
                propNode.AddValue("name", resourceRatios[i].resourceName);
                propNode.AddValue("ratio", resourceRatios[i].resourceRatio);
                propNode.AddValue("resourceFlowMode", flowMode.ToString());
                node.AddNode(propNode);
            }
            return node;
        }

    }

    /// <summary>
    /// Read-only data class for a ContainerFuelPreset ratio data for a single resource
    /// </summary>
    public class ContainerResourceRatio
    {
        public readonly string resourceName;
        public readonly int resourceRatio;
        public readonly float resourceVolume;
        public ContainerResourceRatio(ConfigNode node)
        {
            resourceName = node.GetStringValue("resource");
            resourceRatio = node.GetIntValue("ratio", 1);
            resourceVolume = FuelTypes.INSTANCE.getResourceVolume(resourceName);
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

        public static void loadConfigData()
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
