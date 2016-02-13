using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUAirstreamShield : PartModule, IAirstreamShield
    { 

        [KSPField]
        public String meshName;

        [KSPField]
        public bool useAttachNodeTop = false;

        [KSPField]
        public bool useAttachNodeBottom = false;

        [KSPField]
        public bool shieldSelf = false;

        [KSPField]
        public float topY;

        [KSPField]
        public float topRadius;

        [KSPField]
        public float bottomY;

        [KSPField]
        public float bottomRadius;

        [KSPField]
        public int shieldID = 0;

        [KSPField(guiName ="Shielded Parts", guiActive =true, guiActiveEditor =true)]
        public int partsShielded = 0;

        [KSPField(isPersistant = true)]
        public bool shieldEnabled = true;
        
        private List<Part> shieldedParts = new List<Part>();

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(onVesselModified));
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }
        
        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            GameEvents.onVesselWasModified.Remove(new EventData<Vessel>.OnEvent(onVesselModified));
        }

        public void Start()
        {
            updateShieldStatus();
        }        

        public void onEditorVesselModified(ShipConstruct ship)
        {
            updateShieldStatus();
        }

        public void onVesselModified(Vessel vessel)
        {
            updateShieldStatus();
        }

        public bool ClosedAndLocked()
        {
            return shieldEnabled;
        }

        public Part GetPart()
        {
            return part;
        }

        public Vessel GetVessel()
        {
            return vessel;
        }

        private void updateShieldStatus()
        {
            clearShieldedParts();
            if (shieldEnabled)
            {
                if (useAttachNodeTop)
                {
                    AttachNode node = part.findAttachNode("top");
                    if (node == null || node.attachedPart == null) { return; }
                }
                if (useAttachNodeBottom)
                {
                    AttachNode node = part.findAttachNode("bottom");
                    if (node == null || node.attachedPart == null) { return; }
                }
                findShieldedParts();
            }
            print("updated shielding status, new shielded part count: " + partsShielded);
        }

        private void clearShieldedParts()
        {
            partsShielded = 0;
            if (shieldedParts.Count > 0)
            {
                foreach (Part part in shieldedParts)
                {
                    part.RemoveShield(this);
                }
                shieldedParts.Clear();
            }
        }

        private void findShieldedParts()
        {
            clearShieldedParts();
            float height = topY - bottomY;
            if (!String.IsNullOrEmpty(meshName))
            {
                findShieldedPartsMesh();
            }
            else
            {
                findShieldedPartsCylinder();
            }
        }

        private void findShieldedPartsMesh()
        {

        }

        private void findShieldedPartsCylinder()
        {
            float height = topY - bottomY;
            findShieldedPartsCylinder(part, shieldedParts, topY, bottomY, topRadius, bottomRadius);
            if (shieldEnabled && shieldSelf)
            {
                shieldedParts.Add(part);
            }
            for (int i = 0; i < shieldedParts.Count; i++)
            {
                shieldedParts[i].AddShield(this);
            }
            partsShielded = shieldedParts.Count;
        }

        //TODO
        public static void findShieldedPartsMesh(Part basePart, String rootMeshName, List<Part> shieldedParts)
        {
            Bounds combinedBounds = SSTUUtils.getRendererBoundsRecursive(basePart.transform.FindRecursive(rootMeshName).gameObject);
        }

        //TODO clean this up to be easier to read/understand now that it is optimized for cylinder check only
        public static void findShieldedPartsCylinder(Part basePart, List<Part> shieldedParts, float topY, float bottomY, float topRadius, float bottomRadius)
        {
            float height = topY - bottomY;
            float largestRadius = topRadius > bottomRadius ? topRadius : bottomRadius;

            Vector3 lookupCenterLocal = new Vector3(0, bottomY + (height * 0.5f), 0);
            Vector3 lookupTopLocal = new Vector3(0, topY, 0);
            Vector3 lookupBottomLocal = new Vector3(0, bottomY, 0);
            Vector3 lookupCenterGlobal = basePart.transform.TransformPoint(lookupCenterLocal);

            Ray lookupRay = new Ray(lookupBottomLocal, new Vector3(0, 1, 0));

            List<Part> partsFound = new List<Part>();
            //do a basic sphere check vs the maximal size of the cylinder
            Collider[] foundColliders = Physics.OverlapSphere(lookupCenterGlobal, height * 1.5f, 1);
            foreach (Collider col in foundColliders)
            {
                Part pt = col.gameObject.GetComponentUpwards<Part>();
                if (pt != null && pt != basePart && pt.vessel == basePart.vessel && !partsFound.Contains(pt))
                {
                    partsFound.Add(pt);
                }
            }
            
            Vector3 otherPartCenterLocal;

            float partYPos;
            float partYPercent;
            float partYRadius;
            float radiusOffset = topRadius - bottomRadius;

            foreach (Part pt in partsFound)
            {
                Vector3 otherPartCenter = pt.partTransform.TransformPoint(PartGeometryUtil.FindBoundsCentroid(pt.GetRendererBounds(), pt.transform));               
                //check part bounds center point against conic projection of the fairing
                otherPartCenterLocal = basePart.transform.InverseTransformPoint(otherPartCenter);

                //check vs top and bottom of the shielded area                
                if (otherPartCenterLocal.y > lookupTopLocal.y || otherPartCenterLocal.y < lookupBottomLocal.y)
                {
                    continue;
                }

                //quick check vs cylinder radius
                float distFromLine = SSTUUtils.distanceFromLine(lookupRay, otherPartCenterLocal);
                if (distFromLine > largestRadius)
                {
                    continue;
                }

                //more precise check vs radius of the cone at that Y position
                partYPos = otherPartCenterLocal.y - lookupBottomLocal.y;
                partYPercent = partYPos / height;
                partYRadius = partYPercent * radiusOffset;
                if (distFromLine > (partYRadius + bottomRadius))
                {
                    continue;
                }
                shieldedParts.Add(pt);
                //print("Shielding part: " + pt);
            }
        }
    }

}
