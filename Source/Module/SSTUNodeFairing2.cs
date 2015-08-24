using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
namespace SSTUTools
{

	//nodeFairing module config file layout
	//MODULE
	//{
	//	name = SSTUNodeFairing
	//  watchNode = true // if true, fairing will only spawn if the watched node has a part present
	//  nodeName = bottom  // fairing will spawn if part is present on this node
	//  canAdjustAttach = true // if true, user can adjust if fairing is attached to parent part or node-attached part
	//  attachedToNode = true // if true, fairing is attached to the node-attached part; if false, fairing is attached to parent part
	//  jettisonOnDetach = false // if true, fairing will be decoupled from whatever it is attached to whenever it is decoupled (will float free in space)
	//  canManuallyDeploy = true //if true, user can right click to jettison/deploy fairings, if false they will rely on node-attachment rules
	//  actionName = Jettison Panels // the right-click/action group display name for panel jettison action
	//  diffuseTextureName = <path to diffuse texture>
	//  normalTextureName = <path to normal texture>
	//  FAIRING
	//  {
	//  	name = Bottom
	//      topY = 0
	//      bottomY = -1
	//      wallThickness = 0.025
	//      capSize = 0.1
	//      numOfSections = 4
	//      cylinderSides = 24
	//      topRadius = 0.625
	//      bottomRadius = 0.625
	//      canAdjustTopRadius = false
	//      canAdjustBottomRadius = true
	//      rotationOffset = 0,0,0
	//      jettisonDirection = 0,0,1
	//      jettsionForce = 1
	//      fairingMass = 1
	//      maxPanelHeight = 1
	//  }
	//  FAIRING
	//  {
	//  	name = Bottom2
	//      topY = -1
	//      bottomY = -2
	//      wallThickness = 0.025
	//      capSize = 0.1
	//      numOfSections = 4
	//      cylinderSides = 24
	//      topRadius = 0.625
	//      bottomRadius = 0.625
	//      canAdjustTopRadius = false
	//      canAdjustBottomRadius = true
	//      rotationOffset = 0,0,0
	//      jettisonDirection = 0,0,1
	//      jettsionForce = 1
	//      fairingMass = 1
	//      maxPanelHeight = 1
	//  }
	//}

	public class SSTUNodeFairing2 : PartModule, IAirstreamShield
	{

		#region KSP Part Module config vars for entire fairing
		
		[KSPField]
		public string diffuseTextureName = "UNKNOWN";
		
		[KSPField]
		public string normalTextureName = "UNKNOWN";
		
		//if true fairing will only be spawned when a part is attached to the specified node in nodeName
		//else fairing will always be present until/unless discarded by other means (manual if enabled, or attached to node, or jettison on decouple)
		[KSPField]
		public bool watchNode = true;

		//watch this node, if a part is attached, spawn the fairing
		//only enabled if 'watchNode==true'
		[KSPField]
		public string nodeName = "bottom";

		//CSV list of transform names to disable renders on (to override stock ModuleJettison mechanics) - should also MM patch remove the ModuleJettsion from the part...
		[KSPField]
		public string rendersToRemove = string.Empty;

		//if manual deploy is enabled, this will be the button/action group text
		[KSPField]
		public string actionName = "Jettison Panels";

		[KSPField]
		public bool canManuallyDeploy = false;

		[KSPField(isPersistant=true)]
		public bool fairingEnabled = false;

		//should the fairing be attached to the watched node?
		//e.g. if attached and this part is decoupled from that node, the fairing will stay attached to the -other- part, e.g. stock engine fairing behavior
		//can have the fairing decouple from both this part and watched node (as debris), by setting jettisonOnDetach to true and attachedToNode to false
		[UI_Toggle (disabledText = "False", enabledText = "True"), KSPField (guiName = "Attached to Node", isPersistant = true, guiActiveEditor = true)]
		public bool attachedToNode = true;
		
		//is this fairing jettisoned when the watched node is detached? only functions if attachedToNode=false
		[UI_Toggle (disabledText = "False", enabledText = "True"), KSPField (guiName = "Jettison on Detach", isPersistant = true, guiActiveEditor = true)]
		public bool jettisonOnDetach = false;

		public float topRadiusAdjustSize = 0.625f;

		public float bottomRadiusAdjustSize = 0.625f;
			
