using System;
using System.Collections.Generic;

namespace SSTUTools
{
	public class SSTUResourceSwitch : PartModule, IPartCostModifier
	{
		public static Dictionary<String, TankConfig[]> tankSetupsByPart = new Dictionary<String, TankConfig[]>();

		//example tank definitions
		//
		//TANK
		//{
		//	tankName = Ore
		//	tankDryMass = 0.1
		//	tankCost = 500
		//	TANKRESOURCE
		//	{
		//		name=Ore
		//		amount=10
		//		fillAmount=10
		//	}
		//}
		//
		//TANK
		//{
		//	tankName = LFO
		//	tankDryMass = 0.1
		//	tankCost = 500
		//	TANKRESOURCE
		//	{
		//		name=LiquidFuel
		//		amount=90
		//		fillAmount=90
		//	}
		//	TANKRESOURCE
		//	{
		//		name=Oxidizer
		//		amount=110
		//		fillAmount=110
		//	}
		//}
				
		//has the tank been initialized the first time?
		//if not, and it is -in the editor-, the tank contents will be setup
		//used to prevent swapping of contents of in-flight tanks if module is added at a later date
		//also prevents swapping of contents if tank setup indices change
		[KSPField(isPersistant=true)]
		public bool initialized = false;
		
		//used to track the current tank type from those loaded from config, mostly used in editor
		[KSPField(isPersistant=true)]
		public int tankType = 0;
		
		[KSPField(guiActiveEditor=true, guiName="Tank Type", guiActive=true)]
		public String tankTypeName = "NONE";
		
		float defaultMass;
		bool hasTankCost=false;
		float tankCost;
		float resourceCost;

		TankConfig[] configs;
				
		public SSTUResourceSwitch ()
		{
			
		}
		
		[KSPEvent(name="nextTankEvent", guiName="Next Tank", guiActiveEditor=true)]
		public void nextTankEvent()
		{
			tankType++;
			if(tankType>=configs.Length){tankType=0;}
			setTankToConfig(tankType);
		}	
		
		[KSPEvent(name="prevTankEvent", guiName="Prev. Tank", guiActiveEditor=true)]
		public void prevTankEvent()
		{
			tankType--;
			if(tankType<0){tankType = configs.Length - 1;}
			setTankToConfig(tankType);
		}	
		
		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);

