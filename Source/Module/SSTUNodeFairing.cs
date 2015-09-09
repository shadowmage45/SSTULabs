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

		//radius adjustment fields, mostly used in editor
		//these values are restored during the OnStart operation, and only used in the editor
		//the 'live' values for the fairing are stored persistently and used directly to update the
		//fairing physical attributes.
		//the 'live' values will be set from these values for further operations in the editor
		private float editorTopRadius = 0;
		private float editorBottomRadius = 0;
		private float lastTopExtra = 0;
		private float lastBottomExtra = 0;

		[KSPField(guiActiveEditor=true, guiName="Top Rad Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
		public float topRadiusExtra;	

		[KSPField(guiActiveEditor=true, guiName="Bot Rad Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
		public float bottomRadiusExtra;

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
			if (topAdjust != null && editorTopRadius < maxTopRadius)
			{
				editorTopRadius += topRadiusAdjustSize;
				if(editorTopRadius > maxTopRadius){editorTopRadius = maxTopRadius;}
				rebuildFairing();
			}
		}

		//DONE
		[KSPEvent (name= "decreaseTopRadiusEvent", guiName = "Top Rad -", guiActiveEditor = true)]
		public void decreaseTopRadiusEvent()
		{
			if (topAdjust != null && editorTopRadius > minTopRadius)
			{
				editorTopRadius -= topRadiusAdjustSize;
				if (editorTopRadius < minTopRadius)	{editorTopRadius = minTopRadius;}
				rebuildFairing();
			}	
		}

		//DONE
		[KSPEvent (name= "increaseBottomRadiusEvent", guiName = "Bottom Rad +", guiActiveEditor = true)]
		public void increaseBottomRadiusEvent()
		{
			if (bottomAdjust != null && editorBottomRadius < maxBottomRadius)
			{
				editorBottomRadius += bottomRadiusAdjustSize;
				if(editorBottomRadius>maxBottomRadius){editorBottomRadius=maxBottomRadius;}
				rebuildFairing();
			}
		}

		//DONE
		[KSPEvent (name= "decreaseBottomRadiusEvent", guiName = "Bottom Rad -", guiActiveEditor = true)]
		public void decreaseBottomRadiusEvent()
		{
			if (bottomAdjust != null && editorBottomRadius > minBottomRadius)
			{
				editorBottomRadius -= bottomRadiusAdjustSize;
				if(editorBottomRadius<minBottomRadius){editorBottomRadius=minBottomRadius;}
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

			restoreEditorFields ();

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
			if (topAdjust!=null &&  topRadiusExtra != lastTopExtra)
			{
				lastTopExtra = topRadiusExtra;
				rebuildFairing();
			}
			if (bottomAdjust!=null && bottomRadiusExtra != lastBottomExtra)
			{
				lastBottomExtra = bottomRadiusExtra;
				rebuildFairing();
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

		//DONE
		//restores the values to the editor size-adjust fields from the loaded values from the fairing
		private void restoreEditorFields()
		{
			foreach (FairingData fd in fairingParts)
			{
				if(fd.canAdjustTop)
				{
					topAdjust = fd;
				}
				if(fd.canAdjustBottom)
				{
					bottomAdjust = fd;
				}
			}
			float div, whole, extra;
			if (topAdjust != null)
			{
				div = topAdjust.topRadius / topRadiusAdjustSize;
				whole = (int)div;
				extra = div-whole;
				editorTopRadius = whole * topRadiusAdjustSize;
				topRadiusExtra = extra;
				lastTopExtra = topRadiusExtra;
			}
			if (bottomAdjust != null)
			{
				div = bottomAdjust.bottomRadius / bottomRadiusAdjustSize;
				whole = (int)div;
				extra = div-whole;
				editorBottomRadius = whole * bottomRadiusAdjustSize;
				bottomRadiusExtra = extra;
				lastBottomExtra = bottomRadiusExtra;
			}
		}

		//DONE
		private void updateFairingParameters()
		{
			if (topAdjust != null)
			{
				topAdjust.topRadius = editorTopRadius + (topRadiusExtra * topRadiusAdjustSize);			
			}
			if (bottomAdjust != null)
			{
				bottomAdjust.bottomRadius = editorBottomRadius + (bottomRadiusExtra * bottomRadiusAdjustSize);				
			}
		}

		//DONE
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

			Fields ["topRadiusExtra"].guiActiveEditor = tEn;
			Fields ["bottomRadiusExtra"].guiActiveEditor = bEn;

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
				if(fd.canAdjustTop)
				{
					topAdjust = fd;
				}
				if(fd.canAdjustBottom)
				{
					bottomAdjust = fd;
				}
			}
			updateFairingParameters();
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



}

