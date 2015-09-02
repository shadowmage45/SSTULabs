using UnityEngine;
using System;
namespace SSTUTools
{
	public class SSTUProceduralDecoupler : ModuleDecouple, IPartCostModifier, IPartMassModifier
	{	
		#region fields
		[KSPField]
		public string diffuseTextureName = "UNKNOWN";
		
		[KSPField]
		public string normalTextureName = "UNKNOWN";

		[KSPField]
		public bool canAdjustRadius = false;
		
		[KSPField]
		public bool canAdjustThickness = false;
		
		[KSPField]
		public bool canAdjustHeight = false;
		
		[KSPField(isPersistant=true)]
		public float radius = 0.625f;
		
		[KSPField(isPersistant=true)]
		public float height = 0.1f;
		
		[KSPField(isPersistant=true)]
		public float thickness = 0.1f;

		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Rad Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
		public float radiusExtra;
		
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Height Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
		public float heightExtra;
		
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Thick Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
		public float thicknessExtra;
		
		[KSPField]
		public float capHeight = 0f;
		
		[KSPField]
		public float maxPanelHeight = 1f;
		
		[KSPField]
		public int cylinderSides = 24;
		
		[KSPField]
		public float radiusAdjust = 0.3125f;
		
		[KSPField]
		public float heightAdjust = 0.1f;
		
		[KSPField]
		public float thicknessAdjust = 0.1f;
		
		[KSPField]
		public float minRadius = 0.3125f;
		
		[KSPField]
		public float maxRadius = 5f;
		
		[KSPField]
		public float minThickness = 0.1f;
		
		[KSPField]
		public float maxThickness = 5f;
		
		[KSPField]
		public float minHeight = 0.1f;
		
		[KSPField]
		public float maxHeight = 0.5f;
		
		[KSPField]
		public float massPerCubicMeter = 1;
		
		[KSPField]
		public float costPerCubicMeter = 1;

		[KSPField]
		public float forcePerKg = 1;

		public float modifiedMass = 0;
		
		public float modifiedCost = 0;

		public float volume = 0;

		private ProceduralCylinderModel model;
		
		private float lastRadiusExtra;
		private float lastHeightExtra;
		private float lastThicknessExtra;
		

		#endregion

		#region KSP GUI Actions/Events

		[KSPEvent(guiName="Radius +",guiActiveEditor=true)]
		public void increaseRadius()
		{
			radius+=radiusAdjust;
			if(radius>maxRadius){radius=maxRadius;}
			recreateModel ();
		}
		
		[KSPEvent(guiName="Radius -",guiActiveEditor=true)]
		public void decreaseRadius()
		{
			radius-=radiusAdjust;
			if(radius<minRadius){radius=minRadius;}
			recreateModel ();
		}
		
		[KSPEvent(guiName="Height +",guiActiveEditor=true)]
		public void increaseHeight()
		{
			height+=heightAdjust;
			if(height>maxHeight){height=maxHeight;}
			recreateModel ();
			updateAttachNodePositions();			
		}
		
		[KSPEvent(guiName="Height -",guiActiveEditor=true)]
		public void decreaseHeight()
		{
			height-=heightAdjust;
			if(height<minHeight){height=minHeight;}
			recreateModel ();
			updateAttachNodePositions();
		}
		
		[KSPEvent(guiName="Thickness +",guiActiveEditor=true)]
		public void increaseThickness()
		{
			thickness+=thicknessAdjust;
			if(thickness>maxThickness){thickness=maxThickness;}
			recreateModel ();
		}
		
		[KSPEvent(guiName="Thickness -",guiActiveEditor=true)]
		public void decreaseThickness()
		{
			thickness-=thicknessAdjust;
			if(thickness<minThickness){thickness=minThickness;}
			recreateModel ();
		}

		#endregion

		#region KSP Lifecycle and KSP Overrides

		//DONE
		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
			{
				prepModel ();
			}
		}

		//DONE
		public override string GetInfo ()
		{
			model.destroyModel ();
			SSTUUtils.destroyChildren(part.FindModelTransform ("model"));//remove the original empty proxy model and any created models
			model = null;
			return base.GetInfo ();
		}

		//DONE
		public override void OnStart (PartModule.StartState state)
		{
			base.OnStart (state);
			prepModel ();
			updateGuiState();
		}

		//DONE
		public float GetModuleCost (float defaultCost)
		{
			return modifiedCost;
		}

		//DONE
		public float GetModuleMass (float defaultMass)
		{
			return modifiedMass;
		}
		
