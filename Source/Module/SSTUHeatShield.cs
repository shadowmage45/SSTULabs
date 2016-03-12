using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUHeatShield : PartModule
    {
        [KSPField]
        public String resourceName = "Ablator";

        [KSPField]
        public Vector3 heatShieldVector = Vector3.down;
        
        [KSPField]
        public float ablationStartTemp = 500f;
        
        [KSPField]
        public float heatShieldMinDot = 0.2f;

        [KSPField]
        public float heatShieldMaxDot = 0.8f;

        [KSPField]
        public float ablationEfficiency = 6000f;

        [KSPField]
        public float ablationMult = 1f;

        [KSPField]
        public float fluxMult = 1f;

        [KSPField]
        public bool heatSoak = false;
        
        [KSPField]
        public FloatCurve heatCurve;

        

        [KSPField(guiActive =true, guiName ="HS Flux")]
        public double guiShieldFlux = 0;
        [KSPField(guiActive = true, guiName = "HS Use")]
        public double guiShieldUse = 0;
        [KSPField(guiActive = true, guiName = "HS Temp")]
        public double guiShieldTemp = 0;
        [KSPField(guiActive = true, guiName = "HS Eff")]
        public double guiShieldEff = 0;

        private double baseSkinIntMult = 1;
        private double useToFluxMultiplier = 1;
        private PartResource resource;
        private MaterialColorUpdater mcu;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            initialize();
        }

        public override void OnAwake()
        {
            base.OnAwake();
            if (heatCurve == null)
            {
                heatCurve = new FloatCurve();
                heatCurve.Add(0, 0.00002f);//very minimal initial ablation factor
                heatCurve.Add(50, 0.00005f);//ramp it up fairly quickly though
                heatCurve.Add(150, 0.00015f);
                heatCurve.Add(500, 0.00050f);
                heatCurve.Add(750, 0.00075f);
                heatCurve.Add(1000, 0.00100f);
                heatCurve.Add(2000, 0.00400f);
                heatCurve.Add(3000, 0.00800f);//generally, things will explode before this point
                heatCurve.Add(10000, 0.05000f);//but just in case, continue the curve up to insane levels
            }
        }

        private void initialize()
        {
            mcu = new MaterialColorUpdater(part.transform.FindRecursive("model"), PhysicsGlobals.TemperaturePropertyID);
            double hsp = 1;
            double dens = 1;
            if (heatSoak)
            {
                PartResourceDefinition resource = PartResourceLibrary.Instance.GetDefinition(resourceName);
                hsp = resource.specificHeatCapacity;
                dens = resource.density;
            }
            else
            {
                resource = part.Resources[resourceName];
                if (resource != null)
                {
                    hsp = resource.info.specificHeatCapacity;
                    dens = resource.info.density;
                }
                else
                {
                    hsp = PhysicsGlobals.StandardSpecificHeatCapacity;
                    dens = 0.005f;
                }
            }
            useToFluxMultiplier = hsp * ablationEfficiency * dens * ablationMult;
            baseSkinIntMult = part.skinInternalConductionMult;
        }

        public void FixedUpdate()
        {
            guiShieldTemp = part.skinTemperature;
            guiShieldFlux = 0;
            guiShieldUse = 0;
            guiShieldEff = 0;
            part.skinInternalConductionMult = baseSkinIntMult;
            updateDebugGuiStatus();
            if (!HighLogic.LoadedSceneIsFlight) { return; }            
            if (part.atmDensity <= 0) { return; }
            if (part.temperature > part.skinTemperature) { return; }

            Vector3 localFlightDirection = -part.dragVectorDirLocal;
            float dot = Vector3.Dot(heatShieldVector, localFlightDirection);
            if (dot < heatShieldMinDot) { return; }
            //TODO check for occlusion

            float directionalEffectiveness = 0;
            if (dot > heatShieldMaxDot)
            {
                directionalEffectiveness = 1f;
            }
            else
            {
                float minMaxDelta = heatShieldMaxDot - heatShieldMinDot;
                float offset = dot - heatShieldMinDot;
                directionalEffectiveness = offset / minMaxDelta;
            }
            guiShieldEff = directionalEffectiveness;
            float mult = (float)baseSkinIntMult * (1.0f - (0.8f * directionalEffectiveness));
            part.skinInternalConductionMult = mult;
            if (part.skinTemperature > ablationStartTemp)
            {
                double d = part.skinTemperature - ablationStartTemp;
                applyAblation(d, directionalEffectiveness);
            }
        }

        private void updateDebugGuiStatus()
        {
            bool active = PhysicsGlobals.ThermalDataDisplay;
            Fields["guiShieldTemp"].guiActive = active;
            Fields["guiShieldFlux"].guiActive = active;
            Fields["guiShieldUse"].guiActive = active;
            Fields["guiShieldEff"].guiActive = active;
        }

        private void applyAblation(double tempDelta, float effectiveness)
        {
            double skinMass = part.skinThermalMass;
            double maxFluxRemoved = heatCurve.Evaluate((float)tempDelta) * fluxMult * effectiveness;
            if (heatSoak)
            {
                part.AddExposedThermalFlux(-maxFluxRemoved);
                guiShieldFlux = maxFluxRemoved;
                guiShieldUse = 0;
            }
            else
            {
                double maxResourceUsed = maxFluxRemoved / useToFluxMultiplier;
                maxResourceUsed *= TimeWarp.fixedDeltaTime; //convert to a per-tick usage amount
                if (maxResourceUsed > resource.amount)
                {
                    maxResourceUsed = resource.amount;
                    maxFluxRemoved = maxResourceUsed * useToFluxMultiplier;
                }
                part.TransferResource(resource, -maxResourceUsed);
                part.AddExposedThermalFlux(-maxFluxRemoved);
                guiShieldFlux = maxFluxRemoved;
                guiShieldUse = maxResourceUsed;
            }
        }

        //hack to fix 'glowing parts' when heatshield is really the only thing that should be glowing
        public void LateUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (guiShieldEff > 0)
                {
                    mcu.Update(Color.black);
                }
            }
        }

    }
}
