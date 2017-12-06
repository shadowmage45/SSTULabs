using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{

    /// <summary>
    /// Wrapper for animation handling and UI setup.  Should include all functions needed to support save, load, and UI interaction.
    /// Intended to wrap a single 'set' of animations that will respond to a single deploy/retract button/action-group.
    /// </summary>
    public class AnimationModule<T> where T : PartModule
    {
        /// <summary>
        /// The part that this container class belongs to
        /// </summary>
        public readonly Part part;

        /// <summary>
        /// The direct owning part-module for this container class
        /// </summary>
        public readonly T module;

        /// <summary>
        /// Reference to the persistent data field for this animation.
        /// </summary>
        public readonly BaseField persistentDataField;

        /// <summary>
        /// Reference to the deploy-limit field for this animation.  May be null if deploy limit is not supported by the owning PartModule.
        /// </summary>
        public readonly BaseField deployLimitField;

        /// <summary>
        /// Reference to the deploy event from the PartModule, used to update GUI status depending on current animation status and availability (no anim = UI disabled)
        /// </summary>
        public readonly BaseEvent deployEvent;

        /// <summary>
        /// Reference to the retract event from the PartModule, used to update GUI status depending on current animation status and availability (no anim = UI disabled)
        /// </summary>
        public readonly BaseEvent retractEvent;
        
        /// <summary>
        /// Internal cache of the current animation state as an Enum
        /// </summary>
        private AnimState animationState = AnimState.STOPPED_START;

        /// <summary>
        /// Internal cache of the current list of animation data blocks.
        /// </summary>
        private List<SSTUAnimData> animationData = new List<SSTUAnimData>();

        /// <summary>
        /// Internal cache of the current animation position.
        /// </summary>
        private float animationPosition = 0f;

        /// <summary>
        /// Internal cache of if retract/deploy should be usable while the vessel is not the currently focused vessel.
        /// </summary>
        private bool usableUnfocused;

        /// <summary>
        /// Internal cache of if retract/deploy should be usable while the vessel is not currently controllable/commanded (no comm-net connection, or no probe core)
        /// </summary>
        private bool usableUncommanded;

        /// <summary>
        /// Internal chache of 'eva-only' flag for animation.  If set to true, the animation will -only- be available to EVA kerbals when vessel is not the currently focused vessel.
        /// TODO -- verify the above information is actually how this flag works
        /// </summary>
        private bool usableEVA;

        public float deployLimit
        {
            get { return deployLimitField == null ? 1.0f : deployLimitField.GetValue<float>(module); }
        }

        public float animTime
        {
            get { return animationPosition; }
        }

        public AnimState animState
        {
            get { return animationState; }
        }

        public string persistentData
        {
            get { return persistentDataField.GetValue<string>(module); }
            set { persistentDataField.SetValue(value, module); }
        }
        
        public AnimationModule(Part part, T module, BaseField persistence, BaseField deployLimit, BaseEvent deploy, BaseEvent retract)
        {
            this.part = part;
            this.module = module;
            this.persistentDataField = persistence;
            this.deployLimitField = deployLimit;
            if (deployLimitField != null)
            {
                deployLimitField.uiControlEditor.onFieldChanged = onDeployLimitUpdated;
                deployLimitField.uiControlFlight.onFieldChanged = onDeployLimitUpdated;
            }
            this.deployEvent = deploy;
            this.retractEvent = retract;
            loadAnimationState(persistentData);
        }

        /// <summary>
        /// Can be called at any point, but if called late in lifecycle, the UI update method should be called to update the UI field visibility immediately.
        /// </summary>
        /// <param name="unfocused"></param>
        /// <param name="eva"></param>
        /// <param name="uncommanded"></param>
        public void setUsableFlags(bool unfocused, bool eva, bool uncommanded)
        {
            usableUnfocused = unfocused;
            usableEVA = eva;
            usableUncommanded = uncommanded;
        }

        /// <summary>
        /// Internal method to load animation persistent data from the persistent data string.
        /// </summary>
        /// <param name="persistence"></param>
        private void loadAnimationState(string persistence)
        {
            if (!string.IsNullOrEmpty(persistence))
            {
                animationState = (AnimState)Enum.Parse(typeof(AnimState), persistence);
            }
            if (animationState == AnimState.PLAYING_BACKWARD)
            {
                animationState = AnimState.STOPPED_START;
                animationPosition = 0f;
            }
            else if (animationState == AnimState.PLAYING_FORWARD)
            {
                animationState = AnimState.STOPPED_END;
                animationPosition = deployLimit;
            }
            else if (animationState == AnimState.STOPPED_END)
            {
                animationPosition = deployLimit;
            }
            else if (animationState == AnimState.STOPPED_START)
            {
                animationPosition = 0f;
            }
        }

        /// <summary>
        /// Update the backing persistent data field with the string representation of the current animation state.
        /// </summary>
        private void updatePersistentData()
        {
            persistentData = animationState.ToString();
        }

        #region REGION - UI INTERACTION

        /// <summary>
        /// Internal method that is called whenever the UI slider for the deploy limit is changed.<para/>
        /// Updates the animations' current cached deploy limits, and will stop the animation if it is currently playing and the limit is adjusted to a point prior to the current time.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        private void onDeployLimitUpdated(BaseField a, System.Object b)
        {
            int len = animationData.Count;
            bool shouldStop = false;
            for (int i = 0; i < len; i++)
            {
                shouldStop = shouldStop || animationData[i].setMaxTime(deployLimit, animationState);
            }
            if (shouldStop)
            {
                stopAnimation();
                animationState = AnimState.STOPPED_END;
            }
        }

        /// <summary>
        /// Should be called directly from the PartModule when the KSPEvent for deploy is called.
        /// </summary>
        public virtual void onDeployEvent()
        {
            if (animationState == AnimState.STOPPED_START || animationState == AnimState.PLAYING_BACKWARD)
            {
                setAnimState(AnimState.PLAYING_FORWARD);
                updateUIState();
            }
        }

        /// <summary>
        /// Should be called directly from the PartModule when the KSPEvent for retract is called.
        /// </summary>
        public virtual void onRetractEvent()
        {
            if (animationState == AnimState.STOPPED_END || animationState == AnimState.PLAYING_FORWARD)
            {
                setAnimState(AnimState.PLAYING_FORWARD);
                updateUIState();
            }
        }

        /// <summary>
        /// Should be called directly from the PartModule when the KSPAction for deploy is activated.
        /// </summary>
        public void onDeployAction(KSPActionParam param)
        {
            onDeployEvent();
        }

        /// <summary>
        /// Should be called directly from the PartModule when the KSPAction for retract is activated.
        /// </summary>
        public void onRetractAction(KSPActionParam paran)
        {
            onRetractEvent();
        }

        /// <summary>
        /// Should be called directly from the PartModule when the KSPAction for toggle is activated.
        /// </summary>
        public void onToggleAction(KSPActionParam param)
        {
            if (animationState == AnimState.STOPPED_START || animationState == AnimState.PLAYING_BACKWARD)
            {
                onDeployEvent();
            }
            else
            {
                onRetractEvent();
            }
        }

        #endregion ENDREGION - UI INTERACTION

        #region REGION - Initialization and Updating

        /// <summary>
        /// Should be called after constructor to initialize the internal animation data.<para/>
        /// Should also be called anytime the animation data needs changed/updated from parent module/models.
        /// </summary>
        /// <param name="anims"></param>
        public void setupAnimations(SSTUAnimData[] anims)
        {
            animationData.Clear();
            animationData.AddUniqueRange(anims);
            setAnimState(animationState);
            setAnimTime(animationPosition);
            updateUIState();
        }

        /// <summary>
        /// Should be called from the owning modules' Update() method, and should be called/updated per-frame.
        /// (TODO -- investigate if this can be moved to FixedUpdate, or other?)
        /// </summary>
        public virtual void updateAnimations()
        {
            if (animationState == AnimState.PLAYING_BACKWARD || animationState == AnimState.PLAYING_FORWARD)
            {
                bool playing = false;
                int len = animationData.Count;
                float time = animTime;
                for (int i = 0; i < len; i++)
                {
                    if (animationData[i].updateAnimation(out time))
                    {
                        playing = true;
                    }
                }
                if (animTime != time)
                {
                    animationPosition = time;
                }
                //if no longer playing, set the new animation state and inform the callback of the change
                if (!playing)
                {
                    AnimState newState = animationState == AnimState.PLAYING_BACKWARD ? AnimState.STOPPED_START : AnimState.STOPPED_END;
                    onAnimationStateChange(newState);
                }
            }
        }

        #endregion ENDREGION - Initialization and Updating

        /// <summary>
        /// Should be called if the animation state is ever updated from external processes.<para/>
        /// Updates the internal and visual animation states to the input state.
        /// </summary>
        /// <param name="newState"></param>
        public void setAnimState(AnimState newState)
        {
            switch (newState)
            {
                case AnimState.PLAYING_BACKWARD:
                    {
                        setAnimSpeed(-1f);
                        if (animationState == AnimState.STOPPED_END)//enforce play backwards from end
                        {
                            setAnimTime(1f);//no need to sample, the play update will take care of it
                        }
                        playAnimation();
                        break;
                    }
                case AnimState.PLAYING_FORWARD:
                    {
                        setAnimSpeed(1f);
                        if (animationState == AnimState.STOPPED_START)//enforce play forwards from beginning
                        {
                            setAnimTime(0f);//no need to sample, the play update will take care of it
                        }
                        playAnimation();
                        break;
                    }
                case AnimState.STOPPED_END:
                    {
                        setAnimTime(1, true);
                        stopAnimation();
                        break;
                    }
                case AnimState.STOPPED_START:
                    {
                        setAnimTime(0, true);
                        stopAnimation();
                        break;
                    }
            }
            onAnimationStateChange(newState);
            updateUIState();
        }

        /// <summary>
        /// Internal UI state update method.  Should be called anytime that animation state changes or animations initialized/added.
        /// </summary>
        private void updateUIState()
        {
            bool moduleEnabled = animationData.Count > 0;
            bool deployEnabled = moduleEnabled && (animationState == AnimState.STOPPED_START || animationState == AnimState.PLAYING_BACKWARD);
            bool retractEnabled = moduleEnabled && (animationState == AnimState.STOPPED_END || animationState == AnimState.PLAYING_FORWARD);
            bool deployLimitEnabled = moduleEnabled;//TODO -- add in control for if deploy limit is available for specific animations/etc
            deployEvent.guiActive = deployEvent.guiActiveEditor = deployEnabled;
            retractEvent.guiActive = retractEvent.guiActiveEditor = retractEnabled;
            if (deployLimitField != null)
            {
                deployLimitField.guiActive = deployLimitEnabled;
                deployLimitField.guiActiveEditor = deployLimitEnabled;
            }
            deployEvent.guiActiveUncommand = usableUncommanded;
            deployEvent.guiActiveUnfocused = usableUnfocused;
            deployEvent.externalToEVAOnly = usableEVA;
            retractEvent.guiActiveUncommand = usableUncommanded;
            retractEvent.guiActiveUnfocused = usableUnfocused;
            retractEvent.externalToEVAOnly = usableEVA;
        }

        /// <summary>
        /// Internal method to update the persistent data state(s) from the current animation state.  May be overriden for additional functionality.
        /// </summary>
        /// <param name="newState"></param>
        protected virtual void onAnimationStateChange(AnimState newState)
        {
            MonoBehaviour.print("Anim state changed to: " + newState);
            animationState = newState;
            persistentData = newState.ToString();
        }

        /// <summary>
        /// Internal method to activate the animation.  Sets internal AnimationClip to 'playing' state.
        /// </summary>
        protected void playAnimation()
        {
            int len = animationData.Count;
            for (int i = 0; i < len; i++)
            {
                animationData[i].playAnimation();
            }
        }

        /// <summary>
        /// Internal method to deactivate the animation.  Sets internal AnimationClip to 'stopped' state.
        /// </summary>
        protected void stopAnimation()
        {
            int len = animationData.Count;
            for (int i = 0; i < len; i++)
            {
                animationData[i].stopAnimation();
            }
        }

        /// <summary>
        /// Updates the animations internal 'time' value.<para/>
        /// Optionally updates the transforms to the current time value if 'sample'==true
        /// </summary>
        /// <param name="time"></param>
        /// <param name="sample"></param>
        protected void setAnimTime(float time, bool sample = false)
        {
            int len = animationData.Count;
            for (int i = 0; i < len; i++)
            {
                animationData[i].setAnimTime(time, sample);
            }
        }

        /// <summary>
        /// Sets the animations internal 'speed' value.<para/>
        /// This is a multiplier that is applied to whatever duration the animation was compiled for.
        /// </summary>
        /// <param name="speed"></param>
        protected void setAnimSpeed(float speed)
        {
            int len = animationData.Count;
            for (int i = 0; i < len; i++)
            {
                animationData[i].setAnimSpeed(speed);
            }
        }

    }

}
