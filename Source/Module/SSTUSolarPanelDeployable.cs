using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace SSTUTools
{
	
	//Multi-panel solar panel module, each with own suncatcher and pivot and occlusion checks
	//Animation code based from stock, Near-Future, and Firespitter code
	//Solar panel code based from stock code
	public class SSTUSolarPanelDeployable : SSTUControlledModule
	{
		//panel state enum, each represents a discrete state
		public enum SSTUPanelState
		{
			EXTENDED,
			EXTENDING,
			RETRACTED,
			RETRACTING,
			BROKEN,
		}
		
		public class PanelData
		{
			public Transform pivotTransform;
			public Transform rayCastTransform;
			public Quaternion defaultOrientation;
			public float angle;
		}
		
		//config field, should contain CSV of transform names for ray cast checks
		[KSPField]
		public String rayTransforms = String.Empty;
		
		//config field, should contain CSV of pivot names for panels
		[KSPField]
		public String pivotTransforms = String.Empty;
		
		[KSPField]
		public String windBreakTransformName = String.Empty;
		
		[KSPField]
		public String resourceName = "ElectricCharge";
		
		[KSPField]
		public float resourceAmount = 3.0f;
		
		[KSPField]
		public float windResistance = 30.0f;
		
		[KSPField]
		public bool breakable = true;
		
		[KSPField]
		public bool canDeployShrouded = false;
		
		[KSPField]
		public float trackingSpeed = 0.25f;
		
		[KSPField]
		public int animationID = 0;	
		
		[KSPField]
		public FloatCurve temperatureEfficCurve;
		
		//BELOW HERE ARE NON-CONFIG EDITABLE FIELDS
				
		//used purely to persist rough estimate of animation state; if it is retracting/extending when reloaded, it will default to the start of that animation transition
		//defaults to retracted state for any new/uninitialized parts
		[KSPField(isPersistant = true)]
		public String savedAnimationState = "RETRACTED";

		//Status displayed for panel state, includes animation state and energy state;  Using in place of the three-line output from stock panels
		[KSPField(guiName = "S.P.", guiActive = true)]
		public String guiStatus = String.Empty;
				
		//parsed list of pivot transform names, should be same length and order as suncatchers
		private List<String> pivotNames = new List<String>();
		
		//parsed list of suncatching ray transform names
		private List<String> suncatcherNames = new List<String>();
		
		//list of panel data (pivot and ray transform, and cached angles/etc needed for each)
		private List<PanelData> panelData = new List<PanelData>();
		
		//current state of this solar panel module
		private SSTUPanelState panelState = SSTUPanelState.RETRACTED;			

		//cached energy flow value, used to update gui
		private float energyFlow = 0.0f;
		
		private String occluderName = String.Empty;
				
		private Transform sunTransform;
		
		private Transform windBreakTransform;
			
		private SSTUAnimateControlled animationController;
		
		private bool initialized = false;
			
		//KSP Action Group 'Extend Panels' action, will only trigger when panels are actually retracted/ing
		[KSPAction("Extend Panels")]
		public void extendAction(KSPActionParam param)
		{
			if(panelState==SSTUPanelState.RETRACTED || panelState==SSTUPanelState.RETRACTING)
			{
				toggle ();				
			}
		}
		
		//KSP Action Group 'Retract Panels' action, will only trigger when panels are actually extended/ing
		[KSPAction("Retract Panels")]
		public void retractAction(KSPActionParam param)
		{
			if(panelState==SSTUPanelState.EXTENDING || panelState==SSTUPanelState.EXTENDED)
			{
				toggle ();
			}			
		}
		
		//KSP Action Group 'Toggle Panels' action, will operate regardless of current panel status (except broken)
		[KSPAction("Toggle Panels")]
		public void toggleAction(KSPActionParam param)
		{
			toggle ();
		}
				
		[KSPEvent (name= "extendEvent", guiName = "Extend Panels", guiActiveUnfocused = true, externalToEVAOnly = true, guiActive = true, unfocusedRange = 4f, guiActiveEditor = true)]
		public void extendEvent()
		{
			toggle ();
		}
		
		[KSPEvent (name= "retractEvent", guiName = "Retract Panels", guiActiveUnfocused = true, externalToEVAOnly = true, guiActive = true, unfocusedRange = 4f, guiActiveEditor = true)]
		public void retractEvent()
		{
			toggle ();
		}
				
		public override void OnStart (StartState state)
		{
			base.OnStart (state);
			initializeState();
		}
		
		public override void OnAwake ()
		{			
			base.OnAwake ();
			this.temperatureEfficCurve = new FloatCurve ();
			this.temperatureEfficCurve.Add (4f, 1.2f, 0f, -0.0005725837f);
			this.temperatureEfficCurve.Add (300f, 1f, -0.0008277721f, -0.0008277721f);
			this.temperatureEfficCurve.Add (1200f, 0.1f, -0.0003626566f, -0.0003626566f);
			this.temperatureEfficCurve.Add (2500f, 0.01f, 0f, 0f);
		}
					
		//load saved persistent data... this should really only be the panel status (broken/extend/retract)
		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			try
			{
				panelState = (SSTUPanelState)Enum.Parse(typeof(SSTUPanelState), savedAnimationState);
			}
			catch
			{
				panelState = SSTUPanelState.RETRACTED;	
			}
			initializeState();
		}
		
		//TODO
		public override string GetInfo ()
		{
			return base.GetInfo ();
		}
		
		public void FixedUpdate ()
		{
			if (!moduleControlEnabled)
			{
				return;
			}
			energyFlow = 0.0f;
			occluderName = String.Empty;
			if(panelState==SSTUPanelState.EXTENDED && HighLogic.LoadedSceneIsFlight)
			{
				updatePowerStatus ();
				checkForBreak();				
			}
			updateGuiData();
		}
		
		public void onAnimationStatusChanged(SSTUAnimState state)
		{
			if(state==SSTUAnimState.STOPPED_END)
			{
				setPanelState(SSTUPanelState.EXTENDED);
			}
			else if(state==SSTUAnimState.STOPPED_START)
			{
				setPanelState(SSTUPanelState.RETRACTED);
			}
		}
		
		public override void updateGuiControlsFromState (bool enabled)
		{
			updateGuiData();
		}
		
		private void initializeState()
		{	
			if(initialized){return;}
			initialized = true;			
			parseTransformData();
			findTransforms();
			animationController = SSTUAnimateControlled.locateAnimationController (part, animationID, onAnimationStatusChanged);
			setPanelState(panelState);
			updateGuiData();
		}
		
		//triggers retract or deploy final action depending upon which is needed
		private void toggle()
		{		
			if(panelState==SSTUPanelState.BROKEN)
			{
				//noop, broken panel... print message?
			}
			else if(panelState==SSTUPanelState.EXTENDING || panelState==SSTUPanelState.EXTENDED)
			{	
				retract ();
			}
			else //must be retracting or retracted
			{
				deploy ();
			}
		}
		
		//final deploy activation action, sets animation speed and panel state to trigger deployment
		private void deploy()
		{	
			if(!canDeployShrouded && part.ShieldedFromAirstream)
			{
				print ("Cannot deploy while shielded from airstream!!");
				return;
			}			
			setPanelState (SSTUPanelState.EXTENDING);			
		}
		
		//final retract activation action, sets animation speed and panel state to trigger retraction, additionally resets panel pivot transforms to default orientations when retract is triggered
		private void retract()
		{
			setPanelState(SSTUPanelState.RETRACTING);		
		}
	
		//update power status every tick if panels are extended (and not broken)
		private void updatePowerStatus()
		{	
			//TODO update power state, raycasts... the whole solar panel bit		
			energyFlow = 0.0f;
			occluderName = String.Empty;
			CelestialBody sun = FlightGlobals.Bodies[0];
			sunTransform = sun.transform;
			foreach(PanelData pd in panelData)
			{
				updatePanelRotation(pd);
				updatePanelPower(pd);
			}		
			if(energyFlow>0)
			{				
				part.RequestResource (resourceName, -energyFlow);
			}
		}	
		
		//based on stock solar panel sun tracking code
		private void updatePanelRotation(PanelData pd)
		{
			//vector from pivot to sun
			Vector3 vector = pd.pivotTransform.InverseTransformPoint (sunTransform.position);		
			
			//finding angle to turn towards based on direction of vector on a single axis
			float y = Mathf.Atan2 (vector.x, vector.z) * 57.29578f;		
			
			//lerp towards destination rotation by trackingSpeed amount		
			Quaternion to = pd.pivotTransform.rotation * Quaternion.Euler (0f, y, 0f);	
			pd.pivotTransform.rotation = Quaternion.Lerp (pd.pivotTransform.rotation, to, TimeWarp.deltaTime * this.trackingSpeed);
		}	
		
		private void updatePanelPower(PanelData pd)
		{			
			if(!isOccludedByPart(pd.rayCastTransform))
			{
				Vector3 normalized = (sunTransform.position - pd.rayCastTransform.position).normalized;
				
				float sunAOA = Mathf.Clamp (Vector3.Dot (pd.rayCastTransform.forward, normalized), 0f, 1f);
				float distMult = (float)(vessel.solarFlux / PhysicsGlobals.SolarLuminosityAtHome);
				
				if(distMult==0 && FlightGlobals.currentMainBody!=null)//vessel.solarFlux == 0, so occluded by a planetary body
				{
					occluderName = FlightGlobals.currentMainBody.name;//just guessing..but might be occluded by the body we are orbiting?
				}
				
				float efficMult = temperatureEfficCurve.Evaluate ((float)part.temperature);
				float panelEnergy = resourceAmount * TimeWarp.fixedDeltaTime * sunAOA * distMult * efficMult;
				energyFlow += panelEnergy;
			}
		}
		
		//does a very short raycast for vessel/part occlusion checking
		//rely on stock thermal input data for body occlusion checks
		private bool isOccludedByPart(Transform tr)
		{
			RaycastHit hit;
			if( Physics.Raycast(tr.position, (sunTransform.position - tr.position).normalized, out hit, 300f) )
			{		
				occluderName = hit.transform.gameObject.name;						
				return true;
			}
			return false;	
		}
		
		//set the panel state enum, updates persistent saved state variable and gui data and buttons
		private void setPanelState(SSTUPanelState newState)
		{
			SSTUPanelState oldState = panelState;
			panelState = newState;
			switch(newState)
			{
				
			case SSTUPanelState.BROKEN:
			{
				setAnimationState(SSTUAnimState.STOPPED_END);
				breakPanels();
				break;
			}
				
			case SSTUPanelState.EXTENDED:
			{
				setAnimationState(SSTUAnimState.STOPPED_END);
				break;
			}
				
			case SSTUPanelState.EXTENDING:
			{
				setAnimationState(SSTUAnimState.PLAYING_FORWARD);
				setPanelsToDefaultOrientation();
				break;
			}
				
			case SSTUPanelState.RETRACTED:
			{
				setAnimationState(SSTUAnimState.STOPPED_START);
				break;
			}
				
			case SSTUPanelState.RETRACTING:
			{
				setAnimationState(SSTUAnimState.PLAYING_BACKWARD);
				setPanelsToDefaultOrientation();								
				break;
			}
				
			}	
			
			energyFlow = 0.0f;
			savedAnimationState = panelState.ToString();
			updateGuiData();
		}
		
		private void setAnimationState(SSTUAnimState state)
		{
			if(animationController!=null){animationController.setToState(state);}
		}
				
		//sets each panels pivotTransform to its default orientation, to be used on panel retract
		private void setPanelsToDefaultOrientation()
		{
			foreach(PanelData panel in panelData)
			{
				panel.pivotTransform.localRotation = new Quaternion(panel.defaultOrientation.x, panel.defaultOrientation.y, panel.defaultOrientation.z, panel.defaultOrientation.w);			
			}
		}
		
		private void checkForBreak()
		{
			if( !breakable || panelState==SSTUPanelState.BROKEN || vessel==null || vessel.packed )
			{
				return;//noop
			}
			Vector3 tr;
			if(windBreakTransform!=null)
			{
				tr = windBreakTransform.forward;
			}			
			else
			{
				tr = vessel.transform.up;
			}
			float num = Mathf.Abs (Vector3.Dot (base.vessel.srf_velocity.normalized, tr.normalized));
			float num2 = (float)base.vessel.srf_velocity.magnitude * num * (float)base.vessel.atmDensity;
			if (num2 >= this.windResistance)
			{
				setPanelState(SSTUPanelState.BROKEN);
			}
		}
		
		private void breakPanels()
		{
			foreach(PanelData pd in panelData)
			{
				SSTUUtils.enableRenderRecursive(pd.pivotTransform, false);				
			}
		}
		
		private void repairPanels()
		{
			foreach(PanelData pd in panelData)
			{
				SSTUUtils.enableRenderRecursive(pd.pivotTransform, true);
			}
		}
			
		//parses the rayTransforms and pivotTransforms names into lists
		private void parseTransformData()		
		{
			suncatcherNames.Clear();
			pivotNames.Clear();			
			String[] suncatcherNamesTempArray = rayTransforms.Split(',');			
			for(int i = 0; i < suncatcherNamesTempArray.Length; i++){suncatcherNames.Add(suncatcherNamesTempArray[i].Trim());}
			String[] pivotNamesTempArray = pivotTransforms.Split(',');
			for(int i = 0; i < pivotNamesTempArray.Length; i++){pivotNames.Add(pivotNamesTempArray[i].Trim());}			
		}
		
		//loads transforms from model given the transform names specified in config
		//TODO rework to remove the lists, no need to keep names afterwards
		private void findTransforms()
		{
			panelData.Clear();
			
			if(pivotNames.Count!=suncatcherNames.Count)
			{
				print ("pivot and suncatcher names length not equal, error!");
				return;
			}	
			
			PanelData pd;
			String pn, sn;
			Transform t1, t2;
			int length = pivotNames.Count;//lists -should- be the same size, or there will be problems
			for(int i = 0; i < length; i++)
			{
				pn = pivotNames[i];
				sn = suncatcherNames[i];
				t1 = part.FindModelTransform(pn);
				t2 = part.FindModelTransform(sn);
				if(t1==null || t2==null)
				{
					print ("null transform found for names.. "+pn+" :: "+sn);
					continue;
				}
				pd = new PanelData();
				pd.pivotTransform = t1;
				pd.rayCastTransform = t2;
				pd.angle = 0;
				pd.defaultOrientation = new Quaternion(t1.localRotation.x, t1.localRotation.y, t1.localRotation.z, t1.localRotation.w);//set a copy of original rotation, to restore when panels are retracted
				panelData.Add (pd);
			}
			
			if(windBreakTransformName!=null && windBreakTransformName.Length>0)
			{
				t1 = part.FindModelTransform(windBreakTransformName);
				if(t1!=null)
				{
					windBreakTransform = t1;
				}
			}
			if(windBreakTransform==null && panelData.Count>0)
			{
				windBreakTransform = panelData[0].pivotTransform;
			}//else it will use default vessel transform
		}
		
		//updates GUI information and buttons from panel state and energy flow values
		//TODO clean up this mess....
		private void updateGuiData()
		{			
			if (!moduleControlEnabled)
			{				
				Events["extendEvent"].active=false;
				Events["retractEvent"].active=false;
				Fields["guiStatus"].guiActive = false;
				return;
			}
			if(energyFlow==0 && occluderName.Length>0)//if occluded, state that information first
			{
				guiStatus = "OCC: "+occluderName;
			}
			else
			{
				guiStatus = panelState.ToString();
				if(energyFlow > 0)
				{
					guiStatus += " : "+String.Format("{0:F1}", (energyFlow* (1/TimeWarp.fixedDeltaTime)))+" e/s";
				}	
			}			
			if(panelState==SSTUPanelState.BROKEN)			
			{
				Events["extendEvent"].active=false;
				Events["retractEvent"].active=false;
			}
			else if(panelState==SSTUPanelState.EXTENDING || panelState==SSTUPanelState.EXTENDED)
			{
				Events["extendEvent"].active = false;	
				Events["retractEvent"].active = true;	
			}
			else//
			{
				Events["extendEvent"].active = true;	
				Events["retractEvent"].active = false;	
			}
		}
	}
	
}

