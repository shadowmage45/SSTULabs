using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUDeployableEngine : PartModule
    {

        [KSPField]
        public int animationID = -1;

        [KSPField]
        public string engineID = "Engine";

        private SSTUAnimateControlled animationControl;
        private ModuleEnginesFX engineModule;
        
        [KSPAction("Activate Engine")]
        public void deployEngineAction(KSPActionParam param)
        {
            deployEngineEvent();
        }

        [KSPAction("Shutdown Engine")]
        public void retractEngineAction(KSPActionParam param)
        {
            retractEngineEvent();
        }

        [KSPEvent(name = "deployEngineEvent", guiName = "Activate Engine", guiActive = true, guiActiveEditor = false)]
        public void deployEngineEvent()
        {
            AnimState state = animationControl.getAnimationState();
            if (state != AnimState.STOPPED_END)
            {
                setAnimationState(AnimState.PLAYING_FORWARD);
            }
            else
            {
                engineModule.Activate();
            }
            setupGuiFields(animationControl.getAnimationState(), engineModule.EngineIgnited);
        }

        [KSPEvent(name = "retractEngineEvent", guiName = "Shutdown Engine", guiActive = true, guiActiveEditor = false)]
        public void retractEngineEvent()
        {

            AnimState state = animationControl.getAnimationState();
            if (state != AnimState.STOPPED_START)
            {
                setAnimationState(AnimState.PLAYING_BACKWARD);
                engineModule.Shutdown();
            }
            else
            {
                engineModule.Shutdown();
            }
            setupGuiFields(animationControl.getAnimationState(), engineModule.EngineIgnited);
        }

        public void Start()
        {

            animationControl = SSTUAnimateControlled.locateAnimationController(part, animationID, onAnimationStatusChanged);
            engineModule = null;
            ModuleEnginesFX[] engines = part.GetComponents<ModuleEnginesFX>();
            int len = engines.Length;
            for (int i = 0; i < len; i++)
            {
                if (engines[i].engineID == engineID)
                {
                    engineModule = engines[i];
                }
            }
            if (engineModule == null)
            {
                MonoBehaviour.print("ERROR: Could not locate engine by ID: " + engineID + " for part: " + part + " for SSTUAnimateEngineHeat.  This will cause errors during gameplay.  Setting engine to first engine module (if present)");
                if (engines.Length > 0) { engineModule = engines[0]; }
            }
            setupEngineModuleGui();
            setupGuiFields(animationControl.getAnimationState(), engineModule.EngineIgnited);
        }

        public void onAnimationStatusChanged(AnimState state)
        {
            if (state == AnimState.STOPPED_END && HighLogic.LoadedSceneIsFlight)
            {
                engineModule.Activate();
            }
        }

        //check for control enabled and deployment status (if animated)
        public override void OnActive()
        {
            if (animationControl.getAnimationState() == AnimState.STOPPED_END)
            {
                engineModule.Activate();
            }
            else
            {
                setAnimationState(AnimState.PLAYING_FORWARD);
                if (engineModule.EngineIgnited)
                {
                    engineModule.Shutdown();
                }
            }
            setupGuiFields(animationControl.getAnimationState(), engineModule.EngineIgnited);
        }

        private void setAnimationState(AnimState state)
        {
            AnimState currentState = animationControl.getAnimationState();
            //exceptions below fix issues of OnActive being called by moduleEngine during startup
            if (currentState == AnimState.STOPPED_END && state == AnimState.PLAYING_FORWARD) { return; }//don't allow deploying from deployed
            else if (currentState == AnimState.STOPPED_START && state == AnimState.PLAYING_BACKWARD) { return; }//don't allow retracting from retracted
            animationControl.setToState(state);
        }

        private void setupEngineModuleGui()
        {
            engineModule.Events["Activate"].active = false;
            engineModule.Events["Shutdown"].active = false;
            engineModule.Events["Activate"].guiActive = false;
            engineModule.Events["Shutdown"].guiActive = false;
            engineModule.Actions["ActivateAction"].active = false;
            engineModule.Actions["ShutdownAction"].active = false;
            engineModule.Actions["OnAction"].active = false;
        }
        
        //TODO - stuff for non-restartable and non-stoppable engines
        private void setupGuiFields(AnimState state, bool engineActive)
        {
            bool isEditor = HighLogic.LoadedSceneIsEditor;
            switch (state)
            {
                case AnimState.PLAYING_BACKWARD://retracting
                    {
                        Events["deployEngineEvent"].active = true;
                        Events["retractEngineEvent"].active = false;
                        break;
                    }

                case AnimState.PLAYING_FORWARD://deploying
                    {
                        Events["deployEngineEvent"].active = false;
                        Events["retractEngineEvent"].active = true;
                        break;
                    }

                case AnimState.STOPPED_END://deployed or no anim
                    {
                        Events["deployEngineEvent"].active = false;
                        Events["retractEngineEvent"].active = true;
                        break;
                    }

                case AnimState.STOPPED_START://retracted
                    {
                        Events["deployEngineEvent"].active = true;
                        Events["retractEngineEvent"].active = false;
                        break;
                    }

            }
        }

    }
}
