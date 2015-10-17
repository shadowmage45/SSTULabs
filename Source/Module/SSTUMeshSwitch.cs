using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	
	//Needed capabilities
	// * Swap out render meshes and colliders
	// * Swap main part resources
	// * Swap / move attach nodes per mesh config
	// Swap texture of individual meshes for a specific variant
	// -- can be added as additional config node in the mesh config definition (similar to attach-node config defs)
	// Allow list of tank setups for each mesh
	// -- can be listed in each mesh config setup as 'alternate tanks'
	// -- can track what 'alternate tank' number is currently in use, and try to use that alternate tank when switching mesh variants (if the new variant has that #)
	// Allow specific texture per tank setup (e.g. specific texture for a fuel type)	
	// Allow in-field switching of resources (and textures), but not huge mesh changes (config defined/restricted)
	// ??create stand-alone texture-switch module that is responsible for storing/managing textures?
	// ----would need to come before the resource/mesh switch modules, to be controlled from either
	
	//mesh switch module
	//driven by config node system, can be linked to resource switch through config specification
	//TODO
	// how to allow for setting of default mesh AND resources during prefab part instantiation?
	// -- have an externally accessible var/method in SSTUResourceSwitch that accepts a tankName for default instantiation?
	public class SSTUMeshSwitch : PartModule, IPartCostModifier//, IPartMassModifier
	{
		//used to suffix the part-name in order to store persistent config data in static dictionary		
		//deprecated, needs removed
		[KSPField]
		public int moduleID = 0;
				
		//the default variant to be shown in the editor icon
		[KSPField]		
		public String defaultVariantName;
		
		//persistent storage of current config index; -1 indicates uninitialized
		[KSPField(isPersistant=true)]
		public int currentConfiguration = -1;
		
		//currently displayed mesh/variant name
		[KSPField(guiActive=true, guiActiveEditor = true, guiName = "Variant")]
		public String meshDisplayName = String.Empty;
		
		//the 'name' of the variant type in the editor (e.g. nose-cone, tank geometry, whatever); this is just to present info to user
		[KSPField]
		public String variantLabel = "Variant";
		
		//if true, this specific module will be responsible for controlling 'optional' tank setups in the resource switch module
		[KSPField]
		public bool controlsTankOptions = false;
		
		//if true, the 'prev variant' button will be enabled in the editor
		[KSPField]
		public bool enablePrevButton = true;
		
		//working var, used to store persistent information from the original config as it is parsed during prefab loading
		[Persistent]
		public String configNodeString;

		//current mesh configuration data, the total set of configs, and the specific current config
		private MeshConfig[] meshConfigurations;
		private MeshConfig currentConfig;
		
		//linked resource switch module, if any
		private SSTUResourceSwitch resourceSwitch;
		//linked module control module, if any
		private SSTUModuleControl moduleControl;
					
		[KSPEvent(name="nextMeshEvent", guiName="Next Variant", guiActiveEditor=true)]
		public void nextMeshEvent()
		{
			int prevConfig = currentConfiguration;
			currentConfiguration++;
			if(currentConfiguration>=meshConfigurations.Length){currentConfiguration=0;}
			if(!meshConfigurations[currentConfiguration].canSwitchToVariant())
			{
				currentConfiguration = prevConfig;
				//TODO print error msg
				return;
			}
			setToMeshConfig(currentConfiguration);
			updateResourceSwitch();
			updateModuleSwitch();
			updatePartMass();
			GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}	
		
		[KSPEvent(name="prevMeshEvent", guiName="Prev. Variant", guiActiveEditor=true)]
		public void prevMeshEvent()
		{
			int prevConfig = currentConfiguration;
			currentConfiguration--;
			if(currentConfiguration<0){currentConfiguration = meshConfigurations.Length - 1;}
			if(!meshConfigurations[currentConfiguration].canSwitchToVariant())
			{
				currentConfiguration = prevConfig;
				//TODO print error msg
				return;
			}
			setToMeshConfig(currentConfiguration);
			updateResourceSwitch();
			updateModuleSwitch();
			updatePartMass();
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
		
		public float GetModuleCost (float defaultCost)
		{
			return currentConfig==null? 0 : currentConfig.variantCost;
		}
		
		#endregion
		
		private void initialize()
		{
			findLinkedModules();
			loadConfigFromPrefab();
			reloadSavedMeshData();
			updatePartMass();
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
			updatePartMass();
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
//							print ("enabling module: "+modules[k]);
							moduleControl.enableControlledModule(modules[k]);
						}
						else
						{
//							print ("disabling module: "+modules[k]);
							moduleControl.disableControlledModule(modules[k]);
						}
					}
				}
			}
		}
		
		private void updatePartMass()
		{
			float mass = currentConfig==null? 0 : currentConfig.variantMass;			
			SSTUMeshSwitch[] switches = part.GetComponents<SSTUMeshSwitch>();
			//find all other mesh-switch modules and manually add that mass in, even though they will all run the same circular calculation whenever they are upated
			if(switches!=null && switches.Length>0)
			{
				foreach(SSTUMeshSwitch sw in switches)
				{
					if(sw.currentConfig!=null){mass+=sw.currentConfig.variantMass;}
				}
			}			
			//lastly, if resourceSwitch is not null, add the base mass from the resourceswitch
			if(resourceSwitch!=null){mass+=resourceSwitch.GetModuleMass(0);}
			if(mass==0){mass=part.mass;}//if mass not set in config, use existing part mass
			part.mass = mass;
		}
		
