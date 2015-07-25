using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SSTUTools
{
	//multi-leg capable landing leg
	//maintains wheel collider for each leg
	//uses SSTUAnimate for deploy/retract handling
	public class SSTULandingLeg : PartModule
	{
		
		public enum LegState
		{
			DEPLOYING,
			DEPLOYED,
			RETRACTING,
			RETRACTED,
			BROKEN
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
		
		
		//animation setup config values
		
		[KSPField]
		public string animationName = "unknown";
		
		[KSPField]
		public int animationLayer = 1;
		
		[KSPField]
		public float customAnimationSpeed = 1;
	
		
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
						
		private Animation deployAnimation;		
		
		private bool firstUpdate;		

		public SSTULandingLeg ()
		{
			
		}
		
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
		}
						
		public override void OnStart (PartModule.StartState state)
		{
			base.OnStart (state);			
			
			#region animationSetup		
			
			deployAnimation = part.FindModelAnimators(animationName).FirstOrDefault();
			if(deployAnimation!=null)
			{	
				deployAnimation[animationName].layer = animationLayer;			
				if(legState==LegState.DEPLOYED)
				{
					deployAnimation[animationName].normalizedTime = 1;
				}
				else//retracted or broken
				{
					deployAnimation[animationName].normalizedTime = 0;
				}
			}
			else
			{
				print ("Could not locate animation for name: "+animationName);
			}

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
						print ("wheel collider transform does not contain a valid wheel collider!");
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
			
			if(HighLogic.LoadedSceneIsFlight && legState==LegState.DEPLOYED)
			{
				enableFootColliders(true);
			}
		}
		
		#endregion
		
		#region Unity Overrides
		
		public void FixedUpdate()
		{			
			//print ("SSTULandingLeg FixedUpdate");
			updateAnimation();
			if(HighLogic.LoadedSceneIsFlight && !part.packed)
			{
				fixedUpdateFlight();			
			}
			else if(HighLogic.LoadedSceneIsEditor)
			{
				fixedUpdateEditor();
			}
		}
		
		#endregion
		
		#region private udpate methods
		
		private void fixedUpdateFlight()
		{
			if(!firstUpdate)
			{
				firstUpdate=true;
				if(legState==LegState.DEPLOYED)
				{
					enableFootColliders(false);
				}
			}
			if(legState==LegState.DEPLOYED)
			{								
				updateSuspension();				
			}
			if(decompressTime>0)
			{
				decompress();
			}
		}
				
		private void fixedUpdateEditor()
		{

		}
		
		private void updateAnimation()
		{
			if(!deployAnimation.isPlaying)
			{
				if(legState==LegState.DEPLOYING){setLegState(LegState.DEPLOYED);}
				else if(legState==LegState.RETRACTING){setLegState(LegState.RETRACTED);}
			}
		}
		
		private void decompress()
		{
			decompressTime -= TimeWarp.fixedDeltaTime;
			if(decompressTime<0){decompressTime = 0;}			
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
					float distance = Vector3.Distance(raycastHit.point, wheelCollider.transform.position);					
					float suspensionTravel = wheelCollider.suspensionDistance;
					float wheelRadius = wheelCollider.radius * part.rescaleFactor;
					if(distance >= suspensionTravel){ distance = suspensionTravel;}
					float compressionAmount = suspensionTravel - distance;
					suspensionParent.position = wheelCollider.transform.position + (wheelCollider.transform.up * compressionAmount) + (wheelCollider.transform.up * suspensionOffset * part.rescaleFactor);
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
			legState = newState;
			persistentState = legState.ToString();
			
			AnimationState anim = deployAnimation[animationName];			
			switch(newState)
			{				
			case LegState.BROKEN:
				enableWheelColliders(false);
				enableFootColliders(false);
				anim.normalizedTime = 0;
				anim.speed = -1;
				deployAnimation.Play(animationName);
				break;
				
			case LegState.DEPLOYED:
				anim.normalizedTime = 1;
				anim.speed = 1;
				if(HighLogic.LoadedSceneIsFlight)
				{
					enableWheelColliders(true);
					enableFootColliders(false);
				}
				break;
				
			case LegState.RETRACTED:	
				enableWheelColliders(false);
				enableFootColliders(false);
				anim.normalizedTime = 0;
				anim.speed = -1;
				break;
				
			case LegState.DEPLOYING:
				enableWheelColliders(false);
				if(HighLogic.LoadedSceneIsFlight)
				{
					enableFootColliders(true);	
				}
				anim.speed = 1;
				resetSuspensionPosition();
				deployAnimation.Play(animationName);
				break;
				
			case LegState.RETRACTING:	
				enableWheelColliders(false);
				enableFootColliders(true);
//				if(oldState==LegState.DEPLOYED)//fix for 'instant retract' bug on newly loaded vessel, replaced by code in OnStart()
//				{
//					anim.normalizedTime = 1;
//				}
				anim.speed = -1;
				resetSuspensionPosition();
				deployAnimation.Play(animationName);
				break;
			}
			updateGuiState();
		}
		
		private void updateGuiState()
		{
			switch(legState)
			{				
			case LegState.BROKEN:
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

