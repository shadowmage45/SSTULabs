using System;

namespace SSTUTools
{
    
    public class SSTUDeployableEngine : ModuleEnginesFX
    {

        [KSPField]
        public int animationID = -1;

        private SSTUAnimateControlled animationControl;

        [KSPAction("Toggle Engine Deployment")]
        public void toggleDeployment(KSPActionParam param)
        {
            if (animationControl != null)
            {
                AnimState state = animationControl.getAnimationState();
                if (state == AnimState.PLAYING_BACKWARD || state == AnimState.STOPPED_START)//either playing retract anim, or already retracted; deploy it
                {
                    deployEngineEvent();
                }
                else//else it was deployed/deploying; retract it
                {
                    retractEngineEvent();
                }
            }
        }

        [KSPEvent(name = "deployEngineEvent", guiName = "Deploy Engine", guiActive = true, guiActiveEditor = true)]
        public void deployEngineEvent()
        {
            setAnimationState(AnimState.PLAYING_FORWARD);
        }

        [KSPEvent(name = "retractEngineEvent", guiName = "Retract Engine", guiActive = true, guiActiveEditor = true)]
        public void retractEngineEvent()
        {
            Shutdown();
            setAnimationState(AnimState.PLAYING_BACKWARD);
        }
        
        public override void OnStart(PartModule.StartState state)
        {
            animationControl = SSTUAnimateControlled.locateAnimationController(part, animationID, onAnimationStatusChanged);
            base.OnStart(state);
            setupGuiFields(animationControl == null ? AnimState.STOPPED_END : animationControl.getAnimationState(), EngineIgnited);
        }

        //check for control enabled and deployment status (if animated)
        public override void OnActive()
        {
            if (animationControl == null || animationControl.getAnimationState() == AnimState.STOPPED_END)
            {
                base.OnActive();
            }
            else
            {
                setAnimationState(AnimState.PLAYING_FORWARD);
            }
        }
        
        new public void FixedUpdate()
        {
            base.FixedUpdate();
            setupGuiFields(animationControl == null ? AnimState.STOPPED_END : animationControl.getAnimationState(), EngineIgnited);
        }

        //TODO - stuff for non-restartable and non-stoppable engines
        private void setupGuiFields(AnimState state, bool engineActive)
        {
            bool hasAnim = animationControl != null;
            bool isEditor = HighLogic.LoadedSceneIsEditor;
            switch (state)
            {

                case AnimState.PLAYING_BACKWARD://retracting
                    {
                        Events["Activate"].active = false;
                        Events["Shutdown"].active = false;
                        Actions["ActivateAction"].active = isEditor;
                        Actions["ShutdownAction"].active = isEditor;
                        Actions["OnAction"].active = isEditor;
                        Events["deployEngineEvent"].active = hasAnim;
                        Events["retractEngineEvent"].active = false;
                        break;
                    }

                case AnimState.PLAYING_FORWARD://deploying
                    {
                        Events["Activate"].active = false;
                        Events["Shutdown"].active = false;
                        Actions["ActivateAction"].active = isEditor;
                        Actions["ShutdownAction"].active = isEditor;
                        Actions["OnAction"].active = isEditor;
                        Events["deployEngineEvent"].active = false;
                        Events["retractEngineEvent"].active = hasAnim;
                        break;
                    }

                case AnimState.STOPPED_END://deployed or no anim
                    {
                        Events["Activate"].active = !engineActive;
                        Events["Shutdown"].active = engineActive;
                        Actions["ActivateAction"].active = true;
                        Actions["ShutdownAction"].active = true;
                        Actions["OnAction"].active = true;
                        Events["deployEngineEvent"].active = false;
                        Events["retractEngineEvent"].active = hasAnim;
                        break;
                    }

                case AnimState.STOPPED_START://retracted
                    {
                        Events["Activate"].active = false;
                        Events["Shutdown"].active = false;
                        Actions["ActivateAction"].active = isEditor;
                        Actions["ShutdownAction"].active = isEditor;
                        Actions["OnAction"].active = isEditor;
                        Events["deployEngineEvent"].active = hasAnim;
                        Events["retractEngineEvent"].active = false;
                        break;
                    }

            }
        }
        
        public void onAnimationStatusChanged(AnimState state)
        {
            if (state == AnimState.STOPPED_END)
            {
                if (HighLogic.LoadedSceneIsFlight)
                {
                    base.Activate();
                }
            }
        }
        
        private void setAnimationState(AnimState state)
        {
            if (animationControl != null)
            {
                AnimState currentState = animationControl.getAnimationState();
                //exceptions below fix issues of OnActive being called by moduleEngine during startup
                if (currentState == AnimState.STOPPED_END && state == AnimState.PLAYING_FORWARD) { return; }//don't allow deploying from deployed
                else if (currentState == AnimState.STOPPED_START && state == AnimState.PLAYING_BACKWARD) { return; }//don't allow retracting from retracted
                animationControl.setToState(state);
            }
        }
    }
}