			//only init tank types if not previously loaded
			if(tankSetupsByPart.ContainsKey(part.name))
			{
				return;
			}
			if(HighLogic.LoadedSceneIsEditor)//somehow init got missed
			{
				//create fake config node, structural
				TankConfig structural = TankConfig.STRUCTURAL;
				configs = new TankConfig[]{structural};
				return;
			}
			defaultMass = part.mass;
			if(node.HasNode("TANK"))
			{				
				ConfigNode[] tankNodes = node.GetNodes("TANK");			
				List<TankConfig> tanks = new List<TankConfig>();
				TankConfig tank;
				foreach(ConfigNode n2 in tankNodes)
				{
					tank = parseTankConfig(n2);
					if(tank!=null)
					{
						tanks.Add (tank);
					}				
				}
				TankConfig[] configs = tanks.ToArray();
				tankSetupsByPart.Add (part.name, configs);
			}
			else
			{
				tankSetupsByPart.Add(part.name, new TankConfig[]{TankConfig.STRUCTURAL});
			}
		}
		
		public override void OnStart (PartModule.StartState state)
		{
			base.OnStart (state);

			//initialize local tank config list; memory use should be minimal as it is just a reference to an already existing array
			configs = getConfigForPart (part);
			//only run init if first time the part is being setup AND it is in the editor
			if(!initialized && HighLogic.LoadedSceneIsEditor)
			{
				setTankToConfig(0);
			}
			if(configs.Length<=1)
			{
				Events["nextTankEvent"].active=false;
				Events["prevTankEvent"].active=false;
			}
			tankTypeName = configs[tankType].tankName;
		}
		
		public float GetModuleCost (float defaultCost)
		{
			if(hasTankCost){return -defaultCost + tankCost + resourceCost;}
			else{return resourceCost;}
		}
		
		private void setTankToConfig(int index)
		{	
			initialized = true;
			if(index>=configs.Length || index<0)
			{
				index = 0;				
			}
			tankType = index;
			TankConfig config = configs[index];
			tankTypeName = config.tankName;
			setTankToResourcesFromConfig(config);
			
			if(config.tankDryMass>=0)
			{
				part.mass = config.tankDryMass;
			}
			else
			{
				part.mass = defaultMass;
			}
			
			resourceCost = config.getResourceCost();
			
			if(config.tankCost>=0)
			{
				tankCost = config.tankCost;				
				hasTankCost = true;
			}
			else
			{
				tankCost = -1;
				hasTankCost = false;
			}
			if(HighLogic.LoadedSceneIsEditor)
			{
				GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
			}
		}
		
		private void setTankToResourcesFromConfig(TankConfig config)
		{
			part.Resources.list.Clear ();
			PartResource[] resources = part.GetComponents<PartResource> ();
			int len = resources.Length;
			for (int i = 0; i < len; i++)
			{
				DestroyImmediate(resources[i]);
			}
			foreach(ConfigNode node in config.getResourceConfigNodes())
			{
				part.AddResource(node);
			}
		}
		
		private TankConfig parseTankConfig(ConfigNode node)
		{			
			TankConfig config = new TankConfig(node);
			return config;
		}

		private TankConfig[] getConfigForPart(Part part)
		{
			TankConfig[] cfgs;
			if(!tankSetupsByPart.TryGetValue(part.name, out cfgs))
		   	{
				return new TankConfig[0];
			}
			return cfgs;
		}
	}
	
	public class TankConfig
	{
		public static TankConfig STRUCTURAL;
		public String tankName;
		public float tankCost = -1;
		public float tankDryMass = -1;
		private float tankResourceCost = -1;
		List<TankResourceConfig> tankResourceConfigs = new List<TankResourceConfig>();	
				
		static TankConfig()
		{
			STRUCTURAL = new TankConfig();
			STRUCTURAL.tankName = "Structural";
		}
		
		private TankConfig (){}
		
		public TankConfig(ConfigNode node)
		{
			tankName = node.GetValue("tankName");	
			if(node.HasValue("tankCost")){tankCost = (float)SSTUUtils.safeParseDouble(node.GetValue("tankCost"));}
			if(node.HasValue("tankDryMass")){tankDryMass = (float)SSTUUtils.safeParseDouble(node.GetValue("tankDryMass"));}			
			
			ConfigNode[] tanks = node.GetNodes("TANKRESOURCE");
			foreach(ConfigNode tcn in tanks){loadTankConfigNode(tcn);}
		}
		
		private void loadTankConfigNode(ConfigNode node)
		{
			TankResourceConfig cfg = new TankResourceConfig();
			cfg.resourceName = node.GetValue("resource");
			cfg.amount = (float)SSTUUtils.safeParseDouble(node.GetValue("amount"));
			cfg.fillAmount = (float)SSTUUtils.safeParseDouble(node.GetValue("fillAmount"));
			tankResourceConfigs.Add (cfg);
		}

		public ConfigNode[] getResourceConfigNodes()
		{
			int length = tankResourceConfigs.Count;
			ConfigNode[] nodes = new ConfigNode[length];
			ConfigNode node;			
			for (int i = 0; i < length; i++)
			{
				node = nodes[i] = new ConfigNode("RESOURCE");
				node.AddValue("name", tankResourceConfigs[i].resourceName);
				node.AddValue("maxAmount", tankResourceConfigs[i].amount);
				node.AddValue("amount", tankResourceConfigs[i].fillAmount);
			}
			return nodes;
		}
		
		public override string ToString ()
		{
			return string.Format (
				"[TankConfig:   "+tankName+"]\n" +				
				"TankCost:      "+tankCost+"\n" +
				"TankMass:      "+tankDryMass+"\n" +
				"Resources: \n"+getResourcesString());
		}
		
		public float getResourceCost()
		{
			if(tankResourceCost==-1)
			{
				tankResourceCost = 0;
				PartResourceDefinition def;
				foreach(TankResourceConfig cfg in tankResourceConfigs)
				{
					if(PartResourceLibrary.Instance.resourceDefinitions.Contains(cfg.resourceName))
					{
						def = PartResourceLibrary.Instance.GetDefinition(cfg.resourceName);						
						tankResourceCost += def.unitCost * cfg.fillAmount;
					}
				}
			}
			return tankResourceCost;
		}
		
		private String getResourcesString()
		{
			return SSTUUtils.printList(tankResourceConfigs, "\n");
		}
	}
	
	public class TankResourceConfig
	{
		public String resourceName;
		public float amount;
		public float fillAmount;
		
		public override string ToString ()
		{
			return string.Format ("[TankResource: "+resourceName+" - "+fillAmount+" / "+amount+"]");
		}
	}
}

