using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System.Text;
namespace SSTUTools
{
    /// <summary>
    /// Procedrually created (and adjustable/configurable) replacement for engine fairings, or any other part-attached fairing.
    /// </summary>           
    public class SSTUNodeFairing : PartModule
    {
        #region REGION - Standard KSP Config Fields
        /// <summary>
        /// CSV List of transforms to remove from the model, to be used to override stock engine fairing configuration
        /// </summary>
        [KSPField]
        public String rendersToRemove = String.Empty;

        /// <summary>
        /// Name used for GUI actions for this fairing
        /// </summary>
        [KSPField]
        public String fairingName = "Fairing";
        
        /// <summary>
        /// If can manually jettison, this will be the action name in the GUI (combined with fairing name above)
        /// </summary>
        [KSPField]
        public String actionName = "Jettison";

        /// <summary>
        /// The node that this fairing will watch if fairing type == node
        /// </summary>
        [KSPField]
        public String nodeName = String.Empty;

        [KSPField]
        public bool snapToNode = true;

        [KSPField]
        public bool snapToSecondNode = false;

        [KSPField]
        public bool updateDragCubes = true;

        [KSPField]
        public bool canDisableInEditor = true;

        /// <summary>
        /// Can user jettison fairing manually when in flight? - should mostly be used for non-node attached fairings
        /// </summary>
        [KSPField]
        public bool canManuallyJettison = false;

        /// <summary>
        /// If the fairing will automatically jettison/reparent when its attached node is decoupled
        /// </summary>
        [KSPField]
        public bool canAutoJettison = true;

        //determines if user can adjust bottom diameter (also needs per-fairing canAdjustBottom flag)
        [KSPField]
        public bool canAdjustTop = false;

        //determines if user can adjust top diameter (also needs per-fairing canAdjustTop flag)
        [KSPField]
        public bool canAdjustBottom = false;

        /// <summary>
        /// Can user adjust how many fairing sections this fairing consists of?
        /// </summary>
        [KSPField]
        public bool canAdjustSections = true;

        /// <summary>
        /// Increment to be used when adjusting top radius
        /// </summary>
        [KSPField]
        public float topDiameterIncrement = 0.625f;

        /// <summary>
        /// Increment to be used when adjusting bottom radius
        /// </summary>
        [KSPField]
        public float bottomDiameterIncrement = 0.625f;

        /// <summary>
        /// Maximum top radius (by whole increment; adjust slider will allow this + one radius increment)
        /// </summary>
        [KSPField]
        public float maxTopDiameter = 10;

        /// <summary>
        /// Minimum top radius
        /// </summary>
        [KSPField]
        public float minTopDiameter = 0.625f;

        /// <summary>
        /// Maximum bottom radius (by whole increment; adjust slider will allow this + one radius increment)
        /// </summary>
        [KSPField]
        public float maxBottomDiameter = 10;

        /// <summary>
        /// Minimum bottom radius
        /// </summary>
        [KSPField]
        public float minBottomDiameter = 0.625f;

        #endregion

        #region REGION - GUI Visible Config Fields

        [KSPField(isPersistant =true, guiActiveEditor = true, guiName ="Opacity"), UI_Toggle(disabledText ="Opaque", enabledText = "Transparent", suppressEditorShipModified = true)]
        public bool editorTransparency = true;

        /// <summary>
        /// Number of sections for the fairing, only enabled for editing if 'canAdjustSections' == true
        /// </summary>
        [KSPField(guiActiveEditor = true, guiName = "Sections", isPersistant = true), UI_FloatRange(minValue = 1f, stepIncrement = 1f, maxValue = 6f, suppressEditorShipModified = true)]
        public float numOfSections = 1;

        [KSPField(guiName = "Top Diam", guiActiveEditor = true),
         UI_FloatEdit(sigFigs =4, suppressEditorShipModified = true)]
        public float guiTopDiameter = 1.25f;

