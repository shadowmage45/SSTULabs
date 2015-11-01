using System;

namespace SSTUTools
{
    
    public class SSTUDeployableEngine : ModuleEnginesFX
    {

        [KSPField]
        public int animationID = -1;

        private SSTUAnimateControlled animationControl;

        [KSPAction("Toggle Deploy")]
        public void toggleDeployment(KSPActionParam param)
        {
            if (animationControl != null)
            {
                SSTUAnimState state = animationControl.getAnimationState();
                if (state == SSTUAnimState.PLAYING_BACKWARD || state == SSTUAnimState.STOPPED_START)//either playing retract anim, or already retracted; deploy it
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
            setAnimationState(SSTUAnimState.PLAYING_FORWARD);
        }

        [KSPEvent(name = "retractEngineEvent", guiName = "Retract Engine", guiActive = true, guiActiveEditor = true)]
        public void retractEngineEvent()
        {
            Shutdown();
            setAnimationState(SSTUAnimState.PLAYING_BACKWARD);
        }
        
        public override void OnStart(PartModule.StartState state)
        {
            animationControl = SSTUAnimateControlled.locateAnimationController(part, animationID, onAnimationStatusChanged);
            base.OnStart(state);
            setupGuiFields(animationControl == null ? SSTUAnimState.STOPPED_END : animationControl.getAnimationState(), EngineIgnited);
        }

        //check for control enabled and deployment status (if animated)
        public override void OnActive()
        {
            if (animationControl == null || animationControl.getAnimationState() == SSTUAnimState.STOPPED_END)
            {
                base.OnActive();
            }
            else
            {
                setAnimationState(SSTUAnimState.PLAYING_FORWARD);
            }
        }

        #warning this causes NREs in the editor
        //TODO fix ^^
        //hopefully unity is smart about calling the proper FixedUpdate method, this... seems to work so far
        new public void FixedUpdate()
        {
            base.FixedUpdate();
            setupGuiFields(animationControl == null ? SSTUAnimState.STOPPED_END : animationControl.getAnimationState(), EngineIgnited);
        }

        //TODO - stuff for non-restartable and non-stoppable engines
        private void setupGuiFields(SSTUAnimState state, bool engineActive)
        {
            bool hasAnim = animationControl != null;
            bool isEditor = HighLogic.LoadedSceneIsEditor;
            switch (state)
            {

                case SSTUAnimState.PLAYING_BACKWARD://retracting
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

                case SSTUAnimState.PLAYING_FORWARD://deploying
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

                case SSTUAnimState.STOPPED_END://deployed or no anim
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

                case SSTUAnimState.STOPPED_START://retracted
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
        
        public void onAnimationStatusChanged(SSTUAnimState state)
        {
            if (state == SSTUAnimState.STOPPED_END)
            {
                base.Activate();
            }
        }
        
        private void setAnimationState(SSTUAnimState state)
        {
            if (animationControl != null)
            {
                SSTUAnimState currentState = animationControl.getAnimationState();
                //exceptions below fix issues of OnActive being called by moduleEngine during startup
                if (currentState == SSTUAnimState.STOPPED_END && state == SSTUAnimState.PLAYING_FORWARD) { return; }//don't allow deploying from deployed
                else if (currentState == SSTUAnimState.STOPPED_START && state == SSTUAnimState.PLAYING_BACKWARD) { return; }//don't allow retracting from retracted
                animationControl.setToState(state);
            }
        }
    }
}

