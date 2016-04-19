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
        /// Current volume in liters, determines sub-container volumes
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Total Volume")]
        public float volume;

        /// <summary>
        /// Gui displayed usable volume, tallied from containers
        /// </summary>
        [KSPField(isPersistant = false, guiActiveEditor = true, guiName = "Usable Volume")]
        public float usableVolume;
               
        /// <summary>
        /// Gui displayed dry mass, tallied from containers
        /// </summary>
        [KSPField(isPersistant = false, guiActiveEditor = true, guiName = "Tankage Mass")]
        public float tankageMass;

        /// <summary>
        /// Gui displayed unusable volume, tallied from containers
        /// </summary>
        [KSPField(isPersistant = false, guiActiveEditor = true, guiName = "Tankage Volume")]
        public float tankageVolume;

        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        /// <summary>
        /// Persistent save data for containers
        /// </summary>
        [KSPField(isPersistant = true)]
        public string persistentData = string.Empty;
        
        //private cached vars for... things....
        private ContainerDefinition[] containers;
        public float modifiedMass;
        public float modifiedCost;

        //cached vars for GUI interaction
        private bool guiEnabled = false;

        [KSPEvent(guiName = "Next Fuel Type", guiActiveEditor = true)]
        public void nextFuelTypeEvent()
        {
            ContainerDefinition def = containers[0];
            string current = def.fuelPreset;
            ContainerFuelPreset preset = SSTUUtils.findNext(def.fuelPresets, m => m.name == current);            
            if (preset == null)//was using a custom ratio setup, reset it all
            {
                def.setFuelPreset(def.fuelPresets[0]);
            }
            else
            {
                def.setFuelPreset(preset);
            }
            updateTankResources();
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

            //disable next fuel event button if main container does not have more than one preset type available 
            BaseEvent nextFuelEvent = Events["nextFuelTypeEvent"];
            nextFuelEvent.active = containers[0].fuelPresets.Length > 1;

            if (!initializedResources && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                updateTankResources();
            }
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return -defaultMass + modifiedMass; }
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) { return -defaultCost + modifiedCost; }
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        public void onVolumeUpdated(float newVolume)
        {
            if (newVolume != volume)
            {
                volume = newVolume;
                MonoBehaviour.print("ON VOLUME UPDATED");
                updateContainerVolumes();
                updateMassAndCost();                
                updateTankResources();
            }
        }

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
                MonoBehaviour.print("Loading persistent container data from: " + persistentData);
                node = SSTUConfigNodeUtils.parseConfigNode(persistentData);
                containerNodes = node.GetNodes("CONTAINER");
                len = containers.Length;
                for (int i = 0; i < len && i < containerNodes.Length; i++)
                {
                    containers[i].load(containerNodes[i]);
                }
            }
        }

        private void updatePersistentData()
        {
            ConfigNode node = new ConfigNode("PERSISTENTDATA");
            if (containers != null)
            {
                int len = containers.Length;
                ConfigNode saveData;
                for (int i = 0; i < len; i++)
                {
                    saveData = new ConfigNode("CONTAINER");
                    containers[i].save(saveData);
                    node.AddNode(saveData);
                }
            }
            persistentData = node.ToString();
            MonoBehaviour.print("UPDATED PERSISTENT DATA!! :: " + persistentData);
        }

        public void containerFuelTypeAdded(ContainerDefinition container, ContainerFuelPreset fuelType)
        {
            MonoBehaviour.print("Preset added: " + fuelType.name);
            container.addPresetRatios(fuelType);
        }

        public void containerTypeUpdated(ContainerDefinition container, ContainerModifier newType)
        {
            MonoBehaviour.print("Modifier Updated: " + newType.name);
            container.setModifier(newType);      
        }
        
        private void updateTankResources()
        {
            MonoBehaviour.print("UPDATE TANK RESOURCES!!");
            SSTUResourceList list = new SSTUResourceList();
            int len = containers.Length;
            for (int i = 0; i < len; i++)
            {
                containers[i].getResources(list);
            }
            list.setResourcesToPart(part, HighLogic.LoadedSceneIsEditor);
            updateMassAndCost();
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
            tankageVolume = 0;
            ContainerDefinition container;
            int len = containers.Length;
            for (int i = 0; i < len; i++)
            {
                container = containers[i];
                modifiedMass += container.containerMass;
                modifiedCost += container.containerCost;
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
                for (int i = 0; i < len; i++) { if (containers[i].isDirty) { update = true; } }
                if (update)
                {
                    for (int i = 0; i < len; i++) { containers[i].clearDirty(); }
                    updateTankResources();
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