		#endregion	

		#region fairing airstream shield vars
		[KSPField]
		public bool shieldParts = false;
		
		[KSPField]
		public float shieldTopY;
		
		[KSPField]
		public float shieldBottomY;
		
		[KSPField]
		public float shieldTopRadius;
		
		[KSPField]
		public float shieldBottomRadius;
		#endregion

		#region private working vars
		
		//the current fairing panels
		private FairingData[] fairingParts;
		private FairingData topAdjust;//if applicable, will be populated by the fairing whose top radius can be adjusted
		private FairingData bottomAdjust;//if applicable, will be populated by the fairing whose bottom radius can be adjusted

		//quick reference to the currently watched attach node, if any
		private AttachNode watchedNode;	

		//material used for procedural fairing, created from the texture references above
		private Material fairingMaterial;

		//list of shielded parts
		private List<Part> shieldedParts = new List<Part> ();

		//only marked public so that it can be serialized from prefab into instance parts
		[Persistent]
		public String configNodeString = String.Empty;

		private ConfigNode reloadedNode;
				
		#endregion

		#region gui actions

		[KSPAction("Jettison Fairing")]
		public void jettisonAction(KSPActionParam param)
		{
			onJettisonEvent ();
		}
		
		[KSPEvent(name="jettisonEvent", guiName="Jettison Panels", guiActive = true, guiActiveEditor = true)]
		public void jettisonEvent()
		{
			onJettisonEvent();
		}
		
		[KSPEvent (name= "increaseTopRadiusEvent", guiName = "Top Radius +", guiActiveEditor = true)]
		public void increaseTopRadiusEvent()
		{
			if (topRadius < topRadiusAdjust*8)
			{
				topRadius += topRadiusAdjust;
				if(topRadius>topRadiusAdjust*8){topRadius=topRadiusAdjust*8;}
				rebuildFairing();
			}
		}

		[KSPEvent (name= "decreaseTopRadiusEvent", guiName = "Top Radius -", guiActiveEditor = true)]
		public void decreaseTopRadiusEvent()
		{
			if (topRadius > topRadiusAdjust)
			{
				topRadius -= topRadiusAdjust;
				if(topRadius<topRadiusAdjust){topRadius=topRadiusAdjust;}
				rebuildFairing();
			}	
		}

		[KSPEvent (name= "increaseBottomRadiusEvent", guiName = "Bottom Radius +", guiActiveEditor = true)]
		public void increaseBottomRadiusEvent()
		{
			if (bottomRadius < bottomRadiusAdjust*8)
			{
				bottomRadius += bottomRadiusAdjust;
				if(bottomRadius>bottomRadiusAdjust*8){bottomRadius=bottomRadiusAdjust*8;}
				rebuildFairing();
			}
		}

		[KSPEvent (name= "decreaseBottomRadiusEvent", guiName = "Bottom Radius -", guiActiveEditor = true)]
		public void decreaseBottomRadiusEvent()
		{
			if (bottomRadius > bottomRadiusAdjust)
			{
				bottomRadius -= bottomRadiusAdjust;
				if(bottomRadius<bottomRadiusAdjust){bottomRadius=bottomRadiusAdjust;}
				rebuildFairing();
			}	
		}
		
		#endregion

		//TODO
		#region ksp overrides

		//TODO
		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			if(fairingMaterial==null)
			{
				loadMaterial();
			}
			//if prefab, load persistent config data into config node string
			if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
			{
				configNodeString = node.ToString ();
				//build fairings for prefab part?
			}
			else
			{
				reloadedNode = node;//not a prefab part, can persist the config node until needed (hopefully)
			}
		}

