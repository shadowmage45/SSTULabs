using UnityEngine;
using System;
namespace SSTUTools
{
    public class ProceduralCylinderModel : ProceduralModel
    {
        public float radius = 0.625f;

        public float height = 0.1f;

        public float thickness = 0.1f;        

        public int cylinderSides = 24;

        public UVArea outsideUV;
        public UVArea insideUV;
        public UVArea topUV;
        public UVArea bottomUV;

        public void setModelParameters(float radius, float height, float thickness, int cylinderSides)
        {
            this.radius = radius;
            this.height = height;
            this.thickness = thickness;
            this.cylinderSides = cylinderSides;
        }

        protected override void generateModel(GameObject root)
        {
            CylinderMeshGenerator gen2 = new CylinderMeshGenerator(new Vector3(0, -height / 2f, 0), cylinderSides, height, radius, radius, radius - thickness, radius - thickness);
            gen2.outsideUV = outsideUV;
            gen2.insideUV = insideUV;
            gen2.topUV = topUV;
            gen2.bottomUV = bottomUV;
            Mesh mesh = gen2.generateMesh();
            MeshFilter mf = root.GetComponent<MeshFilter>();
            if (mf == null) { mf = root.AddComponent<MeshFilter>(); }
            MeshRenderer mr = root.GetComponent<MeshRenderer>();
            if (mr == null) { mr = root.AddComponent<MeshRenderer>(); }
            mf.mesh = mesh;
            MeshCollider mc = root.GetComponent<MeshCollider>();
            if (mc != null) { Component.DestroyImmediate(mc); }
            mc = root.AddComponent<MeshCollider>();//re-init mesh collider
            mc.sharedMesh = mesh;
        }
    }
}

