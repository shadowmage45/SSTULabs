using UnityEngine;
using System;
namespace SSTUTools
{
    public class FairingData
    {
        //gameObject storage class
        public FairingBase theFairing;
        public String fairingName = "Fairing";
        public Vector3 rotationOffset = Vector3.zero;//default rotation offset is zero; must specify if custom rotation offset is to be used, not normally needed
        public float topY = 1;
        public float bottomY = -1;
        public float capSize = 0.1f;
        public float wallThickness = 0.025f;
        public float maxPanelHeight = 1f;
        public int cylinderSides = 24;//default is for 24 sided cylinders; must specify values for other cylinder sizes
        public int numOfSections = 1;//default is for a single segment fairing panel; must specify values for multi-part fairings
        public float topRadius = 0.625f;//default radius adjustment, only need to specify if other value is desired
        public float bottomRadius = 0.625f;//default radius adjustment, only need to specify if other value is desired
        public bool canAdjustTop = false;//must explicitly specify that radius can be adjusted
        public bool canAdjustBottom = false;//must explicitly specify that radius can be adjusted
        public bool removeMass = true; //if true, fairing mass is removed from parent part when jettisoned (and on part reload)
        public float fairingJettisonMass = 0.1f;//mass of the fairing to be jettisoned; combined with jettisonForce this determines how energetically they are jettisoned
        public float jettisonForce = 10;//force in N to apply to jettisonDirection to each of the jettisoned panel sections
        public Vector3 jettisonDirection = new Vector3(0, 0, 1);//default jettison direction is negative Y (downwards)

        //to be called on initial prefab part load; populate the instance with the default values from the input node
        public virtual void load(ConfigNode node)
        {
            rotationOffset = node.GetVector3("rotationOffset", Vector3.zero);
            topY = node.GetFloatValue("topY", topY);
            bottomY = node.GetFloatValue("bottomY", bottomY);
            capSize = node.GetFloatValue("capSize", capSize);
            wallThickness = node.GetFloatValue("wallThickness", wallThickness);
            maxPanelHeight = node.GetFloatValue("maxPanelHeight", maxPanelHeight);
            cylinderSides = node.GetIntValue("cylinderSides", cylinderSides);
            numOfSections = node.GetIntValue("numOfSections", numOfSections);
            topRadius = node.GetFloatValue("topRadius", topRadius);
            bottomRadius = node.GetFloatValue("bottomRadius", bottomRadius);
            canAdjustTop = node.GetBoolValue("canAdjustTop", canAdjustTop);
            canAdjustBottom = node.GetBoolValue("canAdjustBottom", canAdjustBottom);
            removeMass = node.GetBoolValue("removeMass", removeMass);
            fairingJettisonMass = node.GetFloatValue("fairingJettisonMass", fairingJettisonMass);
            jettisonForce = node.GetFloatValue("jettisonForce", jettisonForce);
            jettisonDirection = node.GetVector3("jettisonDirection", jettisonDirection);
            fairingName = node.GetStringValue("name", fairingName);
        }

        public void createFairing(Part part, Material material)
        {
            float height = topY - bottomY;
            CylinderMeshGenerator fg = new CylinderMeshGenerator(-height * 0.5f, capSize, height, maxPanelHeight, bottomRadius, topRadius, wallThickness, numOfSections, cylinderSides);
            FairingBase fb = fg.buildFairing();
            fb.root.transform.NestToParent(part.transform);
            fb.root.transform.position = part.transform.position;
            fb.root.transform.localPosition = new Vector3(0, topY - height * 0.5f, 0);
            fb.root.transform.rotation = part.transform.rotation;
            //fb.root.transform.Rotate (rotationOffset);//TODO check that rotation offset function works with fairings
            fb.setMaterial(material);
            if (HighLogic.LoadedSceneIsEditor)
            {
                fb.setPanelOpacity(0.25f);
            }
            theFairing = fb;
        }

        public void destroyFairing()
        {
            if (theFairing != null)
            {
                GameObject.Destroy(theFairing.root);
                theFairing = null;
            }
        }

        public void recreateFairing(Part part, Material material)
        {
            destroyFairing();
            createFairing(part, material);
        }

        public void jettisonPanels(Part part)
        {
            if (theFairing != null)
            {
                theFairing.jettisonPanels(part, jettisonForce, jettisonDirection, fairingJettisonMass / (float)numOfSections);
            }
        }

        public void enableRenders(bool enable)
        {
            SSTUUtils.enableRenderRecursive(theFairing.root.transform, enable);
        }

        public void enablePanelColliders(bool enable, bool convex)
        {
            theFairing.enablePanelColliders(enable, convex);
        }
    }
}

