using System;

namespace SSTUTools
{
	//TODO remove staging icon if module is disabled while in editor
	public class SSTUDeployableEngine : ModuleEnginesFX, IControlledModule
	{		
		
		[KSPField(isPersistant=true)]
		public bool moduleControlEnabled = false;
		
		[KSPField]
		public int controlID = -1;
		
		[KSPField]
		public int animationID = -1;
		
		private SSTUAnimateControlled animationControl;
		
		[KSPEvent(name="deployEngineEvent", guiName = "Deploy Engine", guiActive = true, guiActiveEditor=true)]
		public void deployEngineEvent()
		{			
			setAnimationState(SSTUAnimState.PLAYING_FORWARD);
		}

		[KSPEvent(name="retractEngineEvent", guiName = "Retract Engine", guiActive = true, guiActiveEditor=true)]
		public void retractEngineEvent()
		{
			Shutdown ();
			setAnimationState(SSTUAnimState.PLAYING_BACKWARD);
		}
		
		//TODO - might need to force-disable effects (if they exist)
		//find animations, check for control enabled
		public override void OnStart(PartModule.StartState state)
		{
			if(controlID==-1){moduleControlEnabled=true;}
			animationControl = SSTUAnimateControlled.locateAnimationController (part, animationID, onAnimationStatusChanged);
			base.OnStart(state);
			if(moduleControlEnabled)
			{
				setupGuiFields(animationControl==null? SSTUAnimState.STOPPED_END : animationControl.getAnimationState(), EngineIgnited);
			}
			else//disabled engine entire, disable all events/gui stuff/staging icon
			{				
				disableAllGuiFields();
				if(part.stagingIcon!=null && part.stagingIcon.Equals(DefaultIcons.LIQUID_ENGINE.ToString()))
				{
					part.stagingIcon = string.Empty;
				}
			}
		}
		
		//check for control enabled and deployment status (if animated)
		public override void OnActive ()
		{
			if(moduleControlEnabled)
			{
				if(animationControl==null || animationControl.getAnimationState()==SSTUAnimState.STOPPED_END)
				{
					base.OnActive ();
				}
				else
				{
					setAnimationState(SSTUAnimState.PLAYING_FORWARD);
				}
			}
		}
		
		//hopefully unity is smart about calling the proper FixedUpdate method, this... seems to work so far
		new public void FixedUpdate()
		{
			if(moduleControlEnabled)
			{
				base.FixedUpdate();
				setupGuiFields(animationControl==null? SSTUAnimState.STOPPED_END : animationControl.getAnimationState(), EngineIgnited);
			}
		}
		
		//TODO
		public void disableModule ()
		{
			disableAllGuiFields();
			moduleControlEnabled = false;
			if(part.stagingIcon!=null && part.stagingIcon.Equals(DefaultIcons.LIQUID_ENGINE.ToString()))
			{
				part.stagingIcon = string.Empty;
				//re-sort staging icons/etc
			}
			throw new NotImplementedException ();
		}
		
		//TODO
		public void enableModule ()
		{
			moduleControlEnabled = true;
			if(part.stagingIcon!=null || part.stagingIcon.Length==0)
			{
				part.stagingIcon = DefaultIcons.LIQUID_ENGINE.ToString();
				//re-sort staging icons/etc
			}
			throw new NotImplementedException ();
		}
		
		//DONE
		public int getControlID ()
		{
			return controlID;
		}
		
		//DONE
		public bool isControlEnabled ()
		{
			return moduleControlEnabled;
		}
		
		//TODO - stuff for non-restartable and non-stoppable engines
		private void setupGuiFields(SSTUAnimState state, bool engineActive)
		{
			bool hasAnim = animationControl!=null;
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
		
		//DONE
		//disable all gui fields, to be called for disabled engine module
		private void disableAllGuiFields()
		{
			Events["Activate"].active = false;
			Events["Shutdown"].active = false;
			Actions["ActivateAction"].active = false;
			Actions["ShutdownAction"].active = false;
			Actions["OnAction"].active = false;
			Fields["thrustPercentage"].guiActive = Fields["thrustPercentage"].guiActiveEditor = false;
			Fields["realIsp"].guiActive = false;
			Fields["finalThrust"].guiActive = false;
			Fields["fuelFlowGui"].guiActive = false;
			Fields["status"].guiActive = false;
			Fields["statusL2"].guiActive = false;
						
			Events["deployEngineEvent"].active = false;
			Events["retractEngineEvent"].active = false;
		}

		//DONE
		public void onAnimationStatusChanged(SSTUAnimState state)
		{
			if(state==SSTUAnimState.STOPPED_END)
			{
				base.Activate();
			}
		}
		
		//DONE
		private void setAnimationState(SSTUAnimState state)
		{
			print ("DeployableEngine setting anim state to : "+state);
			if(animationControl!=null)
			{
				SSTUAnimState currentState = animationControl.getAnimationState();
				//exceptions below fix issues of OnActive being called by moduleEngine during startup
				if(currentState==SSTUAnimState.STOPPED_END && state==SSTUAnimState.PLAYING_FORWARD){return;}//don't allow deploying from deployed
				else if(currentState == SSTUAnimState.STOPPED_START && state == SSTUAnimState.PLAYING_BACKWARD){return;}//don't allow retracting from retracted
				animationControl.setToState(state);
				throw new NotImplementedException();
			}
		}
	}
}

