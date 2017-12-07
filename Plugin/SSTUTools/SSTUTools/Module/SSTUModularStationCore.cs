using System;
using System.Collections.Generic;
using UnityEngine;
using KSPShaderTools;

namespace SSTUTools
{

    public class SSTUModularStationCore : PartModule, IPartMassModifier, IPartCostModifier, IRecolorable
    {

        #region REGION - Standard Part Config Fields

        //for RO rescale use
        [KSPField]
        public float coreDiameter = 2.5f;

        [KSPField]
        public float topDiameter = 1.875f;

        [KSPField]
        public float bottomDiameter = 2.5f;

        [KSPField]
        public bool useAdapterVolume = false;

        [KSPField]
        public bool useAdapterMass = false;

        [KSPField]
        public bool useAdapterCost = false;

        [KSPField]
        public string solarAnimationID = "solarDeploy";

        [KSPField]
        public string topManagedNodes = "top1, top2, top3, top4, top5";

        [KSPField]
        public string bottomManagedNodes = "bottom1, bottom2, bottom3, bottom4, bottom5";

        [KSPField]
        public int solarAnimationLayer = 1;

        //persistent config fields for module selections
        //also GUI controls for module selection

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

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Solar:")]
        public string solarPanelStatus = string.Empty;

        //persistent data for modules; stores colors and other per-module data
        [KSPField(isPersistant = true)]
        public string topModulePersistentData;
        [KSPField(isPersistant = true)]
        public string coreModulePersistentData;
        [KSPField(isPersistant = true)]
        public string bottomModulePersistentData;

        //persistence data for solar module, stores animation state and rotation cache
        [KSPField(isPersistant = true)]
        public string solarAnimationPersistentData = string.Empty;
        [KSPField(isPersistant = true)]
        public string solarRotationPersistentData = string.Empty;

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
        
        ModelModule<SingleModelData, SSTUModularStationCore> topModule;
        ModelModule<SingleModelData, SSTUModularStationCore> coreModule;
        ModelModule<SingleModelData, SSTUModularStationCore> bottomModule;
        ModelModule<SolarModelData, SSTUModularStationCore> solarModule;

        SolarModule<SSTUModularStationCore> solarPanelModule;

        #endregion ENDREGION - Private working vars

        #region REGION - GUI methods

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Deploy Solar Panels")]
        public void solarDeployEvent() { solarPanelModule.onDeployEvent(); }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Retract Solar Panels")]
        public void solarRetractEvent() { solarPanelModule.onRetractEvent(); }

        [KSPAction(guiName = "Toggle Solar Panels")]
        public void solarToggleAction(KSPActionParam param) { solarPanelModule.onToggleAction(param); }

        #endregion ENDREGION - GUI methods

        #region REGION - Standard KSP Overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize(false);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize(true);

            Action<SSTUModularStationCore> modelChangedAction = delegate (SSTUModularStationCore m)
            {
                m.updateModulePositions();
                m.updateMassAndCost();
                m.updateAttachNodes(true);
                m.updateDragCubes();
                m.updateResourceVolume();
                m.updateGUI();
            };

            Fields[nameof(currentTop)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                topModule.modelSelected(currentTop);
                this.actionWithSymmetry(modelChangedAction);
            };

            Fields[nameof(currentCore)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                coreModule.modelSelected(currentCore);
                this.actionWithSymmetry(modelChangedAction);
            };

            Fields[nameof(currentBottom)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                bottomModule.modelSelected(currentBottom);
                this.actionWithSymmetry(modelChangedAction);
            };

            Fields[nameof(currentSolar)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                solarModule.modelSelected(currentSolar);
                this.actionWithSymmetry(m =>
                {
                    modelChangedAction(m);
                    m.updateSolarModules();
                });
            };

            Fields[nameof(currentTopTexture)].uiControlEditor.onFieldChanged = topModule.textureSetSelected;
            Fields[nameof(currentCoreTexture)].uiControlEditor.onFieldChanged = coreModule.textureSetSelected;
            Fields[nameof(currentBottomTexture)].uiControlEditor.onFieldChanged = bottomModule.textureSetSelected;

            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
            updateDragCubes();
        }

        public void Start()
        {
            if (!initializedDefaults)
            {
                updateResourceVolume();
            }
            initializedDefaults = true;
            updateSolarModules();
        }
        
        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        //standard Unity lifecyle override
        public void Update()
        {
            solarPanelModule.updateAnimations();
            solarPanelModule.solarUpdate();
        }

