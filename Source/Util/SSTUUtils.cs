using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	public class SSTUUtils
	{
		//retrieve an array of Components that implement <T>/ extend <T>;
		//<T> may be an interface or class
		public static T[] getComponentsImplementing<T>(GameObject obj) where T : class
		{
			List<T> interfacesList = new List<T> ();
			Component[] comps = obj.GetComponents<MonoBehaviour> ();
			T t;
			foreach (Component c in comps)
			{
				t = c as T;
				if(t!=null)
				{
					interfacesList.Add (t);
				}
			}
			return interfacesList.ToArray();
		}

		public static double safeParseDouble(String val)
		{
			double returnVal = 0;
			try
			{
				returnVal = double.Parse(val);
			}
			catch(Exception e)
			{
				MonoBehaviour.print ("could not parse double value from: "+val+"\n"+e.Message);
			}
			return returnVal;
		}
		
		public static int safeParseInt(String val)
		{
			int returnVal = 0;
			try
			{
				returnVal = int.Parse(val);
			}
			catch(Exception e)
			{
				MonoBehaviour.print ("could not parse double value from: "+val+"\n"+e.Message);
			}
			return returnVal;
		}
		
		public static String concatArray(float[] array)
		{
			String val = "";
			if(array!=null)
			{
				foreach(float f in array){val=val+f+",";}				
			}
			return val;
		}
		
		public static String concatArray(String[] array)
		{
			String val = "";
			if(array!=null)
			{
				foreach(String f in array){val=val+f+",";}				
			}
			return val;
		}
		
		public static String printList<T>(List<T> list, String separator)
		{
			String str = "";
			int len = list.Count;
			for(int i = 0; i < len; i++)
			{
				str = str + list[i].ToString();
				if(i<len-1){str = str+separator;}
			}
			return str;
		}
		
		public static String printArray<T>(T[] array, String separator)
		{
			String str = "";
			if(array!=null)
			{
				int len = array.Length;
				for(int i = 0; i < len; i++)
				{
					str = str + array[i].ToString();
					if(i<len-1){str = str+separator;}
				}				
			}
			return str;
		}
						
		public static void recursePrintChildTransforms(Transform tr, String prefix)
		{			
			MonoBehaviour.print ("Transform found: "+prefix+tr.name);
			for(int i = 0; i < tr.childCount; i++)
			{
				recursePrintChildTransforms(tr.GetChild(i), prefix+"  ");
			}
		}
		
		public static void recursePrintComponents(GameObject go, String prefix)
		{
			MonoBehaviour.print 	("Found gameObject: "+prefix+go.name);	
			int childCount = go.transform.childCount;
			Component[] comps = go.GetComponents<Component>();
			foreach(Component comp in comps)
			{
				MonoBehaviour.print ("Found Component : "+prefix +"* "+comp.GetType());
			}
			
			for(int i = 0; i < childCount; i++)
			{
				recursePrintComponents(go.transform.GetChild(i).gameObject, prefix+"  ");
			}
		}
						
		public static void enableMeshColliderRecursive(Transform tr, bool enabled, bool convex)
		{
			MeshCollider mc = tr.GetComponent<MeshCollider>();
			if(mc!=null)
			{
				mc.enabled = enabled;
				mc.convex = convex;
			}
			int len = tr.childCount;
			for(int i = 0; i < len; i++)
			{
				enableMeshColliderRecursive(tr.GetChild(i), enabled, convex);
			}
		}

		public static void recursePrintOjbectTree(GameObject go)
		{
			MonoBehaviour.print("Object graph for: " + go.name);
			printObjectTree (go, "", true);
		}

		public static ConfigNode findModuleNode(Part part, String moduleName, String idField, String idValue)
		{
			ConfigNode partNode = PartLoader.Instance.GetDatabaseConfig(part);
			if(partNode==null)
			{
				MonoBehaviour.print("partNode==null!!");
			}
			else
			{
				MonoBehaviour.print ("Found part node: \n"+partNode);
			}
			if(moduleName==null)
			{
				MonoBehaviour.print ("moduleName==null!!");
			}
			ConfigNode[] moduleNodes = partNode.GetNodes ("MODULE", "name", moduleName);
			int len = moduleNodes.Length;
			String val;
			for (int i = 0; i < len; i++)
			{
				if(moduleNodes[i].HasValue(idField))
				{
					val = moduleNodes[i].GetValue(idField);
					if(idValue.Equals(val))
					{
						return moduleNodes[i];
					}
				}
			}
			return null;
		}
		
		public static AttachNode findRemoteParentNode(Part searchRoot, Part toFind)
		{
			AttachNode returnNode = null;
			List<AttachNode> searchedNodes = new List<AttachNode>();
			foreach(AttachNode node in searchRoot.attachNodes)
			{
				searchedNodes.AddUnique(node);
				if(node.attachedPart==toFind)
				{
					returnNode = node;
					break;
				}
				else if(nodeTreeContains(node, toFind, searchedNodes))
				{
					returnNode = node;
					break;
				}
			}
			return returnNode;
		}
		
		private static bool nodeTreeContains(AttachNode node, Part toFind, List<AttachNode> searchedNodes)
		{
			if(node.attachedPart==null){return false;}
			foreach(AttachNode on in node.attachedPart.attachNodes)
			{
				if(searchedNodes.Contains(on)){continue;}//prevent stack overflow
				searchedNodes.AddUnique (on);
				if(on.attachedPart==toFind){return true;}
				if(nodeTreeContains(on, toFind, searchedNodes)){return true;}
			}
			return false;
		}

		private static void printObjectTree(GameObject go, String prefix, bool isTail)
		{
			
			//			http://stackoverflow.com/questions/4965335/how-to-print-binary-tree-diagram
			//			final String name;
			//			final List<TreeNode> children;
			//			
			//			public TreeNode(String name, List<TreeNode> children) {
			//				this.name = name;
			//				this.children = children;
			//			}
			//			
			//			public void print() {
			//				print("", true);
			//			}
			//			
			//			private void print(String prefix, boolean isTail) {
			//				System.out.println(prefix + (isTail ? "└── " : "├── ") + name);
			//				for (int i = 0; i < children.size() - 1; i++) {
			//					children.get(i).print(prefix + (isTail ? "    " : "│   "), false);
			//				}
			//				if (children.size() > 0) {
			//					children.get(children.size() - 1).print(prefix + (isTail ?"    " : "│   "), true);
			//				}
			//			}

			//alternative to investigate:
			//http://stackoverflow.com/questions/1649027/how-do-i-print-out-a-tree-structure

			MonoBehaviour.print (prefix  + (isTail ? "└── " : "├── " + go.name));
			Component[] comps = go.GetComponents<MonoBehaviour>();
			bool compTail = false;
			for (int i = 0; i < comps.Length; i++)
			{
				compTail = i>=comps.Length-1;
				MonoBehaviour.print (prefix + (compTail ? "    " : "|   ") +comps[i].GetType());
			}
			for (int i = 0; i< go.transform.childCount-1; i++)
			{
				printObjectTree(go.transform.GetChild(i).gameObject, prefix + (isTail ? "    " : "|   "), false);
			}
			if (go.transform.childCount > 0)
			{
				printObjectTree(go.transform.GetChild(go.transform.childCount-1).gameObject, prefix + (isTail ?"    " : "│   "), true);
			}
		}
		
		public static void enableRenderRecursive(Transform tr, bool val)
		{			
			if(tr.renderer!=null)
			{
				tr.renderer.enabled = val;	
			}
			for(int i = 0; i < tr.childCount; i++)
			{
				enableRenderRecursive(tr.GetChild(i), val);
			}
		}
		
		public static void enableColliderRecursive(Transform tr, bool val)
		{			
			foreach(Collider collider in tr.gameObject.GetComponents<Collider>())
			{
				collider.enabled = val;
			}
			for(int i = 0; i < tr.childCount; i++)
			{
				enableColliderRecursive(tr.GetChild(i), val);
			}
		}
		
		public static Texture findTexture(String textureName)
		{
			Texture[] textures = (Texture[])Resources.FindObjectsOfTypeAll(typeof(Texture));
			foreach(Texture tex in textures)
			{
				if(tex.name.Equals(textureName)){return tex;}
			}
			return null;
		}
				
		public static float distanceFromLine(Ray ray, Vector3 point)
		{
			return Vector3.Cross(ray.direction, point - ray.origin).magnitude;
		}
		
		public static Bounds getRendererBoundsRecursive(GameObject gameObject)
		{
			Renderer[] childRenders = gameObject.GetComponentsInChildren<Renderer>(false);
			Renderer parentRender = gameObject.GetComponent<Renderer>();
			
			Bounds combinedBounds = default(Bounds);
			
			bool initializedBounds = false;			
			
			if(parentRender!=null && parentRender.enabled)
			{
				combinedBounds = parentRender.bounds;
				initializedBounds = true;
			}
			int len = childRenders.Length;
			for(int i = 0 ; i<len; i++)
			{
				if(initializedBounds)
				{
					combinedBounds.Encapsulate(childRenders[i].bounds);
				}
				else
				{
					combinedBounds = childRenders[i].bounds;
					initializedBounds=true;
				}
			}
			return combinedBounds;
		}

		public static void findShieldedPartsCylinder(Part basePart, Bounds fairingRenderBounds, List<Part> shieldedParts, float topY, float bottomY, float topRadius, float bottomRadius)
		{
			float height = topY - bottomY;
			float largestRadius = topRadius > bottomRadius ? topRadius : bottomRadius;			
			
			Vector3 lookupCenterLocal = new Vector3(0, bottomY + (height*0.5f), 0);
			Vector3 lookupTopLocal = new Vector3(0, topY, 0);
			Vector3 lookupBottomLocal = new Vector3(0, bottomY, 0);			
			Vector3 lookupCenterGlobal = basePart.transform.TransformPoint(lookupCenterLocal);
			
			Ray lookupRay = new Ray(lookupBottomLocal, new Vector3(0,1,0));
						
			List<Part> partsFound = new List<Part>();
			Collider[] foundColliders = Physics.OverlapSphere(lookupCenterGlobal, height*1.5f, 1);
			foreach(Collider col in foundColliders)
			{
				Part pt = col.gameObject.GetComponentUpwards<Part>();
				if(pt!=null && pt!=basePart && pt.vessel==basePart.vessel)
				{
//					MonoBehaviour.print ("found part to test for containment: "+pt);
					partsFound.AddUnique(pt);
				}
			}
			
			Bounds[] otherPartBounds;	
			Vector3 otherPartCenterLocal;			
					
			float partYPos;
			float partYPercent;
			float partYRadius;			
			float radiusOffset = topRadius - bottomRadius;			
			
			foreach(Part pt in partsFound)
			{
				//check basic render bounds for containment
				//TODO this check misses the case where the fairing is long/tall, containing a wide part; it will report that the wide part can fit inside
				//of the fairing, due to the relative size of their colliders
				otherPartBounds = pt.GetRendererBounds();
				if(PartGeometryUtil.MergeBounds(otherPartBounds, pt.transform).size.sqrMagnitude > fairingRenderBounds.size.sqrMagnitude)
				{
//					MonoBehaviour.print ("otherPart too large to contain in render bounds! "+pt);
					continue;
				}
				
				Vector3 otherPartCenter = pt.partTransform.TransformPoint(PartGeometryUtil.FindBoundsCentroid(otherPartBounds, pt.transform));				
				if(!fairingRenderBounds.Contains(otherPartCenter))
				{
//					MonoBehaviour.print ("other part centroid outside of render bounds! "+pt);
					continue;
				}
				
				//check part bounds center point against conic projection of the fairing
				otherPartCenterLocal = basePart.transform.InverseTransformPoint(otherPartCenter);
				//check vs top and bottom
				if(otherPartCenterLocal.y > lookupTopLocal.y)
				{
//					MonoBehaviour.print ("other part too high for shielding: "+pt);
					continue;
				}	

				if(otherPartCenterLocal.y < lookupBottomLocal.y)
				{
//					MonoBehaviour.print ("other part too low for shielding: "+pt);
					continue;
				}				
				
				//quick check vs cylinder radius
				float distFromLine = SSTUUtils.distanceFromLine(lookupRay, otherPartCenterLocal);
				if(distFromLine > largestRadius)
				{
//					MonoBehaviour.print ("part outside of cylinder collider: "+pt);
					continue;
				}
				
				//more precise check vs radius of the cone at that Y position
				partYPos = otherPartCenterLocal.y - lookupBottomLocal.y;
				partYPercent = partYPos / height;
				partYRadius = partYPercent * radiusOffset;
				if(distFromLine > (partYRadius+bottomRadius))
				{
//					MonoBehaviour.print ("part outside of cone collider at its Ylevel"+pt);
					continue;
				}				
				shieldedParts.Add(pt);
			}
		}
		
		public class DebugBoundsRender
		{
			private GameObject gameObject;
			private LineRenderer lineRender;
			int vert = 0;			
						
			float x1, y1, z1, x2, y2, z2;
			
			public DebugBoundsRender(Transform parent)
			{
				this.gameObject = new GameObject();
				
				this.gameObject.transform.parent = parent;
				this.gameObject.transform.position = parent.position;
				this.gameObject.transform.localPosition = Vector3.zero;
				this.gameObject.transform.rotation = parent.rotation;
				this.gameObject.transform.localRotation = Quaternion.identity;
				
				this.lineRender = this.gameObject.AddComponent<LineRenderer>();
				this.lineRender.SetColors(Color.white, Color.white);
				this.lineRender.SetWidth(0.05f, 0.05f);				
				this.lineRender.useWorldSpace = true;
				lineRender.SetVertexCount(10);
			}
			
			public DebugBoundsRender setFromBounds(Bounds bounds)
			{				
				x1 = bounds.center.x - bounds.extents.x;
				x2 = bounds.center.x + bounds.extents.x;				
				
				y1 = bounds.center.y - bounds.extents.y;
				y2 = bounds.center.y + bounds.extents.y;
				
				z1 = bounds.center.z - bounds.extents.z;
				z2 = bounds.center.z + bounds.extents.z;
				
				addVertex (x1,y1,z1);
				addVertex (x2,y1,z1);
				addVertex (x2,y1,z2);
				addVertex (x1,y1,z2);
				addVertex (x1,y1,z1);				
				addVertex (x1,y2,z1);
				addVertex (x2,y2,z1);
				addVertex (x2,y2,z2);
				addVertex (x1,y2,z2);
				addVertex (x1,y2,z1);
				
				MonoBehaviour.print ("set DBR from bounds: "+bounds);
				return this;
			}
			
			private void addVertex(float x, float y, float z)
			{				
				addVertex(new Vector3(x,y,z));
			}
			
			private void addVertex(Vector3 vec)
			{				
				lineRender.SetPosition(vert, vec);
				vert++;
			}
			
			public void destroy()
			{
				GameObject.Destroy(gameObject);
			}
		}
	}
}

