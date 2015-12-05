using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUInterstageFairing : PartModule, IMultipleDragCube, IAirstreamShield, IPartCostModifier, IPartMassModifier
    {

        #region KSP MODULE fields
        //config fields for various transform and node names

        //reference to the model for this part; the texture and shader are retrieved from this model
        [KSPField]
        public String modelMeshName = "ISABase";

        [KSPField]
        public String topNodeName = "top";

        [KSPField]
        public String bottomNodeName = "bottom";

        [KSPField]
        public String internalNodeName = "internal";

        [KSPField]
        public String diffuseTextureName = "UNKNOWN";

        [KSPField]
        public String normalTextureName = "UNKNOWN";

        //how many sections should the fairing have radially?
        [KSPField]
        public int numOfRadialSections = 4;

        //height of the upper and lower caps; used to calculate node positions
        [KSPField]
        public float capHeight = 0.1f;

        //radius of the part, used to calculate mesh
        [KSPField(isPersistant = true)]
        public float bottomRadius = 1.25f;

        //radius of the top of the part, used to calculate mesh
        [KSPField(isPersistant = true)]
        public float topRadius = 1.25f;

        //stored current height of the panels, used to recreate mesh on part reload, may be set in config to set the default starting height
        [KSPField(isPersistant = true)]
        public float currentHeight = 1.0f;

        //if top radius !=bottom radius, this will create a 'split' panel at this position, for a straight-up-then-tapered/flared fairing
        [KSPField(isPersistant = true)]
        public float currentStraightHeight = 0f;
        
        //how tall is the decoupler base-cap
        [KSPField]
        public float baseHeight = 0.25f;

        [KSPField]
        public float boltPanelHeight = 0.075f;

        [KSPField]
        public float wallThickness = 0.025f;

        [KSPField]
        public float maxPanelSectionHeight = 1.0f;

        [KSPField]
        public int cylinderSides = 24;

        //maximum height
        [KSPField]
        public float maxHeight = 15.0f;

        //minimum height
        [KSPField]
        public float minHeight = 1.0f;

        [KSPField]
        public float minRadius = 0.3125f;

        [KSPField]
        public float maxRadius = 5f;

        //are planels deployed and upper node decoupled?
        //toggled to true as soon as deploy action is activated
        [KSPField(isPersistant = true)]
        public bool deployed = false;

        //is inner node decoupled?
        //toggled to true as soon as inner node is decoupled, only available after deployed=true
        [KSPField(isPersistant = true)]
        public bool decoupled = false;

        //how far should the panels be rotated for the 'deployed' animation
        [KSPField]
        public float deployedRotation = 60f;

        //how many degrees per second should the fairings rotate while deploy animation is playing?
        [KSPField]
        public float animationSpeed = 5f;

        //deployment animation persistence field
        [KSPField(isPersistant = true)]
        public float currentRotation = 0.0f;

        [KSPField(isPersistant = true)]
        public bool animating = false;

        [KSPField(guiActive = true, guiName = "Parts Shielded", guiActiveEditor = true)]
        public int partsShielded = 0;

        [KSPField(guiName = "Fairing Cost", guiActiveEditor = true)]
        public float fairingCost;
        [KSPField(guiName = "Fairing Mass", guiActiveEditor = true)]
        public float fairingMass;

        [KSPField]
        float costPerBaseVolume = 1500f;
        [KSPField]
        float costPerPanelArea = 50f;

        [KSPField]
        float massPerBaseVolume = 0.5f;
        [KSPField]
        float massPerPanelArea = 0.025f;

        [KSPField]
        float topRadiusAdjust = 0.625f;
        [KSPField]
        float bottomRadiusAdjust = 0.625f;
        [KSPField]
        float heightAdjust = 1;

        [KSPField(guiActiveEditor = true, guiName = "Top Rad Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.05f, maxValue = 0.95f)]
        public float topRadiusExtra;

        [KSPField(guiActiveEditor = true, guiName = "Bot Rad Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.05f, maxValue = 0.95f)]
        public float bottomRadiusExtra;

        [KSPField(guiActiveEditor = true, guiName = "Height Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.05f, maxValue = 0.95f)]
        public float heightExtra;

        [KSPField(guiActiveEditor = true, guiName = "Straight Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.05f, maxValue = 0.95f)]
        public float straightExtra;

        #endregion

        #region private working variables

        /// <summary>
        /// Stashed copy of the raw config node data, to hack around KSP not passing in the modules base node data after prefab construction
        /// </summary>
        [Persistent]
        public String configNodeData = String.Empty;

        private float editorTopRadius;
        private float editorBottomRadius;
        private float editorHeight;
        private float editorStraightHeight;
        private float lastTopRadiusExtra;
        private float lastBottomRadiusExtra;
        private float lastHeightExtra;
        private float lastStraightExtra;

        private bool initialized;

        //the current fairing object, contains the base, panels, and temporary editor-only colliders
        private FairingBase fairingBase;

        //material used for procedural fairing, created from the texture references above
        private Material fairingMaterial;

        //list of parts that are shielded from the airstream
        //rebuilt whenever vessel is modified
        private List<Part> shieldedParts = new List<Part>();

        // tech limit values are updated every time the part is initialized in the editor; ignored otherwise
        private float techLimitMaxHeight;
        private float techLimitMaxDiameter;
        private TechLimitDiameterHeight[] techLimits;

        //lerp between the two cubes depending upon deployed state
        //re-render the cubes on fairing rebuild
        private DragCube closedCube;
        private DragCube openCube;                

        #endregion

        #region KSP GUI Actions

        [KSPEvent(name = "increaseHeightEvent", guiName = "Increase Height", guiActiveEditor = true)]
        public void increaseHeightEvent()
        {
            setHeightFromEditor(currentHeight + heightAdjust, true);
        }

        [KSPEvent(name = "decreaseHeightEvent", guiName = "Decrease Height", guiActiveEditor = true)]
        public void decreaseHeightEvent()
        {
            setHeightFromEditor(currentHeight - heightAdjust, true);
        }

        [KSPEvent(guiName = "Increase Straight Height", guiActiveEditor = true)]
        public void increaseStraightHeightEvent()
        {
            setStraightHeightFromEditor(currentStraightHeight + heightAdjust, true);
        }

        [KSPEvent(guiName = "Decrease Straight Height", guiActiveEditor = true)]
        public void decreaseStraightHeightEvent()
        {
            setStraightHeightFromEditor(currentStraightHeight - heightAdjust, true);
        }

        [KSPEvent(name = "increaseTopRadiusEvent", guiName = "Top Radius +", guiActiveEditor = true)]
        public void increaseTopRadiusEvent()
        {
            setTopRadiusFromEditor(topRadius + topRadiusAdjust, true);
        }

        [KSPEvent(name = "decreaseTopRadiusEvent", guiName = "Top Radius -", guiActiveEditor = true)]
        public void decreaseTopRadiusEvent()
        {
            setTopRadiusFromEditor(topRadius - topRadiusAdjust, true);
        }

        [KSPEvent(name = "increaseBottomRadiusEvent", guiName = "Bottom Radius +", guiActiveEditor = true)]
        public void increaseBottomRadiusEvent()
        {
            setBottomRadiusFromEditor(bottomRadius + bottomRadiusAdjust, true);
        }

        [KSPEvent(name = "decreaseBottomRadiusEvent", guiName = "Bottom Radius -", guiActiveEditor = true)]
        public void decreaseBottomRadiusEvent()
        {
            setBottomRadiusFromEditor(bottomRadius - bottomRadiusAdjust, true);
        }

        [KSPEvent(name = "deployEvent", guiName = "Deploy Panels", guiActive = true)]
        public void deployEvent()
        {
            onDeployEvent();
        }

        [KSPEvent(name = "decoupleEvent", guiName = "Decouple Inner Node", guiActive = true)]
        public void decoupleEvent()
        {
            onDecoupleEvent();
        }

        [KSPAction("Deploy and release")]
        public void deployAction(KSPActionParam param)
        {
            onDeployEvent();
        }

        [KSPAction("Decouple inner node")]
        public void decoupleAction(KSPActionParam param)
        {
            onDecoupleEvent();
        }

        #endregion

        #region KSP overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
            {
                configNodeData = node.ToString();
            }
            initialize();
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);  
                                  
            initialize();

            if (HighLogic.LoadedSceneIsFlight)
            {
                enableEditorColliders(false);
            }

            //register for game events, used to notify when to update shielded parts
            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(onVesselModified));
            GameEvents.onVesselGoOffRails.Add(new EventData<Vessel>.OnEvent(onVesselUnpack));
            GameEvents.onVesselGoOnRails.Add(new EventData<Vessel>.OnEvent(onVesselPack));
            GameEvents.onPartDie.Add(new EventData<Part>.OnEvent(onPartDestroyed));
        }

        public override void OnActive()
        {
            base.OnActive();
            if (!deployed)
            {
                onDeployEvent();
            }
            else if (!decoupled)
            {
                onDecoupleEvent();
            }
        }

        public override string GetInfo()
        {
            return "This part has configurable diameter (independent top/bottom) and height.";
        }
               
        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            GameEvents.onVesselWasModified.Remove(new EventData<Vessel>.OnEvent(onVesselModified));
            GameEvents.onVesselGoOffRails.Remove(new EventData<Vessel>.OnEvent(onVesselUnpack));
            GameEvents.onVesselGoOnRails.Remove(new EventData<Vessel>.OnEvent(onVesselPack));
            GameEvents.onPartDie.Remove(new EventData<Part>.OnEvent(onPartDestroyed));
        }

        //Unity updatey cycle override/hook
        public void FixedUpdate()
        {
            if (animating)
            {
                updateAnimation();
            }
        }

        //IMultipleDragCube override
        public string[] GetDragCubeNames()
        {
            return new string[]
            {
                "Open",
                "Closed"
            };
        }

        //IMultipleDragCube override
        public void AssumeDragCubePosition(string name)
        {
            if ("Open".Equals(name))
            {
                setPanelRotations(deployedRotation);
            }
            else
            {
                setPanelRotations(0);
            }
        }

        //IMultipleDragCube override
        public bool UsesProceduralDragCubes() { return false; }

        //IAirstreamShield override
        public bool ClosedAndLocked() { return !deployed; }

        //IAirstreamShield override
        public Vessel GetVessel() { return part.vessel; }

        //IAirstreamShield override
        public Part GetPart() { return part; }

        //IPartCostModifier override
        public float GetModuleCost(float cost) { return fairingCost; }

        //IPartMassModifier override
        public float GetModuleMass(float mass) { return -mass + fairingMass; }

        #endregion

        #region KSP Game Event callback methods

        public void onEditorVesselModified(ShipConstruct ship)
        {
            if (lastTopRadiusExtra != topRadiusExtra )
            {
                setTopRadiusFromEditor(editorTopRadius + (topRadiusExtra * topRadiusAdjust), true);
            }
            if (lastBottomRadiusExtra != bottomRadiusExtra )
            {
                setBottomRadiusFromEditor(editorBottomRadius + (bottomRadiusExtra * bottomRadiusAdjust), true);
            }
            if ( lastHeightExtra != heightExtra)
            {
                setHeightFromEditor(editorHeight + (heightExtra * heightAdjust), true);
            }
            if (lastStraightExtra != straightExtra)
            {
                setStraightHeightFromEditor(editorStraightHeight + (straightExtra * heightAdjust), true);
            }
            setPanelOpacity(0.25f);
        }

        public void onVesselModified(Vessel v)
        {
            updateShieldStatus();
        }

        public void onVesselUnpack(Vessel v)
        {
            updateShieldStatus();
        }

        public void onVesselPack(Vessel v)
        {
            clearShieldedParts();
        }

        public void onPartDestroyed(Part p)
        {
            clearShieldedParts();
            if (p != part)
            {
                updateShieldStatus();
            }
        }

        #endregion

        #region private action handling methods

        private void onDeployEvent()
        {
            if (!deployed)
            {
                animating = true;
                deployed = true;
                decoupleNode(part.findAttachNode(topNodeName));
                updateShieldStatus();
                updateGuiState();
            }
        }

        private void onDecoupleEvent()
        {
            if (deployed && !decoupled)
            {
                decoupled = true;
                decoupleNode(part.findAttachNode(internalNodeName));
                updateGuiState();
            }
        }

        private void decoupleNode(AttachNode node)
        {
            Part attachedPart = node.attachedPart;
            if (attachedPart == null) { return; }
            if (attachedPart == part.parent)
            {
                part.decouple(0f);
            }
            else
            {
                attachedPart.decouple(0f);
            }
        }

        #endregion

        #region fairing data update methods

        private void setPanelRotations(float rotation)
        {
            if (fairingBase != null)
            {
                fairingBase.setPanelRotations(rotation);
            }
        }

        private void setPanelOpacity(float val)
        {
            if (fairingBase != null) { fairingBase.setPanelOpacity(val); }
        }

        private void updateAnimation()
        {
            float delta = TimeWarp.fixedDeltaTime * animationSpeed;
            float previousAngle = currentRotation;
            currentRotation += delta;
            if (currentRotation >= deployedRotation)
            {
                currentRotation = deployedRotation;
                animating = false;
                updateShieldStatus();
            }
            setPanelRotations(currentRotation);
            updateDragCube();
        }

        private void updateDragCube()
        {
            float percentDeployed = currentRotation / deployedRotation;
            part.DragCubes.SetCubeWeight("Open", percentDeployed);
            part.DragCubes.SetCubeWeight("Closed", 1f - percentDeployed);
        }

        private void enableEditorColliders(bool val)
        {
            if (fairingBase.editorColliders != null)
            {
                SSTUUtils.enableColliderRecursive(fairingBase.editorColliders.transform, val);
            }
        }
                
        #endregion

        #region fairing rebuild methods            

        private void rebuildFairing(bool userInput)
        {
            if (fairingBase != null)
            {
                fairingBase.root.transform.parent = null;
                GameObject.Destroy(fairingBase.root);
                fairingBase = null;
            }
            createPanels();
            setPanelRotations(currentRotation);//set animation status to whatever is current
            fairingBase.enablePanelColliders(false, false);
            updateFairingMassAndCost();
            updateNodePositions(userInput);
            recreateDragCubes();
            updateShieldStatus();
        }

        //create procedural panel sections for the current part configuration (radialSection count), with orientation set from base panel orientation
        private void createPanels()
        {
            float totalHeight = baseHeight + currentHeight;
            float startHeight = -(totalHeight / 2);

            float tRad, bRad, height;
            tRad = topRadius;
            bRad = bottomRadius;
            height = currentHeight;

            InterstageFairingGenerator fg = new InterstageFairingGenerator(startHeight, baseHeight, currentStraightHeight, boltPanelHeight, height, maxPanelSectionHeight, bRad, tRad, wallThickness, numOfRadialSections, cylinderSides);
            fairingBase = fg.buildFairing();
            Transform modelTransform = part.transform.FindRecursive("model");
            fairingBase.root.transform.NestToParent(modelTransform);
            fairingBase.root.transform.rotation = modelTransform.rotation;
            fairingBase.setMaterial(fairingMaterial);
            if (HighLogic.LoadedSceneIsEditor) { setPanelOpacity(0.25f); }
            else { setPanelOpacity(1.0f); }
        }

        private void recreateDragCubes()
        {
            if (part.partInfo == null) { return; }
            setPanelRotations(deployedRotation);
            this.openCube = DragCubeSystem.Instance.RenderProceduralDragCube(part);
            setPanelRotations(0);
            this.closedCube = DragCubeSystem.Instance.RenderProceduralDragCube(part);
            this.closedCube.Name = "Closed";
            this.openCube.Name = "Open";
            part.DragCubes.ClearCubes();
            part.DragCubes.Cubes.Add(closedCube);
            part.DragCubes.Cubes.Add(openCube);
            part.DragCubes.ResetCubeWeights();
            updateDragCube();
        }

        private void updateFairingMassAndCost()
        {
            float baseVolume = bottomRadius * bottomRadius * baseHeight * Mathf.PI;
            float avgRadius = bottomRadius + (topRadius - bottomRadius) * 0.5f;
            float panelArea = avgRadius * 2f * Mathf.PI * currentHeight;

            float baseCost = costPerBaseVolume * baseVolume;
            float panelCost = costPerPanelArea * panelArea;
            float baseMass = massPerBaseVolume * baseVolume;
            float panelMass = massPerPanelArea * panelArea;

            fairingCost = panelCost + baseCost;
            fairingMass = panelMass + baseMass;

            part.mass = fairingMass;
        }
              
        #endregion

        #region shield update methods

        private void updateShieldStatus()
        {
            clearShieldedParts();
            AttachNode upperNode = part.findAttachNode(topNodeName);
            if (!deployed && upperNode!=null && upperNode.attachedPart != null)
            {
                findShieldedParts();
            }
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
            if (shieldedParts.Count > 0)
            {
                clearShieldedParts();
            }
            AttachNode upperNode = part.findAttachNode(topNodeName);
            if (upperNode==null || upperNode.attachedPart == null)//nothing on upper node to do the shielding...
            {
                return;
            }

            float totalHeight = currentHeight + baseHeight;
            float topY = (totalHeight * 0.5f);
            float bottomY = -(totalHeight * 0.5f) + baseHeight;

            Bounds combinedBounds = SSTUUtils.getRendererBoundsRecursive(fairingBase.root);
            SSTUUtils.findShieldedPartsCylinder(part, combinedBounds, shieldedParts, topY, bottomY, topRadius, bottomRadius);

            for (int i = 0; i < shieldedParts.Count; i++)
            {
                shieldedParts[i].AddShield(this);
            }
            partsShielded = shieldedParts.Count;
        }

        #endregion

        #region private helper methods

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            SSTUUtils.destroyChildren(part.transform.FindRecursive("model"));
            ConfigNode node = SSTUNodeUtils.parseConfigNode(configNodeData);
            ConfigNode[] limitNodes = node.GetNodes("TECHLIMIT");
            int len = limitNodes.Length;
            techLimits = new TechLimitDiameterHeight[len];
            for (int i = 0; i < len; i++) { techLimits[i] = new TechLimitDiameterHeight(limitNodes[i]); }

            loadMaterial();
            updateTechLimits();
            if (topRadius * 2 > techLimitMaxDiameter) { topRadius = techLimitMaxDiameter * 0.5f; }
            if (bottomRadius * 2 > techLimitMaxDiameter) { bottomRadius = techLimitMaxDiameter * 0.5f; }
            rebuildFairing(false);//will create fairing using default / previously saved fairing configuration

            restoreEditorFields();
            updateGuiState();
        }

        private void restoreEditorFields()
        {
            float div = topRadius / topRadiusAdjust;
            float whole = (int)div;
            float extra = div - whole;
            editorTopRadius = whole * topRadiusAdjust;
            topRadiusExtra = lastTopRadiusExtra = extra;

            div = bottomRadius / bottomRadiusAdjust;
            whole = (int)div;
            extra = div - whole;
            editorBottomRadius = whole * bottomRadiusAdjust;
            bottomRadiusExtra = lastBottomRadiusExtra = extra;

            div = currentHeight / heightAdjust;
            whole = (int)div;
            extra = div - whole;
            editorHeight = whole * heightAdjust;
            heightExtra = lastHeightExtra = extra;

            div = currentStraightHeight / heightAdjust;
            whole = (int)div;
            extra = div - whole;
            editorStraightHeight = whole * heightAdjust;
            straightExtra = lastStraightExtra = extra;
        }

        private void loadMaterial()
        {
            if (fairingMaterial != null)
            {
                Material.Destroy(fairingMaterial);
                fairingMaterial = null;
            }
            fairingMaterial = SSTUUtils.loadMaterial(diffuseTextureName, normalTextureName, "KSP/Bumped Specular");
        }

        private void setTopRadiusFromEditor(float newRadius, bool updateSymmetry)
        {
            if (newRadius > maxRadius) { newRadius = maxRadius; }
            if (newRadius < minRadius) { newRadius = minRadius; }
            if (SSTUUtils.isResearchGame() && newRadius * 2 > techLimitMaxDiameter) { newRadius = techLimitMaxDiameter * 0.5f; }
            topRadius = newRadius;
            rebuildFairing(true);
            updateShieldStatus();
            restoreEditorFields();

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUInterstageFairing>().setTopRadiusFromEditor(newRadius, false);
                }
            }
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        private void setBottomRadiusFromEditor(float newRadius, bool updateSymmetry)
        {
            if (newRadius > maxRadius) { newRadius = maxRadius; }
            if (newRadius < minRadius) { newRadius = minRadius; }
            if (SSTUUtils.isResearchGame() && newRadius*2 > techLimitMaxDiameter) { newRadius = techLimitMaxDiameter*0.5f; }
            bottomRadius = newRadius;
            rebuildFairing(true);
            updateShieldStatus();
            restoreEditorFields();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUInterstageFairing>().setBottomRadiusFromEditor(newRadius, false);
                }
            }
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        private void setHeightFromEditor(float newHeight, bool updateSymmetry)
        {
            if (newHeight > maxHeight) { newHeight = maxHeight; }
            if (newHeight < minHeight) { newHeight = minHeight; }
            if (SSTUUtils.isResearchGame() && newHeight > techLimitMaxHeight) { newHeight = techLimitMaxHeight; }
            if (currentStraightHeight > newHeight) { currentStraightHeight = newHeight; }
            currentHeight = newHeight;
            rebuildFairing(true);
            updateShieldStatus();
            restoreEditorFields();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUInterstageFairing>().setHeightFromEditor(newHeight, false);
                }
            }
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        private void setStraightHeightFromEditor(float newHeight, bool updateSymmetry)
        {
            if (newHeight > currentHeight) { newHeight = currentHeight; }
            if (newHeight < 0) { newHeight = 0; }
            currentStraightHeight = newHeight;
            rebuildFairing(true);
            updateShieldStatus();
            restoreEditorFields();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUInterstageFairing>().setHeightFromEditor(newHeight, false);
                }
            }
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        /// <summary>
        /// Update the tech limitations for this part
        /// </summary>        
        private void updateTechLimits()
        {
            techLimitMaxDiameter = float.PositiveInfinity;
            techLimitMaxHeight = float.PositiveInfinity;
            if (!SSTUUtils.isResearchGame()) { return; }
            if (HighLogic.CurrentGame == null) { return; }
            techLimitMaxDiameter = 0;
            techLimitMaxHeight = 0;
            foreach (TechLimitDiameterHeight limit in techLimits)
            {
                if (limit.isUnlocked())
                {
                    if (limit.maxDiameter > techLimitMaxDiameter) { techLimitMaxDiameter = limit.maxDiameter; }
                    if (limit.maxHeight > techLimitMaxHeight) { techLimitMaxHeight = limit.maxHeight; }
                }
            }
        }

        private void updateNodePositions(bool userInput)
        {
            float halfDistance = (currentHeight + baseHeight) * 0.5f;
            float lowestY = -halfDistance;
            float innerY = -halfDistance + baseHeight;
            float topY = halfDistance;

            Vector3 topLocal = new Vector3(0, topY, 0);
            Vector3 innerLocal = new Vector3(0, innerY, 0);
            Vector3 bottomLocal = new Vector3(0, lowestY, 0);

            AttachNode node = part.findAttachNode(bottomNodeName);
            if (node != null)
            {
                SSTUUtils.updateAttachNodePosition(part, node, bottomLocal, node.orientation, userInput);
            }
            node = part.findAttachNode(internalNodeName);
            if (node != null)
            {
                SSTUUtils.updateAttachNodePosition(part, node, innerLocal, node.orientation, userInput);
            }
            node = part.findAttachNode(topNodeName);
            if (node != null)
            {
                SSTUUtils.updateAttachNodePosition(part, node, topLocal, node.orientation, userInput);
            }
        }

        private void updateGuiState()
        {
            Events["deployEvent"].active = !deployed && !decoupled;//only available if not previously deployed or decoupled
            Events["decoupleEvent"].active = deployed && !decoupled;//only available if deployed but not decoupled
            Actions["deployAction"].active = !deployed && !decoupled;//only available if not previously deployed or decoupled
            Actions["decoupleAction"].active = deployed && !decoupled;//only available if deployed but not decoupled			
        }

        #endregion
    }
}

