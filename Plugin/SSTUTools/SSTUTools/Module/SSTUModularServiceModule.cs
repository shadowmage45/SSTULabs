using System;
using System.Collections.Generic;
using UnityEngine;
using KSPShaderTools;

namespace SSTUTools
{

    public class SSTUModularServiceModule : PartModule, IPartMassModifier, IPartCostModifier, IRecolorable
    {

        #region REGION - Standard Part Config Fields

        [KSPField]
        public float diameterIncrement = 0.625f;

        [KSPField]
        public float minDiameter = 0.625f;

        [KSPField]
        public float maxDiameter = 10f;

        [KSPField]
        public float fairingRatio = 1.0f;

        [KSPField]
        public bool useAdapterVolume = false;

        [KSPField]
        public bool useAdapterMass = false;

        [KSPField]
        public bool useAdapterCost = false;

        [KSPField]
        public bool updateSolar = true;

        [KSPField]
        public string solarAnimationID = "solarDeploy";

        [KSPField]
        public string bayAnimationID = "bayDeploy";

        [KSPField]
        public string rcsThrustTransformName = "RCSThrustTransform";

        [KSPField]
        public string topManagedNodes = "top1, top2";

        [KSPField]
        public string bottomManagedNodes = "bottom1, bottom2";

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

        //persistent config fields for module selections
        //also GUI controls for module selection

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Diameter"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentDiameter = 2.5f;

