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
        public bool updateSolar = true;

        [KSPField]
        public string solarAnimationID = "solarDeploy";

        [KSPField]
        public string topManagedNodes = "top1, top2, top3, top4, top5";

        [KSPField]
        public string bottomManagedNodes = "bottom1, bottom2, bottom3, bottom4, bottom5";

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
        
        ModelModule<SingleModelData, SSTUModularStationCore> topModule;
        ModelModule<SingleModelData, SSTUModularStationCore> coreModule;
        ModelModule<SingleModelData, SSTUModularStationCore> bottomModule;
        ModelModule<SolarModelData, SSTUModularStationCore> solarModule;

        private SSTUAnimateControlled animationControl;

        #endregion ENDREGION - Private working vars

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

        public string[] getSectionNames()
        {
            return new string[] { "Top", "Body", "Bottom" };
        }

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
            coreModule.setupModel();//TODO -- only setup core module if not the prefab part -- else need to add transform updating/fx-updating for RCS and engine modules, as they lack proper handling for transform swapping at runtime
            bottomModule.setupModel();
            solarModule.setupModel();

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
            if (!updateSolar)
            {
                return;
            }
            if (animationControl == null && !string.Equals("none", solarAnimationID))
            {
                SSTUAnimateControlled[] controls = part.GetComponents<SSTUAnimateControlled>();
                int len = controls.Length;
                for (int i = 0; i < len; i++)
                {
                    if (controls[i].animationID == solarAnimationID)
                    {
                        animationControl = controls[i];
                        break;
                    }
                }
                if (animationControl == null)
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

            if (animationControl != null)
            {
                animationControl.animationName = animName;
                animationControl.animationSpeed = animSpeed;
                animationControl.reInitialize();
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

    public class SolarModelData : SingleModelData
    {
        
        public readonly string pivotNames;
        public readonly string sunNames;
        public readonly float energy;
        public readonly string sunAxis;
        public readonly bool panelsEnabled = true;

        private GameObject[] models;

        public SolarPosition[] positions;
        
        public SolarModelData(ConfigNode node) : base(node)
        {
            ConfigNode solarNode = modelDefinition.configNode.GetNode("SOLARDATA");
            if (solarNode == null)
            {
                panelsEnabled = false;
            }
            if (panelsEnabled)
            {
                pivotNames = solarNode.GetStringValue("pivotNames");
                sunNames = solarNode.GetStringValue("sunNames");
                panelsEnabled = solarNode.GetBoolValue("enabled");
                sunAxis = solarNode.GetStringValue("sunAxis", SSTUSolarPanelDeployable.Axis.ZPlus.ToString());
                energy = node.GetFloatValue("energy", solarNode.GetFloatValue("energy"));//allow local override of energy
                ConfigNode[] posNodes = node.GetNodes("POSITION");
                int len = posNodes.Length;
                positions = new SolarPosition[len];
                for (int i = 0; i < len; i++)
                {
                    positions[i] = new SolarPosition(posNodes[i]);
                }
            }
        }

        public override void setupModel(Transform parent, ModelOrientation orientation)
        {
            model = new GameObject("MSCSolarRoot");
            model.transform.NestToParent(parent);
            if (string.IsNullOrEmpty(modelDefinition.modelName)) { return; }
            int len = positions==null? 0 : positions.Length;
            models = new GameObject[len];
            for (int i = 0; i < len; i++)
            {
                models[i] = new GameObject("MSCSolar");
                models[i].transform.NestToParent(model.transform);
                SSTUUtils.cloneModel(modelDefinition.modelName).transform.NestToParent(models[i].transform);
                models[i].transform.Rotate(positions[i].rotation, Space.Self);
                models[i].transform.localPosition = positions[i].position;
                models[i].transform.localScale = positions[i].scale;
            }
        }

        public override void updateModel()
        {
            base.updateModel();
            if (models == null) { return; }
            int len = models.Length;
            for (int i = 0; i < len; i++)
            {
                models[i].transform.localPosition = positions[i].position;
                models[i].transform.localScale = positions[i].scale;
            }
        }

        public override void destroyCurrentModel()
        {
            if (model != null)
            {
                model.transform.parent = null;
                GameObject.Destroy(model);//will destroy children as well
            }
            //de-reference them all, just in case
            model = null;
            models = null;
        }

        public override float getModuleCost()
        {
            return positions == null ? modelDefinition.cost : modelDefinition.cost * positions.Length;
        }

        public override float getModuleMass()
        {
            return positions == null ? modelDefinition.mass : modelDefinition.mass * positions.Length;
        }

        public override float getModuleVolume()
        {
            return positions == null ? modelDefinition.volume : modelDefinition.volume * positions.Length;
        }

        public ConfigNode getSolarData()
        {
            ConfigNode mergedNode = new ConfigNode("SOLAR");            
            ConfigNode[] prevPanelNodes = modelDefinition.solarData.GetNodes("PANEL");
            int len2 = prevPanelNodes.Length;
            for (int i = 0; i < len2; i++)
            {
                mergedNode.AddNode(prevPanelNodes[i]);
            }
            ConfigNode[] panelNodes;
            int len = positions.Length;
            for (int i = 1; i < len; i++)
            {
                panelNodes = new ConfigNode[len2];
                for (int k = 0; k < len2; k++)
                {
                    panelNodes[k] = prevPanelNodes[k].CreateCopy();
                    incrementPanelNode(panelNodes[k]);
                    mergedNode.AddNode(panelNodes[k]);
                }
                prevPanelNodes = panelNodes;
            }
            return mergedNode;
        }

        private void incrementPanelNode(ConfigNode input)
        {
            int idx = input.GetIntValue("mainPivotIndex");
            int str = input.GetIntValue("mainPivotStride");
            idx += str;
            input.RemoveValue("mainPivotIndex");
            input.SetValue("mainPivotIndex", idx, true);

            idx = input.GetIntValue("secondPivotIndex");
            str = input.GetIntValue("secondPivotStride");
            idx += str;
            input.RemoveValue("secondPivotIndex");
            input.SetValue("secondPivotIndex", idx, true);

            ConfigNode[] suncatcherNodes = input.GetNodes("SUNCATCHER");
            int len = suncatcherNodes.Length;
            for (int i = 0; i < len; i++)
            {
                idx = suncatcherNodes[i].GetIntValue("suncatcherIndex");
                str = suncatcherNodes[i].GetIntValue("suncatcherStride");
                idx += str;
                suncatcherNodes[i].RemoveValue("suncatcherIndex");
                suncatcherNodes[i].SetValue("suncatcherIndex", idx, true);
            }
        }
    }

    public class SolarPosition
    {
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public SolarPosition(ConfigNode node)
        {
            position = node.GetVector3("position", Vector3.zero);
            rotation = node.GetVector3("rotation", Vector3.zero);
            scale = node.GetVector3("scale", Vector3.one);
        }
        public SolarPosition(SolarPosition pos, float scale)
        {
            this.position = pos.position * scale;
            this.rotation = pos.rotation;
            this.scale = pos.scale;
        }
    }

}
