using System;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
namespace SSTUTools
{
    /// <summary>
    /// Procedrually created (and adjustable/configurable) replacement for engine fairings, or any other part-attached fairing.
    /// </summary>           
    public class SSTUNodeFairing : PartModule, IAirstreamShield
    {

        #region KSP Part Module config vars for entire fairing

        [KSPField(isPersistant = true, guiName = "Fairing Type", guiActiveEditor = true)]
        public string fairingType = FairingType.NODE_ATTACHED.ToString();

        /// <summary>
        /// Is the fairing -currently- enabled? this may change in VAB from user adding/removing parts, or in-flight from disconnect.  this will be force-set to false if jettisioned is true.  
        /// This var basically just tracks the visible status at any moment
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool fairingEnabled = false;

        /// <summary>
        /// Has the fairing been jettisoned?  It may not be recovered once jettisoned; this flag will keep the node-watching from respawning the fairing
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool jettisoned = false;

        [KSPField]
        public string diffuseTextureName = "UNKNOWN";

        [KSPField]
        public string normalTextureName = "UNKNOWN";

        //watch this node, if a part is attached, spawn the fairing
        //only enabled if 'watchNode==true'
        [KSPField]
        public string nodeName = "bottom";

        //CSV list of transform names to disable renders on (to override stock ModuleJettison mechanics) - should also MM patch remove the ModuleJettsion from the part...
        [KSPField]
        public string rendersToRemove = string.Empty;

        [KSPField]
        public string fairingName = "Fairing";

        //if manual deploy is enabled, this will be the button/action group text
        [KSPField]
        public string actionName = "Jettison";

        /// <summary>
        /// Can user adjust the enabled/disabled and fairing type?
        /// </summary>
        [KSPField]
        public bool canAdjustToggles = false;

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
        public float minTopRadius = 0.625f;

        /// <summary>
        /// Maximum bottom radius (by whole increment; adjust slider will allow this + one radius increment)
        /// </summary>
        [KSPField]
        public float maxBottomRadius = 5;

        /// <summary>
        /// Minimum bottom radius
        /// </summary>
        [KSPField]
        public float minBottomRadius = 0.625f;

        /// <summary>
        /// UI field to tell user how many parts are shielded by fairing.  Only enabled on those fairings that have shielding enabled
        /// </summary>
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Shielded Part Count")]
        public int shieldedPartCount = 0;

        #endregion

        #region fairing airstream shield vars
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

        /// <summary>
        /// Persistent tracking of if the fairing has removed its 'jettision mass' from the parent part (and added it to its attached part, if a node-attached fairing)
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool removedMass = false;
        #endregion

        #region working vars, not user editable
        ///// <summary>
        ///// Basic persistent top radius, to persist the editor fields
        ///// TODO deprecate, replace with the per-fairing values.  But -how- to replace this functionality?
        ///// Seems like I should just leave this field in-place and just multi-update -all- fairings that have topRadius defined.
        ///// during init, any fairing with topRadiusAdjust will all be set to the same common radius; all fairings with radius adjust will be adjusted at the same time.
        ///// </summary>
        //[KSPField(isPersistant = true)]
        //public float persistentTopRadius;

        ////used to persist user-edited top/bottom radius data
        //[KSPField(isPersistant = true)]
        //public float persistentBottomRadius;

        //radius adjustment fields, mostly used in editor
        //these values are restored during the OnStart operation, and only used in the editor
        //the 'live' values for the fairing are stored persistently and used directly to update the
        //fairing physical attributes.
        //the 'live' values will be set from these values for further operations in the editor		
        private float editorTopRadius = 0;
        private float editorBottomRadius = 0;
        private float lastTopExtra = 0;
        private float lastBottomExtra = 0;

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

        //The current user-selected or config designated fairing type
        private FairingType typeEnum = FairingType.NODE_ATTACHED;

        //the part attached to this fairings watched node, if applicable.  null either means no part, or not applicable for this fairing.
        private Part attachedPart = null;

        //the current fairing panels
        private SSTUNodeFairingData[] fairingParts;

        private bool canAdjustBottomRadius;
        private bool canAdjustTopRadius;

        //quick reference to the currently watched attach node, if any
        private AttachNode watchedNode;

        //material used for procedural fairing, created from the texture references above
        private Material fairingMaterial;

