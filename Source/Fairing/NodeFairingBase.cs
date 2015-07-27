using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	public class NodeFairingBase
	{
		GameObject root;
		FairingPanel[] panels;
		
		public NodeFairingBase (GameObject root, GameObject[] panelsGOs)
		{
			this.root = root;
			this.panels = new FairingPanel[panelsGOs.Length];
			for (int i = 0; i < panelsGOs.Length; i++)
			{
				this.panels[i] = new FairingPanel(panelsGOs[i]);
			}
		}
				
	}
}

