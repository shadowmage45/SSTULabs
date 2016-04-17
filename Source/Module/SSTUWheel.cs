using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System.Text;

namespace SSTUTools
{
    public class SSTUWheel : PartModule
    {

        public static int wheelLayerMask = 622593;
        public static int boundsLayer = 27; //wheelCollidersIgnore layer

        [KSPField]
        public bool lockSteering;

        [KSPField]
        public int animationID = -1;

        [KSPField(isPersistant = true)]
        public String currentStateString = WheelState.RETRACTED.ToString();

        private SSTUAnimateControlled animationControl;
        private WheelState currentState;
        private List<SSTUWheelData> wheelDatas = new List<SSTUWheelData>();
        private bool initialized;
        
        [KSPAction("Deploy/Retract Wheel", actionGroup = KSPActionGroup.Gear, guiName = "Deploy/Retract Wheel")]
        public void toggleGearAction(KSPActionParam param)
        {
            if (param.type == KSPActionType.Activate)
            {
                if (currentState == WheelState.RETRACTED || currentState==WheelState.RETRACTING || currentState==WheelState.DECOMPRESSING)
                {
                    setWheelState(WheelState.DEPLOYING);
                }
            }
            else if (param.type == KSPActionType.Deactivate)
            {
                if (currentState == WheelState.DEPLOYED || currentState == WheelState.DEPLOYING)
                {
                    setWheelState(WheelState.RETRACTING);
                }
            }
        }

        [KSPEvent(guiName = "Toggle Landing Gear", guiActive =true, guiActiveEditor =true)]
        public void toggleGearEvent()
        {
            if (currentState == WheelState.RETRACTED || currentState == WheelState.RETRACTING || currentState == WheelState.DECOMPRESSING)
            {
                setWheelState(WheelState.DEPLOYING);
            }
            else if (currentState == WheelState.DEPLOYED || currentState == WheelState.DEPLOYING)
            {
                setWheelState(WheelState.RETRACTING);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            //initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                StartCoroutine(delayedBoundsCoroutine());
            }
            GameEvents.onVesselGoOnRails.Add(new EventData<Vessel>.OnEvent(onVesselPack));
            GameEvents.onVesselGoOffRails.Add(new EventData<Vessel>.OnEvent(onVesselUnpack));
        }

        public void OnDestroy()
        {
            GameEvents.onVesselGoOnRails.Remove(new EventData<Vessel>.OnEvent(onVesselPack));
            GameEvents.onVesselGoOffRails.Remove(new EventData<Vessel>.OnEvent(onVesselUnpack));
        }

        public void onVesselPack(Vessel v)
        {

        }

        public void onVesselUnpack(Vessel v)
        {
            MonoBehaviour.print("Re-init wheels from unpack!");
            int len = wheelDatas.Count;
            for (int i = 0; i < len; i++)
            {
                wheelDatas[i].initializeWheel();
            }

        }

        public void Start()
        {
            initialize();
        }

        private IEnumerator delayedBoundsCoroutine()
        {
            yield return new WaitForFixedUpdate();
            delayedBoundsRemoval();
        }

