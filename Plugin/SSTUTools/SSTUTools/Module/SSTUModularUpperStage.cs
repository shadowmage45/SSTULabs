using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUModularUpperStage : PartModule, IPartCostModifier, IPartMassModifier, IRecolorable
    {

        #region ----------------- REGION - Standard KSP-accessible config fields -----------------

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
        public String noseInterstageNode = "noseInterstage";

        /// <summary>
        /// Name of the 'interstage' node; positioned depending on mount interstage location (CB) / bottom of the upper tank (ST).
        /// </summary>
        [KSPField]
        public String mountInterstageNode = "mountInterstage";

        /// <summary>
        /// A thrust transform of this name is created in the prefab part in order for the ModuleRCS to initialize properly even when no RCS model is present.
        /// This name -must- match the name in the ModuleRCS, as this data is needed prior to the ModuleRCS loading its config data (??unconfirmed)
        /// </summary>
        [KSPField]
        public String rcsThrustTransformName = "thrustTransform";
        
        /// <summary>
        /// RealFuels compatibility config field, set to false when RF is in use to let RF handle mass/cost updates -- TODO not sure if it needs to be true or false
        /// </summary>
        [KSPField]
        public bool subtractMass = false;

        /// <summary>
        /// RealFuels compatibility config field, set to false when RF is in use to let RF handle mass/cost updates -- TODO not sure if it needs to be true or false
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
        /// quick/dirty/easy flag to determine if should even attempt to load/manipulate split-tank elements
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Tank Type"),
         UI_Toggle(disabledText = "CommonBulkhead", enabledText = "SplitTank", suppressEditorShipModified = true)]
        public bool splitTank = true;

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
        public float currentTankHeight = 1f;

        /// <summary>
        /// The currently selected/enabled nose option
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Nose"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentNose = String.Empty;

        /// <summary>
        /// The currently selected/enabled nose option
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Upper Tank"),
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
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Lower Tank"),
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
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "RCS"),
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

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "RCS Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentRCSTexture = String.Empty;

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

        [KSPField(isPersistant = true)]
        public string rcsPersistentData;

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

        public GameObject[] rcsThrustTransforms = null;

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
        private float rcsThrust = -1;

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
                Fields[nameof(currentIntertankTexture)].guiActiveEditor = false;
                Fields[nameof(currentLowerTexture)].guiActiveEditor = false;
            }

            Action<SSTUModularUpperStage> modelChangeAction = m =>
            {
                m.updateModules(true);
                m.updateModels();
                m.updateTankStats();
                m.updateRCSThrust();
                m.updateContainerVolume();
                m.updateGuiState();
            };

            Fields[nameof(splitTank)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.splitTank = splitTank;
                    if (m.splitTank)
                    {
                        m.intertankModule.setupModel();
                        m.lowerModule.setupModel();
                    }
                    else
                    {
                        m.intertankModule.model.destroyCurrentModel();
                        m.lowerModule.model.destroyCurrentModel();
                    }
                    m.Fields[nameof(currentIntertank)].guiActiveEditor = m.splitTank;
                    m.Fields[nameof(currentLower)].guiActiveEditor = m.splitTank;
                    m.Fields[nameof(currentIntertankTexture)].guiActiveEditor = m.splitTank;
                    m.Fields[nameof(currentLowerTexture)].guiActiveEditor = m.splitTank;
                    modelChangeAction(m);
                });
            };

            Fields[nameof(currentNose)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                noseModule.modelSelected(currentNose);
                this.actionWithSymmetry(modelChangeAction);
            };

            Fields[nameof(currentUpper)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                upperModule.modelSelected(currentUpper);
                this.actionWithSymmetry(modelChangeAction);
            };

            Fields[nameof(currentIntertank)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                intertankModule.modelSelected(currentIntertank);
                this.actionWithSymmetry(modelChangeAction);
            };

            Fields[nameof(currentLower)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                lowerModule.modelSelected(currentLower);
                this.actionWithSymmetry(modelChangeAction);
            };

            Fields[nameof(currentMount)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                mountModule.modelSelected(currentMount);
                this.actionWithSymmetry(modelChangeAction);
            };

            Fields[nameof(currentRCS)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                rcsModule.modelSelected(currentRCS);
                this.actionWithSymmetry(m => 
                {
                    MonoBehaviour.print("RCS model updated!");
                    m.rebuildRCSThrustTransforms(true);
                    modelChangeAction(m);
                });
            };

            Fields[nameof(currentNoseTexture)].uiControlEditor.onFieldChanged = noseModule.textureSetSelected;
            Fields[nameof(currentUpperTexture)].uiControlEditor.onFieldChanged = upperModule.textureSetSelected;
            Fields[nameof(currentMountTexture)].uiControlEditor.onFieldChanged = mountModule.textureSetSelected;
            Fields[nameof(currentIntertankTexture)].uiControlEditor.onFieldChanged = intertankModule.textureSetSelected;
            Fields[nameof(currentLowerTexture)].uiControlEditor.onFieldChanged = lowerModule.textureSetSelected;
            Fields[nameof(currentRCSTexture)].uiControlEditor.onFieldChanged = rcsModule.textureSetSelected;

            Callback<BaseField, System.Object> editorUpdateDelegate = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentTankDiameter = currentTankDiameter; }//else it conflicts with stock slider functionality
                    if (m != this) { m.currentTankHeight = currentTankHeight; }
                    m.updateModules(true);
                    m.updateModels();
                    m.updateTankStats();
                    m.updateContainerVolume();
                    m.updateGuiState();
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentTankDiameter)].uiControlEditor.onFieldChanged = editorUpdateDelegate;

            Fields[nameof(currentTankHeight)].uiControlEditor.onFieldChanged = editorUpdateDelegate;

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
            return "This part has configurable diameter, height, nose, tanks, mount, and fairings.";
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
            noseModule = new ModelModule<SingleModelData>(part, this, getRootTransform("MUSNose"), ModelOrientation.TOP, nameof(nosePersistentData), nameof(currentNose), nameof(currentNoseTexture));
            noseModule.getSymmetryModule = delegate (PartModule m) { return ((SSTUModularUpperStage)m).noseModule; };
            noseModule.setupModelList(SingleModelData.parseModels(node.GetNodes("NOSE")));
            noseModule.setupModel();

            upperModule = new ModelModule<SingleModelData>(part, this, getRootTransform("MUSUpper"), ModelOrientation.TOP, nameof(upperPersistentData), nameof(currentUpper), nameof(currentUpperTexture));
            upperModule.getSymmetryModule = delegate (PartModule m) { return ((SSTUModularUpperStage)m).upperModule; };
            upperModule.setupModelList(SingleModelData.parseModels(node.GetNodes("UPPER")));
            upperModule.setupModel();
            intertankModule = new ModelModule<SingleModelData>(part, this, getRootTransform("MUSIntertank"), ModelOrientation.TOP, nameof(intertankPersistentData), nameof(currentIntertank), nameof(currentIntertankTexture));
            intertankModule.getSymmetryModule = delegate (PartModule m) { return ((SSTUModularUpperStage)m).intertankModule; };
            intertankModule.setupModelList(SingleModelData.parseModels(node.GetNodes("INTERTANK")));
            intertankModule.setupModel();

            lowerModule = new ModelModule<SingleModelData>(part, this, getRootTransform("MUSLower"), ModelOrientation.TOP, nameof(lowerPersistentData), nameof(currentLower), nameof(currentLowerTexture));
            lowerModule.getSymmetryModule = delegate (PartModule m) { return ((SSTUModularUpperStage)m).lowerModule; };
            lowerModule.setupModelList(SingleModelData.parseModels(node.GetNodes("LOWER")));
            lowerModule.setupModel();

            mountModule = new ModelModule<SingleModelData>(part, this, getRootTransform("MUSMount"), ModelOrientation.BOTTOM, nameof(mountPersistentData), nameof(currentMount), nameof(currentMountTexture));
            mountModule.getSymmetryModule = delegate (PartModule m) { return ((SSTUModularUpperStage)m).mountModule; };
            mountModule.setupModelList(SingleModelData.parseModels(node.GetNodes("MOUNT")));
            mountModule.setupModel();

            rcsModule = new ModelModule<SSTUModularUpperStageRCS>(part, this, getRootTransform("MUSRCS"), ModelOrientation.CENTRAL, nameof(rcsPersistentData), nameof(currentRCS), nameof(currentRCSTexture));
            rcsModule.getSymmetryModule = delegate (PartModule m) { return ((SSTUModularUpperStage)m).rcsModule; };
            rcsModule.setupModelList(SingleModelData.parseModels(node.GetNodes("RCS"), m=> new SSTUModularUpperStageRCS(m)));
            rcsModule.setupModel();
            rebuildRCSThrustTransforms(false);

            if (!splitTank)
            {
                intertankModule.model.destroyCurrentModel();
                lowerModule.model.destroyCurrentModel();
            }
        }

        #endregion

        #region ----------------- REGION - Module Position / Parameter Updating ----------------- 

        /// <summary>
        /// Updates the internal cached scale of each of the modules; applied to models later
        /// </summary>
        private void updateModuleScales()
        {
            noseModule.model.updateScaleForDiameter(currentTankDiameter);

            float hScale, vScale;
            hScale = currentTankDiameter / upperModule.model.modelDefinition.diameter;
            vScale = hScale * currentTankHeight;
            upperModule.model.updateScale(hScale, vScale);
            if (splitTank)
            {
                intertankModule.model.updateScaleForDiameter(currentTankDiameter * 0.75f);
                hScale = (currentTankDiameter * 0.75f) / lowerModule.model.modelDefinition.diameter;
                vScale = hScale * currentTankHeight;
                lowerModule.model.updateScale(hScale, vScale);
            }
            mountModule.model.updateScaleForDiameter(splitTank ? currentTankDiameter * 0.75f : currentTankDiameter);
            rcsModule.model.updateScaleForDiameter(splitTank ? currentTankDiameter * 0.75f : currentTankDiameter);
        }
                
        /// <summary>
        /// Updated the models position values and calculates fairing and attach node locations
        /// </summary>
        private void updateModulePositions()
        {
            float totalHeight = 0;
            totalHeight += noseModule.model.currentHeight;
            totalHeight += upperModule.model.currentHeight;
            if (splitTank)
            {
                totalHeight += intertankModule.model.currentHeight;
                totalHeight += lowerModule.model.currentHeight;
            }
            totalHeight += mountModule.model.currentHeight;

            partTopY = totalHeight * 0.5f;
            partBottomY = -(totalHeight * 0.5f);//bottom attach node location, and bottom fairing location

            float start = partTopY;
            start -= noseModule.model.currentHeight;
            noseModule.model.setPosition(start, ModelOrientation.TOP);

            start -= upperModule.model.currentHeight;
            upperModule.model.setPosition(start, upperModule.model.modelDefinition.orientation);

            if (splitTank)
            {
                start -= intertankModule.model.currentHeight;
                intertankModule.model.setPosition(start, ModelOrientation.TOP);

                start -= lowerModule.model.currentHeight;
                lowerModule.model.setPosition(start, lowerModule.model.modelDefinition.orientation);
            }

            mountModule.model.setPosition(start, ModelOrientation.BOTTOM);

            rcsModule.model.setPosition(start + mountModule.model.currentHeightScale * mountModule.model.modelDefinition.rcsVerticalPosition);            
            rcsModule.model.currentHorizontalPosition = mountModule.model.modelDefinition.rcsHorizontalPosition * mountModule.model.currentDiameterScale;
            rcsModule.model.mountVerticalRotation = mountModule.model.modelDefinition.rcsVerticalRotation;
            rcsModule.model.mountHorizontalRotation = mountModule.model.modelDefinition.rcsHorizontalRotation;

            topFairingBottomY = noseModule.model.currentVerticalPosition + noseModule.model.modelDefinition.fairingTopOffset * noseModule.model.currentDiameterScale;
            if (splitTank)
            {
                bottomFairingTopY = intertankModule.model.currentVerticalPosition + intertankModule.model.modelDefinition.fairingTopOffset * intertankModule.model.currentDiameterScale;
            }
            else
            {
                bottomFairingTopY = mountModule.model.currentVerticalPosition + mountModule.model.modelDefinition.fairingTopOffset * mountModule.model.currentDiameterScale;
            }

            guiTankHeight = totalHeight;
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
            if (topNode != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, topNode, new Vector3(0, partTopY, 0), topNode.orientation, userInput);
            }

            AttachNode bottomNode = part.FindAttachNode("bottom");
            if (bottomNode != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, bottomNode, new Vector3(0, partBottomY, 0), bottomNode.orientation, userInput);
            }

            Vector3 pos = new Vector3(0, topFairingBottomY, 0);
            SSTUSelectableNodes.updateNodePosition(part, noseInterstageNode, pos);
            AttachNode noseInterstage = part.FindAttachNode(noseInterstageNode);
            if (noseInterstage != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, noseInterstage, pos, Vector3.up, userInput);
            }

            pos = new Vector3(0, bottomFairingTopY, 0);
            SSTUSelectableNodes.updateNodePosition(part, mountInterstageNode, pos);
            AttachNode mountInterstage = part.FindAttachNode(mountInterstageNode);
            if (mountInterstage != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, mountInterstage, pos, Vector3.down, userInput);
            }

            if (userInput)
            {
                //TODO -- cache prev tank diameter somewhere, use that for child offset functionality
                //SSTUAttachNodeUtils.updateSurfaceAttachedChildren(part, null, currentTankDiameter);
            }
        }

        /// <summary>
        /// Updates models from module current parameters for scale and positioning
        /// </summary>
        private void updateModels()
        {
            noseModule.model.updateModel();
            upperModule.model.updateModel();
            if (splitTank)
            {
                intertankModule.model.updateModel();
                lowerModule.model.updateModel();
            }
            mountModule.model.updateModel();
            rcsModule.model.updateModel();
            rcsModule.model.updateThrustTransformPositions(rcsThrustTransforms);
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
        }
        
        /// <summary>
        /// Calculates the internal volume from all of the currentl selected parts, their configurations, and their current scales
        /// </summary>
        private void updateFuelVolume()
        {
            totalTankVolume = 0;
            totalTankVolume += noseModule.model.getModuleVolume();
            totalTankVolume += upperModule.model.getModuleVolume();
            if (splitTank)
            {
                totalTankVolume += intertankModule.model.getModuleVolume();
                totalTankVolume += lowerModule.model.getModuleVolume();
            }
            totalTankVolume += mountModule.model.getModuleVolume();
        }

        /// <summary>
        /// Updates the cached part-mass value from the calculated masses of the current modules/tank setup.  Safe to call..whenever.
        /// Does -not- update part masses.  See -updatePartMass()- for that function.
        /// </summary>
        private void updateModuleMass()
        {
            moduleMass = 0;
            moduleMass += noseModule.model.getModuleMass();
            moduleMass += upperModule.model.getModuleMass();
            if (splitTank)
            {
                moduleMass += intertankModule.model.getModuleMass();
                moduleMass += lowerModule.model.getModuleMass();
            }
            moduleMass += mountModule.model.getModuleMass();
            moduleMass += rcsModule.model.getModuleMass();
        }
                
        /// <summary>
        /// Updates the tankCost field with the current cost for the selected fuel type and tank size, including cost for tankage
        /// </summary>
        private void updateModuleCost()
        {
            moduleCost = 0;
            moduleCost += noseModule.model.getModuleCost();
            moduleCost += upperModule.model.getModuleCost();
            if (splitTank)
            {
                moduleCost += intertankModule.model.getModuleCost();
                moduleCost += lowerModule.model.getModuleCost();
            }
            moduleCost += mountModule.model.getModuleCost();
            moduleCost += rcsModule.model.getModuleCost();
        }

        /// <summary>
        /// update external RCS-module with thrust value;
        /// </summary>
        private void updateRCSThrust()
        {
            ModuleRCS[] rcsMod = part.GetComponents<ModuleRCS>();
            int len = rcsMod.Length;
            float scale = currentTankDiameter / upperModule.model.modelDefinition.diameter;
            if (rcsThrust < 0 && len>0)
            {
                rcsThrust = rcsMod[0].thrusterPower;
            }
            float thrust = rcsThrust * scale * scale;
            if (rcsModule.model.dummyModel) { thrust = 0; }
            for (int i = 0; i < len; i++)
            {
                rcsMod[i].thrusterPower = thrust;
                rcsMod[i].moduleIsEnabled = thrust > 0;
            }
            guiRcsThrust = thrust;
        }

        private void rebuildRCSThrustTransforms(bool updateRCSModule)
        {
            if (rcsThrustTransforms != null)
            {
                //destroy immediate on existing, or optionally attempt to copy and re-use some of them?
                int l = rcsThrustTransforms.Length;
                for (int i = 0; i < l; i++)
                {
                    rcsThrustTransforms[i].transform.parent = null;//so that it doesn't get found by the rcs module, free-floating transform for one frame until destroyed
                    GameObject.Destroy(rcsThrustTransforms[i]);//destroy
                    rcsThrustTransforms[i] = null;//dereference
                }
                rcsThrustTransforms = null;//dump the whole array
            }
            rcsThrustTransforms = rcsModule.model.createThrustTransforms(rcsThrustTransformName, part.transform.FindRecursive("model"));
            if (updateRCSModule)
            {
                ModuleRCS[] modules = part.GetComponents<ModuleRCS>();
                int len = modules.Length;
                for (int i = 0; i < len; i++)
                {
                    part.fxGroups.RemoveAll(m => modules[i].thrusterFX.Contains(m));
                    part.fxGroups.ForEach(m => MonoBehaviour.print(m.name));
                    modules[i].thrusterFX.ForEach(m => m.fxEmitters.ForEach(s => GameObject.Destroy(s.gameObject)));
                    modules[i].thrusterFX.Clear();
                    modules[i].thrusterTransforms.Clear();//clear, in case it is holding refs to the old ones that were just unparented/destroyed
                    modules[i].OnStart(StartState.Editor);//force update of fx/etc
                    modules[i].DeactivateFX();//doesn't appear to work
                    //TODO -- clean up this mess of linked stuff
                    modules[i].thrusterFX.ForEach(m => 
                    {
                        m.setActive(false);
                        m.SetPower(0);
                        m.fxEmitters.ForEach(s => s.enabled = false);
                    });
                }
            }
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

        private Transform getRootTransform(String name, bool recreate = true)
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
        public string thrustTransformName;
        public bool dummyModel = false;
        public float currentHorizontalPosition;
        public float modelRotation = 0;
        public float modelHorizontalZOffset = 0;
        public float modelHorizontalXOffset = 0;
        public float modelVerticalOffset = 0;

        public float mountVerticalRotation = 0;
        public float mountHorizontalRotation = 0;

        public SSTUModularUpperStageRCS(ConfigNode node) : base(node)
        {
            dummyModel = node.GetBoolValue("dummyModel");
            modelRotation = node.GetFloatValue("modelRotation");
            modelHorizontalZOffset = node.GetFloatValue("modelHorizontalZOffset");
            modelHorizontalXOffset = node.GetFloatValue("modelHorizontalXOffset");
            modelVerticalOffset = node.GetFloatValue("modelVerticalOffset");            
            thrustTransformName = modelDefinition.configNode.GetStringValue("thrustTransformName");
        }
        
        public override void setupModel(Transform parent, ModelOrientation orientation)
        {
            model = new GameObject(modelDefinition.name);
            model.transform.NestToParent(parent);
            if (models != null) { destroyCurrentModel(); }
            models = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                models[i] = SSTUUtils.cloneModel(modelDefinition.modelName);
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

        public override void destroyCurrentModel()
        {
            if (models == null) { return; }
            int len = models.Length;
            for (int i = 0; i < len; i++)
            {
                if (models[i] == null) { continue; }
                models[i].transform.parent = null;
                GameObject.Destroy(models[i]);
                models[i] = null;
            }
            models = null;
        }

        public GameObject[] createThrustTransforms(string name, Transform parent)
        {
            MonoBehaviour.print("Creating new thrust transforms");
            if (dummyModel)
            {
                GameObject[] dumArr = new GameObject[1];
                dumArr[0] = new GameObject(name);
                dumArr[0].transform.NestToParent(parent);
                return dumArr;
            }
            int len = 4, len2;
            List<GameObject> goList = new List<GameObject>();
            Transform[] trs;
            GameObject go;
            for (int i = 0; i < len; i++)
            {
                trs = models[i].transform.FindChildren(thrustTransformName);
                len2 = trs.Length;
                for (int k = 0; k < len2; k++)
                {
                    go = new GameObject(name);
                    go.transform.NestToParent(parent);
                    goList.Add(go);
                }
            }
            return goList.ToArray();
        }

        public void updateThrustTransformPositions(GameObject[] gos)
        {
            MonoBehaviour.print("Updating transform positions");
            if (dummyModel) { return; }
            Transform[] trs;
            int len;
            GameObject go;
            int index = 0;
            int goLen = gos.Length;
            for (int i = 0; i < 4; i++)
            {
                trs = models[i].transform.FindChildren(thrustTransformName);
                len = trs.Length;
                for (int k = 0; k < len && index < goLen; k++, index++)
                {
                    go = gos[index];
                    go.transform.position = trs[k].position;
                    go.transform.rotation = trs[k].rotation;
                }
            }
        }

    }

}
