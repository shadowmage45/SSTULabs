using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	
	//mesh switch module
	//driven by config node system, can be linked to resource switch through config specification
	public class SSTUMeshSwitch : PartModule
	{
		public static Dictionary<String, MeshConfigData> meshConfigByPart = new Dictionary<String, MeshConfigData>();
					
		[KSPField]
		public String defaultVariantName;
		
		[KSPField(isPersistant=true)]
		public int currentConfiguration = -1;
		
		[KSPField(guiActive=true, guiActiveEditor = true, guiName = "Variant")]
		public String meshDisplayName = String.Empty;
		
		MeshConfig[] meshConfigurations;
		
		//linked resource switch module
		SSTUResourceSwitch resourceSwitch;
				
		public SSTUMeshSwitch ()
		{
			
		}
		
		[KSPEvent(name="nextMeshEvent", guiName="Next Variant", guiActiveEditor=true)]
		public void nextMeshEvent()
		{
			currentConfiguration++;
			if(currentConfiguration>=meshConfigurations.Length){currentConfiguration=0;}
			setToMeshConfig(currentConfiguration, true);
		}	
		
		[KSPEvent(name="prevMeshEvent", guiName="Prev. Variant", guiActiveEditor=true)]
		public void prevMeshEvent()
		{
			currentConfiguration--;
			if(currentConfiguration<0){currentConfiguration = meshConfigurations.Length - 1;}
			setToMeshConfig(currentConfiguration, true);
		}	
		
		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			MeshConfigData mcd = null;
			//only run on prefab init.  how to properly catch that state?
			
			if(!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
			{
				mcd = new MeshConfigData();
				mcd.moduleConfigNode = node;
				meshConfigByPart.Remove(part.name);
				meshConfigByPart.Add(part.name, mcd);
			}		
			initMeshConfig(false);	
		}
		
		public override void OnStart (PartModule.StartState state)
		{
			base.OnStart (state);
			initMeshConfig(true);
		}
		
		private void initMeshConfig(bool enableResourceSwitch)
		{			
			MeshConfigData mcd = null;
			if(meshConfigByPart.TryGetValue(part.name, out mcd))
			{
				meshConfigurations = mcd.getConfigFor(part);
			}
			if(enableResourceSwitch)
			{				
				resourceSwitch = part.GetComponent<SSTUResourceSwitch>();				
			}
			if(currentConfiguration==-1)//uninitialized part
			{
				setToMeshConfig(defaultVariantName, true);
			}
			else
			{
				setToMeshConfig(currentConfiguration, !HighLogic.LoadedSceneIsFlight);//only update resources (refill/reset) if not in flight
			}
		}
		
		public void setToMeshConfig(String variantName, bool updateResources)
		{
			int len = meshConfigurations.Length;
			for(int i = 0; i < len; i++)
			{
				if(meshConfigurations[i].variantName.Equals(variantName))
				{
					setToMeshConfig(i, updateResources);
					return;
				}
			}
		}
		
		private void setToMeshConfig(int index, bool updateResources)
		{
			currentConfiguration = index;
			MeshConfig config = meshConfigurations[index];
			meshDisplayName = config.variantName;
			
			int len = meshConfigurations.Length;
			for(int i = 0; i < len; i++)
			{
				if(i==index){continue;}
				meshConfigurations[i].disable();
				if(HighLogic.LoadedSceneIsFlight)
				{
					meshConfigurations[i].removeMeshes();
				}
			}
			config.enable();			
			if(updateResources && config.tankName.Length>0 && resourceSwitch!=null)
			{
				resourceSwitch.initModule();
				resourceSwitch.setTankToConfig(config.tankName);
			}
		}
	}
	
	//wraps the persistent config node data, to be read by individual part instances
	public class MeshConfigData
	{
		public ConfigNode moduleConfigNode;
		
		public MeshConfig[] getConfigFor(Part part)
		{
			ConfigNode[] variantNodes = moduleConfigNode.GetNodes("MESHVARIANT");
			MeshConfig[] cfgs = new MeshConfig[variantNodes.Length];
			for(int i = 0; i < cfgs.Length; i++)
			{
				cfgs[i] = new MeshConfig(variantNodes[i], part);
			}
			return cfgs;
		}
	}
	
	//single part variant configuration
	public class MeshConfig
	{
		public String variantName = String.Empty;
		public String tankName = String.Empty;
		public MeshData[] meshData;
		
		public MeshConfig(ConfigNode node, Part part)
		{
			variantName = node.GetValue("variantName");
			tankName = node.GetValue("tankName");
			String meshNames = node.GetValue("meshNames");
			MonoBehaviour.print ("mesh names: "+meshNames);
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
		
		public void removeMeshes()
		{
			foreach(MeshData md in meshData)
			{
				md.removeMesh();
			}
		}
		
		public override string ToString ()
		{
			return string.Format ("[MeshConfig\n" +
				"variantName =" + variantName+"\n"+
				"tankType = " + tankName + "\n" +
				"meshData: \n"+SSTUUtils.printArray(meshData,"\n")+"]");
		}
	}
	
	public class MeshData
	{
		public String meshName;
		GameObject gameObject;
		
		public MeshData(String name, Part part)
		{
			meshName = name;
			Transform tr = part.FindModelTransform(meshName);
			if(tr!=null){gameObject = tr.gameObject;}
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
				SSTUUtils.enableRenderRecursive(gameObject.transform, false);
				SSTUUtils.enableColliderRecursive(gameObject.transform, false);	
			}			
		}
		
		public void removeMesh()
		{
//			GameObject.Destroy(gameObject);
//			gameObject=null;
		}
	}
}

