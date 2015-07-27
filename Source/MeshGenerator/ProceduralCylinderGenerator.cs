using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	
	public class ProceduralCylinderGenerator : MonoBehaviour
	{
		public ProceduralCylinderGenerator ()
		{
			print("PCG cstr");
		}
		
		public void Start()
		{
			print ("PCG Start!");
			
			float startHeight = 0;
			float baseHeight = 0.25f;
			float boltPanelHeight = 0.1f;
			float totalPanelHeight = 3.5f;
			float maxPanelSectionHeight = 1.0f;
			float bottomRadius = 2.5f;
			float topRadius = 2.5f;
			float wallThickness = 0.05f;
			int numOfPanels = 4;
			int cylinderSides = 24;
			
			InterstageFairingGenerator fg = new InterstageFairingGenerator(startHeight, baseHeight, boltPanelHeight, totalPanelHeight, maxPanelSectionHeight, bottomRadius, topRadius, wallThickness, numOfPanels, cylinderSides);
			FairingBase f = fg.buildFairing();						
			f.root.transform.parent = transform;
			f.root.transform.position = transform.position;
			f.root.transform.rotation = transform.rotation;			
			f.setMaterial (transform.renderer.sharedMaterial);
		}	
	}
	
}
