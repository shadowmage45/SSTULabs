using System;

namespace SSTUTools
{
	//decoupler that can be disabled by moduleSwitch
	//TODO adapt to allow to play an animation on/before decouple
	//TODO adapt to allow a 'decoupled mesh' similar to moduleJettison
	public class SSTUDecoupler : ModuleDecouple, IControlledModule
	{		
		[KSPField]
		public int controlID = -1;
		
		[KSPField(isPersistant=true)]
		public bool moduleControlEnabled = false;
		
		[KSPField]
		public bool disableCrossflow = true;
		
		[KSPField(isPersistant=true)]
		public bool useStaging = true;

		[KSPField]
		public bool canAdjustStaging = true;
		
		[KSPField]
		public bool invertNode = false;

		[KSPField]
		public string mainStagingIcon = DefaultIcons.DECOUPLER_VERT.ToString();

		[KSPField]
		public string alternateStagingIcon = DefaultIcons.COMMAND_POD.ToString();
		
		private bool subscribedToEvents = false;

		private AttachNode otherNode;
		private bool otherNodeDefaultFlow;
		private bool updatedCrossflow = false;
		
		[KSPEvent(guiName="Toggle Decoupler Staging", guiActiveEditor=true, active=false)]
		public void toggleStagingEvent()
		{
			useStaging = !useStaging;
			staged = useStaging;//TODO investigate any problems due to this....
			setupStagingIcon();
		}
				
		public override void OnStart (PartModule.StartState state)
		{
			if(controlID==-1){moduleControlEnabled=true;}
			base.OnStart (state);
			if(moduleControlEnabled)
			{
				if(disableCrossflow)
				{
					subscribeToEvents();
					updatePartCrossflow();						
				}
			}
			updateAttachNode();
			setupStagingIcon();	
			updateGuiFromState();
		}		
		
		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			updateAttachNode();
		}
		
		public override void OnActive ()
		{
			if(moduleControlEnabled && useStaging)
			{
				base.OnActive ();				
			}
		}
		
		public void OnDestroy()
		{
			if(subscribedToEvents)
			{
				removeSubscriptions();
			}
		}
		
		public void FixedUpdate()
		{
			if(!moduleControlEnabled){return;}
			if(!updatedCrossflow)
			{
				updatedCrossflow=true;
				updatePartCrossflow();
			}
		}
		
		public bool isControlEnabled ()
		{
			return moduleControlEnabled;
		}
		
		public int getControlID ()
		{
			return controlID;
		}
		
		public void enableModule ()
		{
			moduleControlEnabled = true;			
			updateGuiFromState();			
			if(disableCrossflow)
			{
				subscribeToEvents();				
			}
			updateAttachNode();
			updatePartCrossflow();
			setupStagingIcon();
		}
		
		public void disableModule ()
		{
			moduleControlEnabled = false;
			updateGuiFromState();			
			removeSubscriptions();
			updateAttachNode();
			updatePartCrossflow();
			setupStagingIcon();
		}
		
		private void subscribeToEvents()
		{
			if(!subscribedToEvents)
			{
				subscribedToEvents = true;
				GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
				GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(onVesselModified));
			}
		}
		
		private void removeSubscriptions()
		{
			if(subscribedToEvents)
			{
				subscribedToEvents = false;		
				GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
				GameEvents.onVesselWasModified.Remove(new EventData<Vessel>.OnEvent(onVesselModified));	
			}
		}
		
		public void onEditorVesselModified(ShipConstruct c)
		{
			updatePartCrossflow();
		}
		
		public void onVesselModified(Vessel v)
		{
			updatePartCrossflow();
		}
		
		private void updateAttachNode()
		{
			AttachNode node = part.findAttachNode(explosiveNodeID);
			if(node==null){return;}
			if(invertNode && moduleControlEnabled)
			{
				node.orientation = node.originalOrientation * -1;
			}
			else
			{
				node.orientation = node.originalOrientation;
			}
		}
		
		private void setupStagingIcon()
		{			
			if(useStaging && moduleControlEnabled)
			{
				if(part.stagingIcon==string.Empty)
				{
					part.stagingIcon = mainStagingIcon;
					part.stackIcon.iconImage = (DefaultIcons)Enum.Parse (typeof(DefaultIcons),mainStagingIcon);
					part.stackIcon.CreateIcon();					
				}
			}
			else
			{
				if(part.stagingIcon == mainStagingIcon)
				{
					part.stagingIcon=string.Empty;
					part.stackIcon.RemoveIcon();
				}
				if(part.stagingIcon==string.Empty && alternateStagingIcon!=null && alternateStagingIcon.Length>0)
				{
					part.stagingIcon = alternateStagingIcon;
					part.stackIcon.iconImage = (DefaultIcons)Enum.Parse (typeof(DefaultIcons),alternateStagingIcon);
					part.stackIcon.CreateIcon();
				}
			}		
			Staging.GenerateStagingSequence(part.localRoot);
			Staging.SortIcons();
		}
		
		private void updatePartCrossflow()
		{
			if(otherNode!=null){otherNode.ResourceXFeed=otherNodeDefaultFlow;}
			otherNode=null;
			AttachNode node = part.findAttachNode(explosiveNodeID);			
			if(node!=null)
			{
				node.ResourceXFeed = !disableCrossflow;
				Part otherPart = node.attachedPart;
				AttachNode oNode = otherPart==null ? null : otherPart.findAttachNodeByPart(part);
								
				if(oNode!=null)
				{
					otherNode = oNode;
					otherNodeDefaultFlow = oNode.ResourceXFeed;
					if(disableCrossflow){oNode.ResourceXFeed=false;}				
				}
				else if(otherPart!=null)
				{
					AttachNode on = SSTUUtils.findRemoteParentNode(otherPart, part);
					if(on!=null)
					{
						otherNode = on;
						otherNodeDefaultFlow = on.ResourceXFeed;
						if(disableCrossflow){on.ResourceXFeed=false;}
					}
					else
					{
						print ("Found part connected to node, but could not trace parantage through nodes. parent: "+part+" dest: "+otherPart);
					}
				}
			}
		}
		
		private void updateGuiFromState()
		{
			if(!moduleControlEnabled)
			{
				Fields["ejectionForcePercent"].guiActive = Fields["ejectionForcePercent"].guiActiveEditor = false;
				Events["Decouple"].active = false;
				Events["toggleStagingEvent"].active = false;
				Actions["DecoupleAction"].active = false;
			}
			else
			{
				Fields["ejectionForcePercent"].guiActive = Fields["ejectionForcePercent"].guiActiveEditor = true;
				Events["Decouple"].active = true;
				Events["toggleStagingEvent"].active = canAdjustStaging;
				Actions["DecoupleAction"].active = true;
			}
		}
	}
}

