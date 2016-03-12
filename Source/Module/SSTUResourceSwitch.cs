using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUResourceSwitch : PartModule, IPartCostModifier//, IPartMassModifier
    {
        //example tank definitions
        //
        //TANK
        //{
        //	tankName = Ore
        //	tankDryMass = 0.1
        //	tankCost = 500
        //	TANKRESOURCE
        //	{
        //		name=Ore
        //		amount=10
        //		fillAmount=10
        //	}
        //}
        //
        //TANK
        //{
        //	tankName = LFO
        //	tankDryMass = 0.1
        //	tankCost = 500
        //	TANKRESOURCE
        //	{
        //		name=LiquidFuel
        //		amount=90
        //		fillAmount=90
        //	}
        //	TANKRESOURCE
        //	{
        //		name=Oxidizer
        //		amount=110
        //		fillAmount=110
        //	}
        //}

        //used to track the current tank type from those loaded from config, mostly used in editor
        [KSPField(isPersistant = true)]
        public int tankType = -1;

        [KSPField(isPersistant = true)]
        public int optionType = -1;

        [KSPField(guiActiveEditor = true, guiName = "Tank Type", guiActive = true)]
        public String tankTypeName = "NONE";

        [KSPField(guiActiveEditor = false, guiName = "Tank Option Type", guiActive = false)]
        public String tankOptionName = String.Empty;

        [KSPField]
        public String defaultTankName = String.Empty;

        //is being controlled by SSTUMeshSwitch? (or other...)
        //if true, disables automatic loading of tank type and allows mesh-switch to specify tank type
        //if not set properly will result in undefined behavior
        [KSPField]
        public bool externalControl = false;

        [KSPField]
        public float defaultTankCost = 0;

        [KSPField]
        public float defaultTankMass = 0;

        [KSPField(isPersistant = true)]
        public float persistentCost;

        [KSPField(isPersistant = true)]
        public float persistentMass;
        
        private TankConfig[] configs;
        private TankConfig[] optionConfigs;
        private TankConfig currentConfig;//main configuration; primary resources
        private TankConfig currentOption;//additional optional configuration; secondary resources; e.g. a monoprop secondary tank or battery bank, none are enabled by default

        public SSTUResourceSwitch()
        {

        }

        [KSPEvent(name = "nextTankEvent", guiName = "Next Tank", guiActiveEditor = true)]
        public void nextTankEvent()
        {
            tankType++;
            if (tankType >= configs.Length) { tankType = 0; }
            updateTankStats();
            updateResources();
        }

        [KSPEvent(name = "prevTankEvent", guiName = "Prev. Tank", guiActiveEditor = true)]
        public void prevTankEvent()
        {
            tankType--;
            if (tankType < 0) { tankType = configs.Length - 1; }
            updateTankStats();
            updateResources();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            //only runs for prefab construction, e.g. loading screen or database reload while on space center screen
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
            {
                loadConfigFromNode(node);
            }
            else
            {
                loadTankConfigsFromPrefab();
                if (persistentMass > 0) { part.mass = persistentMass; }
            }
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            loadTankConfigsFromPrefab();
            if (!externalControl)
            {
                if (tankType == -1)//only init default tank if first time part is ever setup
                {
                    initDefaultTank();//loads the default tank, updates resources and part mass					
                }
                else
                {
                    updateTankStats();//this will reload all of the GUI data/etc, but not reset part resources
                }
            }
            else
            {
                if (tankType >= 0)//tank was already initialized, external control should -NOT- re-set tank type
                {
                    updateTankStats();
                }
                else
                {
                    //allow for external control to set things as it sees fit; this then becomes a passive module for data-storage and method sharing...	
                }
            }
            if (configs.Length <= 1 || externalControl)
            {
                Events["nextTankEvent"].active = false;
                Events["prevTankEvent"].active = false;
                Fields["tankTypeName"].guiActive = false;
                Fields["tankTypeName"].guiActiveEditor = false;
            }
        }

        //runs only on prefab part, used for editor info initialization
        public override string GetInfo()
        {
            initDefaultTank();
            return base.GetInfo();
        }

        private void loadTankConfigsFromPrefab()
        {
            loadConfigFromNode(SSTUStockInterop.getPartModuleConfig(part, this));
        }

        //OVERRIDE from IPartCostModifier
        //return offset/modifier to the part cost, input is the cost listed in the config
        public float GetModuleCost(float defaultCost)
        {
            return persistentCost;
        }

        //OVERRIDE from IPartMassModifier
        //return offset/modifier to the part mass, input is the mass listed in config
        public float GetModuleMass(float defaultMass)
        {
            return persistentMass;
        }

        public void setTankMainConfig(String tankName)
        {
            int len = configs.Length;
            for (int i = 0; i < len; i++)
            {
                if (configs[i].tankName.Equals(tankName))
                {
                    tankType = i;
                    updateTankStats();
                    updateResources();
                    return;
                }
            }
            //will only hit here if tank not found
            tankType = 0;
            updateTankStats();
            updateResources();
        }

        //external control method to set option type by name
        public void setTankOption(String optionName)
        {
            TankConfig optionConfig = null;
            int index = 0;
            int length = optionConfigs.Length;
            for (index = 0; index < length; index++)
            {
                if (optionConfigs[index].tankName.Equals(optionName))
                {
                    optionConfig = optionConfigs[index];
                    break;
                }
            }
            setTankOption(optionConfig == null ? -1 : index);
        }

        public void clearTankOption()
        {
            setTankOption(-1);
        }

        private void setTankOption(int index)
        {
            optionType = index;
            updateTankStats();
            updateResources();
        }

        /// <summary>
        /// Inits the default tank. To be used by the prefab (in getInfo()), and ONCE on first editor load.
        /// </summary>
        private void initDefaultTank()
        {
            if (defaultTankName.Length > 0)
            {
                setTankMainConfig(defaultTankName);
            }
            else
            {
                tankType = 0;
                updateTankStats();
                updateResources();
            }
        }

        /// <summary>
        /// Updates current config and option references from index as well as updating gui name references
        /// </summary>
        private void updateTankStats()
        {
            currentConfig = configs[tankType];
            currentOption = optionType == -1 ? null : optionConfigs[optionType];
            tankTypeName = currentConfig.tankName;
            tankOptionName = currentOption == null ? String.Empty : currentOption.tankName;
            persistentCost = currentConfig == null ? defaultTankCost : currentConfig.tankCost;
            persistentCost += currentOption == null ? 0 : currentOption.tankCost;
            persistentMass = currentConfig == null ? defaultTankMass : currentConfig.tankDryMass;
            persistentMass += currentOption == null ? 0 : currentOption.tankDryMass;
            part.mass = persistentMass;
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
            else if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.onVesselWasModified.Fire(part.vessel);
            }
        }

        /// <summary>
        /// Updates the parts resources from the current config and option config (if any).
        /// </summary>
        private void updateResources()
        {
            part.Resources.list.Clear();
            PartResource[] resources = part.GetComponents<PartResource>();
            int len = resources.Length;
            for (int i = 0; i < len; i++)
            {
                DestroyImmediate(resources[i]);
            }
            if (currentConfig != null)
            {
                foreach (ConfigNode node in currentConfig.getResourceConfigNodes())
                {
                    part.AddResource(node);
                }
            }
            if (currentOption != null)
            {
                foreach (ConfigNode node in currentOption.getResourceConfigNodes())
                {
                    part.AddResource(node);
                }
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
            else if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.onVesselWasModified.Fire(part.vessel);
            }
        }

        private void loadConfigFromNode(ConfigNode node)
        {
            if (node.HasNode("TANK"))
            {
                ConfigNode[] tankNodes = node.GetNodes("TANK");
                List<TankConfig> tanks = new List<TankConfig>();
                List<TankConfig> optionTanks = new List<TankConfig>();
                TankConfig tank;
                foreach (ConfigNode n2 in tankNodes)
                {
                    tank = parseTankConfig(n2);
                    if (tank != null)
                    {
                        if (tank.isOption)
                        {
                            optionTanks.Add(tank);
                        }
                        else
                        {
                            tanks.Add(tank);
                        }
                    }
                }
                if (tanks.Count == 0) { tanks.Add(TankConfig.STRUCTURAL); }
                configs = tanks.ToArray();
                optionConfigs = optionTanks.ToArray();
            }
        }

        private TankConfig parseTankConfig(ConfigNode node)
        {
            return new TankConfig(node, defaultTankCost, defaultTankMass);
        }
    }

    public class TankConfig
    {
        public static TankConfig STRUCTURAL;
        public String tankName;
        public float tankCost = 0;
        public float tankDryMass = 0;
        public bool isOption = false;
        private float tankResourceCost = -1f;
        List<TankResourceConfig> tankResourceConfigs = new List<TankResourceConfig>();

        static TankConfig()
        {
            STRUCTURAL = new TankConfig();
            STRUCTURAL.tankName = "Structural";
            STRUCTURAL.tankDryMass = 0.125f;
            STRUCTURAL.tankCost = 1200f;
            STRUCTURAL.tankResourceCost = 0f;
        }

        private TankConfig() { }

        public TankConfig(ConfigNode node, float defaultCost, float defaultMass)
        {
            tankName = node.GetValue("tankName");
            if (node.HasValue("tankCost")) { tankCost = (float)SSTUUtils.safeParseDouble(node.GetValue("tankCost")); }
            else { tankCost = defaultCost; }
            if (node.HasValue("tankDryMass")) { tankDryMass = (float)SSTUUtils.safeParseDouble(node.GetValue("tankDryMass")); }
            else { tankDryMass = defaultMass; }
            if (node.HasValue("isOption")) { isOption = Boolean.Parse(node.GetValue("isOption")); }
            ConfigNode[] tanks = node.GetNodes("TANKRESOURCE");
            foreach (ConfigNode tcn in tanks) { loadTankConfigNode(tcn); }
        }

        private void loadTankConfigNode(ConfigNode node)
        {
            TankResourceConfig cfg = new TankResourceConfig();
            cfg.resourceName = node.GetValue("resource");
            cfg.amount = (float)SSTUUtils.safeParseDouble(node.GetValue("amount"));
            cfg.fillAmount = (float)SSTUUtils.safeParseDouble(node.GetValue("fillAmount"));
            tankResourceConfigs.Add(cfg);
        }

        public ConfigNode[] getResourceConfigNodes()
        {
            int length = tankResourceConfigs.Count;
            ConfigNode[] nodes = new ConfigNode[length];
            for (int i = 0; i < length; i++)
            {
                nodes[i] = tankResourceConfigs[i].getResourceConfigNode();
            }
            return nodes;
        }

        public override string ToString()
        {
            return string.Format(
                "[TankConfig:   " + tankName + "]\n" +
                "TankCost:      " + tankCost + "\n" +
                "TankMass:      " + tankDryMass + "\n" +
                "Resources: \n" + getResourcesString());
        }

        public float getResourceCost()
        {
            if (tankResourceCost == -1)
            {
                tankResourceCost = 0;
                PartResourceDefinition def;
                foreach (TankResourceConfig cfg in tankResourceConfigs)
                {
                    if (PartResourceLibrary.Instance.resourceDefinitions.Contains(cfg.resourceName))
                    {
                        def = PartResourceLibrary.Instance.GetDefinition(cfg.resourceName);
                        tankResourceCost += def.unitCost * cfg.fillAmount;
                    }
                }
            }
            return tankResourceCost;
        }

        private String getResourcesString()
        {
            return SSTUUtils.printList(tankResourceConfigs, "\n");
        }
    }

    public class TankResourceConfig
    {
        public String resourceName;
        public float amount;
        public float fillAmount;

        public ConfigNode getResourceConfigNode()
        {
            ConfigNode node = new ConfigNode("RESOURCE");
            node.AddValue("name", resourceName);
            node.AddValue("maxAmount", amount);
            node.AddValue("amount", fillAmount);
            return node;
        }

        public override string ToString()
        {
            return string.Format("[TankResource: " + resourceName + " - " + fillAmount + " / " + amount + "]");
        }
    }
}

