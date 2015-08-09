using System;
using UnityEngine;
namespace SSTUTools
{
	public class SSTUAnimateEngineHeat : PartModule
	{

		//amount of 'heat' added per second at full throttle
		[KSPField]
		public float heatOutput = 300;

		//amount of heat dissipated per second, adjusted by the heatDissipationCurve below
		[KSPField]
		public float heatDissipation = 100;

		//point at which the object will begin to glow
		[KSPField]
		public float draperPoint = 400;

		//maximum amount of heat allowed in this engine
		[KSPField]
		public float maxHeat = 2400;

		//curve to adjust heat dissipation; should generally expel heat faster when hotter
		[KSPField]
		public FloatCurve heatDissipationCurve = new FloatCurve();

		//the heat-output curve for an engine, in case it is not linear
		[KSPField]
		public FloatCurve heatAccumulationCurve = new FloatCurve();

		[KSPField]
		public FloatCurve redCurve = new FloatCurve();

		[KSPField]
		public FloatCurve blueCurve = new FloatCurve();

		[KSPField]
		public FloatCurve greenCurve = new FloatCurve();

		[KSPField]
		public int engineModuleIndex;

		[KSPField]
		public String meshName = String.Empty;

		[KSPField(isPersistant = true)]
		public float currentHeat = 0;
		
		int shaderEmissiveID;
		
		private ModuleEngines engineModule;

		private Transform animatedTransform;
		
		private Color emissiveColor = new Color(0f,0f,0f,1f);
		
		public override void OnAwake ()
		{
			base.OnAwake ();
			heatDissipationCurve.Add (0f, 0.2f);
			heatDissipationCurve.Add (1f, 1f);

			heatAccumulationCurve.Add (0f, 0f);
			heatAccumulationCurve.Add (1f, 1f);

			redCurve.Add (0f, 0f);
			redCurve.Add (1f, 1f);

			blueCurve.Add (0f, 0f);
			blueCurve.Add (1f, 1f);

			greenCurve.Add (0f, 0f);
			greenCurve.Add (1f, 1f);
			
			shaderEmissiveID = Shader.PropertyToID("_EmissiveColor");
		}
		
		public override void OnStart (StartState state)
		{
			base.OnStart (state);
			animatedTransform = part.FindModelTransform (meshName);
			if(animatedTransform==null){print ("ERROR: Could not locate transform for name: "+meshName);}
			locateEngineModule ();
		}

		public void FixedUpdate()
		{
			if(!HighLogic.LoadedSceneIsFlight){return;}			
			updateHeat ();
		}

		private void locateEngineModule()
		{
			engineModule = null;
			if (engineModuleIndex < part.Modules.Count)
			{
				ModuleEngines engine = part.Modules[engineModuleIndex] as ModuleEngines;
				if(engine!=null)
				{
					engineModule = engine;
				}
			}
			if (engineModule == null)
			{
				print ("ERROR!  SSTUAnimateHeat could not locate engine module at index: "+engineModuleIndex);
			}
		}

		private void updateHeat()
		{		
			//add heat from engine
			if (engineModule.EngineIgnited && !engineModule.flameout && engineModule.currentThrottle>0)
			{
				float throttle = vessel.ctrlState.mainThrottle;				
				float heatIn = heatAccumulationCurve.Evaluate(throttle) * heatOutput * TimeWarp.fixedDeltaTime;
				currentHeat += heatIn;		
			}
			
			//dissipate heat
			float heatPercent = currentHeat / maxHeat;
			if(heatPercent>1f){heatPercent=1f;}
			if (currentHeat > 0f)
			{
				float heatOut = heatDissipationCurve.Evaluate(heatPercent) * heatDissipation * TimeWarp.fixedDeltaTime;
				if(heatOut>currentHeat){heatOut = currentHeat;}
				currentHeat -= heatOut;
			}
			
			float emissivePercent = 0f;
			
			float mhd = maxHeat - draperPoint;	
			float chd = currentHeat - draperPoint;
			
			if(chd<0f){chd = 0f;}		
			emissivePercent = chd / mhd;
			if(emissivePercent>1f){emissivePercent=1f;}
			emissiveColor.r = redCurve.Evaluate (emissivePercent);
			emissiveColor.g = greenCurve.Evaluate (emissivePercent);
			emissiveColor.b = blueCurve.Evaluate (emissivePercent);
			setEmissiveColors();
		}

		private void setEmissiveColors()
		{			
			if(animatedTransform!=null)
			{				
				setTransfromEmissive(animatedTransform, emissiveColor);				
			}
		}
		
		private void setTransfromEmissive(Transform tr, Color color)
		{
			if(tr.renderer!=null)
			{
				tr.renderer.material.SetColor(shaderEmissiveID, color);
			}
			int len = tr.childCount;
			for(int i = 0; i < len; i++)			
			{
				setTransfromEmissive(tr.GetChild(i), color);
			}
		}
		
	}
}

