using System;

namespace SSTUTools
{
	//TODO
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
		
		[KSPField(guiName="AnimState", isPersistant=true)]
		public String displayState = string.Empty;
				
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
		public bool useResourcesWhileDeployed = false;
				
		[KSPField]
		public String resourceNames = string.Empty;
		
		[KSPField]
		public String resourceAmounts = string.Empty;
		
		//TODO
		private String[] resNames;
		private float[] resAmounts;
				
		private SSTUAnimateControlled animationControl;
		
		[KSPEvent(guiName="Deploy", guiActive = true, guiActiveEditor = true)]
		public void deployEvent()
		{
			setAnimationState(SSTUAnimState.PLAYING_FORWARD);
		}
		
		[KSPEvent(guiName="Retract", guiActive = true, guiActiveEditor = true)]
		public void retractEvent()
		{
			setAnimationState(SSTUAnimState.PLAYING_BACKWARD);
		}
		
		[KSPAction("Deploy")]
		public void deployAction(KSPActionParam p)
		{
			deployEvent();
		}
		
		[KSPAction("Retract")]
		public void retractAction(KSPActionParam p)
		{
			retractEvent();
		}
		
		public override void OnStart (PartModule.StartState state)
		{
			base.OnStart (state);
			animationControl = SSTUAnimateControlled.locateAnimationController(part, animationID, onAnimationStatusChanged);
			initializeGuiFields();
			updateGuiDataFromState(animationControl.getAnimationState());
		}
		
		//DONE
		private void updateGuiDataFromState(SSTUAnimState state)
		{
			switch(state)
			{
			case SSTUAnimState.PLAYING_BACKWARD:
			{				
				Events["deployEvent"].active = true;
				Events["retractEvent"].active = false;
				Actions["deployAction"].active = true;
				Actions["retractAction"].active = true;
				displayState = retractingStateName;
				break;
			}
			case SSTUAnimState.PLAYING_FORWARD:
			{
				Events["deployEvent"].active = false;
				Events["retractEvent"].active = true;
				Actions["deployAction"].active = true;
				Actions["retractAction"].active = true;
				displayState = deployingStateName;
				break;
			}
			case SSTUAnimState.STOPPED_END:
			{
				Events["deployEvent"].active = false;
				Events["retractEvent"].active = true;
				Actions["deployAction"].active = true;
				Actions["retractAction"].active = true;
				displayState = deployedStateName;
				break;
			}
			case SSTUAnimState.STOPPED_START:
			{
				Events["deployEvent"].active = true;
				Events["retractEvent"].active = false;
				Actions["deployAction"].active = true;
				Actions["retractAction"].active = true;
				displayState = retractedStateName;
				break;
			}
			}
		}
		
		//DONE
		public void onAnimationStatusChanged(SSTUAnimState state)
		{
			print ("usable anim rec callback state change: "+state);
			updateGuiDataFromState(state);
		}
		
		//DONE
		private void setAnimationState(SSTUAnimState state)
		{
			if(animationControl!=null){animationControl.setToState(state);}
			updateGuiDataFromState(state);
		}

		//DONE
		private void initializeGuiFields()
		{
			Fields["displayState"].guiName = stateLabel;
			if(showState)
			{
				Fields["displayState"].guiActive = Fields["displayState"].guiActiveEditor = true;
			}
			Events["deployEvent"].guiName = deployActionName;
			Events["retractEvent"].guiName = retractActionName;
			Actions["deployAction"].guiName = deployActionName;
			Actions["retractAction"].guiName = retractActionName;
		}
	}
}

