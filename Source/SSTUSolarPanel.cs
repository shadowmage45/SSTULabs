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
		public String windBreakTransformName = string.Empty;
		
		[KSPField(isPersistant = false)]
		public String resourceName = "ElectricCharge";
		
		[KSPField(isPersistant = false)]
		public float resourceAmount = 3.0f;
		
		[KSPField(isPersistant = false)]
		public float windResistance = 30.0f;
		
		[KSPField(isPersistant = false)]
		public bool breakable = true;
		
		[KSPField(isPersistant = false)]
		public bool canDeployShrouded = false;
		
		//config field, sets animation layer to reduce conflicts with stock layers/etc
		[KSPField(isPersistant = false)]
		public int animationLayer = 1;
		
		[KSPField (isPersistant = false)]
		public FloatCurve temperatureEfficCurve;
		
		//BELOW HERE ARE NON-CONFIG EDITABLE FIELDS
				
		//used purely to persist rough estimate of animation state; if it is retracting/extending when reloaded, it will default to the start of that animation transition
		//defaults to retracted state for any new/uninitialized parts
		[KSPField(isPersistant = true)]
		public String savedAnimationState = "RETRACTED";

		//Status displayed for panel state, includes animation state and energy state;  Using in place of the three-line output from stock panels
		[KSPField(isPersistant = false, guiName = "S.P.", guiActive = true)]
		public String guiStatus = "unknown";
				
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
		
		private Transform windBreakTransform;
			
		//panel state enum, each represents a discrete state
		public enum SSTUPanelState
		{
			EXTENDED,
			EXTENDING,
			RETRACTED,
			RETRACTING,
			BROKEN,//not sure I should include, but might be good for if someone keeps them deployed in atmosphere... or something
		}
					
		public SSTUSolarPanel () : base()//call super needed?
		{	
			//the default stock temperatureEfficiencyCurve
			this.temperatureEfficCurve = new FloatCurve ();
			this.temperatureEfficCurve.Add (4f, 1.2f, 0f, -0.0005725837f);
			this.temperatureEfficCurve.Add (300f, 1f, -0.0008277721f, -0.0008277721f);
			this.temperatureEfficCurve.Add (1200f, 0.1f, -0.0003626566f, -0.0003626566f);
			this.temperatureEfficCurve.Add (2500f, 0.01f, 0f, 0f);
		}
		
		public override void OnStart (StartState state)
		{
			base.OnStart (state);
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
			else if(panelState==SSTUPanelState.BROKEN)
			{
				setAnimNormTime(1.0f);
				setAnimSpeed(1);
				breakPanels();
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
		}
		
		//TODO
		public override string GetInfo ()
		{
			return base.GetInfo ();
		}
		
		//per ? tick update; checks animation state for completion
		public void Update()
		{
			//TODO -- find best place for this code to run..... fixedUpdate might be appropriate, no clue how often it is ran
			//check animations to see if should toggle state
			//clamp times to 0-1 range for ease of checking (and proper reverse handling)
			//find largest time from range
			float animTimeBig = 0;//start at 0, will go up if anything is > 1
			float animTimeSmall = 1;//start at 1, will go down if anything is < 1
			foreach(AnimationState state in animationStates)
			{				
				state.normalizedTime = Mathf.Clamp01(state.normalizedTime);//clamp animation times to 0-1 range
				animTimeBig = Math.Max(state.normalizedTime, animTimeBig);//find largest time
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
			//updating GUI status is done in fixed update....should possibly be done here though? what freq does this update run at?
		}
		
		public void FixedUpdate ()
		{
			updatePowerStatus ();
			checkForBreak();
			updateGuiData();
		}
		
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
			if(!canDeployShrouded && base.part.ShieldedFromAirstream)
			{
				print ("Cannot deploy while shielded from airstream!!");
				return;
			}
			setAnimSpeed(1.0f);
			setPanelsToDefaultOrientation();
			setState (SSTUPanelState.EXTENDING);			
		}
		
		//final retract activation action, sets animation speed and panel state to trigger retraction, additionally resets panel pivot transforms to default orientations when retract is triggered
		private void retract()
		{
			setAnimSpeed(-1.0f);
			setPanelsToDefaultOrientation();
			setState(SSTUPanelState.RETRACTING);
			occluderName = String.Empty;
			energyFlow = 0.0f;
		}
		
		//TODO
		//update power status every tick if panels are extended (and not broken)
		private void updatePowerStatus()
		{	
			//TODO update power state, raycasts... the whole solar panel bit		
			energyFlow = 0.0f;
			occluderName = String.Empty;
			if(panelState!=SSTUPanelState.EXTENDED || !flight){return;}	
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
				float distMult = (float)(base.vessel.solarFlux / PhysicsGlobals.SolarLuminosityAtHome);
				
				if(distMult==0 && FlightGlobals.currentMainBody!=null)//vessel.solarFlux == 0, so occluded by a planetary body
				{
					occluderName = FlightGlobals.currentMainBody.name;//just guessing..but might be occluded by the body we are orbiting?
				}
				
				float efficMult = this.temperatureEfficCurve.Evaluate ((float)base.part.temperature);
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
			if(tr==null){return;}//unpossible...but w/e
			float num = Mathf.Abs (Vector3.Dot (base.vessel.srf_velocity.normalized, tr.normalized));
			float num2 = (float)base.vessel.srf_velocity.magnitude * num * (float)base.vessel.atmDensity;
			if (num2 >= this.windResistance)
			{
				breakPanels();
				setState(SSTUPanelState.BROKEN);
			}
		}
		
		private void breakPanels()
		{
			print ("BREAKING SC-B-SM SOLAR PANELS......");
			foreach(PanelData pd in panelData)
			{
				recurseTransforms(pd.pivotTransform, false);
			}
		}
		
		private void repairPanels()
		{
			foreach(PanelData pd in panelData)
			{
				recurseTransforms(pd.pivotTransform, true);
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
		//TODO cache/reduce GC churn
		private void updateGuiData()
		{			
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
		
		//recurse through all children of the input transform, enabling or disabling the renderer component (if present) based on the enable flag
		private void recurseTransforms(Transform tr, bool enableRender)
		{
			if(tr.renderer!=null)
			{
				tr.renderer.enabled = enableRender;				
			}
			for(int i = 0; i < tr.childCount; i++)
			{
				recurseTransforms(tr.GetChild(i), enableRender);
			}
		}		
		//TODO load an index from config and use this method for people with custom games? no clue how multiple solar systems handle body references...
//		private CelestialBody getSun()
//		{
//			return FlightGlobals.Bodies[0];
//		}
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

