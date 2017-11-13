using System;
using System.Collections.Generic;
using UnityEngine;
namespace SSTUTools
{

    //Multi-panel solar panel module, each with own suncatcher and pivot and occlusion checks
    //Animation code based from stock, Near-Future, and Firespitter code
    //Solar panel code based from Near-Future code originally, but has been vastly changed since the original implementation
    //solar pivots rotate around localY, to make localZ face the sun
    //e.g. y+ should point towards origin, z+ should point towards the panel solar input direction
    public class SSTUSolarPanelDeployable : PartModule, IContractObjectiveModule
    {
        //panel state enum, each represents a discrete state
        public enum SSTUPanelState
        {
            EXTENDED,
            EXTENDING,
            RETRACTED,
            RETRACTING,
            BROKEN,
        }

        public enum Axis
        {
            XPlus,
            XNeg,
            YPlus,
            YNeg,
            ZPlus,
            ZNeg
        }

        private class PivotData
        {
            public Transform pivotTransform;
            public Quaternion defaultOrientation;
            private float currentAngle = 0f;

            public void setCurrentAngle(float angle)
            {
                currentAngle = angle;
                pivotTransform.rotation = defaultOrientation * Quaternion.Euler(0f, currentAngle, 0f);
            }

            public float getCurrentAngle()
            {
                return currentAngle;
            }

            public float getTargetAngle(Vector3 targetPos)
            {
                Vector3 fwd = pivotTransform.InverseTransformDirection(pivotTransform.forward);
                Vector3 pos = pivotTransform.position;
                Vector3 ptd = targetPos - pos;//pos-tgt-delta
                ptd = pivotTransform.InverseTransformDirection(ptd);
                float angleFwd = (float)SSTUUtils.toDegrees(Mathf.Atan2(fwd.x, fwd.z));
                float angleTgt = (float)SSTUUtils.toDegrees(Mathf.Atan2(ptd.x, ptd.z));
                return angleTgt - angleFwd;
            }
        }

        private class SuncatcherData
        {
            public Transform suncatcherTransform;
        }

        //config field, should contain CSV of transform names for ray cast checks
        [KSPField]
        public String rayTransforms = String.Empty;

        //config field, should contain CSV of pivot names for panels
        [KSPField]
        public String pivotTransforms = String.Empty;

        [KSPField]
        public String windBreakTransformName = String.Empty;

        [KSPField]
        public String resourceName = "ElectricCharge";

        [KSPField]
        public float resourceAmount = 3.0f;

        [KSPField]
        public float windResistance = 30.0f;

        [KSPField]
        public bool breakable = true;

        [KSPField]
        public bool canDeployShrouded = false;

        [KSPField]
        public bool canRetract = true;

        [KSPField]
        public float trackingSpeed = 0.25f;

        [KSPField]
        public string animationID = string.Empty;

        [KSPField]
        public string sunAxis = Axis.ZPlus.ToString();

        [KSPField]
        public FloatCurve temperatureEfficCurve;

