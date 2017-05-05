using System;
using System.Collections;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUModularUpperStage : PartModule, IPartCostModifier, IPartMassModifier, IRecolorable
    {

        #region ----------------- REGION - Standard KSP-accessible config fields -----------------
        /// <summary>
        /// quick/dirty/easy flag to determine if should even attempt to load/manipulate split-tank elements
        /// </summary>
        [KSPField]
        public bool splitTank = true;

        /// <summary>
        /// How much is the 'diameter' incremented for every 'large' tank diameter step? - this value is -not- scaled, and used as-is.
        /// </summary>
        [KSPField]
        public float tankDiameterIncrement = 0.625f;

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
        /// Index in same-type modules for the SSTUNodeFairing responsible for the 'top' fairing.
        /// </summary>
        [KSPField]
        public int topFairingIndex = 0;

        /// <summary>
        /// Index in same-type modules for the SSTUNodeFairing responsible for the 'bottom' fairing.
        /// </summary>
        [KSPField]
        public int lowerFairingIndex = 1;

        /// <summary>
        /// Name of the 'interstage' node; positioned depending on mount interstage location (CB) / bottom of the upper tank (ST).
        /// </summary>
        [KSPField]
        public String noseInterstageNodeName = "noseInterstage";

        /// <summary>
        /// Name of the 'interstage' node; positioned depending on mount interstage location (CB) / bottom of the upper tank (ST).
        /// </summary>
        [KSPField]
        public String mountInterstageNodeName = "mountInterstage";
        
        /// <summary>
        /// RealFuels compatibility config field, set to true when RF is in use to let RF handle mass/cost updates
        /// </summary>
        [KSPField]
        public bool subtractMass = false;

        /// <summary>
        /// RealFuels compatibility config field, set to true when RF is in use to let RF handle mass/cost updates
        /// </summary>
        [KSPField]
        public bool subtractCost = false;

        #endregion

        #region ----------------- REGION - GUI visible fields and fine tune adjustment contols - do not edit through config -----------------

        /// <summary>
        /// Current height of the tank, including nose, tanks, intertank, and mount.
        /// </summary>
        [KSPField(guiName = "Tank Height", guiActive = false, guiActiveEditor = true)]
        public float guiTankHeight;

        /// <summary>
        /// RCS thrust, based on tank model scale.
        /// </summary>
        [KSPField(guiName = "RCS Thrust", guiActive = false, guiActiveEditor = true)]
        public float guiRcsThrust;
        #endregion

        #region ----------------- REGION - persistent data fields ----------------- 
        /**
         * The below persistent data fields -MUST- be filled in in the config file properly for the part being set up.  
         * E.G. split-tank type must have all models populated, CB only needs nose/uppertank/mount populated.
         * **/

        /// <summary>
        /// Current absolute tank diameter (of the upper tank for split-tank, or of the full tank for common-bulkhead types)
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor =true, guiName ="Diameter"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentTankDiameter = 1.25f;

        /// <summary>
        /// Current scale height of the tank sections
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor =true, guiName = "Scale"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true, minValue = 0.25f, maxValue = 1.75f)]
        public float currentTankHeight = 0.5f;

        /// <summary>
        /// The currently selected/enabled nose option
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Nose"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentNose = String.Empty;

        /// <summary>
        /// The currently selected/enabled nose option
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Upper"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentUpper = String.Empty;

        /// <summary>
        /// The currently selected/enabled intertank option (if any).
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor =true, guiName ="Intertank"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentIntertank = String.Empty;

        /// <summary>
        /// The currently selected/enabled nose option
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Lower"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentLower = String.Empty;

        /// <summary>
        /// The currently selected/enabled mount option.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Mount"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentMount = String.Empty;

        /// <summary>
        /// The currently selected/enabled RCS option.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Mount"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentRCS = String.Empty;

        /// <summary>
        /// Percent of volume of the part that is dedicated to EC/hypergolics
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Support Tank %"),
         UI_FloatRange(suppressEditorShipModified = true, minValue = 0, maxValue = 20, stepIncrement = 0.25f)]
        public float supportPercent = 5f;

        /// <summary>
        /// Texture set persistent values for model-modules.  Initialized to the default texture set for the model during part init, can be left blank in the config unless specific sets are requested.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Nose Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentNoseTexture = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Upper Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentUpperTexture = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Intertank Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentIntertankTexture = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Lower Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentLowerTexture = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Mount Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentMountTexture = String.Empty;

        /**
         * Persistent data fields for storage of model-module custom data -- colors/etc.
         * **/
        [KSPField(isPersistant = true)]
        public string nosePersistentData;

        [KSPField(isPersistant = true)]
        public string upperPersistentData;

        [KSPField(isPersistant = true)]
        public string intertankPersistentData;

        [KSPField(isPersistant = true)]
        public string lowerPersistentData;

        [KSPField(isPersistant = true)]
        public string mountPersistentData;

        /// <summary>
        /// Used solely to track if resources have been initialized, as this should only happen once on first part creation (regardless of if it is created in flight or in the editor);
        /// Unsure of any cleaner way to track a simple boolean value across the lifetime of a part, seems like the part-persistence data is probably it...
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        /// <summary>
        /// Used to track if the fairing states have been initialized to the currently setup tank sizes.  After initialization, resizing only occurs based on user input.
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool initializedFairing = false;

        [Persistent]
        public string configNodeData = string.Empty;

        #endregion

        #region ----------------- REGION - Private working value fields ----------------- 
                
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

        private ModelModule<SingleModelData> noseModule;
        private ModelModule<SingleModelData> upperModule;
        private ModelModule<SingleModelData> intertankModule;
        private ModelModule<SingleModelData> lowerModule;
        private ModelModule<SingleModelData> mountModule;
        private ModelModule<SSTUModularUpperStageRCS> rcsModule;

        private bool initialized = false;
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
            this.updateUIFloatEditControl(nameof(currentTankDiameter), minTankDiameter, maxTankDiameter, tankDiameterIncrement * 2, tankDiameterIncrement, tankDiameterIncrement * 0.05f, true, currentTankDiameter);
            this.updateUIFloatEditControl(nameof(currentTankHeight), 0.25f, 1.75f, 0.25f, 0.125f, 0.005f, true, currentTankHeight);

            if (!splitTank)
            {
                Fields[nameof(currentIntertank)].guiActiveEditor = false;
                Fields[nameof(currentLower)].guiActiveEditor = false;
            }

            Fields[nameof(currentNose)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                noseModule.modelSelected(currentNose);
                this.actionWithSymmetry(m => 
                {
                    //TODO model position updates, recalc volume, mass, reposition fairings
                });
            };

            Fields[nameof(currentUpper)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                upperModule.modelSelected(currentUpper);
                this.actionWithSymmetry(m =>
                {
                    //TODO model position updates, recalc volume, mass, reposition fairings
                });
            };

            Fields[nameof(currentMount)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                mountModule.modelSelected(currentMount);
                this.actionWithSymmetry(m =>
                {
                    //TODO model position updates, recalc volume, mass, reposition fairings
                });
            };

            Fields[nameof(currentRCS)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                rcsModule.modelSelected(currentRCS);
                this.actionWithSymmetry(m =>
                {
                    //TODO model position updates, recalc volume, mass, reposition fairings
                });
            };

            Fields[nameof(currentNoseTexture)].uiControlEditor.onFieldChanged = noseModule.textureSetSelected;
            Fields[nameof(currentUpperTexture)].uiControlEditor.onFieldChanged = upperModule.textureSetSelected;
            Fields[nameof(currentMountTexture)].uiControlEditor.onFieldChanged = mountModule.textureSetSelected;

            if (splitTank)
            {
                Fields[nameof(currentIntertank)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) { };
                Fields[nameof(currentLower)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) { };

                Fields[nameof(currentIntertankTexture)].uiControlEditor.onFieldChanged = intertankModule.textureSetSelected;
                Fields[nameof(currentLowerTexture)].uiControlEditor.onFieldChanged = lowerModule.textureSetSelected;
            }

            Fields[nameof(supportPercent)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.supportPercent = supportPercent; }//else it conflicts with stock slider functionality
                    m.updateContainerVolume();
                    m.updateGuiState();
                });
                SSTUStockInterop.fireEditorUpdate();
            };

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

        public string[] getSectionNames()
        {
            if (splitTank)
            {
                return new string[] { "Nose", "Upper", "Intertank", "Lower", "Mount" };
            }
            return new string[] { "Nose", "Upper", "Mount" };
        }

        public Color[] getSectionColors(string section)
        {
            if (section == "Nose")
            {
                return noseModule.customColors;
            }
            else if (section == "Upper")
            {
                return upperModule.customColors;
            }
            else if (section == "Intertank")
            {
                return intertankModule.customColors;
            }
            else if (section == "Lower")
            {
                return lowerModule.customColors;
            }
            else if (section == "Mount")
            {
                return mountModule.customColors;
            }
            return new Color[] { Color.white, Color.white, Color.white };
        }

        public void setSectionColors(string section, Color[] colors)
        {
            if (section == "Nose")
            {
                noseModule.setSectionColors(colors);
            }
            else if (section == "Upper")
            {
                upperModule.setSectionColors(colors);
            }
            else if (section == "Intertank")
            {
                intertankModule.setSectionColors(colors);
            }
            else if (section == "Lower")
            {
                lowerModule.setSectionColors(colors);
            }
            else if (section == "Mount")
            {
                mountModule.setSectionColors(colors);
            }
        }

        #endregion

        #region ----------------- REGION - Initialization Methods ----------------- 

        /// <summary>
        /// Basic initialization code, should only be ran once per part-instance (though, is safe to call from both start and load)
        /// </summary>
        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            loadConfigData();
            updateModules(false);
            buildSavedModel();
            updateModels();
            updateTankStats();
            updateGuiState();
        }
        
        /// <summary>
        /// Loads all of the part definitions and values from the stashed config node data
        /// </summary>
        private void loadConfigData()
        {
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);

            ConfigNode[] noseNodes = node.GetNodes("NOSE");
            ConfigNode upperNode = node.GetNode("UPPERTANK");
            ConfigNode[] mountNodes = node.GetNodes("MOUNT");
            ConfigNode rcsNode = node.GetNode("RCS");

            noseModule = new ModelModule<SingleModelData>(part, this, null, ModelOrientation.TOP, nameof(nosePersistentData), nameof(currentNose), nameof(currentNoseTexture));
            noseModule.getSymmetryModule = delegate (PartModule m) { return ((SSTUModularUpperStage)m).noseModule; };
            noseModule.setupModelList(SingleModelData.parseModels(noseNodes));

            upperModule = new ModelModule<SingleModelData>(part, this, null, ModelOrientation.TOP, nameof(upperPersistentData), nameof(currentUpper), nameof(currentUpperTexture));
            upperModule.getSymmetryModule = delegate (PartModule m) { return ((SSTUModularUpperStage)m).upperModule; };
            upperModule.setupModelList(SingleModelData.parseModels(new ConfigNode[] { upperNode }));

            if (splitTank)
            {
                ConfigNode[] interNodes = node.GetNodes("INTERTANK");
                ConfigNode lowerNode = node.GetNode("LOWERTANK");
                intertankModule = new ModelModule<SingleModelData>(part, this, null, ModelOrientation.TOP, nameof(intertankPersistentData), nameof(currentIntertank), nameof(currentIntertankTexture));
                intertankModule.getSymmetryModule = delegate (PartModule m) { return ((SSTUModularUpperStage)m).intertankModule; };
                intertankModule.setupModelList(SingleModelData.parseModels(interNodes));

                lowerModule = new ModelModule<SingleModelData>(part, this, null, ModelOrientation.TOP, nameof(lowerPersistentData), nameof(currentLower), nameof(currentLowerTexture));
                lowerModule.getSymmetryModule = delegate (PartModule m) { return ((SSTUModularUpperStage)m).lowerModule; };
                lowerModule.setupModelList(SingleModelData.parseModels(new ConfigNode[] { lowerNode }));
            }

            mountModule = new ModelModule<SingleModelData>(part, this, null, ModelOrientation.BOTTOM, nameof(mountPersistentData), nameof(currentMount), nameof(currentMountTexture));
            mountModule.getSymmetryModule = delegate (PartModule m) { return ((SSTUModularUpperStage)m).mountModule; };
            mountModule.setupModelList(SingleModelData.parseModels(mountNodes));

            rcsModule = new ModelModule<SSTUModularUpperStageRCS>(part, this, null, ModelOrientation.CENTRAL, null, null, null);
            rcsModule.getSymmetryModule = delegate (PartModule m) { return ((SSTUModularUpperStage)m).rcsModule; };
            rcsModule.setupModelList(null);
        }

        #endregion

        #region ----------------- REGION - Module Position / Parameter Updating ----------------- 

        /// <summary>
        /// Updates the internal cached scale of each of the modules; applied to models later
        /// </summary>
        private void updateModuleScales()
        {
            //float scale = currentTankDiameter / defaultTankDiameter;
            //upperDomeModule.updateScaleForDiameter(currentTankDiameter);
            //upperTopCapModule.updateScaleForDiameter(currentTankDiameter);
            //upperModule.updateScaleForHeightAndDiameter(currentTankHeight * scale, currentTankDiameter);
            //upperBottomCapModule.updateScaleForDiameter(currentTankDiameter);

            //float mountDiameterScale = currentTankDiameter;
            //if (splitTank)
            //{
            //    currentIntertankModule.updateScaleForDiameter(currentTankDiameter);
            //    float lowerDiameter = currentTankDiameter * 0.75f;
            //    float lowerHeight = currentTankHeight * 0.75f;
            //    mountDiameterScale = lowerDiameter;
            //    lowerTopCapModule.updateScaleForDiameter(lowerDiameter);
            //    lowerModule.updateScaleForHeightAndDiameter(lowerHeight, lowerDiameter);
            //    lowerBottomCapModule.updateScaleForDiameter(lowerDiameter);
            //}            
            //currentMountModule.updateScaleForDiameter(mountDiameterScale);
            //rcsModule.updateScaleForDiameter(mountDiameterScale);
        }
                
        /// <summary>
        /// Updated the modules internal cached position value.  This value is used later to update the actual model positions.
        /// </summary>
        private void updateModulePositions()
        {
            //float totalHeight = 0;
            //totalHeight += upperDomeModule.currentHeight;
            //totalHeight += upperTopCapModule.currentHeight;
            //totalHeight += upperModule.currentHeight;
            //totalHeight += upperBottomCapModule.currentHeight;
            //if (splitTank)
            //{
            //    totalHeight += currentIntertankModule.currentHeight;
            //    totalHeight += lowerTopCapModule.currentHeight;
            //    totalHeight += lowerModule.currentHeight;
            //    totalHeight += lowerBottomCapModule.currentHeight;
            //}
            //totalHeight += currentMountModule.currentHeight;
            
            ////start height = total height * 0.5
            //float startY = totalHeight * 0.5f;
            //partTopY = startY;
            //partBottomY = -partTopY;

            ////next 'position' is the origin for the dome and start for the fairings
            //startY -= upperDomeModule.currentHeight;
            //topFairingBottomY = startY;
            //upperDomeModule.currentVerticalPosition = startY;

            ////next position is the origin for the upper-tank-top-cap portion of the model
            //startY -= upperTopCapModule.currentHeight;
            //upperTopCapModule.currentVerticalPosition = startY;

            ////next position is the origin for the upper-tank stretchable model; it uses a center-origin system, so position it using half of its height            
            //startY -= upperModule.currentHeight * 0.5f;
            //upperModule.currentVerticalPosition = startY;

            ////next position is the origin for the upper-tank-lower-cap
            //startY -= upperModule.currentHeight * 0.5f;//finish moving downward for the upper-tank-stretch segment
            //startY -= upperTopCapModule.currentHeight;
            //upperBottomCapModule.currentVerticalPosition = startY;
            
            ////next position the split-tank elements if ST is enabled            
            //if (splitTank)
            //{
            //    //move downward for the intertank height
            //    startY -= currentIntertankModule.currentHeight;
            //    currentIntertankModule.currentVerticalPosition = startY;

            //    //move downward for the lower tank top cap
            //    startY -= lowerTopCapModule.currentHeight;
            //    lowerTopCapModule.currentVerticalPosition = startY;

            //    //move downward for half height of the lower stretch tank
            //    startY -= lowerModule.currentHeight * 0.5f;
            //    lowerModule.currentVerticalPosition = startY;
            //    startY -= lowerModule.currentHeight * 0.5f;

            //    //move downward for the lower tank bottom cap 
            //    startY -= lowerBottomCapModule.currentHeight;
            //    lowerBottomCapModule.currentVerticalPosition = startY;                
            //}

            ////and should already be positioned properly for the mount
            //currentMountModule.currentVerticalPosition = startY;
            //rcsModule.currentVerticalPosition = currentMountModule.currentVerticalPosition + (currentMountModule.modelDefinition.rcsVerticalPosition * currentMountModule.currentHeightScale);
            //rcsModule.currentHorizontalPosition = currentMountModule.modelDefinition.rcsHorizontalPosition * currentMountModule.currentDiameterScale;
            //rcsModule.mountVerticalRotation = currentMountModule.modelDefinition.rcsVerticalRotation;
            //rcsModule.mountHorizontalRotation = currentMountModule.modelDefinition.rcsHorizontalRotation;

            //if (splitTank)
            //{
            //    bottomFairingTopY = upperBottomCapModule.currentVerticalPosition;
            //}
            //else
            //{
            //    bottomFairingTopY = currentMountModule.currentVerticalPosition;
            //    bottomFairingTopY += currentMountModule.modelDefinition.fairingTopOffset * currentMountModule.currentHeightScale;
            //}
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
            
            if (!String.IsNullOrEmpty(noseInterstageNodeName))
            {
                Vector3 pos = new Vector3(0, bottomFairingTopY, 0);
                SSTUSelectableNodes.updateNodePosition(part, noseInterstageNodeName, pos);
                AttachNode interstage = part.FindAttachNode(noseInterstageNodeName);
                if (interstage != null)
                {
                    Vector3 orientation = new Vector3(0, -1, 0);
                    SSTUAttachNodeUtils.updateAttachNodePosition(part, interstage, pos, orientation, userInput);
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
            //Transform modelBase = part.transform.FindRecursive("model").FindOrCreate(baseTransformName);

            //setupModel(upperDomeModule, modelBase, ModelOrientation.CENTRAL);
            //setupModel(upperTopCapModule, modelBase, ModelOrientation.CENTRAL);
            //setupModel(upperModule, modelBase, ModelOrientation.CENTRAL);
            //setupModel(upperBottomCapModule, modelBase, ModelOrientation.CENTRAL);
            
            //if (splitTank)
            //{
            //    if (currentIntertankModule.name != defaultIntertank)
            //    {
            //        SingleModelData dim = Array.Find<SingleModelData>(intertankModules, l => l.name == defaultIntertank);
            //        dim.setupModel(modelBase, ModelOrientation.CENTRAL);
            //        removeCurrentModel(dim);
            //    }                
            //    setupModel(currentIntertankModule, modelBase, ModelOrientation.CENTRAL);
            //    setupModel(lowerTopCapModule, modelBase, ModelOrientation.CENTRAL);
            //    setupModel(lowerModule, modelBase, ModelOrientation.CENTRAL);
            //    setupModel(lowerBottomCapModule, modelBase, ModelOrientation.CENTRAL);
            //}
            //if (currentMountModule.name != defaultMount)
            //{
            //    SingleModelData dmm = Array.Find<SingleModelData>(mountModules, l => l.name == defaultMount);
            //    dmm.setupModel(modelBase, ModelOrientation.BOTTOM);
            //    removeCurrentModel(dmm);
            //}

            //setupModel(currentMountModule, modelBase, ModelOrientation.BOTTOM);
            //setupModel(rcsModule, part.transform.FindRecursive("model").FindOrCreate(rcsTransformName), ModelOrientation.CENTRAL);
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
            //upperDomeModule.updateModel();
            //upperTopCapModule.updateModel();
            //upperModule.updateModel();
            //upperBottomCapModule.updateModel();

            //if (splitTank)
            //{
            //    currentIntertankModule.updateModel();
            //    lowerTopCapModule.updateModel();
            //    lowerModule.updateModel();
            //    lowerBottomCapModule.updateModel();
            //}

            //currentMountModule.updateModel();
            //rcsModule.updateModel();

            //SSTUModInterop.onPartGeometryUpdate(part, true);
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
            //totalTankVolume = 0;
            //totalTankVolume += upperDomeModule.getModuleVolume();
            //totalTankVolume += upperTopCapModule.getModuleVolume();
            //totalTankVolume += upperModule.getModuleVolume();
            //totalTankVolume += upperBottomCapModule.getModuleVolume();
            //if(splitTank)
            //{
            //    totalTankVolume += currentIntertankModule.getModuleVolume();
            //    totalTankVolume += lowerTopCapModule.getModuleVolume();
            //    totalTankVolume += lowerModule.getModuleVolume();
            //    totalTankVolume += lowerBottomCapModule.getModuleVolume();
            //}
            //totalTankVolume += currentMountModule.getModuleVolume();
        }

        /// <summary>
        /// Updates the cached part-mass value from the calculated masses of the current modules/tank setup.  Safe to call..whenever.
        /// Does -not- update part masses.  See -updatePartMass()- for that function.
        /// </summary>
        private void updateModuleMass()
        {
            //moduleMass = upperTopCapModule.getModuleMass() + upperModule.getModuleMass() + upperBottomCapModule.getModuleMass() + currentMountModule.getModuleMass() + rcsModule.getModuleMass()+upperDomeModule.getModuleMass();
            //if (splitTank)
            //{
            //    moduleMass += currentIntertankModule.getModuleMass() + lowerModule.getModuleMass() + lowerBottomCapModule.getModuleMass() + lowerTopCapModule.getModuleMass(); ;
            //}
        }
                
        /// <summary>
        /// Updates the tankCost field with the current cost for the selected fuel type and tank size, including cost for tankage
        /// </summary>
        private void updateModuleCost()
        {
            //moduleCost = upperTopCapModule.getModuleCost() + upperModule.getModuleCost() + upperBottomCapModule.getModuleCost() + currentMountModule.getModuleCost() + rcsModule.getModuleCost()+upperDomeModule.getModuleCost();
            //if (splitTank)
            //{
            //    moduleCost += currentIntertankModule.getModuleCost() + lowerModule.getModuleCost() + lowerBottomCapModule.getModuleCost()+lowerTopCapModule.getModuleCost();
            //}
        }

        /// <summary>
        /// update external RCS-module with thrust value;
        /// TODO - may need to cache the 'needs update' flag, and run on first OnUpdate/etc, as otherwise the RCS module will likely not exist yet
        /// </summary>
        private void updateRCSThrust()
        {
            //ModuleRCS[] rcsMod = part.GetComponents<ModuleRCS>();
            //int len = rcsMod.Length;
            //float scale = currentTankDiameter / defaultTankDiameter;
            //rcsThrust = defaultRcsThrust * scale * scale;
            //for (int i = 0; i < len; i++)
            //{
            //    rcsMod[i].thrusterPower = rcsThrust;
            //}
        }

        /// <summary>
        /// Updates current gui button availability status as well as updating the visible GUI variables from internal state vars
        /// </summary>
        private void updateGuiState()
        {
            guiTankHeight = noseModule.model.currentHeight + upperModule.model.currentHeight + mountModule.model.currentHeight;
            if (splitTank) { guiTankHeight += intertankModule.model.currentHeight + lowerModule.model.currentHeight; }
            guiRcsThrust = rcsThrust;
        }

        private Transform getRootTransform(String name, bool recreate)
        {
            Transform modelBase = part.transform.FindRecursive("model");
            Transform root = modelBase.FindRecursive(name);
            if (root == null || recreate)
            {
                if (root != null) { GameObject.DestroyImmediate(root.gameObject); }
                root = new GameObject(name).transform;
                root.NestToParent(modelBase);
            }
            return root;
        }

        #endregion

        #region ----------------- REGION - Part Updating - Resource/Mass ----------------- 

        /// <summary>
        /// Updates the min/max quantities of resource in the part based on the current 'totalFuelVolume' field and currently set fuel type
        /// </summary>
        private void updateContainerVolume()
        {
            SSTUVolumeContainer vc = part.GetComponent<SSTUVolumeContainer>();
            if (vc != null)
            {
                float tankPercent = 100 - supportPercent;
                float monoPercent = supportPercent;
                float[] pcts = new float[2];
                pcts[0] = tankPercent * 0.01f;
                pcts[1] = monoPercent * 0.01f;
                vc.setContainerPercents(pcts, totalTankVolume * 1000f);
            }
            else
            {
                //real-fuels handling....
                SSTUModInterop.onPartFuelVolumeUpdate(part, totalTankVolume * 1000f);
            }
        }
        #endregion
    }

    public class SSTUModularUpperStageRCS : SingleModelData
    {
        public GameObject[] models;
        public float currentHorizontalPosition;
        public float modelRotation = 0;
        public float modelHorizontalZOffset = 0;
        public float modelHorizontalXOffset = 0;
        public float modelVerticalOffset = 0;

        public float mountVerticalRotation = 0;
        public float mountHorizontalRotation = 0;

        public SSTUModularUpperStageRCS(ConfigNode node) : base(node)
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
