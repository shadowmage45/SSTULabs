using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
namespace SSTUTools
{

	public class SSTUNodeFairing : PartModule, IAirstreamShield
	{

		#region KSP Part Module config vars
		
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
		
		//if true, enabled gui toggles for attachToNode and jettisonOnDetach
		[KSPField]
		public bool canAdjustToggles = false;

		//should the fairing be attached to the watched node?
		//e.g. if attached and this part is decoupled from that node, the fairing will stay attached to the -other- part, e.g. stock engine fairing behavior
		//can have the fairing decouple from both this part and watched node (as debris), by setting jettisonOnDetach to true and attachedToNode to false
		[UI_Toggle (disabledText = "False", enabledText = "True"), KSPField (guiName = "Attached to Node", isPersistant = true, guiActiveEditor = true)]
		public bool attachedToNode = true;
		
		//is this fairing jettisoned when the watched node is detached? only functions if attachedToNode=false
		[UI_Toggle (disabledText = "False", enabledText = "True"), KSPField (guiName = "Jettison on Detach", isPersistant = true, guiActiveEditor = true)]
		public bool jettisonOnDetach = false;
		
		//does this fairing have a right-click action to manually deploy the fairing?
		[KSPField]
		public bool canManuallyDeploy = false;
		
		//if manual deploy is enabled, this will be the button/action group text
		[KSPField]
		public string actionName = "Jettison Panels";
		
		//the direction relative to the fairing panel for jettisoning
		//y is up
		//z is outward for multi-panel setups, indetermined for single panel/cylindrical fairings
		//x is left (looking outward) for multi-panel setups, indetermined for single panel/cylindrical fairings
		[KSPField]
		public Vector3 jettisonDirection = new Vector3(0,-1,0);
		
		//the force that should be added to the fairing part(s) on jettison
		[KSPField]
		public float jettisonForce = 1.0f;
		
		//the total mass of all fairing panels; subtracted from part mass on jettison or detach
		[KSPField]
		public float fairingMass = 0.10f;
		
		//TODO
		//if true, this fairing will shield any -other- parts within its bounds
		//e.g. for use on service-module side panels
		[KSPField]
		public bool shieldsParts = false;
			
		//TODO
		//if not empty, these tranform names will be removed from the model entirely, e.g. for removing existing model-based fairing panels from stock engines
		//accepts a csv list of transform names
		[KSPField]
		public string rendersToRemove = string.Empty;
				
		#endregion
				
		#region procedural generation fields
		
		//how many panels make up this fairing? e.g. one for normal engine fairings, 2 for NERVA fairing, more for custom uses
		[KSPField]
		public int fairingSections = 1;
		
		//in node-relative coordinates, what is the top of the fairing?
		[KSPField]
		public float fairingTopY = -1.47298f;
		//in node-relative coordinates, what is the bottom of the fairing?
		[KSPField]
		public float fairingBottomY = -1.891f;
		
		//top and bottom radius, persistant in case of changes
		[KSPField(isPersistant=true)]
		public float topRadius = 2.1f;
		[KSPField(isPersistant=true)]
		public float bottomRadius = 1.875f;
		
		//height of the black bolt panel, set to 0 for none
		[KSPField]
		public float capSize = 0.1f;
		//max height of each vertical section; if the fairing is taller than this the texture will be repeated
		[KSPField]
		public float maxPanelHeight = 1.0f;
		//thickness of fairing panels
		[KSPField]
		public float wallThickness = 0.025f;
		//how many sides on a complete fairing cylinder
		[KSPField]
		public int cylinderSides = 24;

		//radius adjust control fields		
		[KSPField]
		public bool canAdjustTopRadius = false;
		
		[KSPField]
		public bool canAdjustBottomRadius = false;
		
		[KSPField]
		public float topRadiusAdjust = 0.625f;
		
		[KSPField]
		public float bottomRadiusAdjust = 0.625f;
				
		#endregion
		
		#region persistance variables		
		
		//persistent field to track if fairing should be rebuilt on part reload (for manual deploy non-node related fairings)
		[KSPField(isPersistant=true)]
		public bool fairingEnabled = true;
		
		#endregion

		#region private working vars
		
		//the current fairing panels
		private FairingBase fairingBase;
		//quick reference to the currently watched attach node, if any
		private AttachNode watchedNode;	
		//material used for procedural fairing, created from the texture references above
		private Material fairingMaterial;
		//list of shielded parts
		private List<Part> shieldedParts = new List<Part> ();
				
		#endregion

		public SSTUNodeFairing ()
		{
		}
		
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
		
		#region ksp overrides
		
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
		
		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			
			loadMaterial();
		}
		
