using UnityEngine;
using System;
namespace SSTUTools
{
    public class ProceduralCylinderModel : ProceduralModel
    {
        public float radius = 0.625f;

        public float height = 0.1f;

        public float thickness = 0.1f;

        public float capHeight = 0f;

        public float maxPanelHeight = 1f;

        public int cylinderSides = 24;

        public void setModelParameters(float radius, float height, float thickness, float capHeight, float maxPanelHeight, int cylinderSides)
        {
            this.radius = radius;
            this.height = height;
            this.thickness = thickness;
            this.capHeight = capHeight;
            this.maxPanelHeight = maxPanelHeight;
            this.cylinderSides = cylinderSides;
        }

        protected override void generateModel(GameObject root)
        {
            CylinderMeshGenerator gen = new CylinderMeshGenerator(-height / 2f, capHeight, height, maxPanelHeight, radius, radius, thickness, 1, cylinderSides);
            gen.panelName = "P-CylinderModel";
            gen.buildFairingBasic(root);
        }
    }
}