        [KSPField(isPersistant = true, guiName = "Top"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentTop = "Mount-None";

        [KSPField(isPersistant = true, guiName = "Core"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCore = "Mount-None";

        [KSPField(isPersistant = true, guiName = "Bottom"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentBottom = "Mount-None";

        [KSPField(isPersistant = true, guiName = "Solar"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentSolar = "Solar-None";

        [KSPField(isPersistant = true, guiName = "RCS"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentRCS = "MUS-RCS1";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "RCS V.Offset"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true, minValue = 0, maxValue = 1, incrementLarge = 0.5f, incrementSmall = 0.25f, incrementSlide = 0.01f)]
        public float currentRCSOffset = 0f;

        //persistent config fields for module texture sets
        //also GUI controls for texture selection
        [KSPField(isPersistant = true, guiName = "Top Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentTopTexture = "default";

        [KSPField(isPersistant = true, guiName = "Core Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCoreTexture = "default";

        [KSPField(isPersistant = true, guiName = "Bottom Tex"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentBottomTexture = "default";

        //persistent data for modules; stores colors and other per-module data
        [KSPField(isPersistant = true)]
        public string topModulePersistentData;
        [KSPField(isPersistant = true)]
        public string coreModulePersistentData;
        [KSPField(isPersistant = true)]
        public string bottomModulePersistentData;

        //tracks if default textures and resource volumes have been initialized; only occurs once during the parts first Start() call
        [KSPField(isPersistant = true)]
        public bool initializedDefaults = false;

        //standard work-around for lack of config-node data being passed consistently and lack of support for mod-added serializable classes
        [Persistent]
        public string configNodeData = string.Empty;

        #endregion REGION - Standard Part Config Fields

        #region REGION - Private working vars

        private bool initialized = false;
        private float modifiedMass = 0;
        private float modifiedCost = 0;
        private string[] topNodeNames;
        private string[] bottomNodeNames;
        
        ModelModule<SingleModelData, SSTUModularServiceModule> topModule;
        ModelModule<ServiceModuleCoreModel, SSTUModularServiceModule> coreModule;
        ModelModule<SingleModelData, SSTUModularServiceModule> bottomModule;
        ModelModule<SolarData, SSTUModularServiceModule> solarModule;
        ModelModule<ServiceModuleRCSModelData, SSTUModularServiceModule> rcsModule;

        //animate controlled reference for solar panel animation module
        private SSTUAnimateControlled solarAnimationControl;

        //animate controlled reference for service bay animation module
        private SSTUAnimateControlled bayAnimationControl;

        /// <summary>
        /// ref to the ModularRCS module that updates fuel type and thrust for RCS
        /// </summary>
        private SSTUModularRCS modularRCSControl;

        #endregion ENDREGION - Private working vars

        #region REGION - Standard KSP Overrides

        //standard KSP lifecyle override
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize(false);
        }

        //standard KSP lifecyle override
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize(true);

            Action<SSTUModularServiceModule> modelChangedAction = delegate (SSTUModularServiceModule m)
            {
                m.updateModulePositions();
                m.updateMassAndCost();
                m.updateAttachNodes(true);
                m.updateDragCubes();
                m.updateResourceVolume();
                m.updateFairing(true);
                m.updateGUI();
                SSTUModInterop.onPartGeometryUpdate(m.part, true);
            };

            Fields[nameof(currentDiameter)].uiControlEditor.onFieldChanged = delegate (BaseField a, object b)
            {
                this.actionWithSymmetry(m =>
                {
                    modelChangedAction(m);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentTop)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                topModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentCore)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                coreModule.modelSelected(a, b);                
                this.actionWithSymmetry(modelChangedAction);
                if (!coreModule.model.isValidSolarOption(currentSolar, coreModule.model.currentDiameterScale))
                {
                    this.actionWithSymmetry(m => 
                    {
                        m.currentSolar = m.coreModule.model.getAvailableSolarVariants(coreModule.model.currentDiameterScale)[0];
                        m.solarModule.modelSelected(m.currentSolar);
                        modelChangedAction(m);
                        m.updateSolarModules();
                    });
                }
                this.actionWithSymmetry(m => 
                {
                    m.updateBayAnimation();
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentBottom)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                bottomModule.modelSelected(a, b);
                this.actionWithSymmetry(modelChangedAction);
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentSolar)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                solarModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    modelChangedAction(m);
                    m.updateSolarModules();
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentRCS)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                rcsModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.rcsModule.model.renameThrustTransforms(rcsThrustTransformName);
                    modelChangedAction(m);
                    m.updateRCSModule();
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentRCSOffset)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    modelChangedAction(m);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            if (maxDiameter == minDiameter)
            {
                Fields[nameof(currentDiameter)].guiActiveEditor = false;
            }
            else
            {
                this.updateUIFloatEditControl(nameof(currentDiameter), minDiameter, maxDiameter, diameterIncrement * 2, diameterIncrement, diameterIncrement * 0.05f, true, currentDiameter);
            }

            Fields[nameof(currentTopTexture)].uiControlEditor.onFieldChanged = topModule.textureSetSelected;
            Fields[nameof(currentCoreTexture)].uiControlEditor.onFieldChanged = coreModule.textureSetSelected;
            Fields[nameof(currentBottomTexture)].uiControlEditor.onFieldChanged = bottomModule.textureSetSelected;

            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
            updateDragCubes();
        }

        //standard Unity lifecyle override
        public void Start()
        {
            if (!initializedDefaults)
            {
                updateResourceVolume();
            }
            initializedDefaults = true;
            updateSolarModules();
            updateBayAnimation();
            updateRCSModule();
            updateFairing(false);
        }
        
        //standard Unity lifecyle override
        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        //KSP editor modified event callback
        private void onEditorVesselModified(ShipConstruct ship)
        {
            updateGUI();
        }

        //IPartMass/CostModifier override
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        //IPartMass/CostModifier override
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        //IPartMass/CostModifier override
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (modifiedMass == 0) { return 0; }
            return -defaultMass + modifiedMass;
        }

        //IPartMass/CostModifier override
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (modifiedCost == 0) { return 0; }
            return -defaultCost + modifiedCost;
        }

        //IRecolorable override
        public string[] getSectionNames()
        {
            return new string[] { "Top", "Body", "Bottom" };
        }

        //IRecolorable override
        public RecoloringData[] getSectionColors(string section)
        {
            if (section == "Top")
            {
                return topModule.customColors;
            }
            else if (section == "Body")
            {
                return coreModule.customColors;
            }
            else if (section == "Bottom")
            {
                return bottomModule.customColors;
            }
            return coreModule.customColors;
        }

        //IRecolorable override
        public void setSectionColors(string section, RecoloringData[] colors)
        {
            if (section == "Top")
            {
                topModule.setSectionColors(colors);
            }
            else if (section == "Body")
            {
                coreModule.setSectionColors(colors);
            }
            else if (section == "Bottom")
            {
                bottomModule.setSectionColors(colors);
            }
        }

        //IRecolorable override
        public TextureSet getSectionTexture(string section)
        {
            if (section == "Top")
            {
                return topModule.currentTextureSet;
            }
            else if (section == "Body")
            {
                return coreModule.currentTextureSet;
            }
            else if (section == "Bottom")
            {
                return bottomModule.currentTextureSet;
            }
            return coreModule.currentTextureSet;
        }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Custom Update Methods

        private void initialize(bool start)
        {
            if (initialized) { return; }
            initialized = true;

            topNodeNames = SSTUUtils.parseCSV(topManagedNodes);
            bottomNodeNames = SSTUUtils.parseCSV(bottomManagedNodes);

            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);

            coreModule = new ModelModule<ServiceModuleCoreModel, SSTUModularServiceModule>(part, this, getRootTransform("MSC-CORE", true), ModelOrientation.TOP, nameof(coreModulePersistentData), nameof(currentCore), nameof(currentCoreTexture));
            coreModule.getSymmetryModule = m => m.coreModule;
            coreModule.setupModelList(ModelData.parseModels(node.GetNodes("CORE"), m => new ServiceModuleCoreModel(m)));

            topModule = new ModelModule<SingleModelData, SSTUModularServiceModule>(part, this, getRootTransform("MSC-TOP", true), ModelOrientation.TOP, nameof(topModulePersistentData), nameof(currentTop), nameof(currentTopTexture));
            topModule.getSymmetryModule = m => m.topModule;
            topModule.getValidSelections = m => topModule.models.FindAll(s => s.canSwitchTo(part, topNodeNames));

            bottomModule = new ModelModule<SingleModelData, SSTUModularServiceModule>(part, this, getRootTransform("MSC-BOTTOM", true), ModelOrientation.BOTTOM, nameof(bottomModulePersistentData), nameof(currentBottom), nameof(currentBottomTexture));
            bottomModule.getSymmetryModule = m => m.bottomModule;
            bottomModule.getValidSelections = m => bottomModule.models.FindAll(s => s.canSwitchTo(part, bottomNodeNames));

            solarModule = new ModelModule<SolarData, SSTUModularServiceModule>(part, this, getRootTransform("MSC-Solar", true), ModelOrientation.CENTRAL, null, nameof(currentSolar), null);
            solarModule.getSymmetryModule = m => m.solarModule;
            solarModule.setupModelList(ModelData.parseModels(node.GetNodes("SOLAR"), m => new SolarData(m)));
            solarModule.getValidSelections = delegate (IEnumerable<SolarData> all) 
            {
                //System.Linq.Enumerable.Where(all, s => s.isAvailable(upgradesApplied));
                float scale = coreModule.model.currentDiameterScale;
                //find all solar panels that are unlocked via upgrades/tech-tree
                List<SolarData> unlocked = solarModule.models.FindAll(s => s.isAvailable(upgradesApplied));
                //filter those to find only the ones available for the current
                List<SolarData> availableByScale = unlocked.FindAll(s => coreModule.model.isValidSolarOption(s.name, scale));
                return availableByScale;
            };
            solarModule.preModelSetup = delegate (SolarData d) 
            {
                d.positions = coreModule.model.getPanelConfiguration(d.name).getScaledPositions(coreModule.model.currentDiameterScale);
            };

            rcsModule = new ModelModule<ServiceModuleRCSModelData, SSTUModularServiceModule>(part, this, getRootTransform("MSC-Rcs", true), ModelOrientation.CENTRAL, null, nameof(currentRCS), null);
            rcsModule.getSymmetryModule = m => m.rcsModule;
            rcsModule.setupModelList(ModelData.parseModels(node.GetNodes("RCS"), m => new ServiceModuleRCSModelData(m)));
            rcsModule.getValidSelections = m => rcsModule.models.FindAll(s => s.isAvailable(upgradesApplied));

            List<ConfigNode> tops = new List<ConfigNode>();
            List<ConfigNode> bottoms = new List<ConfigNode>();
            ConfigNode[] mNodes = node.GetNodes("CAP");
            ConfigNode mNode;
            int len = mNodes.Length;
            for (int i = 0; i < len; i++)
            {
                mNode = mNodes[i];
                if (mNode.GetBoolValue("useForTop", true)) { tops.Add(mNode); }
                if (mNode.GetBoolValue("useForBottom", true)) { bottoms.Add(mNode); }
            }
            topModule.setupModelList(SingleModelData.parseModels(tops.ToArray()));
            bottomModule.setupModelList(SingleModelData.parseModels(bottoms.ToArray()));

            tops.Clear();
            bottoms.Clear();
            topModule.setupModel();
            coreModule.setupModel();
            coreModule.model.updateScaleForDiameter(currentDiameter);
            bottomModule.setupModel();
            solarModule.setupModel();
            rcsModule.setupModel();

            updateModulePositions();
            updateMassAndCost();
            updateAttachNodes(false);
            SSTUStockInterop.updatePartHighlighting(part);
        }

        private void updateModulePositions()
        {
            //update for model scale
            topModule.model.updateScaleForDiameter(currentDiameter * coreModule.model.topRatio);
            coreModule.model.updateScaleForDiameter(currentDiameter);
            bottomModule.model.updateScaleForDiameter(currentDiameter * coreModule.model.bottomRatio);
            solarModule.model.updateScale(1);
            float coreScale = coreModule.model.currentDiameterScale;
            rcsModule.model.updateScale(coreScale);

            //calc positions
            float yPos = topModule.moduleHeight + (coreModule.moduleHeight * 0.5f);
            yPos -= topModule.moduleHeight;
            float topY = yPos;
            yPos -= coreModule.moduleHeight;
            float coreY = yPos;
            float bottomY = coreY;
            yPos -= bottomModule.moduleHeight;
            float bottomDockY = yPos;

            //update internal ref of position
            topModule.setPosition(topY);
            coreModule.setPosition(coreY);
            solarModule.setPosition(coreY);
            bottomModule.setPosition(bottomY, ModelOrientation.BOTTOM);
            rcsModule.setPosition(coreY + (coreScale * currentRCSOffset * coreModule.model.rcsOffsetRange) + (coreScale * coreModule.model.rcsPosition));

            //update actual model positions and scales
            topModule.updateModel();
            coreModule.updateModel();
            bottomModule.updateModel();
            solarModule.updateModel();
            rcsModule.model.currentHorizontalPosition = coreModule.model.currentDiameterScale * coreModule.model.modelDefinition.rcsHorizontalPosition;
            rcsModule.updateModel();
        }
        
        private void updateResourceVolume()
        {
            float volume = coreModule.moduleVolume;
            if (useAdapterVolume)
            {
                volume += topModule.moduleVolume;
                volume += bottomModule.moduleVolume;
            }
            SSTUModInterop.onPartFuelVolumeUpdate(part, volume * 1000f);
        }
        
        private void updateMassAndCost()
        {
            modifiedMass = coreModule.moduleMass;
            modifiedMass += solarModule.moduleMass;
            if (useAdapterMass)
            {
                modifiedMass += topModule.moduleMass;
                modifiedMass += bottomModule.moduleMass;
            }

            modifiedCost = coreModule.moduleCost;
            modifiedCost += solarModule.moduleCost;
            if (useAdapterCost)
            {
                modifiedCost += topModule.moduleCost;
                modifiedCost += bottomModule.moduleCost;
            }
        }

        private void updateBayAnimation()
        {
            if (bayAnimationControl == null && !string.Equals("none", bayAnimationID))
            {
                SSTUAnimateControlled[] controls = part.GetComponents<SSTUAnimateControlled>();
                int len = controls.Length;
                for (int i = 0; i < len; i++)
                {
                    if (controls[i].animationID == bayAnimationID)
                    {
                        bayAnimationControl = controls[i];
                        break;
                    }
                }
                if (bayAnimationControl == null)
                {
                    MonoBehaviour.print("ERROR: Animation controller was null for ID: " + bayAnimationID);
                    return;
                }
            }

            string animName = string.Empty;
            float animSpeed = 1f;

            if (coreModule.model.hasAnimation())
            {
                ModelAnimationData mad = coreModule.model.modelDefinition.animationData[0];
                animName = mad.animationName;
                animSpeed = mad.speed;
            }

            if (solarAnimationControl != null)
            {
                bayAnimationControl.animationName = animName;
                bayAnimationControl.animationSpeed = animSpeed;
                bayAnimationControl.reInitialize();
            }
        }
        
        private void updateSolarModules()
        {
            if (!updateSolar)
            {
                return;
            }
            if (solarAnimationControl == null && !string.Equals("none", solarAnimationID))
            {
                SSTUAnimateControlled[] controls = part.GetComponents<SSTUAnimateControlled>();
                int len = controls.Length;
                for (int i = 0; i < len; i++)
                {
                    if (controls[i].animationID == solarAnimationID)
                    {
                        solarAnimationControl = controls[i];
                        break;
                    }
                }
                if (solarAnimationControl == null)
                {
                    MonoBehaviour.print("ERROR: Animation controller was null for ID: " + solarAnimationID);
                    return;
                }
            }

            bool animEnabled = solarModule.model.hasAnimation();
            bool solarEnabled = solarModule.model.panelsEnabled;
            string animName = string.Empty;
            float animSpeed = 1f;

            if (animEnabled)
            {
                ModelAnimationData mad = solarModule.model.modelDefinition.animationData[0];
                animName = mad.animationName;
                animSpeed = mad.speed;
            }
            else
            {
                animName = string.Empty;
                animSpeed = 1f;
            }

            if (solarAnimationControl != null)
            {
                solarAnimationControl.animationName = animName;
                solarAnimationControl.animationSpeed = animSpeed;
                solarAnimationControl.reInitialize();
            }

            SSTUSolarPanelDeployable solar = part.GetComponent<SSTUSolarPanelDeployable>();
            if (solar != null)
            {
                if (solarEnabled)
                {
                    solar.resourceAmount = solarModule.model.energy;
                    solar.pivotTransforms = solarModule.model.pivotNames;
                    solar.rayTransforms = solarModule.model.sunNames;

                    SSTUSolarPanelDeployable.Axis axis = (SSTUSolarPanelDeployable.Axis)Enum.Parse(typeof(SSTUSolarPanelDeployable.Axis), solarModule.model.sunAxis);
                    solar.setSuncatcherAxis(axis);
                    solar.enableModule();
                }
                else
                {
                    solar.disableModule();
                }
            }
        }

        private void updateRCSModule()
        {
            modularRCSControl = part.GetComponent<SSTUModularRCS>();
            if (modularRCSControl != null)
            {
                modularRCSControl.Start();
            }
            ModuleRCS rcs = part.GetComponent<ModuleRCS>();
            if (rcs != null)
            {
                rcs.moduleIsEnabled = !rcsModule.model.dummyModel;
               // rcs.OnStart(HighLogic.LoadedSceneIsEditor ? StartState.Editor : StartState.Flying);
            }
        }

        private void updateAttachNodes(bool userInput)
        {
            topModule.model.updateAttachNodes(part, topNodeNames, userInput, ModelOrientation.TOP);
            bottomModule.model.updateAttachNodes(part, bottomNodeNames, userInput, ModelOrientation.BOTTOM);
            
            Vector3 pos = new Vector3(0, getTopFairingBottomY(), 0);
            SSTUSelectableNodes.updateNodePosition(part, noseInterstageNode, pos);
            AttachNode noseInterstage = part.FindAttachNode(noseInterstageNode);
            if (noseInterstage != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, noseInterstage, pos, Vector3.up, userInput);
            }

            float bottomFairingTopY = getBottomFairingTopY();
            pos = new Vector3(0, bottomFairingTopY, 0);
            SSTUSelectableNodes.updateNodePosition(part, mountInterstageNode, pos);
            AttachNode mountInterstage = part.FindAttachNode(mountInterstageNode);
            if (mountInterstage != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, mountInterstage, pos, Vector3.down, userInput);
            }
        }

        private void updateFairing(bool userInput)
        {
            SSTUNodeFairing[] modules = part.GetComponents<SSTUNodeFairing>();
            if (modules == null || modules.Length < 2)
            {
                MonoBehaviour.print("ERROR: Could not locate both fairing modules for part: " + part.name);
                return;
            }
            SSTUNodeFairing topFairing = modules[0];
            if (topFairing != null)
            {
                float topFairingBottomY = getTopFairingBottomY();
                FairingUpdateData data = new FairingUpdateData();
                data.setTopY(getPartTopY());
                data.setBottomY(topFairingBottomY);
                data.setBottomRadius(currentDiameter * 0.5f);
                if (userInput) { data.setTopRadius(currentDiameter * 0.5f); }
                topFairing.updateExternal(data);
            }
            SSTUNodeFairing bottomFairing = modules[1];
            if (bottomFairing != null)
            {
                float bottomFairingTopY = getBottomFairingTopY();
                FairingUpdateData data = new FairingUpdateData();
                data.setTopRadius(currentDiameter * 0.5f);
                data.setTopY(bottomFairingTopY);
                if (userInput) { data.setBottomRadius(currentDiameter * 0.5f); }
                bottomFairing.updateExternal(data);
            }
        }

        private float getPartTopY()
        {
            return coreModule.model.currentHeight * 0.5f + topModule.model.currentHeight;
        }

        private float getTopFairingBottomY()
        {
            return topModule.model.getPosition(ModelOrientation.TOP) + topModule.model.getFairingOffset();
        }

        private float getBottomFairingTopY()
        {
            if (!coreModule.model.modelDefinition.fairingDisabled)
            {
                return coreModule.model.getPosition(ModelOrientation.TOP) + coreModule.model.getFairingOffset();
            }
            return bottomModule.model.getPosition(ModelOrientation.BOTTOM) - bottomModule.model.getFairingOffset();
        }
        
        private void updateGUI()
        {
            topModule.updateSelections();
            bottomModule.updateSelections();
            solarModule.updateSelections();
        }

        private void updateDragCubes()
        {
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        private Transform getRootTransform(string name, bool recreate)
        {
            Transform root = part.transform.FindRecursive(name);
            if (recreate && root != null)
            {
                GameObject.DestroyImmediate(root.gameObject);
                root = null;
            }
            if (root == null)
            {
                root = new GameObject(name).transform;
            }
            root.NestToParent(part.transform.FindRecursive("model"));
            return root;
        }

        #endregion ENDREGION - Custom Update Methods

    }

    public class ServiceModuleCoreModel : SingleModelData
    {
        //list of available solar panel model definitions
        //each one will have a list of 'positions' relative to the unscaled core model
        //each one will list a minimum 'core scale', below which it is unavailable.
        public ServiceModuleSolarPanelConfiguration[] solarConfigs;
        public float rcsOffsetRange = 0f;
        public float rcsPosition = 0f;
        public float topRatio = 1f;
        public float bottomRatio = 1f;

        public ServiceModuleCoreModel(ConfigNode node) : base(node)
        {
            topRatio = modelDefinition.configNode.GetFloatValue("topRatio", topRatio);
            bottomRatio = modelDefinition.configNode.GetFloatValue("bottomRatio", bottomRatio);
            rcsOffsetRange = modelDefinition.configNode.GetFloatValue("rcsOffsetRange", 0f);
            rcsPosition = modelDefinition.configNode.GetFloatValue("rcsPosition", 0f);
            ConfigNode[] solarNodes = modelDefinition.configNode.GetNodes("SOLAR");
            int len = solarNodes.Length;
            solarConfigs = new ServiceModuleSolarPanelConfiguration[len];
            for (int i = 0; i < len; i++)
            {
                solarConfigs[i] = new ServiceModuleSolarPanelConfiguration(solarNodes[i]);
            }
        }

        /// <summary>
        /// Return the list of solar panel variants that are currently available to this body model
        /// </summary>
        /// <param name="scale"></param>
        /// <returns></returns>
        public string[] getAvailableSolarVariants(float scale)
        {
            List<string> vars = new List<string>();
            int len = solarConfigs.Length;
            for (int i = 0; i < len; i++)
            {
                if (scale >= solarConfigs[i].minScale)
                {
                    vars.Add(solarConfigs[i].name);
                }
            }
            return vars.ToArray();
        }

        /// <summary>
        /// Returns if the input solar panel variant is a valid option at the input scale
        /// </summary>
        /// <param name="name"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public bool isValidSolarOption(string name, float scale)
        {
            ServiceModuleSolarPanelConfiguration config = getPanelConfiguration(name);
            return scale >= config.minScale;
        }

        /// <summary>
        /// Get the solar panel configuration for the input solar panel type name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public ServiceModuleSolarPanelConfiguration getPanelConfiguration(string name)
        {
            return Array.Find(solarConfigs, m => m.name == name);
        }

    }

    /// <summary>
    /// Wrapper for RCS
    /// </summary>
    public class ServiceModuleRCSModelData : SingleModelData
    {
        public GameObject[] models;
        public string thrustTransformName;
        public bool dummyModel = false;
        public float currentHorizontalPosition;
        public float modelRotation = 0;
        public float modelHorizontalZOffset = 0;
        public float modelVerticalOffset = 0;

        public float mountVerticalRotation = 0;

        public ServiceModuleRCSModelData(ConfigNode node) : base(node)
        {
            dummyModel = node.GetBoolValue("dummyModel");
            mountVerticalRotation = node.GetFloatValue("rotation");
            modelHorizontalZOffset = node.GetFloatValue("modelHorizontalZOffset");
            modelVerticalOffset = node.GetFloatValue("modelVerticalOffset");
            thrustTransformName = modelDefinition.configNode.GetStringValue("thrustTransformName");
        }

        public override void setupModel(Transform parent, ModelOrientation orientation)
        {
            if (model!=null || models != null) { destroyCurrentModel(); }
            model = new GameObject(modelDefinition.name);
            model.transform.NestToParent(parent);
            models = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                models[i] = SSTUUtils.cloneModel(modelDefinition.modelName);
            }
            foreach (GameObject go in models)
            {
                go.transform.NestToParent(model.transform);
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
                }
            }
        }

        public override void destroyCurrentModel()
        {
            GameObject.Destroy(model);
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

        public void renameThrustTransforms(string moduleThrustTransformName)
        {
            Transform[] trs = model.transform.FindChildren(thrustTransformName);
            int len = trs.Length;
            for (int i = 0; i < len; i++)
            {
                trs[i].name = trs[i].gameObject.name = moduleThrustTransformName;
            }
        }

    }

    public class ServiceModuleSolarPanelConfiguration
    {
        public readonly string name;
        public readonly SolarPosition[] positions;
        public readonly float minScale;
        public ServiceModuleSolarPanelConfiguration(ConfigNode node)
        {
            name = node.GetStringValue("name");
            minScale = node.GetFloatValue("minScale", 0f);
            ConfigNode[] posNodes = node.GetNodes("POSITION");
            ConfigNode posNode;
            int len = posNodes.Length;
            positions = new SolarPosition[len];
            for (int i = 0; i < len; i++)
            {
                posNode = posNodes[i];
                positions[i] = new SolarPosition(posNode);
            }
        }

        public SolarPosition[] getScaledPositions(float scale)
        {
            int len = this.positions.Length;
            SolarPosition[] positions = new SolarPosition[len];
            for (int i = 0; i < len; i++)
            {
                positions[i] = new SolarPosition(this.positions[i], scale);
            }
            return positions;
        }
    }

}