        //standard Unity lifecyle override
        public void FixedUpdate()
        {
            solarPanelModule.solarFixedUpdate();
        }

        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (modifiedMass == 0) { return 0; }
            return -defaultMass + modifiedMass;
        }

        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (modifiedCost == 0) { return 0; }
            return -defaultCost + modifiedCost;
        }

        private void onEditorVesselModified(ShipConstruct ship)
        {
            updateGUI();
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

            coreModule = new ModelModule<SingleModelData, SSTUModularStationCore>(part, this, getRootTransform("MSC-CORE", true), ModelOrientation.TOP, nameof(coreModulePersistentData), nameof(currentCore), nameof(currentCoreTexture));
            coreModule.getSymmetryModule = m => m.coreModule;
            coreModule.setupModelList(SingleModelData.parseModels(node.GetNodes("CORE")));

            topModule = new ModelModule<SingleModelData, SSTUModularStationCore>(part, this, getRootTransform("MSC-TOP", true), ModelOrientation.TOP, nameof(topModulePersistentData), nameof(currentTop), nameof(currentTopTexture));
            topModule.getSymmetryModule = m => m.topModule;
            topModule.getValidSelections = m => topModule.models.FindAll(s => s.canSwitchTo(part, topNodeNames));

            bottomModule = new ModelModule<SingleModelData, SSTUModularStationCore>(part, this, getRootTransform("MSC-BOTTOM", true), ModelOrientation.BOTTOM, nameof(bottomModulePersistentData), nameof(currentBottom), nameof(currentBottomTexture));
            bottomModule.getSymmetryModule = m => m.bottomModule;
            bottomModule.getValidSelections = m => bottomModule.models.FindAll(s => s.canSwitchTo(part, bottomNodeNames));

            solarModule = new ModelModule<SolarModelData, SSTUModularStationCore>(part, this, getRootTransform("MSC-Solar", true), ModelOrientation.CENTRAL, null, nameof(currentSolar), null);
            solarModule.getSymmetryModule = m => m.solarModule;
            solarModule.setupModelList(SingleModelData.parseModels(node.GetNodes("SOLAR"), m => new SolarModelData(m)));
            solarModule.getValidSelections = m => solarModule.models.FindAll(s => s.isAvailable(upgradesApplied));

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
            bottomModule.setupModel();
            solarModule.setupModel();

            //solar panel animation and solar panel UI controls
            solarPanelModule = new SolarModule<SSTUModularStationCore>(part, this, Fields[nameof(solarAnimationPersistentData)], Fields[nameof(solarRotationPersistentData)], Fields[nameof(solarPanelStatus)], Events[nameof(solarDeployEvent)], Events[nameof(solarRetractEvent)]);
            solarPanelModule.getSymmetryModule = m => m.solarPanelModule;
            solarPanelModule.setupAnimations(solarModule.animationData, solarModule.root, solarAnimationLayer);
            solarPanelModule.setupSolarPanelData(solarModule.model.getSolarData(), solarModule.root);

            updateModulePositions();
            updateMassAndCost();
            updateAttachNodes(false);
            SSTUStockInterop.updatePartHighlighting(part);
        }

        private void updateModulePositions()
        {
            //update for model scale
            topModule.model.updateScaleForDiameter(topDiameter);
            coreModule.model.updateScaleForDiameter(coreDiameter);
            bottomModule.model.updateScaleForDiameter(bottomDiameter);
            solarModule.model.updateScale(1);

            //calc positions
            float yPos = topModule.moduleHeight + (coreModule.moduleHeight * 0.5f);
            float topDockY = yPos;
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

            //update actual model positions and scales
            topModule.updateModel();
            coreModule.updateModel();
            bottomModule.updateModel();
            solarModule.updateModel();
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

        private void updateSolarModules()
        {
            solarPanelModule.setupAnimations(solarModule.animationData, solarModule.root, solarAnimationLayer);
            solarPanelModule.setupSolarPanelData(solarModule.model.getSolarData(), solarModule.root);
        }

        private void updateAttachNodes(bool userInput)
        {
            topModule.model.updateAttachNodes(part, topNodeNames, userInput, ModelOrientation.TOP);
            bottomModule.model.updateAttachNodes(part, bottomNodeNames, userInput, ModelOrientation.BOTTOM);
        }
        
        private void updateGUI()
        {
            topModule.updateSelections();
            bottomModule.updateSelections();
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

}
