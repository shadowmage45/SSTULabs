using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUAirstreamShield : PartModule, IAirstreamShield
    {
        
        [KSPField]
        public bool useAttachNodeTop = false;

        [KSPField]
        public bool useAttachNodeBottom = false;

        [KSPField]
        public string animationID = string.Empty;
        
        [KSPField]
        public float topY;

        [KSPField]
        public float topRadius;

        [KSPField]
        public float bottomY;

        [KSPField]
        public float bottomRadius;
        
        [KSPField(guiName = "Shielded Parts", guiActive = true, guiActiveEditor = true)]
        public int partsShielded = 0;
        
        private List<Part> shieldedParts = new List<Part>();

        private List<AirstreamShieldArea> shieldedAreas = new List<AirstreamShieldArea>();
        
        private AirstreamShieldArea baseArea;

        private bool needsUpdate = true;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (topRadius != 0 && bottomRadius != 0)//add a shield based on base config params
            {
                baseArea = new AirstreamShieldArea("baseArea", topRadius, bottomRadius, topY, bottomY, useAttachNodeTop, useAttachNodeBottom);
                shieldedAreas.AddUnique(baseArea);
            }
            //TODO check config node for additional shield defs; persist these regardless of external modules
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
            needsUpdate = true;
        }

        public void LateUpdate()
        {
            if ((HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight) && needsUpdate)
            {
                needsUpdate = false;
                updateShieldStatus();
            }
        }

        public void onEditorVesselModified(ShipConstruct ship)
        {
            needsUpdate = true;
        }

        public void onVesselModified(Vessel vessel)
        {
            needsUpdate = true;
        }

        public bool ClosedAndLocked()
        {
            return shieldedAreas.Count>0;
        }

        public Part GetPart()
        {
            return part;
        }

        public Vessel GetVessel()
        {
            return vessel;
        }

        public void addShieldArea(String name, float topRad, float bottomRad, float topY, float bottomY, bool topNode, bool bottomNode)
        {
            AirstreamShieldArea area = shieldedAreas.Find(m => m.name == name);
            if (area != null) { shieldedAreas.Remove(area); }
            area = new AirstreamShieldArea(name, topRad, bottomRad, topY, bottomY, topNode, bottomNode);
            shieldedAreas.Add(area);
            needsUpdate = true;
        }

        public void removeShieldArea(String name)
        {
            AirstreamShieldArea area = shieldedAreas.Find(m => m.name == name);
            if (area != null) { shieldedAreas.Remove(area); }
            needsUpdate = true;
        }

        public void updateShieldStatus()
        {
            clearShieldedParts();
            findShieldedParts();
        }

        private void clearShieldedParts()
        {
            partsShielded = 0;
            if (shieldedParts.Count > 0)
            {
                int len = shieldedParts.Count;
                for (int i = 0; i < len; i++)
                {
                    if (shieldedParts[i] == null) { continue; }
                    shieldedParts[i].RemoveShield(this);
                }
                shieldedParts.Clear();
            }
        }

        private void findShieldedParts()
        {
            clearShieldedParts();
            if (string.IsNullOrEmpty(animationID))
            {
                findShieldedPartsCylinder();
            }
            else
            {
                IScalarModule[] ism = part.GetComponents<IScalarModule>();
                int len = ism.Length;
                for (int i = 0; i < len; i++)
                {
                    if (ism[i].ScalarModuleID == animationID)
                    {
                        if (ism[i].GetScalar <= 0)//stopped and closed
                        {
                            findShieldedPartsCylinder();
                        }
                        break;//only care about the first matching animation module
                    }
                }
            }
        }

        private void findShieldedPartsCylinder()
        {
            int len = shieldedAreas.Count;
            AirstreamShieldArea area;
            for (int i = 0; i < len; i++)
            {
                area = shieldedAreas[i];
                if (area.useTopNode)
                {
                    AttachNode node = part.FindAttachNode("top");
                    if (node != null && node.attachedPart == null) { continue; }
                }
                if (area.useBottomNode)
                {
                    AttachNode node = part.FindAttachNode("bottom");
                    if (node != null && node.attachedPart == null) { continue; }
                }
                shieldedAreas[i].updateShieldStatus(part, shieldedParts);
            }
            len = shieldedParts.Count;
            for (int i = 0; i < len; i++)
            {
                shieldedParts[i].AddShield(this);
            }
            partsShielded = shieldedParts.Count;
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

    public class AirstreamShieldArea
    {
        public readonly string name;
        public readonly float topY;
        public readonly float topRadius;
        public readonly float bottomY;
        public readonly float bottomRadius;
        public readonly bool useTopNode;
        public readonly bool useBottomNode;

        public AirstreamShieldArea(string name, float topRad, float bottomRad, float topY, float bottomY, bool topNode, bool bottomNode)
        {
            this.name = name;
            this.topY = topY;
            this.bottomY = bottomY;
            this.topRadius = topRad;
            this.bottomRadius = bottomRad;
            this.useTopNode = topNode;
            this.useBottomNode = bottomNode;
        }

        public void updateShieldStatus(Part p, List<Part> shieldedParts)
        {
            SSTUAirstreamShield.findShieldedPartsCylinder(p, shieldedParts, topY, bottomY, topRadius, bottomRadius);
        }

    }

}