		//TODO
		public override void OnStart (StartState state)
		{			
			base.OnStart (state);
			if(fairingMaterial==null)
			{
				loadMaterial();
			}
			if(rendersToRemove.Length>0)
			{
				removeTransforms();
			}


			if (fairingEnabled)
			{
				buildFairing();
			}
						
			if(watchNode)
			{
				watchedNode = part.findAttachNode(nodeName);
				updateStatusFromNode();				
			}
			
			enableFairingRender(fairingEnabled);
			updateAttachedStatus();
			updateShieldStatus();
			
			Events["jettisonEvent"].guiName = actionName;
			if(!canManuallyDeploy || !fairingEnabled)
			{
				Events["jettisonEvent"].active = false;
				Actions["jettisonAction"].active = false;
			}
			Events["decreaseBottomRadiusEvent"].active = canAdjustBottomRadius;
			Events["increaseBottomRadiusEvent"].active = canAdjustBottomRadius;
			
			Events["decreaseTopRadiusEvent"].active = canAdjustTopRadius;
			Events["increaseTopRadiusEvent"].active = canAdjustTopRadius;
			
			Fields["jettisonOnDetach"].guiActiveEditor = canAdjustToggles;
			Fields["attachedToNode"].guiActiveEditor = canAdjustToggles;
						
			GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
			GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(onVesselModified));
			GameEvents.onVesselGoOffRails.Add(new EventData<Vessel>.OnEvent(onVesselUnpack));
			GameEvents.onVesselGoOnRails.Add(new EventData<Vessel>.OnEvent(onVesselPack));
			GameEvents.onPartDie.Add(new EventData<Part>.OnEvent(onPartDestroyed));
		}

		//DONE
		public void OnDestroy()
		{
			GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
			GameEvents.onVesselWasModified.Remove(new EventData<Vessel>.OnEvent(onVesselModified));
			GameEvents.onVesselGoOffRails.Remove(new EventData<Vessel>.OnEvent(onVesselUnpack));
			GameEvents.onVesselGoOnRails.Remove(new EventData<Vessel>.OnEvent(onVesselPack));
			GameEvents.onPartDie.Remove(new EventData<Part>.OnEvent(onPartDestroyed));
		}
				
		//TODO
		public void onVesselModified(Vessel v)
		{			
			//here we are trying to catch the case of a node-dependent fairing that is not node attached
			//we want these to instantly despawn when the vessel is decoupled/etc
			if(watchNode && fairingEnabled)
			{
				if(watchedNode==null || watchedNode.attachedPart==null)
				{					
					fairingEnabled = false;
					if( !attachedToNode && jettisonOnDetach)//fairing pieces are jettisoned individually
					{
						onJettison();					
					}
					else if(attachedToNode)//part attached to other node, it is already gone
					{
						part.mass -= fairingMass;	
					}
					updateDragCube();
				}
			}
			updateShieldStatus ();
		}

		public void onEditorVesselModified(ShipConstruct ship)
		{
			if(watchNode)
			{
				updateStatusFromNode();
				enableFairingRender(fairingEnabled);
				updateAttachedStatus();
				updateButtonStatus();
			}
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

		//TODO
		#region nodeUpdateMethods

		//TODO
		//if attachedToNode is enabled, this will re-parent the fairing panels to the part at that node when called		
		private void updateAttachedStatus()
		{
			if(HighLogic.LoadedSceneIsEditor){return;}
			if(attachedToNode && watchedNode!=null && watchedNode.attachedPart!=null)
			{
				fairingBase.root.transform.parent = watchedNode.attachedPart.transform;
			}
		}
		
		private void updateButtonStatus()
		{
			if(watchedNode!=null && watchedNode.attachedPart!=null)
			{
				//enable fairing size adjust buttons if they should be enabled
			}
			else
			{
				//disable fairing size adjust buttons
			}
		}
		
		//DONE
		private void updateStatusFromNode()
		{
			fairingEnabled = watchedNode!=null && watchedNode.attachedPart!=null;
		}
		#endregion

		//DONE FIRST PASS
		#region fairingJettison methods

		private void onJettisonEvent()
		{			
			if(HighLogic.LoadedSceneIsEditor)
			{
				fairingEnabled = false;
				enableFairingRender(false);
				Events["jettisonEvent"].active = false;
			}
			else if(HighLogic.LoadedSceneIsFlight)
			{
				if(fairingEnabled && canManuallyDeploy)
				{	
					fairingEnabled = false;
					onJettison();
					updateDragCube();
					updateShieldStatus();
				}
			}
		}

		private void onJettison()
		{	
			if (fairingEnabled)
			{
				fairingEnabled = false;
				Events["jettisonEvent"].active = false;
				foreach (FairingData fd in fairingParts)
				{
					fd.jettisonPanels(part);
					if(fd.removeMass)
					{
						part.mass -= fd.fairingJettisonMass;
					}
				}
			}
		}

		#endregion

		//TODO
		#region private utility methods

		//TODO
		private void enableFairingRender(bool val)
		{
			fairingEnabled = val;
			SSTUUtils.enableRenderRecursive(fairingBase.root.transform, val);
		}

		//DONE FIRST PASS
		private void rebuildFairing()
		{
			foreach (FairingData fd in fairingParts)
			{
				fd.destroyFairing();
			}
			buildFairing();
		}

		//DONE FIRST PASS
		private void buildFairing()
		{
			foreach (FairingData fd in fairingParts)
			{
				fd.createFairing(part, fairingMaterial);
			}
			updateDragCube();
			updateShieldStatus();
		}

		//DONE
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

		//DONE FIRST PASS
		private void updateDragCube()
		{
			//TODO
			if(part.DragCubes.Procedural)
			{
				//do nothing, let them update on procedural update ticks?
			}
			else
			{
				if(part.DragCubes.Cubes.Count>1)
				{
					//has multiple cubes; no clue...
					//TODO
					//ask modules to re-render their cubes?
				}
				else if(part.DragCubes.Cubes.Count==1)//has only one cube, update it!
				{
					DragCube c = part.DragCubes.Cubes[0];
					String name = c.Name;
					DragCube c2 = DragCubeSystem.Instance.RenderProceduralDragCube(part);
					c2.Name = name;					
					part.DragCubes.ClearCubes();
					part.DragCubes.ResetCubeWeights();
					part.DragCubes.Cubes.Add (c2);
					part.DragCubes.SetCubeWeight(name, 1.0f);//set the cube to full weight...
				}
			}
		}

		//DONE
		private void removeTransforms()
		{
			if(rendersToRemove!=null && rendersToRemove.Length>0)
			{
				Transform[] trs;
				String[] splitBits = rendersToRemove.Split(',');
				foreach(String name in splitBits)
				{
					trs = part.FindModelTransforms(name.Trim());
					foreach(Transform tr in trs)
					{
						print ("Removed transform from model: "+tr.name);
						tr.parent = null;
						GameObject.Destroy(tr.gameObject);						
					}
				}
			}
		}

		#endregion

		//DONE
		#region KSP AirstreamShield update methods
			
		//IAirstreamShield override
		public bool ClosedAndLocked(){return fairingEnabled;}
		
		//IAirstreamShield override
		public Vessel GetVessel(){return part.vessel;}
		
		//IAirstreamShield override
		public Part GetPart(){return part;}

		//DONE
		private void updateShieldStatus()
		{
			clearShieldedParts();
			if(shieldParts && fairingEnabled)
			{
				findShieldedParts();	
			}
		}

		//DONE
		private void clearShieldedParts()
		{
			if(shieldedParts.Count>0)
			{
				foreach(Part part in shieldedParts)
				{
					part.RemoveShield(this);
				}
				shieldedParts.Clear();
			}
		}

		//DONE
		private void findShieldedParts()
		{
			clearShieldedParts();		
			Bounds combinedBounds = SSTUUtils.getRendererBoundsRecursive(part.gameObject);//TODO verify this works as intended.... could have weird side-effects (originally was pulling render bounds for the fairing object)
			//TODO instead of using entire part render bounds... combine the render bounds of all fairingData represented by this module
			SSTUUtils.findShieldedPartsCylinder (part, combinedBounds, shieldedParts, shieldTopY, shieldBottomY, shieldTopRadius, shieldBottomRadius);
			for (int i = 0; i < shieldedParts.Count; i++)
			{
				shieldedParts[i].AddShield(this);
				print ("SSTUNodeFairing is shielding: "+shieldedParts[i].name);
			}
		}

		#endregion

	}

	//TODO
	//wrapper for an individual fairing part
	public class FairingData
	{
		public FairingBase theFairing;
		public Vector3 rotationOffset = Vector3.zero;
		public float topY = 1;
		public float bottomY = -1;
		public float capSize = 0.1f;
		public float wallThickness = 0.025f;
		public float maxPanelHeight = 1f;
		public int cylinderSides = 24;
		public int numOfSections = 1;
		public float topRadius = 0.625f;
		public float bottomRadius = 0.625f;
		public bool canAdjustTop = false;
		public bool canAdjustBottom = false;			
		public bool removeMass = true; //if true, fairing mass is removed from parent part when jettisoned (and on part reload)
		public float fairingJettisonMass = 0.1f;//mass of the fairing to be jettisoned; combined with jettisonForce this determines how energetically they are jettisoned
		public float jettisonForce = 10;//force in N to apply to jettisonDirection to each of the jettisoned panel sections
		public Vector3 jettisonDirection = new Vector3(0,-1,0);		

		//DONE
		//to be called on initial prefab part load; populate the instance with the default values from the input node
		public void load(ConfigNode node)
		{
			//TODO parse rotationOffset
			//TODO parse jettisonDirection
			rotationOffset = node.GetVector3 ("rotationOffset");
			topY = node.GetFloatValue("topY", topY);
			bottomY = node.GetFloatValue ("bottomY", bottomY);
			capSize = node.GetFloatValue ("capSize", capSize);
			wallThickness = node.GetFloatValue ("wallThickness", wallThickness);
			maxPanelHeight = node.GetFloatValue ("maxPanelHeight", maxPanelHeight);
			cylinderSides = node.GetIntValue ("cylinderSides", cylinderSides);
			numOfSections = node.GetIntValue ("numOfSections", numOfSections);
			topRadius = node.GetFloatValue ("topRadius", topRadius);
			bottomRadius = node.GetFloatValue ("bottomRadius", bottomRadius);
			canAdjustTop = node.GetBoolValue ("canAdjustTop", canAdjustTop);
			canAdjustBottom = node.GetBoolValue ("canAdjustBottom", canAdjustBottom);
			removeMass = node.GetBoolValue ("removeMass");
			fairingJettisonMass = node.GetFloatValue ("fairingJettisonMass");
			jettisonForce = node.GetFloatValue ("jettisonForce");
			jettisonDirection = node.GetVector3 ("jettisonDirection", jettisonDirection);
		}

		//DONE
		//to be called on part re-load (editor reload/revert, launch, in-flight reload)
		public void reload(ConfigNode node)
		{
			topRadius = node.GetFloatValue ("topRadius", topRadius);
			bottomRadius = node.GetFloatValue ("bottomRadius", bottomRadius);
		}

		//DONE
		//to be called by part OnSave method to persist any persistent data for the fairing
		//this will generally only be the top and bottom radius (everything else is static)
		//the passed in node is the RAW config node for the entire module; values for this fairing should
		//be added as sub-nodes
		public void savePersistence(ConfigNode node)
		{
			ConfigNode output = null;
			if (canAdjustTop)
			{
				output = new ConfigNode("FAIRING");
				output.AddValue("topRadius", topRadius);
			}
			if (canAdjustBottom)
			{
				if(output==null){output = new ConfigNode("FAIRING");}
				output.AddValue ("bottomRadius", bottomRadius);
			}
			if (output != null)
			{
				node.AddNode(output);
			}
		}

		//DONE
		public void createFairing(Part part, Material material)
		{
			float height = topY - bottomY;
			NodeFairingGenerator fg = new NodeFairingGenerator(-height*0.5f, capSize, height, maxPanelHeight, bottomRadius, topRadius, wallThickness, numOfSections, cylinderSides);
			FairingBase fb = fg.buildFairing();
			fb.root.transform.NestToParent(part.transform);
			fb.root.transform.position = part.transform.position;
			fb.root.transform.localPosition = new Vector3(0, topY - height*0.5f,0);
			fb.root.transform.rotation = part.transform.rotation;
			fb.root.transform.Rotate (rotationOffset);//TODO verify this works...
			fb.setMaterial(material);
			if(HighLogic.LoadedSceneIsEditor)
			{
				fb.setPanelOpacity(0.25f);
			}
			theFairing = fb;
		}

		//DONE
		public void destroyFairing()
		{
			if(theFairing!=null)
			{
				GameObject.Destroy(theFairing.root);
				theFairing=null;				
			}	
		}

		//DONE
		public void recreateFairing(Part part, Material material)
		{
			destroyFairing ();
			createFairing (part, material);
		}

		//DONE
		public void jettisonPanels(Part part)
		{
			theFairing.jettisonPanels (part, jettisonForce, jettisonDirection, fairingJettisonMass / (float)numOfSections);
		}

		//TODO
		public void enableRenders(bool enable)
		{

		}

		public void enablePanelColliders(bool enable, bool convex)
		{

		}

	}

}