        [KSPField(guiName = "Bot. Diam", guiActiveEditor = true),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float guiBottomDiameter = 1.25f;

        [KSPField(isPersistant = true)]
        public String currentTextureSet = String.Empty;

        [KSPField(isPersistant = true, guiName ="Colliders", guiActiveEditor = true), UI_Toggle(disabledText = "Disabled", enabledText = "Enabled", suppressEditorShipModified = true)]
        public bool generateColliders = false;

        #endregion

        #region REGION - Persistent config fields


        /// <summary>
        /// Has the fairing been jettisoned?  If true, no further interaction is possible.  Only set to true by in-flight jettison actions
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool fairingJettisoned = false;
        
        /// <summary>
        /// If fairing has been currently enabled/disabled by user
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool fairingEnabled = true;

        /// <summary>
        /// If fairing has been 'force' enabled/disabled by external plugin (MEC).  This completely removes GUI interaction and forces permanent disabled status unless re-enabled by external plugin
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool fairingForceDisabled = false;

        /// <summary>
        /// Persistent tracking of if the fairing has removed its 'jettision mass' from the parent part (and added it to its attached part, if a node-attached fairing)
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool removedMass = false;

        //this one is quite hacky; storing ConfigNode data in the string, because the -fields- load fine on revert-to-vab (and everywhere), but the config-node data is not present in all situations
        /// <summary>
        /// Persistent data from fairing parts; stores their current top/bottom positions and radius data
        /// </summary>
        [KSPField(isPersistant = true)]
        public String persistentDataString = String.Empty;

        [Persistent]
        public string configNodeData = string.Empty;

        #endregion

        #region REGION - private working vars, not user editable
        private float prevNumOfSections = 0;
        private float prevTopDiameter;
        private float prevBottomDiameter;

        private bool currentlyEnabled = false;
        private bool renderingJettisonedFairing = false;
        
        //the current fairing panels
        private SSTUNodeFairingData[] fairingParts;

        /// <summary>
        /// If not null, will be applied during initialization or late update tick
        /// </summary>
        private FairingUpdateData externalUpdateData = null;

        //private vars set from examining the individual fairing sections; these basically control gui enabled/disabled status
        private bool enableBottomDiameterControls;
        private bool enableTopDiameterControls;
        
        //material used for procedural fairing, created from the texture references in the texture set definitions
        private Material fairingMaterial;

        private TextureSet[] textureSets;
        
        private Part prevAttachedPart = null;

        private bool needsStatusUpdate = false;
        
        private bool needsRebuilt = false;

        private bool needsGuiUpdate = false;

        private bool currentRenderEnabled = true;
                
        #endregion

        #region REGION - Gui Interaction Methods

        [KSPAction("Jettison Fairing")]
        public void jettisonAction(KSPActionParam param)
        {
            jettisonFairing();
            updateGuiState();
        }
        
        [KSPEvent(guiName = "Jettison Fairing", guiActive = true, guiActiveEditor = true)]
        public void jettisonEvent()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                enableFairing(!fairingEnabled);
            }
            else
            {
                jettisonFairing();
            }
            updateGuiState();
            SSTUNodeFairing f;
            int index = part.Modules.IndexOf(this);
            foreach (Part p in part.symmetryCounterparts)
            {
                f = (SSTUNodeFairing)p.Modules[index];
                if (HighLogic.LoadedSceneIsEditor) { f.enableFairing(fairingEnabled); }
                else { f.jettisonFairing(); }
                f.updateGuiState();
            }
        }
        
        [KSPEvent(guiName = "Next Texture", guiActiveEditor = true)]
        public void nextTextureEvent()
        {
            if (textureSets != null && textureSets.Length > 0)
            {
                TextureSet s = SSTUUtils.findNext(textureSets, m => m.name == currentTextureSet, false);
                currentTextureSet = s.name;
                updateTextureSet();
            }
            SSTUNodeFairing f;
            int index = part.Modules.IndexOf(this);
            foreach (Part p in part.symmetryCounterparts)
            {
                f = (SSTUNodeFairing)p.Modules[index];
                f.currentTextureSet = this.currentTextureSet;
                f.updateTextureSet();
            }
        }

        public void colliderGuiUpdated(BaseField field, object obj)
        {
            foreach (FairingData fd in fairingParts)
            {
                if (fd.generateColliders != generateColliders)
                {
                    fd.generateColliders = this.generateColliders;
                    needsRebuilt = true;
                }
            }
        }

        public void transparencyUpdated(BaseField field, object obj)
        {
            updateOpacity();
        }

