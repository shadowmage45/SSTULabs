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
    public class SSTUNodeFairing : PartModule, IAirstreamShield
    {
        #region REGION - Standard KSP Config Fields
        
        [KSPField]
        public String diffuseTextureName = "SSTU/Assets/SC-GEN-Fairing-DIFF";
                
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

        [KSPField]
        public bool canAdjustTop = false;

        [KSPField]
        public bool canAdjustBottom = false; 

        /// <summary>
        /// Can user adjust the enabled/disabled and fairing type?
        /// </summary>
        [KSPField]
        public bool canAdjustType = false;

        /// <summary>
        /// Can user adjust how many fairing sections this fairing consists of?
        /// </summary>
        [KSPField]
        public bool canAdjustSections = true;

        /// <summary>
        /// Increment to be used when adjusting top radius
        /// </summary>
        [KSPField]
        public float topRadiusAdjustSize = 0.625f;

        /// <summary>
        /// Increment to be used when adjusting bottom radius
        /// </summary>
        [KSPField]
        public float bottomRadiusAdjustSize = 0.625f;

        /// <summary>
        /// Maximum top radius (by whole increment; adjust slider will allow this + one radius increment)
        /// </summary>
        [KSPField]
        public float maxTopRadius = 5;

        /// <summary>
        /// Minimum top radius
        /// </summary>
        [KSPField]
        public float minTopRadius = 0.3125f;

        /// <summary>
        /// Maximum bottom radius (by whole increment; adjust slider will allow this + one radius increment)
        /// </summary>
        [KSPField]
        public float maxBottomRadius = 5;

        /// <summary>
        /// Minimum bottom radius
        /// </summary>
        [KSPField]
        public float minBottomRadius = 0.3125f;

        #endregion

        #region REGION - GUI Visible Config Fields

        /// <summary>
        /// UI field to tell user how many parts are shielded by fairing.  Only enabled on those fairings that have shielding enabled
        /// </summary>
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Shielded Part Count")]
        public int shieldedPartCount = 0;

        /// <summary>
        /// Number of sections for the fairing, only enabled for editing if 'canAdjustSections' == true
        /// </summary>
        [KSPField(guiActiveEditor = true, guiName = "Fairing Sections", isPersistant = true), UI_FloatRange(minValue = 1f, stepIncrement = 1f, maxValue = 6f)]
        public float numOfSections = 1;

        /// <summary>
        /// UI and functional fields to allow for fine-grained adjustment of fairing radius
        /// </summary>
        [KSPField(guiActiveEditor = true, guiName = "Top Rad Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
        public float topRadiusExtra;

        /// <summary>
        /// UI and functional fields to allow for fine-grained adjustment of fairing radius
        /// </summary>
        [KSPField(guiActiveEditor = true, guiName = "Bot Rad Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
        public float bottomRadiusExtra;

        [KSPField(isPersistant = true)]
        public String currentTextureSet = String.Empty;

        #endregion

        #region REGION - Persistent config fields

        /// <summary>
        /// Has the fairing been jettisoned?  If true, no further interaction is possible.  Only set to true by in-flight jettison actions
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool fairingJettisoned = false;
        
        /// <summary>
        /// If fairing has been 'force' disabled by user or external plugin interaction - overrides all other enabled/disabled settings
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool fairingEnabled = true;

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
        
        /// <summary>
        /// The raw config node for this PartModule, stashed during prefab initialization, public/peristent so that unity will serialize it to non-prefab parts correctly
        /// </summary>
        [Persistent]
        public String configNodeData = String.Empty;

        #endregion

        #region REGION - fairing airstream shield vars

        /// <summary>
        /// Determines if this fairing should check for part shielding.
        /// If true, the shieldTopY, shieldBottomY, shieldTopRadius, and shieldBottomRadius fields will need to be properly populated in the config
        /// </summary>
        [KSPField]
        public bool shieldParts = false;

        [KSPField]
        public float shieldTopY;

        [KSPField]
        public float shieldBottomY;

        [KSPField]
        public float shieldTopRadius;

        [KSPField]
        public float shieldBottomRadius;
        #endregion

        #region REGION - private working vars, not user editable
        //radius adjustment fields, mostly used in editor
        //these values are restored during the OnStart operation, and only used in the editor
        //the 'live' values for the fairing are stored persistently and used directly to update the
        //fairing physical attributes.
        //the 'live' values will be set from these values for further operations in the editor		
        private float editorTopRadius = 0;
        private float editorBottomRadius = 0;
        private float lastTopExtra = 0;
        private float lastBottomExtra = 0;
        private float prevNumOfSections = 0;

        private bool currentlyEnabled = false;
        private bool renderingJettisonedFairing = false;
        
        //the current fairing panels
        private SSTUNodeFairingData[] fairingParts;

        private bool canAdjustBottomRadius;
        private bool canAdjustTopRadius;
        
        //material used for procedural fairing, created from the texture references above
        private Material fairingMaterial;

        private TextureSet[] textureSets;

        //list of shielded parts
        private List<Part> shieldedParts = new List<Part>();

        private Part prevAttachedPart = null;

        private bool needsShieldUpdate = false;

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

        //TODO symmetry updates
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
        }

        //TODO symmetry updates
        [KSPEvent(guiName = "Top Rad +", guiActiveEditor = true)]
        public void increaseTopRadiusEvent()
        {
            if (canAdjustTopRadius && editorTopRadius < maxTopRadius)
            {
                editorTopRadius += topRadiusAdjustSize;
                if (editorTopRadius > maxTopRadius) { editorTopRadius = maxTopRadius; }
                needsRebuilt = true;
            }
        }

        //TODO symmetry updates
        [KSPEvent(guiName = "Top Rad -", guiActiveEditor = true)]
        public void decreaseTopRadiusEvent()
        {
            if (canAdjustTopRadius && editorTopRadius > minTopRadius)
            {
                editorTopRadius -= topRadiusAdjustSize;
                if (editorTopRadius < minTopRadius) { editorTopRadius = minTopRadius; }
                needsRebuilt = true;
            }
        }

        //TODO symmetry updates
        [KSPEvent(guiName = "Bottom Rad +", guiActiveEditor = true)]
        public void increaseBottomRadiusEvent()
        {
            if (canAdjustBottomRadius && editorBottomRadius < maxBottomRadius)
            {
                editorBottomRadius += bottomRadiusAdjustSize;
                if (editorBottomRadius > maxBottomRadius) { editorBottomRadius = maxBottomRadius; }
                needsRebuilt = true;
            }
        }

        //TODO symmetry updates
        [KSPEvent(guiName = "Bottom Rad -", guiActiveEditor = true)]
        public void decreaseBottomRadiusEvent()
        {
            if (canAdjustBottomRadius && editorBottomRadius > minBottomRadius)
            {
                editorBottomRadius -= bottomRadiusAdjustSize;
                if (editorBottomRadius < minBottomRadius) { editorBottomRadius = minBottomRadius; }
                needsRebuilt = true;
            }
        }

        //TODO symmetry updates
        [KSPEvent(guiName = "Next Texture", guiActiveEditor = true)]
        public void nextTextureEvent()
        {
            if (textureSets != null && textureSets.Length > 0)
            {
                TextureSet s = SSTUUtils.findNext(textureSets, m => m.setName == currentTextureSet, false);
                currentTextureSet = s.setName;
                MonoBehaviour.print("set texture set name to: " + currentTextureSet);
                updateTextureSet();
            }
        }

        #endregion

        #region REGION - ksp overrides

        //on load, not called properly on 'revertToVAB'
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            //if prefab, load persistent config data into config node string
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
            {
                configNodeData = node.ToString();
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
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

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            
            //remove any stock transforms for engine-fairing overrides
            if (rendersToRemove != null && rendersToRemove.Length > 0)
            {
                removeTransforms();
            }


            //load FairingData instances from config values (persistent data nodes also merged in)
            loadFairingData(SSTUConfigNodeUtils.parseConfigNode(configNodeData));

            loadMaterial();

            //restore the editor field values from the loaded fairing (radius adjust stuff)
            if (HighLogic.LoadedSceneIsEditor)
            {
                restoreEditorFields();
            }

            //construct fairing from loaded data
            buildFairing();

            //update the visible and attachment status for the fairing
            updateFairingStatus();
            updateGuiState();

            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(onVesselModified));
            GameEvents.onPartDie.Add(new EventData<Part>.OnEvent(onPartDestroyed));

            if (textureSets != null)
            {
                print("checking texture sets length...");
                if (textureSets.Length <= 1)//only a single, (or no) texture set selected/avaialable
                {
                    print("disabling next texture button due to no more textures");
                    Events["nextTextureEvent"].active = false;
                }
                if (textureSets.Length > 0 && String.IsNullOrEmpty(currentTextureSet))
                {
                    print("setting current texture set to first available");
                    TextureSet s = textureSets[0];
                    print("loading from texture set: " + s);
                    currentTextureSet = textureSets[0].setName;
                }
            }
            else
            {
                print("textures were null!");
            }
            StartCoroutine(delayedDragUpdate());
        }
        
        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            GameEvents.onVesselWasModified.Remove(new EventData<Vessel>.OnEvent(onVesselModified));
            GameEvents.onPartDie.Remove(new EventData<Part>.OnEvent(onPartDestroyed));
        }

        public void onVesselModified(Vessel v)
        {
            if (HighLogic.LoadedSceneIsEditor) { return; }
            updateFairingStatus();
        }

        public void onEditorVesselModified(ShipConstruct ship)
        {
            if (canAdjustTopRadius && topRadiusExtra != lastTopExtra)
            {
                lastTopExtra = topRadiusExtra;
                needsRebuilt = true;
            }
            if (canAdjustBottomRadius && bottomRadiusExtra != lastBottomExtra)
            {
                lastBottomExtra = bottomRadiusExtra;
                needsRebuilt = true;
            }
            if (numOfSections != prevNumOfSections)
            {
                prevNumOfSections = numOfSections;
                needsRebuilt = true;
            }
            updateFairingStatus();
        }
        
        public void onPartDestroyed(Part p)
        {
            clearShieldedParts();
            if (p != part)
            {
                needsShieldUpdate = true;
            }
        }
        
        private IEnumerator delayedDragUpdate()
        {
            yield return new WaitForFixedUpdate();
            updateDragCube();
        }

        public void LateUpdate()
        {
            if (needsRebuilt)
            {
                updateFairingStatus();
                rebuildFairing();
                needsShieldUpdate = true;
                needsGuiUpdate = true;
                needsRebuilt = false;
            }
            if (needsShieldUpdate)
            {
                updateShieldStatus();
                needsGuiUpdate = true;
                needsShieldUpdate = false;
            }
            if (needsGuiUpdate)
            {
                updateGuiState();
                needsGuiUpdate = false;
            }
        }

        public bool initialized()
        {
            return fairingParts != null;
        }

        #endregion

        #region REGION - KSP AirstreamShield update methods

        //IAirstreamShield override
        public bool ClosedAndLocked() { return currentlyEnabled; }

        //IAirstreamShield override
        public Vessel GetVessel() { return part.vessel; }

        //IAirstreamShield override
        public Part GetPart() { return part; }

        private void updateShieldStatus()
        {
            clearShieldedParts();
            if (shieldParts && currentlyEnabled)
            {
                findShieldedParts();
            }
        }

        private void clearShieldedParts()
        {
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
            //TODO verify this works as intended.... could have weird side-effects (originally was pulling render bounds for the fairing object)
            //TODO instead of using entire part render bounds... combine the render bounds of all fairingData represented by this module
            Bounds combinedBounds = SSTUUtils.getRendererBoundsRecursive(part.gameObject);
            SSTUUtils.findShieldedPartsCylinder(part, combinedBounds, shieldedParts, shieldTopY, shieldBottomY, shieldTopRadius, shieldBottomRadius);
            for (int i = 0; i < shieldedParts.Count; i++)
            {
                shieldedParts[i].AddShield(this);
                //print("SSTUNodeFairing is shielding: " + shieldedParts[i].name);
            }
        }
        #endregion
        
        #region REGION - external interaction methods
            
        public void setFairingTopY(float newValue)
        {
            foreach (SSTUNodeFairingData data in fairingParts)
            {
                if (data.canAdjustTop)
                {
                    if (newValue != data.topY)
                    {
                        needsRebuilt = true;
                    }
                    data.topY = newValue;
                }
            }
        }
        
        public void setFairingBottomY(float newValue)
        {
            foreach (SSTUNodeFairingData data in fairingParts)
            {
                if (data.canAdjustBottom)
                {
                    if (newValue != data.bottomY)
                    {
                        needsRebuilt = true;
                    }
                    data.bottomY = newValue;
                }
            }
        }
        
        public void setFairingTopRadius(float topRadius)
        {
            foreach (SSTUNodeFairingData data in fairingParts)
            {
                if (data.canAdjustTop)
                {
                    if (topRadius != data.topRadius)
                    {
                        needsRebuilt = true;
                    }
                    data.topRadius = topRadius;
                }
            }
            restoreEditorFields();
        }
        
        public void setFairingBottomRadius(float bottomRadius)
        {
            foreach (SSTUNodeFairingData data in fairingParts)
            {
                if (data.canAdjustBottom)
                {
                    if (bottomRadius != data.bottomRadius)
                    {
                        needsRebuilt = true;
                    }
                    data.bottomRadius = bottomRadius;
                }
            }
            restoreEditorFields();
        }
        
        /// <summary>
        /// Only works in editor.  Intended to be used for other PartModules to interface with, so that they can dynamically adjust if the fairing is present or not.
        /// </summary>
        /// <param name="enable"></param>                      
        public void enableFairingFromEditor(bool enable)
        {
            enableFairing(enable);
        }
          
        #endregion
        
        //TODO updateDragCube
        #region REGION - private utility methods

        #region REGION - Initialization methods

        //restores the values to the editor size-adjust fields from the loaded values from the fairing
        private void restoreEditorFields()
        {
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
            float div, whole, extra;
            prevNumOfSections = numOfSections;
            if (canAdjustTopRadius)
            {
                div = topRadius / topRadiusAdjustSize;
                whole = (int)div;
                extra = div - whole;
                editorTopRadius = whole * topRadiusAdjustSize;
                topRadiusExtra = extra;
                lastTopExtra = topRadiusExtra;
            }
            if (canAdjustBottomRadius)
            {
                div = bottomRadius / bottomRadiusAdjustSize;
                whole = (int)div;
                extra = div - whole;
                editorBottomRadius = whole * bottomRadiusAdjustSize;
                bottomRadiusExtra = extra;
                lastBottomExtra = bottomRadiusExtra;
            }
            
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
            int moduleIndex = l;// part.Modules.IndexOf(this);
            for (int i = 0; i < fairingNodes.Length; i++)
            {
                parent = modelBase.FindOrCreate(fairingName + "-" + moduleIndex + "-"+i);
                fairingParts[i] = new SSTUNodeFairingData();
                fairingParts[i].load(fairingNodes[i], parent.gameObject);
                if (fairingParts[i].canAdjustTop)
                {
                    canAdjustTopRadius = true;
                }
                if (fairingParts[i].canAdjustBottom)
                {
                    canAdjustBottomRadius = true;
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
            
            textureSets = TextureSet.loadTextureSets(node.GetNodes("TEXTURESET"));
        }

        private void loadMaterial()
        {
            if (fairingMaterial != null)
            {
                Material.Destroy(fairingMaterial);
                fairingMaterial = null;
            }

            if (textureSets != null && !String.IsNullOrEmpty(currentTextureSet))
            {
                TextureSet t = Array.Find(textureSets, m => m.setName == currentTextureSet);
                if (t != null)
                {
                    TextureData d = t.textureDatas[0];
                    if (d != null)
                    {
                        diffuseTextureName = d.diffuseTextureName;
                    }
                }
            }
            fairingMaterial = SSTUUtils.loadMaterial(diffuseTextureName, String.Empty, "KSP/Specular");
        } 
        
        //removes any existing render transforms, for removal/overwriting of stock fairing module
        private void removeTransforms()
        {
            if (!String.IsNullOrEmpty(rendersToRemove))
            {
                String[] names = SSTUUtils.parseCSV(rendersToRemove);
                SSTUUtils.removeTransforms(part, names);
            }
        }

        #endregion

        #region REGION - Fairing Update Methods
        
        /// <summary>
        /// Blanket method to update the attached/visible status of the fairing based on its fairing type, current jettisoned status, and if a part is present on the fairings watched node (if any/applicable)
        /// </summary>
        private void updateFairingStatus()
        {            
            if (!fairingEnabled || fairingJettisoned)
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
                    AttachNode node = part.findAttachNode(nodeName);
                    if (node != null)
                    {
                        float fairingPos = node.position.y;
                        Part attachedPart = node.attachedPart;
                        if (snapToSecondNode)
                        {
                            if (attachedPart != null)
                            {
                                AttachNode newNode = null;
                                foreach (AttachNode n in attachedPart.attachNodes)
                                {
                                    if (newNode == null || n.position.y < newNode.position.y)
                                    {
                                        newNode = n;
                                    }
                                }
                                
                                AttachNode otn = attachedPart.findAttachNodeByPart(part);
                                Vector3 pos = newNode.position;
                                pos = attachedPart.transform.TransformPoint(pos);
                                pos = part.transform.InverseTransformPoint(pos);
                                fairingPos = pos.y;
                                node = newNode;
                                attachedPart = node == null ? null : node.attachedPart;
                            }
                        }

                        if (attachedPart != null)
                        {
                            currentlyEnabled = true;
                            enableFairingRender(true);
                            prevAttachedPart = attachedPart;
                        }
                        else//nothing -currently- attached; if there -was- something attached, either jettison or disable the fairing
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

                        if (currentlyEnabled && snapToNode && node!=null)
                        {
                            setFairingBottomY(fairingPos);
                        }
                    }
                }
                else//manual fairing
                {
                    currentlyEnabled = true;
                    enableFairingRender(true);
                }
            }

            needsShieldUpdate = true;
            updateGuiState();
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
        }
        
        private void enableFairing(bool enable)
        {
            fairingEnabled = enable;
            updateFairingStatus();
        }

        private void removeFairingMass()
        {
            if (removedMass) { return; }
            removedMass = true;
            foreach (FairingData fd in fairingParts)
            {
                if (fd.removeMass)
                {
                    part.mass -= fd.fairingJettisonMass;
                }
            }
        }

        private void addFairingMass(Part part)
        {
            if (part == null)
            {
                return;
            }
            foreach (FairingData fd in fairingParts)
            {
                if (fd.removeMass)
                {
                    part.mass += fd.fairingJettisonMass;
                }
            }
        }

        private void enableFairingRender(bool val)
        {
            currentRenderEnabled = val;
            foreach (FairingData fd in fairingParts)
            {
                fd.enableRenders(val);
            }
        }

        private void buildFairing()
        {
            if (HighLogic.LoadedSceneIsEditor)//only enforce editor sizing while in the editor;
            {
                foreach (SSTUNodeFairingData fd in fairingParts)
                {
                    if (fd.canAdjustTop)
                    {
                        fd.topRadius = editorTopRadius + (topRadiusExtra * topRadiusAdjustSize);
                    }
                    if (fd.canAdjustBottom)
                    {
                        fd.bottomRadius = editorBottomRadius + (bottomRadiusExtra * bottomRadiusAdjustSize);
                    }
                }
            }
            foreach (FairingData fd in fairingParts)
            {
                fd.numOfSections = (int)Math.Round(numOfSections);
                fd.createFairing(fairingMaterial);
            }
            updateDragCube();
            needsShieldUpdate = true;
        }

        private void rebuildFairing()
        {
            foreach (FairingData fd in fairingParts)
            {
                fd.destroyFairing();
            }
            buildFairing();
            enableFairingRender(currentRenderEnabled);
        }

        //TODO need to finish drag cube update code for NodeFairing
        private void updateDragCube()
        {
            if (!updateDragCubes) { return; }
            SSTUModInterop.onPartGeometryUpdate(part, updateDragCubes);
        }

        private void updateTextureSet()
        {
            if (textureSets != null && !String.IsNullOrEmpty(currentTextureSet))
            {
                TextureSet t = Array.Find(textureSets, m => m.setName == currentTextureSet);
                if (t != null)
                {
                    MonoBehaviour.print("updating texture set for: " + t.setName);
                    TextureData d = t.textureDatas[0];
                    if (d != null)
                    {
                        fairingMaterial.mainTexture = SSTUUtils.findTexture(d.diffuseTextureName, false);
                        foreach (SSTUNodeFairingData f in fairingParts)
                        {
                            f.setMaterial(fairingMaterial);
                        }
                    }
                    else
                    {
                        MonoBehaviour.print("ERROR: Could not locate texture data for set name: " + t.setName);
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Updates GUI labels and action availability based on current module state (jettisoned, watchedNode attached status, canAdjustRadius, etc)
        /// </summary>
        private void updateGuiState()
        {
            bool topAdjustEnabled = canAdjustTop;
            bool bottomAdjustEnabled = canAdjustBottom;
            if (!currentlyEnabled || fairingJettisoned)//adjustment not possible if faring jettisoned
            {
                topAdjustEnabled = bottomAdjustEnabled = false;
            }

            Events["decreaseTopRadiusEvent"].guiName = fairingName + " Top Rad -";
            Events["increaseTopRadiusEvent"].guiName = fairingName + " Top Rad +";
            Events["decreaseBottomRadiusEvent"].guiName = fairingName + " Bottom Rad -";
            Events["increaseBottomRadiusEvent"].guiName = fairingName + " Bottom Rad +";

            Events["decreaseTopRadiusEvent"].active = topAdjustEnabled;
            Events["increaseTopRadiusEvent"].active = topAdjustEnabled;
            Fields["topRadiusExtra"].guiActiveEditor = topAdjustEnabled;
            Events["decreaseBottomRadiusEvent"].active = bottomAdjustEnabled;
            Events["increaseBottomRadiusEvent"].active = bottomAdjustEnabled;
            Fields["bottomRadiusExtra"].guiActiveEditor = bottomAdjustEnabled;

            Fields["numOfSections"].guiActiveEditor = currentlyEnabled;
            Events["nextTextureEvent"].active = currentlyEnabled && textureSets!=null && textureSets.Length>1;
            Events["nextTextureEvent"].guiName = fairingName + " Next Texture";

            String guiActionName = HighLogic.LoadedSceneIsEditor ? (currentlyEnabled ? "Disable" : "Enable" ) : actionName;
            Events["jettisonEvent"].guiName = guiActionName + " " + fairingName;
            Actions["jettisonAction"].guiName = actionName + " " + fairingName;
            Events["jettisonEvent"].active = Actions["jettisonAction"].active = (HighLogic.LoadedSceneIsEditor && canDisableInEditor) || (currentlyEnabled && canManuallyJettison);

            Fields["shieldedPartCount"].guiActive = Fields["shieldedPartCount"].guiActiveEditor = currentlyEnabled && shieldParts;

            shieldedPartCount = shieldedParts.Count;
        }
        
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

}

