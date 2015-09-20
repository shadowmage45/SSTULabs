using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SSTUTools
{
	//multi-leg capable landing leg
	//maintains wheel collider for each leg
	//uses SSTUAnimate for deploy/retract handling
	public class SSTULandingLeg : SSTUControlledModule
	{
		
		public enum LegState
		{
			DEPLOYING,
			DEPLOYED,
			RETRACTING,
			RETRACTED,
			BROKEN,
		}
		
		public class LandingLegData
		{
			public Transform suspensionTransform;
			public Transform footColliderTransform;
			public WheelCollider wheelCollider;	
		}
		
		//loaded from config, comma-separated list of transform names that have attached wheel colliders
		[KSPField]
		public string wheelColliderNames = "unknown";
		
		//loaded from config, comma-separated list of suspension transform names
		[KSPField]
		public string suspensionTransformNames = "unknown";	
		
		[KSPField]
		public string footColliderNames = "unknown";	
				
		//wheel collider setup config values		
		[KSPField]
		public float suspensionTravel = -1;

		[KSPField]
		public float suspensionSpring = -1;

		[KSPField]
		public float suspensionDamper = -1;
		
		[KSPField]
		public float suspensionTarget = -1;
		
		[KSPField]
		public float wheelColliderRadius = -1;
		
		[KSPField]
		public float suspensionUpperLimit = -1;
		
		[KSPField]
		public float suspensionOffset = 0;

		[KSPField]
		public int animationID = 0;	
		
		//gui label setup config values		
		[KSPField]
		public string deployGuiName = "Deploy Gear";
			
		[KSPField]
		public string retractGuiName = "Retract Gear";
		
		[KSPField]
		public string actionGroupGuiName = "Deploy/Retract Gear";
		
		[KSPField]
		public string repairGuiName = "Repair Gear";
				
		//module internal use fields		
		[KSPField(isPersistant=true)]
		public string persistentState = LegState.RETRACTED.ToString();
								
		private float decompressTime = 0.0f;
								
		private List<LandingLegData> legData = new List<LandingLegData>();
		
		private LegState legState = LegState.RETRACTED;
						
		private SSTUAnimateControlled animationController;

		#region KSP Actions

		//KSP Action Group 'Toggle'
		[KSPAction("Retract/Deploy Gear", KSPActionGroup.Gear)]
		public void toggleAction(KSPActionParam param)
		{
			if (param.type == KSPActionType.Activate)
			{
				if(legState==LegState.RETRACTED || legState==LegState.RETRACTING)
				{
					deployEvent	();
				}	
			}
			else			
			{
				if(legState==LegState.DEPLOYED || legState==LegState.DEPLOYING)
				{
					retractEvent ();
				}	
			}			
		}

		[KSPEvent (name= "deployEvent", guiName = "Deploy Gear", guiActiveUnfocused = true, externalToEVAOnly = true, guiActive = true, unfocusedRange = 4f, guiActiveEditor = true)]
		public void deployEvent()
		{
			if(vessel!=null && !vessel.IsControllable){return;}
			if(legState==LegState.BROKEN){return;}
			extendLeg();
		}

		[KSPEvent (name= "retractEvent", guiName = "Retract Gear", guiActiveUnfocused = true, externalToEVAOnly = true, guiActive = true, unfocusedRange = 4f, guiActiveEditor = true)]
		public void retractEvent()
		{
			if(vessel!=null && !vessel.IsControllable){return;}
			if(legState==LegState.BROKEN){return;}
			retractLeg();
		}
		
		[KSPEvent (name= "repairEvent", guiName = "Repair Gear", guiActiveUnfocused = false, externalToEVAOnly = true, guiActive = false, unfocusedRange = 4f, guiActiveEditor = false)]
		public void repairEvent()
		{
			if(vessel!=null && !vessel.IsControllable){return;}
			if(legState!=LegState.BROKEN){return;}
			repairLeg();
		}

		#endregion
		
		#region KSP overrides
		
		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			try
			{
				legState = (LegState)Enum.Parse(typeof(LegState), persistentState);
			}
			catch(Exception e)
			{
				print (e.Message);
			}
			if(legState==LegState.RETRACTING)
			{
				legState=LegState.RETRACTED;
			}
			else if(legState==LegState.DEPLOYING)
			{
				legState=LegState.DEPLOYED;
			}
			updateGuiControlsFromState();
		}
						
		public override void OnStart (PartModule.StartState state)
		{
			base.OnStart (state);	

			#region animationSetup
			animationController = SSTUAnimateControlled.locateAnimationController(part, animationID, onAnimationStatusChanged);
			#endregion
			
			#region gui setup			
			Events["retractEvent"].guiName = retractGuiName;
			Events["deployEvent"].guiName = deployGuiName;
			Events["repairEvent"].guiName = repairGuiName;
			Actions["toggleAction"].guiName = actionGroupGuiName;
			
			Events["retractEvent"].active = legState==LegState.DEPLOYED;
			Events["deployEvent"].active = legState==LegState.RETRACTED;
			Events["repairEvent"].active = legState==LegState.BROKEN;			
			#endregion					
			
			#region colliderSetup
			if(HighLogic.LoadedSceneIsFlight)
			{							
				//setup suspension upper limits, defaults to config value for suspension travel
				if(suspensionUpperLimit==-1){suspensionUpperLimit=suspensionTravel;}
				
				String[] wcNameArray = wheelColliderNames.Split(',');			
				String[] susNameArray = suspensionTransformNames.Split(',');
				String[] footNameArray = footColliderNames.Split(',');
				int length = wcNameArray.Length < susNameArray.Length ? wcNameArray.Length : susNameArray.Length;
				
				LandingLegData legData;
				Transform suspensionTransform;
				Transform wheelColliderTransform;
				Transform footColliderTransform;
				WheelCollider wheelCollider;
				float wcRadius = 0, wcTravel = 0, wcTarget = 0, wcSpring = 0, wcDamper = 0;
				
				for(int i = 0; i < length; i++)
				{
					
					suspensionTransform = part.FindModelTransform(susNameArray[i].Trim());
					wheelColliderTransform = part.FindModelTransform(wcNameArray[i].Trim());				
					if(suspensionTransform==null || wheelColliderTransform==null)
					{
						print ("error locating transforms for names: "+susNameArray[i]+", "+wcNameArray[i]);
						print ("found objects: "+suspensionTransform+", "+wheelColliderTransform); 
						continue;
					}					
					wheelCollider = wheelColliderTransform.GetComponent<WheelCollider>();
					if(wheelCollider==null)
					{
						
						print ("Wheel collider transform does not contain a valid wheel collider!  name: "+wcNameArray[i]);
						SSTUUtils.recursePrintComponents(wheelColliderTransform.gameObject, "");
						continue;
					}					
					if(i<footNameArray.Length)
					{
						footColliderTransform = part.FindModelTransform(footNameArray[i].Trim ());					
					}
					else
					{
						footColliderTransform = null;
					}					
					legData = new LandingLegData();
					legData.suspensionTransform = suspensionTransform;
					legData.wheelCollider = wheelCollider;
					legData.footColliderTransform = footColliderTransform;
					this.legData.Add (legData);
					
					//use default values if config values were not written
					wcRadius = wheelColliderRadius==-1 ? wheelCollider.radius : wheelColliderRadius;
					wcTravel = suspensionTravel==-1 ? wheelCollider.suspensionDistance : suspensionTravel;
					wcTarget = suspensionTarget==-1 ? wheelCollider.suspensionSpring.targetPosition : suspensionTarget;
					wcSpring = suspensionSpring==-1 ? wheelCollider.suspensionSpring.spring : suspensionSpring;
					wcDamper = suspensionDamper==-1 ? wheelCollider.suspensionSpring.damper : suspensionDamper;
					
					//setup wheel collider radius and suspension distance in case of overrides in config
					wheelCollider.radius = wcRadius;				
					wheelCollider.suspensionDistance = wcTravel;
					wheelCollider.brakeTorque = 1000f;
					
					//setup a new JointSpring for the wheel to use, overriding values from config if needed
					JointSpring spring = new JointSpring();
					spring.spring = wcSpring;
					spring.damper = wcDamper;
					spring.targetPosition = wcTarget;
					wheelCollider.suspensionSpring = spring;//assign the new spring joint to the wheel collider						
				}								
			}
			
			#endregion	
			
			setLegState(legState);
			if(!moduleControlEnabled)
			{
				onControlDisabled();
			}
		}
		
		#endregion		

		#region IControlledModule overrides

		public override void updateGuiControlsFromState (bool enabled)
		{
			updateGuiControlsFromState ();
		}
		
		public override void onControlEnabled ()
		{
			setLegState(legState);
			updateGuiControlsFromState();
		}
		
		public override void onControlDisabled ()
		{
			enableWheelColliders(false);
			enableFootColliders(false);
			updateGuiControlsFromState();
		}

		#endregion

		#region SSTUAnimateControlled interface

		public void onAnimationStatusChanged(SSTUAnimState state)
		{
			if(state==SSTUAnimState.STOPPED_START)
			{
				setLegState(LegState.RETRACTED);
			}
			else if(state==SSTUAnimState.STOPPED_END)
			{
				setLegState(LegState.DEPLOYED);
			}
		}

		#endregion
		
		#region Unity Overrides
		
		public void FixedUpdate()
		{			
			if (!moduleControlEnabled)
			{
				return;
			}
			if(legState==LegState.DEPLOYED)
			{
				updateSuspension();									
			}
			else if(decompressTime>0)
			{
				decompress();		
			}
		}
		
		#endregion
		
		#region private udpate methods
		
		private void decompress()
		{
			float fixedTick = TimeWarp.fixedDeltaTime;
			
			float percent = fixedTick / decompressTime;//need to move this percent of total remaining move
			if(percent>1){percent=1;}
			if(percent<0){percent=0;}
			
			Vector3 startPos;
			Vector3 endPos;
			Vector3 diff;
			
			for (int i = 0; i < legData.Count; i++)
			{				
				WheelCollider wheelCollider = legData[i].wheelCollider;
				Transform suspensionParent = legData[i].suspensionTransform;
				startPos = suspensionParent.transform.position;
				endPos = wheelCollider.transform.position;
				diff = endPos - startPos;
				suspensionParent.position = suspensionParent.position + diff * percent;
			}
			
			decompressTime-= fixedTick;
			if(decompressTime<=0)
			{
				decompressTime = 0;
				resetSuspensionPosition();
				enableWheelColliders(false);
				animationController.setToState(SSTUAnimState.PLAYING_BACKWARD);
			}			
		}
			
		private void updateSuspension()
		{
			for (int i = 0; i < legData.Count; i++)
			{
				RaycastHit raycastHit;
				WheelCollider wheelCollider = legData[i].wheelCollider;
				Transform suspensionParent = legData[i].suspensionTransform;
				int layerMask = 622593;
				if (Physics.Raycast(wheelCollider.transform.position, -wheelCollider.transform.up, out raycastHit, ((wheelCollider.suspensionDistance + wheelCollider.radius) * part.rescaleFactor), layerMask))
				{
					float wheelRadius = wheelCollider.radius * part.rescaleFactor;
					float distance = Vector3.Distance(raycastHit.point, wheelCollider.transform.position);					
					float suspensionTravel = wheelCollider.suspensionDistance*part.rescaleFactor + wheelRadius;					
					if(distance >= suspensionTravel){ distance = suspensionTravel;}
					float compressionAmount = (suspensionTravel - distance) + (suspensionOffset * part.rescaleFactor);					
					if(compressionAmount < 0){compressionAmount=0;}//fix for negative compression (extended too far)
					suspensionParent.position = wheelCollider.transform.position + (wheelCollider.transform.up * compressionAmount);
				}
				else
				{
					suspensionParent.position = wheelCollider.transform.position;
				}		
			}
		}
		
		#endregion
		
		#region private utility methods
		
		private void retractLeg()
		{
			setLegState(LegState.RETRACTING);
		}
		
		private void extendLeg()
		{
			setLegState(LegState.DEPLOYING);	
		}
		
		private void repairLeg()
		{
			setLegState(LegState.RETRACTED);
		}
		
		private void setLegState(LegState newState)
		{
			LegState prevState = legState;
			legState = newState;
			persistentState = legState.ToString();
									
			switch(newState)
			{	
				
			case LegState.BROKEN:
			{
				enableWheelColliders(false);
				enableFootColliders(false);
				animationController.setToState(SSTUAnimState.STOPPED_START);
				decompressTime = 0;
				break;
			}		
				
			case LegState.DEPLOYED:
			{			
				animationController.setToState(SSTUAnimState.STOPPED_END);
				decompressTime = 0;
				if(HighLogic.LoadedSceneIsFlight)
				{
					enableWheelColliders(true);
					enableFootColliders(false);
				}
				break;	
			}
				
			case LegState.RETRACTED:
			{			
				enableWheelColliders(false);
				enableFootColliders(false);
				decompressTime = 0;
				animationController.setToState(SSTUAnimState.STOPPED_START);
				break;	
			}
			
			case LegState.DEPLOYING:
			{
				if(decompressTime>0)//decompress/retracting; just stop decompressing, disable foot colliders, and set state to deployed;
				{
					decompressTime = 0;
					legState = LegState.DEPLOYED;
					enableFootColliders(false);
					enableWheelColliders(true);
					break;
				}
				else
				{
					enableWheelColliders(false);	
					decompressTime = 0;
					resetSuspensionPosition();//just in case something got fubard
					if(HighLogic.LoadedSceneIsFlight)
					{
						enableFootColliders(true);	
						animationController.setToState(SSTUAnimState.PLAYING_FORWARD);
					}
					else//we are in editor
					{	
						legState = LegState.DEPLOYED;
						animationController.setToState(SSTUAnimState.STOPPED_END);
					}
					break;	
				}				
			}
								
			case LegState.RETRACTING:	
			{
				enableFootColliders(true);
				if(prevState==LegState.DEPLOYED)//fix for 'instant retract' bug on newly loaded vessel
				{						
					if(HighLogic.LoadedSceneIsFlight)
					{
						decompressTime = 1.0f;
						//from here the decompress logic will trigger retract animation when it is needed...
					}
					else//else we are in editor, do not bother with retract decompress, go straight to playing animation;
					{
						//TODO instant-retract when in editor
						resetSuspensionPosition();						
						animationController.setToState(SSTUAnimState.STOPPED_START);
						legState=LegState.RETRACTED;
					}
				}
				else
				{
					if(HighLogic.LoadedSceneIsFlight)
					{						
						resetSuspensionPosition();
						enableWheelColliders(false);
						animationController.setToState(SSTUAnimState.PLAYING_BACKWARD);
					}
					else
					{
						resetSuspensionPosition();						
						animationController.setToState(SSTUAnimState.STOPPED_START);
						legState=LegState.RETRACTED;
					}
				}
				break;
			}

			}//end switch

			updateGuiControlsFromState();
		}
		
		private void updateGuiControlsFromState()
		{
			if(!moduleControlEnabled)
			{
				Events["repairEvent"].active = false;
				Events["deployEvent"].active = false;
				Events["retractEvent"].active = false;
				Actions["toggleAction"].active = false;
				return;
			}
			switch(legState)
			{				
			case LegState.BROKEN:
				Events["repairEvent"].active = true;
				Events["deployEvent"].active = false;
				Events["retractEvent"].active = false;
				Actions["toggleAction"].active = false;
				break;
				
			case LegState.DEPLOYED:	
				Events["deployEvent"].active = false;
				Events["retractEvent"].active = true;
				Actions["toggleAction"].active = true;
				break;
				
			case LegState.RETRACTED:		
				Events["deployEvent"].active = true;
				Events["retractEvent"].active = false;
				Actions["toggleAction"].active = true;		
				break;
				
			case LegState.DEPLOYING:
				Events["deployEvent"].active = false;
				Events["retractEvent"].active = true;
				Actions["toggleAction"].active = true;
				break;
				
			case LegState.RETRACTING:	
				Events["deployEvent"].active = true;
				Events["retractEvent"].active = false;
				Actions["toggleAction"].active = true;
				break;
			}
		}
		
		private void enableWheelColliders(bool val)
		{
			int len = legData.Count;
			for(int i = 0; i < len; i++)
			{
				legData[i].wheelCollider.enabled = val;
			}
		}
		
		private void enableFootColliders(bool val)
		{
			int len = legData.Count;
			for(int i = 0; i < len; i++)
			{
				if(legData[i].footColliderTransform!=null && legData[i].footColliderTransform.collider!=null)
				{
					legData[i].footColliderTransform.collider.enabled = val;
				}
			}
		}
		
		private void resetSuspensionPosition()
		{
			int len = legData.Count;
			for(int i = 0; i < len; i++)
			{
				legData[i].suspensionTransform.position = legData[i].wheelCollider.transform.position;
			}
		}
		#endregion
	}
}

