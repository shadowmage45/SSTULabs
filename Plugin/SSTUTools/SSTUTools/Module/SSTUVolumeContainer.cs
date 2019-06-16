using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static SSTUTools.SSTULog;

namespace SSTUTools
{
    public class SSTUVolumeContainer : PartModule, IPartCostModifier, IPartMassModifier
    {

        /// <summary>
        /// Current volume in liters, summed from sub-container volumes
        /// DO NOT UPDATE MANUALLY -- call container.onVolumeUpdated(float volume)
        /// </summary>
        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Total Volume", guiUnits = "l")]
        public float volume;

        /// <summary>
        /// Config field for if user can change resources with the 'Next Fuel Type' button (or slider...)
        /// </summary>
        [KSPField]
        public bool enableFuelTypeChange = true;

        /// <summary>
        /// Config field for if user can open container editing GUI for this part
        /// </summary>
        [KSPField]
        public bool enableContainerEdit = true;

        /// <summary>
        /// Determines which container the fuel-type slider will adjust fuel types for
        /// </summary>
        [KSPField]
        public int baseContainerIndex = 0;

        [KSPField]
        public bool subtractMass = true;

        [KSPField]
        public bool subtractCost = true;

        /// <summary>
        /// Gui displayed usable volume, tallied from containers
        /// </summary>
        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Usable Volume", guiUnits = "l")]
        public float usableVolume;

        /// <summary>
        /// Gui displayed dry mass, tallied from containers
        /// </summary>
        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Tankage Mass", guiUnits = "t")]
        public float tankageMass;

