using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	
	//mesh switch module
	//driven by config node system, can be linked to resource switch through config specification
	//TODO
	// how to allow for setting of default mesh AND resources during prefab part instantiation?
	// -- have an externally accessible var/method in SSTUResourceSwitch that accepts a tankName for default instantiation?
	public class SSTUMeshSwitch : PartModule, IPartCostModifier, IPartMassModifier
	{
		//used to suffix the part-name in order to store persistent config data in static dictionary
		[KSPField]
		public int moduleID = 0;
					
		[KSPField]
		public String defaultVariantName;
		
		[KSPField(isPersistant=true)]
		public int currentConfiguration = -1;
		
		[KSPField(guiActive=true, guiActiveEditor = true, guiName = "Variant")]
		public String meshDisplayName = String.Empty;
		
		[KSPField]
		public String variantLabel = "Variant";
		
		[KSPField]
		public bool modifyPartMass = false;

		[KSPField]
		public bool controlsTankOptions = false;

		[KSPField]
		public bool enablePrevButton = true;


		[Persistent]
		public String configNodeString;

		//current mesh configuration data
		private MeshConfig[] meshConfigurations;
		private MeshConfig currentConfig;
		
		//linked resource switch module, if any
		private SSTUResourceSwitch resourceSwitch;
		private SSTUModuleControl moduleControl;
					
		[KSPEvent(name="nextMeshEvent", guiName="Next Variant", guiActiveEditor=true)]
		public void nextMeshEvent()
		{
			currentConfiguration++;
			if(currentConfiguration>=meshConfigurations.Length){currentConfiguration=0;}
			setToMeshConfig(currentConfiguration);
			updateResourceSwitch();
			updateModuleSwitch();
			GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}	
		
		[KSPEvent(name="prevMeshEvent", guiName="Prev. Variant", guiActiveEditor=true)]
		public void prevMeshEvent()
		{
			currentConfiguration--;
			if(currentConfiguration<0){currentConfiguration = meshConfigurations.Length - 1;}
			setToMeshConfig(currentConfiguration);
			updateResourceSwitch();
			updateModuleSwitch();
			GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}	
		
		#region KSP Overrides
				
		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);									
			if(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
			{				
				initialize();
			}
			else
			{
				configNodeString = node.ToString();
				onPrefabLoad(node);//only occurs on database load (loading screen, reload on space-center screen)		
			}
		}
		
		public override void OnStart (PartModule.StartState state)
		{
			base.OnStart (state);			
			initialize();
			Events["nextMeshEvent"].guiName = "Next "+variantLabel;
			Events["prevMeshEvent"].guiName = "Prev. "+variantLabel;
			Events["prevMeshEvent"].active = enablePrevButton;
			Fields["meshDisplayName"].guiName = variantLabel;
		}
		
		public override string GetInfo ()
		{
			print ("SSTUMeshSwitch GetInfo "+GetHashCode());
			return base.GetInfo ();
		}
		
		public float GetModuleCost (float defaultCost)
		{
			return currentConfig==null? 0 : currentConfig.variantCost;
		}
		
		public float GetModuleMass (float defaultMass)
		{
			return modifyPartMass ? 0 : (currentConfig==null? 0 : currentConfig.variantMass);
		}
		
		#endregion
		
		private void initialize()
		{
			findLinkedModules();
			loadConfigFromPrefab();
			reloadSavedMeshData();		
		}
				
		/// <summary>
		/// To be called by the prefab part only during initialization and/or database reload.
		/// Sets the current active mesh to the specified default from the part config file (or first found if no default specified)
		/// </summary>
		/// <param name="node">Node.</param>
		private void onPrefabLoad(ConfigNode node)
		{		
			loadConfigFromNode(node);
			loadDefaultMeshConfig();
		}
		
		private void loadConfigFromPrefab()
		{
			loadConfigFromNode (SSTUNodeUtils.parseConfigNode (configNodeString));
		}
		
		private void loadConfigFromNode(ConfigNode node)
		{
			ConfigNode[] variantNodes = node.GetNodes("MESHVARIANT");
			MeshConfig[] cfgs = new MeshConfig[variantNodes.Length];
			for(int i = 0; i < cfgs.Length; i++)
			{
				cfgs[i] = new MeshConfig(variantNodes[i], part);
			}
			meshConfigurations = cfgs;
		}

		private void findLinkedModules()
		{
			moduleControl = part.GetComponent<SSTUModuleControl>();
			resourceSwitch = part.GetComponent<SSTUResourceSwitch>();
		}
		
		private void updateResourceSwitch()
		{
			if(resourceSwitch==null){findLinkedModules();}
			if(resourceSwitch!=null)
			{
				MeshConfig config = meshConfigurations[currentConfiguration];
				if(config!=null && config.tankName!=null && config.tankName.Length>0)
				{					
					resourceSwitch.setTankMainConfig(config.tankName);	
				}
				if(controlsTankOptions)
				{
					if(config.tankOption!=null && config.tankOption.Length>0)
					{
						resourceSwitch.setTankOption(config.tankOption);
					}
					else
					{
						resourceSwitch.clearTankOption();
					}
				}
			}
		}

		private void updateModuleSwitch()
		{
			if(moduleControl==null){findLinkedModules ();}
			if(moduleControl!=null)
			{
				int len = meshConfigurations.Length;
				MeshConfig cfg;
				int[] modules;
				for(int i = 0; i < len; i++)
				{
					cfg = meshConfigurations[i];
					modules = cfg.controlledModules;
					for(int k = 0; k < modules.Length; k++)
					{
						if(i==currentConfiguration)
						{
							print ("enabling module: "+modules[k]);
							moduleControl.enableControlledModule(modules[k]);
						}
						else
						{
							print ("disabling module: "+modules[k]);
							moduleControl.disableControlledModule(modules[k]);
						}
					}
				}
			}
		}
		
		private void enableAllMeshes()
		{
			foreach(MeshConfig cfg in meshConfigurations)
			{
				cfg.enable();
			}
		}

		private void reloadSavedMeshData()
		{
			setToMeshConfig (currentConfiguration);
		}

		private void loadDefaultMeshConfig()
		{
			setToMeshConfig (defaultVariantName);
		}
		
		private void setToMeshConfig(String variantName)
		{
			if(variantName==null || variantName.Length==0)
			{
				setToMeshConfig(0);
			}
			int len = meshConfigurations.Length;
			for(int i = 0; i < len; i++)
			{
				if(meshConfigurations[i].variantName.Equals(variantName))
				{
					setToMeshConfig(i);
					return;
				}
			}
			setToMeshConfig(0);
		}
		
		private void setToMeshConfig(int index)
		{
			if(meshConfigurations==null){print ("ERROR: NO MESH CONFIG FOR PART: "+part);}
			if(index < 0 || index >= meshConfigurations.Length){index = 0;}
			currentConfiguration = index;
			
			MeshConfig config = meshConfigurations[index];
			meshDisplayName = config.variantName;
			currentConfig = config;
			
			int len = meshConfigurations.Length;
			for(int i = 0; i < len; i++)
			{
				if(i==index){continue;}
				meshConfigurations[i].disable();
			}
			currentConfig.enable();						
			
			if(modifyPartMass)
			{
				part.mass = config.variantMass;
			}
		}
		
		private void printConfig()
		{
			print ("MeshSwitch Config:");
			print ("defaultVariantName: "+defaultVariantName);
			print ("moduleID: "+moduleID);
			print ("currentConfiguration: "+currentConfiguration);
			print ("meshDisplayName: "+meshDisplayName);
			print ("Configs: "+SSTUUtils.printArray(meshConfigurations, "\n"));
			print ("-----end config---");
		}
	
	}
	
	//single part variant configuration
	public class MeshConfig
	{
		public String variantName = String.Empty;
		public String tankName = String.Empty;
		public String tankOption = String.Empty;
		public MeshData[] meshData;
		public int[] controlledModules;
		public float variantMass = 0;
		public float variantCost = 0;
		
		public MeshConfig(ConfigNode node, Part part)
		{
			variantName = node.GetValue("variantName");
			if(variantName==null || variantName.Length==0)
			{
				MonoBehaviour.print ("ILLEGAL VARIANT NAME: "+variantName);
			}
			if(node.HasValue("tankName"))//else it ends up null?
			{			
				tankName = node.GetValue("tankName");
			}
			if(node.HasValue("variantMass"))
			{
				variantMass = (float)SSTUUtils.safeParseDouble(node.GetValue("variantMass"));
			}
			if(node.HasValue("variantCost"))
			{
				variantCost = (float)SSTUUtils.safeParseDouble(node.GetValue("variantCost"));
			}	
			if (node.HasValue ("tankOption"))
			{
				tankOption = node.GetValue("tankOption");
			}
			String meshNames = node.GetValue("meshNames");	
			if(meshNames==null || meshNames.Length==0)
			{
				meshData = new MeshData[0];
			}
			else
			{
				String[] splitNames = meshNames.Split(',');
				meshData = new MeshData[splitNames.Length];
				int len = splitNames.Length;
				for(int i = 0; i < len; i++)
				{
					meshData[i] = new MeshData(splitNames[i].Trim (), part);
				}				
			}			
			String configControlIDs = node.GetValue ("controlIDs");
			if (configControlIDs != null && configControlIDs.Length > 0)
			{
				String[] splitIDs = configControlIDs.Split(',');				
				controlledModules = new int[splitIDs.Length];
				for(int i = 0; i < splitIDs.Length; i++){controlledModules[i] = SSTUUtils.safeParseInt(splitIDs[i].Trim());}				
			}
			else
			{
				controlledModules = new int[0];
			}
		}
		
		public void enable()
		{				
			foreach(MeshData md in meshData)
			{
				md.enable();
			}
		}
		
		public void disable()
		{
			foreach(MeshData md in meshData)
			{
				md.disable();
			}
		}
		
		public override string ToString ()
		{
			return string.Format ("[MeshConfig\n" +
				"variantName = " + variantName+"\n"+
				"tankType = " + tankName + "\n" +
				"meshData: \n"+SSTUUtils.printArray(meshData, "\n")+"]");
		}
	}
	
	public class MeshData
	{
		public String meshName;
		GameObject gameObject;
		
		public MeshData(String name, Part part)
		{
			meshName = name;
			GameObject g = part.gameObject.GetChild(meshName);
//			Transform tr = part.FindModelTransform(meshName);
			Transform tr = g==null? null : g.transform;
			if(tr!=null){gameObject = tr.gameObject;}
			else
			{
				MonoBehaviour.print ("ERROR! Could not locate transform for name: "+name);
				SSTUUtils.recursePrintComponents(part.gameObject, "");
			}
		}
		
		public void enable()
		{
			if(gameObject!=null)
			{	
				SSTUUtils.enableRenderRecursive(gameObject.transform, true);
				SSTUUtils.enableColliderRecursive(gameObject.transform, true);
			}
		}
		
		public void disable()
		{		
			if(gameObject!=null)
			{
				if(HighLogic.LoadedSceneIsFlight)
				{
					//TODO change this over to -delete- the extra unused meshes
					SSTUUtils.enableRenderRecursive(gameObject.transform, false);
					SSTUUtils.enableColliderRecursive(gameObject.transform, false);
				}
				else
				{
					SSTUUtils.enableRenderRecursive(gameObject.transform, false);
					SSTUUtils.enableColliderRecursive(gameObject.transform, false);
				}
			}			
		}

		public override string ToString ()
		{
			return string.Format ("[MeshData: "+meshName+"]");
		}
	}
	
}