		public override void OnStart (StartState state)
		{			
			base.OnStart (state);
			
			if(rendersToRemove.Length>0)
			{
				removeTransforms();
			}
						
			buildFairing();
			
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
				
		public void OnDestroy()
		{
			GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
			GameEvents.onVesselWasModified.Remove(new EventData<Vessel>.OnEvent(onVesselModified));
			GameEvents.onVesselGoOffRails.Remove(new EventData<Vessel>.OnEvent(onVesselUnpack));
			GameEvents.onVesselGoOnRails.Remove(new EventData<Vessel>.OnEvent(onVesselPack));
			GameEvents.onPartDie.Remove(new EventData<Part>.OnEvent(onPartDestroyed));
		}
				
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

		//IAirstreamShield override
		public bool ClosedAndLocked(){return fairingEnabled;}
		
		//IAirstreamShield override
		public Vessel GetVessel(){return part.vessel;}
		
		//IAirstreamShield override
		public Part GetPart(){return part;}
		
		#endregion
		
		#region private utility methods

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
			fairingEnabled = false;
			Events["jettisonEvent"].active = false;
			FairingPanel[] panels = fairingBase.panels;
			fairingBase.enablePanelColliders(true, false);//generate non-convex mesh colliders for panels
			GameObject panelGO;
			Rigidbody rb;
			Vector3 globalForceDirection;
			for(int i = 0; i < panels.Length; i++)
			{
				panelGO = panels[i].panel;
				panelGO.transform.parent = null;
				panelGO.AddComponent<physicalObject>();//auto-destroy when more than 1km away
				rb = panelGO.AddComponent<Rigidbody>();
				rb.velocity = part.rigidbody.velocity;
				rb.mass = fairingMass / (float)fairingSections;
				globalForceDirection = panelGO.transform.TransformPoint(jettisonDirection) - panelGO.transform.position;
				rb.AddForce (globalForceDirection * jettisonForce);
				rb.useGravity = false;
			}
			part.mass -= fairingMass;
		}
		
		//if attachedToNode is enabled, this will re-parent the fairing panels to the part at that node when called		
		private void updateAttachedStatus()
		{
			if(HighLogic.LoadedSceneIsEditor){return;}
			if(attachedToNode && watchedNode!=null && watchedNode.attachedPart!=null)
			{
				fairingBase.root.transform.parent = watchedNode.attachedPart.transform;
			}
		}
		
		private void updateStatusFromNode()
		{
			fairingEnabled = watchedNode!=null && watchedNode.attachedPart!=null;
		}
		
		private void enableFairingRender(bool val)
		{
			fairingEnabled = val;
			SSTUUtils.enableRenderRecursive(fairingBase.root.transform, val);
		}
		
		//editor method to rebuild the fairing on radius adjust
		private void rebuildFairing()
		{
			if(fairingBase!=null)
			{
				GameObject.Destroy(fairingBase.root);
				fairingBase=null;				
			}
			buildFairing();
		}
		
		private void buildFairing()
		{
			float height = fairingTopY - fairingBottomY;
			NodeFairingGenerator fg = new NodeFairingGenerator(-height*0.5f, capSize, height, maxPanelHeight, bottomRadius, topRadius, wallThickness, fairingSections, cylinderSides);
			FairingBase fb = fg.buildFairing();
			fb.root.transform.NestToParent(part.transform);
			fb.root.transform.position = part.transform.position;
			fb.root.transform.localPosition = new Vector3(0,fairingTopY - height*0.5f,0);
			fb.root.transform.rotation = part.transform.rotation;
			if(fairingMaterial!=null)
			{
				fb.setMaterial(fairingMaterial);				
			}
			if(HighLogic.LoadedSceneIsEditor)
			{
				fb.setPanelOpacity(0.25f);
			}
			fairingBase = fb;
			updateDragCube();
			updateShieldStatus();
		}
		
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
		
		private void updateShieldStatus()
		{
			clearShieldedParts();
			if(shieldsParts && fairingEnabled)
			{
				findShieldedParts();	
			}
		}
		
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

		private void findShieldedParts()
		{
			if(shieldedParts.Count>0)
			{
				clearShieldedParts();
			}
			
			Bounds combinedBounds = SSTUUtils.getRendererBoundsRecursive(fairingBase.root);
			SSTUUtils.findShieldedPartsCylinder (part, combinedBounds, shieldedParts, fairingTopY, fairingBottomY, topRadius, bottomRadius);
			for (int i = 0; i < shieldedParts.Count; i++)
			{
				shieldedParts[i].AddShield(this);
				print ("SSTUNodeFairing is shielding: "+shieldedParts[i].name);
			}
		}
		
		#endregion
		
	}
}

