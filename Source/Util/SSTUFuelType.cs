using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	public class SSTUFuelTypes
	{
		public static readonly SSTUFuelTypes INSTANCE = new SSTUFuelTypes();
		
		private bool loadedDefs = false;
		
		private Dictionary<String, SSTUFuelType> fuelTypes = new Dictionary<String, SSTUFuelType>();
		
		private Dictionary<String, float> resourceVolumes = new Dictionary<String, float>();
		
		private void loadDefs()
		{
			if(loadedDefs){return;}
			fuelTypes.Clear();
			resourceVolumes.Clear();
			
			ConfigNode[] configs = GameDatabase.Instance.GetConfigNodes("SSTU_RESOURCEVOLUME");
			foreach(ConfigNode node in configs)
			{
				resourceVolumes.Add(node.GetStringValue("name"), node.GetFloatValue("volume"));
			}
			
			configs = GameDatabase.Instance.GetConfigNodes("SSTU_FUELTYPE");
			SSTUFuelType fuelType;
			foreach(ConfigNode node in configs)
			{
				fuelType = new SSTUFuelType(node);
				fuelTypes.Add(fuelType.name, fuelType);				
			}
			
			loadedDefs = true;
		}
		
		public SSTUFuelType getFuelType(String type)
		{
			loadDefs();
			SSTUFuelType t = null;
			fuelTypes.TryGetValue(type, out t);
			return t;
		}
		
		public float getResourceVolume(String name)
		{
			float val = 0;
			resourceVolumes.TryGetValue(name, out val);
			return val;
		}
	}
	
	public class SSTUFuelType
	{
		private List<SSTUFuelEntry> fuelEntries = new List<SSTUFuelEntry>();		
		public readonly String name;
		public readonly float tankageVolumeLoss;		
		public readonly float tankageMassFactor;
		public readonly float costPerDryTon;
		
		private float litersPerUnit;
		private float costPerUnit;
		private float unitsPerCubicMeter;
		
		public SSTUFuelType(ConfigNode node)
		{
			name = node.GetStringValue("name");
			tankageVolumeLoss = node.GetFloatValue("tankageVolumeLoss");
			tankageMassFactor = node.GetFloatValue("tankageMassFactor", 1);
			costPerDryTon = node.GetFloatValue("costPerDryTon");
			ConfigNode[] fuelConfigs = node.GetNodes("RESOURCE");			
			SSTUFuelEntry e = null;	
			foreach(ConfigNode n in fuelConfigs)
			{
				e = new SSTUFuelEntry(n);
				fuelEntries.Add(e);
				litersPerUnit += SSTUFuelTypes.INSTANCE.getResourceVolume(e.resourceName) * e.ratio;
				costPerUnit += PartResourceLibrary.Instance.GetDefinition(e.resourceName).unitCost * e.ratio;
			}
			unitsPerCubicMeter = 1000f / litersPerUnit;
//			MonoBehaviour.print("Loaded SSTU_FUEL TYPE: "+name+"  units/m3: "+unitsPerCubicMeter+"  costPerUnit: "+costPerUnit +"  costPerM3: "+(unitsPerCubicMeter*costPerUnit));
		}
		
		public override string ToString ()
		{
			return string.Format ("[SSTUFuelType: "+name+"]");
		}
		
		public void setResourcesInPart(Part part, float volume, bool fill)
		{
			part.Resources.list.Clear ();
			PartResource[] resources = part.GetComponents<PartResource> ();
			int len = resources.Length;
			for (int i = 0; i < len; i++)
			{
				GameObject.Destroy(resources[i]);
			}
			int rawFuelUnits = (int)(volume * unitsPerCubicMeter);
			int units;
			ConfigNode resourceNode;
			foreach (SSTUFuelEntry entry in fuelEntries)
			{
				units = entry.ratio * rawFuelUnits;
				resourceNode = new ConfigNode("RESOURCE");			
				resourceNode.AddValue("name", entry.resourceName);
				resourceNode.AddValue("maxAmount", units);
				resourceNode.AddValue("amount", fill? units : 0);
				part.AddResource(resourceNode);
			}
		}
		
		public float getResourceCost(float cubicMeters)
		{
			return costPerUnit * cubicMeters * unitsPerCubicMeter;
		}
	}
	
	public class SSTUFuelEntry
	{
		public String resourceName;
		public int ratio;
		public SSTUFuelEntry(ConfigNode node)
		{
			resourceName = node.GetStringValue("resource");
			ratio = node.GetIntValue("ratio", 1);
		}
	}
	
}

