using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class AnimationController
    {

        private AnimState currentAnimState = AnimState.STOPPED_START;
        private Action<AnimState> stateChangeCallback;
        private List<SSTUAnimData> animationData = new List<SSTUAnimData>();

        private float animTime = 0f;
        private float maxTime = 1f;

        public AnimationController(float time, float maxTime)
        {
            this.animTime = time;
            this.maxTime = maxTime;
        }

        public AnimState animationState
        {
            get { return currentAnimState; }
        }

        public void setStateChangeCallback(Action<AnimState> cb) { stateChangeCallback = cb; }

        public void addAnimationData(SSTUAnimData data)
        {
            animationData.Add(data);
        }

        public void addAnimationData(IEnumerable<SSTUAnimData> data)
        {
            animationData.Clear();
            animationData.AddRange(data);
        }

        public void clearAnimationData() { animationData.Clear(); }

        public void updateAnimationState()
        {
            if (currentAnimState == AnimState.PLAYING_BACKWARD || currentAnimState == AnimState.PLAYING_FORWARD)
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
                    animTime = time;
                }
                //if no longer playing, set the new animation state and inform the callback of the change
                if (!playing)
                {
                    AnimState newState = currentAnimState == AnimState.PLAYING_BACKWARD ? AnimState.STOPPED_START : AnimState.STOPPED_END;
                    setAnimState(newState, true);
                }
            }
        }

        public void setAnimState(String state)
        {
            AnimState eState = (AnimState)Enum.Parse(typeof(AnimState), state);
            setAnimState(eState, false);
        }
        
        public void setAnimState(AnimState newState, bool callback)
        {
            switch (newState)
            {
                case AnimState.PLAYING_BACKWARD:
                    {
                        setAnimSpeed(-1f);
                        if (currentAnimState == AnimState.STOPPED_END)//enforce play backwards from end
                        {
                            setAnimTime(1f);//no need to sample, the play update will take care of it
                        }
                        playAnimation();
                        break;
                    }
                case AnimState.PLAYING_FORWARD:
                    {
                        setAnimSpeed(1f);
                        if (currentAnimState == AnimState.STOPPED_START)//enforce play forwards from beginning
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
            currentAnimState = newState;
            if (callback && stateChangeCallback != null)
            {
                stateChangeCallback.Invoke(currentAnimState);
            }
        }

        public void restorePreviousAnimationState(AnimState state, float maxTime)
        {
            this.maxTime = maxTime;
            if (state == AnimState.PLAYING_BACKWARD)
            {
                state = AnimState.STOPPED_START;
                animTime = 0f;
            }
            else if (state == AnimState.PLAYING_FORWARD)
            {
                state = AnimState.STOPPED_END;
                animTime = maxTime;
            }
            else if (state == AnimState.STOPPED_END)
            {
                animTime = maxTime;
            }
            else if (state == AnimState.STOPPED_START)
            {
                animTime = 0f;
            }
            setAnimState(state, false);
        }

        public void setMaxTime(float time)
        {
            maxTime = time;
            int len = animationData.Count;
            bool shouldStop = false;
            for (int i = 0; i < len; i++)
            {
                if (animationData[i].setMaxTime(maxTime, currentAnimState))
                {
                    shouldStop = true;
                }
            }
            if (shouldStop)
            {
                
                stopAnimation();
                currentAnimState = AnimState.STOPPED_END;
                if (stateChangeCallback != null)
                {
                    stateChangeCallback.Invoke(AnimState.STOPPED_END);
                }
            }
        }

        public void setCurrentTime(float time, bool sample = true)
        {
            int len = animationData.Count;
            for (int i = 0; i < len; i++)
            {
                animationData[i].setAnimTime(time, true);
            }
        }

        public float getCurrentTime()
        {
            return animTime;
        }

        private void playAnimation()
        {
            int len = animationData.Count;
            for (int i = 0; i < len; i++)
            {
                animationData[i].playAnimation();
            }
        }

        private void stopAnimation()
        {
            int len = animationData.Count;
            for (int i = 0; i < len; i++)
            {
                animationData[i].stopAnimation();
            }
        }

        private void setAnimTime(float time, bool sample = false)
        {
            int len = animationData.Count;
            for (int i = 0; i < len; i++)
            {
                animationData[i].setAnimTime(time, sample);
            }
        }

        private void setAnimSpeed(float speed)
        {
            int len = animationData.Count;
            for (int i = 0; i < len; i++)
            {
                animationData[i].setAnimSpeed(speed);
            }
        }

    }

    /// <summary>
    /// Wrapper class for the data to manage a single animation, including animation speed and layers, and min/max time handling (default to 0-1 normalized time)
    /// </summary>
    public class SSTUAnimData
    {
        private Animation[] animations;
        private String animationName;
        private float animationSpeed = 1;
        private int animationLayer = 1;
        public float maxDeployTime = 1f;

        public SSTUAnimData(ConfigNode node, Transform transform)
        {
            animationName = node.GetStringValue("name");
            animationSpeed = node.GetFloatValue("speed", animationSpeed);
            animationLayer = node.GetIntValue("layer", animationLayer);
            maxDeployTime = node.GetFloatValue("max", maxDeployTime);
            setupController(animationName, animationSpeed, animationLayer, transform);
        }

        public SSTUAnimData(String name, float speed, int layer, Transform transform)
        {
            animationName = name;
            animationSpeed = speed;
            animationLayer = layer;
            setupController(name, speed, layer, transform);
        }

        private void setupController(String name, float speed, int layer, Transform transform)
        {
            Animation[] allAnimations = transform.gameObject.GetComponentsInChildren<Animation>(true);
            List<Animation> animList = new List<Animation>();
            Animation anim;
            int len = allAnimations.Length;
            for (int i = 0; i < len; i++)
            {
                anim = allAnimations[i];
                AnimationClip c = anim.GetClip(animationName);
                if (c != null)
                {
                    animList.Add(anim);
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
