using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace SSTUTools
{

	/// <summary>
	/// SSTU generator.
	/// Multipurpose generic generator/converter module.
	/// May have multiple inputs and/or outputs.
	/// May optionally use an engine module throttle to determine actual output percentage (as an alternator).
	/// </summary>

	//should make it a generic converter with delta-catchup and optional inputs or ?
	//should make it also function as an alternator module, generating EC from throttle for engine on the part?
	public class SSTUConverter : PartModule, IControlledModule
	{
		
		//  if >=0, module will attempt to look for a stock-derived engine at the given index
		//  and use its throttle input to determine amount of output.
		//  To be used in place of the barely-functional stock ModuleAlternator
		[KSPField]
		public int engineModuleIndex = -1;//disabled by default

		//  the last game-universe time for the last processed update, set to 0 anytime the converter is turned off
		//  will check for zero when processing, and if zero will process a single full tick and set the last time
		//  to the processing time.  From then on processing will happen for the length of time between last update and
		//  current time.
		[KSPField(isPersistant=true)]
		public double lastUpdateTime = 0;

		//if true, half-life based decay will be used to adjust output percentage (also effects input percent used, efficiency remains the same)
		[KSPField]
		public bool halfLifeDecay;

		//the half-life time for this converter; to be used by RTGs or similar
		[KSPField]
		public double halfLife;

		//used when half-life is enabled, to reduce converter output amount
		//used to calculate the time difference between start and current time
		//to determine the actual half-life based output of the converter
		[KSPField(isPersistant=true)]
		public double baseStartTime = -1;
		
		//TODO
		[KSPField]
		public String specialistType = String.Empty;
		//TODO				
		[KSPField]
		public float specialistBonus = 0.0f;

		[KSPField]
		public bool canDisable = true;

		[KSPField(isPersistant=true)]
		public bool isDisabled = false;
		
		//TODO
		[KSPField]
		public float heatOutput = 0;

		//IControlledModule fields
		[KSPField(isPersistant=true)]
		public bool moduleControlEnabled = false;

		//IControlledModule fields
		[KSPField]
		public int controlID = -1;
		
		[Persistent]
		public String configNodeString = String.Empty;
										
		private ConverterRecipe recipe;
		
		private ModuleEngines engineModule;
		
		[KSPEvent(guiActive=true, guiName="enableConverter")]
		public void enableConverter()
		{
			lastUpdateTime = 0;
			isDisabled = false;
			updateGuiState();
		}
		
		[KSPEvent(guiActive=true, guiName="disableConverter")]
		public void disableConverter()
		{
			isDisabled = true;
			lastUpdateTime = 0;
			updateGuiState();
		}
		
		public override void OnLoad(ConfigNode node)
		{
			if(node.HasNode("CONVERTERRECIPE"))//load the recipe config for the prefab part instance;
			{
				loadRecipeFromNode(node);
				configNodeString = node.ToString();				
			}
		}

		public override void OnStart (StartState state)
		{
			base.OnStart (state);			
			if (controlID == -1)
			{
				moduleControlEnabled = true;
			}
			loadRecipeFromSavedConfigString ();
			if (recipe == null)
			{
				print ("ERROR - recipe was not loaded from prefab; nothing to process!");
				//will NRE shortly after this...but at least I know why...
			}
			if (HighLogic.LoadedSceneIsFlight && baseStartTime == -1)//set initial start time if uninitialized and in flight
			{
				baseStartTime = Planetarium.GetUniversalTime();
			}
			if (engineModuleIndex >= 0 && engineModuleIndex < part.Modules.Count)//locate engine module, if applicable
			{
				ModuleEngines me = part.Modules[engineModuleIndex] as ModuleEngines;
				if(me!=null)
				{
					engineModule = me;
				}
			}
			if(!canDisable){isDisabled=false;}
			updateGuiState();
		}
		
		private void loadRecipeFromNode(ConfigNode node)
		{
			if (node.HasNode ("CONVERTERRECIPE"))
			{
				recipe = new ConverterRecipe ().loadFromNode(node.GetNode ("CONVERTERRECIPE"));
			}
			else
			{
				print ("ERROR - Config node contains no recipe node to load converter recipe from!");
			}			
		}

		private void loadRecipeFromSavedConfigString()
		{
			ConfigNode moduleNode = SSTUNodeUtils.parseConfigNode(configNodeString);
			loadRecipeFromNode(moduleNode);
		}

		//unity fixedUpdate; for processing of the recipe in relation to time-delta
		public void FixedUpdate()
		{
			if (moduleControlEnabled && !isDisabled && HighLogic.LoadedSceneIsFlight)
			{
				processRecipe();
			}
		}	

		//IControlledModule method
		public void enableModule ()
		{
			moduleControlEnabled = true;
			if (!isDisabled)
			{
				enableConverter();
			}
			updateGuiState ();
		}

		//IControlledModule method
		public void disableModule ()
		{
			moduleControlEnabled = false;
			disableConverter ();
			updateGuiState ();
		}
		
		//IControlledModule method
		public bool isControlEnabled ()
		{
			return moduleControlEnabled;
		}
		
		//IControlledModule method
		public int getControlID ()
		{
			return controlID;
		}
		
		private void processRecipe()
		{		
			float time = getProcessTime ();
			float percent = getProcessPercent ();
			recipe.process(part, percent, time);
		}

		private float getProcessTime()
		{
			double currentTime = Planetarium.GetUniversalTime ();
			print ("ct: "+currentTime+" lt: "+lastUpdateTime);
			if (lastUpdateTime == 0)//initialize time
			{
				lastUpdateTime = currentTime;
				return TimeWarp.fixedDeltaTime;
			}
			else
			{	
				float maxProcessTime = 100f;//one hundred seconds...
				double returnVal = currentTime - lastUpdateTime;//raw elapsed time
				if (returnVal > maxProcessTime)
				{
					lastUpdateTime += maxProcessTime;//don't fully process if interval > maxProcessTime; let it catch up on future ticks (if/when timewarp stops)
					returnVal = maxProcessTime;
				}
				else
				{
					lastUpdateTime = currentTime;
				}
				return (float)returnVal;
			}
		}

		private double getElapsedTimeFromStart()
		{
			return  lastUpdateTime - baseStartTime;
		}

		private float getProcessPercent()
		{
			float percent = 1f;
			if (halfLifeDecay)
			{
				//actual equation is:	//output = baseInput / (2 ^ (elapsedTime/half-life))
				double time = getElapsedTimeFromStart();
				double timeQuotient = time/halfLife;
				double timePow = Math.Pow (2, timeQuotient);
				percent *= 1f / (float)timePow;
				//print ("process percent based on halfLife "+percent);
			}
			if (engineModule!=null)
			{
				if(engineModule.currentThrottle>0 && engineModule.EngineIgnited)
				{
					percent *= engineModule.currentThrottle;
				}
			}
			if (specialistType!=null && specialistType.Length > 0 && specialistBonus > 0)
			{
				float specialistLevel = vesselContainsSpecialist(specialistType);
				percent = percent + (specialistBonus * specialistLevel * percent);
			}
			return percent;
		}

		//TODO
		//return level of specialist, if any;
		//should return crew.level + 1 for the highest-level crew of the input type, or 0 if no crew of type was found
		private float vesselContainsSpecialist(String type)
		{
			return 0;
		}

		//TODO update gui status fields/buttons/etc based on current module state (enabled, active, if should display data/etc)
		private void updateGuiState()
		{
			if(!moduleControlEnabled)
			{
				Events["disableConverter"].active=false;
				Events["enableConverter"].active=false;
			}
			else if(isDisabled)
			{
				Events["disableConverter"].active=false;
				Events["enableConverter"].active=true;
			}
			else
			{
				Events["disableConverter"].active=canDisable;
				Events["enableConverter"].active=false;
			}
		}
	}
}