//		private void enableAllMeshes()
//		{
//			foreach(MeshConfig cfg in meshConfigurations)
//			{
//				cfg.enable(true);
//			}
//		}

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
					if(meshConfigurations[i].canSwitchToVariant())
					{
						setToMeshConfig(i);
					}
					else
					{
						//TODO print on-screen message regarding cannot switch due to node stuff
						#warning need to add output screen message
					}
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
			currentConfig.enable(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor);			
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
		public MeshNodeData[] nodeData;
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
			tankName = node.GetStringValue("tankName");
			variantMass = (float)node.GetDoubleValue ("variantMass");
			variantCost = (float)node.GetDoubleValue ("variantCost");
			tankOption = node.GetStringValue("tankOption");
			String meshNames = node.GetStringValue("meshNames");
			if(String.IsNullOrEmpty(meshNames))
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
			String configControlIDs = node.GetStringValue ("controlIDs");
			if (String.IsNullOrEmpty (configControlIDs))
			{
				controlledModules = new int[0];
			}
			else
			{
				String[] splitIDs = configControlIDs.Split(',');				
				controlledModules = new int[splitIDs.Length];
				for(int i = 0; i < splitIDs.Length; i++){controlledModules[i] = SSTUUtils.safeParseInt(splitIDs[i].Trim());}				
			}
			ConfigNode[] nodeNodes = node.GetNodes ("MESHNODE");
			if (nodeNodes!=null && nodeNodes.Length > 0)
			{
				nodeData = new MeshNodeData[nodeNodes.Length];
				int len = nodeNodes.Length;
				for(int i = 0; i < len; i++)
				{
					nodeData[i] = new MeshNodeData(nodeNodes[i], part);
				}
			}
			else
			{
				nodeData = new MeshNodeData[0];
			}
		}
		
		public void enable(bool updateNodes)
		{		
			foreach(MeshData md in meshData)
			{
				md.enable();
			}
			if(!updateNodes){return;}
			foreach (MeshNodeData mnd in nodeData)
			{
				mnd.enableConfig();
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

		public bool canSwitchToVariant()
		{
			foreach (MeshNodeData mnd in nodeData)
			{
				if(!mnd.canEnable())
				{
					return false;
				}
			}
			return true;
		}
	}
	
	public class MeshTextureData
	{
		public String meshName;
		public String textureName;
	}

	//data for one manipulatable node in a mesh
	public class MeshNodeData
	{
		public Part part;
		public String nodeName = "top";
		public bool nodeEnabled = true;
		public Vector3 nodePosition = Vector3.zero;
		public Vector3 nodeOrientation = Vector3.zero;
		public int nodeSize = 2;

		public MeshNodeData(ConfigNode inputNode, Part inputPart)
		{			
			nodeName = inputNode.GetStringValue ("name");			
			if(String.IsNullOrEmpty(nodeName)){MonoBehaviour.print ("ERROR!! : Node name was null for meshswitch node data!!");}
			nodeEnabled = inputNode.GetBoolValue ("enabled", true);			
			this.part = inputPart;			
			if (inputNode.HasValue ("position"))
			{				
				nodePosition = inputNode.GetVector3 ("position");
			}
			else if(nodeEnabled==true)
			{
				MonoBehaviour.print ("ERROR -- no position assigned, but node: "+nodeName+" is enabled for mesh switch");
				nodePosition = Vector3.zero;
			}
			if (inputNode.HasValue ("orientation"))
			{
				nodeOrientation = inputNode.GetVector3 ("orientation");
			}
			else if(nodeEnabled==true)
			{					
				MonoBehaviour.print ("ERROR -- no orientation assigned, but node: "+nodeName+" is enabled for mesh switch");
				nodeOrientation = Vector3.zero;				
			}
			nodeSize = inputNode.GetIntValue("size", 2);
		}

		public void enableConfig()
		{
			AttachNode node = part.findAttachNode (nodeName);
			if (nodeEnabled)//node is enabled in this config; add it if not present, move it if it is already present
			{			
				if (node == null)
				{
					
					AttachNode newNode = new AttachNode();
					node = newNode;
					newNode.id = nodeName;
					newNode.owner = part;
					newNode.nodeType = AttachNode.NodeType.Stack;
					newNode.size = nodeSize;
					newNode.originalPosition.x = newNode.position.x = nodePosition.x;
					newNode.originalPosition.y = newNode.position.y = nodePosition.y;
					newNode.originalPosition.z = newNode.position.z = nodePosition.z;
					newNode.originalOrientation.x = newNode.orientation.x = nodeOrientation.x;
					newNode.originalOrientation.y = newNode.orientation.y = nodeOrientation.y;
					newNode.originalOrientation.z = newNode.orientation.z = nodeOrientation.z;
					part.attachNodes.Add (newNode);
					//TODO trigger editor update to refresh node position...; just send editor-part-modified event?
				}
				else
				{
					Vector3 pos = node.position;
					Vector3 diff = nodePosition - node.position;//used to adjust part positions
					diff = part.transform.InverseTransformPoint(diff);
					diff += part.transform.position;
					node.position = node.originalPosition = nodePosition.CopyVector();
					node.orientation = node.originalOrientation = nodeOrientation.CopyVector();
					if(node.attachedPart!=null)
					{
						if(node.attachedPart.parent == part)//is a child of this part, move it the entire offset distance
						{
							node.attachedPart.attPos0 += diff;
							node.attachedPart.transform.position += diff;
						}
						else//is a parent of this part, do not move it, instead move this part the full amount
						{
							part.attPos0 -= diff;
							part.transform.position -= diff;
						}
					}
					//check for attached parts, move attached parts if current node position and new node position differ
				}
			}
			else//node not enabled for this config, remove it if possible (no parts attached)  
			{
				if(node!=null)
				{
					if(node.attachedPart!=null)//cannot disable node, it will cause crashes if part is still attached (at least at vessel reload)
					{
						MonoBehaviour.print ("Could not remove attach node: "+nodeName+" as it has attached parts!!");
						return;
					}
					else
					{
						node.owner = null;
						part.attachNodes.Remove(node);
						if(node.icon!=null)
						{
							GameObject.Destroy(node.icon);
						}
					} 
				}
				//else NOOP, nothing to do if node is not present
			}			
		}

		public bool canEnable()
		{
			if (nodeEnabled)//node is enabled for this config, can be set regardless of part attached status
			{
				return true;
			}
			else//node not enabled for this config, cannot switch to if parts are currently attached to it
			{
				AttachNode persistentNode = part.findAttachNode(nodeName);
				if(persistentNode==null){return true;}
				return persistentNode.attachedPart == null;
			}
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