        //list of shielded parts
        private List<Part> shieldedParts = new List<Part>();

        //only marked public so that it can be serialized from prefab into instance parts
        [Persistent]
        public String configNodeString = String.Empty;

        //this one is quite hacky; storing ConfigNode data in the string, because the -fields- load fine on revert-to-vab (and everywhere), but the config-node data is not present in all situations
        [KSPField(isPersistant = true)]
        public String persistentDataString = String.Empty;

        #endregion

        #region gui actions

        [KSPAction("Jettison Fairing")]
        public void jettisonAction(KSPActionParam param)
        {
            onJettisonEvent();
        }

        [KSPEvent(name = "jettisonEvent", guiName = "Jettison Fairing", guiActive = true, guiActiveEditor = true)]
        public void jettisonEvent()
        {
            onJettisonEvent();
        }

        [KSPEvent(name = "increaseTopRadiusEvent", guiName = "Top Rad +", guiActiveEditor = true)]
        public void increaseTopRadiusEvent()
        {
            if (canAdjustTopRadius && editorTopRadius < maxTopRadius)
            {
                editorTopRadius += topRadiusAdjustSize;
                if (editorTopRadius > maxTopRadius) { editorTopRadius = maxTopRadius; }
                rebuildFairing();
            }
        }

        [KSPEvent(name = "decreaseTopRadiusEvent", guiName = "Top Rad -", guiActiveEditor = true)]
        public void decreaseTopRadiusEvent()
        {
            if (canAdjustTopRadius && editorTopRadius > minTopRadius)
            {
                editorTopRadius -= topRadiusAdjustSize;
                if (editorTopRadius < minTopRadius) { editorTopRadius = minTopRadius; }
                rebuildFairing();
            }
        }

        [KSPEvent(name = "increaseBottomRadiusEvent", guiName = "Bottom Rad +", guiActiveEditor = true)]
        public void increaseBottomRadiusEvent()
        {
            if (canAdjustBottomRadius && editorBottomRadius < maxBottomRadius)
            {
                editorBottomRadius += bottomRadiusAdjustSize;
                if (editorBottomRadius > maxBottomRadius) { editorBottomRadius = maxBottomRadius; }
                rebuildFairing();
            }
        }

        [KSPEvent(name = "decreaseBottomRadiusEvent", guiName = "Bottom Rad -", guiActiveEditor = true)]
        public void decreaseBottomRadiusEvent()
        {
            if (canAdjustBottomRadius && editorBottomRadius > minBottomRadius)
            {
                editorBottomRadius -= bottomRadiusAdjustSize;
                if (editorBottomRadius < minBottomRadius) { editorBottomRadius = minBottomRadius; }
                rebuildFairing();
            }
        }

        [KSPEvent(name = "changeTypeEvent", guiName = "Next Fairing Type", guiActiveEditor = true)]
        public void changeTypeEvent()
        {
            switch (typeEnum)
            {
                case FairingType.NODE_ATTACHED:
                    {
                        typeEnum = FairingType.NODE_DESPAWN;
                        break;
                    }
                case FairingType.NODE_DESPAWN:
                    {
                        typeEnum = FairingType.NODE_JETTISON;
                        break;
                    }
                case FairingType.NODE_JETTISON:
                    {
                        typeEnum = FairingType.NODE_ATTACHED;
                        break;
                    }
                    //NOOP FOR MANUAL_JETTISON OR NODE_STATIC
            }
            fairingType = typeEnum.ToString();//update enum state; it is only ever altered through this method, so this -should- keep things synched
            updateGuiState();
        }

        #endregion


        #region ksp overrides

        //on load, not called properly on 'revertToVAB'
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            //if prefab, load persistent config data into config node string
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
            {
                configNodeString = node.ToString();
            }
            //load the material...uhh...for use in prefab?? no clue why it is loaded here...probably some reason
            if (fairingMaterial == null)
            {
                loadMaterial();
            }

            loadFairingType();
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
            print("saved persistent data: " + persistentDataString);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            //remove any stock transforms for engine-fairing overrides
            if (rendersToRemove != null && rendersToRemove.Length > 0)
            {
                removeTransforms();
            }

            //load fairing material
            if (fairingMaterial == null)
            {
                loadMaterial();
            }

