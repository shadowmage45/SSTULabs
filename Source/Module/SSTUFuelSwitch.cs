using System;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUFuelSwitch : PartModule, IPartMassModifier, IPartCostModifier
    {

        [KSPField]
        public bool addMass = true;

        [KSPField]
        public bool addCost = true;

        [KSPField(isPersistant = true)]
        public string currentTank = String.Empty;

        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        public float prefabMass;

        private SSTUTank[] tankConfigs;
        private SSTUTank baseTankConfig;
        private SSTUTank currentTankConfig;
        private bool initialized = false;        
        private float modifiedMass;
        private float modifiedCost;

        [KSPEvent(guiName ="Next Tank", guiActiveEditor = true, guiActive = false)]
        public void nextTank()
        {
            SSTUTank next = SSTUUtils.findNext(tankConfigs, m => m.name == currentTank, false);
            onTankChangedEditor(next.name, true);
        }

        private void onTankChangedEditor(String newTank, bool updateSymmetry)
        {
            currentTank = newTank;
            currentTankConfig = Array.Find(tankConfigs, m => m.name == currentTank);
            updatePartCost();
            updatePartMass();
            updatePartResources();
            if (updateSymmetry)
            {
                //TODO
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { prefabMass = part.mass; }
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        public override string GetInfo()
        {
            return base.GetInfo();
        }

        public float GetModuleMass(float defaultMass)
        {
            if (addMass) { return modifiedMass; }
            return -defaultMass + modifiedMass;
        }

        public float GetModuleCost(float defaultCost)
        {
            if (addCost) { return modifiedCost; }
            return -defaultCost + modifiedCost;
        }

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);
            if (node.HasNode("BASETANK"))
            {
                baseTankConfig = new SSTUTank(node.GetNode("BASETANK"));
            }
            ConfigNode[] tankNodes = node.GetNodes("TANK");
            int len = tankNodes.Length;
            tankConfigs = new SSTUTank[len];
            for (int i = 0; i < len; i++)
            {
                tankConfigs[i] = new SSTUTank(tankNodes[i]);
            }
            currentTankConfig = Array.Find(tankConfigs, m => m.name == currentTank);
            if (currentTankConfig==null)
            {
                currentTankConfig = tankConfigs[0];
                currentTank = currentTankConfig.name;                
                initializedResources = false;
            }
            
            updatePartCost();
            updatePartMass();

            if ((HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight) && !initializedResources)
            {
                initializedResources = true;
                updatePartResources();
            }
        }

        private void updatePartMass()
        {
            modifiedMass = currentTankConfig.getTankDryMass();
            if (baseTankConfig != null) { modifiedMass += baseTankConfig.getTankDryMass(); }
            part.mass = modifiedMass;
            if (addMass) { part.mass += prefabMass; }
        }

        private void updatePartCost()
        {
            modifiedCost = currentTankConfig.getTankDryCost() + currentTankConfig.getTankResourceCost();
            if (baseTankConfig != null)
            {
                modifiedCost += baseTankConfig.getTankDryCost() + baseTankConfig.getTankResourceCost();
            }
        }

        private void updatePartResources()
        {
            SSTUResourceList list = new SSTUResourceList();
            currentTankConfig.getResources(list);
            if (baseTankConfig != null) { baseTankConfig.getResources(list); }
            list.setResourcesToPart(part, !HighLogic.LoadedSceneIsFlight);
        }

        private class SSTUTank
        {
            public readonly String name;
            public SSTUTankFuelEntry[] fuelEntries;

            public SSTUTank(ConfigNode node)
            {
                name = node.GetStringValue("name");
                ConfigNode[] entryNodes = node.GetNodes("TANKENTRY");
                int len = entryNodes.Length;
                fuelEntries = new SSTUTankFuelEntry[len];
                for (int i = 0; i < len; i++)
                {
                    fuelEntries[i] = new SSTUTankFuelEntry(entryNodes[i]);
                }
            }

            public void getResources(SSTUResourceList list)
            {
                int len = fuelEntries.Length;
                for (int i = 0; i < len; i++)
                {
                    fuelEntries[i].addResources(list);
                }
            }

            public float getTankDryMass()
            {
                float mass = 0f;
                int len = fuelEntries.Length;
                for (int i = 0; i < len; i++)
                {
                    mass += fuelEntries[i].dryMass();
                }
                return mass;
            }

            public float getTankResourceMass()
            {
                float mass = 0f;
                int len = fuelEntries.Length;
                for (int i = 0; i < len; i++)
                {
                    mass += fuelEntries[i].resMass();
                }
                return mass;
            }

            public float getTankDryCost()
            {
                float cost = 0f;
                int len = fuelEntries.Length;
                for (int i = 0; i < len; i++)
                {
                    cost += fuelEntries[i].dryCost();
                }
                return cost;
            }

            public float getTankResourceCost()
            {
                float cost = 0f;
                int len = fuelEntries.Length;
                for (int i = 0; i < len; i++)
                {
                    cost += fuelEntries[i].resCost();
                }
                return cost;
            }
        }

        private class SSTUTankFuelEntry
        {
            public readonly String name;
            public readonly float volume;
            public readonly FuelTypeData fuelData;

            public SSTUTankFuelEntry(ConfigNode node)
            {
                name = node.GetStringValue("name");
                if (node.HasNode("FUELTYPE"))
                {
                    fuelData = new FuelTypeData(node.GetNode("FUELTYPE"));
                    if (node.HasValue("volume"))
                    {
                        volume = node.GetFloatValue("volume");
                    }
                    else if (node.HasValue("mass"))
                    {
                        float mass = node.GetFloatValue("mass");
                        volume = mass / fuelData.fuelType.tonsPerCubicMeter;
                    }
                    else if (node.HasValue("units"))
                    {
                        volume = node.GetFloatValue("units") * fuelData.fuelType.litersPerUnit * 0.001f;
                    }
                }
                else
                {
                    PartResourceDefinition prd = PartResourceLibrary.Instance.GetDefinition(name);
                    float litersPerUnit = FuelTypes.INSTANCE.getResourceVolume(name);
                    float density = prd.density;
                    if (node.HasValue("volume"))
                    {
                        volume = node.GetFloatValue("volume");
                    }
                    else if (node.HasValue("mass"))
                    {
                        float mass = node.GetFloatValue("mass");
                        volume = (mass / density) * litersPerUnit * 0.001f;
                    }
                    else if (node.HasValue("units"))
                    {
                        volume = node.GetFloatValue("units") * litersPerUnit * 0.001f;
                    }
                }
            }

            public void addResources(SSTUResourceList list)
            {
                if (fuelData == null)
                {
                    //TODO
                }
                else
                {
                    fuelData.addResources(volume, list);
                }
            }

            public float dryCost() { return fuelData == null ? 0 : fuelData.getDryCost(volume); }
            public float resCost() { return fuelData == null ? 0 : fuelData.getResourceCost(volume); }
            public float dryMass() { return fuelData == null ? 0 : fuelData.getTankageMass(volume); }
            public float resMass() { return fuelData == null ? 0 : fuelData.getResourceMass(volume); }
        }
    }
}
