using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	public class MeshGenerator
	{
		private List<Vertex> vertices = new List<Vertex>();
		private List<Vector3> verts = new List<Vector3>();
		private List<Vector3> norms = new List<Vector3>();
		private List<Vector4> tangents = new List<Vector4>();
		private List<Vector2> uvs = new List<Vector2>();
		private List<int> indices = new List<int>();
		
		private int vertexCount = 0;
		
		private float u1 = 0, v1 = 0, u2 = 1, v2 = 1;//current UV coords to use for geometry; UV coord for each vertice is calculated based upon this bounding box
		
		//set texture area from regular (topleft=0,0) texture coordinates		
		//y1 and y2 will be inverted, v1 = y2, v2 = y1
		public void setUVArea(int x1, int y1, int x2, int y2, int textureSize)
		{
			float areaPerPx = 1.0f / (float)textureSize;
			u1 = (float)x1 * areaPerPx;
			u2 = (float)x2 * areaPerPx;
			v1 = (float)(textureSize-1 - (y2-1)) * areaPerPx;
			v2 = (float)(textureSize-1 - y1) * areaPerPx;
		}
		
		public void setUVArea(float u1, float v1, float u2, float v2)
		{
			this.u1 = u1;
			this.v1 = v1;
			this.u2 = u2;
			this.v2 = v2;
		}
		
		public void setUVArea(UVArea area)
		{
			this.u1 = area.u1;
			this.u2 = area.u2;
			this.v1 = area.v1;
			this.v2 = area.v2;
		}
		
		public void addTriangle(int a, int b, int c)
		{
			indices.Add(a);
			indices.Add(b);
			indices.Add(c);
		}
		
		public Vertex addVertex(float x, float y, float z, float nx, float ny, float nz, float u, float v)
		{
			Vertex vert = new Vertex();
			vert.vertex = new Vector3(x,y,z);
			vert.normal = new Vector3(nx, ny, nz);
			vert.uv = new Vector2(u, v);
			vert.index = vertexCount;
			vertexCount++;
			
			verts.Add (vert.vertex);
			norms.Add (vert.normal);
			uvs.Add (vert.uv);
			vertices.Add (vert);
			
			return vert;
		}
		
		public Mesh createMesh()
		{
			Mesh mesh = new Mesh();
			mesh.vertices = verts.ToArray();
			mesh.triangles = indices.ToArray();
			mesh.normals = norms.ToArray();
			mesh.uv = uvs.ToArray();
			calculateTangents ();
			mesh.tangents = tangents.ToArray ();
			mesh.RecalculateBounds();
			MonoBehaviour.print("SSTU MeshGenerator: Creating Proceudral mesh with vertex count of: "+verts.Count);
			return mesh;
		}
		
		public void clear()
		{
			vertices.Clear ();
			verts.Clear();
			norms.Clear();
			uvs.Clear();
			indices.Clear();
			tangents.Clear ();
			vertexCount = 0;
		}
		
		//Code based on:
		//http://answers.unity3d.com/questions/7789/calculating-tangents-vector4.html
		//and adapted to use in-line lists from meshgenerator for calculation
		private void calculateTangents()
		{
			//variable definitions
			int triangleCount = indices.Count;
			int vertexCount = verts.Count;
			
			Vector3[] tan1 = new Vector3[vertexCount];
			Vector3[] tan2 = new Vector3[vertexCount];
			
			Vector4[] tangents = new Vector4[vertexCount];
			
			for (long a = 0; a < triangleCount; a += 3)
			{
				long i1 = indices[(int)a + 0];
				long i2 = indices[(int)a + 1];
				long i3 = indices[(int)a + 2];
				
				Vector3 v1 = verts[(int)i1];
				Vector3 v2 = verts[(int)i2];
				Vector3 v3 = verts[(int)i3];
				
				Vector2 w1 = uvs[(int)i1];
				Vector2 w2 = uvs[(int)i2];
				Vector2 w3 = uvs[(int)i3];
				
				float x1 = v2.x - v1.x;
				float x2 = v3.x - v1.x;
				float y1 = v2.y - v1.y;
				float y2 = v3.y - v1.y;
				float z1 = v2.z - v1.z;
				float z2 = v3.z - v1.z;
				
				float s1 = w2.x - w1.x;
				float s2 = w3.x - w1.x;
				float t1 = w2.y - w1.y;
				float t2 = w3.y - w1.y;
				
				float r = 1.0f / (s1 * t2 - s2 * t1);
				
				Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
				Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);
				
				tan1[i1] += sdir;
				tan1[i2] += sdir;
				tan1[i3] += sdir;
				
				tan2[i1] += tdir;
				tan2[i2] += tdir;
				tan2[i3] += tdir;
			}
						
			for (long a = 0; a < vertexCount; ++a)
			{
				Vector3 n = norms[(int)a];
				Vector3 t = tan1[(int)a];
				Vector3.OrthoNormalize(ref n, ref t);
				tangents[a].x = t.x;
				tangents[a].y = t.y;
				tangents[a].z = t.z;				
				tangents[a].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[a]) < 0.0f) ? -1.0f : 1.0f;
			}
			
			this.tangents.Clear ();
			for (int i = 0; i < tangents.Length; i++)
			{
				this.tangents.Add (tangents[i]);
			}
		}
		
		//generates a wall of a cylinder with all normals pointing directly away from the center position
		public void generateCylinderWallSection(float centerX, float centerZ, float startY, float height, float topRadius, float bottomRadius, int sides, float anglePerSide, float startAngle, bool outsideWall)
		{						
			float x, y, y1, z, nx, ny, nz, u, angle;
			y = startY;
			y1 = startY + height;
			
			float uSize;
			uSize = u2 - u1;
			
			float radiusDiff = topRadius - bottomRadius;
			float sideAngle = Mathf.Atan2(height, radiusDiff) - 90*Mathf.Deg2Rad;
			float yCos = Mathf.Cos(sideAngle);
			float ySin = Mathf.Sin(sideAngle);
						
			Vertex[] verts1 = new Vertex[sides+1];
			Vertex[] verts2 = new Vertex[sides+1];									
			//add outer cylinder wall
			for (int i = 0; i < sides+1; i++) 
			{				
				angle = startAngle + (anglePerSide * (float)i);
				x = Mathf.Cos(angle * Mathf.Deg2Rad);
				z = Mathf.Sin(angle * Mathf.Deg2Rad);	
				nx = (outsideWall? x : -x) * yCos;
				ny = ySin;
				nz = (outsideWall? z : -z) * yCos;
				u = ((float)i / (float)(sides) * uSize) + u1;				
				verts1[i] = addVertex(x*topRadius + centerX, y1, z*topRadius + centerZ, nx, ny, nz, u, v1);//upper ring vertex
				verts2[i] = addVertex(x*bottomRadius + centerX, y, z*bottomRadius + centerZ, nx, ny, nz, u, v2);//lower ring vertex
			}
			
			if(outsideWall)
			{
				for (int i = 0; i < sides; i++) 
				{
					addTriangle(verts1[i].index, verts1[i+1].index, verts2[i].index);
					addTriangle(verts1[i+1].index, verts2[i+1].index, verts2[i].index);
				}
			}
			else
			{
				for (int i = 0; i < sides; i++) 
				{
					addTriangle(verts1[i+1].index, verts1[i].index, verts2[i].index);
					addTriangle(verts2[i+1].index, verts1[i+1].index, verts2[i].index);
				}
			}
			
		}
		
		//generates a vertical strip for a sidewall of a cylinder, e.g. the flat vertical side wall of a fairing panel
		public void generateCylinderPanelSidewall(float centerX, float centerZ, float bottomY, float height, float topOuterRadius, float topInnerRadius, float bottomOuterRadius, float bottomInnerRadius, float angle, bool invertTris)
		{
			float x, z, nx, ny, nz;
			float xto, xti, xbo, xbi, zto, zti, zbo, zbi;
			
			Vertex[] verts = new Vertex[4];
			x = Mathf.Cos(angle * Mathf.Deg2Rad);
			z = Mathf.Sin(angle * Mathf.Deg2Rad);
			
			xto = x * topOuterRadius + centerX;
			xti = x * topInnerRadius + centerX;
			xbo = x * bottomOuterRadius + centerX;
			xbi = x * bottomInnerRadius + centerX;
			zto = z * topOuterRadius + centerZ;
			zti = z * topInnerRadius + centerZ;
			zbo = z * bottomOuterRadius + centerZ;
			zbi = z * bottomInnerRadius + centerZ;
			
			nx = Mathf.Cos((angle+90) * Mathf.Deg2Rad) * (invertTris ? -1 : 1);
			nz = Mathf.Sin((angle+90) * Mathf.Deg2Rad) * (invertTris ? -1 : 1);
			ny = 0;	
			
			//outer verts
			verts[0] = addVertex (xbo, bottomY, zbo, nx, ny, nz, u2, v1);//outer bottom//v3
			verts[1] = addVertex (xto, bottomY+height,zto, nx, ny, nz, u1, v1);//outer top//v1
			//inner verts
			verts[2] = addVertex (xbi, bottomY, zbi, nx, ny, nz, u2, v2);//inner bottom//v4
			verts[3] = addVertex (xti, bottomY+height, zti, nx, ny, nz, u1, v2);//inner top//v2
			
			if(invertTris)
			{	
				addTriangle (verts[1].index, verts[0].index, verts[2].index);
				addTriangle (verts[3].index, verts[1].index, verts[2].index);		
			}
			else
			{
				addTriangle (verts[0].index, verts[1].index, verts[2].index);
				addTriangle (verts[1].index, verts[3].index, verts[2].index);				
			}
		}
		
		//generates a triangle fan, with a single vertex in the center; uv is mapped as if the center vertex is the center of the uv area
		public void generateTriangleFan(float centerX, float centerZ, float y, float radius, int sides, float anglePerSide, float startAngle, bool top)
		{
			float x, z, nx, ny, nz, u, v, angle;
			
			float uSizeHalf, vSizeHalf;
			float uCenter, vCenter;
			uSizeHalf = (u2 - u1) * 0.5f;
			vSizeHalf = (v2 - v1) * 0.5f;
			uCenter = u1 + uSizeHalf;
			vCenter = v1 + vSizeHalf;
			
			Vertex center = addVertex(centerX, y, centerZ, 0, top? 1 : -1, 0, uCenter, vCenter);
			Vertex[] verts1 = new Vertex[sides+1];
			for (int i = 0; i < sides+1; i++) 
			{				
				angle = startAngle + (anglePerSide * (float)i);
				x = Mathf.Cos(angle * Mathf.Deg2Rad);
				z = Mathf.Sin(angle * Mathf.Deg2Rad);	
				nx = 0;
				ny = top ? 1 : -1;
				nz = 0;
				u = uCenter + x * uSizeHalf;
				v = vCenter + z * vSizeHalf;
				verts1[i] = addVertex(x*radius+centerX, y, z*radius+centerZ, nx, ny, nz, u, v);
				if(i>0)
				{
					if(top)
					{
						addTriangle(verts1[i-1].index, center.index, verts1[i].index);						
					}
					else
					{					
						addTriangle(center.index, verts1[i-1].index, verts1[i].index);	
					}
				}
			}
		}
		
		//generates a partial cylinder cap, e.g. for the top of a fairing panel
		public void generateCylinderPartialCap(float centerX, float centerZ, float height, float outerRadius, float innerRadius, int sides, float anglePerSide, float startAngle, bool top)
		{						
			float x, z, nx, ny, nz, u, angle;
			float uSize;
			uSize = u2 - u1;
			
			Vertex[] verts1 = new Vertex[sides+1];
			Vertex[] verts2 = new Vertex[sides+1];
			for (int i = 0; i < sides+1; i++) 
			{				
				angle = startAngle + (anglePerSide * (float)i);
				x = Mathf.Cos(angle * Mathf.Deg2Rad);
				z = Mathf.Sin(angle * Mathf.Deg2Rad);	
				nx = 0;
				ny = top ? 1 : -1;
				nz = 0;
				u = ((float)i / (float)(sides) * uSize) + u1;	
				verts1[i] = addVertex(x*outerRadius+centerX, height, z*outerRadius+centerZ, nx, ny, nz, u, v1);//outer ring vertex
				verts2[i] = addVertex(x*innerRadius+centerX, height, z*innerRadius+centerZ, nx, ny, nz, u, v2);//inner ring vertex
			}
			
			if(top)
			{
				for (int i = 0; i < sides; i++) 
				{
					addTriangle(verts1[i+1].index, verts1[i].index, verts2[i].index);
					addTriangle(verts2[i+1].index, verts1[i+1].index, verts2[i].index);
				}				
			}
			else
			{
				for (int i = 0; i < sides; i++) 
				{
					addTriangle(verts1[i].index, verts1[i+1].index, verts2[i].index);
					addTriangle(verts1[i+1].index, verts2[i+1].index, verts2[i].index);
				}
			}
		}
		
	}
	
	public class Vertex
	{
		public Vector3 vertex;
		public Vector3 normal;
		public Vector2 uv;
		public int index;
	}
	
	public class UVArea
	{
		public float u1;
		public float u2;
		public float v1;
		public float v2;
		
		public UVArea(UVArea input)
		{
			this.u1 = input.u1;
			this.u2 = input.u2;
			this.v1 = input.v1;
			this.v2 = input.v2;
		}
		
		public UVArea(float u1, float v1, float u2, float v2)
		{
			this.u1 = u1;
			this.v1 = v1;
			this.u2 = u2;
			this.v2 = v2;
		}
		
		public UVArea(int x1, int y1, int x2, int y2, int textureSize)
		{
			float areaPerPx = 1.0f / (float)textureSize;
			u1 = (float)x1 * areaPerPx;
			u2 = (float)x2 * areaPerPx;
			v1 = (float)(textureSize-1 - (y2-1)) * areaPerPx;
			v2 = (float)(textureSize-1 - y1) * areaPerPx;
		}
	}
	
}

