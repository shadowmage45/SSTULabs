using UnityEngine;

namespace SSTUTools
{
    public class SSTUAnimateRotation : PartModule
    {

        [KSPField]
        public string transformName;

        [KSPField]
        public float rpm = 1;

        [KSPField]
        public Vector3 rotationAxis = Vector3.up;

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
            if (rotating)
            {
                float rotationPerFrame = Time.deltaTime  * (rpm * 0.0166666666666667f) * 360f;
                int len = transforms.Length;
                for (int i = 0; i < len; i++)
                {
                    transforms[i].Rotate(rotationAxis, rotationPerFrame, Space.Self);
                }
            }
        }
        
        private void init()
        {
            if (initialized) { return; }
            initialized = true;
            transforms = part.transform.FindChildren(transformName);
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
