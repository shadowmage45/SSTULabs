using UnityEngine;

namespace SSTUTools
{
    public class SSTUAnimateRotation : PartModule
    {

        [KSPField]
        public string transformName;

        [KSPField]
        public string secondaryTransformName;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Rotation"),
         UI_FloatEdit(suppressEditorShipModified = true, minValue = 0, maxValue = 10, incrementLarge = 5, incrementSmall = 1, incrementSlide = 0.1f, sigFigs = 2, unit = "rpm")]
        public float rpm = 1;

        [KSPField]
        public float secondaryRotationMultiplier = 1f;

        [KSPField]
        public float gCalcRadius = 5f;

        [KSPField(guiActive = true, guiActiveEditor = true, guiUnits = "g", guiName = "ArtificialGravity")]
        public float displayGravity = 0.0f;

        [KSPField]
        public Vector3 rotationAxis = Vector3.forward;

        [KSPField]
        public Vector3 secondaryRotationAxis = Vector3.forward;

        [KSPField]
        public bool autoRotate = true;

        /// <summary>
        /// If this is >=0, interaction buttons will only display when the dependent animation is in the deployed state
        /// </summary>
        [KSPField]
        public int animationID = -1;
        
        [KSPField(isPersistant = true)]
        public bool rotating = false;

        private bool initialized = false;
        private SSTUAnimateControlled animController;
        private Transform[] transforms;
        private Transform[] secondaryTransforms;

        //---------------- rpm   *  degPerRot *  degToRad *  minToSec
        //radiansPerSec = rotPerMin * 360 * 0.0174533 * 0.016666666
        //display gravity = radiansPerSec * radiansPerSec * radiusMeters
        private static readonly float rpmToRadiansPerSecond = 360 * 0.0166666f * 0.0174533f;

        [KSPEvent(guiName = "Start Rotation", guiActive = true, guiActiveEditor = true)]
        public void toggleRotationEvent()
        {
            if (autoRotate) { return; }//controlled by parent animation state
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
        }

        public void Start()
        {
            AnimState state = AnimState.STOPPED_END;
            if (animationID >= 0)
            {
                animController = SSTUAnimateControlled.locateAnimationController(part, animationID, onAnimStateChange);
                state = animController.getAnimationState();
                if (state == AnimState.STOPPED_END && autoRotate)
                {
                    rotating = true;
                }
            }
            bool uiEnabled = state == AnimState.STOPPED_END && !autoRotate;
            updateUIControlState(uiEnabled);
        }

        public void Update()
        {
            if (rotating && rpm > 0)
            {
                float rotationPerFrame = Time.deltaTime * (rpm * 0.0166666666666667f) * 360f;
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
            transforms = part.transform.FindChildren(transformName);
            secondaryTransforms = string.IsNullOrEmpty(secondaryTransformName) ? secondaryTransforms = new Transform[0] : part.transform.FindChildren(secondaryTransformName);
        }

        private void onAnimStateChange(AnimState newState)
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
            BaseEvent evt = Events["toggleRotationEvent"];
            evt.guiActive = evt.guiActiveEditor = enable;
            evt.guiName = rotating ? "Stop Rotation" : "Start Rotation";
        }
    }
}