            //reload any previously persistent fairing-type data, e.g. from config for in-editor new parts (cannot rely on OnLoad for new parts)
            loadFairingType();

            //load FairingData instances from config values (persistent data nodes also merged in)
            loadFairingData(SSTUNodeUtils.parseConfigNode(configNodeString));

            //restore the editor field values from the loaded fairing (radius adjust stuff)
            if (HighLogic.LoadedSceneIsEditor)
            {
                restoreEditorFields();
            }

            //construct fairing from loaded data
            buildFairing();

            //update the visible and attachment status for the fairing
            updateFairingStatus();

            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(onVesselModified));
            GameEvents.onVesselGoOffRails.Add(new EventData<Vessel>.OnEvent(onVesselUnpack));
            GameEvents.onVesselGoOnRails.Add(new EventData<Vessel>.OnEvent(onVesselPack));
            GameEvents.onPartDie.Add(new EventData<Part>.OnEvent(onPartDestroyed));
        }

        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            GameEvents.onVesselWasModified.Remove(new EventData<Vessel>.OnEvent(onVesselModified));
            GameEvents.onVesselGoOffRails.Remove(new EventData<Vessel>.OnEvent(onVesselUnpack));
            GameEvents.onVesselGoOnRails.Remove(new EventData<Vessel>.OnEvent(onVesselPack));
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
                rebuildFairing();
            }
            if (canAdjustBottomRadius && bottomRadiusExtra != lastBottomExtra)
            {
                lastBottomExtra = bottomRadiusExtra;
                rebuildFairing();
            }
            updateFairingStatus();
        }

        public void onVesselUnpack(Vessel v)
        {
            updateShieldStatus();
            updateGuiState();
        }

        public void onVesselPack(Vessel v)
        {
            clearShieldedParts();
            updateGuiState();
        }

        //TODO - how to best handle this?
        public void onPartDestroyed(Part p)
        {
            clearShieldedParts();
            if (p != part)
            {
                updateShieldStatus();
            }
            updateGuiState();
        }

        #endregion

        #region external interaction methods

        public void setFairingTopY(String fairingName, float newValue)
        {
            FairingData data = findFairingData(fairingName);
            if (data != null)
            {
                updateFairingPosition(data, newValue, data.bottomY);
            }
        }

        public void setFairingBottomY(String fairingName, float newValue)
        {
            FairingData data = findFairingData(fairingName);
            if (data != null)
            {
                updateFairingPosition(data, data.topY, newValue);
            }
        }

        public void updateFairingPosition(String fairingName, float newTop, float newBottom)
        {
            FairingData data = findFairingData(fairingName);
            if (data != null)
            {
                updateFairingPosition(data, newTop, newBottom);
            }
        }

        /// <summary>
        /// Only works in editor.  Intended to be used for other PartModules to interface with, so that they can dynamically adjust if the fairing is present or not.
        /// </summary>
        /// <param name="enable"></param>                      
        public void enableFairing(bool enable)
        {
            if (!HighLogic.LoadedSceneIsEditor || fairingParts == null) { return; }
            jettisoned = !enable;
            updateFairingStatus();
        }

        private FairingData findFairingData(String name)
        {
            if (fairingParts == null) { return null; }
            foreach (FairingData data in fairingParts)
            {
                if (String.Equals(data.fairingName, name))
                {
                    return data;
                }
            }
            return null;
        }

        private void updateFairingPosition(FairingData data, float topY, float bottomY)
        {
            data.topY = topY;
            data.bottomY = bottomY;
            rebuildFairing();
            updateDragCube();
            updateShieldStatus();
        }

        #endregion

        #region fairingJettison methods

        /// <summary>
        /// only called from UI, which is only available for manual-deployed fairing type
        /// </summary>
        private void onJettisonEvent()
        {
            if (jettisoned)
            {
                print("Cannot jettison already jettisoned fairing");
                return;
            }
            if (!fairingEnabled)
            {
                print("Cannot jettison disabled fairing");
                return;
            }
            bool jettison = HighLogic.LoadedSceneIsFlight;
            jettisonFairing(jettison, jettison, true, false);
        }

        private void jettisonFairing(bool jettisonPanels, bool render, bool removeMass, bool addMass)
        {
            if (jettisonPanels && HighLogic.LoadedSceneIsFlight)
            {
                foreach (FairingData fd in fairingParts)
                {
                    fd.jettisonPanels(part);
                }
            }
            jettisoned = true;
            fairingEnabled = false;
            enableFairingRender(render);
            if (removeMass)
            {
                removeFairingMass();
            }
            if (addMass && attachedPart != null)
            {
                addFairingMass(attachedPart);
            }
            updateDragCube();
            updateShieldStatus();
            updateGuiState();
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

        #endregion

        //TODO updateDragCube
        #region private utility methods

        //restores the values to the editor size-adjust fields from the loaded values from the fairing
        private void restoreEditorFields()
        {
            float topRadius = 0;
            float bottomRadius = 0;
            foreach (FairingData data in fairingParts)
            {
                if (data.canAdjustTop && data.topRadius > topRadius) { topRadius = data.topRadius; }
                if (data.canAdjustBottom && data.bottomRadius > bottomRadius) { bottomRadius = data.bottomRadius; }
            }
            foreach (FairingData data in fairingParts)//loop a second time to fix any adjustable fairings that were below the adjustment size for whatever reason
            {
                if (data.canAdjustTop && data.topRadius < topRadius) { data.topRadius =  topRadius; }
                if (data.canAdjustBottom && data.bottomRadius < bottomRadius) { data.bottomRadius = bottomRadius; }
            }
            float div, whole, extra;
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

        private void loadFairingType()
        {
            try
            {
                typeEnum = (FairingType)Enum.Parse(typeof(FairingType), fairingType);
            }
            catch (Exception e)
            {
                print(e.Message);
                fairingType = FairingType.NODE_ATTACHED.ToString();
                typeEnum = FairingType.NODE_ATTACHED;
            }
        }

        //creates/recreates FairingData instances from data from config node and any persistent node (if applicable)
        private void loadFairingData(ConfigNode node)
        {
            ConfigNode[] fairingNodes = node.GetNodes("FAIRING");
            fairingParts = new SSTUNodeFairingData[fairingNodes.Length];
            for (int i = 0; i < fairingNodes.Length; i++)
            {
                fairingParts[i] = new SSTUNodeFairingData();
                fairingParts[i].load(fairingNodes[i]);
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
                String[] datas = SSTUUtils.parseCSV(persistentDataString, ";");
                int length = datas.Length;
                for (int i = 0; i < length; i++)
                {
                    fairingParts[i].loadPersistence(datas[i]);
                }
            }
        }
        
        /// <summary>
        /// Blanket method to update the attached/visible status of the fairing based on its fairing type, current jettisoned status, and if a part is present on the fairings watched node (if any/applicable)
        /// </summary>
        private void updateFairingStatus()
        {
            //if jettisoned, fairing is never enabled; jettisoned means -fucking gone-, no coming back from that one
            if (jettisoned)
            {
                fairingEnabled = false;
            }
            else if (typeEnum == FairingType.MANUAL_JETTISON)
            {
                fairingEnabled = !jettisoned;
            }
            else if (typeEnum == FairingType.NODE_DESPAWN || typeEnum == FairingType.NODE_JETTISON || typeEnum == FairingType.NODE_ATTACHED)
            {
                watchedNode = part.findAttachNode(nodeName);
                if (watchedNode == null || watchedNode.attachedPart == null)//fairing should be disabled; there is no lower-part to trigger it being present
                {
                    if (HighLogic.LoadedSceneIsEditor)
                    {
                        fairingEnabled = false;
                    }
                    else if (HighLogic.LoadedSceneIsFlight)
                    {
                        if (typeEnum == FairingType.NODE_JETTISON)//should jettison and float freely
                        {
                            jettisonFairing(true, true, true, false);
                        }
                        else if (typeEnum == FairingType.NODE_DESPAWN)//should immediately despawn
                        {
                            jettisonFairing(false, false, true, false);
                        }
                        else if (typeEnum == FairingType.NODE_ATTACHED)//should remain attached to other part, transfer mass to attached part
                        {
                            jettisonFairing(false, true, true, true);
                        }
                    }
                }
                else//else has attached node and part, mark as fairingEnabled, and update parentage (if not in editor)
                {
                    fairingEnabled = true;
                    if (HighLogic.LoadedSceneIsFlight)//only reparent the fairing panels if in active flight
                    {
                        attachedPart = null;
                        attachedPart = watchedNode.attachedPart;
                        foreach (FairingData fd in fairingParts)
                        {
                            fd.theFairing.root.transform.parent = watchedNode.attachedPart.transform;
                        }
                    }
                }
            }
            else if (typeEnum == FairingType.NODE_STATIC)
            {
                watchedNode = part.findAttachNode(nodeName);
                attachedPart = null;
                if (HighLogic.LoadedSceneIsEditor)
                {
                    fairingEnabled = watchedNode != null && watchedNode.attachedPart != null;
                }
            }
            enableFairingRender(fairingEnabled);
            updateShieldStatus();
            updateGuiState();
        }

        /// <summary>
        /// Updates GUI labels and action availability based on current module state (jettisoned, watchedNode attached status, canAdjustRadius, etc)
        /// </summary>
        private void updateGuiState()
        {
            bool topAdjustEnabled = canAdjustTopRadius;
            bool bottomAdjustEnabled = canAdjustBottomRadius;
            if (jettisoned)//adjustment not possible if faring jettisoned
            {
                topAdjustEnabled = bottomAdjustEnabled = false;
            }
            else if (typeEnum != FairingType.MANUAL_JETTISON)//update status for node-attached fairing....
            {
                if (watchedNode == null || watchedNode.attachedPart == null)//ajustment not possible when fairing not visible (node-watching fairing, with nothing attached to node)
                {
                    topAdjustEnabled = bottomAdjustEnabled = false;
                }
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

            Events["changeTypeEvent"].guiName = fairingName + " Type";

            Events["changeTypeEvent"].guiActiveEditor = fairingEnabled && canAdjustToggles && !jettisoned;

            Events["jettisonEvent"].guiName = Actions["jettisonAction"].guiName = actionName + " " + fairingName;

            Events["jettisonEvent"].active = Actions["jettisonAction"].active = fairingEnabled && !jettisoned && typeEnum == FairingType.MANUAL_JETTISON;

            Fields["shieldedPartCount"].guiActive = Fields["shieldedPartCount"].guiActiveEditor = fairingEnabled && shieldParts && !jettisoned;
            Fields["fairingType"].guiActiveEditor = canAdjustToggles;

            shieldedPartCount = shieldedParts.Count;
        }

        private void enableFairingRender(bool val)
        {
            foreach (FairingData fd in fairingParts)
            {
                fd.enableRenders(val);
            }
        }

        private void rebuildFairing()
        {
            foreach (FairingData fd in fairingParts)
            {
                fd.destroyFairing();
            }
            buildFairing();
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
                fd.createFairing(part, fairingMaterial);
            }
            updateDragCube();
            updateShieldStatus();
        }

        private void loadMaterial()
        {
            if (fairingMaterial != null)
            {
                Material.Destroy(fairingMaterial);
                fairingMaterial = null;
            }
            fairingMaterial = SSTUUtils.loadMaterial(diffuseTextureName, normalTextureName);
        }

        //TODO need to finish drag cube update code for NodeFairing
        private void updateDragCube()
        {
            if (!part.DragCubes.Procedural)
            {                
                if (part.DragCubes.Cubes.Count > 1)
                {
                    //has multiple cubes; no clue...
                    //ask modules to re-render their cubes?
                }
                else if (part.DragCubes.Cubes.Count == 1)//has only one cube, update it!
                {
                    DragCube c = part.DragCubes.Cubes[0];
                    String name = c.Name;
                    DragCube c2 = DragCubeSystem.Instance.RenderProceduralDragCube(part);
                    c2.Name = name;
                    part.DragCubes.ClearCubes();
                    part.DragCubes.ResetCubeWeights();
                    part.DragCubes.Cubes.Add(c2);
                    part.DragCubes.SetCubeWeight(name, 1.0f);//set the cube to full weight...
                }
            }
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

        
        #region KSP AirstreamShield update methods

        //IAirstreamShield override
        public bool ClosedAndLocked() { return fairingEnabled && !jettisoned; }

        //IAirstreamShield override
        public Vessel GetVessel() { return part.vessel; }

        //IAirstreamShield override
        public Part GetPart() { return part; }

        private void updateShieldStatus()
        {
            clearShieldedParts();
            if (shieldParts && !jettisoned && fairingEnabled)
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
                print("SSTUNodeFairing is shielding: " + shieldedParts[i].name);
            }
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

