using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    /// <summary>
    /// Generic procedural fairing container
    /// </summary>
    public class FairingContainer
    {
        public readonly int faces;

        private int panels;
        private float startAngle;
        private float endAngle;
        private float thickness;

        private Material material;
        private float opacity;
        private float rotation;

        private List<ArcRing> profile = new List<ArcRing>();
        public readonly GameObject rootObject;
        private GameObject[] panelPivots;
        private Quaternion[] defaultPivotLocalRotations;
        public UVArea outsideUV;
        public UVArea insideUV;
        public UVArea edgesUV;
        public bool generateColliders = false;
        public int facesPerCollider = 1;

        public FairingContainer(GameObject root, int cylinderFaces, int numberOfPanels, float thickness)
        {
            this.rootObject = root;
            this.faces = cylinderFaces;
            this.panels = numberOfPanels;
            this.thickness = thickness;
            setNumberOfPanels(panels, false);
        }

        public void setNumberOfPanels(int panels, bool recreate)
        {
            this.panels = panels;
            float anglePerPanel = 360f / (float)panels;
            float halfAngle = anglePerPanel * 0.5f;
            startAngle = halfAngle;
            endAngle = startAngle + 360f;
            if (recreate)
            {
                recreateModels();
            }
        }
        
        public virtual void generateFairing()
        {
            ArcMeshGenerator gen = new ArcMeshGenerator(Vector3.zero, faces, panels, startAngle, endAngle, thickness, generateColliders, facesPerCollider);
            gen.outsideUV = outsideUV;
            gen.insideUV = insideUV;
            gen.edgesUV = edgesUV;
            foreach (ArcRing ring in profile)
            {
                gen.addArc(ring.height, ring.radius);
            }
            Vector3 pivot = new Vector3(0, 0, -profile[0].radius);
            panelPivots = gen.generatePanels(rootObject.transform, pivot);

            int len = panelPivots.Length;
            defaultPivotLocalRotations = new Quaternion[len];
            for (int i = 0; i < len; i++)
            {
                defaultPivotLocalRotations[i] = panelPivots[i].transform.localRotation;
            }
        }

        public void reparentFairing(Transform newParent, bool nest)
        {
            rootObject.transform.parent = newParent;
            if (nest)
            {
                rootObject.transform.localPosition = Vector3.zero;
                rootObject.transform.rotation = newParent.rotation;
            }
        }

        public void recreateModels()
        {
            generateFairing();
            setMaterial(material);
            setOpacity(opacity);
            setPanelRotations(rotation);
        }

        public void setMaterial(Material mat)
        {
            material = mat;
            SSTUUtils.setMaterialRecursive(rootObject.transform, mat);
        }

        public void setOpacity(float value)
        {
            opacity = value;
            if (rootObject != null)
            {
                SSTUUtils.setOpacityRecursive(rootObject.transform, value);
            }
        }

        public void jettisonPanels(Part part, float force, Vector3 jettisonDirection, float perPanelMass)
        {
            GameObject panelGO;
            Rigidbody rb;
            Vector3 globalForceDirection;
            for (int i = 0; i < panelPivots.Length; i++)
            {
                panelGO = panelPivots[i];
                panelGO.transform.parent = null;
                panelGO.AddComponent<physicalObject>();//auto-destroy when more than 1km away
                rb = panelGO.AddComponent<Rigidbody>();
                rb.velocity = part.rb.velocity;
                rb.mass = perPanelMass;
                globalForceDirection = panelGO.transform.TransformDirection(jettisonDirection);
                rb.AddForce(globalForceDirection * force);
                rb.useGravity = false;
            }
        }

        public void setPanelRotations(float angle)
        {
            for (int i = 0; i < panels; i++)
            {
                panelPivots[i].transform.localRotation = defaultPivotLocalRotations[i];
                panelPivots[i].transform.Rotate(new Vector3(1, 0, 0), angle, Space.Self);
            }
            rotation = angle;
        }

        public void clearProfile()
        {
            profile.Clear();
        }

        public void addRing(float height, float radius)
        {
            profile.Add(new ArcRing(height, radius));
        }

        public float getHeight()
        {
            if (profile.Count <= 0) { return 0f; }
            float bottomY = profile[0].height;
            float topY = profile[profile.Count - 1].height;
            return topY - bottomY;
        }

        public float getTopRadius()
        {
            if (profile.Count <= 0) { return 0f; }
            return profile[profile.Count - 1].radius;
        }

        public float getBottomRadius()
        {
            if (profile.Count <= 0) { return 0f; }
            return profile[0].radius;
        }

        public void enableRender(bool val)
        {
            SSTUUtils.enableRenderRecursive(rootObject.transform, val);
        }

        public void enableColliders(bool val)
        {
            SSTUUtils.enableColliderRecursive(rootObject.transform, val);
        }
    }
}
