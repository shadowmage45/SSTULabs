using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSTUTools
{

    /// <summary>
    /// Wrapper for animation handling and UI setup.  Should include all functions needed to support save, load, and UI interaction.
    /// Intended to wrap a single 'set' of animations that will respond to a single deploy/retract button/action-group.
    /// </summary>
    public class AnimationModule<T> where T : PartModule
    {

        public readonly Part part;
        public readonly T module;

        public readonly BaseField persistentDataField;
        public readonly BaseField deployLimitField;
        public readonly BaseEvent deployEvent;
        public readonly BaseEvent retractEvent;
        
        private AnimState animationState = AnimState.STOPPED_START;
        private List<SSTUAnimData> animationData = new List<SSTUAnimData>();
        private float animationPosition = 0f;

        private bool usableUnfocused;
        private bool usableEVA;
        private bool usableUncommanded;

        public float deployLimit
        {
            get { return deployLimitField == null ? 1.0f : deployLimitField.GetValue<float>(deployLimitField); }
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
            get { return persistentDataField.GetValue<string>(persistentDataField); }
            set { persistentDataField.SetValue(value, persistentDataField); }
        }
        
        public AnimationModule(Part part, T module, BaseField persistence, BaseField deployLimit, BaseEvent deploy, BaseEvent retract)
        {
            this.part = part;
            this.module = module;
            this.persistentDataField = persistence;
            this.deployLimitField = deployLimit;
            loadAnimationState(persistentData);
            if (deployLimitField != null)
            {
                deployLimitField.uiControlEditor.onFieldChanged = onDeployLimitUpdated;
                deployLimitField.uiControlFlight.onFieldChanged = onDeployLimitUpdated;
            }            
        }

        public void setUsableFlags(bool unfocused, bool eva, bool uncommanded)
        {
            usableUnfocused = unfocused;
            usableEVA = eva;
            usableUncommanded = uncommanded;
        }

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

        private void updatePersistentData()
        {
            persistentData = animationState.ToString();
        }

        #region REGION - UI INTERACTION

        private void onDeployLimitUpdated(BaseField a, System.Object b)
        {
            int len = animationData.Count;
            bool shouldStop = false;
            for (int i = 0; i < len; i++)
            {
                if (animationData[i].setMaxTime(deployLimit, animationState))
                {
                    shouldStop = true;
                }
            }
            if (shouldStop)
            {
                stopAnimation();
                animationState = AnimState.STOPPED_END;
            }
        }

        public void onDeployEvent()
        {
            if (animationState == AnimState.STOPPED_START || animationState == AnimState.PLAYING_BACKWARD)
            {
                setAnimState(AnimState.PLAYING_FORWARD);
            }
        }

        public void onRetractEvent()
        {
            if (animationState == AnimState.STOPPED_END || animationState == AnimState.PLAYING_FORWARD)
            {
                setAnimState(AnimState.PLAYING_FORWARD);
            }
        }

        public void onDeployAction(KSPActionParam param)
        {
            onDeployEvent();
        }

        public void onRetractAction(KSPActionParam paran)
        {
            onRetractEvent();
        }

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
        public void updateAnimations()
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
            animationState = newState;
            persistentData = newState.ToString();
        }

        protected void playAnimation()
        {
            int len = animationData.Count;
            for (int i = 0; i < len; i++)
            {
                animationData[i].playAnimation();
            }
        }

        protected void stopAnimation()
        {
            int len = animationData.Count;
            for (int i = 0; i < len; i++)
            {
                animationData[i].stopAnimation();
            }
        }

        protected void setAnimTime(float time, bool sample = false)
        {
            int len = animationData.Count;
            for (int i = 0; i < len; i++)
            {
                animationData[i].setAnimTime(time, sample);
            }
        }

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
