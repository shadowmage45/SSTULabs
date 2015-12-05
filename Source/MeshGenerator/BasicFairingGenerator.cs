using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    //creates a basic panels-only fairing with the given parameters
    public class BasicFairingGenerator
    {

        public String baseName = "FairingBase";
        public String panelName = "FairingPanel";
        #region publicReadOnlyVars		
        protected float startHeight;
        protected float boltPanelHeight;
        protected float totalPanelHeight;
        protected float maxPanelSectionHeight;
        protected int numOfPanels;
        protected int cylinderSides;
        protected float topRadius;
        protected float bottomRadius;
        protected float wallThickness;
        public bool topBoltPanel = true;
        public bool bottomBoltPanel = true;
        #endregion

        #region publicUVvars
        //UV map areas
        //made public so that they could potentially be overriden by KSPModule code and/or set from a config file
        public UVArea innerCap = new UVArea(0, 5, 1023, 24, 1024);
        public UVArea innerPanel = new UVArea(0, 30, 1023, 335, 1024);
        public UVArea outerCap = new UVArea(0, 346, 1023, 364, 1024);
        public UVArea outerPanel = new UVArea(0, 371, 1023, 675, 1024);
        #endregion

        #region privateWorkingVars
        //private internal working vars
        protected int sidesPerPanel;
        protected float anglePerPanel;
        protected float anglePerSide;
        protected float startAngle;
        protected float topOuterRadius;
        protected float topInnerRadius;
        protected float bottomOuterRadius;
        protected float bottomInnerRadius;
        protected float bottomOuterCirc;
        protected float topOuterCirc;
        protected float centerX;
        protected float centerZ;
        #endregion

        public BasicFairingGenerator(float startHeight, float boltPanelHeight, float totalPanelHeight, float maxPanelSectionHeight, float bottomRadius,
            float topRadius, float wallThickness, int numOfPanels, int cylinderSides)
        {
            this.startHeight = startHeight;
            this.boltPanelHeight = boltPanelHeight;
            this.totalPanelHeight = totalPanelHeight;
            this.maxPanelSectionHeight = maxPanelSectionHeight;
            this.bottomRadius = bottomRadius;
            this.topRadius = topRadius;
            this.wallThickness = wallThickness;
            this.numOfPanels = numOfPanels;
            this.cylinderSides = cylinderSides;

            sidesPerPanel = cylinderSides / numOfPanels;
            anglePerPanel = 360.0f / (float)numOfPanels;
            anglePerSide = anglePerPanel / (float)sidesPerPanel;
            startAngle = 90.0f - (anglePerPanel / 2.0f);
            topOuterRadius = topRadius;
            topInnerRadius = topRadius - wallThickness;
            bottomOuterRadius = bottomRadius;
            bottomInnerRadius = bottomRadius - wallThickness;
            bottomOuterCirc = Mathf.PI * bottomOuterRadius * 2f;
            topOuterCirc = Mathf.PI * topOuterRadius * 2f;
            centerX = 0;
            centerZ = -bottomRadius;            
        }

        #region privateGeneration methods

        protected void generateFairingPanel(MeshGenerator gen)
        {
            //generate outer wall
            generateFairingWallSection(gen, outerCap, outerPanel, 0, totalPanelHeight, maxPanelSectionHeight, boltPanelHeight, topOuterRadius, bottomOuterRadius, sidesPerPanel, anglePerSide, startAngle, true);

            //generate inner wall
            generateFairingWallSection(gen, innerCap, innerPanel, 0, totalPanelHeight, maxPanelSectionHeight, boltPanelHeight, topInnerRadius, bottomInnerRadius, sidesPerPanel, anglePerSide, startAngle, false);


            //setup proper scaled UVs to maintain 1:1 aspect ratio for a straight cylinder panel
            UVArea innerCapUV = new UVArea(this.innerCap);
            float vHeight = innerCapUV.v2 - innerCapUV.v1;
            float uScale = vHeight / wallThickness;
            innerCapUV.u2 = (bottomOuterCirc / (float)numOfPanels) * uScale;

            gen.setUVArea(innerCapUV);
            //generate bottom cap
            gen.generateCylinderPartialCap(0, -bottomOuterRadius, 0, bottomOuterRadius, bottomInnerRadius, sidesPerPanel, anglePerSide, startAngle, false);


            innerCapUV.u2 = (topOuterCirc / (float)numOfPanels) * uScale;
            gen.setUVArea(innerCapUV);
            //generate top cap
            gen.generateCylinderPartialCap(0, -bottomOuterRadius, 0 + totalPanelHeight, topOuterRadius, topInnerRadius, sidesPerPanel, anglePerSide, startAngle, true);


            // uScale is already determined by wall thickness
            // thus only uv-U needs scaled based on actual panel aspect ratio
            Vector2 topMeasure = new Vector2(topOuterRadius - bottomOuterRadius, totalPanelHeight);
            float sideHeight = topMeasure.magnitude;
            innerCapUV.u2 = sideHeight * uScale;
            gen.setUVArea(innerCapUV);
            //generate left? panel side
            gen.generateCylinderPanelSidewall(0, -bottomOuterRadius, 0, totalPanelHeight, topOuterRadius, topInnerRadius, bottomOuterRadius, bottomInnerRadius, startAngle, true);
            //generate right? panel side
            gen.generateCylinderPanelSidewall(0, -bottomOuterRadius, 0, totalPanelHeight, topOuterRadius, topInnerRadius, bottomOuterRadius, bottomInnerRadius, startAngle + anglePerPanel, false);
        }

        protected void generateFairingWallSection(MeshGenerator gen, UVArea capUV, UVArea panelUV, float startY, float totalHeight, float maxHeightPerPanel, float boltPanelHeight, float topRadius, float bottomRadius, int sides, float anglePerSide, float startAngle, bool outsideWall)
        {
            UVArea capUVCopy = new UVArea(capUV);
            UVArea panelUVCopy = new UVArea(panelUV);
            Vector2 start = new Vector2(bottomRadius, startY);		
            Vector2 topOffset = new Vector2(topRadius - bottomRadius, totalHeight);

            float totalPanelHeight = topOffset.magnitude;
            float capPercent = boltPanelHeight / totalPanelHeight;
            float fullPanelPercent = maxHeightPerPanel / totalPanelHeight;
            float mainPanelHeight = totalPanelHeight;
            if (topBoltPanel) { mainPanelHeight -= boltPanelHeight; }
            if (bottomBoltPanel) { mainPanelHeight -= boltPanelHeight; }

            

            float height;
            Vector2 pBottom;
            Vector2 pTop;

            if (boltPanelHeight > 0)
            {
                //generate caps
                float capVHeight = capUV.v2 - capUV.v1;
                float capVScale = capVHeight / boltPanelHeight;
                float capU = (bottomOuterCirc / (float)this.numOfPanels) * capVScale;
                capUVCopy.u2 = capU;
                
                if (bottomBoltPanel)
                {
                    //bottom cap
                    pBottom = start;
                    pTop = start + (topOffset * capPercent);
                    height = pTop.y - pBottom.y;
                    gen.setUVArea(capUVCopy);
                    gen.generateCylinderWallSection(centerX, centerZ, pBottom.y, height, pTop.x, pBottom.x, sides, anglePerSide, startAngle, outsideWall);

                }

                if (topBoltPanel)
                {
                    capU = (topOuterCirc / (float)this.numOfPanels) * capVScale;
                    capUVCopy.u2 = capU;
                    //bottom cap
                    pBottom = start + topOffset - (topOffset * capPercent);
                    pTop = start + topOffset;
                    height = pTop.y - pBottom.y;
                    gen.setUVArea(capUVCopy);
                    gen.generateCylinderWallSection(centerX, centerZ, pBottom.y, height, pTop.x, pBottom.x, sides, anglePerSide, startAngle, outsideWall);
                }
            }


            //generate panels
            //setup UV scaling for full height panels
            float panelVHeight = panelUV.v2 - panelUV.v1;
            float panelVScale = panelVHeight / maxHeightPerPanel;
            float panelU = (bottomOuterCirc / (float)this.numOfPanels) * panelVScale;
            panelUVCopy.u2 = panelU;//scale the u2 coordinate (right-hand extent) by the scale factor, to setup a 1:1 texture ratio across the panel

            gen.setUVArea(panelUVCopy);
            // modulus operation to get the whole and remainder
            float extraPanelHeight = mainPanelHeight / maxHeightPerPanel;
            int numOfVerticalSections = (int)extraPanelHeight;
            extraPanelHeight -= numOfVerticalSections;

            pBottom = start;
            if (bottomBoltPanel)
            {
                pBottom += (topOffset * capPercent);
            }
            for (int i = 0; i < numOfVerticalSections; i++)//generate full panels
            {
                pTop = pBottom + (fullPanelPercent * topOffset);
                height = pTop.y - pBottom.y;
                gen.generateCylinderWallSection(centerX, centerZ, pBottom.y, height, pTop.x, pBottom.x, sides, anglePerSide, startAngle, outsideWall);
                //increment for next full panel
                pBottom = pTop;
            }
            if (extraPanelHeight > 0)//gen extra partial panel if needed
            {
                //gen 'extra' panel			
                //setup UV scaling for partial height panel, both u and V;
                panelVHeight = panelUV.v2 - panelUV.v1;
                panelVScale = panelVHeight / maxHeightPerPanel;
                panelU = (bottomOuterCirc / (float)this.numOfPanels) * panelVScale;
                panelUVCopy.u2 = panelU;//scale u area
                float panelV = panelUVCopy.v1 + panelVScale * extraPanelHeight;
                panelUVCopy.v2 = panelV;//scale v area

                float extraPanelPercent = extraPanelHeight / totalPanelHeight;
                pTop = pBottom + (extraPanelPercent * topOffset);
                height = pTop.y - pBottom.y;
                gen.setUVArea(panelUVCopy);
                gen.generateCylinderWallSection(centerX, centerZ, pBottom.y, height, pTop.x, pBottom.x, sides, anglePerSide, startAngle, outsideWall);
            }
        }

        protected void generateCylinderCollider(MeshGenerator gen, float offsetX, float offsetZ, float startY, float colliderHeight, float topRadius, float bottomRadius, int sides)
        {
            float anglePerSide = 360.0f / (float)sides;
            gen.setUVArea(0, 0, 1, 1);
            gen.generateCylinderWallSection(offsetX, offsetZ, startY, colliderHeight, topRadius, bottomRadius, sides, anglePerSide, 0, true);
            gen.generateTriangleFan(offsetX, offsetZ, startY, bottomRadius, sides, anglePerSide, 0, false);
            gen.generateTriangleFan(offsetX, offsetZ, startY + colliderHeight, topRadius, sides, anglePerSide, 0, true);
        }

        #endregion

    }
}

