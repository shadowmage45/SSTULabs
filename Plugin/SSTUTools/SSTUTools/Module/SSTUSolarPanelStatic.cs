using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace SSTUTools
{

    //Multi-panel solar panel module, each with own suncatcher and occlusion checks
    //Solar panel energy update code based loosely from stock code
    public class SSTUSolarPanelStatic : PartModule, IContractObjectiveModule
    {

        //config field, should contain CSV of transform names for ray cast checks
        [KSPField(isPersistant = false)]
        public String suncatcherTransforms = string.Empty;

        [KSPField(isPersistant = false)]
        public String resourceName = "ElectricCharge";

        [KSPField(isPersistant = false)]
        public float resourceAmount = 3.0f;

        [KSPField(isPersistant = false)]
        public FloatCurve temperatureEfficCurve;


        //BELOW HERE ARE NON-CONFIG EDITABLE FIELDS

        //Status displayed for panel state, includes animation state and energy state;  Using in place of the three-line output from stock panels
        [KSPField(isPersistant = false, guiName = "S.P.", guiActive = true)]
        public String guiStatus = "unknown";

        //parsed list of suncatching ray transform names
        private List<String> suncatcherNames = new List<String>();

        //list of panel data (pivot and ray transform, and cached angles/etc needed for each)
        private List<Transform> panelData = new List<Transform>();

        //cached energy flow value, used to update gui
        private float energyFlow = 0.0f;

        private String occluderName = String.Empty;

        private Transform sunTransform;

        public SSTUSolarPanelStatic()
        {
            //the default stock temperatureEfficiencyCurve
            this.temperatureEfficCurve = new FloatCurve();
            this.temperatureEfficCurve.Add(4f, 1.2f, 0f, -0.0005725837f);
            this.temperatureEfficCurve.Add(300f, 1f, -0.0008277721f, -0.0008277721f);
            this.temperatureEfficCurve.Add(1200f, 0.1f, -0.0003626566f, -0.0003626566f);
            this.temperatureEfficCurve.Add(2500f, 0.01f, 0f, 0f);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            parseTransformData();
            findTransforms();
            updateGuiData();
        }

        public void FixedUpdate()
        {
            updatePowerStatus();
            updateGuiData();
        }

        public string GetContractObjectiveType()
        {
            return "Generator";
        }

        public bool CheckContractObjectiveValidity()
        {
            return true;
        }

        //update power status every tick if panels are extended (and not broken)
        private void updatePowerStatus()
        {
            energyFlow = 0.0f;
            occluderName = String.Empty;
            if (!HighLogic.LoadedSceneIsFlight)//only update power if in active flight
            {
                return;
            }
            CelestialBody sun = FlightGlobals.Bodies[0];
            sunTransform = sun.transform;
            foreach (Transform pd in panelData)
            {
                updatePanelPower(pd);
            }
            if (energyFlow > 0)
            {
                part.RequestResource(resourceName, -energyFlow);
            }
        }

        private void updatePanelPower(Transform pd)
        {
            if (!isOccludedByPart(pd))
            {
                Vector3 normalized = (sunTransform.position - pd.position).normalized;

                float sunAOA = Mathf.Clamp(Vector3.Dot(pd.forward, normalized), 0f, 1f);
                float distMult = (float)(base.vessel.solarFlux / PhysicsGlobals.SolarLuminosityAtHome);

                if (distMult == 0 && FlightGlobals.currentMainBody != null)//vessel.solarFlux == 0, so occluded by a planetary body
                {
                    occluderName = FlightGlobals.currentMainBody.name;//just guessing..but might be occluded by the body we are orbiting?
                }

                float efficMult = this.temperatureEfficCurve.Evaluate((float)base.part.temperature);
                energyFlow += resourceAmount * TimeWarp.fixedDeltaTime * sunAOA * distMult * efficMult;
            }
        }

        //does a very short raycast for vessel/part occlusion checking
        //rely on stock thermal input data for body occlusion checks
        private bool isOccludedByPart(Transform tr)
        {
            RaycastHit hit;
            if (Physics.Raycast(tr.position, (sunTransform.position - tr.position).normalized, out hit, 300f))
            {
                occluderName = hit.transform.gameObject.name;
                return true;
            }
            return false;
        }

        //parses the rayTransforms and pivotTransforms names into lists
        private void parseTransformData()
        {
            suncatcherNames.Clear();
            String[] suncatcherNamesTempArray = suncatcherTransforms.Split(',');
            for (int i = 0; i < suncatcherNamesTempArray.Length; i++) { suncatcherNames.Add(suncatcherNamesTempArray[i].Trim()); }
        }

        //loads transforms from model given the transform names specified in config
        private void findTransforms()
        {
            panelData.Clear();
            String suncatcherName;
            Transform suncatcherTransform;
            int length = suncatcherNames.Count;//lists -should- be the same size, or there will be problems
            for (int i = 0; i < length; i++)
            {
                suncatcherName = suncatcherNames[i];
                suncatcherTransform = part.FindModelTransform(suncatcherName);
                if (suncatcherTransform == null)
                {
                    print("ERROR: null transform found for names.. " + suncatcherName);
                    continue;
                }
                panelData.Add(suncatcherTransform);
            }
        }

        //updates GUI information and buttons from panel state and energy flow values
        private void updateGuiData()
        {
            if (energyFlow == 0 && occluderName.Length > 0)//if fully occluded, state that information first
            {
                guiStatus = "OCC: " + occluderName;
            }
            else
            {
                guiStatus = String.Format("{0:F1}", (energyFlow * (1 / TimeWarp.fixedDeltaTime))) + " e/s";
            }
        }
    }
}

