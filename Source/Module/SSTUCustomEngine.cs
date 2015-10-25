using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	public class SSTUCustomEngine : PartModule, IPartCostModifier
	{
		//used to look up the config data from external config file, so as to not need to store all data in the part.cfg
		[KSPField]
		public String configName = "1.25m Engine Cluster";

		//used to rescale mount and engines by a set factor; should be used by RSS to rescale entire setup by a set amount
		[KSPField]
		public float scale = 1;

		[KSPField(isPersistant=true)]
		public String engineDefPersistance;

		[KSPField(isPersistant=true)]
		public String clusterDefPersistance;

		//if true, the shroud model for the cluster definition will also be loaded and rendered
		//TODO  decrease atmospheric drag when shroud is enabled?
		[KSPField(isPersistant=true)]
		public bool shroudEnabled = false;

		//used to store modified part cost for return by GetModuleCost method
		private float modifiedCost = 0;

		//the engine module that will be manipulated by this plugin module
		private ModuleEnginesFX engineModule;

		//the gimbal module that will be manipulated by this plugin
		private ModuleGimbal gimbalModule;

		//the fairing module for the lower skirt/interstage fairing
		private SSTUNodeFairing fairingModule;

		//config node, pulled from gameDatabase
		private ConfigNode moduleData;

		private EngineDefinition currentSetup;

		private EngineDefinition[] possibleDefs;

		private ClusterAdapterDefinition currentClusterSetup;

		private ClusterAdapterDefinition[] possibleClusterDefs;

		public float GetModuleCost(float input){return modifiedCost;}

		public override void OnStart (StartState state)
		{
			base.OnStart (state);
			locateModules ();
		}

		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			locateModules ();
		}

		private void locateModules()
		{
			if (engineModule == null)
			{
				engineModule = part.GetComponent<ModuleEnginesFX> ();
				gimbalModule = part.GetComponent<ModuleGimbal> ();
				fairingModule = part.GetComponent<SSTUNodeFairing> ();
			}
		}

	}

	//stores data to link part.cfg to list of cluster types
	public class EnginePartConfig
	{
		public readonly String configName = "1.25m Engine Cluster";
		public readonly List<String> clusterNames = new List<String> ();
		
		public EnginePartConfig(ConfigNode node)
		{
			//TODO
		}
	}

	//active engine setup, basically a container to manage the engine stuff
	public class EngineSetup
	{
		private List<object> activeConstraints = new List<object>();
		private List<object> activeEffects = new List<object> ();
		private List<Transform> activeThrustTransforms = new List<Transform> ();
		private List<Transform> activeGimbalTransforms = new List<Transform> ();

		public void setupThrustTransforms(Part part, EngineDefinition def, List<Transform> transformsOutput)
		{

		}

		public void setupConstraints(Part part, EngineDefinition def)
		{

		}

		public void updateConstraints()
		{

		}
	}

	//persistent engine definition
	public class EngineDefinition
	{
		public EngineDefinition(ConfigNode node)
		{
			//TODO
		}
	}

	public class ClusterAdapterDefinition
	{
		public ClusterAdapterDefinition(ConfigNode node)
		{
			//TODO
		}
	}

	public class LayoutDefinition
	{
		public readonly String layoutName;
		public readonly String layoutDesc;
		public readonly List<Vector3> unscaledPositions = new List<Vector3>();

		public LayoutDefinition(ConfigNode node)
		{
			//TODO
		}
	}
}

