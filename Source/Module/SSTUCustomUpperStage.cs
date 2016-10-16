using System;
using System.Collections;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUCustomUpperStage : PartModule, IPartCostModifier, IPartMassModifier
    {

        #region ----------------- REGION - Standard KSP-accessible config fields -----------------
        /// <summary>
        /// quick/dirty/easy flag to determine if should even attempt to load/manipulate split-tank elements
        /// </summary>
        [KSPField]
        public bool splitTank = true;

        /// <summary>
        /// How much is the 'height' incremented for every 'large' tank height step? - this value is further scaled based on the currently selected tank diameter
        /// </summary>
        [KSPField]
        public float tankHeightIncrement = 0.5f;

        /// <summary>
        /// How much is the 'diameter' incremented for every 'large' tank diameter step? - this value is -not- scaled, and used as-is.
        /// </summary>
        [KSPField]
        public float tankDiameterIncrement = 1.25f;

        /// <summary>
        /// Minimum tank height that can be set through VAB; this is just the adjustable tank portion and does not include any cap height
        /// </summary>
        [KSPField]
        public float minTankHeight = 1;

        /// <summary>
        /// Maximum tank height that may be set through the VAB; this is just the adjustable portion and does not include any cap height
        /// </summary>
        [KSPField]
        public float maxTankHeight = 5;

        /// <summary>
        /// Minimum tank diameter that may be set through the VAB
        /// </summary>
        [KSPField]
        public float minTankDiameter = 1.25f;

        /// <summary>
        /// Maximum tank diameter that may be set through the VAB
        /// </summary>
        [KSPField]
        public float maxTankDiameter = 10;

        /// <summary>
        /// Determines the diameter of the upper stage part.  Used to re-scale the input model to this diameter
        /// </summary>
        [KSPField]
        public float defaultTankDiameter = 2.5f;

        /// <summary>
        /// Default 'height' of the adjustable tank portion.
        /// </summary>
        [KSPField]
        public float defaultTankHeight = 1f;

        /// <summary>
        /// The default mount model to use when the upper stage is first initialized/pulled out of the editor; can be further adjusted by user in editor
        /// Mandatory field, -must- be populated by a valid mount option for this part.
        /// </summary>
        [KSPField]
        public String defaultMount = String.Empty;

        /// <summary>
        /// The default name entry for the intertank structure definition
        /// </summary>
        [KSPField]
        public String defaultIntertank = String.Empty;

        /// <summary>
        /// The thrust output of the RCS system at the default tank diameter; scaled using square-scaling methods to determine 
        /// </summary>
        [KSPField]
        public float defaultRcsThrust = 1;

        [KSPField]
        public int topFairingIndex = 0;

        [KSPField]
        public int lowerFairingIndex = 1;

        [KSPField]
        public String interstageNodeName = "interstage";

        [KSPField]
        public String baseTransformName = "SSTUCustomUpperStageBaseTransform";

        [KSPField]
        public String rcsTransformName = "SSTUMUSRCS";

        [KSPField]
        public bool subtractMass = false;

        [KSPField]
        public bool subtractCost = false;

        #endregion

        #region ----------------- REGION - GUI visible fields and fine tune adjustment contols - do not edit through config -----------------
        [KSPField(guiName = "Tank Height", guiActive = false, guiActiveEditor = true)]
        public float guiTankHeight;

        [KSPField(guiName = "Total Height", guiActive = false, guiActiveEditor = true)]
        public float guiTotalHeight;

        [KSPField(guiName = "Tank Mass", guiActive = false, guiActiveEditor = true)]
        public float guiDryMass;

        [KSPField(guiName = "RCS Thrust", guiActive = false, guiActiveEditor = true)]
        public float guiRcsThrust;
        #endregion

        #region ----------------- REGION - persistent data fields - do not edit through config ----------------- 
        //  --  NOTE  -- 
        // Below here are non-config-editable fields, used for persistance of the current settings; do not attempt to alter/adjust these through config, or things will -not- go as you expect

        /// <summary>
        /// Current absolute tank diameter (of the upper tank for split-tank, or of the full tank for common-bulkhead types)
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor =true, guiName ="Diameter"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentTankDiameter = -1f;

        /// <summary>
        /// Current absolute (post-scale) height of the adjustable tank portion
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor =true, guiName = "Height"),
         UI_FloatEdit(sigFigs =3, suppressEditorShipModified = true)]
        public float currentTankHeight = 0f;

        /// <summary>
        /// The currently selected/enabled intertank option (if any).
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor =true, guiName ="Intertank"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentIntertank = String.Empty;

        /// <summary>
        /// The currently selected/enabled mount option.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Mount"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentMount = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Dome Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentDomeTextureSet = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Upper Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentUpperTextureSet = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Intertank Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentIntertankTextureSet = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Lower Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentLowerTextureSet = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Mount Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentMountTexture = String.Empty;

        /// <summary>
        /// The current RCS thrust; this value will be 'set' into the RCS module (if found/present)
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentRcsThrust = 0f;

        /// <summary>
        /// Used solely to track if resources have been initialized, as this should only happen once on first part creation (regardless of if it is created in flight or in the editor);
        /// Unsure of any cleaner way to track a simple boolean value across the lifetime of a part, seems like the part-persistence data is probably it...
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        [KSPField(isPersistant = true)]
        public bool initializedFairing = false;

        [Persistent]
        public string configNodeData = string.Empty;

        #endregion

        #region ----------------- REGION - Private working value fields ----------------- 

        //cached values for editor updating of height/diameter        
        private string prevMount;
        private string prevIntertank;
        private float prevHeight;
        private float prevTankDiameter;
        
        //geometry related values, mostly for updating of fairings        
        private float partTopY;
        private float topFairingBottomY;
        private float partBottomY;
        private float bottomFairingTopY;

        //cached values for updating of part volume and mass
        private float totalTankVolume = 0;
        private float moduleMass = 0;
        private float moduleCost = 0;
        private float rcsThrust = 0;

        //Private-instance-local fields for tracking the current/loaded config; basically parsed from configNodeData when config is loaded
        //upper, rcs, and mount must be present for every part
        private SingleModelData upperDomeModule;
        private SingleModelData upperTopCapModule;
        private SingleModelData upperModule;
        private SingleModelData upperBottomCapModule;
        //lower and intertank need only be present for split-tank type parts
        private SingleModelData[] intertankModules;
        private SingleModelData currentIntertankModule;
        private SingleModelData lowerTopCapModule;
        private SingleModelData lowerModule;
        private SingleModelData lowerBottomCapModule;        
        //mount and RCS are present for every part
        private SSTUCustomUpperStageRCS rcsModule;
        private SingleModelData[] mountModules;
        private SingleModelData currentMountModule;

        private bool initialized = false;
        #endregion

        #region ----------------- REGION - GUI Interaction methods -----------------

        public void onDomeTextureUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentDomeTextureSet)
            {
                setTankDomeTextureFromEditor(currentDomeTextureSet, true);
            }
        }

        public void onUpperTextureUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentUpperTextureSet)
            {
                setTankUpperTextureFromEditor(currentUpperTextureSet, true);
            }
        }

        public void onIntertankTextureUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentIntertankTextureSet)
            {
                setIntertankTextureFromEditor(currentIntertankTextureSet, true);
            }
        }

        public void onLowerTextureUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentLowerTextureSet)
            {
                setTankLowerTextureFromEditor(currentLowerTextureSet, true);
            }
        }

        public void onMountTextureUpdated(BaseField field, object obj)
        {
            if ((string)obj != currentMountTexture)
            {
                setMountTextureFromEditor(currentMountTexture, true);
            }
        }

        public void onDiameterUpdated(BaseField field, object obj)
        {
            if (currentTankDiameter != prevTankDiameter)
            {
                setTankDiameterFromEditor(currentTankDiameter, true);
                SSTUModInterop.onPartGeometryUpdate(part, true);
                SSTUStockInterop.fireEditorUpdate();
            }            
        }

        public void onHeightUpdated(BaseField field, object obj)
        {
            if (currentTankHeight != prevHeight)
            {
                updateTankHeightFromEditor(currentTankHeight, true);
                SSTUModInterop.onPartGeometryUpdate(part, true);
                SSTUStockInterop.fireEditorUpdate();
            }
        }

        public void onMountUpdated(BaseField field, object obj)
        {
            updateMountModelFromEditor(currentMount, true);
            SSTUModInterop.onPartGeometryUpdate(part, true);
            SSTUStockInterop.fireEditorUpdate();
        }

        public void onIntertankUpdated(BaseField field, object obj)
        {
            updateIntertankModelFromEditor(currentIntertank, true);
            SSTUModInterop.onPartGeometryUpdate(part, true);
            SSTUStockInterop.fireEditorUpdate();
        }

        /// <summary>
        /// Editor callback method for when tank height changes.  Updates model positions, attach node/attached part positions, 
        /// </summary>
        private void updateTankHeightFromEditor(float newHeight, bool updateSymmetry)
        {
            currentTankHeight = newHeight;
            updateEditorFields();
            updateModules(true);
            updateModels();
            updateTankStats();
            updateContainerVolume();
            updateGuiState();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().updateTankHeightFromEditor(newHeight, false);
                }
            }
        }

        /// <summary>
        /// Updates the current tank diameter from user input.  Subsequently updates internal and GUI variables, and redoes the setup for the part resources
        /// </summary>
        private void setTankDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {     
            currentTankDiameter = newDiameter;
            updateEditorFields();
            updateModules(true);
            updateModels();
            updateTankStats();
            updateContainerVolume();
            updateGuiState();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().setTankDiameterFromEditor(newDiameter, false);
                }
            }
        }

        /// <summary>
        /// Updates the selected mount model from user input
        /// </summary>
        /// <param name="nextDef"></param>
        private void updateMountModelFromEditor(String newMount, bool updateSymmetry)
        {
            removeCurrentModel(currentMountModule);
            currentMountModule = Array.Find(mountModules, m => m.name == newMount);
            currentMount = newMount;
            setupModel(currentMountModule, part.transform.FindRecursive("model").FindOrCreate(baseTransformName), ModelOrientation.BOTTOM);
            updateModules(true);
            updateModels();
            updateFuelVolume();
            updateContainerVolume();
            updateGuiState();
            if (!currentMountModule.isValidTextureSet(currentMountTexture)) { currentMountTexture = currentMountModule.getDefaultTextureSet(); }
            currentMountModule.enableTextureSet(currentMountTexture);
            currentMountModule.updateTextureUIControl(this, "currentMountTexture", currentMountTexture);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().updateMountModelFromEditor(newMount, false);
                }
            }
        }

        /// <summary>
        /// Updates the current intertank mesh/model from user input
        /// </summary>
        /// <param name="newDef"></param>
        private void updateIntertankModelFromEditor(String newModel, bool updateSymmetry)
        {
            removeCurrentModel(currentIntertankModule);
            currentIntertankModule = Array.Find(intertankModules, m => m.name == newModel);
            currentIntertank = newModel;
            setupModel(currentIntertankModule, part.transform.FindRecursive("model").FindOrCreate(baseTransformName), ModelOrientation.CENTRAL);
            if (!currentIntertankModule.isValidTextureSet(currentIntertankTextureSet))
            {
                currentIntertankTextureSet = currentIntertankModule.getDefaultTextureSet();
            }
            currentIntertankModule.enableTextureSet(currentIntertankTextureSet);
            currentIntertankModule.updateTextureUIControl(this, "currentIntertankTextureSet", currentIntertankTextureSet);
            updateModules(true);
            updateModels();
            updateTankStats();
            updateContainerVolume();
            updateGuiState();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().updateIntertankModelFromEditor(newModel, false);
                }
            }
        }

        private void setMountTextureFromEditor(String newSet, bool updateSymmetry)
        {
            currentMountTexture = newSet;
            currentMountModule.enableTextureSet(newSet);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().setMountTextureFromEditor(newSet, false);
                }
            }
        }

        private void setTankDomeTextureFromEditor(String newSet, bool updateSymmetry)
        {
            currentDomeTextureSet = newSet;
            upperDomeModule.enableTextureSet(newSet);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().setTankDomeTextureFromEditor(newSet, false);
                }
            }
        }

        private void setTankUpperTextureFromEditor(String newSet, bool updateSymmetry)
        {
            currentUpperTextureSet = newSet;
            upperTopCapModule.enableTextureSet(newSet);
            upperModule.enableTextureSet(newSet);
            upperBottomCapModule.enableTextureSet(newSet);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().setTankUpperTextureFromEditor(newSet, false);
                }
            }
        }

        private void setIntertankTextureFromEditor(String newSet, bool updateSymmetry)
        {
            currentIntertankTextureSet = newSet;
            currentIntertankModule.enableTextureSet(newSet);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().setIntertankTextureFromEditor(newSet, false);
                }
            }
        }

        private void setTankLowerTextureFromEditor(String newSet, bool updateSymmetry)
        {
            currentLowerTextureSet = newSet;
            lowerTopCapModule.enableTextureSet(newSet);
            lowerModule.enableTextureSet(newSet);
            lowerBottomCapModule.enableTextureSet(newSet);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().setTankLowerTextureFromEditor(newSet, false);
                }
            }
        }

        #endregion

        #region ----------------- REGION - KSP Overrides ----------------- 

        /// <summary>
        /// OnLoad override.  Loads previously saved config data, stores module config node for later reading, and does pre-start initialization
        /// </summary>
        /// <param name="node"></param>
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
        }
        
        /// <summary>
        /// OnStart override, does basic startup/init stuff, including building models and registering for editor events
        /// </summary>
        /// <param name="state"></param>
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
            this.updateUIFloatEditControl("currentTankDiameter", minTankDiameter, maxTankDiameter, tankDiameterIncrement*2, tankDiameterIncrement, tankDiameterIncrement*0.05f, true, currentTankDiameter);
            this.updateUIFloatEditControl("currentTankHeight", minTankHeight, maxTankHeight, tankHeightIncrement*2f, tankHeightIncrement, tankHeightIncrement*0.05f, true, currentTankHeight);
            string[] names = SSTUUtils.getNames(mountModules, m => m.name);
            this.updateUIChooseOptionControl("currentMount", names, names, true, currentMount);
            if (this.splitTank)
            {
                names = SSTUUtils.getNames(intertankModules, m => m.name);
                this.updateUIChooseOptionControl("currentIntertank", names, names, true, currentIntertank);
            }
            else
            {
                Fields["currentIntertank"].guiActiveEditor = false;
            }

            upperDomeModule.updateTextureUIControl(this, "currentDomeTextureSet", currentDomeTextureSet);
            upperModule.updateTextureUIControl(this, "currentUpperTextureSet", currentUpperTextureSet);
            if (splitTank)
            {
                currentIntertankModule.updateTextureUIControl(this, "currentIntertankTextureSet", currentIntertankTextureSet);
                lowerModule.updateTextureUIControl(this, "currentLowerTextureSet", currentLowerTextureSet);
            }
            currentMountModule.updateTextureUIControl(this, "currentMountTexture", currentMountTexture);

            Fields["currentTankDiameter"].uiControlEditor.onFieldChanged = onDiameterUpdated;
            Fields["currentTankHeight"].uiControlEditor.onFieldChanged = onHeightUpdated;
            Fields["currentMount"].uiControlEditor.onFieldChanged = onMountUpdated;
            Fields["currentIntertank"].uiControlEditor.onFieldChanged = onIntertankUpdated;

            Fields["currentDomeTextureSet"].uiControlEditor.onFieldChanged = onDomeTextureUpdated;
            Fields["currentUpperTextureSet"].uiControlEditor.onFieldChanged = onUpperTextureUpdated;
            Fields["currentIntertankTextureSet"].uiControlEditor.onFieldChanged = onIntertankTextureUpdated;
            Fields["currentLowerTextureSet"].uiControlEditor.onFieldChanged = onLowerTextureUpdated;
            Fields["currentMountTexture"].uiControlEditor.onFieldChanged = onMountTextureUpdated;

            SSTUModInterop.onPartGeometryUpdate(part, true);
            SSTUStockInterop.fireEditorUpdate();
        }

        public override string GetInfo()
        {
            return "This part has configurable diameter, height, bottom-cap, and fairings.";
        }

        /// <summary>
        /// Unity method override, supposedly called after -all- modules have had OnStart() called.
        /// Overriden to update fairing and RCS modules, which might not exist when OnStart() is called for this module
        /// </summary>
        public void Start()
        {
            if (!initializedFairing && HighLogic.LoadedSceneIsEditor)
            {
                initializedFairing = true;
                updateFairing(true);
            }
            if (!initializedResources && HighLogic.LoadedSceneIsEditor)
            {
                initializedResources = true;
                updateContainerVolume();
            }
            updateRCSThrust();
            updateGuiState();
        }

        /// <summary>
        /// Return the current part cost/modifier.  Returns the pre-calculated tank cost.
        /// </summary>
        /// <param name="defaultCost"></param>
        /// <returns></returns>
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            return subtractCost ? -defaultCost + moduleCost : moduleCost;
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            return subtractMass ? -defaultMass + moduleMass : moduleMass;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        #endregion

        #region ----------------- REGION - Initialization Methods ----------------- 

        /// <summary>
        /// Basic initialization code, should only be ran once per part-instance (though, is safe to call from both start and load)
        /// </summary>
        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            SSTUUtils.destroyChildren(part.transform.FindRecursive(baseTransformName));
            if (currentTankDiameter==-1f)
            {
                //should only run once, on the prefab part; which is fine, as the models/settings will be cloned and carried over to the editor part
                currentTankDiameter = defaultTankDiameter;
                currentTankHeight = defaultTankHeight;
                currentMount = defaultMount;
                currentIntertank = defaultIntertank;
                currentRcsThrust = defaultRcsThrust;
            }
            loadConfigData();
            updateModules(false);
            buildSavedModel();
            updateModels();
            updateTankStats();
            updateEditorFields();
            updateGuiState();
            updateTextureSet(false);
        }
                
        /// <summary>
        /// Restores the editor-only diameter and height-adjustment values;
        /// </summary>
        private void updateEditorFields()
        {
            prevIntertank = currentIntertank;
            prevMount = currentMount;
            prevTankDiameter = currentTankDiameter;
            prevHeight = currentTankHeight;
        }
        
        /// <summary>
        /// Loads all of the part definitions and values from the stashed config node data
        /// </summary>
        private void loadConfigData()
        {
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);

            //mandatory nodes, -all- tank types must have these
            ConfigNode upperDomeNode = node.GetNode("TANKDOME");
            ConfigNode upperTopCapNode = node.GetNode("TANKUPPERTOPCAP");
            ConfigNode tankUpperNode = node.GetNode("TANKUPPER");
            ConfigNode upperBottomCapNode = node.GetNode("TANKUPPERBOTTOMCAP");

            ConfigNode rcsNode = node.GetNode("RCS");
            ConfigNode[] mountNodes = node.GetNodes("MOUNT");

            upperDomeModule = new SingleModelData(upperDomeNode);
            upperTopCapModule = new SingleModelData(upperTopCapNode);
            upperModule = new SingleModelData(tankUpperNode);            
            upperBottomCapModule = new SingleModelData(upperBottomCapNode);
            rcsModule = new SSTUCustomUpperStageRCS(rcsNode);

            //load mount configs
            int len = mountNodes.Length;
            mountModules = new SingleModelData[len];
            for (int i = 0; i < len; i++)
            {
                mountModules[i] = new SingleModelData(mountNodes[i]);
            }
            currentMountModule = Array.Find(mountModules, l => l.name == currentMount);
            if (!currentMountModule.isValidTextureSet(currentMountTexture))
            {
                currentMountTexture = currentMountModule.getDefaultTextureSet();
            }
            if (splitTank)
            {
                //fields that are only populated by split-tank type upper-stages
                ConfigNode lowerTopCapNode = node.GetNode("TANKLOWERTOPCAP");
                ConfigNode tankLowerNode = node.GetNode("TANKLOWER");
                ConfigNode lowerBottomCapNode = node.GetNode("TANKLOWERBOTTOMCAP");
                ConfigNode[] intertankNodes = node.GetNodes("INTERTANK");
                lowerTopCapModule = new SingleModelData(lowerTopCapNode);
                lowerModule = new SingleModelData(tankLowerNode);
                lowerBottomCapModule = new SingleModelData(lowerBottomCapNode);
                //load intertank configs
                len = intertankNodes.Length;
                intertankModules = new SingleModelData[len];
                for (int i = 0; i < len; i++)
                {
                    intertankModules[i] = new SingleModelData(intertankNodes[i]);
                }
                currentIntertankModule = Array.Find(intertankModules, l => l.name == currentIntertank);
            }
            if (!upperDomeModule.isValidTextureSet(currentDomeTextureSet)) { currentDomeTextureSet = upperDomeModule.getDefaultTextureSet(); }
            if (!upperModule.isValidTextureSet(currentUpperTextureSet)) { currentUpperTextureSet = upperModule.getDefaultTextureSet(); }
            if (splitTank && !currentIntertankModule.isValidTextureSet(currentIntertankTextureSet)){ currentIntertankTextureSet = currentIntertankModule.getDefaultTextureSet(); }
            if (splitTank && !lowerModule.isValidTextureSet(currentLowerTextureSet)) { currentLowerTextureSet = lowerModule.getDefaultTextureSet(); }
        }

        #endregion

        #region ----------------- REGION - Module Position / Parameter Updating ----------------- 

        /// <summary>
        /// Updates the internal cached scale of each of the modules; applied to models later
        /// </summary>
        private void updateModuleScales()
        {
            float scale = currentTankDiameter / defaultTankDiameter;
            upperDomeModule.updateScaleForDiameter(currentTankDiameter);
            upperTopCapModule.updateScaleForDiameter(currentTankDiameter);
            upperModule.updateScaleForHeightAndDiameter(currentTankHeight * scale, currentTankDiameter);
            upperBottomCapModule.updateScaleForDiameter(currentTankDiameter);

            float mountDiameterScale = currentTankDiameter;
            if (splitTank)
            {
                currentIntertankModule.updateScaleForDiameter(currentTankDiameter);
                float lowerDiameter = currentTankDiameter * 0.75f;
                float lowerHeight = currentTankHeight * 0.75f;
                mountDiameterScale = lowerDiameter;
                lowerTopCapModule.updateScaleForDiameter(lowerDiameter);
                lowerModule.updateScaleForHeightAndDiameter(lowerHeight, lowerDiameter);
                lowerBottomCapModule.updateScaleForDiameter(lowerDiameter);
            }            
            currentMountModule.updateScaleForDiameter(mountDiameterScale);
            rcsModule.updateScaleForDiameter(mountDiameterScale);
        }
                
        /// <summary>
        /// Updated the modules internal cached position value.  This value is used later to update the actual model positions.
        /// </summary>
        private void updateModulePositions()
        {
            float totalHeight = 0;
            totalHeight += upperDomeModule.currentHeight;
            totalHeight += upperTopCapModule.currentHeight;
            totalHeight += upperModule.currentHeight;
            totalHeight += upperBottomCapModule.currentHeight;
            if (splitTank)
            {
                totalHeight += currentIntertankModule.currentHeight;
                totalHeight += lowerTopCapModule.currentHeight;
                totalHeight += lowerModule.currentHeight;
                totalHeight += lowerBottomCapModule.currentHeight;
            }
            totalHeight += currentMountModule.currentHeight;
            
            //start height = total height * 0.5
            float startY = totalHeight * 0.5f;
            partTopY = startY;
            partBottomY = -partTopY;

            //next 'position' is the origin for the dome and start for the fairings
            startY -= upperDomeModule.currentHeight;
            topFairingBottomY = startY;
            upperDomeModule.currentVerticalPosition = startY;

            //next position is the origin for the upper-tank-top-cap portion of the model
            startY -= upperTopCapModule.currentHeight;
            upperTopCapModule.currentVerticalPosition = startY;

            //next position is the origin for the upper-tank stretchable model; it uses a center-origin system, so position it using half of its height            
            startY -= upperModule.currentHeight * 0.5f;
            upperModule.currentVerticalPosition = startY;

            //next position is the origin for the upper-tank-lower-cap
            startY -= upperModule.currentHeight * 0.5f;//finish moving downward for the upper-tank-stretch segment
            startY -= upperTopCapModule.currentHeight;
            upperBottomCapModule.currentVerticalPosition = startY;
            
            //next position the split-tank elements if ST is enabled            
            if (splitTank)
            {
                //move downward for the intertank height
                startY -= currentIntertankModule.currentHeight;
                currentIntertankModule.currentVerticalPosition = startY;

                //move downward for the lower tank top cap
                startY -= lowerTopCapModule.currentHeight;
                lowerTopCapModule.currentVerticalPosition = startY;

                //move downward for half height of the lower stretch tank
                startY -= lowerModule.currentHeight * 0.5f;
                lowerModule.currentVerticalPosition = startY;
                startY -= lowerModule.currentHeight * 0.5f;

                //move downward for the lower tank bottom cap 
                startY -= lowerBottomCapModule.currentHeight;
                lowerBottomCapModule.currentVerticalPosition = startY;                
            }

            //and should already be positioned properly for the mount
            currentMountModule.currentVerticalPosition = startY;
            rcsModule.currentVerticalPosition = currentMountModule.currentVerticalPosition + (currentMountModule.modelDefinition.rcsVerticalPosition * currentMountModule.currentHeightScale);
            rcsModule.currentHorizontalPosition = currentMountModule.modelDefinition.rcsHorizontalPosition * currentMountModule.currentDiameterScale;
            rcsModule.mountVerticalRotation = currentMountModule.modelDefinition.rcsVerticalRotation;
            rcsModule.mountHorizontalRotation = currentMountModule.modelDefinition.rcsHorizontalRotation;

            if (splitTank)
            {
                bottomFairingTopY = upperBottomCapModule.currentVerticalPosition;
            }
            else
            {
                bottomFairingTopY = currentMountModule.currentVerticalPosition;
                bottomFairingTopY += currentMountModule.modelDefinition.fairingTopOffset * currentMountModule.currentHeightScale;
            }
        }

        /// <summary>
        /// Blanket method for when module parameters have changed (heights, diameters, mounts, etc)
        /// updates
        /// Does not update fuel/resources/mass
        /// </summary>
        private void updateModules(bool userInput)
        {
            updateModuleScales();
            updateModulePositions();
            updateNodePositions(userInput);
            updateFairing(userInput || (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor));
        }
                
        /// <summary>
        /// Update the fairing module height and position based on current tank parameters
        /// </summary>
        private void updateFairing(bool userInput)
        {
            SSTUNodeFairing[] modules = part.GetComponents<SSTUNodeFairing>();
            if (modules == null || modules.Length < 2)
            {
                return;                
            }
            SSTUNodeFairing topFairing = modules[topFairingIndex];
            if (topFairing != null)
            {
                FairingUpdateData data = new FairingUpdateData();
                data.setTopY(partTopY);
                data.setBottomY(topFairingBottomY);
                data.setBottomRadius(currentTankDiameter * 0.5f);
                if (userInput){data.setTopRadius(currentTankDiameter * 0.5f);}
                topFairing.updateExternal(data);
            }            
            SSTUNodeFairing bottomFairing = modules[lowerFairingIndex];
            if (bottomFairing != null)
            {
                FairingUpdateData data = new FairingUpdateData();
                data.setTopRadius(currentTankDiameter * 0.5f);
                data.setTopY(bottomFairingTopY);
                if (userInput) { data.setBottomRadius(currentTankDiameter * 0.5f); }
                bottomFairing.updateExternal(data);
            }
        }

        /// <summary>
        /// Update the attach node positions based on the current tank parameters.
        /// </summary>
        private void updateNodePositions(bool userInput)
        {
            AttachNode topNode = part.FindAttachNode("top");
            SSTUAttachNodeUtils.updateAttachNodePosition(part, topNode, new Vector3(0, partTopY, 0), topNode.orientation, userInput);

            AttachNode topNode2 = part.FindAttachNode("top2");
            SSTUAttachNodeUtils.updateAttachNodePosition(part, topNode2, new Vector3(0, topFairingBottomY, 0), topNode2.orientation, userInput);

            AttachNode bottomNode = part.FindAttachNode("bottom");
            SSTUAttachNodeUtils.updateAttachNodePosition(part, bottomNode, new Vector3(0, partBottomY, 0), bottomNode.orientation, userInput);
            
            if (!String.IsNullOrEmpty(interstageNodeName))
            {
                Vector3 pos = new Vector3(0, bottomFairingTopY, 0);
                SSTUSelectableNodes.updateNodePosition(part, interstageNodeName, pos);
                AttachNode interstage = part.FindAttachNode(interstageNodeName);
                if (interstage != null)
                {
                    Vector3 orientation = new Vector3(0, -1, 0);
                    SSTUAttachNodeUtils.updateAttachNodePosition(part, interstage, pos, orientation, userInput);
                }
            }
        }

        private void updateTextureSet(bool updateSymmetry)
        {
            upperDomeModule.enableTextureSet(currentDomeTextureSet);
            upperTopCapModule.enableTextureSet(currentUpperTextureSet);
            upperModule.enableTextureSet(currentUpperTextureSet);
            upperBottomCapModule.enableTextureSet(currentUpperTextureSet);
            if (splitTank)
            {
                currentIntertankModule.enableTextureSet(currentIntertankTextureSet);
                lowerTopCapModule.enableTextureSet(currentLowerTextureSet);
                lowerModule.enableTextureSet(currentLowerTextureSet);
                lowerBottomCapModule.enableTextureSet(currentLowerTextureSet);
            }
            currentMountModule.enableTextureSet(currentMountTexture);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().updateTextureSet(false);
                }
            }
        }

        #endregion

        #region ----------------- REGION - Model Build / Updating ----------------- 

        /// <summary>
        /// Builds the model from the current/default settings, and/or restores object links from existing game-objects
        /// </summary>
        private void buildSavedModel()
        {
            Transform modelBase = part.transform.FindRecursive("model").FindOrCreate(baseTransformName);

            setupModel(upperDomeModule, modelBase, ModelOrientation.CENTRAL);
            setupModel(upperTopCapModule, modelBase, ModelOrientation.CENTRAL);
            setupModel(upperModule, modelBase, ModelOrientation.CENTRAL);
            setupModel(upperBottomCapModule, modelBase, ModelOrientation.CENTRAL);
            
            if (splitTank)
            {
                if (currentIntertankModule.name != defaultIntertank)
                {
                    SingleModelData dim = Array.Find<SingleModelData>(intertankModules, l => l.name == defaultIntertank);
                    dim.setupModel(modelBase, ModelOrientation.CENTRAL);
                    removeCurrentModel(dim);
                }                
                setupModel(currentIntertankModule, modelBase, ModelOrientation.CENTRAL);
                setupModel(lowerTopCapModule, modelBase, ModelOrientation.CENTRAL);
                setupModel(lowerModule, modelBase, ModelOrientation.CENTRAL);
                setupModel(lowerBottomCapModule, modelBase, ModelOrientation.CENTRAL);
            }
            if (currentMountModule.name != defaultMount)
            {
                SingleModelData dmm = Array.Find<SingleModelData>(mountModules, l => l.name == defaultMount);
                dmm.setupModel(modelBase, ModelOrientation.BOTTOM);
                removeCurrentModel(dmm);
            }

            setupModel(currentMountModule, modelBase, ModelOrientation.BOTTOM);
            setupModel(rcsModule, part.transform.FindRecursive("model").FindOrCreate(rcsTransformName), ModelOrientation.CENTRAL);
        }

        /// <summary>
        /// Finds the model for the given part, if it currently exists; else it clones it
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        private void setupModel(ModelData model, Transform parent, ModelOrientation orientation)
        {
            model.setupModel(parent, orientation);
        }

        /// <summary>
        /// Removes the current model of the passed in upper-stage part; used when switching mounts or intertank parts
        /// </summary>
        /// <param name="usPart"></param>
        private void removeCurrentModel(ModelData usPart)
        {
            usPart.destroyCurrentModel();
        }

        /// <summary>
        /// Updates models from module current parameters for scale and positioning
        /// </summary>
        private void updateModels()
        {
            upperDomeModule.updateModel();
            upperTopCapModule.updateModel();
            upperModule.updateModel();
            upperBottomCapModule.updateModel();

            if (splitTank)
            {
                currentIntertankModule.updateModel();
                lowerTopCapModule.updateModel();
                lowerModule.updateModel();
                lowerBottomCapModule.updateModel();
            }

            currentMountModule.updateModel();
            rcsModule.updateModel();

            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        #endregion

        #region ----------------- REGION - Tank stats updating - volume/mass/cost/thrust ----------------- 

        /// <summary>
        /// Wrapper for methods that update the tanks internal statistics -- mass, volume, etc.  Does NOT update part resources.
        /// Calls: updateFuelVolume(); updatePartMass; updatePartCost; updateRCSThrust;
        /// </summary>
        private void updateTankStats()
        {
            updateFuelVolume();
            updateModuleMass();
            updateModuleCost();
            updateRCSThrust();
        }
        
        /// <summary>
        /// Calculates the internal volume from all of the currentl selected parts, their configurations, and their current scales
        /// </summary>
        private void updateFuelVolume()
        {
            totalTankVolume = 0;
            totalTankVolume += upperDomeModule.getModuleVolume();
            totalTankVolume += upperTopCapModule.getModuleVolume();
            totalTankVolume += upperModule.getModuleVolume();
            totalTankVolume += upperBottomCapModule.getModuleVolume();
            if(splitTank)
            {
                totalTankVolume += currentIntertankModule.getModuleVolume();
                totalTankVolume += lowerTopCapModule.getModuleVolume();
                totalTankVolume += lowerModule.getModuleVolume();
                totalTankVolume += lowerBottomCapModule.getModuleVolume();
            }
            totalTankVolume += currentMountModule.getModuleVolume();
        }

        /// <summary>
        /// Updates the cached part-mass value from the calculated masses of the current modules/tank setup.  Safe to call..whenever.
        /// Does -not- update part masses.  See -updatePartMass()- for that function.
        /// </summary>
        private void updateModuleMass()
        {
            moduleMass = upperTopCapModule.getModuleMass() + upperModule.getModuleMass() + upperBottomCapModule.getModuleMass() + currentMountModule.getModuleMass() + rcsModule.getModuleMass()+upperDomeModule.getModuleMass();
            if (splitTank)
            {
                moduleMass += currentIntertankModule.getModuleMass() + lowerModule.getModuleMass() + lowerBottomCapModule.getModuleMass() + lowerTopCapModule.getModuleMass(); ;
            }
        }
                
        /// <summary>
        /// Updates the tankCost field with the current cost for the selected fuel type and tank size, including cost for tankage
        /// </summary>
        private void updateModuleCost()
        {
            moduleCost = upperTopCapModule.getModuleCost() + upperModule.getModuleCost() + upperBottomCapModule.getModuleCost() + currentMountModule.getModuleCost() + rcsModule.getModuleCost()+upperDomeModule.getModuleCost();
            if (splitTank)
            {
                moduleCost += currentIntertankModule.getModuleCost() + lowerModule.getModuleCost() + lowerBottomCapModule.getModuleCost()+lowerTopCapModule.getModuleCost();
            }
        }

        /// <summary>
        /// update external RCS-module with thrust value;
        /// TODO - may need to cache the 'needs update' flag, and run on first OnUpdate/etc, as otherwise the RCS module will likely not exist yet
        /// </summary>
        private void updateRCSThrust()
        {
            ModuleRCS[] rcsMod = part.GetComponents<ModuleRCS>();
            int len = rcsMod.Length;
            float scale = currentTankDiameter / defaultTankDiameter;
            rcsThrust = defaultRcsThrust * scale * scale;
            for (int i = 0; i < len; i++)
            {
                rcsMod[i].thrusterPower = rcsThrust;
            }
        }

        /// <summary>
        /// Updates current gui button availability status as well as updating the visible GUI variables from internal state vars
        /// </summary>
        private void updateGuiState()
        {
            Fields["currentDomeTextureSet"].guiActiveEditor = upperDomeModule.modelDefinition.textureSets.Length > 1;
            Fields["currentUpperTextureSet"].guiActiveEditor = upperModule.modelDefinition.textureSets.Length > 1;
            Fields["currentIntertankTextureSet"].guiActiveEditor = splitTank && currentIntertankModule.modelDefinition.textureSets.Length > 1;
            Fields["currentLowerTextureSet"].guiActiveEditor = splitTank && lowerModule.modelDefinition.textureSets.Length > 1;
            Fields["currentMountTexture"].guiActiveEditor = currentMountModule.modelDefinition.textureSets.Length > 1;
            guiDryMass = moduleMass;
            guiTotalHeight = partTopY + Math.Abs(partBottomY);
            guiTankHeight = upperModule.currentHeight;
            guiRcsThrust = rcsThrust;
        }

        #endregion

        #region ----------------- REGION - Part Updating - Resource/Mass ----------------- 

        /// <summary>
        /// Updates the min/max quantities of resource in the part based on the current 'totalFuelVolume' field and currently set fuel type
        /// </summary>
        private void updateContainerVolume()
        {
            SSTUModInterop.onPartFuelVolumeUpdate(part, totalTankVolume * 1000f);
        }        
        #endregion

    }

    public class SSTUCustomUpperStageRCS : ModelData
    {
        public GameObject[] models;
        public float currentHorizontalPosition;
        public float modelRotation = 0;
        public float modelHorizontalZOffset = 0;
        public float modelHorizontalXOffset = 0;
        public float modelVerticalOffset = 0;

        public float mountVerticalRotation = 0;
        public float mountHorizontalRotation = 0;

        public SSTUCustomUpperStageRCS(ConfigNode node) : base(node)
        {
            modelRotation = node.GetFloatValue("modelRotation");
            modelHorizontalZOffset = node.GetFloatValue("modelHorizontalZOffset");
            modelHorizontalXOffset = node.GetFloatValue("modelHorizontalXOffset");
            modelVerticalOffset = node.GetFloatValue("modelVerticalOffset");
        }
        
        public override void setupModel(Transform parent, ModelOrientation orientation)
        {
            models = new GameObject[4];
            Transform[] trs = parent.FindChildren(modelDefinition.modelName);
            if (trs != null && trs.Length>0)
            {
                for (int i = 0; i < 4; i++)
                {
                    models[i] = trs[i].gameObject;
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    models[i] = SSTUUtils.cloneModel(modelDefinition.modelName);
                }
            }
            foreach (GameObject go in models)
            {
                go.transform.NestToParent(parent);
            }            
        }

        public override void updateModel()
        {
            if (models != null)
            {
                float rotation = 0;
                float posX = 0, posZ = 0, posY = 0;
                float scale = 1;
                float length = 0;
                for (int i = 0; i < 4; i++)
                {
                    rotation = (float)(i * 90) + mountVerticalRotation;
                    scale = currentDiameterScale;
                    length = currentHorizontalPosition + (scale * modelHorizontalZOffset);                    
                    posX = (float)Math.Sin(SSTUUtils.toRadians(rotation)) * length;
                    posZ = (float)Math.Cos(SSTUUtils.toRadians(rotation)) * length;                    
                    posY = currentVerticalPosition + (scale * modelVerticalOffset);
                    models[i].transform.localScale = new Vector3(currentDiameterScale, currentHeightScale, currentDiameterScale);
                    models[i].transform.localPosition = new Vector3(posX, posY, posZ);
                    models[i].transform.localRotation = Quaternion.AngleAxis(rotation + 90f, new Vector3(0, 1, 0));
                    models[i].transform.Rotate(new Vector3(0, 0, 1), mountHorizontalRotation, Space.Self);
                }
            }
        }
    }

}