        public void topDiameterUpdated(BaseField field, object obj)
        {
            float radius = guiTopDiameter * 0.5f;
            foreach (SSTUNodeFairingData data in fairingParts)
            {
                if (data.canAdjustTop)
                {
                    if (data.topRadius != radius)
                    {
                        needsRebuilt = true;
                        data.topRadius = radius;
                    }
                }
            }
            prevTopDiameter = guiTopDiameter;
        }

        public void bottomDiameterUpdated(BaseField field, object obj)
        {
            float radius = guiBottomDiameter * 0.5f;
            foreach (SSTUNodeFairingData data in fairingParts)
            {
                if (data.canAdjustBottom)
                {
                    if (data.bottomRadius != radius)
                    {
                        needsRebuilt = true;
                        data.bottomRadius = radius;
                    }
                }
            }
            prevBottomDiameter = guiBottomDiameter;
        }

        public void sectionsUpdated(BaseField field, object obj)
        {
            if (numOfSections != prevNumOfSections)
            {
                needsRebuilt = true;
                prevNumOfSections = numOfSections;
            }
        }

        #endregion

        #region REGION - ksp overrides

        //on load, not called properly on 'revertToVAB'
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);            
            updatePersistentDataString();
            node.SetValue("persistentDataString", persistentDataString, true);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
            this.updateUIFloatEditControl("guiTopDiameter", minTopDiameter, maxTopDiameter, topDiameterIncrement*2, topDiameterIncrement, topDiameterIncrement*0.05f, true, guiTopDiameter);
            this.updateUIFloatEditControl("guiBottomDiameter", minBottomDiameter, maxBottomDiameter, bottomDiameterIncrement*2, bottomDiameterIncrement, bottomDiameterIncrement*0.05f, true, guiBottomDiameter);
            Fields["guiTopDiameter"].uiControlEditor.onFieldChanged = topDiameterUpdated;
            Fields["guiBottomDiameter"].uiControlEditor.onFieldChanged = bottomDiameterUpdated;
            Fields["numOfSections"].uiControlEditor.onFieldChanged = sectionsUpdated;
            Fields["editorTransparency"].uiControlEditor.onFieldChanged = transparencyUpdated;
            Fields["generateColliders"].uiControlEditor.onFieldChanged = colliderGuiUpdated;
            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(onVesselModified));
        }
        
        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            GameEvents.onVesselWasModified.Remove(new EventData<Vessel>.OnEvent(onVesselModified));
        }

        public void onVesselModified(Vessel v)
        {
            if (!HighLogic.LoadedSceneIsFlight) { return; }
            needsStatusUpdate = true;
        }

        public void onEditorVesselModified(ShipConstruct ship)
        {
            if (!HighLogic.LoadedSceneIsEditor) { return; }
            needsStatusUpdate = true;
        }

        private void initialize()
        {
            if (rendersToRemove != null && rendersToRemove.Length > 0)
            {
                SSTUUtils.removeTransforms(part, SSTUUtils.parseCSV(rendersToRemove));
            }
            loadFairingData(SSTUConfigNodeUtils.parseConfigNode(configNodeData));
            if (externalUpdateData != null)
            {
                updateFromExternalData(externalUpdateData);
            }
            updateEditorFields(false);//update cached editor gui field values for diameter, sections, etc.
            buildFairing();//construct fairing from cached/persistent/default data
            needsStatusUpdate = true;
            if (textureSets != null)
            {
                if (textureSets.Length <= 1)//only a single, (or no) texture set selected/avaialable
                {
                    Events["nextTextureEvent"].active = false;
                }
                if (textureSets.Length > 0 && String.IsNullOrEmpty(currentTextureSet))
                {
                    TextureSet s = textureSets[0];
                    currentTextureSet = textureSets[0].name;
                }
            }
        }

        private void updatePersistentDataString()
        {            
            if (fairingParts == null) { return; }
            StringBuilder sb = new StringBuilder();
            int len = fairingParts.Length;
            for (int i = 0; i < len; i++)
            {
                if (i > 0)
                {
                    sb.Append(":");
                }
                sb.Append(fairingParts[i].getPersistence());
            }
            persistentDataString = sb.ToString();
        }

        public void LateUpdate()
        {
            if (externalUpdateData != null)
            {
                updateFromExternalData(externalUpdateData);
            }
            if (needsStatusUpdate)
            {
                needsStatusUpdate = false;
                updateFairingStatus();
            }
            if (needsRebuilt)
            {
                updateFairingStatus();
                rebuildFairing();
                updatePersistentDataString();
                updateEditorFields(false);
                SSTUStockInterop.fireEditorUpdate();
                needsGuiUpdate = true;
                needsRebuilt = false;
            }
            if (needsGuiUpdate)
            {
                updateGuiState();
                needsGuiUpdate = false;
            }
        }
        
        #endregion
        
        #region REGION - external interaction methods
         
        public void updateExternal(FairingUpdateData data)
        {
            externalUpdateData = data;
        }

        private void updateFromExternalData(FairingUpdateData eData)
        {
            if (fairingParts == null) { MonoBehaviour.print("ERROR: Fairing parts are null for external update"); }
            foreach (SSTUNodeFairingData data in fairingParts)
            {                
                if (eData.hasTopY && data.canAdjustTop)
                {
                    if (eData.topY != data.topY)
                    {
                        needsRebuilt = true;
                    }
                    data.topY = eData.topY;
                }
                if (eData.hasBottomY && data.canAdjustBottom)
                {
                    if (eData.bottomY != data.bottomY)
                    {
                        needsRebuilt = true;
                    }
                    data.bottomY = eData.bottomY;
                }
                if (eData.hasTopRad && data.canAdjustTop)
                {
                    if (eData.topRadius != data.topRadius)
                    {
                        needsRebuilt = true;
                    }
                    data.topRadius = eData.topRadius;
                }
                if (eData.hasBottomRad && data.canAdjustBottom)
                {
                    if (eData.bottomRadius != data.bottomRadius)
                    {
                        needsRebuilt = true;
                    }
                    data.bottomRadius = eData.bottomRadius;
                }
            }
            if (eData.hasEnable)
            {
                fairingForceDisabled = !eData.enable;
            }
            else
            {
                fairingForceDisabled = false;//default to NOT force disabled
            }
            updateEditorFields(true);
            needsGuiUpdate = true;
            externalUpdateData = null;
        }

        #endregion

        //TODO updateDragCube
        #region REGION - private utility methods

        #region REGION - Initialization methods

        //restores the values to the editor size-adjust fields from the loaded values from the fairing
        /// <summary>
        /// Updates the editor GUI fields with current live values and upates the prev/cached check values
        /// </summary>
        private void updateEditorFields(bool forceUpdate)
        {
            if (!HighLogic.LoadedSceneIsEditor) { return; }
            float topRadius = 0;
            float bottomRadius = 0;            
            foreach (FairingData data in fairingParts)
            {
                if (data.canAdjustTop && data.topRadius > topRadius) { topRadius = data.topRadius; }
                if (data.canAdjustBottom && data.bottomRadius > bottomRadius) { bottomRadius = data.bottomRadius; }
                if (data.numOfSections > numOfSections) { numOfSections = data.numOfSections; }
            }
            foreach (FairingData data in fairingParts)//loop a second time to fix any adjustable fairings that were below the adjustment size for whatever reason
            {
                if (data.canAdjustTop && data.topRadius < topRadius) { data.topRadius =  topRadius; }
                if (data.canAdjustBottom && data.bottomRadius < bottomRadius) { data.bottomRadius = bottomRadius; }
                if (data.numOfSections < numOfSections) { data.numOfSections = (int)Math.Round(numOfSections); }
            }
            prevTopDiameter = guiTopDiameter = topRadius * 2f;
            prevBottomDiameter = guiBottomDiameter = bottomRadius * 2f;
            prevNumOfSections = numOfSections;
            if (forceUpdate)
            {
                this.updateUIFloatEditControl("guiTopDiameter", topRadius * 2f);
                this.updateUIFloatEditControl("guiBottomDiameter", bottomRadius * 2f);
            }
            //re-seat the values due to stock firing more update events in the middle of force-updating the widgets
            prevTopDiameter = guiTopDiameter = topRadius * 2f;
            prevBottomDiameter = guiBottomDiameter = bottomRadius * 2f;
        }        

        //creates/recreates FairingData instances from data from config node and any persistent node (if applicable)
        private void loadFairingData(ConfigNode node)
        {
            ConfigNode[] fairingNodes = node.GetNodes("FAIRING");
            fairingParts = new SSTUNodeFairingData[fairingNodes.Length];

            Transform modelBase = part.transform.FindRecursive("model");
            Transform parent;
            SSTUNodeFairing[] cs = part.GetComponents<SSTUNodeFairing>();
            int l = Array.IndexOf(cs, this);
            int moduleIndex = l;
            for (int i = 0; i < fairingNodes.Length; i++)
            {
                parent = modelBase.FindOrCreate(fairingName + "-" + moduleIndex + "-"+i);
                fairingParts[i] = new SSTUNodeFairingData();
                fairingParts[i].load(fairingNodes[i], parent.gameObject);
                if (fairingParts[i].canAdjustTop)
                {
                    enableTopDiameterControls = true;
                }
                if (fairingParts[i].canAdjustBottom)
                {
                    enableBottomDiameterControls = true;
                }
            }
            if (!String.IsNullOrEmpty(persistentDataString))
            {
                String[] datas = SSTUUtils.parseCSV(persistentDataString, ":");
                int length = datas.Length;
                for (int i = 0; i < length; i++)
                {
                    fairingParts[i].loadPersistence(datas[i]);
                }
            }

            if (fairingMaterial != null)
            {
                Material.Destroy(fairingMaterial);
                fairingMaterial = null;
            }

            textureSets = TextureSet.loadGlobalTextureSets(node.GetNodes("TEXTURESET"));
            TextureSet t = Array.Find(textureSets, m => m.name == currentTextureSet);
            fairingMaterial = t.textureData[0].createMaterial("SSTUFairingMaterial");
        }

        /// <summary>
        /// Updates GUI labels and action availability based on current module state (jettisoned, watchedNode attached status, canAdjustRadius, etc)
        /// </summary>
        private void updateGuiState()
        {
            bool topAdjustEnabled = enableTopDiameterControls && canAdjustTop;
            bool bottomAdjustEnabled = enableBottomDiameterControls && canAdjustBottom;
            if (fairingForceDisabled || !currentlyEnabled || fairingJettisoned)//adjustment not possible if faring jettisoned
            {
                topAdjustEnabled = bottomAdjustEnabled = false;
            }
            Fields["guiTopDiameter"].guiActiveEditor = topAdjustEnabled;
            Fields["guiBottomDiameter"].guiActiveEditor = bottomAdjustEnabled;
            Fields["numOfSections"].guiActiveEditor = currentlyEnabled && canAdjustSections;
            Events["nextTextureEvent"].active =  currentlyEnabled && textureSets != null && textureSets.Length > 1;
            Events["nextTextureEvent"].guiName = fairingName + " Next Texture";

            bool enableButtonActive = false;
            if (HighLogic.LoadedSceneIsEditor)
            {
                enableButtonActive = !fairingForceDisabled && ( (currentlyEnabled && canDisableInEditor) || canSpawnFairing() );
            }
            else//flight scene....
            {
                enableButtonActive = currentlyEnabled && canManuallyJettison && (numOfSections > 1 || String.IsNullOrEmpty(nodeName));
            }
            String guiActionName = HighLogic.LoadedSceneIsEditor ? (currentlyEnabled ? "Disable" : "Enable") : actionName;
            Events["jettisonEvent"].guiName = guiActionName + " " + fairingName;
            Actions["jettisonAction"].guiName = actionName + " " + fairingName;
            Events["jettisonEvent"].active = Actions["jettisonAction"].active = enableButtonActive;
            Fields["editorTransparency"].guiActiveEditor = currentlyEnabled;
            Fields["generateColliders"].guiActiveEditor = currentlyEnabled;
        }

        private void updateOpacity()
        {
            float opacity = editorTransparency && HighLogic.LoadedSceneIsEditor ? 0.25f : 1f;
            foreach (FairingData fd in fairingParts) { fd.fairingBase.setOpacity(opacity); }
        }

        #endregion

        #region REGION - Fairing Update Methods

        /// <summary>
        /// Blanket method to update the attached/visible status of the fairing based on its fairing type, current jettisoned status, and if a part is present on the fairings watched node (if any/applicable)
        /// </summary>
        private void updateFairingStatus()
        {
            if (fairingForceDisabled)
            {
                currentlyEnabled = false;
                enableFairingRender(false);
            }
            else if (!fairingEnabled || fairingJettisoned)
            {
                currentlyEnabled = false;
                if (!renderingJettisonedFairing)
                {
                    enableFairingRender(false);
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(nodeName))//should watch node
                {
                    updateStatusForNode();
                }
                else//manual fairing
                {
                    currentlyEnabled = true;
                    enableFairingRender(true);
                }
            }
            updateShieldingStatus();
            updateOpacity();
            needsGuiUpdate = true;
        }

        private void updateStatusForNode()
        {
            AttachNode watchedNode = null;
            Part triggerPart = null;
            float fairingPos = 0;
            if (shouldSpawnFairingForNode(out watchedNode, out triggerPart, out fairingPos))
            {
                if (snapToNode)
                {
                    foreach (SSTUNodeFairingData data in fairingParts)
                    {
                        if (data.canAdjustBottom && data.bottomY != fairingPos)
                        {
                            data.bottomY = fairingPos;
                            needsRebuilt = true;
                        }
                    }
                }
                currentlyEnabled = true;
                enableFairingRender(true);
                prevAttachedPart = triggerPart;
            }
            else
            {
                currentlyEnabled = false;
                if (prevAttachedPart != null)//had part previously attached, jettison it
                {
                    if (HighLogic.LoadedSceneIsFlight)
                    {
                        if (canAutoJettison)
                        {
                            jettisonFairing();
                        }
                        else
                        {
                            renderingJettisonedFairing = true;
                        }
                    }
                }
                if (!renderingJettisonedFairing)
                {
                    enableFairingRender(false);
                }
            }
        }
        
        /// <summary>
        /// Reparents the fairing panel parts to the input part; should only be used on jettison of the fairings when they stay attached to the part below
        /// </summary>
        /// <param name="newParent"></param>
        private void reparentFairing(Part newParent)
        {
            foreach (SSTUNodeFairingData data in fairingParts)
            {
                data.fairingBase.reparentFairing(newParent.transform, false);
            }
        }
        
        private void jettisonFairing()
        {
            renderingJettisonedFairing = true;
            if (numOfSections == 1 && prevAttachedPart!=null)
            {
                reparentFairing(prevAttachedPart);
            }
            else
            {
                foreach (SSTUNodeFairingData data in fairingParts)
                {
                    data.jettisonPanels(part);
                }
            }
            fairingJettisoned = true;
            currentlyEnabled = false;
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }
        
        private void enableFairing(bool enable)
        {
            fairingEnabled = enable;
            needsStatusUpdate = true;
        }
        
        private void enableFairingRender(bool val)
        {
            currentRenderEnabled = val;
            foreach (FairingData fd in fairingParts)
            {
                fd.enableRenders(val);
                fd.enableColliders(generateColliders);
            }
            updateOpacity();
        }

        private void buildFairing()
        {
            if (HighLogic.LoadedSceneIsEditor)//only enforce editor sizing while in the editor;
            {
                foreach (SSTUNodeFairingData fd in fairingParts)
                {
                    if (fd.canAdjustTop)
                    {
                        fd.topRadius = guiTopDiameter * 0.5f;
                    }
                    if (fd.canAdjustBottom)
                    {
                        fd.bottomRadius = guiBottomDiameter * 0.5f;
                    }
                }
            }
            foreach (FairingData fd in fairingParts)
            {
                fd.generateColliders = generateColliders;
                fd.facesPerCollider = 1;
                fd.numOfSections = (int)Math.Round(numOfSections);
                fd.createFairing(fairingMaterial, editorTransparency? 0.25f : 1f);
            }
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }
        
        private void rebuildFairing()
        {
            buildFairing();
            enableFairingRender(currentRenderEnabled);
            updateOpacity();
        }
        
        private void updateTextureSet()
        {
            if (textureSets != null && !String.IsNullOrEmpty(currentTextureSet))
            {
                TextureSet t = Array.Find(textureSets, m => m.name == currentTextureSet);
                if (t != null)
                {
                    TextureSetMaterialData d = t.textureData[0];
                    foreach (SSTUNodeFairingData f in fairingParts)
                    {
                        d.enable(f.fairingBase.rootObject, Color.clear);
                    }
                }
            }
            updateOpacity();
        }

        private bool shouldSpawnFairingForNode(out AttachNode watchedNode, out Part triggerPart, out float fairingPos)
        {
            watchedNode = part.FindAttachNode(nodeName);
            if (watchedNode == null)
            {
                //no node found, return false
                fairingPos = 0;
                triggerPart = null;
                return false;
            }
            triggerPart = watchedNode.attachedPart;
            fairingPos = watchedNode.position.y;
            if (snapToSecondNode && triggerPart != null)
            {
                watchedNode = getLowestNode(triggerPart, out fairingPos);
                if (watchedNode != null && watchedNode.attachedPart != part)//don't spawn fairing if there is only one node and this is the part attached
                {
                    triggerPart = watchedNode.attachedPart;
                }
                else
                {
                    triggerPart = null;
                }
            }
            return triggerPart != null;
        }

        /// <summary>
        /// Returns true for empty/null node name (whereas shouldSpawnFairing returns false)
        /// </summary>
        /// <returns></returns>
        private bool canSpawnFairing()
        {
            if (String.IsNullOrEmpty(nodeName)) { return true; }
            AttachNode n = null;
            Part p = null;
            float pos;
            return shouldSpawnFairingForNode(out n, out p, out pos);
        }

        private AttachNode getLowestNode(Part p, out float fairingPos)
        {
            AttachNode node = null;
            AttachNode nodeTemp;
            float pos = float.PositiveInfinity;
            Vector3 posTemp;
            int len = p.attachNodes.Count;

            for (int i = 0; i < len; i++)
            {
                nodeTemp = p.attachNodes[i];
                posTemp = nodeTemp.position;
                posTemp = p.transform.TransformPoint(posTemp);
                posTemp = part.transform.InverseTransformPoint(posTemp);
                if (posTemp.y < pos)
                {
                    node = nodeTemp;
                    pos = posTemp.y;
                }
            }
            fairingPos = pos;
            return node;
        }

        private void updateShieldingStatus()
        {
            SSTUAirstreamShield shield = part.GetComponent<SSTUAirstreamShield>();
            if (shield != null)
            {
                if (currentlyEnabled)
                {
                    string name = fairingName + "" + part.Modules.IndexOf(this);
                    float top=float.NegativeInfinity, bottom=float.PositiveInfinity, topRad=0, botRad=0;
                    bool useTop=false, useBottom=false;
                    if (!string.IsNullOrEmpty(nodeName))
                    {
                        useTop = nodeName == "top";
                        useBottom = nodeName == "bottom";
                    }
                    int len = fairingParts.Length;
                    FairingData fp;
                    for (int i = 0; i < len; i++)
                    {
                        fp = fairingParts[i];
                        if (fp.topY > top) { top = fp.topY; }
                        if (fp.bottomY < bottom) { bottom = fp.bottomY; }
                        if (fp.topRadius > topRad) { topRad = fp.topRadius; }
                        if (fp.bottomRadius > botRad) { botRad = fp.bottomRadius; }
                    }
                    shield.addShieldArea(name, topRad, botRad, top, bottom, useTop, useBottom);
                }
                else
                {
                    shield.removeShieldArea(fairingName + "" + part.Modules.IndexOf(this));
                }
            }
        }

        #endregion

        #endregion
    }

    public class SSTUNodeFairingData : FairingData
    {
        public void loadPersistence(String data)
        {
            String[] csv = SSTUUtils.parseCSV(data);
            topY = SSTUUtils.safeParseFloat(csv[0]);
            bottomY = SSTUUtils.safeParseFloat(csv[1]);
            topRadius = SSTUUtils.safeParseFloat(csv[2]);
            bottomRadius = SSTUUtils.safeParseFloat(csv[3]);
        }

        public String getPersistence()
        {
            return topY + "," + bottomY + "," + topRadius + "," + bottomRadius;
        }
    }
    
    public class FairingUpdateData
    {
        public bool enable;
        public float topY;
        public float bottomY;
        public float topRadius;
        public float bottomRadius;
        public bool hasEnable;
        public bool hasTopY;
        public bool hasBottomY;
        public bool hasTopRad;
        public bool hasBottomRad;
        public FairingUpdateData() { }
        public void setTopY(float val) { topY = val; hasTopY = true; }
        public void setBottomY(float val) { bottomY = val; hasBottomY = true; }
        public void setTopRadius(float val) { topRadius = val;  hasTopRad = true; }
        public void setBottomRadius(float val) { bottomRadius = val;  hasBottomRad = true; }
        public void setEnable(bool val) { enable = val; hasEnable = true; }

        public override string ToString()
        {
            return enable + ", " + topY + ", " + bottomY + ", " + topRadius + ", " + bottomRadius;
        }
    }

}

