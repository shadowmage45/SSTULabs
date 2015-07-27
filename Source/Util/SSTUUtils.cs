using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	public class SSTUUtils
	{
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
			foreach(float f in array){val=val+f+",";}
			return val;
		}
		
		public static String concatArray(String[] array)
		{
			String val = "";
			foreach(String f in array){val=val+f+",";}
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
			int len = array.Length;
			for(int i = 0; i < len; i++)
			{
				str = str + array[i].ToString();
				if(i<len-1){str = str+separator;}
			}
			return str;
		}
						
		public static void recursePrintChild(Transform tr, String prefix)
		{			
			MonoBehaviour.print ("Transform found: "+prefix+tr.name);
			for(int i = 0; i < tr.childCount; i++)
			{
				recursePrintChild(tr.GetChild(i), prefix+"  ");
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
		
//		public static void enableComponentRecursive<T>(Transform tr, bool val) where T : UnityEngine.Component
//		{
//			UnityEngine.Component t = tr.GetComponent<T>() as UnityEngine.Component;
//			if(t!=null){t.enabled = val;}//apparently not everything has an enable flag...meh
//		} 
		
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

