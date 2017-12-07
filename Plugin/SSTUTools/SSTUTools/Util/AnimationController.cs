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
}
