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
        public String nodeNames = "bottom";

        [KSPField]
        public float maxConductivity = 1f;

        [KSPField]
        public float minConductivity = 0.001f;

        [KSPField]
        public float ablationStartTemp = 500f;

        [KSPField]
        public float ablationPeakTemp = 1100f;

        [KSPField]
        public float heatShieldMinDot = 0.2f;

        [KSPField]
        public float heatShieldMaxDot = 0.8f;

        [KSPField]
        public float minShieldTemp = 200;             

        [KSPField]
        public float ablationEfficiency = 6000f;
        
        [KSPField]
        public float heatShieldDotFalloff;//falloff curve for heat shield efficiency relative to the oncoming direction of drag

        [KSPField]
        public float resourceSpecificHeat = 0;

        [KSPField]
        public bool debug = false;

        [KSPField(isPersistant = true)]
        public double heatShieldTemp = 0;

        public FloatCurve heatCurve;

        private double baseSkinIntMult = 1;
        private double maxResourceMass;
        private double currentResourceMass;
        private PartResource resource;
        private String[] attachNodeNames;

        [KSPEvent(guiName ="Debug", guiActive =true, guiActiveEditor =true)]
        public void debugEvent()
        {
            debug = !debug;
        }
        
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
            heatCurve = new FloatCurve();
            heatCurve.Add(    0, 0.00002f);//very minimal initial ablation factor
            heatCurve.Add(   50, 0.00005f);//ramp it up fairly quickly though
            heatCurve.Add(  150, 0.00010f);
            heatCurve.Add(  750, 0.00015f);
            heatCurve.Add( 1000, 0.00020f);
            heatCurve.Add( 2000, 0.00050f);
            heatCurve.Add( 3000, 0.00100f);//generally, things will explode before this point
            heatCurve.Add(10000, 0.00200f);//but just in case, continue the curve up to insane levels
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) { return; }
            Vector3 localFlightDirection = -part.dragVectorDirLocal;
            float dot = Vector3.Dot(heatShieldVector, localFlightDirection);
            if (dot > heatShieldMinDot)
            {
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
                if (part.skinTemperature > ablationStartTemp)
                {
                    part.skinInternalConductionMult = Mathf.Clamp(1.2f - directionalEffectiveness, 0, 1);
                    double heatPerUnit = resourceSpecificHeat * ablationEfficiency;
                    double delta = part.skinTemperature - ablationStartTemp;
                    double use = heatCurve.Evaluate((float)delta) * delta * directionalEffectiveness;
                    if(debug)print("delta: " + delta + " use: " + use);
                    double actualUse = use * TimeWarp.fixedDeltaTime;
                    if (debug) print("acutal use: " + actualUse);
                    if (actualUse > resource.amount) { actualUse = resource.amount; use = actualUse / TimeWarp.fixedDeltaTime; }
                    part.TransferResource(resource, -actualUse);
                    double flux = use * heatPerUnit * resource.info.density;
                    if (debug) print("heatPerUnit: " + heatPerUnit);
                    if (debug) print("flux: " + flux);
                    part.AddExposedThermalFlux(-flux);
                }
                else
                {
                    part.skinInternalConductionMult = Mathf.Clamp(1.5f - directionalEffectiveness, 0, 1);
                }
            }
            else
            {
                part.skinInternalConductionMult = baseSkinIntMult;
            }
        }

        private void initialize()
        {
            resource = part.Resources[resourceName];
            float hsp = resource.info.specificHeatCapacity;
            if (resourceSpecificHeat == 0)
            {
                resourceSpecificHeat = hsp;
            }
            maxResourceMass = resource.info.density * resource.maxAmount;
            baseSkinIntMult = part.skinInternalConductionMult;
        }

    }
}
