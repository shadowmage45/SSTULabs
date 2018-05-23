using System;
using System.Collections.Generic;
using UnityEngine;
namespace SSTUTools
{

    /// <summary>
    /// Generic animation module intended to be controlled by other PartModules.
    /// <para>Does not include any GUI or direct-user-interactivity; all state changes
    /// must be initiated from external sources</para>
    /// Includes provisions for the animation data to be supplied entirely by external modules as well, to be used
    /// by MFT or other modules that want optional animation support.
    /// </summary>
    public class SSTUAnimateControlled : PartModule, IScalarModule
    {
        /// <summary>
        /// IScalarModule animationID -- used by some stock systems to locate an animated part/model/module
        /// </summary>
        [KSPField]
        public string animationID = "animation";

        [KSPField(isPersistant = true)]
        public float animationMaxDeploy = 1;

        [KSPField]
        public int animationLayer = 1;

        [KSPField(isPersistant = true)]
        public String persistentState = AnimState.STOPPED_START.ToString();

        [Persistent]
        public string configNodeData = string.Empty;

        private AnimationModule animationModule;

        private EventData<float> evt1;

        private EventData<float, float> evt2;

        #region REGION - GUI Interaction Methods

        [KSPAction("Toggle")]
        public void toggleAnimationAction(KSPActionParam param)
        {
            animationModule.onToggleAction(param);
        }

        [KSPEvent(guiName = "Enable", guiActive = true, guiActiveEditor = true)]
        public void enableAnimationEvent()
        {
            animationModule.onDeployEvent();
        }

        [KSPEvent(guiName = "Disable", guiActive = true, guiActiveEditor = true)]
        public void disableAnimationEvent()
        {
            animationModule.onRetractEvent();
        }

        #endregion ENDREGION - GUI Interaction Methods

        #region REGION - IScalarModule fields/methods

        public string ScalarModuleID
        {
            get
            {
                return animationID;
            }
        }

        public float GetScalar
        {            
            get
            {
                if (animationModule.animState == AnimState.STOPPED_END) { return 1.0f; }
                return animationModule.animTime;
            }
        }

        public bool CanMove
        {
            get
            {
                return animationModule.enabled;
            }
        }

        public EventData<float, float> OnMoving
        {
            get
            {
                return evt2;
            }
        }

        public EventData<float> OnStop
        {
            get
            {
                return evt1;
            }
        }

        public void SetScalar(float t)
        {
            animationModule.animTime = t;
        }

        public void SetUIRead(bool state)
        {
            //TODO
        }

        public void SetUIWrite(bool state)
        {
            //TODO
        }

        public bool IsMoving()
        {
            AnimState state = animationModule.animState;
            return state != AnimState.STOPPED_END && state != AnimState.STOPPED_START;
        }

        #endregion ENDREGION - IScalarModule fields/methods

        public override void OnAwake()
        {
            base.OnAwake();
            evt1 = new EventData<float>("SSTUAnimateControlledEvt1");
            evt2 = new EventData<float, float>("SSTUAnimateControlledEvt2");
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }
                
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
        }

        public void Start()
        {
            updateAirstreamShield();
        }

        private void initialize()
        {
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);            
            
            AnimationData animData = new AnimationData(node.GetNode("ANIMATIONDATA"));

            animationModule = new AnimationModule(part, this, nameof(persistentState), nameof(animationMaxDeploy), nameof(enableAnimationEvent), nameof(disableAnimationEvent));
            animationModule.getSymmetryModule = m => ((SSTUAnimateControlled)m).animationModule;
            animationModule.setupAnimations(animData, part.transform.FindRecursive("model"), animationLayer);
            animationModule.onAnimStateChangeCallback = onAnimStateChange;
        }

        public void Update()
        {
            animationModule.Update();
        }

        private void onAnimStateChange(AnimState newState)
        {
            fireEvents(newState);
            updateAirstreamShield();
        }

        private void updateAirstreamShield()
        {
            SSTUAirstreamShield sass = part.GetComponent<SSTUAirstreamShield>();
            if (sass != null)
            {
                sass.updateShieldStatus();
            }
        }

        private void fireEvents(AnimState newState)
        {
            switch (newState)
            {
                case AnimState.STOPPED_START:
                    OnStop.Fire(0f);
                    break;
                case AnimState.STOPPED_END:
                    OnStop.Fire(1f);
                    break;
                case AnimState.PLAYING_FORWARD:
                    OnMoving.Fire(0, 1);
                    break;
                case AnimState.PLAYING_BACKWARD:
                    OnMoving.Fire(1, 0);
                    break;
                default:
                    break;
            }
        }

    }
}

