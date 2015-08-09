using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	//disable another part from pulling fuel from this part through the specified node
	public class SSTUCrossfeedDisabler : PartModule
	{		
		[KSPField(isPersistant=true)]
		public bool moduleControlEnabled;
		
		[KSPField]
		public String nodeName;

		[KSPField]
		public bool disableCrossflow;
				
		private AttachNode otherNode;
		private bool otherNodeDefaultFlow;
		
		public override void OnStart (PartModule.StartState state)
		{
			base.OnStart (state);
			setupNodeCrossfeed();
		}
		
		private void setupNodeCrossfeed()
		{
			AttachNode node = part.findAttachNode(nodeName);
			if(node!=null && node.attachedPart!=null)
			{
				Part ap = node.attachedPart;
				AttachNode an = null;
				if(ap.NoCrossFeedNodeKey==null || ap.NoCrossFeedNodeKey.Length==0)
				{
					foreach(AttachNode m in ap.attachNodes)				
					{
						if(m.attachedPart==part)
						{
							an = m;
							break;
						}
					}
					if(an!=null)
					{
						ap.NoCrossFeedNodeKey = an.id;
						otherNode = an;
					}
				}
			}
		}
		
		private void updatePartCrossflow()
		{
			print ("examining decoupler part crossfeed!");
			if(otherNode!=null){otherNode.ResourceXFeed=otherNodeDefaultFlow;}
			otherNode=null;
			AttachNode node = part.findAttachNode(nodeName);			
			if(node!=null)
			{
				node.ResourceXFeed = !disableCrossflow;
				Part otherPart = node.attachedPart;
				AttachNode oNode = otherPart==null ? null : otherPart.findAttachNodeByPart(part);
				
				print ("set decoupler node crossflow to: "+node.ResourceXFeed+ " for node: "+node.id+" for part: "+part+ " attached part: "+otherPart+ " oNode: "+oNode);
				
				if(oNode!=null)
				{
					otherNode = oNode;
					otherNodeDefaultFlow = oNode.ResourceXFeed;
					if(disableCrossflow){oNode.ResourceXFeed=false;}
					print ("set other node crossflow to: "+oNode.ResourceXFeed);
				}
				else if(otherPart!=null)
				{
					AttachNode on = SSTUUtils.findRemoteParentNode(otherPart, part);
					if(on!=null)
					{
						print ("found remote node connection through: "+on+" :: "+on.id+" :: attached "+on.attachedPart);
						otherNode = on;
						otherNodeDefaultFlow = on.ResourceXFeed;
						if(disableCrossflow){on.ResourceXFeed=false;}
						print ("set remote connected node crosfeed to: "+on.ResourceXFeed);
					}
					else
					{
						print ("found part connected to node, but could not trace parantage through nodes");
					}
				}
			}
		}
	}
}

