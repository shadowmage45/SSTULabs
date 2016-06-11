using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{

    public class CylinderMeshGenerator
    {
        private Vector3 offset = Vector3.zero;
        private float height;
        private float bottomRadius;
        private float topRadius;
        private float bottomInnerRadius;
        private float topInnerRadius;
        private int faces;
        public UVArea outsideUV = new UVArea(0.00390625f, 0.00390625f, 0.99609375f, 0.99609375f);
        public UVArea insideUV = new UVArea(0.00390625f, 0.00390625f, 0.99609375f, 0.99609375f);
        public UVArea topUV = new UVArea(0.00390625f, 0.00390625f, 0.99609375f, 0.99609375f);
        public UVArea bottomUV = new UVArea(0.00390625f, 0.00390625f, 0.99609375f, 0.99609375f);

        public CylinderMeshGenerator(Vector3 offset, int faces, float height, float bottomRadius, float topRadius, float bottomInnerRadius, float topInnerRadius)
        {
            this.offset = offset;
            this.faces = faces;
            this.height = height;
            this.bottomRadius = bottomRadius;
            this.topRadius = topRadius;
            this.bottomInnerRadius =  bottomInnerRadius;
            this.topInnerRadius = topInnerRadius;
        }

        public Mesh generateMesh()
        {
            MeshBuilder builder = new MeshBuilder();
            Arc bottomOuterArc = new Arc(bottomRadius, 0, 0, 360, faces);
            Arc topOuterArc = new Arc(topRadius, height, 0, 360, faces);
            List<Vertex> verts1 = bottomOuterArc.generateVertices(builder, offset, outsideUV, outsideUV.v1, 1, 0);
            List<Vertex> verts2 = topOuterArc.generateVertices(builder, offset, outsideUV, outsideUV.v2, 1, 0);
            builder.generateQuads(verts1, verts2, false);
            if (bottomInnerRadius != 0 || topInnerRadius != 0)
            {
                Arc bottomInnerArc = new Arc(bottomInnerRadius, 0, 0, 360, faces);
                Arc topInnerArc = new Arc(topInnerRadius, height, 0, 360, faces);
                float heightDiff = topInnerArc.height - bottomInnerArc.height;
                float radiusDiff = bottomInnerArc.radius - topInnerArc.radius;
                float sideRadians = Mathf.Atan2(heightDiff, radiusDiff) - 90 * Mathf.Deg2Rad;
                float yCos = Mathf.Cos(sideRadians);
                float ySin = Mathf.Sin(sideRadians);
                verts1 = bottomInnerArc.generateVertices(builder, offset, insideUV, insideUV.v1, -yCos, ySin);
                verts2 = topInnerArc.generateVertices(builder, offset, insideUV, insideUV.v2, -yCos, ySin);
                builder.generateQuads(verts1, verts2, true);
            }
            builder.generateCylinderCap(offset, faces, bottomRadius, bottomInnerRadius, 0, 0, 360, bottomUV, true);
            builder.generateCylinderCap(offset, faces, topRadius, topInnerRadius, height, 0, 360, topUV, false);

            Mesh mesh = builder.buildMesh();
            mesh.name = "ProceduralCylinderMesh";
            return mesh;
        }

        public GameObject[] generateColliders()
        {
            GameObject[] colliders = new GameObject[faces];
            
            float anglePerFace = 360f / (float)faces;
            float start = 0;
            float end = start + anglePerFace;
            float thickness = topRadius - topInnerRadius;
            
            Mesh mesh;
            MeshFilter mf;
            MeshCollider mc;
            MeshBuilder builder = new MeshBuilder();
            
            GameObject collider;
            for (int i = 0; i < faces; i++)
            {
                collider = new GameObject("ProceduralCylinderCollider-" + i);                
                colliders[i] = collider;
                start = (float)i * anglePerFace;
                end = start + anglePerFace;
                builder.generatePanelCollider(offset, start, end, 0, height, bottomRadius, topRadius, thickness);
                mesh = builder.buildMesh();
                mf = collider.AddComponent<MeshFilter>();
                mf.mesh = mesh;
                mc = collider.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
                builder.clear();
            }
            return colliders;
        }
    }

    public class ArcRing
    {
        public readonly float radius;
        public readonly float height;
        public ArcRing(float height, float radius)
        {
            this.height = height;
            this.radius = radius;
        }
    }

    public class ArcMeshGenerator
    {
        /**
        * Input is the height and radius of each edge loop/ring
        *
        * generator will split each ring into edge loops, group edge loops vertically, 
        * and generate vertical panel sections dependant upon the generator settings.
        * 
        * Output is an array of game objects, one per panel.  They will have no material set, only mesh/meshrenderer.
        * Optionally may include a set of collider objects per X faces
        *
        * TODO - determine how to generate convex colliders, or if to do such at all.
        * 
        **/

        private Vector3 offset = Vector3.zero;
        private int faces;//cylinder sides
        private int panels;//number of panels comprising the fairing
        private float startAngle;
        private float endAngle;
        private float thickness;

        //TODO fix these, off by one pixel somewhere...
        public UVArea outsideUV = new UVArea(0.00390625f, 0.00390625f, 0.49609375f, 0.99609375f);
        public UVArea insideUV = new UVArea(0.50390625f, 0.00390625f, 0.99609375f, 0.99609375f);
        public UVArea edgesUV = new UVArea(0.50390625f, 0.00390625f, 0.99609375f, 0.99609375f);

        private PanelArcGroup[] panelGroups;
        private bool colliders;
        private int facesPerCollider;

        public ArcMeshGenerator(Vector3 offset, int sides, int panels, float startAngle, float endAngle, float thickness, bool colliders, int facesPerCollider)
        {
            this.offset = offset;
            this.faces = sides;
            this.panels = panels;
            this.startAngle = startAngle;
            this.endAngle = endAngle;
            this.thickness = thickness;
            this.colliders = colliders;
            this.facesPerCollider = facesPerCollider;
            generatePanelGroups();
        }

        public void addArc(float height, float radius)
        {
            foreach (PanelArcGroup peg in panelGroups)
            {
                peg.addArc(radius, height);
            }
        }

        public GameObject[] generatePanels(Transform parent)
        {
            //each panelEdgeGroup should now contain the vertical edge loops for each vertical panel grouping
            //from here, need to generate meshes for panels depending upon splitPanels setting
            int len = panelGroups.Length;
            GameObject[] gos = new GameObject[panels];
            for (int i = 0; i < len; i++)
            {
                gos[i] = parent.FindOrCreate("FairingPanel-"+i).gameObject;
                SSTUUtils.destroyChildren(gos[i].transform);//remove any existing colliders
                MeshFilter mf = gos[i].GetComponent<MeshFilter>();
                if (mf == null) { mf = gos[i].AddComponent<MeshFilter>(); }
                mf.mesh = panelGroups[i].generatePanels(offset, outsideUV, insideUV, edgesUV);
                if (colliders)
                {
                    GameObject[] cols = panelGroups[i].generateColliders(offset, facesPerCollider);
                    for (int k = 0; k < cols.Length; k++) { cols[k].transform.NestToParent(gos[i].transform); }
                }
                MeshRenderer mr = gos[i].GetComponent<MeshRenderer>();
                if (mr == null) { mr = gos[i].AddComponent<MeshRenderer>(); }
                gos[i].transform.parent = parent;
                gos[i].transform.localPosition = Vector3.zero;
                gos[i].transform.rotation = parent.rotation;
            }
            Transform[] trs;
            for (int i = len; i <8; i++)//destroy extra unused panels
            {
                trs = parent.transform.FindChildren("FairingPanel-" + i);
                for (int k = 0; k < trs.Length; k++) { GameObject.Destroy(trs[k].gameObject); }
            }
            return gos;
        }

        public GameObject[] generatePanels(Transform parent, Vector3 pivot)
        {
            //each panelEdgeGroup should now contain the vertical edge loops for each vertical panel grouping
            //from here, need to generate meshes for panels depending upon splitPanels setting
            int len = panelGroups.Length;
            GameObject[] gos = generatePanels(parent);
            float newX, newZ;
            for (int i = 0; i < len; i++)
            {
                float yRot = panelGroups[i].getPivotRotation();
                float dist = Mathf.Sqrt(pivot.x * pivot.x + pivot.z * pivot.z);

                newX = Mathf.Cos(yRot * Mathf.Deg2Rad)*dist;
                newZ = Mathf.Sin(yRot * Mathf.Deg2Rad)*dist;                
                Vector3 newPivot = new Vector3(newX, pivot.y, newZ);
                                                
                GameObject pivotObject = parent.FindOrCreate("FairingPanelPivot-"+i).gameObject;
                pivotObject.transform.parent = parent;
                pivotObject.transform.rotation = parent.transform.rotation;
                pivotObject.transform.localPosition = newPivot + offset;
                pivotObject.transform.Rotate(new Vector3(0, 1, 0), -yRot + 90f, Space.Self);                
                
                gos[i].transform.parent = pivotObject.transform;
                gos[i] = pivotObject;
            }
            return gos;
        }

        private void generatePanelGroups()
        {
            float totalAngle = endAngle - startAngle;
            float anglePerSide = 360.0f / (float)faces;
            float anglePerPanel = totalAngle / (float)panels;

            int len = panels;
            float start = startAngle;
            float end;
            panelGroups = new PanelArcGroup[panels];

            for (int i = 0; i < len; i++)
            {
                start = startAngle + anglePerPanel * i;
                end = start + anglePerPanel;
                panelGroups[i] = new PanelArcGroup(start, end, thickness, faces, colliders);
            }
        }
    }

    public class Arc
    {
        public readonly float radius;
        public readonly float height;
        public readonly float startAngle;
        public readonly float endAngle;
        public readonly float length;
        public readonly int faces;
        public Vector3 startVector;
        public Vector3 endVector;

        public Arc(float radius, float height, float start, float end, int faces)
        {
            this.radius = radius;
            this.height = height;
            this.startAngle = start;
            this.endAngle = end;
            this.faces = faces;

            float percentageOfCircumference = (end - start) / 360.0f;
            float circumference = Mathf.PI * (radius * 2);
            length = circumference * percentageOfCircumference;
        }

        public List<Vertex> generateVertices(MeshBuilder builder, Vector3 offset, UVArea area, float v, float yCos, float ySin)
        {
            List<Vertex> vertices = builder.generateRadialVerticesFlatUVs(offset, faces, radius, height, startAngle, endAngle, v, area, yCos, ySin);            
            startVector = vertices[0].vertex;
            endVector = vertices[vertices.Count - 1].vertex;
            return vertices;
        }

        public Vector3 getPivotPoint()
        {
            float totalAngle = endAngle - startAngle;
            float halfTotalAngle = totalAngle * 0.5f;
            float midPointAngle = startAngle + halfTotalAngle;
            midPointAngle -= 90f;

            float xCos = Mathf.Cos(midPointAngle * Mathf.Deg2Rad);
            float zSin = Mathf.Sin(midPointAngle * Mathf.Deg2Rad);
            float x = xCos * radius;
            float z = -zSin * radius;
            return new Vector3(x, height, z);
        }

        public float getPivotYRotation()
        {
            float totalAngle = endAngle - startAngle;
            float halfTotalAngle = totalAngle * 0.5f;
            float midPointAngle = startAngle + halfTotalAngle;
            return midPointAngle;
        }
    }

    public class PanelArcGroup
    {
        private List<Arc> outerArcs = new List<Arc>();
        private List<Arc> innerArcs = new List<Arc>();//calculated from outer loops                
        public readonly float startAngle;
        public readonly float endAngle;
        public readonly float thickness;
        public readonly int faces;
        public readonly bool shouldGenerateSidewalls;

        public PanelArcGroup(float start, float end, float thickness, int faces, bool colliders)
        {
            startAngle = start;
            endAngle = end;
            this.thickness = thickness;
            this.faces = faces;
            shouldGenerateSidewalls = (start % 360f != end % 360f);
        }

        public void addArc(float radius, float height)
        {
            Arc outer = new Arc(radius, height, startAngle, endAngle, faces);
            Arc inner = new Arc(radius - thickness, height, startAngle, endAngle, faces);
            outerArcs.Add(outer);
            innerArcs.Add(inner);            
        }

        public GameObject[] generateColliders(Vector3 center, int facesPerCollider)
        {            
            float totalAngle = endAngle - startAngle;
            float anglePerFace = 360f / (float)faces;
            int localFaces = (int)Math.Round(totalAngle / anglePerFace);
            localFaces /= facesPerCollider;
            GameObject[] colliders = new GameObject[localFaces];
            float localStart, localEnd, startY, height, topRadius, bottomRadius, thickness;
            Mesh colliderMesh;
            MeshFilter mf;
            //MeshRenderer mr;
            MeshCollider mc;
            thickness = outerArcs[0].radius - innerArcs[0].radius;
            MeshBuilder builder = new MeshBuilder();
            for (int i = 0; i < localFaces; i++)
            {
                localStart = startAngle + (float)i * anglePerFace;
                localEnd = localStart + (anglePerFace * facesPerCollider);
                for (int k = 0; k < innerArcs.Count - 1; k++)
                {
                    startY = innerArcs[k].height;
                    height = innerArcs[k+1].height - startY;
                    bottomRadius = outerArcs[k].radius;
                    topRadius = outerArcs[k+1].radius;
                    builder.generatePanelCollider(center, localStart, localEnd, startY, height, bottomRadius, topRadius, thickness);
                    colliderMesh = builder.buildMesh();
                    builder.clear();
                    colliders[i] = new GameObject("PanelCollider"+i+"-"+k);
                    mf = colliders[i].AddComponent<MeshFilter>();
                    //mr = colliders[i].AddComponent<MeshRenderer>();
                    mc = colliders[i].AddComponent<MeshCollider>();
                    mf.mesh = colliderMesh;
                    //mr.enabled = true;
                    mc.sharedMesh = colliderMesh;
                    mc.enabled = mc.convex = true;
                }
            }
            return colliders;
        }

        public Mesh generatePanels(Vector3 pos, UVArea outer, UVArea inner, UVArea caps)
        {
            int len = outerArcs.Count;
            MeshBuilder builder = new MeshBuilder();

            for (int i = 0; i < len - 1; i++)
            {
                generatePanelSegment(builder, pos, outerArcs[i], outerArcs[i + 1], outer, false, true, false);//outside panels
                generatePanelSegment(builder, pos, innerArcs[i], innerArcs[i + 1], inner, true, false, true);//inside panels
            }
            generatePanelSegment(builder, pos, outerArcs[0], innerArcs[0], caps, true, false, false);//bottom cap
            generatePanelSegment(builder, pos, outerArcs[outerArcs.Count - 1], innerArcs[innerArcs.Count - 1], caps, false, false, false);//top cap

            if (shouldGenerateSidewalls)
            {
                generateSidewalls(builder, caps);
            }

            return builder.buildMesh();
        }
                
        private void generatePanelSegment(MeshBuilder builder, Vector3 pos, Arc arcA, Arc arcB, UVArea area, bool invertFaces, bool invertNormalY, bool invertNormalXZ)
        {
            float heightDiff = arcB.height - arcA.height;
            float offset = arcA.radius - arcB.radius;
            float sideRadians = Mathf.Atan2(heightDiff, offset) - 90 * Mathf.Deg2Rad;
            float yCos = Mathf.Cos(sideRadians);
            float ySin = Mathf.Sin(sideRadians);
            if (invertNormalY) { ySin *= -1; }
            if (invertNormalXZ) { yCos *= -1; }
            List<Vertex> verts1 = new List<Vertex>();
            List<Vertex> verts2 = new List<Vertex>();
            verts1.AddRange(arcA.generateVertices(builder, pos, area, area.v1, yCos, ySin));
            verts2.AddRange(arcB.generateVertices(builder, pos, area, area.v2, yCos, ySin));
            builder.generateQuads(verts1, verts2, invertFaces);
        }

        private void generateSidewalls(MeshBuilder builder, UVArea caps)
        {
            int len = outerArcs.Count;
            float[] distances = new float[len];
            float[] us = new float[len];
            float totalLength = 0;
            float length;
            Vector3 a, b;
            for (int i = 0; i < len - 1; i++)
            {
                a = outerArcs[i].startVector;
                b = outerArcs[i + 1].startVector;
                length = Vector3.Distance(a, b);
                totalLength += length;
                distances[i + 1] = length;
            }

            length = 0;
            float percent;
            float uDelta = caps.u2 - caps.u1;
            for (int i = 0; i < len; i++)
            {
                length += distances[i];
                percent = length / totalLength;
                us[i] = caps.u1 + percent * uDelta;
            }

            Vector3 leftNorm = new Vector3();
            leftNorm.x = outerArcs[0].startVector.z;
            leftNorm.z = -outerArcs[0].startVector.x;
            leftNorm.Normalize();            
            Vector3 rightNorm = new Vector3();
            rightNorm.x = outerArcs[0].endVector.z;
            rightNorm.z = -outerArcs[0].endVector.x;
            rightNorm.Normalize();
            rightNorm = -rightNorm;//as faces get inverted for this side, so norms get inverted as well

            //left
            List<Vertex> outerStartVerts = new List<Vertex>();
            List<Vertex> innerStartVerts = new List<Vertex>();

            //right
            List<Vertex> outerEndVerts = new List<Vertex>();
            List<Vertex> innerEndVerts = new List<Vertex>();

            len = outerArcs.Count;
            Vector3 outer, inner;
            Vector2 uv;
            for (int i = 0; i < len; i++)
            {
                outer = outerArcs[i].startVector;
                uv = new Vector2(us[i], caps.v1);
                outerStartVerts.Add(builder.addVertex(outer, leftNorm, uv));
                inner = innerArcs[i].startVector;
                uv = new Vector2(us[i], caps.v2);
                innerStartVerts.Add(builder.addVertex(inner, leftNorm, uv));

                outer = outerArcs[i].endVector;
                uv = new Vector2(us[i], caps.v1);
                outerEndVerts.Add(builder.addVertex(outer, rightNorm, uv));
                inner = innerArcs[i].endVector;
                uv = new Vector2(us[i], caps.v2);
                innerEndVerts.Add(builder.addVertex(inner, rightNorm, uv));
            }
            builder.generateQuads(outerStartVerts, innerStartVerts, false);
            builder.generateQuads(outerEndVerts, innerEndVerts, true);
        }

        public Vector3 getPivotVector()
        {
            return outerArcs[0].getPivotPoint();
        }

        public float getPivotRotation()
        {
            return outerArcs[0].getPivotYRotation();
        }

        public float getBottomRadius()
        {
            return outerArcs[0].radius;
        }

    }
    
}