        [KSPField]
        public bool canLockPanels = true;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Panel Rotation"),
         UI_Toggle(suppressEditorShipModified = true, enabledText = "Locked", disabledText = "Tracking")]
        public bool userLock = false;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Panel Rotation"),
         UI_FloatEdit(suppressEditorShipModified = true, minValue = -180f, maxValue = 180f, incrementLarge =90f, incrementSmall = 45, incrementSlide = 1f)]
        public float userRotation = 0f;

        //BELOW HERE ARE NON-CONFIG EDITABLE FIELDS

        //used purely to persist rough estimate of animation state; if it is retracting/extending when reloaded, it will default to the start of that animation transition
        //defaults to retracted state for any new/uninitialized parts
        [KSPField(isPersistant = true)]
        public String savedAnimationState = "RETRACTED";

        //Status displayed for panel state, includes animation state and energy state;  Using in place of the three-line output from stock panels
        [KSPField(guiName = "S.P.", guiActive = true)]
        public String guiStatus = String.Empty;

        [KSPField(isPersistant = true)]
        public string persistentData = string.Empty;

        //current state of this solar panel module
        private SSTUPanelState panelState = SSTUPanelState.RETRACTED;

        private Axis suncatcherAngleAxis = Axis.ZPlus;

        //cached energy flow value, used to update gui
        private float energyFlow = 0.0f;

        private String occluderName = String.Empty;

        private Transform sunTransform;

        private Transform windBreakTransform;

        private PivotData[] pivotData;
        private SuncatcherData[] suncatcherData;

        private SSTUAnimateControlled animationController;

        private float retractLerp = 0;

        private bool initialized = false;

        //KSP Action Group 'Extend Panels' action, will only trigger when panels are actually retracted/ing
        [KSPAction("Extend Solar Panels")]
        public void extendAction(KSPActionParam param)
        {
            if (panelState == SSTUPanelState.RETRACTED || panelState == SSTUPanelState.RETRACTING)
            {
                toggle();
            }
        }

        //KSP Action Group 'Retract Panels' action, will only trigger when panels are actually extended/ing
        [KSPAction("Retract Solar Panels")]
        public void retractAction(KSPActionParam param)
        {
            if (panelState == SSTUPanelState.EXTENDING || panelState == SSTUPanelState.EXTENDED)
            {
                toggle();
            }
        }

        //KSP Action Group 'Toggle Panels' action, will operate regardless of current panel status (except broken)
        [KSPAction("Toggle Solar Panels")]
        public void toggleAction(KSPActionParam param)
        {
            toggle();
        }

        [KSPEvent(name = "extendEvent", guiName = "Extend Solar Panels", guiActiveUnfocused = true, externalToEVAOnly = true, guiActive = true, unfocusedRange = 4f, guiActiveEditor = true)]
        public void extendEvent()
        {
            toggle();
        }

        [KSPEvent(name = "retractEvent", guiName = "Retract Solar Panels", guiActiveUnfocused = true, externalToEVAOnly = true, guiActive = true, unfocusedRange = 4f, guiActiveEditor = true)]
        public void retractEvent()
        {
            toggle();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            this.temperatureEfficCurve = new FloatCurve();
            this.temperatureEfficCurve.Add(4f, 1.2f, 0f, -0.0005725837f);
            this.temperatureEfficCurve.Add(300f, 1f, -0.0008277721f, -0.0008277721f);
            this.temperatureEfficCurve.Add(1200f, 0.1f, -0.0003626566f, -0.0003626566f);
            this.temperatureEfficCurve.Add(2500f, 0.01f, 0f, 0f);
            initializeState();
        }
        
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            try
            {
                panelState = (SSTUPanelState)Enum.Parse(typeof(SSTUPanelState), savedAnimationState);
            }
            catch
            {
                panelState = SSTUPanelState.RETRACTED;
            }
            initializeState();
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            if (pivotData != null)
            {
                savedAnimationState = panelState.ToString();
                node.SetValue("savedAnimationState", savedAnimationState, true);
                node.SetValue(nameof(persistentData), getPivotSaveData());
            }
        }

        private string getPivotSaveData()
        {
            if (pivotData == null) { return persistentData; }
            string output = "";
            int len = pivotData.Length;
            Quaternion rot;
            for (int i = 0; i < len; i++)
            {
                if (pivotData[i].pivotTransform == null)
                {
                    return persistentData;
                }
                else
                {
                    rot = pivotData[i].pivotTransform.localRotation;
                }
                output = output + rot.x + "," + rot.y + "," + rot.z + "," + rot.w;
                if (i < len-1)
                {
                    output = output + ";";
                }
            }
            return output;
        }

        private void loadPivotSaveData(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                //MonoBehaviour.print("ERROR: No saved/persistent pivot data for pivots.");
                return;
            }
            int index = 0;
            string[] split0 = data.Split(';');
            int len = split0.Length;
            if (len != pivotData.Length)
            {
                MonoBehaviour.print("ERROR: Length mismatch between saved pivot data and stored pivot orientation array. Things are likely going to crash...");
            }
            string[] split1;
            Quaternion parsed;
            for (int i = 0; i < len; i++, index++)
            {
                split1 = split0[index].Split(',');
                parsed = new Quaternion(float.Parse(split1[0]), float.Parse(split1[1]), float.Parse(split1[2]), float.Parse(split1[3]));
                pivotData[i].pivotTransform.localRotation = parsed;
            }
        }

        public override string GetInfo()
        {
            if (suncatcherData != null && moduleIsEnabled)
            {
                String data = "Deployable Solar Panel\n";
                data = data + "EC/s max: " + resourceAmount * suncatcherData.Length+"\n";
                data = data + "Tracking Speed: " + trackingSpeed + "deg/s";
                return data;
            }
            return base.GetInfo();
        }

        public void FixedUpdate()
        {
            if (!moduleIsEnabled) { return; }
            energyFlow = 0.0f;
            occluderName = String.Empty;
            if (retractLerp > 0)
            {
                updateRetractLerp();
            }
            else if (panelState == SSTUPanelState.EXTENDED)
            {
                if (HighLogic.LoadedSceneIsFlight)
                {
                    updatePowerStatus();
                    checkForBreak();
                }
                else if (HighLogic.LoadedSceneIsEditor && userLock)
                {
                    int len = pivotData.Length;
                    for (int i = 0; i < len; i++)
                    {
                        updatePivotRotation(pivotData[i]);
                    }
                }
            }
            updateGuiData();
        }

        private void reInitialize()
        {
            initialized = false;
            OnStart(StartState.Flying);
        }

        public void setSuncatcherAxis(Axis axis)
        {
            suncatcherAngleAxis = axis;
            sunAxis = axis.ToString();
        }

        public void onAnimationStatusChanged(AnimState state)
        {
            if (state == AnimState.STOPPED_END)
            {
                setPanelState(SSTUPanelState.EXTENDED);                
            }
            else if (state == AnimState.STOPPED_START)
            {
                setPanelState(SSTUPanelState.RETRACTED);
            }
        }

        public void disableModule()
        {
            moduleIsEnabled = false;
            energyFlow = 0;
            Events["extendEvent"].active = false;
            Events["retractEvent"].active = false;
            Fields["guiStatus"].guiActive = false;
            Actions["toggleAction"].active = false;
            Actions["extendAction"].active = false;
            Actions["retractAction"].active = false;
            sunTransform = null;
            windBreakTransform = null;
            pivotData = null;
            suncatcherData = null;
            if (animationController != null) { animationController.removeCallback(onAnimationStatusChanged); }
            animationController = null;
        }

        public void enableModule()
        {
            moduleIsEnabled = true;
            energyFlow = 0;
            Fields["guiStatus"].guiActive = true;
            Actions["toggleAction"].active = true;
            Actions["extendAction"].active = true;
            Actions["retractAction"].active = true;
            reInitialize();
        }

        private void initializeState()
        {
            if (initialized || !moduleIsEnabled) { return; }
            initialized = true;
            findTransforms();
            animationController = SSTUAnimateControlled.locateAnimationController(part, animationID, onAnimationStatusChanged);
            setupDefaultRotations();
            setPanelState(panelState);
            loadPivotSaveData(persistentData);
            updateGuiData();
            suncatcherAngleAxis = (Axis)Enum.Parse(typeof(Axis), sunAxis);
        }
        
        private void toggle()
        {
            if (panelState == SSTUPanelState.BROKEN)
            {
                //noop, broken panel... print message?
            }
            else if (panelState == SSTUPanelState.EXTENDING || panelState == SSTUPanelState.EXTENDED)
            {
                if (canRetract)
                {
                    retract();
                }                
            }
            else //must be retracting or retracted
            {
                deploy();
            }
        }
        
        private void deploy()
        {
            if (!canDeployShrouded && part.ShieldedFromAirstream)
            {
                //TODO print screen-message
                print("Cannot deploy while shielded from airstream!!");
                return;
            }
            if (retractLerp > 0)
            {
                retractLerp = 0;
                setPanelState(SSTUPanelState.EXTENDED);
            }
            else
            {
                setPanelState(SSTUPanelState.EXTENDING);
            }
        }
        
        private void retract()
        {
            setPanelState(SSTUPanelState.RETRACTING);
        }

        private void updateRetractLerp()
        {
            float fixedTick = TimeWarp.fixedDeltaTime;

            float percent = fixedTick / retractLerp;//need to move this percent of total remaining move
            if (percent > 1) { percent = 1; }
            if (percent < 0) { percent = 0; }

            foreach (PivotData data in pivotData)
            {
                data.pivotTransform.localRotation = Quaternion.Lerp(data.pivotTransform.localRotation, data.defaultOrientation, percent);               
            }

            retractLerp -= fixedTick;

            if (retractLerp <= 0)
            {
                retractLerp = 0;
                setPanelsToDefaultOrientation();
                setAnimationState(AnimState.PLAYING_BACKWARD);//start retract animation playing
            }
        }

        private void updatePowerStatus()
        {
            energyFlow = 0.0f;
            occluderName = String.Empty;
            CelestialBody sun = FlightGlobals.Bodies[0];
            sunTransform = sun.transform;
            int len = pivotData.Length;
            if (vessel.solarFlux > 0)
            {
                for (int i = 0; i < len; i++)
                {
                    updatePivotRotation(pivotData[i]);
                }
            }
            len = suncatcherData.Length;
            for (int i = 0; i < len; i++)
            {
                updatePanelPower(suncatcherData[i]);
            }
            if (energyFlow > 0)
            {
                part.RequestResource(resourceName, -energyFlow);
            }
        }
        
        private void updatePivotRotation(PivotData pd)
        {
            //vector from pivot to sun
            //finding angle to turn towards based on direction of vector on a single axis
            if (userLock)
            {
                float y = userRotation;
                if (pd.pivotTransform.lossyScale.z < 0)
                {
                    y = -y;
                }
                Quaternion current = pd.pivotTransform.rotation;
                pd.pivotTransform.localRotation = pd.defaultOrientation;
                Quaternion target = pd.pivotTransform.rotation * Quaternion.Euler(0f, y, 0f);
                pd.pivotTransform.rotation = Quaternion.Lerp(current, target, TimeWarp.deltaTime * this.trackingSpeed);
            }
            else
            {
                Vector3 vector = pd.pivotTransform.InverseTransformPoint(sunTransform.position);
                float y = (float)SSTUUtils.toDegrees(Mathf.Atan2(vector.x, vector.z));
                if (pd.pivotTransform.lossyScale.z < 0)
                {
                    y = -y;
                }
                Quaternion to = pd.pivotTransform.rotation * Quaternion.Euler(0f, y, 0f);
                pd.pivotTransform.rotation = Quaternion.Lerp(pd.pivotTransform.rotation, to, TimeWarp.deltaTime * this.trackingSpeed);
            }
        }

        private void updatePanelPower(SuncatcherData sc)
        {
            if (!isOccludedByPart(sc.suncatcherTransform))
            {
                Vector3 normalized = (sunTransform.position - sc.suncatcherTransform.position).normalized;
                Vector3 suncatcherVector = Vector3.zero;
                float invertValue = 0f;
                switch (suncatcherAngleAxis)
                {
                    case Axis.XPlus:
                        suncatcherVector = sc.suncatcherTransform.right;
                        invertValue = sc.suncatcherTransform.lossyScale.x;
                        break;
                    case Axis.XNeg:
                        suncatcherVector = -sc.suncatcherTransform.right;
                        invertValue = sc.suncatcherTransform.lossyScale.x;
                        break;
                    case Axis.YPlus:
                        suncatcherVector = sc.suncatcherTransform.up;
                        invertValue = sc.suncatcherTransform.lossyScale.y;
                        break;
                    case Axis.YNeg:
                        suncatcherVector = -sc.suncatcherTransform.up;
                        invertValue = sc.suncatcherTransform.lossyScale.y;
                        break;
                    case Axis.ZPlus:
                        suncatcherVector = sc.suncatcherTransform.forward;
                        invertValue = sc.suncatcherTransform.lossyScale.z;
                        break;
                    case Axis.ZNeg:
                        suncatcherVector = -sc.suncatcherTransform.forward;
                        invertValue = sc.suncatcherTransform.lossyScale.z;
                        break;
                    default:
                        break;
                }
                
                if (invertValue < 0)
                {
                    suncatcherVector = -suncatcherVector;
                }
                float sunAOA = Mathf.Clamp(Vector3.Dot(suncatcherVector, normalized), 0f, 1f);
                float distMult = (float)(vessel.solarFlux / PhysicsGlobals.SolarLuminosityAtHome);

                if (distMult == 0 && FlightGlobals.currentMainBody != null)//vessel.solarFlux == 0, so occluded by a planetary body
                {
                    occluderName = FlightGlobals.currentMainBody.name;//just guessing..but might be occluded by the body we are orbiting?
                }

                float efficMult = temperatureEfficCurve.Evaluate((float)part.temperature);
                float panelEnergy = resourceAmount * TimeWarp.fixedDeltaTime * sunAOA * distMult * efficMult;
                energyFlow += panelEnergy;
            }
        }

        //does a 'very short' raycast for vessel/part occlusion checking
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
        
        private void setPanelState(SSTUPanelState newState)
        {
            SSTUPanelState oldState = panelState;
            panelState = newState;
            switch (newState)
            {

                case SSTUPanelState.BROKEN:
                    {
                        setAnimationState(AnimState.STOPPED_END);
                        breakPanels();
                        break;
                    }

                case SSTUPanelState.EXTENDED:
                    {
                        setAnimationState(AnimState.STOPPED_END);
                        break;
                    }

                case SSTUPanelState.EXTENDING:
                    {
                        setAnimationState(AnimState.PLAYING_FORWARD);
                        break;
                    }

                case SSTUPanelState.RETRACTED:
                    {
                        setAnimationState(AnimState.STOPPED_START);
                        break;
                    }

                case SSTUPanelState.RETRACTING:
                    {                        
                        retractLerp = 2f;//two second retract lerp time; decremented during lerp update                     
                        break;
                    }
            }

            energyFlow = 0.0f;
            savedAnimationState = panelState.ToString();
            updateGuiData();
        }

        private void setAnimationState(AnimState state)
        {
            if (animationController != null) { animationController.setToState(state); }
        }
                        
        private void setPanelsToDefaultOrientation()
        {
            int len = pivotData.Length;
            for (int i = 0; i < len; i++)
            {
                pivotData[i].pivotTransform.localRotation = pivotData[i].defaultOrientation;
            }
        }

        private void checkForBreak()
        {
            if (!breakable || panelState == SSTUPanelState.BROKEN || vessel == null || vessel.packed)
            {
                return;//noop
            }
            Vector3 tr;
            if (windBreakTransform != null)
            {
                tr = windBreakTransform.forward;
            }
            else
            {
                tr = vessel.transform.up;
            }
            float num = Mathf.Abs(Vector3.Dot(base.vessel.srf_velocity.normalized, tr.normalized));
            float num2 = (float)base.vessel.srf_velocity.magnitude * num * (float)base.vessel.atmDensity;
            if (num2 >= this.windResistance)
            {
                setPanelState(SSTUPanelState.BROKEN);
            }
        }

        private void breakPanels()
        {
            int len = pivotData.Length;
            for (int i = 0; i < len; i++)
            {
                SSTUUtils.enableRenderRecursive(pivotData[i].pivotTransform, false);
            }
        }

        private void repairPanels()
        {
            int len = pivotData.Length;
            for (int i = 0; i < len; i++)
            {
                SSTUUtils.enableRenderRecursive(pivotData[i].pivotTransform, true);
            }
        }
        
        private void findTransforms()
        {
            String[] suncatcherNames = SSTUUtils.parseCSV(rayTransforms);
            String[] pivotNames = SSTUUtils.parseCSV(pivotTransforms);

            PivotData pd;
            SuncatcherData sd;
            int len2;
            Transform[] trs;
            String name;

            List<PivotData> tempPivotData = new List<PivotData>();
            int len = pivotNames.Length;
            for (int i = 0; i < len; i++)
            {
                name = pivotNames[i];
                if (String.IsNullOrEmpty(name)) { MonoBehaviour.print("ERROR: Empty name for solar pivot for part: " + part); }
                trs = part.transform.FindChildren(name);
                len2 = trs.Length;
                if (len2 == 0) { MonoBehaviour.print("ERROR: Could not locate solar pivot transforms for name: "+name + " for part: " + part); }
                for (int k = 0; k < len2; k++)
                {
                    pd = new PivotData();
                    pd.pivotTransform = trs[k];
                    pd.defaultOrientation = pd.pivotTransform.localRotation;
                    tempPivotData.Add(pd);
                }
            }
            pivotData = tempPivotData.ToArray();
            tempPivotData.Clear();

            List<SuncatcherData> tempSunData = new List<SuncatcherData>();
            len = suncatcherNames.Length;
            for (int i = 0; i < len; i++)
            {
                name = suncatcherNames[i];
                if (String.IsNullOrEmpty(name)) { MonoBehaviour.print("ERROR: Empty name for suncatcher for part: "+ part); }
                trs = part.transform.FindChildren(name);
                len2 = trs.Length;
                if (len2 == 0) { MonoBehaviour.print("ERROR: Could not locate suncatcher transforms for name: " + name+" for part: "+part); }
                for (int k = 0; k < len2; k++)
                {
                    sd = new SuncatcherData();
                    sd.suncatcherTransform = trs[k];
                    tempSunData.Add(sd);
                }
            }
            suncatcherData = tempSunData.ToArray();

            Transform t1;
            if (windBreakTransformName != null && windBreakTransformName.Length > 0)
            {
                t1 = part.FindModelTransform(windBreakTransformName);
                if (t1 != null)
                {
                    windBreakTransform = t1;
                }
            }
            if (windBreakTransform == null && pivotData.Length > 0)
            {
                windBreakTransform = pivotData[0].pivotTransform;
            }//else it will use default vessel transform
        }

        private void setupDefaultRotations()
        {
            AnimState state = animationController.getAnimationState();
            animationController.setToState(AnimState.STOPPED_END);//will trigger an animation sample
            int len = pivotData.Length;
            for (int i = 0; i < len; i++)
            {
                pivotData[i].defaultOrientation = pivotData[i].pivotTransform.localRotation;
            }
            animationController.setToState(state);//restore actual state...
        }
        
        private void updateGuiData()
        {
            if (energyFlow == 0 && occluderName.Length > 0)//if occluded, state that information first
            {
                guiStatus = "OCC: " + occluderName;
            }
            else
            {
                guiStatus = panelState.ToString();
                if (energyFlow > 0)
                {
                    guiStatus += " : " + String.Format("{0:F1}", (energyFlow * (1 / TimeWarp.fixedDeltaTime))) + " e/s";
                }
            }
            if (panelState == SSTUPanelState.BROKEN)
            {
                Events["extendEvent"].active = false;
                Events["retractEvent"].active = false;
            }
            else if (panelState == SSTUPanelState.EXTENDING || panelState == SSTUPanelState.EXTENDED)
            {
                Events["extendEvent"].active = false;
                Events["retractEvent"].active = canRetract;
            }
            else//
            {
                Events["extendEvent"].active = true;
                Events["retractEvent"].active = false;
            }
            Fields[nameof(userLock)].guiActive = Fields[nameof(userLock)].guiActiveEditor = canLockPanels;
            Fields[nameof(userRotation)].guiActive = Fields[nameof(userRotation)].guiActiveEditor = userLock;
        }

        public string GetContractObjectiveType()
        {
            return "Generator";
        }

        public bool CheckContractObjectiveValidity()
        {
            return moduleIsEnabled;
        }

    }

}

