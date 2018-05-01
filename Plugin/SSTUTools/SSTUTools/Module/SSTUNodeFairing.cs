using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System.Text;
using KSPShaderTools;

namespace SSTUTools
{
    /// <summary>
    /// Procedrually created (and adjustable/configurable) replacement for engine fairings, or any other part-attached fairing.
    /// </summary>           
    public class SSTUNodeFairing : PartModule, IRecolorable
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

        [KSPField(isPersistant =true, guiActiveEditor = true, guiName ="Opacity"),
         UI_Toggle(disabledText ="Opaque", enabledText = "Transparent", suppressEditorShipModified = true)]
        public bool editorTransparency = true;

        /// <summary>
        /// Number of sections for the fairing, only enabled for editing if 'canAdjustSections' == true
        /// </summary>
        [KSPField(guiActiveEditor = true, guiName = "Sections", isPersistant = true),
         UI_FloatRange(minValue = 1f, stepIncrement = 1f, maxValue = 6f, suppressEditorShipModified = true)]
        public float numOfSections = 1;

        [KSPField(guiName = "Top Diam", guiActiveEditor = true, isPersistant = true),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float guiTopDiameter = -1f;

        [KSPField(guiName = "Bot. Diam", guiActiveEditor = true, isPersistant = true),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float guiBottomDiameter = -1f;

        [KSPField(guiName = "Texture Set", guiActiveEditor = true, isPersistant = true),
         UI_ChooseOption(suppressEditorShipModified =true)]
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
        /// Has the fairing mesh been created?  This should be kept up-to-date along with if the mesh exists.
        /// The only desync should be on initial load, where this value determines if the fairing should be created at the 'initialization' stage
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool fairingCreated = false;

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

        [KSPField(isPersistant = true)]
        public string customColorData = string.Empty;

        [KSPField(isPersistant = true)]
        public bool initializedColors = false;

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

        private RecoloringHandler recolorHandler;
                
        //the current fairing panels
        private SSTUNodeFairingData[] fairingParts;

        /// <summary>
        /// If not null, will be applied during initialization or late update tick
        /// </summary>
        private FairingUpdateData externalUpdateData = null;

        //private vars set from examining the individual fairing sections; these basically control gui enabled/disabled status
        private bool enableBottomDiameterControls;
        private bool enableTopDiameterControls;

        /// <summary>
        /// Used as part of attach-node detection.  Set to a part when a part is attached to the 'watchedNode'. 
        /// Used to monitor when the part is detached in flight, to trigger jettison/reparenting of the fairing panels.
        /// </summary>
        private Part prevAttachedPart = null;

        /// <summary>
        /// Flipped to true when the fairing should check/update attach node status, due to editor/vessel modified events and/or startup/init.
        /// </summary>
        private bool needsStatusUpdate = false;

        /// <summary>
        /// Flipped to true during internal updates -- determines if the fairing should be rebuilt on the next LateUpdate()
        /// </summary>
        private bool needsRebuilt = false;

        /// <summary>
        /// Flipped to true during internal updates -- determines if the gui data should be updated on the next LateUpdate()
        /// </summary>
        private bool needsGuiUpdate = false;
                
        #endregion

        #region REGION - Gui Interaction Methods

        [KSPAction("Jettison Fairing")]
        public void jettisonAction(KSPActionParam param)
        {
            jettisonFairing();
            updateGuiState();
        }
        
        [KSPEvent(guiName = "Jettison Fairing", guiActive = true, guiActiveEditor = false)]
        public void jettisonEvent()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                this.actionWithSymmetry(m => 
                {
                    m.jettisonFairing();
                    m.updateGuiState();
                });
            }
        }

