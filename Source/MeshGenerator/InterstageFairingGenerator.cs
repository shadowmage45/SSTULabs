using System;
using System.Collections.Generic;
using UnityEngine;
namespace SSTUTools
{
	//creates an interstage fairing that has a base plate as well as the specified number of panels
	public class InterstageFairingGenerator : BasicFairingGenerator
	{
		#region publicReadOnlyVars
		public String panelsColliderName = "FairingCollider";
		
		public readonly float baseHeight;
		#endregion 
				
		public UVArea baseTopCap = new UVArea(13, 691, 13+320, 691+320, 1024);
		public UVArea baseBottomCap = new UVArea(345, 691, 345+320, 691+320, 1024);
	
		public InterstageFairingGenerator(float startHeight, float baseHeight, float boltPanelHeight, float totalPanelHeight, float maxPanelSectionHeight, float bottomRadius, float topRadius, float wallThickness, int numOfPanels, int cylinderSides)
			:base(startHeight, boltPanelHeight, totalPanelHeight, maxPanelSectionHeight, bottomRadius, topRadius, wallThickness, numOfPanels, cylinderSides)
		{
			this.baseHeight = baseHeight;
		}		
		
		public FairingBase buildFairing()
		{
			MeshGenerator gen = new MeshGenerator();									
			GameObject root = generateFairingBase(gen);				
			GameObject[] panels = generateFairingPanels(gen, root);
			FairingBase fairing = new FairingBase(root, panels);
			fairing.editorColliders = generateEditorTopCollider(gen, root);
			return fairing;
		}	
		
		private GameObject generateEditorTopCollider(MeshGenerator gen, GameObject root)
		{
			GameObject panelCollider = new GameObject(panelsColliderName);
			MeshFilter mf = panelCollider.AddComponent<MeshFilter>();
			
			panelCollider.transform.parent = root.transform;
			panelCollider.transform.position = root.transform.position;
			panelCollider.transform.rotation = root.transform.rotation;
			panelCollider.transform.localPosition = new Vector3(0, startHeight + baseHeight, 0);	
						
			gen.setUVArea(0,0,1,1);
			generateCylinderCollider(gen, 0, 0, totalPanelHeight-boltPanelHeight, boltPanelHeight, topOuterRadius, bottomOuterRadius, 12);			
			mf.mesh = gen.createMesh();
			gen.clear();
						
			MeshCollider mc = panelCollider.AddComponent<MeshCollider>();
			mc.convex = true;
									
			return panelCollider;
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
				panels[i].transform.localPosition = new Vector3(x, startHeight + baseHeight, z);
				panels[i].transform.localRotation = Quaternion.AngleAxis(90.0f + (float)i * anglePerPanel, new Vector3(0,1,0));
				
			}
			return panels;
		}
		
		private GameObject generateFairingBase(MeshGenerator gen)
		{					
			float totalHeight = baseHeight;
			float capHeight = boltPanelHeight;
			float panelHeight = totalHeight - (capHeight*2);
			
			//uv scaling for bolt caps
			UVArea outerCapUV = new UVArea(outerCap);
			float vHeight = outerCapUV.v2 - outerCapUV.v1;			
			float uScale = vHeight / capHeight;									
			outerCapUV.u2 = bottomOuterCirc * uScale;
			
			//generate lower bolt cap	
			gen.setUVArea(outerCapUV);
			gen.generateCylinderWallSection(0, 0, startHeight, boltPanelHeight, bottomOuterRadius, bottomOuterRadius, cylinderSides, anglePerSide, 0, true);			
			//generate upper bolt cap
			gen.generateCylinderWallSection(0, 0, startHeight+baseHeight-boltPanelHeight, boltPanelHeight, bottomOuterRadius, bottomOuterRadius, cylinderSides, anglePerSide, 0, true);
			
			//uv scaling for center panel
			UVArea outerPanelUV = new UVArea(outerPanel);
			float panelVHeight = outerPanelUV.v2 - outerPanelUV.v1;
			float panelVScale = panelVHeight / maxPanelSectionHeight;
			float panelU = (bottomOuterCirc / (float)numOfPanels) * panelVScale;
			outerPanelUV.u2 = panelU;
			outerPanelUV.v2 = outerPanelUV.v1 + (panelVScale * panelHeight);
			
			//generate center section
			gen.setUVArea(outerPanelUV);
			gen.generateCylinderWallSection(0, 0, startHeight+capHeight, panelHeight, bottomOuterRadius, bottomOuterRadius, cylinderSides, anglePerSide, 0, true);			
			
			//generate top circle cap					
			gen.setUVArea(baseTopCap);
			gen.generateTriangleFan(0,0,startHeight+baseHeight,bottomOuterRadius,cylinderSides,anglePerSide,0,true);
			//generate bottom circle cap
			gen.setUVArea(baseBottomCap);
			gen.generateTriangleFan(0,0,startHeight,bottomOuterRadius,cylinderSides,anglePerSide,0,false);
			
			
			GameObject root = new GameObject(baseName);						
			MeshFilter mf = root.AddComponent<MeshFilter>();
			mf.mesh = gen.createMesh();
			gen.clear();
			
			MeshRenderer mr = root.AddComponent<MeshRenderer>();
			MeshCollider mc = root.AddComponent<MeshCollider>();
			mc.convex = true;
			
			return root;
		}		
		
	}
}

