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
        //lookup value for external modules to find the proper animation module when multiple anim modules are in use
        [KSPField]
        public string animationID = "animation";

        [KSPField]
        public float animationSpeed = 1;

        [KSPField]
        public int animationLayer = 1;

        [KSPField]
        public String animationName;

        [KSPField(isPersistant = true)]
        public String persistentState = AnimState.STOPPED_START.ToString();

        [KSPField]
        public bool externalInit = false;

        [Persistent]
        public string configNodeData = string.Empty;
        
        private AnimationController controller;

        private List<Action<AnimState>> callbacks = new List<Action<AnimState>>();
        
        //Static method for use by other modules to locate a control module; reduces code duplication in animation controlling modules
        public static SSTUAnimateControlled locateAnimationController(Part part, string id, Action<AnimState> callback = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            SSTUAnimateControlled[] potentialAnimators = part.GetComponents<SSTUAnimateControlled>();
            foreach (SSTUAnimateControlled ac in potentialAnimators)
            {
                if (ac.animationID == id)
                {
                    if (callback != null)
                    {
                        ac.addCallback(callback);
                    }
                    return ac;
                }
            }
            return null;
        }

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
                throw new NotImplementedException();
            }
        }

        public bool CanMove
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public EventData<float, float> OnMoving
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public EventData<float> OnStop
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public void SetScalar(float t)
        {
            throw new NotImplementedException();
        }

        public void SetUIRead(bool state)
        {
            throw new NotImplementedException();
        }

        public void SetUIWrite(bool state)
        {
            throw new NotImplementedException();
        }

        public bool IsMoving()
        {
            //if (animationControl != null)
            //{
            //    return animationControl.
            //}
            //return false;
            throw new NotImplementedException();
        }

        #endregion ENDREGION - IScalarModule fields/methods

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (controller == null && !externalInit)
            {
                initialize();
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            if (controller != null) { persistentState = controller.animationState.ToString(); }
            node.SetValue("persistentState", persistentState, true);
        }

        //run in OnLoad for prefab parts
        //TODO explicitly only init on load for prefabs; all others init during OnStart...
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            if (!externalInit)
            {
                initialize();
            }
        }

        public void initializeExternal(SSTUAnimData[] animData)
        {
            if (controller != null)
            {
                controller.clearAnimationData();
                controller = null;
            }
            controller = new AnimationController();
            controller.addAnimationData(animData);
            AnimState prevState = (AnimState)Enum.Parse(typeof(AnimState), persistentState);
            controller.restorePreviousAnimationState(prevState);
            controller.setStateChangeCallback(onAnimationStateChange);
        }

        private void initialize()
        {
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            ConfigNode[] animNodes = node.GetNodes("ANIMATION");
            int len = animNodes.Length;
            ConfigNode[] allNodes=null;
            if (!String.IsNullOrEmpty(animationName))
            {
                allNodes = new ConfigNode[len + 1];
                for (int i = 0; i < len; i++) { allNodes[i] = animNodes[i]; }
                ConfigNode legacyNode = new ConfigNode("ANIMATION");
                legacyNode.AddValue("name", animationName);
                legacyNode.AddValue("speed", animationSpeed);
                legacyNode.AddValue("layer", animationLayer);
                allNodes[len] = legacyNode;
            }
            else
            {
                allNodes = animNodes;
            }

            controller = new AnimationController();
            SSTUAnimData animationData;
            len = allNodes.Length;
            for (int i = 0; i < len; i++)
            {
                animationData = new SSTUAnimData(allNodes[i], part.gameObject.transform.FindRecursive("model"));
                controller.addAnimationData(animationData);
            }
            AnimState prevState = (AnimState)Enum.Parse(typeof(AnimState), persistentState);
            controller.restorePreviousAnimationState(prevState);
            controller.setStateChangeCallback(onAnimationStateChange);
        }

        public void reInitialize()
        {
            if (controller != null)
            {
                controller.clearAnimationData();
                controller = null;
                initialize();
            }
        }

        public bool initialized() { return controller != null; }

        public void addCallback(Action<AnimState> cb)
        {
            callbacks.AddUnique(cb);
        }

        public void removeCallback(Action<AnimState> cb)
        {
            callbacks.Remove(cb);
        }

        //External method to set the state; does not callback on this state change, as this is supposed to originate -from- the callback;
        //it should be aware of its own instigated state changes
        // note - ERROR - this ignores cases of multiple registered callbacks, the module(s) not initiating the change will not be aware of it
        public void setToState(AnimState newState)
        {
            persistentState = newState.ToString();
            if (controller != null)
            {
                controller.setAnimState(newState, false);
            }
        }

        public AnimState getAnimationState()
        {
            return controller==null? AnimState.STOPPED_START : controller.animationState;
        }

        public void Update()
        {
            if (controller != null)
            {
                controller.updateAnimationState();
            }
        }
        
        private void onAnimationStateChange(AnimState newState)
        {
            int len = callbacks.Count;
            for (int i = 0; i < len; i++)
            {
                callbacks[i].Invoke(newState);
            }
            persistentState = newState.ToString();
        }

    }
}

