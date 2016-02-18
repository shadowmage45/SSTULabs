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
        public String modelName = "SSTU/Assets/SC-GEN-FR";

        [KSPField]
        public float defaultModelDiameter = 5f;

        [KSPField]
        public float defaultBaseVolume = 1f;

        [KSPField]
        public String diffuseTextureName = "SSTU/Assets/SC-GEN-Fairing-DIFF";
        
        [KSPField]
        public float internalNodePosition;

        [KSPField]
        public float bottomNodePosition;
        
        [KSPField]
        public String topNodeName = "top";

        [KSPField]
        public String bottomNodeName = "bottom";

        [KSPField]
        public String internalNodeName = "internal";

        [KSPField]
        public int topDecouplerModuleIndex = 1;

        [KSPField]
        public int internalDecouplerModuleIndex = 2;

        //how many sections should the fairing have radially?
        [KSPField]
        public int numberOfPanels = 4;
        
        [KSPField]
        public float wallThickness = 0.025f;
        
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

        //how far should the panels be rotated for the 'deployed' animation
        [KSPField]
        public float deployedRotation = 60f;

        //how many degrees per second should the fairings rotate while deploy animation is playing?
        [KSPField]
        public float animationSpeed = 5f;

        [KSPField]
        public float costPerBaseVolume = 1500f;

        [KSPField]
        public float costPerPanelArea = 50f;

        [KSPField]
        public float massPerBaseCubicMeter = 0.5f;

        [KSPField]
        public float massPerPanelArea = 0.025f;

        [KSPField]
        public float topRadiusIncrement = 0.625f;

        [KSPField]
        public float bottomRadiusIncrement = 0.625f;

        [KSPField]
        public float heightIncrement = 1;

        //radius of the part, used to calculate mesh
        [KSPField(isPersistant = true)]
        public float bottomRadius = 1.25f;

        //radius of the top of the part, used to calculate mesh
        [KSPField(isPersistant = true)]
        public float topRadius = 1.25f;

        //stored current height of the panels, used to recreate mesh on part reload, may be set in config to set the default starting height
        [KSPField(isPersistant = true)]
        public float currentHeight = 1.0f;

        //are planels deployed and upper node decoupled?
        //toggled to true as soon as deploy action is activated
        [KSPField(isPersistant = true)]
        public bool deployed = false;

        //is inner node decoupled?
        //toggled to true as soon as inner node is decoupled, only available after deployed=true
        [KSPField(isPersistant = true)]
        public bool decoupled = false;

        //deployment animation persistence field
        [KSPField(isPersistant = true)]
        public float currentRotation = 0.0f;

        [KSPField(isPersistant = true)]
        public bool animating = false;

        //if top radius !=bottom radius, this will create a 'split' panel at this position, for a straight-up-then-tapered/flared fairing
        [KSPField(isPersistant = true)]
        public float currentStraightHeight = 0f;

        [KSPField(guiActive = true, guiName = "Parts Shielded", guiActiveEditor = true)]
        public int partsShielded = 0;

        [KSPField(guiName = "Fairing Cost", guiActiveEditor = true)]
        public float fairingCost;

        [KSPField(guiName = "Fairing Mass", guiActiveEditor = true)]
        public float fairingMass;

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

        private InterstageFairingContainer fairingBase;        

        //material used for procedural fairing, created from the texture references above
        private Material fairingMaterial;

        //list of parts that are shielded from the airstream
        //rebuilt whenever vessel is modified
        private List<Part> shieldedParts = new List<Part>();

        // tech limit values are updated every time the part is initialized in the editor; ignored otherwise
        private float techLimitMaxDiameter;
        private TechLimitDiameter[] techLimits;

        //lerp between the two cubes depending upon deployed state
        //re-render the cubes on fairing rebuild
        private DragCube closedCube;
        private DragCube openCube;

        #endregion

        #region KSP GUI Actions

        [KSPEvent(name = "increaseHeightEvent", guiName = "Increase Height", guiActiveEditor = true)]
        public void increaseHeightEvent()
        {
            setHeightFromEditor(currentHeight + heightIncrement, true);
        }

        [KSPEvent(name = "decreaseHeightEvent", guiName = "Decrease Height", guiActiveEditor = true)]
        public void decreaseHeightEvent()
        {
            setHeightFromEditor(currentHeight - heightIncrement, true);
        }

        [KSPEvent(guiName = "Increase Straight Height", guiActiveEditor = true)]
        public void increaseStraightHeightEvent()
        {
            setStraightHeightFromEditor(currentStraightHeight + heightIncrement, true);
        }

        [KSPEvent(guiName = "Decrease Straight Height", guiActiveEditor = true)]
        public void decreaseStraightHeightEvent()
        {
            setStraightHeightFromEditor(currentStraightHeight - heightIncrement, true);
        }

        [KSPEvent(name = "increaseTopRadiusEvent", guiName = "Top Radius +", guiActiveEditor = true)]
        public void increaseTopRadiusEvent()
        {
            setTopRadiusFromEditor(topRadius + topRadiusIncrement, true);
        }

        [KSPEvent(name = "decreaseTopRadiusEvent", guiName = "Top Radius -", guiActiveEditor = true)]
        public void decreaseTopRadiusEvent()
        {
            setTopRadiusFromEditor(topRadius - topRadiusIncrement, true);
        }

        [KSPEvent(name = "increaseBottomRadiusEvent", guiName = "Bottom Radius +", guiActiveEditor = true)]
        public void increaseBottomRadiusEvent()
        {
            setBottomRadiusFromEditor(bottomRadius + bottomRadiusIncrement, true);
        }

        [KSPEvent(name = "decreaseBottomRadiusEvent", guiName = "Bottom Radius -", guiActiveEditor = true)]
        public void decreaseBottomRadiusEvent()
        {
            setBottomRadiusFromEditor(bottomRadius - bottomRadiusIncrement, true);
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
                setTopRadiusFromEditor(editorTopRadius + (topRadiusExtra * topRadiusIncrement), true);
            }
            if (lastBottomRadiusExtra != bottomRadiusExtra )
            {
                setBottomRadiusFromEditor(editorBottomRadius + (bottomRadiusExtra * bottomRadiusIncrement), true);
            }
            if ( lastHeightExtra != heightExtra)
            {
                setHeightFromEditor(editorHeight + (heightExtra * heightIncrement), true);
            }
            if (lastStraightExtra != straightExtra)
            {
                setStraightHeightFromEditor(editorStraightHeight + (straightExtra * heightIncrement), true);
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
                decoupleByModule(topDecouplerModuleIndex);
                updateShieldStatus();
                updateGuiState();
            }
        }

        private void onDecoupleEvent()
        {
            if (deployed && !decoupled)
            {
                decoupled = true;
                decoupleByModule(internalDecouplerModuleIndex);
                updateGuiState();
            }
        }

        private void decoupleByModule(int index)
        {
            ModuleDecouple d = (ModuleDecouple)part.Modules[index];
            if (!d.isDecoupled) { d.Decouple(); }
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
            if (fairingBase != null) { fairingBase.setOpacity(val); }
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
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor) { return; }//don't touch them in the editor
            float percentDeployed = currentRotation / deployedRotation;
            part.DragCubes.SetCubeWeight("Open", percentDeployed);
            part.DragCubes.SetCubeWeight("Closed", 1f - percentDeployed);
        }

        private void enableEditorColliders(bool val)
        {
            fairingBase.enableEditorCollider(val);
        }
                
        #endregion

        #region fairing rebuild methods            

        private void rebuildFairing(bool userInput)
        {
            Transform model = part.transform.FindModel(modelName);
            if (model != null)
            {
                float scale = getCurrentScale();
                model.transform.localScale = new Vector3(scale, scale, scale);
            }
            else
            {
                SSTUUtils.recursePrintComponents(part.gameObject, "");
            }
            createPanels();
            setPanelRotations(currentRotation);//set animation status to whatever is current            
            updateFairingMassAndCost();
            updateNodePositions(userInput);
            updateShieldStatus();
            enableEditorColliders(HighLogic.LoadedSceneIsEditor);
            SSTUUtils.updatePartHighlighting(part);
            SSTUModInterop.onPartGeometryUpdate(part, false);
            recreateDragCubes();
        }

        //create procedural panel sections for the current part configuration (radialSection count), with orientation set from base panel orientation
        private void createPanels()
        {
            fairingBase.clearProfile();
            fairingBase.addRing(0, bottomRadius);
            fairingBase.addRing(currentStraightHeight, bottomRadius);
            fairingBase.addRing(currentHeight, topRadius);
            fairingBase.generateFairing();
            fairingBase.setMaterial(fairingMaterial);
            if (HighLogic.LoadedSceneIsEditor) { setPanelOpacity(0.25f); }
            else { setPanelOpacity(1.0f); }
        }

        private void recreateDragCubes()
        {
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor) { return; }//don't touch them in the prefab
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
            float baseScale = (bottomRadius*2) / defaultModelDiameter;
            float baseVolume = baseScale * baseScale * baseScale * defaultBaseVolume;
            float avgRadius = bottomRadius + (topRadius - bottomRadius) * 0.5f;
            float panelArea = avgRadius * 2f * Mathf.PI * currentHeight;

            float baseCost = costPerBaseVolume * baseVolume;
            float panelCost = costPerPanelArea * panelArea;
            float baseMass = massPerBaseCubicMeter * baseVolume;
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
            
            Bounds combinedBounds = SSTUUtils.getRendererBoundsRecursive(part.gameObject);
            SSTUUtils.findShieldedPartsCylinder(part, combinedBounds, shieldedParts, currentHeight, 0, topRadius, bottomRadius);

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
            loadMaterial();
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            techLimits = TechLimitDiameter.loadTechLimits(node.GetNodes("TECHLIMIT"));
            TechLimitDiameter.updateTechLimits(techLimits, out techLimitMaxDiameter);
            if (topRadius * 2 > techLimitMaxDiameter)
            {
                topRadius = techLimitMaxDiameter * 0.5f;
            }
            if (bottomRadius * 2 > techLimitMaxDiameter)
            {
                bottomRadius = techLimitMaxDiameter * 0.5f;
            }

            Transform tr = part.transform.FindRecursive("model").FindOrCreate("PetalAdapterRoot");
            fairingBase = new InterstageFairingContainer(tr.gameObject, cylinderSides, numberOfPanels, wallThickness);
            fairingBase.outsideUV = new UVArea(node.GetNode("UVMAP", "name", "outside"));
            fairingBase.insideUV = new UVArea(node.GetNode("UVMAP", "name", "inside"));
            fairingBase.edgesUV = new UVArea(node.GetNode("UVMAP", "name", "edges"));

            rebuildFairing(false);//will create fairing using default / previously saved fairing configuration
            restoreEditorFields();
            updateGuiState();
        }

        private void restoreEditorFields()
        {
            float div = topRadius / topRadiusIncrement;
            float whole = (int)div;
            float extra = div - whole;
            editorTopRadius = whole * topRadiusIncrement;
            topRadiusExtra = lastTopRadiusExtra = extra;

            div = bottomRadius / bottomRadiusIncrement;
            whole = (int)div;
            extra = div - whole;
            editorBottomRadius = whole * bottomRadiusIncrement;
            bottomRadiusExtra = lastBottomRadiusExtra = extra;

            div = currentHeight / heightIncrement;
            whole = (int)div;
            extra = div - whole;
            editorHeight = whole * heightIncrement;
            heightExtra = lastHeightExtra = extra;

            div = currentStraightHeight / heightIncrement;
            whole = (int)div;
            extra = div - whole;
            editorStraightHeight = whole * heightIncrement;
            straightExtra = lastStraightExtra = extra;
        }

        private void loadMaterial()
        {
            if (fairingMaterial != null)
            {
                Material.Destroy(fairingMaterial);
                fairingMaterial = null;
            }
            fairingMaterial = SSTUUtils.loadMaterial(diffuseTextureName, null, "KSP/Specular");
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

        private void updateNodePositions(bool userInput)
        {
            float scale = getCurrentScale();
            float topY = currentHeight;
            float innerY = internalNodePosition * scale;
            float bottomY = bottomNodePosition * scale;
            Vector3 bottomNodePOs = new Vector3(0, bottomY, 0);
            Vector3 innerNodePos = new Vector3(0, innerY, 0);
            Vector3 topNodePos = new Vector3(0, topY, 0);

            AttachNode node = part.findAttachNode(bottomNodeName);
            if (node != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, node, bottomNodePOs, node.orientation, userInput);
            }
            node = part.findAttachNode(internalNodeName);
            if (node != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, node, innerNodePos, node.orientation, userInput);
            }
            node = part.findAttachNode(topNodeName);
            if (node != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, node, topNodePos, node.orientation, userInput);
            }
        }

        private float getCurrentScale()
        {
            return (bottomRadius * 2) / defaultModelDiameter;
        }

        private void updateGuiState()
        {
            Events["deployEvent"].active = !deployed && !decoupled;//only available if not previously deployed or decoupled
            Events["decoupleEvent"].active = deployed && !decoupled;//only available if deployed but not decoupled
            Actions["deployAction"].active = !deployed && !decoupled;//only available if not previously deployed or decoupled
            Actions["decoupleAction"].active = deployed && !decoupled;//only available if deployed but not decoupled			
        }

        #endregion

        private class InterstageFairingContainer : FairingContainer
        {
            //this collider sits at the top of the fairing so that the payload properly snaps into position
            public GameObject editorCollider;
            public float editorColliderHeight = 0.1f;


            public InterstageFairingContainer(GameObject root, int cylinderFaces, int numberOfPanels, float thickness) : base(root, cylinderFaces, numberOfPanels, thickness)
            {

            }           

            public void enableEditorCollider(bool val)
            {
                if (editorCollider != null) { GameObject.Destroy(editorCollider); }
                if (val)
                {
                    float maxHeight = getHeight();
                    Vector3 offset = new Vector3(0, maxHeight - editorColliderHeight, 0);

                    CylinderMeshGenerator cmg = new CylinderMeshGenerator(offset, 12, editorColliderHeight, getBottomRadius(), getTopRadius(), 0, 0);
                    Mesh mesh = cmg.generateMesh();
                    editorCollider = new GameObject("PetalAdapterEditorCollider");
                    MeshFilter mf = editorCollider.AddComponent<MeshFilter>();
                    mf.mesh = mesh;
                    MeshCollider mc = editorCollider.AddComponent<MeshCollider>();
                    mc.convex = true;
                    mc.enabled = true;
                }
            }
        }
    }
}

