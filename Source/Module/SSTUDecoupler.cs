using System;

namespace SSTUTools
{
	public class SSTUDecoupler : PartModule
	{		
		[KSPField]
		public string nodeName = "top";
		
		[KSPField]
		public float decoupleForce = 15;
		
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="DC Stage Enabled")]
		public bool useStaging = false;
		
		[KSPField]
		public string displayName = "Decouple";
		
		[KSPField(isPersistant=true)]
		public bool decoupled = false;
		
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Decoupler Enabled")]
		public bool decouplerEnabled = true;
		
		[KSPField]
		public string stagingIcon = DefaultIcons.DECOUPLER_VERT.ToString();
		
		DefaultIcons stagingIconImage = DefaultIcons.DECOUPLER_VERT;//default value
		
		public SSTUDecoupler ()
		{
			
		}
		
		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			Events["decoupleEvent"].guiName=displayName;
			Actions["decoupleAction"].guiName=displayName;	
			try
			{
				stagingIconImage = (DefaultIcons)Enum.Parse(typeof(DefaultIcons),stagingIcon);
			}
			catch(Exception e)
			{
				stagingIconImage = DefaultIcons.DECOUPLER_VERT;
				print(e.Message);
			}
			
			setupStagingIcon(useStaging);
			setupDecouplerEnabled(decouplerEnabled);
			
			if(decoupled)
			{
				Events["decoupleEvent"].active=false;
				Events["decoupleEvent"].guiActive=false;
			}
			
			if(!decouplerEnabled)
			{
				Events["toggleStagingEvent"].guiActive=false;
			}
		}
		
		public override void OnActive ()
		{
			base.OnActive ();
			if(useStaging)
			{
				decoupleInternal ();
			}
		}
		
		[KSPAction("Decouple")]
		public void decoupleAction(KSPActionParam param)
		{
			decoupleInternal();
		}
		
		[KSPEvent(name= "decoupleEvent", guiName = "Decouple", guiActiveUnfocused = true, externalToEVAOnly = true, guiActive = true, unfocusedRange = 4f, guiActiveEditor = false) ]
		public void decoupleEvent()
		{
			decoupleInternal();
		}
		
		[KSPEvent(name= "toggleStagingEvent", guiName = "Toggle DC Staging", guiActiveUnfocused = false, externalToEVAOnly = false, guiActive = false, guiActiveEditor = true) ]
		public void toggleStagingEvent()
		{
			setupStagingIcon(!useStaging);		
		}
		
		[KSPEvent(name= "toggleEnabledEvent", guiName = "Toggle DC Enabled", guiActiveUnfocused = false, externalToEVAOnly = false, guiActive = false, guiActiveEditor = true) ]
		public void toggleEnabledEvent()
		{			
			setupDecouplerEnabled(!decouplerEnabled);
			if(!decouplerEnabled)
			{
				setupStagingIcon(false);
			}
			Events["toggleStagingEvent"].guiActive=decouplerEnabled;
		}
		
		private void setupDecouplerEnabled(bool enabled)
		{			
			decouplerEnabled = enabled;
			Events["decoupleEvent"].active = enabled  && !decoupled;
			Events["decoupleEvent"].guiActive = enabled  && !decoupled;
		}
		
		private void setupStagingIcon(bool useStaging)
		{
			if(!decouplerEnabled)
			{
				useStaging=false;
			}
			this.useStaging = useStaging;			
			if(useStaging)
			{
				if(part.stagingIcon==string.Empty)
				{
					part.stagingIcon=stagingIcon;
					part.stackIcon.iconImage = stagingIconImage;
					part.stackIcon.CreateIcon();					
				}
			}
			else
			{
				if(part.stagingIcon==stagingIcon)
				{
					part.stagingIcon=string.Empty;
					part.stackIcon.RemoveIcon();
				}
			}		
			Staging.GenerateStagingSequence(part.localRoot);
			Staging.SortIcons();
		}
		
		private void decoupleInternal()
		{
			if(decoupled || !decouplerEnabled){return;}
			decoupled=true;
			
			Events["decoupleEvent"].active=false;
			Events["decoupleEvent"].guiActive=false;
			
			playFX();
			AttachNode node = part.findAttachNode(nodeName);
			if(node==null || node.attachedPart==null){return;}
			
			Part attachedPart = node.attachedPart;
			if (attachedPart == base.part.parent)
			{				
				base.part.decouple (0f);
			}
			else
			{
				attachedPart.decouple (0f);
			}	
			addForces ();
		}
		
		private void playFX()
		{
			
		}
		
		private void addForces()
		{
			//TODO
		}				
	}
}

