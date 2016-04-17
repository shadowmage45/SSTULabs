using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUInterstageFairing : PartModule, IMultipleDragCube, IPartCostModifier, IPartMassModifier
    {
        #region KSP MODULE fields
        //config fields for various transform and node names

        //reference to the model for this part; the texture and shader are retrieved from this model
        [KSPField]
        public String modelName = "SSTU/Assets/SC-GEN-FR";

        /// <summary>
        /// The diameter of the mounting point of the model
        /// </summary>
        [KSPField]
        public float defaultModelDiameter = 5f;
        
        /// <summary>
        /// At the models default diameter, what is the diameter of the bottom of the fairing?
        /// </summary>
        [KSPField]
        public float defaultFairingDiameter = 5f;

        [KSPField]
        public float defaultBaseVolume = 1f;
                
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
        public float minDiameter = 0.625f;

        [KSPField]
        public float maxDiameter = 10f;

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
        public float topDiameterIncrement = 0.625f;

        [KSPField]
        public float bottomDiameterIncrement = 0.625f;

        [KSPField]
        public float heightIncrement = 1;

        [KSPField]
        public String techLimitSet = "Default";

        [KSPField]
        public String uvMap = "NodeFairing";

        [KSPField(isPersistant = true, guiName = "Texture Set", guiActiveEditor = true)]
        public String currentTextureSet = String.Empty;

        /// <summary>
        /// Top diameter of the -fairing-
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Top Diameter"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float topDiameter = 2.5f;
        
        /// <summary>
        /// Bottom diameter of the -model-; this is not necessarily the bottom diameter of the fairing
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Base Diameter"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float bottomDiameter = 2.5f;

        //stored current height of the panels, used to recreate mesh on part reload, may be set in config to set the default starting height
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Total Height"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentHeight = 1.0f;

        //if top radius !=bottom radius, this will create a 'split' panel at this position, for a straight-up-then-tapered/flared fairing
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Taper Height"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentStraightHeight = 0f;

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

        [KSPField(guiName = "Fairing Cost", guiActiveEditor = true)]
        public float fairingCost;

        [KSPField(guiName = "Fairing Mass", guiActiveEditor = true)]
        public float fairingMass;

        [KSPField(guiName ="Editor Transparency", guiActiveEditor = true), UI_Toggle(enabledText ="On", disabledText ="Off", suppressEditorShipModified = true)]
        public bool editorTransparency = true;

        [KSPField(guiName = "Colliders", guiActiveEditor = true, isPersistant =true), UI_Toggle(enabledText = "Enabled", disabledText = "Disabled", suppressEditorShipModified = true)]
        public bool generateColliders = false;

        #endregion

        #region private working variables
        private float prevHeight;
        private float prevStraightHeight;
        private float prevTopDiameter;
        private float prevBottomDiameter;
        private bool initialized;

        private InterstageFairingContainer fairingBase;        

        //material used for procedural fairing, created from the texture references above
        private Material fairingMaterial;

        //list of parts that are shielded from the airstream
        //rebuilt whenever vessel is modified
        private List<Part> shieldedParts = new List<Part>();

        // tech limit values are updated every time the part is initialized in the editor; ignored otherwise
        private float techLimitMaxDiameter;

        private TextureSet currentTextureSetData;
        private TextureSet[] textureSetData;

        //lerp between the two cubes depending upon deployed state
        //re-render the cubes on fairing rebuild
        private DragCube closedCube;
        private DragCube openCube;

        #endregion

        #region KSP GUI Actions

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

        [KSPAction("Deploy and Release Top Node")]
        public void deployAction(KSPActionParam param)
        {
            onDeployEvent();
        }

        [KSPAction("Decouple Inner Node")]
        public void decoupleAction(KSPActionParam param)
        {
            onDecoupleEvent();
        }

        [KSPEvent(guiName = "Next Texture", guiActiveEditor = true)]
        public void nextTextureEvent()
        {
            TextureSet next = SSTUUtils.findNext(textureSetData, m => m.setName == currentTextureSet, false);
            setTextureFromEditor(next == null ? null : next.setName, true);
        }
        
        public void onCollidersUpdated(BaseField field, object obj)
        {
            if (fairingBase.generateColliders != this.generateColliders)
            {
                fairingBase.generateColliders = this.generateColliders;
                rebuildFairing(true);
            }
        }

        public void onTransparencyUpdated(BaseField field, object obj)
        {
            fairingBase.setOpacity(editorTransparency ? 0.25f : 1);
        }

        public void onTopDiameterUpdated(BaseField field, object obj)
        {
            if (prevTopDiameter != topDiameter)
            {
                prevTopDiameter = topDiameter;
                setTopDiameterFromEditor(topDiameter, true);
            }
        }

        public void onBottomDiameterUpdated(BaseField field, object obj)
        {
            if (prevBottomDiameter != bottomDiameter)
            {
                prevBottomDiameter = bottomDiameter;
                setBottomDiameterFromEditor(bottomDiameter, true);
            }
        }

        public void onHeightUpdated(BaseField field, object obj)
        {
            if (prevHeight != currentHeight)
            {
                prevHeight = currentHeight;
                setHeightFromEditor(currentHeight, true);
            }
        }

        public void onStraightUpdated(BaseField field, object obj)
        {
            if (prevStraightHeight != currentStraightHeight)
            {
                prevStraightHeight = currentStraightHeight;
                setStraightHeightFromEditor(currentStraightHeight, true);
            }
        }

        private void setTopDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {
            if (newDiameter > maxDiameter) { newDiameter = maxDiameter; }
            if (newDiameter < minDiameter) { newDiameter = minDiameter; }
            if (SSTUUtils.isResearchGame() && newDiameter > techLimitMaxDiameter) { newDiameter = techLimitMaxDiameter; }
            topDiameter = newDiameter;
            rebuildFairing(true);
            updateShieldStatus();
            restoreEditorFields();

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUInterstageFairing>().setTopDiameterFromEditor(newDiameter, false);
                }
            }
        }

        private void setBottomDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {
            if (newDiameter > maxDiameter) { newDiameter = maxDiameter; }
            if (newDiameter < minDiameter) { newDiameter = minDiameter; }
            if (SSTUUtils.isResearchGame() && newDiameter > techLimitMaxDiameter) { newDiameter = techLimitMaxDiameter; }
            bottomDiameter = newDiameter;
            rebuildFairing(true);
            updateShieldStatus();
            restoreEditorFields();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUInterstageFairing>().setBottomDiameterFromEditor(newDiameter, false);
                }
            }
        }

        private void setHeightFromEditor(float newHeight, bool updateSymmetry)
        {
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
        }

        private void setStraightHeightFromEditor(float newHeight, bool updateSymmetry)
        {
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
        }

        private void setTextureFromEditor(String newTexture, bool updateSymmetry)
        {
            currentTextureSet = newTexture;
            currentTextureSetData = Array.Find(textureSetData, m => m.setName == newTexture);
            if (currentTextureSetData == null)
            {
                currentTextureSetData = textureSetData[0];
                currentTextureSet = currentTextureSetData.setName;
                newTexture = currentTextureSet;
            }
            TextureData data = currentTextureSetData.textureDatas[0];
            data.enableForced(fairingBase.rootObject.transform, true);
            if (updateSymmetry)
            {
                SSTUInterstageFairing dc;
                foreach (Part p in part.symmetryCounterparts)
                {
                    dc = p.GetComponent<SSTUInterstageFairing>();
                    dc.setTextureFromEditor(newTexture, false);
                }
            }
        }

        #endregion

        #region KSP overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            initialize();
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);            
            initialize();
            Events["nextTextureEvent"].guiActiveEditor = textureSetData.Length > 1;
            float max = techLimitMaxDiameter < maxDiameter ? techLimitMaxDiameter : maxDiameter;
            this.updateUIFloatEditControl("topDiameter", minDiameter, max, topDiameterIncrement*2, topDiameterIncrement, topDiameterIncrement*0.05f, true, topDiameter);
            this.updateUIFloatEditControl("bottomDiameter", minDiameter, max, bottomDiameterIncrement*2, bottomDiameterIncrement, bottomDiameterIncrement*0.05f, true, bottomDiameter);
            this.updateUIFloatEditControl("currentHeight", minHeight, maxHeight, heightIncrement*2, heightIncrement, heightIncrement*0.05f, true, currentHeight);
            this.updateUIFloatEditControl("currentStraightHeight", 0, maxHeight, heightIncrement*2, heightIncrement, heightIncrement*0.05f, true, currentStraightHeight);
            Fields["topDiameter"].uiControlEditor.onFieldChanged = onTopDiameterUpdated;
            Fields["bottomDiameter"].uiControlEditor.onFieldChanged = onBottomDiameterUpdated;
            Fields["currentHeight"].uiControlEditor.onFieldChanged = onHeightUpdated;
            Fields["currentStraightHeight"].uiControlEditor.onFieldChanged = onStraightUpdated;
            Fields["editorTransparency"].uiControlEditor.onFieldChanged = onTransparencyUpdated;
            Fields["generateColliders"].uiControlEditor.onFieldChanged = onCollidersUpdated;
            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorShipModified));
        }

        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorShipModified));
        }

        public void onEditorShipModified(ShipConstruct ship)
        {
            if (!HighLogic.LoadedSceneIsEditor) { return; }
            fairingBase.setOpacity(editorTransparency ? 0.25f : 1);
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
            return "This part has configurable top diameter, bottom diameter, height, and taper start height.  Includes functions to decouple both top and inner payloads and airstream protection for inner payload.";
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
        
        //IPartCostModifier override
        public float GetModuleCost(float cost, ModifierStagingSituation sit) { return -cost + fairingCost; }

        //IPartMassModifier override
        public float GetModuleMass(float mass, ModifierStagingSituation sit) { return -mass + fairingMass; }
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        #endregion

        #region KSP Game Event callback methods

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
            SSTUModInterop.onPartGeometryUpdate(part, false);
            recreateDragCubes();
        }

        //create procedural panel sections for the current part configuration (radialSection count), with orientation set from base panel orientation
        private void createPanels()
        {
            float modelFairingScale = defaultFairingDiameter / defaultModelDiameter;
            float bottomRadius = bottomDiameter * modelFairingScale * 0.5f;
            float topRadius = topDiameter * 0.5f;

            fairingBase.clearProfile();
            fairingBase.addRing(0, bottomRadius);
            if (topRadius!=bottomRadius && currentStraightHeight < currentHeight)
            {
                fairingBase.addRing(currentStraightHeight, bottomRadius);
            }
            fairingBase.addRing(currentHeight, topDiameter * 0.5f);
            fairingBase.generateColliders = this.generateColliders;
            fairingBase.generateFairing();
            fairingBase.setMaterial(fairingMaterial);
            if (HighLogic.LoadedSceneIsEditor && editorTransparency) { setPanelOpacity(0.25f); }
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
            float baseScale = bottomDiameter / defaultModelDiameter;
            float baseVolume = baseScale * baseScale * baseScale * defaultBaseVolume;
            float avgDiameter = bottomDiameter + (topDiameter - bottomDiameter) * 0.5f;
            float panelArea = avgDiameter * Mathf.PI * currentHeight;//circumference * height = area

            float baseCost = costPerBaseVolume * baseVolume;
            float panelCost = costPerPanelArea * panelArea;
            float baseMass = massPerBaseCubicMeter * baseVolume;
            float panelMass = massPerPanelArea * panelArea;

            fairingCost = panelCost + baseCost;
            fairingMass = panelMass + baseMass;
        }
              
        #endregion        

        #region private helper methods

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);

            ConfigNode[] textureNodes = node.GetNodes("TEXTURESET");
            textureSetData = TextureSet.loadTextureSets(textureNodes);
            currentTextureSetData = Array.Find(textureSetData, m => m.setName == currentTextureSet);
            if (currentTextureSetData == null)
            {
                currentTextureSetData = textureSetData[0];
                currentTextureSet = currentTextureSetData.setName;
            }

            TextureData data = currentTextureSetData.textureDatas[0];
            fairingMaterial = SSTUUtils.loadMaterial(data.diffuseTextureName, null, "KSP/Specular");

            loadMaterial();

            TechLimit.updateTechLimits(techLimitSet, out techLimitMaxDiameter);
            if (topDiameter > techLimitMaxDiameter)
            {
                topDiameter = techLimitMaxDiameter;
            }
            if (bottomDiameter > techLimitMaxDiameter)
            {
                bottomDiameter = techLimitMaxDiameter;
            }

            Transform tr = part.transform.FindRecursive("model").FindOrCreate("PetalAdapterRoot");
            fairingBase = new InterstageFairingContainer(tr.gameObject, cylinderSides, numberOfPanels, wallThickness);
            UVMap uvs = UVMap.GetUVMapGlobal(uvMap);
            fairingBase.outsideUV = uvs.getArea("outside");
            fairingBase.insideUV = uvs.getArea("inside");
            fairingBase.edgesUV = uvs.getArea("edges");

            rebuildFairing(false);//will create fairing using default / previously saved fairing configuration
            restoreEditorFields();
            updateGuiState();
        }

        private void restoreEditorFields()
        {
            if (currentStraightHeight > currentHeight)
            {
                prevStraightHeight = currentStraightHeight = currentHeight;
                this.updateUIFloatEditControl("currentStraightHeight", currentStraightHeight);
            }
            prevTopDiameter = topDiameter;
            prevBottomDiameter = bottomDiameter;
            prevHeight = currentHeight;
            prevStraightHeight = currentStraightHeight;
        }

        private void loadMaterial()
        {
            if (fairingMaterial != null)
            {
                Material.Destroy(fairingMaterial);
                fairingMaterial = null;
            }
            TextureData data = currentTextureSetData.textureDatas[0];
            fairingMaterial = SSTUUtils.loadMaterial(data.diffuseTextureName, null, "KSP/Specular");
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
            return bottomDiameter / defaultModelDiameter;
        }

        private void updateGuiState()
        {
            Events["deployEvent"].active = !deployed && !decoupled;//only available if not previously deployed or decoupled
            Events["decoupleEvent"].active = deployed && !decoupled;//only available if deployed but not decoupled
            Actions["deployAction"].active = !deployed && !decoupled;//only available if not previously deployed or decoupled
            Actions["decoupleAction"].active = deployed && !decoupled;//only available if deployed but not decoupled			
        }
        
        private void updateShieldStatus()
        {
            SSTUAirstreamShield shield = part.GetComponent<SSTUAirstreamShield>();
            MonoBehaviour.print("SSTUInterstageFairing updating shielding status for module: " + shield + " :: enabled: " + !deployed);
            if (shield != null)
            {
                string name = "InterstageFairingShield" + "" + part.Modules.IndexOf(this);
                if (!deployed)
                {
                    float top = currentHeight; ;
                    float bottom = 0f;
                    float topRad = topDiameter*0.5f;
                    float botRad = bottomDiameter*0.5f;
                    bool useTop = true;
                    bool useBottom = false;
                    shield.addShieldArea(name, topRad, botRad, top, bottom, useTop, useBottom);
                }
                else
                {
                    shield.removeShieldArea(name);
                }
            }
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