        private void delayedBoundsRemoval()
        {
            int len = wheelDatas.Count;
            for (int i = 0; i < len; i++)
            {
                wheelDatas[i].disableBoundsCollider();
            }
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight) { return; }
            int len = wheelDatas.Count;
            switch (currentState)
            {
                case WheelState.RETRACTED:
                    break;
                case WheelState.RETRACTING:
                    break;
                case WheelState.DECOMPRESSING:
                    for (int i = 0; i < len; i++)
                    {
                        wheelDatas[i].decompress();
                    }
                    break;
                case WheelState.DEPLOYING:
                    break;
                case WheelState.DEPLOYED:                    
                    for (int i = 0; i < len; i++)
                    {
                        wheelDatas[i].updateWheel(part);
                    }
                    break;
                default:
                    break;
            }
        }

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);
            ConfigNode[] wheelDataNodes = node.GetNodes("WHEEL");
            foreach (ConfigNode wheelDataNode in wheelDataNodes)
            {
                wheelDatas.AddRange(SSTUWheelData.setupWheels(part, wheelDataNode));
            }
            currentState = (WheelState)Enum.Parse(typeof(WheelState), currentStateString);
            if (animationID < 0)// no animation
            {
                currentState = WheelState.DEPLOYED;
            }
            else
            {
                animationControl = SSTUAnimateControlled.locateAnimationController(part, animationID, onAnimationStateChanged);
            }
            setWheelState(currentState);
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                if (currentState == WheelState.RETRACTED || currentState == WheelState.RETRACTING)
                {
                    int len = wheelDatas.Count;
                    for (int i = 0; i < len; i++)
                    {
                        wheelDatas[i].disableBoundsCollider();
                    }
                }
            }
        }

        private void setWheelState(WheelState state)
        {
            bool decompress = state != WheelState.DECOMPRESSING;
            currentState = state;
            currentStateString = currentState.ToString();
            switch (currentState)
            {
                case WheelState.RETRACTED:
                    setAnimationState(AnimState.STOPPED_START);
                    break;
                case WheelState.RETRACTING:
                    setAnimationState(AnimState.PLAYING_BACKWARD);
                    break;
                case WheelState.DECOMPRESSING:
                    setAnimationState(AnimState.STOPPED_END);
                    break;
                case WheelState.DEPLOYING:
                    setAnimationState(AnimState.PLAYING_FORWARD);
                    break;
                case WheelState.DEPLOYED:
                    setAnimationState(AnimState.STOPPED_END);
                    break;
                default:
                    break;
            }

            if (decompress)
            {
                int len = wheelDatas.Count;
                for (int i = 0; i < len; i++)
                {
                    wheelDatas[i].decompressInstant();
                }
            }
        }

        public void onAnimationStateChanged(AnimState state)
        {
            if (state == AnimState.STOPPED_START) { setWheelState(WheelState.RETRACTED); }
            else if (state == AnimState.STOPPED_END) { setWheelState(WheelState.DEPLOYED); }
        }

        private void setAnimationState(AnimState state)
        {
            if (animationControl != null) { animationControl.setToState(state); }
        }
        
    }

    public enum WheelState
    {
        RETRACTED,
        RETRACTING,
        DECOMPRESSING,
        DEPLOYING,
        DEPLOYED
    }

    /// <summary>
    /// Info shared by multiple wheels, basically the names of their transforms and non-instanced data
    /// </summary>
    public class SSTUWheelInfo
    {
        public readonly String wheelColliderName = "wheelCollider";
        public readonly String wheelMeshName = "wheel";
        public readonly String wheelMeshDamagedName = "wheelDamaged";
        public readonly String suspensionTransformName = "suspension";
        public readonly String suspensionNeutralTransformName = "neutral";
        public readonly String steeringTransformName = "steering";
        public readonly String boundsColliderName = "bounds";

        public readonly float forwardExtremumSlip = -1;
        public readonly float forwardExtremumValue = -1;
        public readonly float forwardAsymptoteSlip = -1;
        public readonly float forwardAsymptoteValue = -1;
        public readonly float forwardStiffness = -1;
        public readonly float sideExtremumSlip = -1;
        public readonly float sideExtremumValue = -1;
        public readonly float sideAsymptoteSlip = -1;
        public readonly float sideAsymptoteValue = -1;
        public readonly float sideStiffness = -1;

        public readonly float suspensionOffset = 0;
        public readonly float suspensionTravel = -1;
        public readonly float suspensionTarget = -1;
        public readonly float suspensionSpring = -1;
        public readonly float suspensionDamper = -1;

        public readonly float wheelRadius = -1;
        public readonly float wheelMass = -1;
        public readonly float motorStrength = -1;
        public readonly float brakeStrength = -1;

        public readonly float steeringAngle = 0;
        public readonly bool invertSteering = false;

        public SSTUWheelInfo(ConfigNode node)
        {
            wheelColliderName = node.GetStringValue("wheelColliderName", wheelColliderName);
            wheelMeshName = node.GetStringValue("wheelMeshName", wheelMeshName);
            wheelMeshDamagedName = node.GetStringValue("wheelMeshDamagedName", wheelMeshDamagedName);
            suspensionTransformName = node.GetStringValue("suspensionTransformName", suspensionTransformName);
            suspensionNeutralTransformName = node.GetStringValue("suspensionNeutralName", suspensionNeutralTransformName);
            steeringTransformName = node.GetStringValue("steeringTransformName", steeringTransformName);
            boundsColliderName = node.GetStringValue("boundsColliderName", boundsColliderName);

            forwardExtremumSlip = node.GetFloatValue("forwardExtremumSlip", forwardExtremumSlip);
            forwardExtremumValue = node.GetFloatValue("forwardExtremumValue", forwardExtremumValue);
            forwardAsymptoteSlip = node.GetFloatValue("forwardAsymptoteSlip", forwardAsymptoteSlip);
            forwardAsymptoteValue = node.GetFloatValue("forwardAsymptoteValue", forwardAsymptoteValue);

            sideExtremumSlip = node.GetFloatValue("sideExtremumSlip", sideExtremumSlip);
            sideExtremumValue = node.GetFloatValue("sideExtremumValue", sideExtremumValue);
            sideAsymptoteSlip = node.GetFloatValue("sideAsymptoteSlip", sideAsymptoteSlip);
            sideAsymptoteValue = node.GetFloatValue("sideAsymptoteValue", sideAsymptoteValue);

            suspensionTravel = node.GetFloatValue("suspensionTravel", suspensionTravel);
            suspensionOffset = node.GetFloatValue("suspensionOffset", suspensionOffset);
            suspensionTarget = node.GetFloatValue("suspensionTarget", suspensionTarget);
            suspensionSpring = node.GetFloatValue("suspensionSpring", suspensionSpring);
            suspensionDamper = node.GetFloatValue("suspensionDamper", suspensionDamper);

            wheelRadius = node.GetFloatValue("wheelRadius", wheelRadius);
            wheelMass = node.GetFloatValue("wheelMass", wheelMass);
            motorStrength = node.GetFloatValue("motorStrength", motorStrength);
            brakeStrength = node.GetFloatValue("brakeStrength", brakeStrength);

            steeringAngle = node.GetFloatValue("steeringAngle", steeringAngle);
            invertSteering = node.GetBoolValue("invertSteering", invertSteering);
        }
    }
    
    public class SSTUWheelData
    {
        public readonly SSTUWheelInfo wheelInfo;
        public readonly GameObject debugModel;
        public readonly Transform wheelColliderTransform;
        public readonly Transform suspensionTransform;
        public readonly Transform steeringTransform;
        public readonly Transform wheelMesh;
        public readonly Transform wheelDamagedMesh;
        public readonly Transform suspensionNeutral;
        public readonly Transform boundsCollider;

        private Quaternion steeringDefaultOrientation;
        private float fullBrakeValue;
        private float fullMotorValue;
        private float decompressTime = 0f;
        private WheelCollider wheelCollider;

        public SSTUWheelData(SSTUWheelInfo info, Transform wheelColliderTransform, Transform suspension, Transform neutral, Transform steering, Transform wheelMesh, Transform wheelDamagedMesh, Transform boundsCollider)
        {
            this.wheelInfo = info;
            this.wheelColliderTransform = wheelColliderTransform;
            this.suspensionTransform = suspension;
            this.steeringTransform = steering;
            this.wheelMesh = wheelMesh;
            this.wheelDamagedMesh = wheelDamagedMesh;
            this.suspensionNeutral = neutral;
            this.boundsCollider = boundsCollider;

            if (steering != null && info.steeringAngle > 0)
            {
                steeringDefaultOrientation = steering.localRotation;
            }

            if (HighLogic.LoadedSceneIsFlight)
            {
                debugModel = SSTUUtils.cloneModel("SSTU/Assets/DEBUG_MODEL");
                debugModel.SetActive(true);
                debugModel.transform.NestToParent(wheelColliderTransform.gameObject.transform);
            }
        }
        
        public void initializeWheel()
        {
            wheelCollider = wheelColliderTransform.GetComponent<WheelCollider>();
            if (wheelCollider != null) { Component.DestroyImmediate(wheelCollider); }
            wheelCollider = wheelColliderTransform.gameObject.AddComponent<WheelCollider>();

            wheelCollider.brakeTorque = 0;
            wheelCollider.motorTorque = 0;
            wheelCollider.forceAppPointDistance = 1.0f;

            wheelCollider.mass = wheelInfo.wheelMass;
            wheelCollider.radius = wheelInfo.wheelRadius;
            wheelCollider.suspensionDistance = wheelInfo.suspensionTravel;

            float target = wheelInfo.suspensionTarget == -1 ? wheelCollider.suspensionSpring.targetPosition : wheelInfo.suspensionTarget;
            float spring = wheelInfo.suspensionSpring == -1 ? wheelCollider.suspensionSpring.spring : wheelInfo.suspensionSpring;
            float damper = wheelInfo.suspensionDamper == -1 ? wheelCollider.suspensionSpring.damper : wheelInfo.suspensionDamper;
            fullBrakeValue = wheelInfo.brakeStrength == -1 ? wheelCollider.brakeTorque : wheelInfo.brakeStrength;
            fullMotorValue = wheelInfo.motorStrength == -1 ? wheelCollider.motorTorque : wheelInfo.motorStrength;

            float fwdExSlip = wheelInfo.forwardExtremumSlip == -1 ? wheelCollider.forwardFriction.extremumSlip : wheelInfo.forwardExtremumSlip;
            float fwdExVal = wheelInfo.forwardExtremumValue == -1 ? wheelCollider.forwardFriction.extremumValue : wheelInfo.forwardExtremumValue;
            float fwdAsSlip = wheelInfo.forwardAsymptoteSlip == -1 ? wheelCollider.forwardFriction.asymptoteSlip : wheelInfo.forwardAsymptoteSlip;
            float fwdAsVal = wheelInfo.forwardAsymptoteValue == -1 ? wheelCollider.forwardFriction.asymptoteValue : wheelInfo.forwardAsymptoteValue;
            float fwdStiff = wheelInfo.forwardStiffness == -1 ? wheelCollider.forwardFriction.stiffness : wheelInfo.forwardStiffness;
            float sideExSlip = wheelInfo.sideExtremumSlip == -1 ? wheelCollider.sidewaysFriction.extremumSlip : wheelInfo.sideExtremumSlip;
            float sideExVal = wheelInfo.sideExtremumValue == -1 ? wheelCollider.sidewaysFriction.extremumValue : wheelInfo.sideExtremumValue;
            float sideAsSlip = wheelInfo.sideAsymptoteSlip == -1 ? wheelCollider.sidewaysFriction.asymptoteSlip : wheelInfo.sideAsymptoteSlip;
            float sideAsVal = wheelInfo.sideAsymptoteValue == -1 ? wheelCollider.sidewaysFriction.asymptoteValue : wheelInfo.sideAsymptoteValue;
            float sideStiff = wheelInfo.sideStiffness == -1 ? wheelCollider.sidewaysFriction.stiffness : wheelInfo.sideStiffness;
            
            WheelFrictionCurve fwdCurve = wheelCollider.forwardFriction;
            fwdCurve.extremumSlip = fwdExSlip;
            fwdCurve.extremumValue = fwdExVal;
            fwdCurve.asymptoteSlip = fwdAsSlip;
            fwdCurve.asymptoteValue = fwdAsVal;
            fwdCurve.stiffness = fwdStiff;
            wheelCollider.forwardFriction = fwdCurve;

            WheelFrictionCurve sideCurve = wheelCollider.sidewaysFriction;
            sideCurve.extremumSlip = sideExSlip;
            sideCurve.extremumValue = sideExVal;
            sideCurve.asymptoteSlip = sideAsSlip;
            sideCurve.asymptoteValue = sideAsVal;
            sideCurve.stiffness = sideStiff;
            wheelCollider.sidewaysFriction = sideCurve;
            
            JointSpring joint = new JointSpring();
            joint.spring = spring;
            joint.damper = damper;
            joint.targetPosition = target;
            
            wheelCollider.suspensionSpring = joint;
            wheelCollider.enabled = true;
        }

        public void disableBoundsCollider()
        {
            Collider c = boundsCollider == null ? null : boundsCollider.GetComponent<Collider>();
            if (c!=null)
            {
                Component.Destroy(c);
                c.enabled = false;
            }
        }

        public void updateWheel(Part part)
        {
            updateSuspension(part);
            updateWheelRotation();
            updateSteering(part);
            updateMotor(part);
            updateBrake(part);
        }

        private void updateSuspension(Part part)
        {
            if (suspensionTransform == null || wheelCollider==null) { return; }            
            RaycastHit hit;
            float wheelRadius = wheelCollider.radius * part.rescaleFactor;
            float suspensionTravel = (wheelCollider.suspensionDistance + wheelRadius) * part.rescaleFactor;
            float rayCastLength = suspensionTravel*2f;
            int mask = SSTUWheel.wheelLayerMask;
            if (Physics.Raycast(wheelCollider.transform.position, -wheelCollider.transform.up, out hit, rayCastLength, mask))
            {
                float distance = Vector3.Distance(hit.point, wheelCollider.transform.position);
                if (distance > suspensionTravel) { distance = suspensionTravel; }                
                distance -= wheelRadius;
                float compression = (wheelCollider.suspensionDistance*part.rescaleFactor) - distance;
                suspensionTransform.position = suspensionNeutral.position + wheelCollider.transform.up * compression;                
            }
            else
            {
                suspensionTransform.localPosition = suspensionNeutral.localPosition;
            }
        }

        public void decompress()
        {

        }

        public void decompressInstant()
        {
            suspensionTransform.localPosition = suspensionNeutral.localPosition;
            decompressTime = 0f;
        }

        private void updateWheelRotation()
        {
            if (wheelMesh == null || wheelCollider==null || wheelCollider.rpm==0) { return; }
            float rotation = Time.deltaTime * wheelCollider.rpm / 60 * 360;
            wheelMesh.Rotate(Vector3.left, rotation);
        }

        private void updateSteering(Part part)
        {
            if (part.vessel == null) { return; }
            if (wheelCollider == null || wheelInfo.steeringAngle == 0) { return; }
            Vessel vessel = part.vessel;
            float steeringValue = vessel.ctrlState.wheelSteer + vessel.ctrlState.wheelSteerTrim;
            float steeringAngle = steeringValue * wheelInfo.steeringAngle;
            steeringAngle = -steeringAngle;
            if (wheelInfo.invertSteering) { steeringAngle = -steeringAngle; }
            wheelCollider.steerAngle = steeringAngle;
            if (steeringTransform == null) { return; }
            steeringTransform.localRotation = steeringDefaultOrientation;
            steeringTransform.Rotate(Vector3.up, steeringAngle, Space.Self);
        }

        private void updateMotor(Part part)
        {
            if (fullMotorValue <= 0) { return; }            
            float input = part.vessel.ctrlState.wheelThrottle + part.vessel.ctrlState.wheelThrottleTrim;
            wheelCollider.motorTorque = input * fullMotorValue;
        }

        private void updateBrake(Part part)
        {
            if (fullBrakeValue <= 0) { return; }
            if (part.vessel.ActionGroups[KSPActionGroup.Brakes])
            {
                wheelCollider.brakeTorque = fullBrakeValue;
            }
            else
            {
                wheelCollider.brakeTorque = 0;
            }
        }

        public static SSTUWheelData[] setupWheels(Part part, ConfigNode wheelDataNode)
        {
            SSTUWheelInfo info = new SSTUWheelInfo(wheelDataNode);

            Transform[] wheelColliderTransforms = part.transform.FindChildren(info.wheelColliderName);
            Transform[] suspensionTransforms = part.transform.FindChildren(info.suspensionTransformName);
            Transform[] suspensionNeutralTransforms = part.transform.FindChildren(info.suspensionNeutralTransformName);
            Transform[] steeringTransforms = part.transform.FindChildren(info.steeringTransformName);
            Transform[] wheelMeshes = part.transform.FindChildren(info.wheelMeshName);
            Transform[] wheelDamagedMeshes = part.transform.FindChildren(info.wheelMeshDamagedName);
            Transform[] boundsColliders = part.transform.FindChildren(info.boundsColliderName);

            int len = wheelColliderTransforms.Length;
            int susLen = suspensionTransforms.Length;
            int nutLen = suspensionNeutralTransforms.Length;
            int steerLen = steeringTransforms.Length;
            int meshLen = wheelMeshes.Length;
            int meshDamLen = wheelDamagedMeshes.Length;
            int boundsLen = boundsColliders.Length;

            SSTUWheelData wheelData;
            Transform suspensionTransform;
            Transform suspensionNeutral;
            Transform steeringTransform;
            Transform wheelMesh;
            Transform wheelDamagedMesh;
            Transform boundsCollider;
            WheelCollider wheelCollider;

            SSTUWheelData[] wheelDatas = new SSTUWheelData[len];

            for (int i = 0; i < len; i++)
            {
                wheelCollider = wheelColliderTransforms[i].GetComponent<WheelCollider>();                
                if (wheelCollider != null)
                {
                    Component.DestroyImmediate(wheelCollider);
                }
                suspensionTransform = susLen == len ? suspensionTransforms[i] : null;
                steeringTransform = steerLen == len ? steeringTransforms[i] : null;
                suspensionNeutral = nutLen == len ? suspensionNeutralTransforms[i] : null;
                wheelMesh = meshLen == len ? wheelMeshes[i] : null;
                wheelDamagedMesh = meshDamLen == len ? wheelDamagedMeshes[i] : null;
                boundsCollider = boundsLen == len ? boundsColliders[i] : null;                
                if (boundsCollider != null)
                {
                    boundsCollider.gameObject.layer = SSTUWheel.boundsLayer;
                }

                wheelData = new SSTUWheelData(info, wheelColliderTransforms[i], suspensionTransform, suspensionNeutral, steeringTransform, wheelMesh, wheelDamagedMesh, boundsCollider);                
                wheelDatas[i] = wheelData;
            }

            return wheelDatas;
        }

    }
}
