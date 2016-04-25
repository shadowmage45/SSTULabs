using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSTUTools
{
    public class SSTUVolumeContainer : PartModule, IPartCostModifier, IPartMassModifier
    {

        /// <summary>
        /// Current volume in liters, determines sub-container volumes<para/>
        /// DO NOT UPDATE MANUALLY -- call container.onVolumeUpdated(float volume)
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Total Volume", guiUnits = "l")]
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
        [KSPField(isPersistant = false, guiActiveEditor = true, guiName = "Usable Volume", guiUnits = "l")]
        public float usableVolume;

        /// <summary>
        /// Gui displayed dry mass, tallied from containers
        /// </summary>
        [KSPField(isPersistant = false, guiActiveEditor = true, guiName = "Tankage Mass", guiUnits = "t")]
        public float tankageMass;

        /// <summary>
        /// Gui displayed fuel type selection; taken from the 'base container'
        /// </summary>
        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "FuelType"),
         UI_ChooseOption(suppressEditorShipModified = true, display =new string[]{"custom"}, options = new string[] { "custom"})]
        public string guiFuelType = "custom";

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

        [KSPField]
        public double lastBoiloffUpdate = 0d;
        
        //private cached vars for... things....
        private ContainerDefinition[] containers;
        private float modifiedMass;
        private float modifiedCost;
        private string prevFuelType;

        //TODO force-disable this as cleanup? or... i guess the gui will just cease to be called when the part is destroyed; but should still clean up the cached vars in the gui-handler...
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
            updateMassAndCost();//update cached part mass and cost values
            updatePersistentData();//update persistent data in case tank was just initialized
            updateFuelSelections();//update the selections for the 'FuelType' UI slider, this adds or removes the 'custom' option as needed
            updatePartStats();//update part stats for crash tolerance and heat, as determined by the container modifiers

            //disable next fuel event button if main container does not have more than one preset type available 
            BaseField fuelSelection = Fields["guiFuelType"];
            fuelSelection.guiActiveEditor = enableFuelTypeChange && getBaseContainer().fuelPresets.Length > 1;
            fuelSelection.uiControlEditor.onFieldChanged = onFuelTypeUpdated;
            MonoBehaviour.print("fuel selections current value: " + guiFuelType);

            BaseEvent editContainerEvent = Events["openGUIEvent"];
            editContainerEvent.active = enableContainerEdit;

            if (!initializedResources && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                initializedResources = true;
                updateTankResources();
                SSTUStockInterop.fireEditorUpdate();//update cost
            }
        }

        public void Start()
        {
            updateKISVolume();
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return subtractMass ? -defaultMass + modifiedMass : modifiedMass; }
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) { return subtractCost ? -defaultCost + modifiedCost : modifiedCost; }
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        private void loadConfigData()
        {
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(this);
            ConfigNode[] containerNodes = node.GetNodes("CONTAINER");
            int len = containerNodes.Length;
            containers = new ContainerDefinition[len];
            for (int i = 0; i < len; i++)
            {
                containers[i] = new ContainerDefinition(containerNodes[i], volume);
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
            MonoBehaviour.print("Updated VC Persistent data to:\n     " + persistentData);
        }

        public void onVolumeUpdated(float newVolume)
        {
            if (newVolume != volume)
            {
                volume = newVolume;
                if (SSTUModInterop.isRFInstalled() || SSTUModInterop.isMFTInstalled())
                {
                    SSTUModInterop.onPartFuelVolumeUpdate(part, volume * 0.001f);//re-convert from l to m^3                    
                }
                else
                {
                    updateContainerVolumes();
                    updateMassAndCost();
                    updateTankResources();
                    updateFuelSelections();
                    updatePersistentData();
                }
                SSTUStockInterop.fireEditorUpdate();
            }
        }

        public int numberOfContainers { get { return containers.Length; } }

        public void setContainerPercents(float[] percents, float totalVolume)
        {
            int len = containers.Length;
            if (len != percents.Length) { throw new IndexOutOfRangeException("Input container percents length does not match containers length: "+percents.Length+" : "+len); }
            float total = 0;
            for (int i = 0; i < len-1; i++)
            {
                total += percents[i];
                containers[i].setContainerPercent(percents[i]);
            }
            if (total > 1) { throw new InvalidOperationException("Input percents total > 1"); }
            containers[len - 1].setContainerPercent(1.0f - total);
            volume = totalVolume;
            updateContainerVolumes();
            updateMassAndCost();
            updateTankResources();
            updateFuelSelections();
            updatePersistentData();
            SSTUStockInterop.fireEditorUpdate();
        }

        public void containerFuelTypeSet(ContainerDefinition container, ContainerFuelPreset fuelType)
        {

            updateFuelSelections();
        }

        public void containerFuelTypeAdded(ContainerDefinition container, ContainerFuelPreset fuelType)
        {
            container.addPresetRatios(fuelType);
            updateFuelSelections();
        }

        public void containerTypeUpdated(ContainerDefinition container, ContainerModifier newType)
        {
            container.setModifier(newType);
            updatePartStats();
        }

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

        private ContainerDefinition getBaseContainer() { return containers[baseContainerIndex]; }

        private void updateKISVolume()
        {
            PartResource kisResource = part.Resources["SSTUKISStorage"];
            float volume = kisResource == null ? 0 : (float) kisResource.maxAmount;
            SSTUModInterop.onPartKISVolumeUpdated(part, volume);
        }
        
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
            list.setResourcesToPart(part, HighLogic.LoadedSceneIsEditor);
            updateMassAndCost();
            updateKISVolume();
        }

        /// <summary>
        /// Update sub-containers volume data for the current total part volume
        /// </summary>
        private void updateContainerVolumes()
        {
            int len = containers.Length;
            for (int i = 0; i < len; i++)
            {
                containers[i].setContainerVolume(volume);
            }
        }
        
        /// <summary>
        /// Update cached mass and cost values from container calculated data
        /// </summary>
        private void updateMassAndCost()
        {
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
            usableVolume = volume - tankageVolume;
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
                }
            }
        }

        private void openGUI()
        {
            VolumeContainerGUI.openGUI(this, containers);
            guiEnabled = true;
            EditorLogic editor = EditorLogic.fetch;
            if (editor != null) { editor.Lock(true, true, true, "SSTUVolumeContainerLock"); }
        }

        public void closeGUI()
        {
            guiEnabled = false;
            EditorLogic editor = EditorLogic.fetch;
            if (editor != null) { editor.Unlock("SSTUVolumeContainerLock"); }
        }
    }

}
