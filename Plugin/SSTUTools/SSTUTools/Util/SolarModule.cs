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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Should be called on fixed-update to calculate the EC output from solar panels.  Includes raycasts
        /// for occlusion checks, as well as
        /// </summary>
        public void solarFixedUpdate()
        {
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
                panelOutput = panelData[i].updatePanel(solarTarget);
                if (panelOutput == 0)
                {
                    occluder = panelData[i].occluder;
                }
            }

            float temperatureMultiplier = 1.0f;//TODO -- add temp curve/multiplier
            totalOutput *= temperatureMultiplier * distMult;//per-second output value
            //use current this to update gui status before converting to delta time updates
            if (totalOutput > 0)
            {
                panelStatus = totalOutput + " EC/s";

                totalOutput *= TimeWarp.fixedDeltaTime;
                //TODO update part resources
            }
            else
            {
                panelStatus = "Occluded: " + occluder;
            }
        }

        public void onPanelRepairEvent()
        {

        }

        /// <summary>
        /// Initialize the default pivot orientations and restore any rotations from persistent data
        /// </summary>
        private void initializeRotations()
        {
            throw new NotImplementedException();
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
        public bool isBroken = false;
        public SolarPivotData mainPivot;
        public SolarPivotData secondPivot;
        public SuncatcherData[] suncatchers;

        public string occluder = string.Empty;

        public PanelData(ConfigNode node, Transform root)
        {

        }

        public void restoreRotation()
        {

        }

        public float updatePanel(Vector3 solarSource)
        {
            if (isBroken) { return 0; }
            //TODO update pivots

            int len = suncatchers.Length;
            float powerOutput = 0f;
            for (int i = 0; i < len; i++)
            {
                powerOutput += suncatchers[i].calcRawOutput(solarSource);
            }
            //TODO -- adjust power output for distance and temps
            return powerOutput;
        }

    }

    public class SuncatcherData
    {
        public readonly Transform suncatcher;
        public readonly Axis suncatcherAxis;
        public readonly float resourceRate;

        public string occluderName = string.Empty;

        private RaycastHit hitData;

        public SuncatcherData(Transform suncatcher, Axis axis, float rate)
        {
            this.suncatcher = suncatcher;
            this.suncatcherAxis = axis;
            this.resourceRate = rate;
        }

        /// <summary>
        /// Return the raw output based on transform orientation.  Is not adjusted for distance or temperature.  Checks part occlusion, does not check for body occlusion.
        /// </summary>
        /// <param name="target"></param>
        public float calcRawOutput(Vector3 targetPos)
        {
            Vector3 panelFacing = getTransformAxis(suncatcherAxis, suncatcher);
            Vector3 directionToSun = (targetPos - suncatcher.position).normalized;
            float sunDot = Mathf.Clamp(Vector3.Dot(panelFacing, directionToSun), 0f, 1f);
            if (sunDot > 0 && checkPartOcclusion(directionToSun))
            {
                //occluded by part, zero out the resource output
                sunDot = 0;
            }
            return sunDot * resourceRate;
        }

        public bool checkPartOcclusion(Vector3 directionToSun)
        {
            if (Physics.Raycast(suncatcher.position, directionToSun, out hitData, 300f))
            {
                occluderName = hitData.transform.gameObject.name;
                return true;
            }
            return false;
        }

        private Vector3 getTransformAxis(Axis axis, Transform transform)
        {
            switch (axis)
            {
                case Axis.XPlus:
                    return transform.right;
                case Axis.XNeg:
                    return -transform.right;
                case Axis.YPlus:
                    return transform.up;
                case Axis.YNeg:
                    return -transform.up;
                case Axis.ZPlus:
                    return transform.forward;
                case Axis.ZNeg:
                    return -transform.forward;
                default:
                    return transform.forward;
            }
        }

    }

    public class SolarPivotData
    {
        public readonly Transform pivot;
        public readonly Quaternion defaultOrientation;
        public readonly Axis pivotAxis;

        public SolarPivotData(Transform pivot, Axis pivotAxis)
        {
            this.pivot = pivot;
            this.pivotAxis = pivotAxis;
            this.defaultOrientation = pivot.localRotation;
        }

        public void rotateTowards(Vector3 targetPos, float rate)
        {

        }

        public void rotateTowardsDefault(float rate)
        {

        }
    }

}
