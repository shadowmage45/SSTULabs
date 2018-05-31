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
    public class AnimationModule
    {
        public delegate AnimationModule SymmetryModule(PartModule module);

        /// <summary>
        /// The part that this container class belongs to
        /// </summary>
        public readonly Part part;

        /// <summary>
        /// The direct owning part-module for this container class
        /// </summary>
        public readonly PartModule module;

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
        /// Delegate for retrieval of the symmetry counterpart module(s) from an input PartModule
        /// </summary>
        public SymmetryModule getSymmetryModule;

        public Action<AnimState> onAnimStateChangeCallback;

        /// <summary>
        /// If true the parts default drag cube will be updated on animation state changes
        /// </summary>
        public bool updateDragCube = true;

        /// <summary>
        /// Internal cache of the current animation state as an Enum
        /// </summary>
        private AnimState animationState = AnimState.STOPPED_START;

        /// <summary>
        /// Reference to the animation data container from the ModelDefinition.  Stores info on UI labels and field availability.
        /// </summary>
        private AnimationData modelAnimationData;

        /// <summary>
        /// Internal cache of the current list of animation data blocks.
        /// </summary>
        private List<ModelAnimationDataControl> animationData = new List<ModelAnimationDataControl>();

        /// <summary>
        /// Internal cache of the current animation position.
        /// </summary>
        private float animationPosition = 0f;

        /// <summary>
        /// Cache of info for display through GetInfo()
        /// </summary>
        private string moduleInfo = string.Empty;

        public float deployLimit
        {
            get { return deployLimitField == null ? 1.0f : deployLimitField.GetValue<float>(module); }
        }

        public float animTime
        {
            get
            {
                return animationPosition;
            }
            set
            {
                animationPosition = value;
                setAnimTime(value, true);
            }
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

        public bool enabled
        {
            get { return animationData.Count > 0; }
        }
        
        public AnimationModule(Part part, PartModule module, string persistence, string deployLimit, string deploy, string retract)
        {
            this.part = part;
            this.module = module;
            this.persistentDataField = string.IsNullOrEmpty(persistence) ? null : module.Fields[persistence];
            this.deployLimitField = string.IsNullOrEmpty(deployLimit) ? null : module.Fields[deployLimit];
            if (deployLimitField != null)
            {
                deployLimitField.uiControlEditor.onFieldChanged = onDeployLimitUpdated;
                deployLimitField.uiControlFlight.onFieldChanged = onDeployLimitUpdated;
            }
            this.deployEvent = string.IsNullOrEmpty(deploy)? null : module.Events[deploy];
            this.retractEvent = string.IsNullOrEmpty(retract)? null : module.Events[retract];
            if (persistentDataField != null)
            {
                loadAnimationState(persistentData);
            }
            //initialized with an empty/dummy animation data holder
            modelAnimationData = new AnimationData();
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
            this.actionWithSymmetry(m =>
            {
                int len = m.animationData.Count;
                bool shouldStop = false;
                for (int i = 0; i < len; i++)
                {
                    shouldStop = shouldStop || m.animationData[i].setMaxTime(deployLimit, animationState);
                }
                if (shouldStop)
                {
                    m.stopAnimation();
                    m.setAnimState(AnimState.STOPPED_END);
                }
            });
        }

        /// <summary>
        /// Should be called directly from the PartModule when the KSPEvent for deploy is called.
        /// </summary>
        public void onDeployEvent()
        {
            if (animationState == AnimState.STOPPED_START || animationState == AnimState.PLAYING_BACKWARD)
            {
                this.actionWithSymmetry(m =>
                {
                    m.setAnimState(AnimState.PLAYING_FORWARD);
                    m.updateUIState();
                });
            }
        }

        /// <summary>
        /// Should be called directly from the PartModule when the KSPEvent for retract is called.
        /// </summary>
        public void onRetractEvent()
        {
            if (animationState == AnimState.STOPPED_END || animationState == AnimState.PLAYING_FORWARD)
            {
                this.actionWithSymmetry(m =>
                {
                    m.setAnimState(AnimState.PLAYING_BACKWARD);
                    m.updateUIState();
                });
            }
        }

        /// <summary>
        /// Should be called directly from the PartModule when the KSPAction for toggle is activated.
        /// </summary>
        public void onToggleAction(KSPActionParam param)
        {
            if (animationState == AnimState.STOPPED_START || animationState == AnimState.PLAYING_BACKWARD)
            {
                setAnimState(AnimState.PLAYING_FORWARD);
                updateUIState();
            }
            else
            {
                setAnimState(AnimState.PLAYING_BACKWARD);
                updateUIState();
            }
        }

        public string getModuleInfo()
        {
            return moduleInfo;
        }

        #endregion ENDREGION - UI INTERACTION

        #region REGION - Initialization and Updating

        /// <summary>
        /// Should be called after constructor to initialize the internal animation data.<para/>
        /// Should also be called anytime the animation data needs changed/updated from parent module/models.
        /// </summary>
        /// <param name="anims"></param>
        public void setupAnimations(AnimationData anims, Transform root, int startLayer)
        {
            if (anims == null)
            {
                disableAnimations();
                MonoBehaviour.print("AnimationData was null -- Disabling animations!");
                return;
            }
            modelAnimationData = anims;
            animationData.Clear();
            if (anims != null)
            {
                animationData.AddUniqueRange(anims.getAnimationData(root, startLayer));
            }
            setAnimState(animationData.Count <= 0 ? AnimState.STOPPED_END : animationState);
            setAnimTime(animationPosition);
            updateUIState();
            updateModuleInfo();
        }

        /// <summary>
        /// Disable the animation module -- hides UI fields and clears module display information.  Clears any cache of animation data.
        /// </summary>
        public void disableAnimations()
        {
            modelAnimationData = new AnimationData();
            animationData.Clear();
            updateUIState();
            updateModuleInfo();
        }

        /// <summary>
        /// Should be called from the owning modules' Update() method, and should be called/updated per-frame.
        /// (TODO -- investigate if this can be moved to FixedUpdate, or other?)
        /// </summary>
        public void Update()
        {
            if (animationState == AnimState.PLAYING_BACKWARD || animationState == AnimState.PLAYING_FORWARD)
            {
                bool playing = false;
                int len = animationData.Count;
                float time = animationPosition;
                for (int i = 0; i < len; i++)
                {
                    if (animationData[i].updateAnimation(out time))
                    {
                        playing = true;
                    }
                }
                animationPosition = time;
                //if no longer playing, set the new animation state and inform the callback of the change
                if (!playing)
                {
                    AnimState newState = animationState == AnimState.PLAYING_BACKWARD ? AnimState.STOPPED_START : AnimState.STOPPED_END;
                    onAnimationStateChange(newState, true);
                }
            }
        }

        public void updateModuleInfo()
        {
            moduleInfo = "Animation\n";
            moduleInfo += modelAnimationData.deployLabel + "\n";
            if (modelAnimationData.oneShot)
            {
                moduleInfo += "Single Use Only";
            }
            if (modelAnimationData.looping)
            {
                moduleInfo += "Looping";
            }
        }

        #endregion ENDREGION - Initialization and Updating

        /// <summary>
        /// Should be called if the animation state is ever updated from external processes.<para/>
        /// Updates the internal and visual animation states to the input state.
        /// </summary>
        /// <param name="newState"></param>
        public void setAnimState(AnimState newState, bool updateCallback = false)
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
            onAnimationStateChange(newState, updateCallback);
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
            bool deployLimitEnabled = moduleEnabled && modelAnimationData.deployLimitActive;

            if (deployEvent != null)
            {
                deployEvent.guiActive = deployEnabled && modelAnimationData.activeFlight;
                deployEvent.guiActiveEditor = deployEnabled && modelAnimationData.activeEditor;
                deployEvent.guiActiveUncommand = modelAnimationData.activeUncommanded;
                deployEvent.guiActiveUnfocused = modelAnimationData.activeUnfocused;
                deployEvent.externalToEVAOnly = modelAnimationData.activeEVAOnly;
                deployEvent.guiName = modelAnimationData.deployLabel;
            }

            if (retractEvent != null)
            {
                retractEvent.guiActive = retractEnabled && modelAnimationData.activeFlight;
                retractEvent.guiActiveEditor = retractEnabled && modelAnimationData.activeEditor;
                retractEvent.guiActiveUncommand = modelAnimationData.activeUncommanded;
                retractEvent.guiActiveUnfocused = modelAnimationData.activeUnfocused;
                retractEvent.externalToEVAOnly = modelAnimationData.activeEVAOnly;
                retractEvent.guiName = modelAnimationData.retractLabel;
            }
            
            if (deployLimitField != null)
            {
                deployLimitField.guiActive = deployLimitEnabled;
                deployLimitField.guiActiveEditor = deployLimitEnabled;
            }
        }

        /// <summary>
        /// Internal method to update the persistent data state(s) from the current animation state.
        /// </summary>
        /// <param name="newState"></param>
        protected void onAnimationStateChange(AnimState newState, bool updateExternal = false)
        {
            animationState = newState;
            persistentData = newState.ToString();
            if (updateDragCube)
            {
                SSTUStockInterop.addDragUpdatePart(part);
            }
            if (updateExternal && onAnimStateChangeCallback!=null)
            {
                onAnimStateChangeCallback(newState);
            }
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
        internal void setAnimTime(float time, bool sample = false)
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
        
        /// <summary>
        /// Internal method to update AnimationModules for symmetry Part-PartModules
        /// </summary>
        /// <param name="action"></param>
        private void actionWithSymmetry(Action<AnimationModule> action)
        {
            action(this);
            int index = part.Modules.IndexOf(module);
            foreach (Part p in part.symmetryCounterparts)
            {
                action(getSymmetryModule(p.Modules[index]));
            }
        }

    }

    /// <summary>
    /// Container class for managing all of the AnimationData corresponding to a single animation control.<para/>
    /// Used by ModelDefinition (which only supports a single animation control set), and AnimateControlled<para/>
    /// Wraps one or more Animation components.  May control only -some- of the animations on a part.<para/>
    /// Includes UI label, deploy limit, and flight/editor/unfocused/uncommanded/eva active control specifications.
    /// </summary>
    public class AnimationData
    {
        public readonly string deployLabel;
        public readonly string retractLabel;
        public readonly string toggleLabel;
        public readonly bool deployLimitActive;
        public readonly bool activeEditor;
        public readonly bool activeFlight;
        public readonly bool activeUnfocused;
        public readonly bool activeUncommanded;
        public readonly bool activeEVAOnly;
        public readonly float unfocusedRange;
        public readonly bool oneShot;
        public readonly bool looping;

        private ModelAnimationData[] mads;

        public AnimationData(ConfigNode node)
        {
            deployLabel = node.GetStringValue("deployLabel", "Deploy");
            retractLabel = node.GetStringValue("retractLabel", "Retract");
            toggleLabel = node.GetStringValue("toggleLabel", "Toggle");
            deployLimitActive = node.GetBoolValue("deployLimitActive", false);
            activeEditor = node.GetBoolValue("activeEditor", true);
            activeFlight = node.GetBoolValue("activeFlight", true);
            activeUnfocused = node.GetBoolValue("activeUnfocused", false);
            activeUncommanded = node.GetBoolValue("activeUncommanded", false);
            activeEVAOnly = node.GetBoolValue("activeEVAOnly", false);
            unfocusedRange = node.GetFloatValue("unfocusedRange", 4f);
            oneShot = node.GetBoolValue("oneShot", false);
            looping = node.GetBoolValue("looping", false);
            //the actual animation data for the model
            mads = ModelAnimationData.parseAnimationData(node.GetNodes("ANIMATION"));
        }

        /// <summary>
        /// Return a blank AnimationData instance, with no internal animation references.  Should be used when 'no' animation is setup for a model.
        /// </summary>
        public AnimationData()
        {
            deployLabel = "Deploy";
            retractLabel = "Retract";
            toggleLabel = "Toggle";
            deployLimitActive = false;
            activeEditor = false;
            activeFlight = false;
            activeUnfocused = false;
            activeUncommanded = false;
            activeEVAOnly = false;
            mads = new ModelAnimationData[0];
        }

        public ModelAnimationDataControl[] getAnimationData(Transform transform, int startLayer)
        {
            int len = mads.Length;
            ModelAnimationDataControl[] data = new ModelAnimationDataControl[len];
            for (int i = 0; i < len; i++, startLayer++)
            {
                data[i] = new ModelAnimationDataControl(mads[i].animationName, mads[i].speed, startLayer, transform, mads[i].isLoop);
            }
            return data;
        }

    }

    /// <summary>
    /// Stores the data defining a single animation
    /// Should include animation name, default speed
    /// </summary>
    public class ModelAnimationData
    {

        /// <summary>
        /// The name of the AnimationClip.
        /// </summary>
        public readonly string animationName;

        /// <summary>
        /// A multiplier applied to the speed of the animation as it was compiled.
        /// </summary>
        public readonly float speed;

        /// <summary>
        /// Is this animation of type LOOP?  Used in compound animation setups to determine what is the 'intro' and what are the 'loop' animation types.
        /// </summary>
        public readonly bool isLoop;

        /// <summary>
        /// Used to increment the base animation layer that is passed into the ModelModule animation handling, in the case of a single model-definition requiring multiple layers.
        /// This must also be accounted for in the PartModule setup itself, or the layers might overrun into values used by other model slots.
        /// </summary>
        public readonly int layerOffset;//offset applied to the 'base' layer

        public ModelAnimationData(ConfigNode node)
        {
            animationName = node.GetStringValue("name");
            speed = node.GetFloatValue("speed", 1f);
            isLoop = node.GetBoolValue("loop", false);
        }

        public static ModelAnimationData[] parseAnimationData(ConfigNode[] nodes)
        {
            int len = nodes.Length;
            ModelAnimationData[] data = new ModelAnimationData[len];
            for (int i = 0; i < len; i++)
            {
                data[i] = new ModelAnimationData(nodes[i]);
            }
            return data;
        }
    }

    /// <summary>
    /// Runtime wrapper class for the data to manage a single animation, including animation speed and layers, and min/max time handling (default to 0-1 normalized time)<para/>
    /// This is the class that would be used internally to manage the start/stopped state of an animation.  Directly interfaces with the Unity Animation class.
    /// </summary>
    public class ModelAnimationDataControl
    {
        private Animation[] animations;
        public readonly String animationName;
        private float animationSpeed = 1;
        private int animationLayer = 1;
        public float maxDeployTime = 1f;
        public bool isLoop;//TODO -- support looping animation

        public ModelAnimationDataControl(ConfigNode node, Transform transform)
        {
            animationName = node.GetStringValue("name");
            animationSpeed = node.GetFloatValue("speed", animationSpeed);
            animationLayer = node.GetIntValue("layer", animationLayer);
            maxDeployTime = node.GetFloatValue("max", maxDeployTime);
            setupController(animationName, animationSpeed, animationLayer, transform);
        }

        public ModelAnimationDataControl(String name, float speed, int layer, Transform transform, bool loop)
        {
            animationName = name;
            animationSpeed = speed;
            animationLayer = layer;
            isLoop = loop;
            setupController(name, speed, layer, transform);
        }

        private void setupController(String name, float speed, int layer, Transform transform)
        {
            int len;
            Animation anim;
            List<Animation> animList = new List<Animation>();
            if (!string.IsNullOrEmpty(animationName) && "none" != animationName)
            {
                Animation[] allAnimations = transform.gameObject.GetComponentsInChildren<Animation>(true);
                len = allAnimations.Length;
                for (int i = 0; i < len; i++)
                {
                    anim = allAnimations[i];
                    AnimationClip c = anim.GetClip(animationName);
                    if (c != null)
                    {
                        animList.Add(anim);
                    }
                }
            }
            animations = animList.ToArray();
            if (animations == null || animations.Length == 0)
            {
                MonoBehaviour.print("ERROR: No animations found for animation name: " + animationName);
                return;
            }
            len = animations.Length;
            for (int i = 0; i < len; i++)
            {
                anim = animations[i];
                anim[animationName].layer = animationLayer;
                anim[animationName].wrapMode = WrapMode.Once;
                anim.wrapMode = WrapMode.Once;
            }
        }

        public bool updateAnimation(out float time)
        {
            time = 0f;
            bool playing = false;
            bool earlyStop = false;
            int len = animations.Length;
            Animation anim;
            AnimationState clip;
            for (int i = 0; i < len; i++)
            {
                anim = animations[i];
                clip = anim[animationName];
                if (clip.enabled)
                {
                    if (clip.normalizedTime > maxDeployTime)
                    {
                        earlyStop = true;
                        clip.normalizedTime = maxDeployTime;
                        clip.speed = 0;
                        clip.enabled = false;//force-stop, sample will not disturb this
                    }
                    else
                    {
                        playing = true;
                    }
                }
                time = clip.normalizedTime;
            }
            if (earlyStop && !playing)
            {
                sample(maxDeployTime);//force update to position
            }
            return playing;
        }

        public void playAnimation()
        {
            int len = animations.Length;
            for (int i = 0; i < len; i++)
            {
                animations[i][animationName].enabled = true;
                animations[i].Play(animationName);
            }
        }

        public void stopAnimation()
        {
            int len = animations.Length;
            for (int i = 0; i < len; i++)
            {
                animations[i][animationName].speed = 0f;
                animations[i].Stop(animationName);
            }
        }

        public void setAnimTime(float time, bool sample = false)
        {
            if (time > maxDeployTime)
            {
                time = maxDeployTime;
            }
            int len = animations.Length;
            for (int i = 0; i < len; i++)
            {
                animations[i][animationName].normalizedTime = time;
            }
            if (sample) { this.sample(time); }
        }

        public void setAnimSpeed(float speed)
        {
            int len = animations.Length;
            for (int i = 0; i < len; i++)
            {
                animations[i][animationName].speed = speed * animationSpeed;
            }
        }

        public void setAnimLayer(int layer)
        {
            int len = animations.Length;
            for (int i = 0; i < len; i++)
            {
                animations[i][animationName].layer = layer;
            }
        }

        public void sample(float time)
        {
            int len = animations.Length;
            for (int i = 0; i < len; i++)
            {
                sample(animations[i], animations[i][animationName], time);
            }
        }

        private void sample(Animation anim, AnimationState clip, float time)
        {
            bool en = clip.enabled;
            clip.enabled = true;
            clip.normalizedTime = time;
            clip.weight = 1;
            anim.Sample();
            clip.enabled = en;//restore previous enabled state...
        }

        public bool setMaxTime(float max, AnimState state)
        {
            maxDeployTime = Mathf.Clamp01(max);
            int len = animations.Length;
            bool shouldStop = false;
            for (int i = 0; i < len; i++)
            {
                if (state == AnimState.STOPPED_END)
                {
                    sample(animations[i], animations[i][animationName], maxDeployTime);
                    //no change in state
                }
                else if (state == AnimState.PLAYING_FORWARD)
                {
                    float nt = animations[i][animationName].normalizedTime;
                    if (nt >= maxDeployTime)
                    {
                        animations[i][animationName].normalizedTime = maxDeployTime;
                        animations[i][animationName].speed = 0f;
                        shouldStop = true;
                    }
                }
                else if (state == AnimState.PLAYING_BACKWARD)
                {
                    float nt = animations[i][animationName].normalizedTime;
                    if (nt > maxDeployTime)
                    {
                        animations[i][animationName].normalizedTime = maxDeployTime;
                    }
                    //no change in state
                }
                else if (state == AnimState.STOPPED_START)
                {
                    //NOOP
                }
            }
            if (shouldStop) { sample(maxDeployTime); }
            return shouldStop;
        }

        public override string ToString()
        {
            return "AnimData: " + animationName + " : " + animationLayer + " : " + animationSpeed + " : " + maxDeployTime;
        }

    }


}
