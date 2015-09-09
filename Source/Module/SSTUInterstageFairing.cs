using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace SSTUTools
{
	public class SSTUInterstageFairing : PartModule, IMultipleDragCube, IAirstreamShield, IPartCostModifier, IPartMassModifier
	{

		#region KSP MODULE fields
		//config fields for various transform and node names
		
		//reference to the model for this part; the texture and shader are retrieved from this model
		[KSPField]
		public String modelMeshName = "ISABase";
		
		[KSPField]
		public String topNodeName = "top";
		
		[KSPField]
		public String bottomNodeName = "bottom";
		
		[KSPField]
		public String internalNodeName = "internal";
		
		[KSPField]
		public String diffuseTextureName = "UNKNOWN";
		
		[KSPField]
		public String normalTextureName = "UNKNOWN";

		//how many sections should the fairing have radially?
		[KSPField]
		public int numOfRadialSections = 4;
			
		//height of the upper and lower caps; used to calculate node positions
		[KSPField]
		public float capHeight = 0.1f;

		//radius of the part, used to calculate mesh
		[KSPField(isPersistant=true)]
		public float bottomRadius = 1.25f;
		
		//radius of the top of the part, used to calculate mesh
		[KSPField(isPersistant=true)]
		public float topRadius = 1.25f;
		
		//stored current height of the panels, used to recreate mesh on part reload, may be set in config to set the default starting height
		[KSPField(isPersistant=true)]
		public float currentHeight = 1.0f;	
		
		//how tall is the decoupler base-cap
		[KSPField]
		public float baseHeight = 0.25f;
		
		[KSPField]
		public float boltPanelHeight = 0.075f;		
		
		[KSPField]
		public float wallThickness = 0.025f;
		
		[KSPField]
		public float maxPanelSectionHeight = 1.0f;
				
		[KSPField]
		public int cylinderSides = 24;
				
		//maximum height
		[KSPField]
		public float maxHeight = 15.0f;
		
		//minimum height
		[KSPField]
		public float minHeight = 1.0f;		

		//are planels deployed and upper node decoupled?
		//toggled to true as soon as deploy action is activated
		[KSPField(isPersistant=true)]
		public bool deployed = false;

		//is inner node decoupled?
		//toggled to true as soon as inner node is decoupled, only available after deployed=true
		[KSPField(isPersistant=true)]
		public bool decoupled = false;

		//how far should the panels be rotated for the 'deployed' animation
		[KSPField]
		public float deployedRotation = 60f;

		//how many degrees per second should the fairings rotate while deploy animation is playing?
		[KSPField]
		public float animationSpeed = 5f;

		//deployment animation persistence field
		[KSPField(isPersistant=true)]
		public float currentRotation = 0.0f;
		
		[KSPField(isPersistant=true)]
		public bool animating = false;
		
		[KSPField(guiActive=true, guiName="Parts Shielded", guiActiveEditor=true)]
		public int partsShielded = 0;
		
		[KSPField(guiName="Fairing Cost", guiActiveEditor=true)]
		public float fairingCost;		
		[KSPField(guiName="Fairing Mass", guiActiveEditor=true)]
		public float fairingMass;
	
		[KSPField]
		float costPerBaseVolume = 1500f;
		[KSPField]
		float costPerPanelArea = 50f;
		
		[KSPField]
		float massPerBaseVolume = 0.5f;
		[KSPField]
		float massPerPanelArea = 0.025f;

		[KSPField]
		float topRadiusAdjust = 0.625f;
		[KSPField]
		float bottomRadiusAdjust = 0.625f;
		[KSPField]
		float heightAdjust = 1;

		[KSPField(guiActiveEditor=true, guiName="Top Rad Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
		public float topRadiusExtra;
		
		[KSPField(guiActiveEditor=true, guiName="Bot Rad Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
		public float bottomRadiusExtra;
		
		[KSPField(guiActiveEditor=true, guiName="Height Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
		public float heightExtra;	
		
		#endregion
			
		#region private working variables

		private float editorTopRadius;
		private float editorBottomRadius;
		private float editorHeight;
		private float lastTopRadiusExtra;
		private float lastBottomRadiusExtra;
		private float lastHeightExtra;

		//quick references to part attach nodes
		private AttachNode lowerNode;
		private AttachNode upperNode;
		private AttachNode innerNode;
		
		//reference to the base transform for the display model for the part
		//used to parent the fairing root to this
		private Transform modelBase;
		
		//the current fairing object, contains the base, panels, and temporary editor-only colliders
		private FairingBase fairingBase;
		
		//temporary bounds collider used for 'put to ground' on vessel launch/unpack
		private BoxCollider boundsCollider;
		
		//material used for procedural fairing, created from the texture references above
		private Material fairingMaterial;
				
		//list of parts that are shielded from the airstream
		//rebuilt whenever vessel is modified
		private List<Part> shieldedParts = new List<Part>();
				
		//lerp between the two cubes depending upon deployed state
		//re-render the cubes on fairing rebuild
		private DragCube closedCube;
		private DragCube openCube;		
		
		#endregion

		#region KSP GUI Actions
		
		[KSPEvent (name= "increaseHeightEvent", guiName = "Increase Height", guiActiveEditor = true)]
		public void increaseHeightEvent()
		{
			if (editorHeight < maxHeight)
			{				
				float prevHeight = currentHeight;
				editorHeight += heightAdjust;
				updateFairingHeight(prevHeight);
			}
		}
		
		[KSPEvent (name= "decreaseHeightEvent", guiName = "Decrease Height", guiActiveEditor = true)]
		public void decreaseHeightEvent()
		{
			if (editorHeight > minHeight)
			{	
				float prevHeight = currentHeight;
				editorHeight -= heightAdjust;			
				updateFairingHeight(prevHeight);
			}
		}

		[KSPEvent (name= "deployEvent", guiName = "Deploy Panels", guiActive = true)]
		public void deployEvent()
		{
			onDeployEvent();
		}

		[KSPEvent (name= "decoupleEvent", guiName = "Decouple Inner Node", guiActive = true)]
		public void decoupleEvent()
		{
			onDecoupleEvent();
		}

		[KSPEvent (name= "increaseTopRadiusEvent", guiName = "Top Radius +", guiActiveEditor = true)]
		public void increaseTopRadiusEvent()
		{
			if (editorTopRadius < 5.0f)
			{
				editorTopRadius += topRadiusAdjust;
				if(editorTopRadius>5.0f){editorTopRadius=5.0f;}
				updateFairingHeight(currentHeight);
			}
		}

		[KSPEvent (name= "decreaseTopRadiusEvent", guiName = "Top Radius -", guiActiveEditor = true)]
		public void decreaseTopRadiusEvent()
		{
			if (editorTopRadius > topRadiusAdjust)
			{
				editorTopRadius -= topRadiusAdjust;
				if(topRadius < topRadiusAdjust){topRadius = topRadiusAdjust;}
				updateFairingHeight(currentHeight);
			}	
		}

		[KSPEvent (name= "increaseBottomRadiusEvent", guiName = "Bottom Radius +", guiActiveEditor = true)]
		public void increaseBottomRadiusEvent()
		{
			if (editorBottomRadius < 5.0f)
			{
				editorBottomRadius += bottomRadiusAdjust;
				if(editorBottomRadius>5.0f){editorBottomRadius=5.0f;}
				updateFairingHeight(currentHeight);
			}
		}

		[KSPEvent (name= "decreaseBottomRadiusEvent", guiName = "Bottom Radius -", guiActiveEditor = true)]
		public void decreaseBottomRadiusEvent()
		{
			if (editorBottomRadius > bottomRadiusAdjust)
			{
				editorBottomRadius -= bottomRadiusAdjust;
				if(editorBottomRadius<bottomRadiusAdjust){bottomRadius=bottomRadiusAdjust;}
				updateFairingHeight(currentHeight);
			}	
		}
		
		[KSPAction("Deploy and release")]
		public void deployAction(KSPActionParam param)
		{
			onDeployEvent();
		}
		
		[KSPAction("Decouple inner node")]
		public void decoupleAction(KSPActionParam param)
		{
			onDecoupleEvent();
		}
		
		#endregion
				
		#region KSP overrides
						
		public override void OnStart (PartModule.StartState state)
		{
			base.OnStart (state);
			upperNode = part.findAttachNode(topNodeName);
			lowerNode = part.findAttachNode(bottomNodeName);
			innerNode = part.findAttachNode(internalNodeName);				
						
			modelBase = part.FindModelTransform(modelMeshName);			
			SSTUUtils.enableRenderRecursive(modelBase, false);
			SSTUUtils.enableColliderRecursive(modelBase, false);
			
			if(boundsCollider!=null)
			{
				Component.Destroy(boundsCollider);
				boundsCollider = null;
			}
			
			restoreEditorFields();
			updateModelParameters();
						
			updateFairingHeight(currentHeight);//set nodes to current height, will not change any positions parts with a zero delta, but will still set nodes to proper height
						
			if(HighLogic.LoadedSceneIsFlight)//if in flight, selectively enable/disable the actions/gui events
			{
				Events ["deployEvent"].active = !deployed && !decoupled;//only available if not previously deployed or decoupled
				Events ["decoupleEvent"].active = deployed && !decoupled;//only available if deployed but not decoupled
				Actions ["deployAction"].active = !deployed && !decoupled;//only available if not previously deployed or decoupled
				Actions ["decoupleAction"].active = deployed && !decoupled;//only available if deployed but not decoupled				
				enableEditorColliders(false);
			}	
			
			//register for game events, used to notify when to update shielded parts
			GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
			GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(onVesselModified));
			GameEvents.onVesselGoOffRails.Add(new EventData<Vessel>.OnEvent(onVesselUnpack));
			GameEvents.onVesselGoOnRails.Add(new EventData<Vessel>.OnEvent(onVesselPack));
			GameEvents.onPartDie.Add(new EventData<Part>.OnEvent(onPartDestroyed));
		}
		
		public override void OnActive ()
		{
			base.OnActive ();
			if(!deployed)
			{
				onDeployEvent();				
			}
			else if(!decoupled)
			{
				onDecoupleEvent();
			}
		}
		
		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			restoreEditorFields();
			updateModelParameters();
			if(HighLogic.LoadedSceneIsFlight)
			{
				boundsCollider = part.gameObject.AddComponent<BoxCollider>();
				boundsCollider.center = Vector3.zero;//part.transform.localPosition;
				float diameter = bottomRadius > topRadius? bottomRadius : topRadius;
				boundsCollider.size = new Vector3(diameter, currentHeight+baseHeight, diameter);
			}
					
			loadMaterial();//reload the material, to catch a case where the material name differs from the prefab config
		}
				
		public override void OnAwake ()
		{
			base.OnAwake ();		
			//loads the material immediately after part init; this will get whatever name was specified in the config file initially/from the prefab
			//only load once, as OnAwake() is called multiple times
			if(fairingMaterial==null)
			{
				loadMaterial();
			}
		}
		
		public void OnDestroy()
		{
	    	GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
			GameEvents.onVesselWasModified.Remove(new EventData<Vessel>.OnEvent(onVesselModified));
			GameEvents.onVesselGoOffRails.Remove(new EventData<Vessel>.OnEvent(onVesselUnpack));
			GameEvents.onVesselGoOnRails.Remove(new EventData<Vessel>.OnEvent(onVesselPack));
			GameEvents.onPartDie.Remove(new EventData<Part>.OnEvent(onPartDestroyed));
		}
		
		//Unity updatey cycle override/hook
		public void FixedUpdate()
		{
			if (animating)
			{
				updateAnimation();
			}
		}
		
		//IMultipleDragCube override
		public string[] GetDragCubeNames ()
		{
			return new string[]
			{
				"Open",
				"Closed"
			};
		}
		
		//IMultipleDragCube override
		public void AssumeDragCubePosition (string name)
		{
			if("Open".Equals(name))
			{
				setPanelRotations(deployedRotation);
			}
			else
			{
				setPanelRotations(0);
			}
		}
		
		//IMultipleDragCube override
		public bool UsesProceduralDragCubes ()
		{
			return false;
		}
		
		//IAirstreamShield override
		public bool ClosedAndLocked(){return !deployed;}
		
		//IAirstreamShield override
		public Vessel GetVessel(){return part.vessel;}
		
		//IAirstreamShield override
		public Part GetPart(){return part;}
		
		//IPartCostModifier override
		public float GetModuleCost(float cost){return fairingCost;}
		
		//IPartMassModifier override
		public float GetModuleMass(float mass){return part.mass;}	
				
		#endregion
		
		#region KSP Game Event callback methods
		
		public void onEditorVesselModified(ShipConstruct ship)
		{
			if(lastTopRadiusExtra != topRadiusExtra || lastBottomRadiusExtra != bottomRadiusExtra || lastHeightExtra != heightExtra)
			{
				float h = currentHeight;
				updateModelParameters();
				updateFairingHeight(h);
			}
			updateShieldStatus();
			setPanelOpacity(0.25f);
		}
		
		public void onVesselModified(Vessel v)
		{
			updateShieldStatus();
		}
		
		public void onVesselUnpack(Vessel v)
		{			
			updateShieldStatus();
		}
		
		public void onVesselPack(Vessel v)
		{
			clearShieldedParts();
		}
		
		public void onPartDestroyed(Part p)
		{
			clearShieldedParts();				
			if(p!=part)
			{
				updateShieldStatus();
			}
		}
		
		#endregion
		
		#region private action handling methods
		
		private void onDeployEvent()
		{
			if(!deployed)
			{
				animating = true;
				deployed = true;
				Events["deployEvent"].active = false;
				Actions["deployAction"].active = false;
				Events["decoupleEvent"].active = true;
				Actions["decoupleAction"].active = true;
				decoupleNode(part.findAttachNode(topNodeName));
				updateShieldStatus();
			}
		}
		
		private void onDecoupleEvent()
		{
			if(deployed && !decoupled)
			{
				decoupled = true;				
				Events["deployEvent"].active = false;
				Actions["deployAction"].active = false;
				Events["decoupleEvent"].active = false;
				Actions["decoupleAction"].active = false;
				decoupleNode(part.findAttachNode(internalNodeName));
			}
		}
		
		private void decoupleNode(AttachNode node)
		{
			Part attachedPart = node.attachedPart;
			if(attachedPart==null){return;}
			if (attachedPart == part.parent)
			{				
				part.decouple (0f);
			}
			else
			{
				attachedPart.decouple (0f);
			}	
		}
				
		#endregion
		
		#region fairing data update methods
		
		private void setPanelRotations(float rotation)
		{
			if(fairingBase!=null)
			{
				fairingBase.setPanelRotations(rotation);				
			}
		}
		
		private void setPanelOpacity(float val)
		{			
			if(fairingBase!=null){fairingBase.setPanelOpacity(val);}
		}
		
		private void updateAnimation()
		{
			float delta = TimeWarp.fixedDeltaTime * animationSpeed;
			float previousAngle = currentRotation;
			currentRotation += delta;
			if(currentRotation >= deployedRotation)
			{
				currentRotation = deployedRotation;
				animating = false;
				updateShieldStatus();
			}
			setPanelRotations(currentRotation);
			updateDragCube();
		}
				
		private void updateDragCube()
		{
			float percentDeployed = currentRotation / deployedRotation;
			part.DragCubes.SetCubeWeight("Open", percentDeployed);
			part.DragCubes.SetCubeWeight("Closed", 1f - percentDeployed);
		}
				
		private void enableEditorColliders(bool val)
		{
			if(fairingBase.editorColliders!=null)
			{
				SSTUUtils.enableColliderRecursive(fairingBase.editorColliders.transform, val);
			}
		}
		
		#endregion
		
		#region fairing rebuild methods

		private void restoreEditorFields()
		{
			float div = topRadius / topRadiusAdjust;
			float whole = (int)div;
			float extra = div - whole;
			editorTopRadius = whole * topRadiusAdjust;
			topRadiusExtra = extra;
			lastTopRadiusExtra = extra;

			div = bottomRadius / bottomRadiusAdjust;
			whole = (int)div;
			extra = div - whole;
			editorBottomRadius = whole * bottomRadiusAdjust;
			bottomRadiusExtra = extra;
			lastBottomRadiusExtra = extra;

			div = currentHeight / heightAdjust;
			whole = (int)div;
			extra = div - whole;
			editorHeight = whole * heightAdjust;
			heightExtra = extra;
			lastHeightExtra = extra;
		}

		private void updateModelParameters()
		{
			lastTopRadiusExtra = topRadiusExtra;
			lastBottomRadiusExtra = bottomRadiusExtra;
			lastHeightExtra = heightExtra;				
			topRadius = editorTopRadius + (topRadiusExtra * topRadiusAdjust);
			bottomRadius = editorBottomRadius + (bottomRadiusExtra * bottomRadiusAdjust);
			currentHeight = editorHeight + (heightExtra * heightAdjust);
//			print ("updated model params. rad: "+topRadius+"::"+bottomRadius+" hi: "+currentHeight);
		}

		private void updateFairingHeight(float prevHeight)
		{
			recreateFairing();
			updateNodePositions(prevHeight);
			if (HighLogic.LoadedSceneIsEditor)//only adjust part positions in editor				
			{					
				float heightDiff = currentHeight - prevHeight;
				updateFairingPartPosition(heightDiff);
				adjustAttachedPartPositions(heightDiff);
			}
			recreateDragCubes();
			updateShieldStatus();
			
			if(HighLogic.LoadedSceneIsEditor)
			{
				GameEvents.onEditorPartEvent.Fire(ConstructionEventType.PartTweaked, part);
								
				EditorLogic el = EditorLogic.fetch;				
				if(el!=null)
				{
					if(el.ship!=null)
					{						
						GameEvents.onEditorShipModified.Fire(el.ship);
					}
				}
			}
		}
		
		private void recreateFairing()
		{
			destroyPanels();
			updateModelParameters ();
			createPanels();
			setPanelRotations(currentRotation);//set animation status to whatever is current
			fairingBase.enablePanelColliders(false, false);
			updateFairingMassAndCost();
		}
		
		//destroy any procedurally created panel sections
		private void destroyPanels()
		{
			if(fairingBase==null){return;}
			fairingBase.root.transform.parent = null;
			GameObject.Destroy(fairingBase.root);
			fairingBase = null;
		}

		//create procedural panel sections for the current part configuration (radialSection count), with orientation set from base panel orientation
		private void createPanels() 
		{			
			float totalHeight = baseHeight + currentHeight;
			float startHeight = - (totalHeight / 2);

			float tRad, bRad, height;
			tRad = topRadius;
			bRad = bottomRadius;
			height = currentHeight;

			InterstageFairingGenerator fg = new InterstageFairingGenerator(startHeight, baseHeight, boltPanelHeight, height, maxPanelSectionHeight, bRad, tRad, wallThickness, numOfRadialSections, cylinderSides);
			fairingBase = fg.buildFairing ();
			Transform modelTransform = part.partTransform.FindChild("model");
			fairingBase.root.transform.NestToParent(modelTransform);
			fairingBase.root.transform.rotation = modelTransform.rotation;		
			fairingBase.setMaterial(fairingMaterial);			
			if(HighLogic.LoadedSceneIsEditor){setPanelOpacity(0.25f);}
			else{setPanelOpacity(1.0f);}
		}

		private void recreateDragCubes()
		{
			setPanelRotations(deployedRotation);
			this.openCube = DragCubeSystem.Instance.RenderProceduralDragCube (part);
			setPanelRotations(0);
			this.closedCube = DragCubeSystem.Instance.RenderProceduralDragCube (part);
			this.closedCube.Name = "Closed";
			this.openCube.Name = "Open";
			part.DragCubes.ClearCubes ();
			part.DragCubes.Cubes.Add (closedCube);
			part.DragCubes.Cubes.Add (openCube);
			part.DragCubes.ResetCubeWeights ();
			updateDragCube();
		}
		
		private void updateFairingMassAndCost()
		{						
			float baseVolume = bottomRadius * bottomRadius * baseHeight * Mathf.PI;
			float avgRadius = bottomRadius + (topRadius - bottomRadius) * 0.5f;
			float panelArea = avgRadius * 2f * Mathf.PI * currentHeight;
			
			float baseCost = costPerBaseVolume * baseVolume;
			float panelCost = costPerPanelArea * panelArea;
			float baseMass = massPerBaseVolume * baseVolume;
			float panelMass = massPerPanelArea * panelArea;
			
			fairingCost = panelCost + baseCost;
			fairingMass = panelMass + baseMass;
			
			part.mass = fairingMass;
		}
		
		#endregion
		
		#region editor position update methods
		
		private void updateNodePositions(float prevHeight)
		{
			float halfDistance = currentHeight + baseHeight;
			halfDistance*=0.5f;
			upperNode.position.y = halfDistance;
			upperNode.originalPosition.y = halfDistance;
			lowerNode.position.y = -halfDistance;
			lowerNode.originalPosition.y = -halfDistance;
			innerNode.position.y = lowerNode.position.y + baseHeight;
			innerNode.originalPosition.y = innerNode.position.y;
		}
		
		private void updateFairingPartPosition(float heightDiff)
		{
			if(part.parent==null)//is root
			{
				//do not adjust part position
			}
			else//not root; do not adjust node that is attached via (only move parts attached to THIS)
			{
				//find what node the part is attached to
				if(upperNode.attachedPart==part.parent)//parent part is upper node
				{
					//move part down by height diff/2
					part.transform.localPosition = part.transform.localPosition + new Vector3(0, -heightDiff/2, 0);
				}
				else if(lowerNode.attachedPart==part.parent || innerNode.attachedPart==part.parent)
				{
					//move part up by height diff/2
					part.transform.localPosition = part.transform.localPosition + new Vector3(0, heightDiff/2, 0);
				}
			}
		}

		private void adjustAttachedPartPositions(float heightDiff)
		{
			if(part.parent==null)//is root
			{
				//update position of all attached parts by half panel height, lower and inner move downwards, upper moves upwards
				//do not change position of surface attached parts
				//if(upperNode.attachedPart!=null){upperNode.attachedPart}
				if(lowerNode.attachedPart!=null)
				{
					adjustAttachedPartPos(lowerNode.attachedPart, -heightDiff*0.5f);
				}
				if(innerNode.attachedPart!=null)
				{
					adjustAttachedPartPos(innerNode.attachedPart, -heightDiff*0.5f);
				}
				if(upperNode.attachedPart!=null)
				{
					adjustAttachedPartPos(upperNode.attachedPart, heightDiff*0.5f);
				}
			}
			else//not root; do not adjust node that is attached via (only move parts attached to THIS)
			{
				//find what node the part is attached to
				if(upperNode.attachedPart==part.parent)//parent part is upper node
				{
					//move inner and lower node parts down by panel height
					if(lowerNode.attachedPart!=null)
					{
						adjustAttachedPartPos(lowerNode.attachedPart, -heightDiff*0.5f);
					}
					if(innerNode.attachedPart!=null)
					{
						adjustAttachedPartPos(innerNode.attachedPart, -heightDiff*0.5f);
					}
				}
				else if(lowerNode.attachedPart==part.parent || innerNode.attachedPart==part.parent)
				{
					//move upper node parts up by panel height
					if(upperNode.attachedPart!=null)
					{
						adjustAttachedPartPos(upperNode.attachedPart, heightDiff*0.5f);
					}
					//adjust the non-parent node part position, as it will be offset when this.part.position is moved
					if(lowerNode.attachedPart==part.parent && innerNode.attachedPart!=null)
					{
						//adjust downwards, as it will be moved by the update to this.part.position
						adjustAttachedPartPos(innerNode.attachedPart, -heightDiff*0.5f);
					}
					else if(innerNode.attachedPart==part.parent && lowerNode.attachedPart!=null)
					{
						//adjust downwards, as it will be moved by the update to this.part.position
						adjustAttachedPartPos(lowerNode.attachedPart, -heightDiff*0.5f);
					}
				}
			}
		}
		
		//update input part position by the passed in amount
		private void adjustAttachedPartPos(Part otherPart, float moveAmount)
		{			
			otherPart.transform.position = otherPart.transform.position + part.transform.up * (moveAmount * part.rescaleFactor);
			otherPart.attPos0 = otherPart.attPos0 + part.transform.up * (moveAmount * part.rescaleFactor);
		}
		
		#endregion

		#region shield update methods
		
		private void updateShieldStatus()
		{
			clearShieldedParts();
			if(!deployed && upperNode.attachedPart!=null)
			{
				findShieldedParts();				
			}
		}
		
		private void clearShieldedParts()
		{
			partsShielded = 0;
			if(shieldedParts.Count>0)
			{
				foreach(Part part in shieldedParts)
				{
					part.RemoveShield(this);
				}
				shieldedParts.Clear();
			}
		}
						
		private void findShieldedParts()
		{
			if(shieldedParts.Count>0)
			{
				clearShieldedParts();
			}
			if(upperNode.attachedPart==null)//nothing on upper node to do the shielding...
			{
				return;
			}
					
			float totalHeight = currentHeight + baseHeight;
			float topY = (totalHeight * 0.5f);
			float bottomY = -(totalHeight * 0.5f) + baseHeight;
			
			Bounds combinedBounds = SSTUUtils.getRendererBoundsRecursive(fairingBase.root);
			SSTUUtils.findShieldedPartsCylinder (part, combinedBounds, shieldedParts, topY, bottomY, topRadius, bottomRadius);
			
			for (int i = 0; i < shieldedParts.Count; i++)
			{
				shieldedParts[i].AddShield(this);
				print ("SSTUInterstageFairing is shielding:"+shieldedParts[i].name);
			}
			partsShielded = shieldedParts.Count;
		}
		
		#endregion
		
		#region private helper methods
		
		private void loadMaterial()
		{
			if(fairingMaterial!=null)
			{
				Material.Destroy(fairingMaterial);
				fairingMaterial = null;
			}
			Texture diffuseTexture = SSTUUtils.findTexture(diffuseTextureName);
			Texture normalTexture = SSTUUtils.findTexture(normalTextureName);
			Shader shader = Shader.Find("KSP/Bumped Specular");					
			fairingMaterial = new Material(shader);
			fairingMaterial.SetTexture("_MainTex", diffuseTexture);
			fairingMaterial.SetTexture ("_BumpMap", normalTexture);
		}
		
		#endregion
	}
}

