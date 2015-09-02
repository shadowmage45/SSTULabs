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

	public class SSTUNodeFairing : PartModule, IAirstreamShield
	{
		#region KSP Part Module config vars for entire fairing

		[KSPField(isPersistant=true, guiName = "Fairing Type", guiActiveEditor=true)]
		public string fairingType = FairingType.NODE_ATTACHED.ToString();

		[KSPField(isPersistant=true)]
		public bool fairingEnabled = false;
		
		[KSPField(isPersistant=true)]
		public bool jettisoned = false;

		[KSPField]
		public string diffuseTextureName = "UNKNOWN";
		
		[KSPField]
		public string normalTextureName = "UNKNOWN";

		//watch this node, if a part is attached, spawn the fairing
		//only enabled if 'watchNode==true'
		[KSPField]
		public string nodeName = "bottom";

		//CSV list of transform names to disable renders on (to override stock ModuleJettison mechanics) - should also MM patch remove the ModuleJettsion from the part...
		[KSPField]
		public string rendersToRemove = string.Empty;

		[KSPField]
		public string fairingName = "Fairing";

		//if manual deploy is enabled, this will be the button/action group text
		[KSPField]
		public string actionName = "Jettison";

		[KSPField]
		public bool canAdjustToggles = false;

		[KSPField]
		public float topRadiusAdjustSize = 0.625f;

		[KSPField]
		public float bottomRadiusAdjustSize = 0.625f;

		[KSPField]
		public float maxTopRadius = 5;

		[KSPField]
		public float minTopRadius = 0.625f;

		[KSPField]
		public float maxBottomRadius = 5;

		[KSPField]
		public float minBottomRadius = 0.625f;

		[KSPField(guiActive=true, guiActiveEditor=true, guiName="Shielded Part Count")]
		public int shieldedPartCount = 0;
			
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

		private FairingType typeEnum = FairingType.NODE_ATTACHED;

		private Part attachedPart = null;
		
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

		//DONE
		#region gui actions

		//DONE
		[KSPAction("Jettison Fairing")]
		public void jettisonAction(KSPActionParam param)
		{
			onJettisonEvent ();
		}

		//DONE
		[KSPEvent(name="jettisonEvent", guiName="Jettison Fairing", guiActive = true, guiActiveEditor = true)]
		public void jettisonEvent()
		{
			onJettisonEvent();
		}

		//DONE
		[KSPEvent (name= "increaseTopRadiusEvent", guiName = "Top Rad +", guiActiveEditor = true)]
		public void increaseTopRadiusEvent()
		{
			if (topAdjust != null && topAdjust.topRadius < maxTopRadius)
			{
				topAdjust.topRadius += topRadiusAdjustSize;
				if(topAdjust.topRadius>maxTopRadius){topAdjust.topRadius=maxTopRadius;}
				rebuildFairing();
			}
		}

		//DONE
		[KSPEvent (name= "decreaseTopRadiusEvent", guiName = "Top Rad -", guiActiveEditor = true)]
		public void decreaseTopRadiusEvent()
		{
			if (topAdjust != null && topAdjust.topRadius > minTopRadius)
			{
				topAdjust.topRadius -= topRadiusAdjustSize;
				if (topAdjust.topRadius < minTopRadius)	{topAdjust.topRadius = minTopRadius;}
				rebuildFairing();
			}	
		}

		//DONE
		[KSPEvent (name= "increaseBottomRadiusEvent", guiName = "Bottom Rad +", guiActiveEditor = true)]
		public void increaseBottomRadiusEvent()
		{
			if (bottomAdjust != null && bottomAdjust.bottomRadius < maxBottomRadius)
			{
				bottomAdjust.bottomRadius += bottomRadiusAdjustSize;
				if(bottomAdjust.bottomRadius>maxBottomRadius){bottomAdjust.bottomRadius=maxBottomRadius;}
				rebuildFairing();
			}
		}

		//DONE
		[KSPEvent (name= "decreaseBottomRadiusEvent", guiName = "Bottom Rad -", guiActiveEditor = true)]
		public void decreaseBottomRadiusEvent()
		{
			if (bottomAdjust != null && bottomAdjust.bottomRadius > minBottomRadius)
			{
				bottomAdjust.bottomRadius -= bottomRadiusAdjustSize;
				if(bottomAdjust.bottomRadius<minBottomRadius){bottomAdjust.bottomRadius=minBottomRadius;}
				rebuildFairing();
			}
		}	

		//DONE
		[KSPEvent (name= "changeTypeEvent", guiName = "Next Fairing Type", guiActiveEditor = true)]
		public void changeTypeEvent()
		{
			switch (typeEnum)
			{
			case FairingType.NODE_ATTACHED:
			{
				typeEnum = FairingType.NODE_DESPAWN;
				break;
			}
			case FairingType.NODE_DESPAWN:
			{
				typeEnum = FairingType.NODE_JETTISON;
				break;
			}
			case FairingType.NODE_JETTISON:
			{
				typeEnum = FairingType.NODE_ATTACHED;
				break;
			}
			//NOOP FOR MANUAL_JETTISON OR NODE_STATIC
			}
			fairingType = typeEnum.ToString ();//update enum state; it is only ever altered through this method, so this -should- keep things synched
			updateGuiState ();
		}

		#endregion

		//TODO - event callback methods; partDestroyed
		#region ksp overrides

		//DONE
		public override void OnSave (ConfigNode node)
		{
			base.OnSave (node);
			if(fairingParts==null || fairingParts.Length==0)
			{
				print ("ERROR, cannot save FairingData for part"+part.name+", no FairingData available to save.");
				return;
			}
			foreach (FairingData fd in fairingParts)
			{
				fd.savePersistence(node);
			}
		}

		//DONE
		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			//if prefab, load persistent config data into config node string
			if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
			{
				configNodeString = node.ToString ();
			}
			else
			{
				reloadedNode = node;//not a prefab part, can persist the config node until needed (hopefully)
			}

			//load the material...uhh...for use in prefab?? no clue why it is loaded here...probably some reason
			if(fairingMaterial==null)
			{
				loadMaterial();
			}

			loadFairingType ();
		}

		//DONE
		public override void OnStart (StartState state)
		{			
			base.OnStart (state);
			//remove any stock transforms for engine-fairing overrides
			if(rendersToRemove!=null && rendersToRemove.Length>0)
			{
				removeTransforms();
			}

			//load fairing material
			if(fairingMaterial==null)
			{
				loadMaterial();
			}

			//load FairingData instances from config values
			loadFairingData (SSTUNodeUtils.parseConfigNode(configNodeString));	

			//reload any previously persistent fairing-type data, e.g. from config for in-editor new parts (cannot rely on OnLoad for new parts)
			loadFairingType ();
			
			//construct fairing from loaded data
			buildFairing ();

			//initialize state for part -- mostly to handle case of new parts, but also to re-init properly for previously saved parts
			switch (typeEnum)
			{
				
			case FairingType.MANUAL_JETTISON:
			{
				//for manual fairings, they are always present unless already manually jettisoned
				fairingEnabled = !jettisoned;
				break;
			}
				
			case FairingType.NODE_DESPAWN:
			case FairingType.NODE_JETTISON:
			case FairingType.NODE_ATTACHED://auto, dependant on node and other part
			{
				//for standard auto-shroud fairings
				if(jettisoned)//already jettisoned previously, make sure fairing is set to disabled
				{
					fairingEnabled=false;
				}
				else//else examine node to see if it is attached
				{
					
					watchedNode = part.findAttachNode(nodeName);
					if(watchedNode==null || watchedNode.attachedPart==null)//fairing should be disabled; there is no lower-part to trigger it being present
					{
						fairingEnabled = false;
						if(!HighLogic.LoadedSceneIsEditor)//if not in editor, mark as jettisoned to remove mass from part
						{
							jettisoned=true;
						}
					}
					else//else has attached node and part, mark as fairingEnabled, and update parentage (if not in editor)
					{
						fairingEnabled=true;
						if(!HighLogic.LoadedSceneIsEditor)
						{
							updateFairingParent();
						}
					}
				}
				break;
			}
			case FairingType.NODE_STATIC:
			{
				watchedNode = part.findAttachNode(nodeName);
				attachedPart = null;
				if(HighLogic.LoadedSceneIsEditor)
				{
					fairingEnabled = watchedNode!=null && watchedNode.attachedPart!=null;
				}
				else//flight scene, leave it alone - let it use whatever stats were loaded from OnLoad / persistent data
				{
					//NOOP
				}
				break;
			}	
			}
			
			//enable renders for fairing panels based on if fairing is still enabled
			enableFairingRender(fairingEnabled);
			
			//if fairing enabled, update shielded status
			if (fairingEnabled)
			{
				updateShieldStatus ();
			}
			else if (jettisoned)//else if not enabled and jettisoned, remove fairing mass from parent part
			{
				removeFairingMass();
			}
			
			//update gui status for current fairing status (enable/disable buttons, update text labels)
			updateGuiState ();
				
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
				
		//DONE
		public void onVesselModified(Vessel v)
		{	
			if (vessel == null || v!=vessel || !fairingEnabled || jettisoned || typeEnum==FairingType.MANUAL_JETTISON)//previously handled, manual fairing or improper ref, nothing to do here
			{
				updateShieldStatus();
				updateGuiState();
			}
			else//node attached fairing, check if should jettison or despawn
			{
				if(watchedNode==null || watchedNode.attachedPart==null)//part should be jettisoned or despawned (or attached to other node still)
				{
					if(typeEnum==FairingType.NODE_JETTISON)//should jettison and float freely
					{
						jettisonFairing(true, true, true, false);
					}
					else if(typeEnum==FairingType.NODE_DESPAWN)//should immediately despawn
					{
						jettisonFairing(false, false, true, false);
					}
					else if(typeEnum==FairingType.NODE_ATTACHED)//should remain attached to other part, transfer mass to attached part
					{
						jettisonFairing(false, true, true, true);
					}
					else if(typeEnum==FairingType.NODE_STATIC)//if already present, should stay attached and rendered
					{
						//NOOP
					}
				}
				else//fairing should remain intact and attached...
				{
					//NOOP
				}
			}
			updateShieldStatus();
			updateGuiState();
		}

		//DONE
		public void onEditorVesselModified(ShipConstruct ship)
		{
			if (!jettisoned && typeEnum!=FairingType.MANUAL_JETTISON)//NOOP if already removed or of manual-jettison type
			{				
				fairingEnabled = watchedNode != null && watchedNode.attachedPart != null;
				enableFairingRender (fairingEnabled);
			}
			updateShieldStatus ();
			updateGuiState ();
		}

		//DONE
		public void onVesselUnpack(Vessel v)
		{			
			updateShieldStatus();
			updateGuiState ();
		}

		//DONE
		public void onVesselPack(Vessel v)
		{
			clearShieldedParts();
			updateGuiState ();
		}

		//TODO - how to best handle this?
		public void onPartDestroyed(Part p)
		{
			clearShieldedParts();				
			if(p!=part)
			{
				updateShieldStatus();
			}
			updateGuiState ();
		}

		#endregion

		//DONE
		#region fairingJettison methods

		//DONE
		private void onJettisonEvent()
		{	
			if (jettisoned)
			{
				print ("Cannot jettison already jettisoned fairing");
				return;
			}
			if (!fairingEnabled)
			{
				print ("Cannot jettison disabled fairing");
				return;
			}
			jettisonFairing (true, typeEnum != FairingType.NODE_DESPAWN, true, typeEnum==FairingType.NODE_ATTACHED);
		}

		//DONE
		private void jettisonFairing(bool jettisonPanels, bool renderPanels, bool removeMass, bool addMass)
		{
			if(jettisonPanels && HighLogic.LoadedSceneIsFlight)//only jettison panel parts if actually in flight
			{
				foreach (FairingData fd in fairingParts)
				{
					fd.jettisonPanels(part);
				}
			}
			jettisoned = true;
			fairingEnabled = false;
			enableFairingRender(renderPanels);
			if (removeMass)
			{
				removeFairingMass();
			}
			if (addMass && attachedPart!=null)
			{
				addFairingMass(attachedPart);
			}
			updateDragCube();
			updateShieldStatus();
			updateGuiState ();
		}

		//DONE
		private void removeFairingMass()
		{
			foreach (FairingData fd in fairingParts)
			{
				if(fd.removeMass)
				{
					part.mass -= fd.fairingJettisonMass;
				}
			}
		}

		//DONE
		private void addFairingMass(Part part)
		{
			if (part == null)
			{
				return;
			}
			foreach (FairingData fd in fairingParts)
			{
				if(fd.removeMass)
				{
					part.mass += fd.fairingJettisonMass;
				}
			}
		}

		#endregion

		//TODO updateDragCube
		#region private utility methods

		private void loadFairingType()
		{
			try
			{
				typeEnum = (FairingType)Enum.Parse (typeof(FairingType),fairingType);
			}
			catch(Exception e)
			{
				print (e.Message);
				fairingType = FairingType.NODE_ATTACHED.ToString();
				typeEnum = FairingType.NODE_ATTACHED;
			}
			print ("loaded fairing type of: "+typeEnum+" from string of: "+fairingType);
		}

		//DONE
		//creates/recreates FairingData instances from data from config node and any persistent node (if applicable)
		private void loadFairingData(ConfigNode node)
		{
			ConfigNode[] fairingNodes = node.GetNodes ("FAIRING");
			fairingParts = new FairingData[fairingNodes.Length];
			for (int i = 0; i < fairingNodes.Length; i++)
			{
				fairingParts[i] = new FairingData();
				fairingParts[i].load(fairingNodes[i]);
			}
			if (reloadedNode != null)
			{
				fairingNodes = reloadedNode.GetNodes ("FAIRING");
				for(int i = 0; i < fairingNodes.Length; i++)
				{
					fairingParts[i].reload(fairingNodes[i]);
				}
			}
		}

		//DONE
		//reparents the fairing panels to the part attached to the watched node, if any
		private void updateFairingParent()
		{
			if(HighLogic.LoadedSceneIsEditor){return;}
			attachedPart = null;
			if (watchedNode == null || watchedNode.attachedPart == null)
			{
				return;
			}
			attachedPart = watchedNode.attachedPart;
			if (typeEnum == FairingType.MANUAL_JETTISON || typeEnum == FairingType.NODE_STATIC)
			{
				return;
			}
			foreach (FairingData fd in fairingParts)
			{
				fd.theFairing.root.transform.parent = watchedNode.attachedPart.transform;
			}
		}
		
		//DONE
		//updates GUI labels and action availability based on current module state (jettisoned, watchedNode attached status, canAdjustRadius, etc)
		private void updateGuiState()
		{
			bool tEn = false;
			bool bEn = false;
			if (!jettisoned && fairingEnabled && (typeEnum==FairingType.MANUAL_JETTISON || (watchedNode != null && watchedNode.attachedPart != null)))
			{
				tEn = topAdjust != null;
				bEn = bottomAdjust != null;
			}
			else
			{
				tEn = false;
				bEn = false;
			}

			Events["decreaseTopRadiusEvent"].guiName = fairingName + " Top Rad -";
			Events["increaseTopRadiusEvent"].guiName = fairingName + " Top Rad +";
			Events["decreaseBottomRadiusEvent"].guiName = fairingName + " Bottom Rad -";
			Events["increaseBottomRadiusEvent"].guiName = fairingName + " Bottom Rad +";

			Events["decreaseTopRadiusEvent"].active = tEn;
			Events["increaseTopRadiusEvent"].active = tEn;
			Events["decreaseBottomRadiusEvent"].active = bEn;
			Events["increaseBottomRadiusEvent"].active = bEn;	

			Events["changeTypeEvent"].guiName = fairingName + " Type";

			Events["changeTypeEvent"].guiActiveEditor = fairingEnabled && canAdjustToggles && !jettisoned;
			
			Events["jettisonEvent"].guiName = actionName +" "+ fairingName;
			Actions["jettisonAction"].guiName = actionName +" "+ fairingName;

			Events["jettisonEvent"].active = !jettisoned && typeEnum==FairingType.MANUAL_JETTISON;
			Actions["jettisonAction"].active = !jettisoned && typeEnum==FairingType.MANUAL_JETTISON;
			
			Fields["shieldedPartCount"].guiActive = Fields["shieldedPartCount"].guiActiveEditor = shieldParts && !jettisoned;
			Fields["fairingType"].guiActiveEditor = canAdjustToggles;			
			
			shieldedPartCount = shieldedParts.Count;
		}

		//DONE
		private void enableFairingRender(bool val)
		{
			foreach (FairingData fd in fairingParts)
			{
				fd.enableRenders(val);
			}
		}

		//DONE
		private void rebuildFairing()
		{
			foreach (FairingData fd in fairingParts)
			{
				fd.destroyFairing();
			}
			buildFairing();
		}

		//DONE
		private void buildFairing()
		{
			topAdjust = null;
			bottomAdjust = null;
			foreach (FairingData fd in fairingParts)
			{
				fd.createFairing(part, fairingMaterial);
				if(fd.canAdjustTop){topAdjust = fd;}
				if(fd.canAdjustBottom){bottomAdjust = fd;}
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
			fairingMaterial = SSTUUtils.loadMaterial (diffuseTextureName, normalTextureName);
		}

		#warning need to finish drag cube update code for NodeFairing
		//TODO finish....
		private void updateDragCube()
		{
			if(part.DragCubes.Procedural)
			{
				//do nothing, let them update on procedural update ticks?
			}
			else
			{
				if(part.DragCubes.Cubes.Count>1)
				{
					//has multiple cubes; no clue...
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
						tr.parent = null;
						GameObject.Destroy(tr.gameObject);						
					}
				}
			}
		}

		#endregion

		//TODO - shielded part finding update for combined render bounds of fairing
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
			if(shieldParts && !jettisoned)
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

	public enum FairingType
	{
		MANUAL_JETTISON,//manually deployed fairing of any/all type.  always present until user jettisons (in editor or flight)
		NODE_ATTACHED,//watches node, only present if part is on node.  stays attached to -other- part
		NODE_JETTISON,//watches node, only present if part is on node.  jettisons to float freely when part detached (true interstage)
		NODE_DESPAWN,//watches node, only present if part is on node.  despawns when part attached to node is decoupled
		NODE_STATIC,//watches node, only present if part is on node.  remains present on parent part regardless of decoupled status
	}

	//wrapper for an individual fairing part
	public class FairingData
	{
		//gameObject storage class
		public FairingBase theFairing;

		public Vector3 rotationOffset = Vector3.zero;//default rotation offset is zero; must specify if custom rotation offset is to be used, not normally needed
		public float topY = 1;
		public float bottomY = -1;
		public float capSize = 0.1f;
		public float wallThickness = 0.025f;
		public float maxPanelHeight = 1f;
		public int cylinderSides = 24;//default is for 24 sided cylinders; must specify values for other cylinder sizes
		public int numOfSections = 1;//default is for a single segment fairing panel; must specify values for multi-part fairings
		public float topRadius = 0.625f;//default radius adjustment, only need to specify if other value is desired
		public float bottomRadius = 0.625f;//default radius adjustment, only need to specify if other value is desired
		public bool canAdjustTop = false;//must explicitly specify that radius can be adjusted
		public bool canAdjustBottom = false;//must explicitly specify that radius can be adjusted
		public bool removeMass = true; //if true, fairing mass is removed from parent part when jettisoned (and on part reload)
		public float fairingJettisonMass = 0.1f;//mass of the fairing to be jettisoned; combined with jettisonForce this determines how energetically they are jettisoned
		public float jettisonForce = 10;//force in N to apply to jettisonDirection to each of the jettisoned panel sections
		public Vector3 jettisonDirection = new Vector3(0,-1,0);//default jettison direction is negative Y (downwards)

		//DONE
		//to be called on initial prefab part load; populate the instance with the default values from the input node
		public void load(ConfigNode node)
		{
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
			removeMass = node.GetBoolValue ("removeMass", removeMass);
			fairingJettisonMass = node.GetFloatValue ("fairingJettisonMass", fairingJettisonMass);
			jettisonForce = node.GetFloatValue ("jettisonForce", jettisonForce);
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
			output = new ConfigNode("FAIRING");
			if (canAdjustTop)
			{
				output.AddValue("topRadius", topRadius);
			}
			if (canAdjustBottom)
			{
				output.AddValue ("bottomRadius", bottomRadius);
			}
			node.AddNode(output);
		}

		//DONE
		public void createFairing(Part part, Material material)
		{
			float height = topY - bottomY;
			CylinderMeshGenerator fg = new CylinderMeshGenerator(-height*0.5f, capSize, height, maxPanelHeight, bottomRadius, topRadius, wallThickness, numOfSections, cylinderSides);
			FairingBase fb = fg.buildFairing();
			fb.root.transform.NestToParent(part.transform);
			fb.root.transform.position = part.transform.position;
			fb.root.transform.localPosition = new Vector3(0, topY - height*0.5f,0);
			fb.root.transform.rotation = part.transform.rotation;
			//fb.root.transform.Rotate (rotationOffset);//TODO verify this works...
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
			if (theFairing != null)
			{
				theFairing.jettisonPanels (part, jettisonForce, jettisonDirection, fairingJettisonMass / (float)numOfSections);
			}
		}

		//TODO
		public void enableRenders(bool enable)
		{
			SSTUUtils.enableRenderRecursive (theFairing.root.transform, enable);
		}

		//TODO
		public void enablePanelColliders(bool enable, bool convex)
		{
			theFairing.enablePanelColliders (enable, convex);
		}

	}

}

