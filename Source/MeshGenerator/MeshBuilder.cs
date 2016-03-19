using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    /// <summary>
    /// Generic mesh-building class; input each vert and triangle as created, it will take care of creating the actual mesh and calculating tangents
    /// </summary>
    public class MeshBuilder
    {
        private int vertexNumber = 0;
        private List<Vertex> vertices = new List<Vertex>();
        private List<int> triangles = new List<int>();
        public int subdivision = 3;

        public Vertex addVertex(Vector3 vert, Vector3 norm, Vector2 uv)
        {
            Vertex vertex = new Vertex(vert, norm, uv, vertexNumber);
            vertexNumber++;
            vertices.Add(vertex);
            return vertex;
        }

        public void addTriangle(int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        public Mesh buildMesh()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = getVerts();
            mesh.triangles = getTriangles();
            mesh.normals = getNorms();
            mesh.uv = getUVs();
            mesh.tangents = calculateTangents();
            mesh.RecalculateBounds();
            mesh.name = "Procedural Mesh";
            MonoBehaviour.print("creating procedural mesh with vertex count of: " + mesh.vertices.Length);
            return mesh;
        }

        /// <summary>
        /// Creates an axis-aligned cube mesh at the given center point, with the given size dimensions, from the input UV coordinates.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="center"></param>
        /// <param name="uvStart"></param>
        /// <param name="uvEnd"></param>
        public void generateCuboid(Vector3 size, Vector3 center, Vector2 uvStart, Vector2 uvEnd)
        {
            Vector3 halfSize = size * 0.5f;

            Vector3 frontBottomLeft = new Vector3(-halfSize.x + center.x, -halfSize.y + center.y, -halfSize.z + center.z);
            Vector3 frontBottomRight = new Vector3(halfSize.x + center.x, -halfSize.y + center.y, -halfSize.z + center.z);
            Vector3 frontTopLeft = new Vector3(-halfSize.x + center.x, halfSize.y + center.y, -halfSize.z + center.z);
            Vector3 frontTopRight = new Vector3(halfSize.x + center.x, halfSize.y + center.y, -halfSize.z + center.z);

            Vector3 rearBottomLeft = new Vector3(-halfSize.x + center.x, -halfSize.y + center.y, halfSize.z + center.z);
            Vector3 rearBottomRight = new Vector3(halfSize.x + center.x, -halfSize.y + center.y, halfSize.z + center.z);
            Vector3 rearTopLeft = new Vector3(-halfSize.x + center.x, halfSize.y + center.y, halfSize.z + center.z);
            Vector3 rearTopRight = new Vector3(halfSize.x + center.x, halfSize.y + center.y, halfSize.z + center.z);

            Vector3 normFront = Vector3.forward;
            Vector3 normRear = Vector3.back;
            Vector3 normLeft = Vector3.left;
            Vector3 normRight = Vector3.right;
            Vector3 normUp = Vector3.up;
            Vector3 normDown = Vector3.down;

            Vector2 uv1 = new Vector2(uvStart.x, uvStart.y);
            Vector2 uv2 = new Vector2(uvEnd.x, uvStart.y);
            Vector2 uv3 = new Vector2(uvStart.x, uvEnd.y);
            Vector2 uv4 = new Vector2(uvEnd.x, uvEnd.y);


            List<Vertex> v1 = new List<Vertex>();
            List<Vertex> v2 = new List<Vertex>();

            //generate front face
            v1.Add(addVertex(frontBottomLeft, normFront, uv1));
            v1.Add(addVertex(frontBottomRight, normFront, uv2));
            v2.Add(addVertex(frontTopLeft, normFront, uv3));
            v2.Add(addVertex(frontTopRight, normFront, uv4));
            generateQuads(v1, v2, false);
            v1.Clear();
            v2.Clear();

            //generate rear face
            v1.Add(addVertex(rearBottomLeft, normRear, uv2));
            v1.Add(addVertex(rearBottomRight, normRear, uv1));
            v2.Add(addVertex(rearTopLeft, normRear, uv4));
            v2.Add(addVertex(rearTopRight, normRear, uv3));
            generateQuads(v1, v2, true);
            v1.Clear();
            v2.Clear();

            //generate left face
            v1.Add(addVertex(frontBottomLeft, normLeft, uv2));
            v1.Add(addVertex(rearBottomLeft, normLeft, uv1));
            v2.Add(addVertex(frontTopLeft, normLeft, uv4));
            v2.Add(addVertex(rearTopLeft, normLeft, uv3));
            generateQuads(v1, v2, true);
            v1.Clear();
            v2.Clear();

            //generate right face
            v1.Add(addVertex(frontBottomRight, normRight, uv1));
            v1.Add(addVertex(rearBottomRight, normRight, uv2));
            v2.Add(addVertex(frontTopRight, normRight, uv3));
            v2.Add(addVertex(rearTopRight, normRight, uv4));
            generateQuads(v1, v2, false);
            v1.Clear();
            v2.Clear();

            //generate top face
            v1.Add(addVertex(frontTopRight, normUp, uv2));
            v1.Add(addVertex(frontTopLeft, normUp, uv1));
            v2.Add(addVertex(rearTopRight, normUp, uv4));
            v2.Add(addVertex(rearTopLeft, normUp, uv3));
            generateQuads(v1, v2, true);
            v1.Clear();
            v2.Clear();

            //generate bottom face
            v1.Add(addVertex(frontBottomRight, normUp, uv1));
            v1.Add(addVertex(frontBottomLeft, normUp, uv2));
            v2.Add(addVertex(rearBottomRight, normUp, uv3));
            v2.Add(addVertex(rearBottomLeft, normUp, uv4));
            generateQuads(v1, v2, false);
            v1.Clear();
            v2.Clear();
        }

        public void generateQuads(List<Vertex> verts1, List<Vertex> verts2, bool invertFaces)
        {
            int sides = verts1.Count - 1;
            for (int i = 0; i < sides; i++)
            {
                //either set works, just different winds
                generateTriangle(verts1[i], verts2[i], verts2[i + 1], invertFaces);
                generateTriangle(verts1[i], verts2[i + 1], verts1[i + 1], invertFaces);

                //generateTriangle(verts1[i + 1], verts1[i], verts2[i], invertFaces);
                //generateTriangle(verts1[i + 1], verts2[i], verts2[i + 1], invertFaces);
            }
        }

        public void generateTriangleFan(List<Vertex> circ, Vertex cent, bool invertFaces)
        {
            int sides = circ.Count - 1;
            for (int i = 0; i < sides; i++)
            {
                generateTriangle(circ[i + 1], circ[i], cent, invertFaces);
            }
        }

        public void generateTriangleFan(List<Vertex> circ, List<Vertex> centers, bool invertFaces)
        {
            int sides = circ.Count - 1;
            for (int i = 0; i < sides; i++)
            {
                generateTriangle(circ[i + 1], circ[i], centers[i], invertFaces);
            }
        }

        public void generateTriangle(Vertex a, Vertex b, Vertex c, bool invertFace)
        {
            if (invertFace)
            {
                addTriangle(c.index, b.index, a.index);
            }
            else
            {
                addTriangle(a.index, b.index, c.index);
            }
        }

        public void generateCylinderCap(Vector3 offset, int faces, float outerRadius, float innerRadius, float height, float startAngle, float endAngle, UVArea area, bool bottomCap)
        {
            float ySin = bottomCap ? -1 : 1;
            List<Vertex> outerVerts = generateRadialVerticesCylinderUVs(offset, faces, outerRadius, height, startAngle, endAngle, area, outerRadius, 0, ySin);
            if (innerRadius > 0)//partial cap
            {
                List<Vertex> innerVerts = generateRadialVerticesCylinderUVs(offset, faces, innerRadius, height, startAngle, endAngle, area, outerRadius, 0, ySin);
                generateQuads(outerVerts, innerVerts, bottomCap);
            }
            else
            {
                Vector2 min = new Vector2(-outerRadius, -outerRadius);
                Vector2 max = new Vector2(outerRadius, outerRadius);
                Vector2 centerUV = area.getUV(min, max, new Vector2(0, 0));
                Vertex center = addVertex(offset + new Vector3(0, height, 0), bottomCap ? Vector3.down : Vector3.up, centerUV);
                generateTriangleFan(outerVerts, center, bottomCap);
            }
        }

        /// <summary>
        /// Generates a ring of vertices using circular UV mapping, where the center of the ring is the center of the UV area.
        /// This would be used to generate a partial ring, such as the procedural decoupler
        /// Input radius is the radius of the actual ring, uvRadius is the radius to be used for maximal UV bounds 
        /// (e.g. they will only be different on an inner-ring; where radius=ring radius, and uvRadius = outerRingRadius)
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="faces"></param>
        /// <param name="radius"></param>
        /// <param name="height"></param>
        /// <param name="startAngle"></param>
        /// <param name="endAngle"></param>
        /// <param name="area">The UV area to map the UV vertex to.</param>
        /// <param name="uvRadius">The radius to be used for the UV area, in case it differs from the radius in use (basically scales the UV area)</param>
        /// <param name="yCos">Determines the x/z normal component</param>
        /// <param name="ySin">Used directly as the y normal component</param>
        /// <returns></returns>
        public List<Vertex> generateRadialVerticesCylinderUVs(Vector3 offset, int faces, float radius, float height, float startAngle, float endAngle, UVArea area, float uvRadius, float yCos, float ySin)
        {
            List<Vertex> verts = new List<Vertex>();
            float startRad = startAngle * Mathf.Deg2Rad;
            float endRad = endAngle * Mathf.Deg2Rad;
            float totalRad = endRad - startRad;
            float radPerFace = (2f*Mathf.PI) / faces;
            //int numOfFaces = (int)(totalRad / radPerFace);
            int numOfFaces = (int)Math.Round(totalRad / radPerFace, 0);
            float rads, xCos, zSin, nx, nz, x, y, z;

            Vector2 min = new Vector2(-uvRadius, -uvRadius);
            Vector2 max = new Vector2(uvRadius, uvRadius);
            Vector2 pos;
            Vector2 uv;

            //unchanging vars
            y = height + offset.y;

            for (int i = 0; i < numOfFaces + 1; i++)
            {
                rads = startRad + (radPerFace * i);
                xCos = Mathf.Cos(rads);
                zSin = Mathf.Sin(rads);
                x = xCos * radius + offset.x;
                z = zSin * radius + offset.z;
                nx = xCos * yCos;
                nz = zSin * yCos;
                pos = new Vector2(xCos * radius, zSin * radius);
                uv = area.getUV(min, max, pos);
                verts.Add(addVertex(new Vector3(x, y, z), new Vector3(nx, ySin, nz), uv));
            }
            return verts;
        }

        /// <summary>
        /// Used to generate a ring of vertices with the uvMapping appropriate for a cylinder wall (increments in +x)
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="faces"></param>
        /// <param name="radius"></param>
        /// <param name="height"></param>
        /// <param name="startAngle"></param>
        /// <param name="endAngle"></param>
        /// <param name="v"></param>
        /// <param name="area"></param>
        /// <param name="yCos"></param>
        /// <param name="ySin"></param>
        /// <returns></returns>
        public List<Vertex> generateRadialVerticesFlatUVs(Vector3 offset, int faces, float radius, float height, float startAngle, float endAngle, float v, UVArea area, float yCos, float ySin)
        {
            List<Vertex> verts = new List<Vertex>();
            float startRad = startAngle * Mathf.Deg2Rad;
            float endRad = endAngle * Mathf.Deg2Rad;
            float totalRad = endRad - startRad;
            float radPerFace = (float)((2.0 * Math.PI) / faces);
            float radPerSub = radPerFace / subdivision;
            int numOfFaces = (int)Math.Round(totalRad / radPerFace, 0);

            float rads, xCos, zSin, nx, ny, nz, x, y, z, percentTotalAngle;
            float subRads, x2 = 0, z2 = 0, x3, z3, xCos2 = 0, zSin2 = 0, nextRads = 0, nextPerc=0, subPerc, lerp;

            y = height + offset.y;
            ny = ySin;

            for (int i = 0; i < numOfFaces + 1; i++)
            {
                if (i > 0)//vars were caled last round for the interpolation
                {
                    rads = nextRads;
                    percentTotalAngle = nextPerc;
                    xCos = xCos2;
                    zSin = zSin2;
                    x = x2;
                    z = z2;
                }
                else
                {
                    rads = startRad + (radPerFace * i);
                    percentTotalAngle = (rads - startRad) / totalRad;
                    xCos = Mathf.Cos(rads);
                    zSin = Mathf.Sin(rads);
                    x = xCos * radius + offset.x;
                    z = zSin * radius + offset.z;
                }                
                
                nx = xCos * yCos;
                nz = zSin * yCos;
                verts.Add(addVertex(new Vector3(x, y, z), new Vector3(nx, ny, nz), new Vector2(area.getU(percentTotalAngle), v)));
                
                //calculate the points/etc for the next panel face, to lerp between positions; data is re-used next iteration if present
                nextRads = startRad + (radPerFace * (i + 1));
                nextPerc = (nextRads - startRad) / totalRad;
                xCos2 = Mathf.Cos(nextRads);
                zSin2 = Mathf.Sin(nextRads);
                x2 = xCos2 * radius + offset.x;
                z2 = zSin2 * radius + offset.z;

                //do not add subdivision panel if the next vertex is the last one in the ring
                if (i == numOfFaces) { break; }
                //if was the last vert... do not add subs
                for (int k = 1; k < subdivision; k++)
                {
                    lerp = (float)k / subdivision;
                    x3 = Mathf.Lerp(x, x2, lerp);
                    z3 = Mathf.Lerp(z, z2, lerp);
                    subRads = rads + (radPerSub * k);//subangle used for normal calc... position calculated from interpolation to maintain cylinder side matching
                    subPerc = (subRads - startRad) / totalRad;
                    xCos = Mathf.Cos(subRads);
                    zSin = Mathf.Sin(subRads);
                    nx = xCos * yCos;
                    nz = zSin * yCos;
                    verts.Add(addVertex(new Vector3(x3, y, z3), new Vector3(nx, ny, nz), new Vector2(area.getU(subPerc), v)));
                }
            }
            return verts;
        }

        private Vector3[] getVerts()
        {
            int len = vertices.Count;
            Vector3[] verts = new Vector3[len];
            for (int i = 0; i < len; i++)
            {
                verts[i] = vertices[i].vertex;
            }
            return verts;
        }

        private Vector3[] getNorms()
        {
            int len = vertices.Count;
            Vector3[] norms = new Vector3[len];
            for (int i = 0; i < len; i++)
            {
                norms[i] = vertices[i].normal;
            }
            return norms;
        }

        private Vector2[] getUVs()
        {
            int len = vertices.Count;
            Vector2[] uvs = new Vector2[len];
            for (int i = 0; i < len; i++)
            {
                uvs[i] = vertices[i].uv;
            }
            return uvs;
        }

        private int[] getTriangles()
        {
            return triangles.ToArray();
        }

        //Code based on:
        //http://answers.unity3d.com/questions/7789/calculating-tangents-vector4.html
        //and adapted to use in-line values from vertex list
        private Vector4[] calculateTangents()
        {
            //variable definitions
            int vertexCount = vertices.Count;
            int triangleCount = triangles.Count;

            Vector3[] tan1 = new Vector3[vertexCount];
            Vector3[] tan2 = new Vector3[vertexCount];

            Vector4[] tangents = new Vector4[vertexCount];

            for (long a = 0; a < triangleCount; a += 3)
            {
                long i1 = triangles[(int)a + 0];
                long i2 = triangles[(int)a + 1];
                long i3 = triangles[(int)a + 2];

                Vector3 v1 = vertices[(int)i1].vertex;
                Vector3 v2 = vertices[(int)i2].vertex;
                Vector3 v3 = vertices[(int)i3].vertex;

                Vector2 w1 = vertices[(int)i1].uv;
                Vector2 w2 = vertices[(int)i2].uv;
                Vector2 w3 = vertices[(int)i3].uv;

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
                Vector3 n = vertices[(int)a].normal;
                Vector3 t = tan1[(int)a];
                Vector3.OrthoNormalize(ref n, ref t);
                tangents[a].x = t.x;
                tangents[a].y = t.y;
                tangents[a].z = t.z;
                tangents[a].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[a]) < 0.0f) ? -1.0f : 1.0f;
            }
            return tangents;
        }

    }


    /// <summary>
    /// Data class to represent a single complete vertex, including normal, UV and index data
    /// </summary>
    public class Vertex
    {
        public Vector3 vertex;
        public Vector3 normal;
        public Vector2 uv;
        public int index;
        public Vertex(Vector3 vert, Vector3 norm, Vector2 uv, int index)
        {
            this.vertex = vert;
            this.normal = norm;
            this.uv = uv;
            this.index = index;
        }
        public Vertex()
        {
            this.vertex = Vector3.zero;
            this.normal = Vector3.one;
            this.uv = Vector2.one;
            this.index = -1;
        }
    }

    /// <summary>
    /// Represents an area of a texture to be used by a set of faces.  Faces will be mapped to this area depending upon the type of mapping selected (straight or circular)
    /// </summary>
    public class UVArea
    {
        public String name;
        public float u1;
        public float u2;
        public float v1;
        public float v2;

        public UVArea(ConfigNode node)
        {
            name = node.GetStringValue("name");
            this.u1 = node.GetFloatValue("u1");
            this.u2 = node.GetFloatValue("u2");
            this.v1 = node.GetFloatValue("v1");
            this.v2 = node.GetFloatValue("v2");
        }

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
            v1 = (float)(textureSize - 1 - (y2 - 1)) * areaPerPx;
            v2 = (float)(textureSize - 1 - y1) * areaPerPx;
        }

        public Vector2 getUV(Vector2 min, Vector2 max, Vector2 vert)
        {
            float xSize = max.x - min.x;
            float ySize = max.y - min.y;
            float xPercent = (vert.x - min.x) / xSize;
            float yPercent = (vert.y - min.y) / ySize;
            Vector2 uv = getUV(xPercent, yPercent);
            return uv;
        }

        public Vector2 getUV(float xPercent, float yPercent)
        {
            return new Vector2(getU(xPercent), getV(yPercent));
        }

        public float getU(float xPercent)
        {
            float uSize = u2 - u1;
            return u1 + xPercent * uSize;
        }

        public float getV(float yPercent)
        {
            float vSize = v2 - v1;
            return v1 + yPercent * vSize;
        }

        public override string ToString()
        {
            return "{" + u1 + "," + u2 + "," + v1 + "," + v2 + "}";
        }
    }

    public class UVMap
    {
        private Dictionary<string, UVArea> uvAreas = new Dictionary<string, UVArea>();
        public UVMap(ConfigNode node)
        {
            ConfigNode[] areaNodes = node.GetNodes("UVAREA");
            int len = areaNodes.Length;
            for (int i = 0; i < len; i++)
            {
                UVArea area = new UVArea(areaNodes[i]);
                uvAreas.Add(area.name, area);
            }
        }

        public UVArea getArea(string name)
        {
            UVArea a = null;
            uvAreas.TryGetValue(name, out a);
            return a;
        }

        public static UVMap GetUVMapGlobal(string name)
        {
            if (String.IsNullOrEmpty(name)) { return null; }
            ConfigNode[] uvMapNodes = GameDatabase.Instance.GetConfigNodes("SSTU_UVMAP");
            ConfigNode uvMapNode = Array.Find(uvMapNodes, m=>m.GetStringValue("name") == name);
            UVMap map = null;
            if (uvMapNode != null)
            {
                map = new UVMap(uvMapNode);
            }
            return map;
        }
    }
}
