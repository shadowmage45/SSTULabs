using System;
using UnityEngine;
using System.Collections.Generic;

namespace SSTUTools
{
	public class SSTUEngineCluster : PartModule
	{		
		private static Dictionary<String, SSTUEngineLayout> layoutMap = new Dictionary<String, SSTUEngineLayout>();
		private static bool mapLoaded = false;
		
		[KSPField]
		public String modelName;
		
		[KSPField]
		public String mountName;
		
		[KSPField]
		public String layoutName;
		
		[KSPField]
		public float mountSpacing = 3f;
		
		[KSPField]
		public float verticalOffset = 0f;
		
		[KSPField]
		public float modelScale = 1f;		
		
		[KSPField]
		public String transformsToRemove = String.Empty;
		
		[KSPField]
		public float mountBaseSize = 3f;
		
		[KSPField]
		public float mountBaseHeight = 0.75f;
		
		[KSPField]
		public float mountSize = 3f;
		
		private List<GameObject> models = new List<GameObject>();
		
		private bool modelsSetup = false;
		
		public override void OnStart (PartModule.StartState state)
		{
			base.OnStart (state);
			initialize();			
		}
		
		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			if(!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
			{
				mapLoaded=false;				
			}
			initialize();
		}
		
		public override string GetInfo ()
		{
			foreach(GameObject go in models)
			{
				GameObject.Destroy(go);
			}
			models.Clear();
			return base.GetInfo ();
		}
		
		private void initialize()
		{
			loadMap();
			setupModels();
			removeTransforms();
			part.InitializeEffects();
		}
		
		private void removeTransforms()
		{
			if(String.IsNullOrEmpty(transformsToRemove)){return;}
			String[] names = SSTUUtils.parseCSV(transformsToRemove);
			foreach(String name in names)
			{
				Transform[] trs = part.FindModelTransforms(name.Trim());
				foreach(Transform tr in trs)
				{
					GameObject.Destroy(tr.gameObject);
				}
			}
		}
		
		private void setupModels()
		{
			if(modelsSetup){return;}
			modelsSetup = true;
			SSTUEngineLayout layout = null;
			layoutMap.TryGetValue(layoutName, out layout);
			
			GameObject mountModel = null;
			if(!String.IsNullOrEmpty(mountName)){mountModel = GameDatabase.Instance.GetModelPrefab(mountName);}
			GameObject engineModel = GameDatabase.Instance.GetModelPrefab(modelName);
			
			Transform modelBase = part.FindModelTransform("model");
			
			float posX, posZ, rot;
			float mountScale = (mountSize / mountBaseSize) * modelScale;
			float spacingScale = modelScale * mountSpacing;
			float mountY = verticalOffset * modelScale;
			float engineY = mountY - mountBaseHeight * mountScale;
			if(mountModel==null){engineY = mountY;}
			
			GameObject mountClone;
			GameObject engineClone;
			foreach(SSTUEnginePosition position in layout.positions)
			{
				posX = position.scaledX(spacingScale);
				posZ = position.scaledZ(spacingScale);
				rot = position.rotation;
				if(mountModel!=null)
				{
					mountClone = (GameObject)GameObject.Instantiate(mountModel);
					mountClone.name = mountModel.name;
					mountClone.transform.name = mountModel.transform.name;
					mountClone.transform.NestToParent(modelBase);
					mountClone.transform.localPosition = new Vector3(posX, mountY, posZ);
					mountClone.transform.localRotation = Quaternion.AngleAxis(rot, Vector3.up);
					mountClone.transform.localScale = new Vector3(mountScale, mountScale, mountScale);
					mountClone.SetActive(true);
					models.Add (mountClone);
				}
				engineClone = (GameObject)GameObject.Instantiate(engineModel);
				engineClone.name = engineModel.name;
				engineClone.transform.name = engineModel.transform.name;
				engineClone.transform.NestToParent(modelBase);
				engineClone.transform.localPosition = new Vector3(posX, engineY, posZ);
				engineClone.transform.localRotation = Quaternion.AngleAxis(rot, Vector3.up);
				engineClone.transform.localScale = new Vector3(modelScale, modelScale, modelScale);
				engineClone.SetActive(true);
				models.Add (engineClone);
			}
		}
		
		private void loadMap()
		{
			if(mapLoaded){return;}
			layoutMap.Clear();
			ConfigNode[] layoutNodes = GameDatabase.Instance.GetConfigNodes("SSTU_ENGINELAYOUT");
			SSTUEngineLayout layout;
			foreach(ConfigNode layoutNode in layoutNodes)
			{
				layout = new SSTUEngineLayout(layoutNode);
				layoutMap.Add(layout.name, layout);
			}
		}
	}
	
	public class SSTUEngineLayout
	{
		public String name = String.Empty;
		public List<SSTUEnginePosition> positions = new List<SSTUEnginePosition>();
		
		public SSTUEngineLayout(ConfigNode node)
		{
			name = node.GetStringValue("name");
			ConfigNode[] posNodes = node.GetNodes("POSITION");
			foreach(ConfigNode posNode in posNodes)
			{
				positions.Add(new SSTUEnginePosition(posNode));
			}
		}
	}
	
	public class SSTUEnginePosition
	{
		public float x;
		public float z;
		public float rotation;
		
		public SSTUEnginePosition(ConfigNode node)
		{
			x = node.GetFloatValue("x");
			z = node.GetFloatValue("z");
			rotation = node.GetFloatValue("rotation");
		}
		
		public float scaledX(float scale)
		{
			return scale * x;
		}
		
		public float scaledZ(float scale)
		{
			return scale * z;
		}
	}
}

