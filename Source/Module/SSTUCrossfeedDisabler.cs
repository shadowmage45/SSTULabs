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
				
		AttachNode otherNode;
		Part otherPart;
		
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
						otherPart = ap;
						otherNode = an;
					}
				}
			}
		}
		
		private void clearCurrentSetup()
		{
			if(otherPart!=null)
			{
				otherPart.NoCrossFeedNodeKey=string.Empty;
				otherNode=null;
				otherPart=null;				
			}
		}
	}
}

