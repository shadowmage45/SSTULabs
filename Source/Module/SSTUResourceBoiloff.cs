using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUResourceBoiloff : PartModule
    {

        [KSPField]
        public float boiloffLossModifier = 1;
        
        [KSPField]
        public float activeInsulationPercent = 0f;

        [KSPField]
        public float activeECCost = 1f;

        [KSPField]
        public float activeInsulationPrevention = 1f;

        [KSPField]
        public float inactiveInsulationPrevention = 0f;

        [KSPField]
        public float passiveInsulationPrevention = 0f;

        [KSPField(guiActive = true, guiName = "BoiloffLoss", guiUnits = "l/s")]
        public float guiVolumeLoss = 0f;

        [KSPField(guiActive = true, guiName = "CoolingCost", guiUnits = "ec/s")]
        public float guiECCost = 0f;

        [KSPField(isPersistant = true)]
        public double lastUpdateTime = -1;

        private BoiloffResourceData[] boiloffData;

        //NOOP?
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }
        
        /// <summary>
        /// Run init sequence, create boiloff data instances for each boiloff-enabled resource in the part; caching references to all relevant data
        /// </summary>
        /// <param name="state"></param>
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }
        
        /// <summary>
        /// Update the boiloff stats from the current VolumeContainer, if available, else use the stats from module config
        /// </summary>
        public void Start()
        {
            updateStatsFromContainer();
        }
        
        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || boiloffData.Length <= 0 || boiloffLossModifier <= 0)
            {
                return;
            }
            int len = boiloffData.Length;
            double universeTime = Planetarium.GetUniversalTime();
            double delta = lastUpdateTime >= 0 ? universeTime - lastUpdateTime : 0;
            lastUpdateTime = universeTime;

            guiVolumeLoss = 0;
            guiECCost = 0;
            if (delta > 0)
            {
                for (int i = 0; i < len; i++)
                {
                    boiloffData[i].processBoiloff(part, delta);
                    guiVolumeLoss += boiloffData[i].volumeLost;
                    guiECCost += boiloffData[i].ecCost;
                }
            }
        }
        
        private void initialize()
        {
            List<BoiloffResourceData> list = new List<BoiloffResourceData>();
            BoiloffData data;
            foreach (PartResource res in part.Resources)
            {
                data = FuelTypes.INSTANCE.getResourceBoiloffValue(res.resourceName);
                if (data != null)
                {
                    list.Add(new BoiloffResourceData(res, data));
                }
            }
            boiloffData = list.ToArray();
            if (boiloffData.Length == 0)
            {
                Fields["guiVolumeLoss"].guiActive = false;
                Fields["guiECCost"].guiActive = false;
            }
        }
        
        private void updateStatsFromContainer()
        {
            int len = boiloffData.Length;
            SSTUVolumeContainer container = part.GetComponent<SSTUVolumeContainer>();
            if (container != null)
            {
                for (int i = 0; i < len; i++)
                {
                    boiloffData[i].setFromContainer(container);
                }
            }
            else
            {
                for (int i = 0; i < len; i++)
                {
                    boiloffData[i].boiloffModifier = boiloffLossModifier;
                    boiloffData[i].activeInsulationPercent = activeInsulationPercent;
                    boiloffData[i].activeECCost = activeECCost;
                    boiloffData[i].activeInsulationPrevention = activeInsulationPrevention;
                    boiloffData[i].inactiveInsulationPrevention = inactiveInsulationPrevention;
                    boiloffData[i].passiveInsulationPrevention = passiveInsulationPrevention;
                }
            }
        }

        //TODO - not going to be called from anywhere; need to possibly subscribe to events... 
        // or... who knows;
        // only need to know when resources are upated in-flight, which should generally never happen
        private void onPartResourcesChanged()
        {
            initialize();
            updateStatsFromContainer();
        }
        
    }

    public class BoiloffResourceData
    {
        public readonly PartResource resource;
        public readonly BoiloffData data;
        public readonly float unitVolume;

        public float boiloffModifier = 1f;
        public float activeInsulationPercent = 0f;        
        public float activeECCost = 0f;        
        public float activeInsulationPrevention = 1f;        
        public float inactiveInsulationPrevention = 0f;        
        public float passiveInsulationPrevention = 0f;

        public float volumeLost;
        public float ecCost;

        public BoiloffResourceData(PartResource resource, BoiloffData data)
        {
            this.resource = resource;
            this.data = data;
            this.unitVolume = FuelTypes.INSTANCE.getResourceVolume(resource.resourceName);
        }
        
        public void setFromContainer(SSTUVolumeContainer container)
        {
            ContainerDefinition highestVolume = container.highestVolumeContainer(data.name);
            if (container == null)
            {
                MonoBehaviour.print("ERROR: Could not locate volume container definition for resource: " + data.name);
            }
            setFromContainer(highestVolume.currentModifier);
        }

        public void setFromContainer(ContainerModifier mod)
        {
            boiloffModifier = mod.boiloffModifier;
            activeInsulationPercent = mod.activeInsulationPercent;
            activeECCost = mod.activeECCost;
            activeInsulationPrevention = mod.activeInsulationPrevention;
            inactiveInsulationPrevention = mod.inactiveInsulationPrevention;
            passiveInsulationPrevention = mod.passiveInsulationPrevention;
        }
        
        public void processBoiloff(Part part, double seconds)
        {            
            double hours = seconds / 3600d;//convert from delta-seconds into delta-hours...
            double resourceVolume = resource.amount * unitVolume;
            double totalLoss = data.value * resourceVolume * hours * boiloffModifier;
            double activePrevention = totalLoss * activeInsulationPrevention * activeInsulationPercent;
            double inactivePrevention = 0f;
            double activePreventionCost = activePrevention * activeECCost * data.cost;
            if (activePreventionCost > 0.000005)
            {
                double activeECUsed = part.RequestResource("ElectricCharge", activePreventionCost);
                if (activeECUsed < activePreventionCost)
                {
                    double activePercent = activeECUsed / activePreventionCost;
                    inactivePrevention = activePrevention - (activePercent * activePrevention);
                    inactivePrevention *= totalLoss * inactiveInsulationPrevention;
                    activePrevention = activePrevention * activePercent;
                    activePreventionCost = activePercent * activePreventionCost;//only used XXX for this tick, though likely won't display long enough on the GUI to matter...
                }
            }
            double passivePrevention = totalLoss * passiveInsulationPrevention * (1.0 - activeInsulationPercent);
            double totalPrevention = activePrevention + inactivePrevention + passivePrevention;            
            double actualLoss = totalLoss - totalPrevention;
            if (actualLoss > 0.000005)
            {
                bool flowState = resource.flowState;
                resource.flowState = true;//hack to enable using the resource even when disabled
                part.RequestResource(data.name, actualLoss / unitVolume, ResourceFlowMode.NO_FLOW);//no flow to only take resources from this part
                resource.flowState = flowState;//re-hack to set resource enabled val back to previous
            }
            volumeLost = (float)actualLoss * (1 / (float)seconds);
            ecCost = (float)activePreventionCost * ( 1 / (float)seconds);
            //MonoBehaviour.print("boiloff  : " + data.name);
            //MonoBehaviour.print("volume   : " + resourceVolume);
            //MonoBehaviour.print("rawLoss  : " + totalLoss);
            //MonoBehaviour.print("actPrev  : " + activePrevention);
            //MonoBehaviour.print("inactPrev: " + inactivePrevention);
            //MonoBehaviour.print("passPrev : " + passivePrevention);
            //MonoBehaviour.print("actLoss  : " + volumeLost);
            //MonoBehaviour.print("ecCost   : " + ecCost);
        }

    }

}
