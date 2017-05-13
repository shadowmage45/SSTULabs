using System;
using System.Collections.Generic;
using UnityEngine;
namespace SSTUTools
{
    public class SSTUAnimateEngineHeat : PartModule
    {

        //amount of 'heat' added per second at full throttle
        [KSPField]
        public float heatOutput = 300;

        //amount of heat dissipated per second, adjusted by the heatDissipationCurve below
        [KSPField]
        public float heatDissipation = 100;

        //point at which the object will begin to glow
        [KSPField]
        public float draperPoint = 400;

        //maximum amount of heat allowed in this engine
        //will reach max glow at this temp, and begin dissipating even faster past this point
        [KSPField]
        public float maxHeat = 2400;

        //maxStoredHeat
        //storedHeat will not go beyond this, sets retention period for maximum glow
        [KSPField]
        public float maxStoredHeat = 3600;

        //curve to adjust heat dissipation; should generally expel heat faster when hotter
        [KSPField]
        public FloatCurve heatDissipationCurve = new FloatCurve();

        //the heat-output curve for an engine (varies with thrust/throttle), in case it is not linear
        [KSPField]
        public FloatCurve heatAccumulationCurve = new FloatCurve();

        [KSPField]
        public FloatCurve redCurve = new FloatCurve();

        [KSPField]
        public FloatCurve blueCurve = new FloatCurve();

        [KSPField]
        public FloatCurve greenCurve = new FloatCurve();

        [KSPField]
        public string engineID = "Engine";

        [KSPField]
        public String meshName = String.Empty;

        [KSPField(isPersistant = true)]
        public float currentHeat = 0;

        int shaderEmissiveID;

        private ModuleEngines engineModule;

        private Renderer[] animatedRenderers;

        private Color emissiveColor = new Color(0f, 0f, 0f, 1f);

        public override void OnAwake()
        {
            base.OnAwake();
            heatDissipationCurve.Add(0f, 0.2f);
            heatDissipationCurve.Add(1f, 1f);

            heatAccumulationCurve.Add(0f, 0f);
            heatAccumulationCurve.Add(1f, 1f);
            
            redCurve.Add(0f, 0f);
            redCurve.Add(1f, 1f);

            blueCurve.Add(0f, 0f);
            blueCurve.Add(1f, 1f);

            greenCurve.Add(0f, 0f);
            greenCurve.Add(1f, 1f);

            shaderEmissiveID = Shader.PropertyToID("_EmissiveColor");
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) { return; }
            updateHeat();
        }

        private void initialize()
        {
            locateAnimatedTransforms();
            locateEngineModule();
        }

        public void reInitialize()
        {
            animatedRenderers = null;
            engineModule = null;
            initialize();
        }

        private void locateEngineModule()
        {
            engineModule = null;
            ModuleEngines[] engines = part.GetComponents<ModuleEngines>();
            int len = engines.Length;
            for (int i = 0; i < len; i++)
            {
                if (engines[i].engineID == engineID)
                {
                    engineModule = engines[i];
                }
            }
            if (engineModule == null)
            {
                MonoBehaviour.print("ERROR: Could not locate engine by ID: " + engineID + " for part: " + part + " for SSTUAnimateEngineHeat.  This will cause errors during gameplay.  Setting engine to first engine module (if present)");
                if (engines.Length > 0) { engineModule = engines[0]; }
            }
        }

        private void locateAnimatedTransforms()
        {
            List<Renderer> renderers = new List<Renderer>();
            Transform[] animatedTransforms = part.transform.FindChildren(meshName);
            int len = animatedTransforms.Length;
            for (int i = 0; i < len; i++)
            {
                renderers.AddRange(animatedTransforms[i].GetComponentsInChildren<Renderer>(false));
            }
            animatedRenderers = renderers.ToArray();
            if (animatedRenderers == null || animatedRenderers.Length == 0) { print("ERROR: Could not locate any emissive meshes for name: " + meshName); }
        }

        private void updateHeat()
        {
            if (engineModule == null) { return; }
            //add heat from engine
            if (engineModule.EngineIgnited && !engineModule.flameout && engineModule.currentThrottle > 0)
            {
                float throttle = vessel.ctrlState.mainThrottle;
                float heatIn = heatAccumulationCurve.Evaluate(throttle) * heatOutput * TimeWarp.fixedDeltaTime;
                currentHeat += heatIn;
            }

            //dissipate heat
            float heatPercent = currentHeat / maxHeat;
            if (currentHeat > 0f)
            {
                float heatOut = heatDissipationCurve.Evaluate(heatPercent) * heatDissipation * TimeWarp.fixedDeltaTime;
                if (heatOut > currentHeat) { heatOut = currentHeat; }
                currentHeat -= heatOut;
            }
            if (currentHeat > maxStoredHeat) { currentHeat = maxStoredHeat; }

            float emissivePercent = 0f;

            float mhd = maxHeat - draperPoint;
            float chd = currentHeat - draperPoint;

            if (chd < 0f) { chd = 0f; }
            emissivePercent = chd / mhd;
            if (emissivePercent > 1f) { emissivePercent = 1f; }
            emissiveColor.r = redCurve.Evaluate(emissivePercent);
            emissiveColor.g = greenCurve.Evaluate(emissivePercent);
            emissiveColor.b = blueCurve.Evaluate(emissivePercent);
            setEmissiveColors();
        }

        private void setEmissiveColors()
        {
            if (animatedRenderers != null)
            {
                bool rebuild = false;
                int len = animatedRenderers.Length;
                for (int i = 0; i < len; i++)
                {
                    if (animatedRenderers[i] == null)
                    {
                        rebuild = true;
                        continue;
                    }
                    animatedRenderers[i].sharedMaterial.SetColor(shaderEmissiveID, emissiveColor);
                }
                if (rebuild)
                {
                    animatedRenderers = null;
                    locateAnimatedTransforms();
                }
            }
        }

    }
}