        /// <summary>
        /// Gui displayed fuel type selection; taken from the 'base container'
        /// </summary>
        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "FuelType"),
         UI_ChooseOption(suppressEditorShipModified = true, display =new string[]{"custom"}, options = new string[] { "custom"})]
        public string guiFuelType = "custom";

        [KSPField(isPersistant = false, guiActiveEditor = false, guiActive = false)]
        public float inflationMultiplier = 1;

        /// <summary>
        /// Track whether resources have had a first-time init or not; could likely just check if the part has any resources??
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        /// <summary>
        /// Persistent save data for containers
        /// </summary>
        [KSPField(isPersistant = true)]
        public string persistentData = string.Empty;

        [Persistent]
        public string configNodeData = string.Empty;

        //private cached vars for... things....
        private ContainerDefinition[] containers;
        private float modifiedMass = -1;
        private float modifiedCost = -1;
        private string prevFuelType;
        private bool guiEnabled = false;

        public void onFuelTypeUpdated(BaseField field, object obj)
        {
            if (guiFuelType != prevFuelType)
            {
                prevFuelType = guiFuelType;
                setSingleFuelType(guiFuelType, true);
            }
        }

        private void setSingleFuelType(string presetName, bool updateSymmetry)
        {
            ContainerFuelPreset preset = Array.Find(getBaseContainer().fuelPresets, m => m.name == presetName);
            if (preset == null) { throw new NullReferenceException("Fuel preset cannot be null. Name: " + presetName); }
            getBaseContainer().setFuelPreset(preset);
            updateTankResources();
            updatePersistentData();
            updateFuelSelections();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUVolumeContainer>().setSingleFuelType(presetName, false);
                }
            }
            SSTUStockInterop.fireEditorUpdate();
        }

        [KSPEvent(guiName = "Configure Containers", guiActiveEditor = true)]
        public void openGUIEvent()
        {
            openGUI();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            updatePersistentData();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            loadConfigData();//initialize the container instances, including initializing default values if needed
            if (initializedResources)
            {
                updateMassAndCost();//update cached part mass and cost values
                updatePersistentData();//update persistent data in case tank was just initialized
                updateFuelSelections();//update the selections for the 'FuelType' UI slider, this adds or removes the 'custom' option as needed
                updatePartStats();//update part stats for crash tolerance and heat, as determined by the container modifiers
                updateGUIControls();
            }

            BaseField fuelSelection = Fields[nameof(guiFuelType)];
            fuelSelection.uiControlEditor.onFieldChanged = onFuelTypeUpdated;
            fuelSelection.guiActive = false;
            Fields[nameof(volume)].guiActive = false;
            Fields[nameof(tankageMass)].guiActive = false;
            Fields[nameof(usableVolume)].guiActive = false;
        }

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);
            if (!initializedResources && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                initializedResources = true;
                recalcVolume();
            }
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (modifiedMass < 0) { return 0; }
            return subtractMass ? -defaultMass + modifiedMass : modifiedMass;
        }

        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (modifiedCost < 0) { return 0; }
            return subtractCost ? -defaultCost + modifiedCost : modifiedCost;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        private void loadConfigData()
        {
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            ConfigNode[] containerNodes = node.GetNodes("CONTAINER");
            int len = containerNodes.Length;
            containers = new ContainerDefinition[len];
            for (int i = 0; i < len; i++)
            {
                containers[i] = new ContainerDefinition(this, containerNodes[i]);
            }
            if (!string.IsNullOrEmpty(persistentData))
            {
                string[] splits = persistentData.Split(':');
                len = containers.Length;
                for (int i = 0; i < len && i < splits.Length; i++)
                {
                    containers[i].loadPersistenData(splits[i]);
                }
            }
            prevFuelType = getBaseContainer().fuelPreset;
        }

        private void updatePersistentData()
        {
            if (containers != null)
            {
                persistentData = "";
                int len = containers.Length;
                for (int i = 0; i < len; i++)
                {
                    if (i > 0) { persistentData = persistentData + ":"; }
                    persistentData = persistentData + containers[i].getPersistentData();
                }
            }
        }

        /// <summary>
        /// Recalculates volume for all containers by finding all IContainerVolumeContributor implementors, and summing the volume for each container from the returned values.
        /// Removes the need to manually calculate new % values for each container.
        /// </summary>
        public void recalcVolume()
        {
            if (!initializedResources || containers == null)
            {
                //not yet initialized -- recalc will be called during Start, so ignore for now
                return;
            }
            float[] volumes = new float[numberOfContainers];
            IContainerVolumeContributor[] contributors = part.FindModulesImplementing<IContainerVolumeContributor>().ToArray();
            ContainerContribution[] cts;
            int len = contributors.Length;
            for (int i = 0; i < len; i++)
            {
                if (contributors[i] == null)
                {
                    SSTULog.error("NULL Container Contributor");
                }
                cts = contributors[i].getContainerContributions();
                if (cts == null)
                {
                    SSTULog.error("NULL Container Contributor Contributions");
                }
                int len2 = cts.Length;
                for (int k = 0; k < len2; k++)
                {
                    int idx = cts[k].containerIndex;
                    if (idx < volumes.Length && idx >= 0)
                    {
                        volumes[cts[k].containerIndex] += cts[k].containerVolume;
                    }
                }
            }
            len = containers.Length;
            for (int i = 0; i < len; i++)
            {
                if (containers[i] == null)
                {
                    SSTULog.error("NULL Container definition for index: " + i);
                }
                containers[i].setContainerVolume(volumes[i]);
            }
            updateMassAndCost();//update cached part mass and cost values
            updatePersistentData();//update persistent data in case tank was just initialized
            updateFuelSelections();//update the selections for the 'FuelType' UI slider, this adds or removes the 'custom' option as needed
            updatePartStats();//update part stats for crash tolerance and heat, as determined by the container modifiers
            updateGUIControls();
            updateTankResources();

            SSTUStockInterop.fireEditorUpdate();
        }

        public int numberOfContainers { get { return containers==null? 0 : containers.Length; } }

        public ContainerDefinition highestVolumeContainer(string resourceName)
        {
            ContainerDefinition high = null;
            ContainerDefinition def;
            float highVal=-1, val;
            int len = containers.Length;
            for (int i = 0; i < len; i++)
            {
                def = containers[i];
                if (def.contains(resourceName))
                {
                    val = def.getResourceVolume(resourceName);
                    if (highVal < 0 || val > highVal)
                    {
                        high = def;
                        highVal = val;
                    }
                }
            }
            return high;
        }

        public void setFuelPreset(int containerIndex, ContainerFuelPreset fuelType, bool updateSymmetry)
        {
            setFuelPreset(containers[containerIndex], fuelType, updateSymmetry);
        }

        private ContainerDefinition getBaseContainer() { return containers[baseContainerIndex]; }

        /// <summary>
        /// Update part impact tolerance and max temp stats based on first containers modifier values and part prefab values
        /// </summary>
        private void updatePartStats()
        {
            if (part.partInfo == null || part.partInfo.partPrefab == null) { return; }
            ContainerModifier mod = containers[0].currentModifier;
            part.crashTolerance = part.partInfo.partPrefab.crashTolerance * mod.impactModifier;
            part.maxTemp = part.partInfo.partPrefab.maxTemp * mod.heatModifier;
            part.skinMaxTemp = part.partInfo.partPrefab.skinMaxTemp * mod.heatModifier;
        }

        /// <summary>
        /// Update the available GUI fuel type selection values for the current container setup
        /// </summary>
        private void updateFuelSelections()
        {
            ContainerDefinition container = getBaseContainer();
            string currentType = container.fuelPreset;
            guiFuelType = prevFuelType = currentType;
            string[] presetNames;
            if (currentType == "custom")
            {
                presetNames = new string[container.fuelPresets.Length+1];
                presetNames[0] = "custom";
                for (int i = 1; i < presetNames.Length; i++) { presetNames[i] = container.fuelPresets[i - 1].name; }
            }
            else
            {
                presetNames = new string[container.fuelPresets.Length];
                for (int i = 0; i < presetNames.Length; i++) { presetNames[i] = container.fuelPresets[i].name; }
            }
            this.updateUIChooseOptionControl("guiFuelType", presetNames, presetNames, true, currentType);
            Fields["guiFuelType"].guiActiveEditor = enableFuelTypeChange && container.rawVolume > 0;
        }
        
        /// <summary>
        /// Update the resources for the part from the resources in the currently configured containers
        /// </summary>
        private void updateTankResources()
        {
            SSTUResourceList list = new SSTUResourceList();
            int len = containers.Length;
            for (int i = 0; i < len; i++)
            {
                containers[i].getResources(list);
            }
            list.setResourcesToPart(part, inflationMultiplier, HighLogic.LoadedSceneIsFlight);
            updateMassAndCost();
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onContainerUpdated(this);
            SSTUResourceBoiloff rb = part.GetComponent<SSTUResourceBoiloff>();
            if (rb != null) { rb.onPartResourcesChanged(); }
        }

        /// <summary>
        /// Update cached mass and cost values from container calculated data
        /// </summary>
        private void updateMassAndCost()
        {
            float totalVolume = getTotalVolume();
            modifiedCost = 0;
            modifiedMass = 0;
            float tankageVolume = 0;
            ContainerDefinition container;
            int len = containers.Length;
            for (int i = 0; i < len; i++)
            {
                container = containers[i];
                modifiedMass += container.containerMass;
                modifiedCost += container.containerCost + container.resourceCost;
                tankageVolume += (container.rawVolume - container.usableVolume);
            }
            tankageMass = modifiedMass;
            usableVolume = totalVolume - tankageVolume;
        }

        /// <summary>
        /// Updates GUI controls.
        /// </summary>
        private void updateGUIControls()
        {
            volume = getTotalVolume();
            //TODO -- sum subcontainer volumes
            Events[nameof(openGUIEvent)].guiActiveEditor = volume > 0 && enableContainerEdit;
            Fields[nameof(guiFuelType)].guiActiveEditor = volume > 0 && enableFuelTypeChange && getBaseContainer().fuelPresets.Length > 1;
            Fields[nameof(volume)].guiActiveEditor = volume > 0;
            Fields[nameof(usableVolume)].guiActiveEditor = volume > 0;
            Fields[nameof(tankageMass)].guiActiveEditor = volume > 0;
        }

        /// <summary>
        /// Summs container current volumes and returns value.
        /// </summary>
        private float getTotalVolume()
        {
            float vol = 0f;
            int len = containers.Length;
            for (int i = 0; i < len; i++)
            {
                vol += containers[i].rawVolume;
            }
            return vol;
        }

        #region GUI update methods with symmetry counterpart handling

        public void setFuelPreset(ContainerDefinition container, ContainerFuelPreset preset, bool updateSymmetry)
        {
            container.setFuelPreset(preset);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    SSTUVolumeContainer mod = p.GetComponent<SSTUVolumeContainer>();
                    ContainerDefinition def2 = mod.getContainer(container.name);
                    ContainerFuelPreset preset2 = def2.internalGetFuelPreset(preset.name);
                    mod.setFuelPreset(def2, preset2, false);
                }
            }
        }
        
        public void subtractPresetRatios(ContainerDefinition container, ContainerFuelPreset preset, bool updateSymmetry)
        {
            container.subtractPresetRatios(preset);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    SSTUVolumeContainer mod = p.GetComponent<SSTUVolumeContainer>();
                    ContainerDefinition def2 = mod.getContainer(container.name);
                    ContainerFuelPreset preset2 = def2.internalGetFuelPreset(preset.name);
                    mod.subtractPresetRatios(def2, preset2, false);
                }
            }
        }
        
        public void addPresetRatios(ContainerDefinition container, ContainerFuelPreset preset, bool updateSymmetry)
        {
            container.addPresetRatios(preset);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    SSTUVolumeContainer symmetryModule = p.GetComponent<SSTUVolumeContainer>();
                    ContainerDefinition symmetryModuleContainer = symmetryModule.getContainer(container.name);
                    ContainerFuelPreset symmetryModulePreset = symmetryModuleContainer.internalGetFuelPreset(preset.name);
                    symmetryModule.addPresetRatios(symmetryModuleContainer, symmetryModulePreset, false);
                }
            }
        }

        public void setResourceRatio(ContainerDefinition def, string resourceName, int newRatio, bool updateSymmetry = false)
        {
            def.setResourceRatio(resourceName, newRatio);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    SSTUVolumeContainer mod = p.GetComponent<SSTUVolumeContainer>();
                    ContainerDefinition def2 = mod.getContainer(def.name);
                    mod.setResourceRatio(def2, resourceName, newRatio, false);
                }
            }
        }
        
        public void setResourceFillPercent(ContainerDefinition def, string resourceName, float newPercent, bool updateSymmetry = false)
        {
            def.setResourceFillPercent(resourceName, newPercent);
            if (updateSymmetry)
            {
                foreach(Part p in part.symmetryCounterparts)
                {
                    SSTUVolumeContainer mod = p.GetComponent<SSTUVolumeContainer>();
                    ContainerDefinition def2 = mod.getContainer(def.name);
                    mod.setResourceFillPercent(def2, resourceName, newPercent, false);
                }
            }
        }

        public void containerTypeUpdated(ContainerDefinition container, ContainerModifier newType, bool updateSymmetry = false)
        {
            container.setModifier(newType);
            updatePartStats();

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    SSTUVolumeContainer mod = p.GetComponent<SSTUVolumeContainer>();
                    ContainerDefinition def2 = mod.getContainer(container.name);
                    ContainerModifier mod2 = def2.internalGetModifier(newType.name);
                    mod.containerTypeUpdated(def2, mod2, false);
                }
            }
        }

        #endregion

        private ContainerDefinition getContainer(string name)
        {
            int len = containers.Length;
            for (int i = 0; i < len; i++)
            {
                if (containers[i].name == name) { return containers[i]; }
            }
            return null;
        }

        private void OnGUI()
        {
            if (guiEnabled)
            {
                VolumeContainerGUI.updateGUI();
                int len = containers.Length;
                bool update = false;
                for (int i = 0; i < len; i++) { if (containers[i].isDirty) { update = true; break; } }
                if (update)
                {
                    for (int i = 0; i < len; i++) { containers[i].clearDirty(); }
                    updateTankResources();
                    updatePersistentData();
                    VolumeContainerGUI.updateGuiData();
                    foreach (Part p in part.symmetryCounterparts)
                    {
                        SSTUVolumeContainer vc = p.GetComponent<SSTUVolumeContainer>();
                        vc.updateTankResources();
                        vc.updatePersistentData();
                    }
                }
            }
        }

        private void openGUI()
        {
            if (VolumeContainerGUI.module != null)
            {
                VolumeContainerGUI.closeGUI();
                return;
            }

            guiEnabled = true;
            EditorLogic editor = EditorLogic.fetch;
            if (editor != null) { editor.Lock(true, true, true, "SSTUVolumeContainerLock"); }
            VolumeContainerGUI.openGUI(this, containers);
        }

        public void closeGUI()
        {
            guiEnabled = false;
            EditorLogic editor = EditorLogic.fetch;
            if (editor != null) { editor.Unlock("SSTUVolumeContainerLock"); }
        }
    }

}
