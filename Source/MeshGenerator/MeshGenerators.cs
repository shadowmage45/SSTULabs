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

        public ArcMeshGenerator(Vector3 offset, int sides, int panels, float startAngle, float endAngle, float thickness)
        {
            this.offset = offset;
            this.faces = sides;
            this.panels = panels;
            this.startAngle = startAngle;
            this.endAngle = endAngle;
            this.thickness = thickness;
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
                MeshFilter mf = gos[i].GetComponent<MeshFilter>();
                if (mf == null) { mf = gos[i].AddComponent<MeshFilter>(); }
                mf.mesh = panelGroups[i].generatePanels(offset, outsideUV, insideUV, edgesUV);

                MeshRenderer mr = gos[i].GetComponent<MeshRenderer>();
                if (mr == null) { mr = gos[i].AddComponent<MeshRenderer>(); }
                gos[i].transform.parent = parent;
                gos[i].transform.localPosition = Vector3.zero;
                gos[i].transform.rotation = parent.rotation;
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

                //GameObject debugModel = SSTUUtils.cloneModel("SSTU/Assets/DEBUG_MODEL");
                //debugModel.transform.parent = pivotObject.transform;
                //debugModel.transform.rotation = pivotObject.transform.rotation;
                //debugModel.transform.position = pivotObject.transform.position;

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
                panelGroups[i] = new PanelArcGroup(start, end, thickness, faces);
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

        private List<Mesh> colliderMeshes = new List<Mesh>();//TODO
        
        public readonly float startAngle;
        public readonly float endAngle;
        public readonly float thickness;
        public readonly int faces;
        public readonly bool shouldGenerateSidewalls;

        public PanelArcGroup(float start, float end, float thickness, int faces)
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

        //TODO
        public void generateMeshColliders(Vector3 pos, int facesPerCollider)
        {
            throw new NotImplementedException();
        }
                
        private Mesh generatePanelCollider(Vector3 center, float startAngle, float endAngle, float height, float bottomRadius, float topRadius, float thickness)
        {
            MeshBuilder builder = new MeshBuilder();

            float bottomInnerRadius = bottomRadius - thickness;
            float topInnerRadius = topRadius - thickness;
            float startRads = Mathf.Deg2Rad * startAngle;
            float endRads = Mathf.Deg2Rad * endAngle;
            float startXSin = -Mathf.Sin(startRads);
            float startZCos = Mathf.Cos(startRads);
            float endXSin = -Mathf.Sin(endRads);
            float endZCos = Mathf.Cos(endRads);

            Vector3 frontBottomLeft = new Vector3(center.x + bottomRadius * startXSin, center.y, center.z + bottomRadius * startZCos);
            Vector3 frontBottomRight = new Vector3(center.x + bottomRadius * endXSin, center.y, center.z + bottomRadius * endZCos);
            Vector3 frontTopLeft = new Vector3(center.x + topRadius * startXSin, center.y + height, center.z + topRadius * startZCos);
            Vector3 frontTopRight = new Vector3(center.x + topRadius * endXSin, center.y + height, center.z + topRadius * endZCos);

            Vector3 rearBottomLeft = new Vector3(center.x + bottomInnerRadius * startXSin, center.y, center.z + bottomInnerRadius * startZCos);
            Vector3 rearBottomRight = new Vector3(center.x + bottomInnerRadius * endXSin, center.y, center.z + bottomInnerRadius * endZCos);
            Vector3 rearTopLeft = new Vector3(center.x + topInnerRadius * startXSin, center.y + height, center.z + topInnerRadius * startZCos);
            Vector3 rearTopRight = new Vector3(center.x + topInnerRadius * endXSin, center.y + height, center.z + topInnerRadius * endZCos);

            Vector3 normFront = Vector3.forward;
            Vector3 normRear = Vector3.back;
            Vector3 normLeft = Vector3.left;
            Vector3 normRight = Vector3.right;
            Vector3 normUp = Vector3.up;
            Vector3 normDown = Vector3.down;

            Vector2 uv1 = new Vector2(0, 0);
            Vector2 uv2 = new Vector2(1, 0);
            Vector2 uv3 = new Vector2(0, 1);
            Vector2 uv4 = new Vector2(1, 1);


            List<Vertex> v1 = new List<Vertex>();
            List<Vertex> v2 = new List<Vertex>();

            //generate front face
            v1.Add(builder.addVertex(frontBottomLeft, normFront, uv1));
            v1.Add(builder.addVertex(frontBottomRight, normFront, uv2));
            v2.Add(builder.addVertex(frontTopLeft, normFront, uv3));
            v2.Add(builder.addVertex(frontTopRight, normFront, uv4));
            builder.generateQuads(v1, v2, false);
            v1.Clear();
            v2.Clear();

            //generate rear face
            v1.Add(builder.addVertex(rearBottomLeft, normRear, uv2));
            v1.Add(builder.addVertex(rearBottomRight, normRear, uv1));
            v2.Add(builder.addVertex(rearTopLeft, normRear, uv4));
            v2.Add(builder.addVertex(rearTopRight, normRear, uv3));
            builder.generateQuads(v1, v2, true);
            v1.Clear();
            v2.Clear();

            //generate left face
            v1.Add(builder.addVertex(frontBottomLeft, normLeft, uv2));
            v1.Add(builder.addVertex(rearBottomLeft, normLeft, uv1));
            v2.Add(builder.addVertex(frontTopLeft, normLeft, uv4));
            v2.Add(builder.addVertex(rearTopLeft, normLeft, uv3));
            builder.generateQuads(v1, v2, true);
            v1.Clear();
            v2.Clear();

            //generate right face
            v1.Add(builder.addVertex(frontBottomRight, normRight, uv1));
            v1.Add(builder.addVertex(rearBottomRight, normRight, uv2));
            v2.Add(builder.addVertex(frontTopRight, normRight, uv3));
            v2.Add(builder.addVertex(rearTopRight, normRight, uv4));
            builder.generateQuads(v1, v2, false);
            v1.Clear();
            v2.Clear();

            //generate top face
            v1.Add(builder.addVertex(frontTopRight, normUp, uv2));
            v1.Add(builder.addVertex(frontTopLeft, normUp, uv1));
            v2.Add(builder.addVertex(rearTopRight, normUp, uv4));
            v2.Add(builder.addVertex(rearTopLeft, normUp, uv3));
            builder.generateQuads(v1, v2, true);
            v1.Clear();
            v2.Clear();

            //generate bottom face
            v1.Add(builder.addVertex(frontBottomRight, normUp, uv1));
            v1.Add(builder.addVertex(frontBottomLeft, normUp, uv2));
            v2.Add(builder.addVertex(rearBottomRight, normUp, uv3));
            v2.Add(builder.addVertex(rearBottomLeft, normUp, uv4));
            builder.generateQuads(v1, v2, false);
            v1.Clear();
            v2.Clear();

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
