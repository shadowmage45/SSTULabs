using System;
using UnityEngine;

namespace SSTUTools
{
	public class CylinderMeshGenerator : BasicFairingGenerator
	{

		public CylinderMeshGenerator(float startHeight, float boltPanelHeight, float totalPanelHeight, float maxPanelSectionHeight, float bottomRadius, 
			float topRadius, float wallThickness, int numOfPanels, int cylinderSides)
			:base(startHeight, boltPanelHeight, totalPanelHeight, maxPanelSectionHeight, bottomRadius, 
				topRadius, wallThickness, numOfPanels, cylinderSides)
		{
			
		}
		
		public FairingBase buildFairing()
		{
			MeshGenerator gen = new MeshGenerator();									
			GameObject root = new GameObject();				
			GameObject[] panels = generateFairingPanels(gen, root);
			FairingBase fairing = new FairingBase(root, panels);
			return fairing;
		}

		public void buildFairingBasic(GameObject root)
		{
			MeshGenerator gen = new MeshGenerator();		
			generateFairingPanels(gen, root);
		}
		
		private GameObject[] generateFairingPanels(MeshGenerator gen, GameObject root)
		{
			GameObject[] panels = new GameObject[numOfPanels];	
			generateFairingPanel(gen);
			
			Mesh panelsMesh = gen.createMesh();//only create a single mesh; use this same mesh for every panel GO			
			gen.clear();							
			
			MeshFilter mf;
			MeshRenderer mr;
			float x, z, a;
			int length = numOfPanels;
			for(int i = 0; i < length; i++)
			{	
				//setup panel game object starting location and rotation
				a = 0 + i * anglePerPanel * Mathf.Deg2Rad;
				x = Mathf.Cos (a) * bottomOuterRadius;
				z = -Mathf.Sin (a) * bottomOuterRadius;
				
				panels[i] = new GameObject(panelName+i);
				mr = panels[i].AddComponent<MeshRenderer>();
				mf = panels[i].AddComponent<MeshFilter>();	
				mf.mesh = panelsMesh;													
				
				panels[i].transform.parent = root.transform;
				panels[i].transform.position = root.transform.position;
				panels[i].transform.rotation = root.transform.rotation;
				panels[i].transform.localPosition = new Vector3(x, startHeight, z);
				panels[i].transform.localRotation = Quaternion.AngleAxis(90.0f + (float)i * anglePerPanel, new Vector3(0,1,0));
				
			}
			return panels;
		}
	}
}

