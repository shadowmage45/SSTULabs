using System;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUAnimateRotation : PartModule
    {

        [KSPField]
        public string transformName;

        [KSPField]
        public string secondaryTransformName;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Rotation", isPersistant = true),
         UI_FloatEdit(suppressEditorShipModified = true, minValue = 0, maxValue = 10, incrementLarge = 5, incrementSmall = 1, incrementSlide = 0.1f, sigFigs = 2, unit = "rpm")]
        public float rpm = 1;

        [KSPField]
        public float secondaryRotationMultiplier = 1f;

        [KSPField]
        public float gCalcRadius = 5f;

        [KSPField]
        public float minRPM = 0f;

        [KSPField]
        public float maxRPM = 10f;

        [KSPField(guiActive = true, guiActiveEditor = true, guiUnits = "g", guiName = "ArtificialGravity")]
        public float displayGravity = 0.0f;

        [KSPField]
        public Vector3 rotationAxis = Vector3.forward;

        [KSPField]
        public Vector3 secondaryRotationAxis = Vector3.forward;

        [KSPField]
        public bool autoRotate = true;

        [KSPField]
        public bool showGravityDisplay = true;
        
        [KSPField(isPersistant = true)]
        public bool rotating = false;

        [KSPField(isPersistant = true)]
        public float rotation = 0f;

        private bool initialized = false;
        private SSTUInflatable inflatable;
        private Transform[] transforms;
        private Transform[] secondaryTransforms;

        //---------------- rpm   *  degPerRot *  degToRad *  minToSec
        //radiansPerSec = rotPerMin * 360 * 0.0174533 * 0.016666666
        //display gravity = radiansPerSec * radiansPerSec * radiusMeters
        private static readonly float rpmToRadiansPerSecond = 360 * 0.0166666f * 0.0174533f;

        [KSPEvent(guiName = "Start Rotation", guiActive = true, guiActiveEditor = true)]
        public void toggleRotationEvent()
        {
            if (autoRotate) { return; }//controlled by animation state
            rotating = !rotating;
            updateUIControlState(true);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            init();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            init();
            Fields[nameof(displayGravity)].guiActive = Fields[nameof(displayGravity)].guiActiveEditor = showGravityDisplay;
            UI_FloatEdit ufe = (UI_FloatEdit) (HighLogic.LoadedSceneIsEditor? Fields[nameof(rpm)].uiControlEditor : Fields[nameof(rpm)].uiControlFlight);
            ufe.minValue = minRPM;
            ufe.maxValue = maxRPM;
        }

        public void Start()
        {
            inflatable = part.GetComponent<SSTUInflatable>();
            if (inflatable != null)
            {
                inflatable.setupRotationModule(this);
            }
        }

        public void Update()
        {
            if (rotating && rpm > 0)
            {
                float rotationPerFrame = Time.deltaTime * (rpm * 0.0166666666666667f) * 360f;
                rotation += rotationPerFrame;
                rotation = rotation % 360f;
                int len = transforms.Length;
                for (int i = 0; i < len; i++)
                {
                    transforms[i].Rotate(rotationAxis, rotationPerFrame, Space.Self);
                }
                len = secondaryTransforms.Length;
                rotationPerFrame *= secondaryRotationMultiplier;
                for (int i = 0; i < len; i++)
                {
                    secondaryTransforms[i].Rotate(secondaryRotationAxis, rotationPerFrame, Space.Self);
                }
                float radSec = rpm * rpmToRadiansPerSecond;
                float gravMetersPerSecond = radSec * radSec * gCalcRadius;
                //gravity in Gs = metersPerSecond / 9.81 = metersPerSecond * 0.101931
                displayGravity = gravMetersPerSecond * 0.101931f;                
            }
            else
            {
                displayGravity = 0f;
            }
        }
        
        private void init()
        {
            if (initialized) { return; }
            initialized = true;
            transforms = part.transform.FindRecursive("model").FindChildren(transformName);
            secondaryTransforms = string.IsNullOrEmpty(secondaryTransformName) ? secondaryTransforms = new Transform[0] : part.transform.FindRecursive("model").FindChildren(secondaryTransformName);
            float restoredRotation = rotation;
            int len = transforms.Length;
            for (int i = 0; i < len; i++)
            {
                transforms[i].Rotate(rotationAxis, restoredRotation, Space.Self);
            }
            len = secondaryTransforms.Length;
            restoredRotation *= secondaryRotationMultiplier;
            for (int i = 0; i < len; i++)
            {
                secondaryTransforms[i].Rotate(secondaryRotationAxis, restoredRotation, Space.Self);
            }
        }

        public void initializeRotationModule(AnimState loadedState)
        {
            rotating = loadedState == AnimState.STOPPED_END && (rotating || autoRotate);
            updateUIControlState(loadedState == AnimState.STOPPED_END && !autoRotate);
        }

        public void onAnimationStateChange(AnimState newState)
        {
            if (autoRotate)
            {
                rotating = newState == AnimState.STOPPED_END;
            }
            bool uiEnabled = newState == AnimState.STOPPED_END && !autoRotate;
            updateUIControlState(uiEnabled);
        }

        private void updateUIControlState(bool enable)
        {
            BaseEvent evt = Events[nameof(toggleRotationEvent)];
            evt.guiActive = evt.guiActiveEditor = enable;
            evt.guiName = rotating ? "Stop Rotation" : "Start Rotation";
        }
    }
}
