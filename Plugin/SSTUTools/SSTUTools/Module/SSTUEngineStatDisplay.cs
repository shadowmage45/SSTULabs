using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSTUTools
{
    public class SSTUEngineStatDisplay : PartModule
    {

        [KSPField]
        public int engineModuleIndex = 0;

        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "Thrust")]
        public string thrustGuiDisplay;

        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "V-ISP", guiUnits = "s")]
        public string ispGuiDisplay;

        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "Mass Flow", guiUnits = "t/s")]
        public string fuelFlowGuiDisplay;

        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "Burn Time", guiUnits = "s")]
        public string burnTimeGuiDisplay;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(editorVesselModified));
        }

        public void Start()
        {
            updateStats();
        }

        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(editorVesselModified));
        }

        private void editorVesselModified(ShipConstruct ship)
        {
            updateStats();
        }

        private void updateStats()
        {
            ModuleEngines[] engines = part.GetComponents<ModuleEngines>();
            if (engines == null || engines.Length < 1 || engineModuleIndex < 0 || engineModuleIndex >= engines.Length)
            {
                //no valid engine module; set UI values to default/0, and disable UI fields
                return;
            }
            ModuleEngines engine = engines[engineModuleIndex];

            //derivation of fuel mass flow from isp and thrust, from expression of thrust from ISP (t = g * i * m)
            //t = thrust(kn), g = g0(m/s), i = isp(s), m = massflowrate(t/s)
            //t = g * i * m //basic definition
            //t = m * i * g //commutative re-arrangement
            //t/i/g = m //isolate mass flow

            float fuelMass = getEnginePropellantMass(engine);
            float ispValue = engine.atmosphereCurve.Evaluate(0);
            float delta = engine.maxThrust - engine.minThrust;
            float limiter = engine.thrustPercentage * 0.01f;
            float thrust = engine.minThrust + limiter * delta;
            float massFlow = thrust / ispValue / 9.81f;//m = t/i/g
            float burnTime = (fuelMass / massFlow);

            this.ispGuiDisplay = ispValue.ToString();
            this.thrustGuiDisplay = thrust.ToString();
            this.burnTimeGuiDisplay = burnTime.ToString();
            this.fuelFlowGuiDisplay = (engine.maxThrust / ispValue).ToString();

            //old code from SSTUModularBooster
            //if (engineModule != null)
            //{
            //    string prop = engineModule.propellants[0].name;
            //    PartResource res = part.Resources[prop];
            //    double propMass = res.info.density * res.amount;
            //    float delta = engineModule.maxThrust - engineModule.minThrust;
            //    float limiter = engineModule.thrustPercentage * 0.01f;
            //    guiThrust = engineModule.minThrust + delta * limiter;
            //    float limit = guiThrust / engineModule.maxThrust;
            //    guiBurnTime = (float)(propMass / engineModule.maxFuelFlow) / limit;
            //}
        }

        //TODO should be expanded to search the entire vessel during flight (respecting stage/flow setup)
        //TODO should be expanded to search the entire ShipConstruct in the editor, respecting stage flow
        private float getEnginePropellantMass(ModuleEngines engine)
        {
            float fuelMass = 0;
            if (engine.propellants != null && engine.propellants.Count > 0)
            {
                int len = engine.propellants.Count;
                for (int i = 0; i < len; i++)
                {
                    string propName = engine.propellants[0].name;
                    PartResource pr = part.Resources[propName];
                    if (pr != null)
                    {
                        fuelMass += (float)(pr.info.density * pr.amount);
                    }
                }
            }
            return fuelMass;
        }

    }
}
