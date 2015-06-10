using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DynamicStorage
{
	
	//Multi-panel solar panel module, each with own suncatcher and pivot
	//Animation code based from stock, Near-Future, and Firespitter code
	//Solar panel code based from stock, Near-Future, and...who knows
	public class SSTUSolarPanel : PartModule
	{
			
		//config field, should contain a single animation name
		[KSPField(isPersistant = false)]
		public String animationName = "unknown";
		
		//config field, should contain CSV of transform names for ray cast checks
		[KSPField(isPersistant = false)]
		public String rayTransforms = string.Empty;
		
		//config field, should contain CSV of pivot names for panels
		[KSPField(isPersistant = false)]
		public String pivotTransforms = string.Empty;
		
		[KSPField(isPersistant = false)]
		public String resourceName = "ElectricCharge";
		
		[KSPField(isPersistant = false)]
		public float resourceAmount = 3.0f;
		
		//config field, sets animation layer to reduce conflicts with stock layers/etc
		[KSPField(isPersistant = false)]
		public int animationLayer = 1;
				
		//used purely to persist rough estimate of animation state; if it is retracting/extending when reloaded, it will default to the start of that animation transition
		//defaults to retracted state for any new/uninitialized parts
		[KSPField(isPersistant = true)]
		public String savedAnimationState = "RETRACTED";

		//Status displayed for panel state, includes animation state and energy state;  Using in place of the three-line output from stock panels
		[KSPField(isPersistant = false, guiName = "Panel Status", guiActive = true)]
		public String guiStatus = "unknown";
		
		[KSPField(isPersistant = false)]
		public FloatCurve powerCurve = new FloatCurve();
		
		//parsed list of pivot transform names, should be same length and order as suncatchers
		private List<String> pivotNames = new List<String>();
		
		//parsed list of suncatching ray transform names
		private List<String> suncatcherNames = new List<String>();
		
		//list of panel data (pivot and ray transform, and cached angles/etc needed for each)
		private List<PanelData> panelData = new List<PanelData>();
		
		//current state of this solar panel module
		private SSTUPanelState panelState = SSTUPanelState.RETRACTED;				
		
		//loaded on part initialization (initialized==true)
		AnimationState[] animationStates;
		
		//is in an active flight scene?  TODO move this to a tools check somewhere, instead of a per-part field
		private bool flight;
		
		//cached energy flow value, used to update gui
		private float energyFlow = 0.0f;
		
		private String occluderName = String.Empty;
		
		private float trackingSpeed = 0.25f;
		
		private Transform sunTransform;
			
		//panel state enum, each represents a discrete state
		public enum SSTUPanelState
		{
			EXTENDED,
			EXTENDING,
			RETRACTED,
			RETRACTING,
			BROKEN,//not sure I should include, but might be good for if someone keeps them deployed in atmosphere... or something
		}
					
		public SSTUSolarPanel ()
		{		
			//base();//super constructor automatically called? (as this doesn't work/isn't valid)
			//yep, i'm lazy and ther is nothing in the constructor;
		}
		
		public override void OnStart (StartState state)
		{
			base.OnStart (state);
			printConfigData();
			initializeAnimations();
			parseTransformData();
			findTransforms();
			
			//set initial animation state
			if(panelState==SSTUPanelState.RETRACTED || panelState==SSTUPanelState.RETRACTING)
			{
				setAnimNormTime(0.0f);
				setAnimSpeed(-1);
			}
			else if(panelState==SSTUPanelState.EXTENDED || panelState==SSTUPanelState.EXTENDING)
			{
				setAnimNormTime(1.0f);
				setAnimSpeed(1);
			}
			
			//and set if in flight or not
			flight = true;
			if(state==StartState.Editor || state==StartState.None)
			{
				flight=false;
			}
			
			//update GUI values and available action buttons
			updateGuiData();			
		}
		
		public override void OnAwake ()
		{
			base.OnAwake ();
		}
		
		public override void OnActive ()
		{
			base.OnActive ();
		}
		
		public override void OnInactive ()
		{
			base.OnInactive ();
		}
		
		public override void OnInitialize ()
		{
			base.OnInitialize ();
		}
			
		public override void OnSave (ConfigNode node)
		{
			base.OnSave (node);
		}
					
		//load saved persistent data... this should really only be the panel status (broken/extend/retract)
		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			loadSavedState(savedAnimationState);
			updateGuiData();
		}
		
		//TODO
		public override string GetInfo ()
		{
			return base.GetInfo ();
		}
		
		//per tick update; checks animation state for completion
		public void Update()
		{
			//TODO -- find best place for this code to run..... fixedUpdate might be appropriate, no clue how often it is ran
			//check animations to see if should toggle state
			//clamp times to 0-1 range for ease of checking
			//find largest time from range
			float animTimeBig = 0;//start at 0, will go up if anything is > 1
			float animTimeSmall = 1;//start at 1, will go down if anything is < 1
			foreach(AnimationState state in animationStates)
			{				
				state.normalizedTime = Mathf.Clamp01(state.normalizedTime);
				animTimeBig = Math.Max(state.normalizedTime, animTimeBig);//clamp animation times to 0-1 range, and find largest time
				animTimeSmall = Math.Min (state.normalizedTime, animTimeSmall);//find smallest time
			}
			if(panelState==SSTUPanelState.EXTENDING && animTimeSmall==1)
			{
				setState(SSTUPanelState.EXTENDED);
			}
			if(panelState==SSTUPanelState.RETRACTING && animTimeBig==0)
			{
				setState(SSTUPanelState.RETRACTED);
			}
			//updating GUI status is done in fixed update....should possibly be done here though?
		}
		
		public void FixedUpdate ()
		{
			updatePowerStatus ();
			updateGuiData();
		}
		
		//KSP Action Group 'Extend Panels' action, will only trigger when panels are actually retracted/ing
		[KSPAction("Extend Panels")]
		public void extendAction()
		{
			if(panelState==SSTUPanelState.RETRACTED || panelState==SSTUPanelState.RETRACTING)
			{
				toggle ();				
			}
		}
		
		//KSP Action Group 'Retract Panels' action, will only trigger when panels are actually extended/ing
		[KSPAction("Retract Panels")]
		public void retractAction()
		{
			if(panelState==SSTUPanelState.EXTENDING || panelState==SSTUPanelState.EXTENDED)
			{
				toggle ();
			}
			
		}
		
		//KSP Action Group 'Toggle Panels' action, will operate regardless of current panel status (except broken)
		[KSPAction("Toggle Panels")]
		public void toggleAction()
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
			setAnimSpeed(1.0f);
			setState (SSTUPanelState.EXTENDING);			
		}
		
		//final retract activation action, sets animation speed and panel state to trigger retraction, additionally resets panel pivot transforms to default orientations when retract is triggered
		private void retract()
		{
			setAnimSpeed(-1.0f);
			setPanelsToDefaultOrientation();
			setState(SSTUPanelState.RETRACTING);
			occluderName = String.Empty;
		}
		
		//TODO
		//update power status every tick if panels are extended (and not broken)
		private void updatePowerStatus()
		{	
			//TODO update power state, raycasts... the whole solar panel bit
			if(panelState!=SSTUPanelState.EXTENDED || !flight){return;}			
			energyFlow = 0.0f;
			occluderName = String.Empty;
			CelestialBody sun = FlightGlobals.Bodies[0];
			sunTransform = sun.transform;
			foreach(PanelData pd in panelData)
			{
				updatePanelRotation(pd);
				updatePanelPower(pd);
				//print ("updated power for panel: "+pd.GetHashCode());
			}
		}	
		
		//based on stock solar panel tracking code, with guesses as to many of the transforms...some things could be inverted; but it seems to work
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
			//check for occlusion below the horizon
			//check for occlusion by other parts	
			//check angle between sun and panel			
			//derive power for that panel from the angle and maximum power; don't think it is linear?
			CelestialBody body = FlightGlobals.currentMainBody;
			bool planetOcclusion = body != FlightGlobals.Bodies[0] && checkSameBodyOcclusion(pd.rayCastTransform);//current body is not the sun and fails occlusion test			
			bool partOcclusion = planetOcclusion || checkPartOcclusion(pd.rayCastTransform);//don't bother with part check if horizon is occluded
			if(!partOcclusion && !planetOcclusion)//if no occlusion, calc angle for power calculations
			{
				float angle = Vector3.Angle(pd.rayCastTransform.forward, sunTransform.position-pd.rayCastTransform.position);
				float energy = Mathf.Clamp01(Mathf.Cos(angle*Mathf.Deg2Rad)) * resourceAmount;
							
				double altitude = FlightGlobals.getAltitudeAtPos(vessel.GetWorldPos3D(), FlightGlobals.Bodies[0]);
				energyFlow += powerCurve.Evaluate((float)altitude) * energy;
				
				part.RequestResource(resourceName, -energyFlow * TimeWarp.fixedDeltaTime);
			}
		}
	
		//called when current body is not the sun, to check if can see the sun
		//I really don't know the math here, based loosely on code from Nertea/Near Future
		private bool checkSameBodyOcclusion(Transform tr)
		{
			CelestialBody sun = FlightGlobals.Bodies[0];			
			CelestialBody body = FlightGlobals.currentMainBody;
			
			Vector3d sunVector = sun.position - part.vessel.GetWorldPos3D();
			Vector3d bodyVector = body.position - part.vessel.GetWorldPos3D();
			
			double bdsq = body.Radius * body.Radius;
			double dot = Vector3d.Dot (sunVector, bodyVector);
			
			if(dot > (bodyVector.sqrMagnitude - bdsq))
			{
				if(((dot*dot)/sunVector.sqrMagnitude) > (bodyVector.sqrMagnitude - bdsq))
				{
					occluderName = body.name;
					return true;
				}	
			}			
			return false;
		}
		
		//TOODO use in conjunction with above method for a slightly more optimized-than-stock panel power update checking sequence
		private bool checkPartOcclusion(Transform tr)
		{
			Transform sunTransform = FlightGlobals.Bodies[0].transform;
			RaycastHit hit;
			if( Physics.Raycast(tr.position, sunTransform.position - tr.position, out hit, 3000f) )
			{		
				occluderName = hit.transform.gameObject.name;
				return true;
			}
			return false;
		}
		
		//set the panel state enum, updates persistent saved state variable and gui data and buttons
		private void setState(SSTUPanelState state)
		{
			panelState = state;
			savedAnimationState = state.ToString();
			updateGuiData();
		}
				
		//sets each panels pivotTransform to its default orientation, to be used on panel retract
		//TODO find a way to not churn new objects
		private void setPanelsToDefaultOrientation()
		{
			foreach(PanelData panel in panelData)
			{
				panel.pivotTransform.localRotation = new Quaternion(panel.defaultOrientation.x, panel.defaultOrientation.y, panel.defaultOrientation.z, panel.defaultOrientation.w);			
			}
		}
		
		//sets normalizedTime for animations to the input value
		private void setAnimNormTime(float time)
		{
			foreach(AnimationState state in animationStates){state.normalizedTime = time;}
		}
		
		//sets animation speed for animations to the input value
		private void setAnimSpeed(float speed)
		{
			foreach(AnimationState state in animationStates){state.speed = speed;}
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
		}
		
		//updates GUI information and buttons from panel state and energy flow values
		//TODO cache/reduce GC churn
		private void updateGuiData()
		{
			guiStatus = panelState.ToString();
			if(energyFlow > 0){guiStatus += " - "+energyFlow+" e/s";}
			if(occluderName.Length>0){guiStatus += " occ: "+occluderName;}
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
		
		//initializes animation state data and triggers parsing of transform names
		private void initializeAnimations()
		{						
			//load all possible animations for the animation name
			Animation[] anims = part.FindModelAnimators(animationName);			
			
			//set up animation data for each animation
			List<AnimationState> states = new List<AnimationState>();
			foreach(Animation animation in anims)
			{
				AnimationState state = animation[animationName];
				state.speed = 0;
				state.enabled = true;
				state.wrapMode = WrapMode.ClampForever;
				animation.Blend(animationName);
				state.layer = animationLayer;
				states.Add (state);				
			}
					
			animationStates = states.ToArray();
		}
		
		private void printConfigData()
		{
			print ("Config Data: ");
			print ("animationName: " + animationName);
			print ("rayTransforms: " + rayTransforms);
			print ("pivotTransforms: " + pivotTransforms);
			print ("guiStatus: "+guiStatus);
			print ("animationStatusString: " +savedAnimationState);
			print ("animationLayer: "+animationLayer);
			print ("EndConfigData");
		}
		
		//parses saved state string into enum state, falls back to retracted status if load fails for any reason
		private void loadSavedState(String state)
		{
			try
			{
				panelState = (SSTUPanelState)Enum.Parse(typeof(SSTUPanelState), savedAnimationState);
			}
			catch
			{
				panelState = SSTUPanelState.RETRACTED;	
			}
			setState(panelState);			
		}
	}
	
	//wrapper class for solar panel data needed on a per-panel bases in a multi-panel setup
	public class PanelData
	{
		public Transform pivotTransform;
		public Transform rayCastTransform;
		public Quaternion defaultOrientation;
		public float angle;
	}
	
	public static class SSTUTools
	{

		
		
	}	
}

