using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SolarModule<T> : AnimationModule<T> where T : PartModule
    {

        /// <summary>
        /// GUI status field for the solar panel.  Updated every Update()?
        /// </summary>
        private BaseField panelStatusField;

        /// <summary>
        /// Persistent data field for panel rotations and broken status.  Backing field should be a string, so arbitrary data can be stored.
        /// </summary>
        private BaseField rotationPersistenceField;

        /// <summary>
        /// Live solar panel data, including references to pivot and suncatcher transforms.  Includes tracking of 'broken' status for each panel.<para/>
        /// Each entry in the array is a single 'panel' denoted by a shared breakage point.
        /// </summary>
        private PanelData[] panelData;

        /// <summary>
        /// Internal flag tracking if the solar panel should be doing the 'pre-retract' rotation back towards default orientation
        /// </summary>
        private bool closingLerp;

        private string panelStatus
        {
            get { return panelStatusField.GetValue<string>(panelStatusField); }
            set { panelStatusField.SetValue(value, panelStatusField); }
        }

        public SolarModule(Part part, T module, BaseField animationPersistence, BaseField rotationPersistence, BaseField panelStatusField, BaseEvent deploy, BaseEvent retract) : base(part, module, animationPersistence, null, deploy, retract)
        {
            this.rotationPersistenceField = rotationPersistence;
            this.panelStatusField = panelStatusField;
        }

        public override void onDeployEvent()
        {
            base.onDeployEvent();
        }

        public override void onRetractEvent()
        {
            if (animState == AnimState.STOPPED_END)
            {
                closingLerp = true;
            }
            else
            {
                base.onRetractEvent();
            }            
        }

        public override void updateAnimations()
        {
            base.updateAnimations();  
        }

        /// <summary>
        /// Must be called after the animations have been setup, so that they may be used
        /// to sample and properly setup default rotations for the pivot transforms
        /// </summary>
        /// <param name="node"></param>
        public void setupSolarPanelData(ConfigNode node, Transform root)
        {
            ConfigNode[] panelNodes = node.GetNodes("PANEL");
            int len = panelNodes.Length;
            panelData = new PanelData[len];
            for (int i = 0; i < len; i++)
            {
                panelData[i] = new PanelData(panelNodes[i], root);
            }
            initializeRotations();
        }

        /// <summary>
        /// Should be called from Update() to update the solar panel transform rotations.
        /// This just rotates the panel pivots towards the solar target, does not check occlusion or ec-output
        /// but will be disabled if the panel is already occluded.
        /// </summary>
        public void solarUpdate()
        {
            //TODO -- support solar panels that lack animations (static panels)
            //TODO -- support solar panel animation locking -- this should have separate lock and angle sliders for main and secondary transforms
            //TODO -- how useful is the locking feature, really?
            //only update if animation is set to deployed
            MonoBehaviour.print("Solar update, anim state: " + animState);
            if (animState != AnimState.STOPPED_END)
            {
                return;
            }
            int len = panelData.Length;
            if (closingLerp)
            {
                bool finished = true;
                for (int i = 0; i < len; i++)
                {
                    finished = finished && panelData[i].panelUpdateRetract();
                }
                if (finished)
                {
                    closingLerp = false;
                    setAnimState(AnimState.PLAYING_BACKWARD);
                }
            }
            else if(HighLogic.LoadedSceneIsFlight)
            {
                Vector3 sunPos = FlightGlobals.Bodies[0].transform.position;
                for (int i = 0; i < len; i++)
                {
                    panelData[i].panelUpdate(sunPos);
                }
            }
            //noop in editor if not lerping closed
        }

        /// <summary>
        /// Should be called on fixed-update to calculate the EC output from solar panels.  Includes raycasts
        /// for occlusion checks, as well as
        /// </summary>
        public void solarFixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || part.vessel == null)
            {
                return;
            }
            float distMult = (float)(part.vessel.solarFlux / PhysicsGlobals.SolarLuminosityAtHome);
            if (distMult == 0)//occluded, zero solar flux input on vessel
            {
                //use current main body as the occluder, even though it might be something else in the case of lunar eclipse/etc
                if (FlightGlobals.currentMainBody != null)
                {
                    panelStatus = "Occluded: " + FlightGlobals.currentMainBody.name;
                }
                else
                {
                    panelStatus = "Occluded: Unknown";
                }
                //TODO loop through panels and set them to 'occluded' status so that they do not update
                return;
            }
            Vector3 solarTarget = FlightGlobals.Bodies[0].transform.position;
            string occluder = string.Empty;
            float panelOutput = 0f;
            float totalOutput = 0f;
            int len = panelData.Length;
            for (int i = 0; i < len; i++)
            {
                panelOutput = panelData[i].panelFixedUpdate(solarTarget);
                if (panelOutput == 0)
                {
                    occluder = panelData[i].occluderName;
                }
            }

            float temperatureMultiplier = 1.0f;//TODO -- add temp curve/multiplier
            totalOutput *= temperatureMultiplier * distMult;//per-second output value
            //use current this to update gui status before converting to delta time updates
            if (totalOutput > 0)
            {
                panelStatus = totalOutput + " EC/s";

                totalOutput *= TimeWarp.fixedDeltaTime;
                MonoBehaviour.print("TODO -- update part resources for EC generation");
                //TODO update part resources
            }
            else
            {
                panelStatus = "Occluded: " + occluder;
            }
        }

        //TODO
        /// <summary>
        /// Should be called from owning part-module when solar panels are to be repaired.
        /// </summary>
        public void onPanelRepairEvent()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Initialize the default pivot orientations and restore any rotations from persistent data
        /// </summary>
        private void initializeRotations()
        {
            float time = animTime;
            //sample to deployed state
            setAnimTime(1, true);
            string[] persistentDataSplits = persistentData.Split(';');
            string data;
            int len = panelData.Length;
            for (int i = 0; i < len; i++)
            {
                if (i < persistentDataSplits.Length)
                {
                    data = persistentDataSplits[i];
                }
                else
                {
                    data = string.Empty;
                }
                //load persistence and restore previous rotations, if applicable
                panelData[i].initializeRotations(data);
            }
            //return animation state to previous state
            setAnimTime(time, true);
        }

        //TODO -- call this from somewhere...
        public void updateSolarPersistence()
        {
            string data = string.Empty;
            int len = panelData.Length;
            for (int i = 0; i < len; i++)
            {
                if (i > 0) { data = data + ";"; }
                data = data + panelData[i].getPersistentData();
            }
            persistentData = data;
        }

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

    /// <summary>
    /// Wrapper for a single 'solar panel' -- suncatchers, pivots, broken status
    /// </summary>
    public class PanelData
    {
        /// <summary>
        /// Dynamic-pressure needed to break the panel if deployed while moving through an atmosphere.
        /// </summary>
        public readonly float breakPressure;

        /// <summary>
        /// G-force required to break the panel if it is deployed while the force is experienced.
        /// The exact g-force experienced by the panel is the input G multiplied
        /// by the dot product of the G vector and the panels break transform axis.
        /// </summary>
        public readonly float breakGs;

        /// <summary>
        /// Persistent tracking for if this individual panel is broken or not
        /// </summary>
        public bool isBroken = false;

        /// <summary>
        /// Main pivot container
        /// </summary>
        public SolarPivotData mainPivot;

        /// <summary>
        /// Secondary pivot container
        /// </summary>
        public SolarPivotData secondPivot;

        /// <summary>
        /// Suncatcher containers
        /// </summary>
        public SuncatcherData[] suncatchers;

        /// <summary>
        /// Name of the occluder, if any.  Updated during panel power update processing.
        /// </summary>
        public string occluderName = string.Empty;

        public PanelData(ConfigNode node, Transform root)
        {
            string mainName = node.GetStringValue("mainPivot", string.Empty);
            if (!string.IsNullOrEmpty(mainName))
            {
                int mainIndex = node.GetIntValue("mainPivotIndex", 0);
                Axis mainSunAxis = node.getAxis("mainSunAxis", Axis.ZPlus);
                Axis mainRotAxis = node.getAxis("mainRotAxis", Axis.XPlus);
                float speed = node.GetFloatValue("mainPivotSpeed", 10f);
                Transform[] trs = root.FindChildren(mainName);
                mainPivot = new SolarPivotData(trs[mainIndex], speed, mainSunAxis, mainRotAxis);

                string secondName = node.GetStringValue("secondPivot", string.Empty);
                if (!string.IsNullOrEmpty(secondName))
                {
                    speed = node.GetFloatValue("secondPivotSpeed", 10f);
                    int secondIndex = node.GetIntValue("secondPivotIndex", 0);
                    Axis secSunAxis = node.getAxis("secondSunAxis", Axis.ZPlus);
                    Axis secRotAxis = node.getAxis("secondRotAxis", Axis.XPlus);
                    trs = root.FindChildren(secondName);
                    secondPivot = new SolarPivotData(trs[secondIndex], speed, secSunAxis, secRotAxis);
                }
            }
            ConfigNode[] suncatcherNodes = node.GetNodes("SUNCATCHER");
            int len = suncatcherNodes.Length;
            suncatchers = new SuncatcherData[len];
            for (int i = 0; i < len; i++)
            {
                suncatchers[i] = new SuncatcherData(suncatcherNodes[i], root);
            }
        }

        /// <summary>
        /// Updates the panels sun-tracking pivots in 'retracting' mode.<para/>
        /// Returns true when finished and ready for retract animation
        /// </summary>
        public bool panelUpdateRetract()
        {
            bool mainDone = true;
            bool secondDone = true;
            if (mainPivot != null)
            {
                mainDone = mainPivot.rotateTowardsDefault();
                if (secondPivot != null)
                {
                    secondDone = secondPivot.rotateTowardsDefault();
                }
            }
            return mainDone && secondDone;
        }

        /// <summary>
        /// Updates the panels sun-tracking pivots, using the config-value for tracking speed.
        /// </summary>
        /// <param name="solarSource"></param>
        public void panelUpdate(Vector3 solarSource)
        {
            if (isBroken) { return; }
            if (!string.IsNullOrEmpty(occluderName)) { return; }
            if (mainPivot != null)
            {
                mainPivot.rotateTowards(solarSource);
                if (secondPivot != null)
                {
                    secondPivot.rotateTowards(solarSource);
                }
            }
        }

        /// <summary>
        /// Updates the panels suncatchers -- checking for occlusion and calcuating EC output if not occluded
        /// </summary>
        /// <param name="solarSource"></param>
        /// <returns></returns>
        public float panelFixedUpdate(Vector3 solarSource)
        {
            occluderName = string.Empty;
            if (isBroken) { return 0; }
            int len = suncatchers.Length;
            float panelOutput = 0f;
            float totalOutput = 0f;
            for (int i = 0; i < len; i++)
            {
                panelOutput = suncatchers[i].calcRawOutput(solarSource);
                if (panelOutput <= 0)//was occluded
                {
                    occluderName = suncatchers[i].occluderName;
                }
                totalOutput += panelOutput;
            }
            return totalOutput;
        }

        public void initializeRotations(string persistentData)
        {
            string mainPivotPersistence = string.Empty;
            string secondPivotPersistence = string.Empty;
            string brokenPersistence = string.Empty;
            if (mainPivot != null)
            {
                mainPivot.initializeRotation(mainPivotPersistence);
            }
            if (secondPivot != null)
            {
                secondPivot.initializeRotation(secondPivotPersistence);
            }
            isBroken = brokenPersistence == "true";
        }

        public string getPersistentData()
        {
            string main = string.Empty;
            string second = string.Empty;
            string broken = isBroken ? "true" : "false";
            return main + ":" + second + ":" + broken;
        }

    }

    public class SuncatcherData
    {
        public readonly Transform suncatcher;
        public readonly Axis suncatcherAxis;
        public readonly float resourceRate;

        public string occluderName = string.Empty;

        private RaycastHit hitData;

        public SuncatcherData(ConfigNode node, Transform root)
        {
            string sunName = node.GetStringValue("suncatcher");
            suncatcherAxis = node.getAxis("suncatcherAxis", Axis.ZPlus);
            int index = node.GetIntValue("suncatcherIndex", 0);
            Transform[] trs = root.FindChildren(sunName);
            suncatcher = trs[index];
            resourceRate = node.GetFloatValue("rate");
        }

        /// <summary>
        /// Return the raw output based on transform orientation.  Is not adjusted for distance or temperature.  Checks part occlusion, does not check for body occlusion.
        /// </summary>
        /// <param name="target"></param>
        public float calcRawOutput(Vector3 targetPos)
        {
            Vector3 panelFacing = suncatcher.getTransformAxis(suncatcherAxis);
            Vector3 directionToSun = (targetPos - suncatcher.position).normalized;
            float sunDot = Mathf.Clamp(Vector3.Dot(panelFacing, directionToSun), 0f, 1f);
            if (sunDot > 0 && checkPartOcclusion(directionToSun))
            {
                //occluded by part, zero out the resource output, this causes outer-loop power calc to examine the occluder name
                sunDot = 0;
            }
            return sunDot * resourceRate;
        }

        /// <summary>
        /// Do a short raycast on the suncatcher to determine if it is occluded.
        /// TODO -- only do raycast occlusion check every X ticks, and/or only raycast-occlusion-check from a single suncatcher per-panel (rather than every suncatcher)
        /// </summary>
        /// <param name="directionToSun"></param>
        /// <returns></returns>
        public bool checkPartOcclusion(Vector3 directionToSun)
        {
            if (Physics.Raycast(suncatcher.position, directionToSun, out hitData, 300f))
            {
                occluderName = hitData.transform.gameObject.name;
                return true;
            }
            return false;
        }

    }

    public class SolarPivotData
    {
        public readonly Transform pivot;
        public readonly Axis pivotSunAxis;
        public readonly Axis pivotRotationAxis;
        public readonly float rotationOffset = 0f;
        public readonly float trackingSpeed = 10f;//degrees per second
        public Quaternion defaultOrientation;

        public SolarPivotData(Transform pivot, float trackingSpeed, Axis pivotSunAxis, Axis pivotRotationAxis)
        {
            this.pivot = pivot;
            this.trackingSpeed = trackingSpeed;
            this.pivotSunAxis = pivotSunAxis;
            this.pivotRotationAxis = pivotRotationAxis;

            //pre-calculate a rotation offset that is used during updating of the pivot rotation
            //this offset is used to speed up constraint calculation
            if (pivotRotationAxis==Axis.XPlus || pivotRotationAxis==Axis.XNeg)
            {
                if (pivotSunAxis == Axis.YPlus)
                {
                    rotationOffset = 90;
                }
                else if (pivotSunAxis == Axis.YNeg)
                {
                    rotationOffset = -90;
                }
                else if (pivotSunAxis == Axis.ZNeg)
                {
                    rotationOffset = 180f;
                }
                //implicit else ZPlus = 0f
            }
            else if (pivotRotationAxis == Axis.YPlus || pivotRotationAxis == Axis.YNeg)
            {
                if (pivotSunAxis == Axis.XPlus)
                {
                    rotationOffset = -90f;
                }
                else if (pivotSunAxis == Axis.XNeg)
                {
                    rotationOffset = 90f;
                }
                else if (pivotSunAxis == Axis.ZNeg)
                {
                    rotationOffset = 180f;
                }
                //implicit else ZPlus = 0f
            }
            else if (pivotRotationAxis == Axis.ZPlus || pivotRotationAxis == Axis.ZNeg)
            {
                if (pivotSunAxis == Axis.XPlus)
                {
                    rotationOffset = 90f;
                }
                else if (pivotSunAxis == Axis.XNeg)
                {
                    rotationOffset = -90f;
                }
                else if (pivotSunAxis == Axis.YNeg)
                {
                    rotationOffset = 180f;
                }
                //implicit else YPlus = 0f
            }
        }

        /// <summary>
        /// Sets the pivot transform to a specific local rotation
        /// </summary>
        /// <param name="localOrientation"></param>
        public void setRotation(Quaternion localOrientation)
        {
            pivot.localRotation = localOrientation;
        }

        /// <summary>
        /// Rotate the pivot towards the input target position.
        /// </summary>
        /// <param name="targetPos"></param>
        /// <returns></returns>
        public bool rotateTowards(Vector3 targetPos)
        {
            //this is the total angle needed to rotate (could be + or -)
            float rawAngle = getRotationAmount(targetPos);
            float absAngle = Mathf.Abs(rawAngle);
            float frameSpeed = trackingSpeed * Time.deltaTime;
            float frameAngle = 0f;

            bool finished = true;
            if (absAngle > frameSpeed)//too much for a single frame
            {
                finished = false;
                frameAngle = frameSpeed * Mathf.Sign(rawAngle);
            }
            else//implicit if (absAngle <= frameSpeed)//can be completed this frame
            {
                finished = true;
                frameAngle = rawAngle;
            }
            pivot.Rotate(pivot.getLocalAxis(pivotRotationAxis), frameAngle, Space.Self);
            MonoBehaviour.print("Updating rotation, finished: " + finished);
            return finished;
        }

        /// <summary>
        /// Rotate the panel pivot towards its default orientation, using the currently configured rotation speed
        /// </summary>
        /// <returns></returns>
        public bool rotateTowardsDefault()
        {
            //cache current orientation
            Quaternion currentOrientation = pivot.localRotation;
            
            //update to the default orientation
            pivot.localRotation = defaultOrientation;
            
            //get a position vector that represents the world-space target from the default orientation
            Vector3 defaultRotTargetPos = pivot.transform.position + pivot.getTransformAxis(pivotSunAxis);

            //restore the actual current rotation
            pivot.localRotation = currentOrientation;

            //finally, update the actual rotation
            return rotateTowards(defaultRotTargetPos);
        }

        /// <summary>
        /// Return the number of degrees the panel needs to rotate around its rotation axis in order to point the
        /// sun-axis as close to the target-pos as is possible given the constrained rotation
        /// </summary>
        /// <param name="targetPos"></param>
        /// <returns></returns>
        public float getRotationAmount(Vector3 targetPos)
        {
            //local-space position of the target
            Vector3 localDiff = pivot.InverseTransformPoint(targetPos);
            float rotation = 0f;
            if (pivotRotationAxis == Axis.XPlus || pivotRotationAxis == Axis.XNeg)
            {
                //use y and z
                rotation = -Mathf.Atan2(localDiff.y, localDiff.z) * Mathf.Rad2Deg + rotationOffset;
            }
            else if (pivotRotationAxis == Axis.YPlus || pivotRotationAxis == Axis.YNeg)
            {
                //use x and z
                rotation = Mathf.Atan2(localDiff.x, localDiff.z) * Mathf.Rad2Deg + rotationOffset;
            }
            else if (pivotRotationAxis == Axis.ZPlus || pivotRotationAxis == Axis.ZNeg)
            {
                //use x and y
                rotation = -Mathf.Atan2(localDiff.x, localDiff.y) * Mathf.Rad2Deg + rotationOffset;
            }
            return rotation;
        }

        public void initializeRotation(string persistentData)
        {
            defaultOrientation = pivot.localRotation;
        }

        public string getPersistentData()
        {
            return "TODO";
        }

    }

}
