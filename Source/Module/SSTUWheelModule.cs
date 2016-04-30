using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System.Text;

namespace SSTUTools
{
    public class SSTUWheelModule : PartModule
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
        private KSPWheel wheel;        
        private bool initialized;
        private Transform wheelMesh;
        private Transform suspensionMesh;
        private Transform suspensionNeutral;
        
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
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
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

        }

        public void Start()
        {
            //initialize();
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) { return; }
            switch (currentState)
            {
                case WheelState.RETRACTED:
                    break;
                case WheelState.RETRACTING:
                    break;
                case WheelState.DECOMPRESSING:
                    break;
                case WheelState.DEPLOYING:
                    break;
                case WheelState.DEPLOYED:
                    wheel.rb = part.Rigidbody;
                    wheel.FixedUpdate();
                    wheelMesh.position = wheel.wheelMeshPosition;
                    suspensionMesh.position = suspensionNeutral.position - (suspensionMesh.transform.up *  (wheel.suspensionLength - wheel.compressionDistance - wheel.wheelRadius));
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
            SSTUWheelInfo info = new SSTUWheelInfo(node.GetNode("WHEEL"));

            Transform colliderTransform = part.transform.FindRecursive(info.wheelColliderName);
            if (colliderTransform == null)
            {
                MonoBehaviour.print("ERROR: collider transform was null for name: " + info.wheelColliderName);
            }
            wheel = new KSPWheel();
            wheel.wheel = colliderTransform.gameObject;
            wheel.wheelRadius = info.wheelRadius;
            wheel.spring = info.suspensionSpring;
            wheel.damper = info.suspensionDamper;
            wheel.suspensionLength = info.suspensionTravel;
            wheel.target = info.suspensionTarget;
            wheel.fwdFrictionConst = info.forwardFrictionConstant;
            wheel.sideFrictionConst = info.sidewaysFrictionConstant;
            wheel.maxSteerAngle = info.steeringAngle;
            wheel.motorTorque = info.motorStrength;
            wheel.brakeTorque = info.brakeStrength;
            wheel.wheelMass = info.wheelMass;

            wheelMesh = part.transform.FindRecursive(info.wheelMeshName);
            suspensionMesh = part.transform.FindRecursive(info.suspensionTransformName);
            suspensionNeutral = part.transform.FindRecursive(info.suspensionNeutralTransformName);
                        
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

            //if (decompress)
            //{
            //    int len = wheelDatas.Count;
            //    for (int i = 0; i < len; i++)
            //    {
            //        wheelDatas[i].decompressInstant();
            //    }
            //}
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

        public readonly float forwardFrictionConstant;
        public readonly float sidewaysFrictionConstant;

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
            
            suspensionTravel = node.GetFloatValue("suspensionTravel", suspensionTravel);
            suspensionOffset = node.GetFloatValue("suspensionOffset", suspensionOffset);
            suspensionTarget = node.GetFloatValue("suspensionTarget", suspensionTarget);
            suspensionSpring = node.GetFloatValue("suspensionSpring", suspensionSpring);
            suspensionDamper = node.GetFloatValue("suspensionDamper", suspensionDamper);

            forwardFrictionConstant = node.GetFloatValue("forwardFriction");
            sidewaysFrictionConstant = node.GetFloatValue("sidewaysFriction");

            wheelRadius = node.GetFloatValue("wheelRadius", wheelRadius);
            wheelMass = node.GetFloatValue("wheelMass", wheelMass);
            motorStrength = node.GetFloatValue("motorStrength", motorStrength);
            brakeStrength = node.GetFloatValue("brakeStrength", brakeStrength);

            steeringAngle = node.GetFloatValue("steeringAngle", steeringAngle);
            invertSteering = node.GetBoolValue("invertSteering", invertSteering);
        }
    }
    
}
