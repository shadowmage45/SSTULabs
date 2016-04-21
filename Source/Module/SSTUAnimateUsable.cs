using System;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUAnimateUsable : PartModule
    {
        [KSPField]
        public int animationID;

        [KSPField]
        public String deployActionName = "Deploy";

        [KSPField]
        public String retractActionName = "Retract";

        [KSPField]
        public bool showState = false;

        [KSPField]
        public String stateLabel = "AnimState";

        [KSPField]
        public String retractedStateName = "Retracted";

        [KSPField]
        public String retractingStateName = "Retracting";

        [KSPField]
        public String deployedStateName = "Deployed";

        [KSPField]
        public String deployingStateName = "Deploying";

        [KSPField]
        public string deployActionGroup = "NONE";

        [KSPField]
        public string retractActionGroup = "NONE";

        [KSPField]
        public bool useResourcesWhileDeployed = false;

        [KSPField]
        public String resourceNames = string.Empty;

        [KSPField]
        public String resourceAmounts = string.Empty;

        [KSPField]
        public bool usableInFlight = true;

        [KSPField]
        public bool usableInEditor = true;

        [KSPField]
        public bool usableFromEVA = false;

        [KSPField]
        public bool usableUnfocused = false;

        [KSPField]
        public bool usableUncommanded = false;

        [KSPField]
        public float unfocusedRange = 200f;
        
        [KSPField(guiName = "AnimState", isPersistant = true)]
        public String displayState = string.Empty;
        
        private SSTUAnimateControlled animationControl;

        [KSPEvent(guiName = "Deploy", guiActive = true, guiActiveEditor = true)]
        public void deployEvent()
        {
            setAnimationState(AnimState.PLAYING_FORWARD);
        }

        [KSPEvent(guiName = "Retract", guiActive = true, guiActiveEditor = true)]
        public void retractEvent()
        {
            setAnimationState(AnimState.PLAYING_BACKWARD);
        }

        [KSPAction("Deploy")]
        public void deployAction(KSPActionParam p)
        {
            if (p.type == KSPActionType.Activate)
            {
                deployEvent();
            }
        }

        [KSPAction("Retract")]
        public void retractAction(KSPActionParam p)
        {
            if (p.type == KSPActionType.Deactivate)
            {
                retractEvent();
            }
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            animationControl = SSTUAnimateControlled.locateAnimationController(part, animationID, onAnimationStatusChanged);
            initializeGuiFields();
            updateGuiDataFromState(animationControl.getAnimationState());
        }
        
        private void updateGuiDataFromState(AnimState state)
        {
            BaseEvent deployEvent = Events["deployEvent"];
            BaseEvent retractEvent = Events["retractEvent"];
            bool sceneEnabled = (HighLogic.LoadedSceneIsFlight && usableInFlight) || (HighLogic.LoadedSceneIsEditor && usableInEditor);
            switch (state)
            {
                case AnimState.PLAYING_BACKWARD:
                    {
                        deployEvent.active = sceneEnabled && true;
                        retractEvent.active = false;
                        displayState = retractingStateName;
                        break;
                    }
                case AnimState.PLAYING_FORWARD:
                    {
                        deployEvent.active = false;
                        retractEvent.active = sceneEnabled && true;
                        displayState = deployingStateName;
                        break;
                    }
                case AnimState.STOPPED_END:
                    {
                        deployEvent.active = false;
                        retractEvent.active = sceneEnabled && true;
                        displayState = deployedStateName;
                        break;
                    }
                case AnimState.STOPPED_START:
                    {
                        deployEvent.active = sceneEnabled && true;
                        retractEvent.active = false;
                        displayState = retractedStateName;
                        break;
                    }
            }
        }
        
        public void onAnimationStatusChanged(AnimState state)
        {
            updateGuiDataFromState(state);
        }
        
        private void setAnimationState(AnimState state)
        {
            if (animationControl != null) { animationControl.setToState(state); }
            updateGuiDataFromState(state);
        }
        
        private void initializeGuiFields()
        {
            Fields["displayState"].guiName = stateLabel;
            Fields["displayState"].guiActive = Fields["displayState"].guiActiveEditor = showState;

            Actions["deployAction"].guiName = deployActionName;
            Actions["retractAction"].guiName = retractActionName;

            if (deployActionGroup != "NONE")
            {
                try
                {
                    KSPActionGroup ag = (KSPActionGroup)Enum.Parse(typeof(KSPActionGroup), deployActionGroup, true);
                    Actions["deployAction"].defaultActionGroup = ag;
                }
                catch (Exception e)
                {
                    MonoBehaviour.print("ERROR PARSING ACTION GROUP FOR NAME: " + deployActionGroup + " :: " + e.Message);                    
                }                                
            }
            if (retractActionGroup != "NONE")
            {
                try
                {
                    KSPActionGroup ag = (KSPActionGroup)Enum.Parse(typeof(KSPActionGroup), retractActionGroup, true);
                    Actions["retractAction"].defaultActionGroup = ag;
                }
                catch (Exception e)
                {
                    MonoBehaviour.print("ERROR PARSING ACTION GROUP FOR NAME: " + retractActionGroup + " :: " + e.Message);
                }
            }


            BaseEvent deployEvent = Events["deployEvent"];
            deployEvent.guiName = deployActionName;
            deployEvent.externalToEVAOnly = usableFromEVA;
            deployEvent.guiActiveUncommand = usableUncommanded;
            deployEvent.guiActiveUnfocused = usableUnfocused;
            deployEvent.unfocusedRange = unfocusedRange;
            BaseEvent retractEvent = Events["retractEvent"];
            retractEvent.guiName = retractActionName;
            retractEvent.externalToEVAOnly = usableFromEVA;
            retractEvent.guiActiveUncommand = usableUncommanded;
            retractEvent.guiActiveUnfocused = usableUnfocused;
            retractEvent.unfocusedRange = unfocusedRange;
        }
    }
}

