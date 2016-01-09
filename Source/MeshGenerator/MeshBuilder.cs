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
            float totalAngle = endAngle - startAngle;
            float anglePerFace = 360f / faces;
            int numOfFaces = (int)(totalAngle / anglePerFace);
            float angle, xCos, zSin, nx, ny, nz, x, y, z;

            Vector2 min = new Vector2(-uvRadius, -uvRadius);
            Vector2 max = new Vector2(uvRadius, uvRadius);
            Vector2 pos;
            Vector2 uv;
            for (int i = 0; i < numOfFaces + 1; i++)
            {
                angle = startAngle + anglePerFace * i;
                xCos = Mathf.Cos(angle * Mathf.Deg2Rad);
                zSin = Mathf.Sin(angle * Mathf.Deg2Rad);
                x = xCos * radius + offset.x;
                y = height + offset.y;
                z = zSin * radius + offset.z;
                nx = xCos * yCos;
                ny = ySin;
                nz = zSin * yCos;
                pos = new Vector2(xCos * radius, zSin * radius);
                uv = area.getUV(min, max, pos);
                verts.Add(addVertex(new Vector3(x, y, z), new Vector3(nx, ny, nz), uv));
            }
            return verts;
        }

        public List<Vertex> generateRadialVerticesFlatUVs(Vector3 offset, int faces, float radius, float height, float startAngle, float endAngle, float v, UVArea area, float yCos, float ySin)
        {
            List<Vertex> verts = new List<Vertex>();
            float totalAngle = endAngle - startAngle;
            float anglePerFace = 360f / faces;
            int numOfFaces = (int)(totalAngle / anglePerFace);

            float anglePerVert = totalAngle / (float)(numOfFaces + 1);

            float angle, xCos, zSin, nx, ny, nz, x, y, z, percentTotalAngle;

            for (int i = 0; i < numOfFaces + 1; i++)
            {
                angle = startAngle + anglePerFace * i;
                percentTotalAngle = (angle - startAngle) / totalAngle;

                xCos = Mathf.Cos(angle * Mathf.Deg2Rad);
                zSin = Mathf.Sin(angle * Mathf.Deg2Rad);
                x = xCos * radius + offset.x;
                y = height + offset.y;
                z = zSin * radius + offset.z;
                nx = xCos * yCos;
                ny = ySin;
                nz = zSin * yCos;
                verts.Add(addVertex(new Vector3(x, y, z), new Vector3(nx, ny, nz), new Vector2(area.getU(percentTotalAngle), v)));
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
        public float u1;
        public float u2;
        public float v1;
        public float v2;

        public UVArea(ConfigNode node)
        {
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
}