		//DONE
		public void Update()
		{
			if(!HighLogic.LoadedSceneIsEditor){return;}			
			if(lastRadiusExtra!=radiusExtra || lastHeightExtra!=heightExtra || lastThicknessExtra!=thicknessExtra)
			{
				lastRadiusExtra = radiusExtra;
				lastHeightExtra = heightExtra;
				lastThicknessExtra = thicknessExtra;
				recreateModel();				
				updatePhysicalAttributes();
				updateAttachNodePositions();
				updateDragCube();
				updateDecouplerForce();
				updateGuiState();
			}
		}

		#endregion

		#region model updating/generation/regeneration

		//DONE
		public void prepModel()
		{
			if (model != null)
			{
				return;
			}
			Transform tr = part.FindModelTransform("model");			
			SSTUUtils.destroyChildren(tr);//remove the original empty proxy model
			model = new ProceduralCylinderModel ();
			setModelParameters ();
			model.setMaterial (SSTUUtils.loadMaterial (diffuseTextureName, normalTextureName));
			model.setMeshColliderStatus (true, false);
			model.createModel ();
			model.setParent(tr);
			updatePhysicalAttributes ();
			updateDecouplerForce ();
			updateDragCube ();
			resetHighlighter();
		}

		//DONE
		public void recreateModel()
		{
			setModelParameters ();
			model.recreateModel ();
			updatePhysicalAttributes ();
			updateDecouplerForce ();
			updateDragCube ();
			resetHighlighter();
		}
		
		//DONE
		private void setModelParameters()
		{
			float r = radius + (radiusExtra * radiusAdjust);
			float h = height + (heightExtra * heightAdjust);
			float t = thickness + (thicknessExtra * thicknessAdjust);
			model.setModelParameters (r, h, t, capHeight, maxPanelHeight, cylinderSides);
		}

		//DONE
		public void updateGuiState()
		{
			Events["increaseRadius"].guiActiveEditor = Events["decreaseRadius"].guiActiveEditor = canAdjustRadius;
			Events["increaseHeight"].guiActiveEditor = Events["decreaseHeight"].guiActiveEditor = canAdjustHeight;
			Events["increaseThickness"].guiActiveEditor = Events["decreaseThickness"].guiActiveEditor = canAdjustThickness;
			Fields ["radiusExtra"].guiActiveEditor = canAdjustRadius;
			Fields ["heightExtra"].guiActiveEditor = canAdjustHeight;
			Fields ["thicknessExtra"].guiActiveEditor = canAdjustThickness;
		}

		//DONE
		public void updateAttachNodePositions()
		{
			float h = (height + heightExtra * heightAdjust)/2f;
			AttachNode topNode = part.findAttachNode("top");
			if(topNode!=null)
			{
				topNode.position.y = h;
			}
			AttachNode bottomNode = part.findAttachNode("bottom");
			if(bottomNode!=null)
			{
				bottomNode.position.y = -h;
			}
		}

		//DONE
		public void updatePhysicalAttributes ()
		{			
			float r = radius + (radiusExtra * radiusAdjust);
			float h = height + (heightExtra * heightAdjust);
			float t = thickness + (thicknessExtra * thicknessAdjust);
			float innerCylVolume = 0;
			float outerCylVolume = 0;
			float innerCylRadius = (r) - (t);
			float outerCylRadius = (r);
			innerCylVolume = (float)Math.PI * innerCylRadius * innerCylRadius * h;
			outerCylVolume = (float)Math.PI * outerCylRadius * outerCylRadius * h;
			volume = outerCylVolume - innerCylVolume;
			modifiedMass = volume * massPerCubicMeter;
			modifiedCost = volume * costPerCubicMeter;
			if (HighLogic.LoadedSceneIsEditor)
			{
				GameEvents.onEditorShipModified.Fire (EditorLogic.fetch.ship);
			}
		}

		//TODO
		public void updateDragCube()
		{
			if (HighLogic.LoadedSceneIsEditor)
			{
				return;//NOOP in editor
			}
			//TODO - do drag cubes even matter for non-physics enabled parts?
			//TODO - basically just re-render the drag cube
		}

		//DONE
		private void updateDecouplerForce()
		{
			ejectionForce = forcePerKg * (modifiedMass / 1000f);
		}
		
		//TODO
		private void resetHighlighter()
		{
			if(part.highlighter!=null)
			{			
			//	part.highlighter.ReinitMaterials();
			}
		}

		#endregion
	}
}