        [KSPEvent(guiName = "Toggle Fairing", guiActive = false, guiActiveEditor = true)]
        public void toggleFairing()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                fairingEnabled = !fairingEnabled;
                this.actionWithSymmetry(m => 
                {
                    m.fairingEnabled = fairingEnabled;
                    if (m.fairingEnabled)
                    {
                        m.needsStatusUpdate = true;
                    }
                    else
                    {
                        m.destroyFairing();
                    }
                });
            }
        }

        #endregion

        #region REGION - ksp overrides

        //on load, not called properly on 'revertToVAB'
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            if (node.HasValue("customColor1"))
            {
                Color c1 = node.GetColorFromFloatCSV("customColor1");
                Color c2 = node.GetColorFromFloatCSV("customColor2");
                Color c3 = node.GetColorFromFloatCSV("customColor3");
                string colorData = c1.r + "," + c1.g + "," + c1.b + "," + c1.a + ",0;";
                colorData = colorData + c2.r + "," + c2.g + "," + c2.b + "," + c2.a + ",0;";
                colorData = colorData + c3.r + "," + c3.g + "," + c3.b + "," + c3.a + ",0";
                customColorData = colorData;
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);            
            updatePersistentDataString();
            node.SetValue(nameof(persistentDataString), persistentDataString, true);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();

            Fields[nameof(guiTopDiameter)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                this.actionWithSymmetry(m =>
                {
                    m.guiTopDiameter = guiTopDiameter;
                    float radius = m.guiTopDiameter * 0.5f;
                    int len = m.fairingParts.Length;
                    for (int i = 0; i < len; i++)
                    {
                        if (m.fairingParts[i].canAdjustTop && m.fairingParts[i].topRadius != radius)
                        {
                            m.fairingParts[i].topRadius = radius;
                            m.needsRebuilt = m.fairingCreated;
                        }
                    }
                });
            };

            Fields[nameof(guiBottomDiameter)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.guiBottomDiameter = guiBottomDiameter;
                    float radius = m.guiBottomDiameter * 0.5f;
                    int len = m.fairingParts.Length;
                    for (int i = 0; i < len; i++)
                    {
                        if (m.fairingParts[i].canAdjustBottom && m.fairingParts[i].bottomRadius != radius)
                        {
                            m.fairingParts[i].bottomRadius = radius;
                            m.needsRebuilt = m.fairingCreated;
                        }
                    }
                });
            };

            Fields[nameof(numOfSections)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                this.actionWithSymmetry(m => 
                {
                    m.numOfSections = numOfSections;
                    m.needsRebuilt = m.fairingCreated;
                });
            };

            Fields[nameof(editorTransparency)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                this.actionWithSymmetry(m => 
                {
                    m.editorTransparency = editorTransparency;
                    m.updateOpacity();
                });
            };

            Fields[nameof(generateColliders)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                this.actionWithSymmetry(m=> 
                {
                    m.generateColliders = generateColliders;
                    m.updateColliders();
                });
            };

            Fields[nameof(currentTextureSet)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                this.actionWithSymmetry(m => 
                {
                    m.currentTextureSet = currentTextureSet;
                    m.updateTextureSet(!SSTUGameSettings.persistRecolor());
                });
            };

            this.updateUIFloatEditControl(nameof(guiTopDiameter), minTopDiameter, maxTopDiameter, topDiameterIncrement * 2, topDiameterIncrement, topDiameterIncrement * 0.05f, true, guiTopDiameter);
            this.updateUIFloatEditControl(nameof(guiBottomDiameter), minBottomDiameter, maxBottomDiameter, bottomDiameterIncrement * 2, bottomDiameterIncrement, bottomDiameterIncrement * 0.05f, true, guiBottomDiameter);

            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(onVesselModified));
        }
        
        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            GameEvents.onVesselWasModified.Remove(new EventData<Vessel>.OnEvent(onVesselModified));
        }

        public void Start()
        {
            if (fairingParts != null)
            {
                updateOpacity();
            }
        }

        public void onVesselModified(Vessel v)
        {
            if (!HighLogic.LoadedSceneIsFlight) { return; }
            //MonoBehaviour.print("Flight Vessel Modified, checking status");
            //updateFairingStatus();
            needsStatusUpdate = true;
        }

        public void onEditorVesselModified(ShipConstruct ship)
        {
            if (!HighLogic.LoadedSceneIsEditor) { return; }
            //MonoBehaviour.print("Vessel modified, checking status");
            needsStatusUpdate = true;
        }

        private void initialize()
        {
            //MonoBehaviour.print("NodeFairingInit: "+fairingCreated+ " :: " +fairingForceDisabled+ " :: "+fairingJettisoned + " :: " +fairingEnabled);
            if (rendersToRemove != null && rendersToRemove.Length > 0)
            {
                SSTUUtils.removeTransforms(part, SSTUUtils.parseCSV(rendersToRemove));
            }
            loadFairingData(SSTUConfigNodeUtils.parseConfigNode(configNodeData));
            if (externalUpdateData != null)
            {
                updateFromExternalData(externalUpdateData);
            }
            if (fairingCreated || (fairingEnabled && !fairingJettisoned && !fairingForceDisabled && string.IsNullOrEmpty(nodeName)))//previously existed, recreate it, or should exist by default values in the config
            {
                buildFairing();
                if (!string.IsNullOrEmpty(nodeName))
                {
                    AttachNode n = part.FindAttachNode(nodeName);
                    if (n != null && n.attachedPart != null)
                    {
                        prevAttachedPart = n.attachedPart;
                        //MonoBehaviour.print("Setting initial attached part to: " + prevAttachedPart);
                    }
                }
            }
            else if(!fairingJettisoned && !fairingForceDisabled && !string.IsNullOrEmpty(nodeName))//else could potentially be activated by a node...check for activation
            {
                needsStatusUpdate = true;
            }
            updateTextureSet(false);
            needsGuiUpdate = true;
            //MonoBehaviour.print("NodeFairingInit End: " + fairingCreated + " :: " + fairingForceDisabled + " :: " + fairingJettisoned + " :: " + fairingEnabled);
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
            //MonoBehaviour.print("Checking late update1: " + needsStatusUpdate + " :: " + needsRebuilt);
            if (externalUpdateData != null)
            {
                updateFromExternalData(externalUpdateData);
            }
            //MonoBehaviour.print("Checking late update2: " + needsStatusUpdate + " :: " + needsRebuilt);
            if (needsStatusUpdate)
            {
                updateFairingStatus();
            }
            //MonoBehaviour.print("Checking late update3: " + needsStatusUpdate + " :: " + needsRebuilt);
            if (needsRebuilt)
            {
                rebuildFairing();
                updatePersistentDataString();
                SSTUStockInterop.fireEditorUpdate();
                needsGuiUpdate = true;
                needsRebuilt = false;
            }
            if (needsGuiUpdate)
            {
                updateGuiState();
                needsGuiUpdate = false;
            }
            if (HighLogic.LoadedSceneIsEditor && fairingParts != null)
            {
                updateOpacity();
            }
        }

        public string[] getSectionNames()
        {
            return new string[] { fairingName };
        }

        public RecoloringData[] getSectionColors(string name)
        {
            return recolorHandler.getColorData();
        }

        public void setSectionColors(string name, RecoloringData[] colors)
        {
            recolorHandler.setColorData(colors);
            updateTextureSet(false);
        }

        //IRecolorable override
        public TextureSet getSectionTexture(string section)
        {
            return TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
        }

        #endregion

        #region REGION - external interaction methods

        public void updateExternal(FairingUpdateData data)
        {
            externalUpdateData = data;
        }

        private void updateFromExternalData(FairingUpdateData eData)
        {
            //MonoBehaviour.print("Updating fairing from external interaction ");
            if (fairingParts == null)
            {
                MonoBehaviour.print("ERROR: Fairing parts are null for external update");
            }
            foreach (SSTUNodeFairingData data in fairingParts)
            {                
                if (eData.hasTopY && data.canAdjustTop)
                {
                    if (eData.topY != data.topY)
                    {
                        needsRebuilt = fairingCreated;
                    }
                    data.topY = eData.topY;
                    //MonoBehaviour.print("Set top pos: " + eData.topY);
                }
                if (eData.hasBottomY && data.canAdjustBottom)
                {
                    if (eData.bottomY != data.bottomY)
                    {
                        needsRebuilt = fairingCreated;
                    }
                    data.bottomY = eData.bottomY;
                    //MonoBehaviour.print("Set bot pos: " + eData.bottomY);
                }
                if (eData.hasTopRad && data.canAdjustTop)
                {
                    if (eData.topRadius != data.topRadius)
                    {
                        needsRebuilt = fairingCreated;
                    }
                    data.topRadius = eData.topRadius;
                    guiTopDiameter = data.topRadius * 2f;
                    //MonoBehaviour.print("Set top rad: " + eData.topRadius);
                }
                if (eData.hasBottomRad && data.canAdjustBottom)
                {
                    if (eData.bottomRadius != data.bottomRadius)
                    {
                        needsRebuilt = fairingCreated;
                    }
                    data.bottomRadius = eData.bottomRadius;
                    guiBottomDiameter = data.bottomRadius * 2f;
                    //MonoBehaviour.print("Set bot rad: " + eData.bottomRadius);
                }
            }
            if (eData.hasEnable)
            {
                fairingForceDisabled = !eData.enable;
                //MonoBehaviour.print("Set enable: " + eData.enable);
            }
            else
            {
                fairingForceDisabled = false;//default to NOT force disabled
            }
            if (fairingCreated && fairingForceDisabled)
            {
                needsRebuilt = false;
                destroyFairing();
            }
            else
            {
                needsStatusUpdate = true;
            }
            needsGuiUpdate = true;
            externalUpdateData = null;
        }

        #endregion
        
        #region REGION - Initialization methods

        //creates/recreates FairingData instances from data from config node and any persistent node (if applicable)
        private void loadFairingData(ConfigNode node)
        {
            recolorHandler = new RecoloringHandler(Fields[nameof(customColorData)]);

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
                    if (guiTopDiameter < 0)
                    {
                        guiTopDiameter = fairingParts[i].topRadius * 2f;
                    }
                    else
                    {
                        fairingParts[i].topRadius = guiTopDiameter * 0.5f;
                    }
                }
                if (fairingParts[i].canAdjustBottom)
                {
                    enableBottomDiameterControls = true;
                    if (guiBottomDiameter < 0)
                    {
                        guiBottomDiameter = fairingParts[i].bottomRadius * 2f;
                    }
                    else
                    {
                        fairingParts[i].bottomRadius = guiBottomDiameter * 0.5f;
                    }
                }
            }
            //reload fairing data from persistence;
            //it -should- already match the guiTopDiameter/guiBottomDiameter (or else was already corrupted/invalid when saved out).
            if (!String.IsNullOrEmpty(persistentDataString))
            {
                String[] datas = SSTUUtils.parseCSV(persistentDataString, ":");
                int length = datas.Length;
                for (int i = 0; i < length; i++)
                {
                    fairingParts[i].loadPersistence(datas[i]);
                }
            }
            string[] names = node.GetStringValues("textureSet");
            string[] titles = SSTUUtils.getNames(TexturesUnlimitedLoader.getTextureSets(names), m => m.title);
            TextureSet t = TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
            if (t == null)
            {
                currentTextureSet = names[0];
                t = TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
                initializedColors = false;
            }
            if (!initializedColors)
            {
                initializedColors = true;
                recolorHandler.setColorData(t.maskColors);
            }
            this.updateUIChooseOptionControl(nameof(currentTextureSet), names, titles, true, currentTextureSet);
        }

        /// <summary>
        /// Updates GUI labels and action availability based on current module state (jettisoned, watchedNode attached status, canAdjustRadius, etc)
        /// </summary>
        private void updateGuiState()
        {
            bool topAdjustEnabled = enableTopDiameterControls && canAdjustTop;
            bool bottomAdjustEnabled = enableBottomDiameterControls && canAdjustBottom;
            bool currentlyEnabled = fairingCreated;
            if (fairingForceDisabled || !currentlyEnabled || fairingJettisoned)//adjustment not possible if faring jettisoned
            {
                topAdjustEnabled = bottomAdjustEnabled = false;
            }
            Fields[nameof(guiTopDiameter)].guiActiveEditor = topAdjustEnabled;
            Fields[nameof(guiBottomDiameter)].guiActiveEditor = bottomAdjustEnabled;
            Fields[nameof(numOfSections)].guiActiveEditor = currentlyEnabled && canAdjustSections;
            Events[nameof(jettisonEvent)].guiName = actionName + " " + fairingName;
            Actions[nameof(jettisonAction)].guiName = actionName + " " + fairingName;
            Events[nameof(jettisonEvent)].active = HighLogic.LoadedSceneIsFlight && currentlyEnabled && canManuallyJettison && (numOfSections > 1 || String.IsNullOrEmpty(nodeName));
            Actions[nameof(jettisonAction)].active = currentlyEnabled && canManuallyJettison && (numOfSections > 1 || String.IsNullOrEmpty(nodeName));
            Events[nameof(toggleFairing)].guiName = "Toggle " + fairingName;
            Events[nameof(toggleFairing)].active = HighLogic.LoadedSceneIsEditor && !fairingForceDisabled && ((currentlyEnabled && canDisableInEditor) || canSpawnFairing());
            Fields[nameof(editorTransparency)].guiActiveEditor = currentlyEnabled;
            Fields[nameof(generateColliders)].guiActiveEditor = currentlyEnabled;
            Fields[nameof(currentTextureSet)].guiActiveEditor = currentlyEnabled;
        }

        #endregion

        #region REGION - Fairing Update Methods

        /// <summary>
        /// Blanket method to update the attached/visible status of the fairing based on its fairing type, current jettisoned status, and if a part is present on the fairings watched node (if any/applicable)
        /// </summary>
        private void updateFairingStatus()
        {
            //MonoBehaviour.print("Updating fairing status");
            needsStatusUpdate = false;

            if (fairingForceDisabled || fairingJettisoned || !fairingEnabled)
            {
                destroyFairing();
            }
            else if (!String.IsNullOrEmpty(nodeName))//should watch node
            {
                updateStatusForNode();
            }
            else if (!fairingJettisoned && fairingEnabled && !fairingCreated)//else manual fairing that needs to be rebuilt
            {
                needsRebuilt = true;
            }
            needsGuiUpdate = true;
        }

        private void updateStatusForNode()
        {
            AttachNode watchedNode = null;
            Part triggerPart = null;
            float fairingPos = 0;
            if (shouldSpawnFairingForNode(out watchedNode, out triggerPart, out fairingPos))
            {
                //MonoBehaviour.print("Triggering (re)build from node attachment");
                needsRebuilt = needsRebuilt || !fairingCreated;
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
                prevAttachedPart = triggerPart;
            }
            else if (prevAttachedPart != null)
            {
                //MonoBehaviour.print("Triggering jettison/disable from node detachment: "+prevAttachedPart);
                if (HighLogic.LoadedSceneIsFlight)
                {
                    if (canAutoJettison)
                    {
                        jettisonFairing();
                    }
                    else
                    {
                        //NOOP
                    }
                }
                else
                {
                    destroyFairing();
                }
            }
            else
            {
                //MonoBehaviour.print("Destroying fairing due to no prev part / no node attachment");
                destroyFairing();
            }
        }
        
        /// <summary>
        /// Reparents the fairing panel parts to the input part; should only be used on jettison of the fairings when they stay attached to the part below
        /// </summary>
        /// <param name="newParent"></param>
        private void reparentFairing(Part newParent)
        {
            int len = fairingParts.Length;
            for (int i = 0; i < len; i++)
            {
                fairingParts[i].fairingBase.reparentFairing(newParent.transform.FindRecursive("model"));
            }
        }
        
        private void jettisonFairing()
        {
            //MonoBehaviour.print("Jettisoning fairing - prev: "+prevAttachedPart);
            if (numOfSections == 1 && prevAttachedPart != null)
            {
                //MonoBehaviour.print("Reparenting fairing to: " + prevAttachedPart);
                reparentFairing(prevAttachedPart);
                SSTUModInterop.onPartGeometryUpdate(prevAttachedPart, true);//update other parts highlight renderers, to add the new fairing bits to it.
            }
            else
            {
                //MonoBehaviour.print("Jettisoning Panels: " + fairingParts.Length);
                foreach (SSTUNodeFairingData data in fairingParts)
                {
                    data.jettisonPanels(part);
                }
            }
            prevAttachedPart = null;
            fairingJettisoned = true;
            fairingEnabled = false;
            destroyFairing();//cleanup any leftover bits in fairing containers
        }

        private void enableFairing(bool enable)
        {
            fairingEnabled = enable;
            if (fairingEnabled)
            {
                needsRebuilt = fairingCreated;
            }
            else
            {
                destroyFairing();
            }
        }

        private void buildFairing()
        {
            //MonoBehaviour.print("Building Fairing "+guiTopDiameter+" : "+guiBottomDiameter);
            needsRebuilt = false;
            fairingCreated = true;
            int len = fairingParts.Length;            
            if (HighLogic.LoadedSceneIsEditor)//only enforce editor sizing while in the editor;
            {
                for (int i = 0; i < len; i++)
                {
                    //MonoBehaviour.print("pt t/b " + fairingParts[i].topRadius + " : " + fairingParts[i].bottomRadius);
                    if (fairingParts[i].canAdjustTop && canAdjustTop)
                    {
                        fairingParts[i].topRadius = guiTopDiameter * 0.5f;
                    }
                    if (fairingParts[i].canAdjustBottom && canAdjustBottom)
                    {
                        fairingParts[i].bottomRadius = guiBottomDiameter * 0.5f;
                    }
                    //MonoBehaviour.print("pt t/b 2" + fairingParts[i].topRadius + " : " + fairingParts[i].bottomRadius);
                }
            }
            for (int i = 0; i < len; i++)
            {
                fairingParts[i].generateColliders = this.generateColliders;
                fairingParts[i].facesPerCollider = 1;
                fairingParts[i].numOfSections = (int)Math.Round(numOfSections);
                fairingParts[i].createFairing(editorTransparency ? 0.25f : 1f);
            }
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }
        
        private void rebuildFairing()
        {
            destroyFairing();
            buildFairing();
            updateTextureSet(false);
            updateColliders();
        }

        public void destroyFairing()
        {
            //MonoBehaviour.print("Destroying Fairing");
            int len = fairingParts.Length;
            for (int i = 0; i < len; i++)
            {
                fairingParts[i].destroyFairing();
            }
            if (fairingCreated)
            {
                SSTUModInterop.onPartGeometryUpdate(part, true);
            }
            fairingCreated = false;
        }
        
        private void updateTextureSet(bool useDefaults)
        {
            TextureSet s = TexturesUnlimitedLoader.getTextureSet(currentTextureSet);
            RecoloringData[] colors = useDefaults ? s.maskColors : getSectionColors(string.Empty);
            int len = fairingParts.Length;
            for (int i = 0; i < len; i++)
            {
                fairingParts[i].fairingBase.enableTextureSet(currentTextureSet, colors);
            }
            if (useDefaults)
            {
                recolorHandler.setColorData(colors);
            }
            updateOpacity();
            SSTUModInterop.onPartTextureUpdated(part);
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
                if (fairingCreated)
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

        private void updateColliders()
        {
            int len = fairingParts.Length;
            for (int i = 0; i < len; i++)
            {
                if (fairingParts[i].generateColliders != generateColliders)
                {
                    fairingParts[i].generateColliders = generateColliders;
                    needsRebuilt = true;
                }
            }
        }

        private void updateOpacity()
        {
            float opacity = editorTransparency && HighLogic.LoadedSceneIsEditor ? 0.25f : 1f;
            foreach (FairingData fd in fairingParts) { fd.fairingBase.setOpacity(opacity); }
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

