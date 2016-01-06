using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{

    public class ArcGeneratorTest : MonoBehaviour
    {

        public void Start()
        {
            print("PCG Start!");
            ArcMeshGenerator amg = new ArcMeshGenerator(Vector3.zero, 24, 4, 0f, 360f, 0.1f);
            amg.addArc(0.0f, 1.00f);
            amg.addArc(1.5f, 1.25f);
            amg.addArc(3.0f, 1.25f);
            amg.addArc(4.5f, 1.00f);
            amg.addArc(5.0f, 0.75f);
            GameObject[] gos = amg.generatePanels();
            foreach (GameObject go in gos)
            {
                go.transform.parent = gameObject.transform;
                go.transform.localPosition = Vector3.zero;
                go.renderer.material = new Material(Shader.Find("Diffuse"));
            }
        }
    }

    public class ArcMeshGenerator
    {
        /**
        * Input is the height and radius of each edge loop/ring
        *
        * generator will split each ring into edge loops, group edge loops vertically, 
        * and generate vertical panel sections dependant upon the generator settings.
        * Panels may be optionally confetti-ized (splitPanels flag)
        * 
        * Output is an array of game objects, one per panel.  They will have no material set, only mesh/meshrenderer.
        *
        * TODO - determine how to generate convex colliders, or if to do such at all.
        * 
        **/

        List<Ring> rings = new List<Ring>();//the rings that make up the fairing, for multi-panel or multi-angled fairings
        private Vector3 offset = Vector3.zero;
        private int sides;//cylinder sides
        private int panels;//number of panels comprising the fairing
        private float startAngle;
        private float endAngle;
        private float thickness;

        //TODO fix these, off by one pixel somewhere...
        UVArea outsideUV = new UVArea(0.00390625f, 0.00390625f, 0.49609375f, 0.99609375f);
        UVArea insideUV = new UVArea(0.50390625f, 0.00390625f, 0.99609375f, 0.99609375f);
        UVArea edgesUV = new UVArea(0.50390625f, 0.00390625f, 0.99609375f, 0.99609375f);

        private PanelEdgeGroup[] panelGroups;

        public ArcMeshGenerator(Vector3 offset, int sides, int panels, float startAngle, float endAngle, float thickness)
        {
            this.offset = offset;
            this.sides = sides;
            this.panels = panels;
            this.startAngle = startAngle;
            this.endAngle = endAngle;
            this.thickness = thickness;
        }

        public void addArc(float height, float radius)
        {
            rings.Add(new Ring(height, radius));
        }

        public GameObject[] generatePanels()
        {
            generatePanelGroups();
            //each panelEdgeGroup should now contain the vertical edge loops for each vertical panel grouping
            //from here, need to generate meshes for panels depending upon splitPanels setting
            int len = panelGroups.Length;
            GameObject[] gos = new GameObject[panels];
            for (int i = 0; i < len; i++)
            {
                gos[i] = new GameObject();
                MeshFilter mf = gos[i].AddComponent<MeshFilter>();
                mf.mesh = panelGroups[i].generatePanels(offset, thickness, outsideUV, insideUV, edgesUV);
                MeshRenderer mr = gos[i].AddComponent<MeshRenderer>();
            }
            return gos;
        }

        private void generatePanelGroups()
        {
            panelGroups = new PanelEdgeGroup[panels];
            for (int i = 0; i < panels; i++)
            {
                panelGroups[i] = new PanelEdgeGroup();
            }

            List<RingArc[]> vertexLoops = new List<RingArc[]>();
            float outerRadius, innerRadius, innerScale;
            foreach (Ring ring in rings)
            {
                outerRadius = ring.radius;
                innerRadius = ring.radius - thickness;
                RingArc[] loops = generateEdgeLoops(offset, ring.height, ring.radius, sides, panels);
                for (int i = 0; i < panels; i++)
                {
                    panelGroups[i].addVertexLoop(loops[i], innerRadius);
                }
            }
        }

        /// <summary>
        /// Generates the exterior vertex loops for a ring at the given height and radius, for the current start/end angles
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="height"></param>
        /// <param name="radius"></param>
        /// <param name="sides"></param>
        /// <param name="panels"></param>
        /// <returns></returns>
        private RingArc[] generateEdgeLoops(Vector3 offset, float height, float radius, int sides, int panels)
        {
            RingArc[] loops = new RingArc[panels];

            float totalAngle = endAngle - startAngle;
            float anglePerSide = 360.0f / (float)sides;
            float anglePerPanel = totalAngle / (float)panels;
            int sidesPerPanel = (int)(anglePerPanel / anglePerSide);

            int len = panels;
            float start = startAngle;
            float end;
            for (int i = 0; i < len; i++)
            {
                start = startAngle + anglePerPanel * i;
                end = start + anglePerPanel;
                loops[i] = new RingArc(radius, height, start, end, sidesPerPanel);
            }
            return loops;
        }
    }

    public class Ring
    {
        public readonly float radius;
        public readonly float height;

        public Ring(float height, float radius)
        {
            this.height = height;
            this.radius = radius;
        }
    }

    public class RingArc
    {
        public float radius;//used to determine angle of normals when paired with another vertexLoop
        public float height;//the Y position of this vertLoop
        public float startAngle, endAngle;
        public float length;
        public int faces;

        public RingArc(float radius, float height, float start, float end, int faces)
        {
            this.radius = radius;
            this.height = height;
            this.startAngle = start;
            this.endAngle = end;
            this.faces = faces;

            float percentageOfCircumference = 360.0f / (end - start);
            float circumference = Mathf.PI * (radius * 2);
            length = circumference * percentageOfCircumference;
        }

        //converts this outer loop into an inner loop
        public RingArc getInnerLoop(float newRadius)
        {
            return new RingArc(newRadius, height, startAngle, endAngle, faces);
        }

        public List<Vertex> generateVertices(MeshBuilder builder, Vector3 offset, float u1, float u2, float v, float wallAngleRadians, bool invertFaces)
        {
            List<Vertex> vertices = new List<Vertex>();
            int vertsToCreate = faces + 1;
            float uPerFace = (u2 - u1) / faces;
            float anglePerFace = (endAngle - startAngle) / (float)faces;
            float percentUPerFace = 1.0f / (float)faces;
            float uDelta = u2 - u1;
            float x, y, z, nx, ny, nz, u;
            float yCos = Mathf.Cos(wallAngleRadians);
            float ySin = Mathf.Sin(wallAngleRadians);
            float xCos, zSin;
            float angle;

            for (int i = 0; i < vertsToCreate; i++)
            {
                angle = startAngle + i * anglePerFace;
                xCos = Mathf.Cos(angle * Mathf.Deg2Rad);
                zSin = Mathf.Sin(angle * Mathf.Deg2Rad);

                x = xCos * radius + offset.x;
                y = height + offset.y;
                z = zSin * radius + offset.z;

                nx = (invertFaces ? -xCos : xCos) * yCos;
                ny = ySin;
                nz = (invertFaces ? -zSin : zSin) * yCos;

                u = u1 + (uDelta * percentUPerFace * (float)i);
                vertices.Add(builder.addVertex(new Vector3(x, y, z), new Vector3(nx, ny, nz), new Vector2(u, v)));
            }

            return vertices;
        }
    }

    public class PanelEdgeGroup
    {
        //list of vertex arc loops that make up this vertical panel grouping
        //if (splitPanels==true) each of these panel segments will form its own mesh
        //else they will all be combined into one mesh object.
        private List<RingArc> outerLoops = new List<RingArc>();
        private List<RingArc> innerLoops = new List<RingArc>();//calculated from outer loops
        private List<PanelSegment> panelSegments = new List<PanelSegment>();//basically a UV island / joined mesh

        public void addVertexLoop(RingArc loop, float innerRadius)
        {
            outerLoops.Add(loop);
            innerLoops.Add(loop.getInnerLoop(innerRadius));
        }

        public Mesh generatePanels(Vector3 pos, float thickness, UVArea outer, UVArea inner, UVArea caps)
        {
            int len = outerLoops.Count;

            for (int i = 0; i < len - 1; i++)
            {
                panelSegments.Add(new PanelSegment(outerLoops[i], outerLoops[i + 1], outer, false));
                panelSegments.Add(new PanelSegment(innerLoops[i], innerLoops[i + 1], inner, true));
            }
            panelSegments.Add(new PanelSegment(outerLoops[0], innerLoops[0], caps, true));
            panelSegments.Add(new PanelSegment(outerLoops[outerLoops.Count - 1], innerLoops[innerLoops.Count - 1], caps, false));

            MeshBuilder builder = new MeshBuilder();

            foreach (PanelSegment seg in panelSegments)
            {
                seg.generateMesh(builder, pos);
            }

            generateSidewalls(builder, caps);

            return builder.buildMesh();
        }

        private void generateSidewalls(MeshBuilder builder, UVArea caps)
        {
            float start = outerLoops[0].startAngle;
            float end = outerLoops[0].endAngle;
            if (start % 360f == end % 360f)//full circle, do not generate sidewalls
            {
                return;
            }
            Vector3 v1, v2, v3, v4;

            int len = outerLoops.Count;
            for (int i = 0; i < len; i++)
            {
            }
        }
    }

    public class PanelSidewall
    {

    }

    public class PanelSegment
    {
        private RingArc arcA = null, arcB = null;
        private UVArea area;
        private bool invertFaces = false;        

        public PanelSegment(RingArc loopA, RingArc loopB, UVArea area, bool invertFaces)
        {
            this.arcA = loopA;
            this.arcB = loopB;
            this.area = area;
            this.invertFaces = invertFaces;
        }

        public void generateMesh(MeshBuilder builder, Vector3 pos)
        {
            float heightDiff = arcB.height - arcA.height;
            float offset = arcA.radius - arcB.radius;
            float sideAngleRadians = Mathf.Atan2(heightDiff, offset) - 90 * Mathf.Deg2Rad;

            List<Vertex> verts1 = new List<Vertex>();
            List<Vertex> verts2 = new List<Vertex>();
            verts1.AddRange(arcA.generateVertices(builder, pos, area.u1, area.u2, area.v1, sideAngleRadians, invertFaces));
            verts2.AddRange(arcB.generateVertices(builder, pos, area.u1, area.u2, area.v2, sideAngleRadians, invertFaces));
            int sides = verts1.Count - 1;
            if (invertFaces)
            {
                for (int i = 0; i < sides; i++)
                {
                    builder.addTriangle(verts1[i].index, verts1[i + 1].index, verts2[i].index);
                    builder.addTriangle(verts1[i + 1].index, verts2[i + 1].index, verts2[i].index);
                }
            }
            else
            {
                for (int i = 0; i < sides; i++)
                {
                    builder.addTriangle(verts1[i + 1].index, verts1[i].index, verts2[i].index);
                    builder.addTriangle(verts2[i + 1].index, verts1[i + 1].index, verts2[i].index);
                }
            }
        }
    }

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
    /// Data class to represent a single complete vertex, including normal and UV data
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
			v1 = (float)(textureSize - 1 - (y2 - 1)) * areaPerPx;
			v2 = (float)(textureSize - 1 - y1) * areaPerPx;
		}
	}
}
