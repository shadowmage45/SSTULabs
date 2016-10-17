using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{

    public class SSTUModularStationCore : PartModule, IPartMassModifier, IPartCostModifier
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
        public int solarAnimationID = 0;

        [KSPField]
        public string topManagedNodes = "top1, top2, top3, top4, top5";

        [KSPField]
        public string bottomManagedNodes = "bottom1, bottom2, bottom3, bottom4, bottom5";

        [KSPField]
        public string topDockNode = "top1";

        [KSPField]
        public string bottomDockNode = "bottom1";

        [KSPField]
        public string topDockName = "topDockTransform";

        [KSPField]
        public string bottomDockName = "bottomDockTransform";

        //persistent config fields for module selections
        //also GUI controls for module selection

        [KSPField(isPersistant = true, guiName = "TDock"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentTopDock = "Mount-None";

        [KSPField(isPersistant = true, guiName = "Top"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentTop = "Mount-None";

        [KSPField(isPersistant = true, guiName = "Core"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentCore = "Mount-None";

        [KSPField(isPersistant = true, guiName = "Bottom"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentBottom = "Mount-None";

        [KSPField(isPersistant = true, guiName = "BDock"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentBottomDock = "Mount-None";

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

        //tracks if default textures and resource volumes have been initialized; only occurs once during the parts first Start() call
        [KSPField(isPersistant = true)]
        public bool initializedDefaults = false;

        [KSPField]
        public bool useModelSelectionGUI = true;

        [Persistent]
        public string configNodeData = string.Empty;

        #endregion REGION - Standard Part Config Fields

        #region REGION - Private working vars

        private bool initialized = false;
        private float modifiedMass = 0;
        private float modifiedCost = 0;
        private string[] topNodeNames;
        private string[] bottomNodeNames;
        private SingleModelData[] topDockModules;
        private SingleModelData[] topModules;
        private SingleModelData[] coreModules;
        private SingleModelData[] bottomModules;
        private SingleModelData[] bottomDockModules;
        private SolarData[] solarModules;
        private SingleModelData topDockModule;
        private SingleModelData topModule;
        private SingleModelData coreModule;
        private SingleModelData bottomModule;
        private SingleModelData bottomDockModule;
        private SolarData solarModule;

        private ModuleDockingNode topDockPartModule;
        private ModuleDockingNode bottomDockPartModule;
        private SSTUAnimateControlled animationControl;

        private Transform topDockTransform;
        private Transform topControlTransform;
        private Transform bottomDockTransform;
        private Transform bottomControlTransform;

        #endregion ENDREGION - Private working vars

        //TODO symmetry counterpart updates for all of these
        #region REGION - GUI Methods
        
        [KSPEvent(guiName = "Select Top", guiActiveEditor = true)]
        public void selectTopEvent()
        {
            ModuleSelectionGUI.openGUI(topModules, topDiameter, setTopEditor);
        }

        [KSPEvent(guiName = "Select Bottom", guiActiveEditor = true)]
        public void selectBottomEvent()
        {
            ModuleSelectionGUI.openGUI(bottomModules, bottomDiameter, setBottomEditor);
        }

        private void onTopDockChanged(BaseField field, System.Object obj)
        {
            setTopDockEditor(currentTopDock, true);
        }

        private void setTopDockEditor(string newDock, bool updateSymmetry)
        {
            currentTopDock = newDock;
            SingleModelData prev = topDockModule;
            topDockModule.destroyCurrentModel();
            topDockModule = SingleModelData.findModel(topDockModules, currentTopDock);
            topDockModule.setupModel(getTopDockRoot(false), ModelOrientation.TOP);
            onModelChanged(prev, topDockModule);
            updateDockingModules(true);
            if (updateSymmetry)
            {
                foreach(Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularStationCore>().setTopDockEditor(currentTopDock, false);
                }
            }
        }

        private void onTopChanged(BaseField field, System.Object obj)
        {
            setTopEditor(currentTop, true);
        }

        private void setTopEditor(string newTop, bool updateSymmetry)
        {
            currentTop = newTop;
            SingleModelData prev = topModule;
            topModule.destroyCurrentModel();
            topModule = SingleModelData.findModel(topModules, currentTop);
            topModule.setupModel(getTopRoot(false), ModelOrientation.TOP);
            onModelChanged(prev, topModule);
            if (!topModule.isValidTextureSet(currentTopTexture))
            {
                currentTopTexture = topModule.getDefaultTextureSet();
            }
            topModule.enableTextureSet(currentTopTexture);
            topModule.updateTextureUIControl(this, "currentTopTexture", currentTopTexture);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularStationCore>().setTopEditor(newTop, false);
                }
            }
        }

        private void onCoreChanged(BaseField field, System.Object obj)
        {
            setCoreEditor(currentCore, true);
        }

        private void setCoreEditor(string newCore, bool updateSymmetry)
        {
            currentCore = newCore;
            SingleModelData prev = coreModule;
            coreModule.destroyCurrentModel();
            coreModule = SingleModelData.findModel(coreModules, currentCore);
            coreModule.setupModel(getCoreRoot(false), ModelOrientation.CENTRAL);
            onModelChanged(prev, coreModule);
            if (!coreModule.isValidTextureSet(currentCoreTexture))
            {
                currentCoreTexture = coreModule.getDefaultTextureSet();
            }
            coreModule.enableTextureSet(currentCoreTexture);
            coreModule.updateTextureUIControl(this, "currentCoreTexture", currentCoreTexture);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularStationCore>().setCoreEditor(newCore, false);
                }
            }
        }
        
        private void onBottomChanged(BaseField field, System.Object obj)
        {
            setBottomEditor(currentBottom, true);
        }

        private void setBottomEditor(string newBottom, bool updateSymmetry)
        {
            currentBottom = newBottom;
            SingleModelData prev = bottomModule;
            bottomModule.destroyCurrentModel();
            bottomModule = SingleModelData.findModel(bottomModules, currentBottom);
            bottomModule.setupModel(getBottomRoot(false), ModelOrientation.BOTTOM);
            onModelChanged(prev, bottomModule);
            if (!bottomModule.isValidTextureSet(currentBottomTexture))
            {
                currentBottomTexture = bottomModule.getDefaultTextureSet();
            }
            bottomModule.enableTextureSet(currentBottomTexture);
            bottomModule.updateTextureUIControl(this, "currentBottomTexture", currentBottomTexture);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularStationCore>().setBottomEditor(newBottom, false);
                }
            }
        }
        
        private void onBottomDockChanged(BaseField field, System.Object obj)
        {
            setBottomDockEditor(currentBottomDock, true);
        }

        private void setBottomDockEditor(string newBottomDock, bool updateSymmetry)
        {
            currentBottomDock = newBottomDock;
            SingleModelData prev = bottomDockModule;
            bottomDockModule.destroyCurrentModel();
            bottomDockModule = SingleModelData.findModel(bottomDockModules, currentBottomDock);
            bottomDockModule.setupModel(getBottomDockRoot(false), ModelOrientation.BOTTOM);
            onModelChanged(prev, bottomDockModule);
            updateDockingModules(true);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularStationCore>().setBottomDockEditor(newBottomDock, false);
                }
            }
        }

        private void onSolarChanged(BaseField field, System.Object obj)
        {
            setSolarEditor(currentSolar, true);
        }

        private void setSolarEditor(string newSolar, bool updateSymmetry)
        {
            currentSolar = newSolar;
            solarModule.disable();
            solarModule = Array.Find(solarModules, m => m.name == currentSolar);//TODO cleanup
            solarModule.enable(getSolarRoot(false), coreModule.currentVerticalPosition);
            updateSolarModules();
            updateCost();
            updateMass();
            updateDragCubes();
            updateGUI();
            SSTUStockInterop.fireEditorUpdate();//update editor for mass/cost values
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUModularStationCore>().setSolarEditor(newSolar, false);
                }
            }
        }

        private void onModelChanged(SingleModelData prev, SingleModelData cur)
        {
            updateModulePositions();
            updateAttachNodes(true);
            updateCost();
            updateMass();
            updateDragCubes();
            updateResourceVolume();
            updateGUI();
        }
                
        private void onTopTextureChanged(BaseField field, System.Object obj)
        {
            if ((String)obj != currentTopTexture)
            {
                topModule.enableTextureSet(currentTopTexture);
            }
        }
        
        private void onCoreTextureChanged(BaseField field, System.Object obj)
        {
            if ((String)obj != currentCoreTexture)
            {
                coreModule.enableTextureSet(currentCoreTexture);
            }
        }
        
        private void onBottomTextureChanged(BaseField field, System.Object obj)
        {
            if ((String)obj != currentBottomTexture)
            {
                bottomModule.enableTextureSet(currentBottomTexture);
            }
        }
        
        #endregion ENDREGION - GUI METHODS

        #region REGION - Standard KSP Overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            init(false);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            init(true);
            initializeGUI();
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
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

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if (modifiedMass == 0) { return 0; }
            return -defaultMass + modifiedMass;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen()
        {
            return ModifierChangeWhen.CONSTANTLY;
        }

        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (modifiedCost == 0) { return 0; }
            return -defaultCost + modifiedCost;
        }

        public ModifierChangeWhen GetModuleCostChangeWhen()
        {
            return ModifierChangeWhen.CONSTANTLY;
        }
        
        private void onEditorVesselModified(ShipConstruct ship)
        {
            updateGUI();
        }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Custom Update Methods

        private void init(bool start)
        {
            if (initialized) { return; }
            initialized = true;

            topNodeNames = SSTUUtils.parseCSV(topManagedNodes);
            bottomNodeNames = SSTUUtils.parseCSV(bottomManagedNodes);
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);

            coreModules = SingleModelData.parseModels(node.GetNodes("CORE"));

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
            topModules = SingleModelData.parseModels(tops.ToArray());
            bottomModules = SingleModelData.parseModels(bottoms.ToArray());
            tops.Clear();
            bottoms.Clear();

            mNodes = node.GetNodes("DOCK");
            len = mNodes.Length;
            for (int i = 0; i < len; i++)
            {
                mNode = mNodes[i];
                if (mNode.GetBoolValue("useForTop", true)) { tops.Add(mNode); }
                if (mNode.GetBoolValue("useForBottom", true)) { bottoms.Add(mNode); }
            }
            topDockModules = SingleModelData.parseModels(tops.ToArray());
            bottomDockModules = SingleModelData.parseModels(bottoms.ToArray());
            tops.Clear();
            bottoms.Clear();

            mNodes = node.GetNodes("SOLAR");
            len = mNodes.Length;
            solarModules = new SolarData[len];
            for (int i = 0; i < len; i++)
            {
                mNode = mNodes[i];
                solarModules[i] = new SolarData(mNode);
            }

            topDockModule = SingleModelData.findModel(topDockModules, currentTopDock);
            topModule = SingleModelData.findModel(topModules, currentTop);
            coreModule = SingleModelData.findModel(coreModules, currentCore);
            bottomModule = SingleModelData.findModel(bottomModules, currentBottom);
            bottomDockModule = SingleModelData.findModel(bottomDockModules, currentBottomDock);
            solarModule = Array.Find(solarModules, m => m.name == currentSolar);//TODO cleanup
            if (!topModule.isValidTextureSet(currentTopTexture)) { currentTopTexture = topModule.getDefaultTextureSet(); }
            if (!coreModule.isValidTextureSet(currentCoreTexture)) { currentCoreTexture = coreModule.getDefaultTextureSet(); }
            if (!bottomModule.isValidTextureSet(currentBottomTexture)) { currentBottomTexture = bottomModule.getDefaultTextureSet(); }
            restoreModels();
            updateModulePositions();
            updateMass();
            updateCost();
            updateAttachNodes(false);
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                ModuleDockingNode[] mdns = part.GetComponents<ModuleDockingNode>();
                if (mdns.Length > 0)
                {
                    if (topDockModule.model != null)
                    {
                        topDockPartModule = mdns[0];
                    }
                    if (bottomDockModule.model != null)
                    {
                        bottomDockPartModule = mdns.Length > 1 ? mdns[1] : mdns[0];
                    }
                }
                updateDockingModules(start);
            }
            //resources are updated in Start(), to ensure that the dependent modules have loaded
        }
        
        private void initializeGUI()
        {
            string[] names = SingleModelData.getModelNames(topDockModules);
            this.updateUIChooseOptionControl("currentTopDock", names, names, true, currentTopDock);
            Fields["currentTopDock"].uiControlEditor.onFieldChanged = onTopDockChanged;
            Fields["currentTopDock"].guiActiveEditor = topDockModules.Length > 1;

            names = SingleModelData.getValidSelectionNames(part, topModules, topNodeNames);
            this.updateUIChooseOptionControl("currentTop", names, names, true, currentTop);
            Fields["currentTop"].uiControlEditor.onFieldChanged = onTopChanged;
            Fields["currentTop"].guiActiveEditor = !useModelSelectionGUI && names.Length > 1;
            Events["selectTopEvent"].guiActiveEditor = useModelSelectionGUI && names.Length > 1;

            names = SingleModelData.getModelNames(coreModules);
            this.updateUIChooseOptionControl("currentCore", names, names, true, currentCore);
            Fields["currentCore"].uiControlEditor.onFieldChanged = onCoreChanged;
            Fields["currentCore"].guiActiveEditor = coreModules.Length > 1;

            names = SingleModelData.getValidSelectionNames(part, bottomModules, bottomNodeNames);
            this.updateUIChooseOptionControl("currentBottom", names, names, true, currentBottom);
            Fields["currentBottom"].uiControlEditor.onFieldChanged = onBottomChanged;
            Fields["currentBottom"].guiActiveEditor = !useModelSelectionGUI && names.Length > 1;
            Events["selectBottomEvent"].guiActiveEditor = useModelSelectionGUI && names.Length > 1;

            names = SingleModelData.getModelNames(bottomDockModules);
            this.updateUIChooseOptionControl("currentBottomDock", names, names, true, currentBottomDock);
            Fields["currentBottomDock"].uiControlEditor.onFieldChanged = onBottomDockChanged;
            Fields["currentBottomDock"].guiActiveEditor = bottomDockModules.Length > 1;

            names = SolarData.getNames(solarModules);
            this.updateUIChooseOptionControl("currentSolar", names, names, true, currentSolar);
            Fields["currentSolar"].uiControlEditor.onFieldChanged = onSolarChanged;
            Fields["currentSolar"].guiActiveEditor = solarModules.Length > 1;

            names = topModule.modelDefinition.getTextureSetNames();
            this.updateUIChooseOptionControl("currentTopTexture", names, names, true, currentTopTexture);
            Fields["currentTopTexture"].uiControlEditor.onFieldChanged = onTopTextureChanged;
            Fields["currentTopTexture"].guiActiveEditor = names.Length > 1;

            names = coreModule.modelDefinition.getTextureSetNames();
            this.updateUIChooseOptionControl("currentCoreTexture", names, names, true, currentCoreTexture);
            Fields["currentCoreTexture"].uiControlEditor.onFieldChanged = onCoreTextureChanged;
            Fields["currentCoreTexture"].guiActiveEditor = names.Length > 1;

            names = bottomModule.modelDefinition.getTextureSetNames();
            this.updateUIChooseOptionControl("currentBottomTexture", names, names, true, currentBottomTexture);
            Fields["currentBottomTexture"].uiControlEditor.onFieldChanged = onBottomTextureChanged;
            Fields["currentBottomTexture"].guiActiveEditor = names.Length > 1;
        }

        private void updateModulePositions()
        {
            //update for model scale
            topDockModule.updateScale(1);
            topModule.updateScaleForDiameter(topDiameter);
            coreModule.updateScaleForDiameter(coreDiameter);
            bottomModule.updateScaleForDiameter(bottomDiameter);
            bottomDockModule.updateScale(1);

            //calc positions
            float yPos = topModule.currentHeight + (coreModule.currentHeight * 0.5f);
            float topDockY = yPos;
            yPos -= topModule.currentHeight;
            float topY = yPos;
            yPos -= coreModule.currentHeight;
            float coreY = yPos;
            float bottomY = coreY;
            yPos -= bottomModule.currentHeight;
            float bottomDockY = yPos;

            //update internal ref of position
            topDockModule.setPosition(topDockY);
            topModule.setPosition(topY);
            coreModule.setPosition(coreY);
            bottomModule.setPosition(bottomY, ModelOrientation.BOTTOM);
            bottomDockModule.setPosition(bottomDockY, ModelOrientation.BOTTOM);

            //update actual model positions and scales
            topDockModule.updateModel();
            topModule.updateModel();
            coreModule.updateModel();
            bottomModule.updateModel();
            bottomDockModule.updateModel();

            solarModule.updateModelPosition(coreModule.currentVerticalPosition);

            Vector3 pos = new Vector3(0, topDockY + topDockModule.currentHeight, 0);
            topDockTransform.localPosition = pos;
            topControlTransform.localPosition = pos;

            pos = new Vector3(0, bottomDockY - bottomDockModule.currentHeight, 0);
            bottomDockTransform.localPosition = pos;
            bottomControlTransform.localPosition = pos;
        }
        
        /// <summary>
        /// Restore the currently selected modules' models, and restore their current texture-set selection
        /// Removes any existing models from special-named root transforms with no attempt at re-use
        /// </summary>
        private void restoreModels()
        {
            topDockModule.setupModel(getTopDockRoot(true), ModelOrientation.TOP);
            topModule.setupModel(getTopRoot(true), ModelOrientation.TOP);
            coreModule.setupModel(getCoreRoot(false), ModelOrientation.CENTRAL, true);//setup model on prefab part and don't touch it after that
            bottomModule.setupModel(getBottomRoot(true), ModelOrientation.BOTTOM);
            bottomDockModule.setupModel(getBottomDockRoot(true), ModelOrientation.BOTTOM);
            solarModule.enable(getSolarRoot(true), 0);
            
            topModule.enableTextureSet(currentTopTexture);
            coreModule.enableTextureSet(currentCoreTexture);
            bottomModule.enableTextureSet(currentBottomTexture);

            //control transforms need to exist during part initialization
            //else things will explode if one of those transforms is the reference transform when a vessel is reloaded
            //so create them on prefab and restore on other instances
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
            {
                Transform modelRoot = part.transform.FindRecursive("model");

                topDockTransform = new GameObject(topDockName).transform;
                topDockTransform.NestToParent(modelRoot);
                topDockTransform.Rotate(-90, 0, 0, Space.Self);
                topControlTransform = new GameObject(topDockName + "Control").transform;
                topControlTransform.NestToParent(modelRoot);

                bottomDockTransform = new GameObject(bottomDockName).transform;
                bottomDockTransform.NestToParent(modelRoot);
                bottomDockTransform.Rotate(90, 0, 0, Space.Self);
                bottomControlTransform = new GameObject(bottomDockName + "Control").transform;
                bottomControlTransform.NestToParent(modelRoot);
                bottomControlTransform.Rotate(180, 0, 0, Space.Self);
            }
            else
            {
                topDockTransform = part.transform.FindRecursive(topDockName);
                topControlTransform = part.transform.FindRecursive(topDockName + "Control");
                bottomDockTransform = part.transform.FindRecursive(bottomDockName);
                bottomControlTransform = part.transform.FindRecursive(bottomDockName + "Control");
            }
        }
        
        private void updateResourceVolume()
        {
            float volume = coreModule.getModuleVolume();
            if (useAdapterVolume)
            {
                volume += topModule.getModuleVolume();
                volume += bottomModule.getModuleVolume();
            }
            SSTUModInterop.onPartFuelVolumeUpdate(part, volume * 1000f);
        }
        
        private void updateCost()
        {
            modifiedCost = coreModule.getModuleCost();
            if (useAdapterCost)
            {
                modifiedCost += topModule.getModuleCost();
                modifiedCost += bottomModule.getModuleCost();
                modifiedCost += topDockModule.getModuleCost();
                modifiedCost += bottomDockModule.getModuleCost();
            }
            modifiedCost += solarModule.getCost();
        }
        
        private void updateMass()
        {
            modifiedMass = coreModule.getModuleMass();
            if (useAdapterMass)
            {
                modifiedMass += topModule.getModuleMass();
                modifiedMass += bottomModule.getModuleMass();
                modifiedMass += topDockModule.getModuleMass();
                modifiedMass += bottomDockModule.getModuleMass();
            }
            modifiedMass += solarModule.getMass();
        }

        private void updateSolarModules()
        {
            if (!updateSolar) { return; }
            if (animationControl == null)
            {
                SSTUAnimateControlled[] controls = part.GetComponents<SSTUAnimateControlled>();
                int len = controls.Length;
                for (int i = 0; i < len; i++)
                {
                    if (controls[i].animationID == solarAnimationID) { animationControl = controls[i]; break; }
                }
            }
            
            animationControl.animationName = solarModule.animationName;
            animationControl.reInitialize();

            SSTUSolarPanelDeployable solar = part.GetComponent<SSTUSolarPanelDeployable>();
            if (solar == null) { return; }

            solar.resourceAmount = solarModule.energy;
            solar.pivotTransforms = solarModule.pivotNames;
            solar.secondaryPivotTransforms = solarModule.secPivotNames;
            solar.rayTransforms = solarModule.sunNames;
            if (solarModule.panelsEnabled)
            {
                solar.enableModule();
            }
            else
            {
                solar.disableModule();
            }
        }
        
        private void updateDockingModules(bool start)
        {
            //TODO only remove and replace modules if the new setup differs from the old
            if (topDockPartModule != null)
            {
                part.RemoveModule(topDockPartModule);
                topDockPartModule = null;
            }
            if (bottomDockPartModule != null)
            {
                part.RemoveModule(bottomDockPartModule);
                bottomDockPartModule = null;
            }
            updateTopDockModule(start);
            updateBottomDockModule(start);
        }

        //TODO load docking module config from sub-config nodes in the module node
        private void updateTopDockModule(bool start)
        {
            bool topNodeActive = topDockModule.model != null;
            if (topNodeActive && topDockPartModule == null)
            {
                ConfigNode topModuleNode = new ConfigNode("MODULE");
                topModuleNode.AddValue("name", "ModuleDockingNode");
                topModuleNode.AddValue("referenceAttachNode", topDockNode);
                topModuleNode.AddValue("useReferenceAttachNode", true);
                topModuleNode.AddValue("nodeTransformName", topDockName);
                topModuleNode.AddValue("controlTransformName", topDockName + "Control");
                topModuleNode.AddValue("nodeType", "size0, size1");
                topModuleNode.AddValue("captureRange", "0.1");
                topDockPartModule = (ModuleDockingNode)part.AddModule(topModuleNode);
                if (start) { topDockPartModule.OnStart(StartState.Editor); }
                topDockPartModule.referenceNode = part.FindAttachNode(topDockNode);
            }
            else if (!topNodeActive && topDockPartModule != null)
            {
                part.RemoveModule(topDockPartModule);
            }
            if (topNodeActive)
            {
                SSTUMultiDockingPort.updateDockingModuleFieldNames(topDockPartModule, "Top Port");
            }
        }

        //TODO load docking module config from sub-config nodes in the module node
        private void updateBottomDockModule(bool start)
        {
            bool bottomNodeActive = bottomDockModule.model != null;
            if (bottomNodeActive && bottomDockPartModule == null)
            {
                ConfigNode bottomModuleNode = new ConfigNode("MODULE");
                bottomModuleNode.AddValue("name", "ModuleDockingNode");
                bottomModuleNode.AddValue("referenceAttachNode", bottomDockNode);
                bottomModuleNode.AddValue("useReferenceAttachNode", true);
                bottomModuleNode.AddValue("nodeTransformName", bottomDockName);
                bottomModuleNode.AddValue("controlTransformName", bottomDockName + "Control");
                bottomModuleNode.AddValue("nodeType", "size0, size1");
                bottomModuleNode.AddValue("captureRange", "0.1");
                bottomDockPartModule = (ModuleDockingNode)part.AddModule(bottomModuleNode);
                if (start) { bottomDockPartModule.OnStart(StartState.Editor); }
                bottomDockPartModule.referenceNode = part.FindAttachNode(bottomDockNode);
            }
            else if (!bottomNodeActive && bottomDockPartModule != null)
            {
                part.RemoveModule(bottomDockPartModule);
            }
            if (bottomNodeActive)
            {
                SSTUMultiDockingPort.updateDockingModuleFieldNames(bottomDockPartModule, "Bottom Port");
            }
        }
        
        private void updateAttachNodes(bool userInput)
        {
            //if XX-dockModule!=empty, remove dock node name from NodeNames
            //I believe this is what is causing the duplicate attach nodes reported by someone on the forums
            topModule.updateAttachNodes(part, topNodeNames, userInput, ModelOrientation.TOP);
            bottomModule.updateAttachNodes(part, bottomNodeNames, userInput, ModelOrientation.BOTTOM);
            topDockModule.updateAttachNodes(part, new string[] { topDockNode }, userInput, ModelOrientation.TOP);
            bottomDockModule.updateAttachNodes(part, new string[] { bottomDockNode }, userInput, ModelOrientation.BOTTOM);
        }
        
        private void updateGUI()
        {
            string[] moduleNames = SingleModelData.getValidSelectionNames(part, topModules, topNodeNames);
            this.updateUIChooseOptionControl("currentTop", moduleNames, moduleNames, true, currentTop);
            Fields["currentTop"].guiActiveEditor = !useModelSelectionGUI && moduleNames.Length > 1;
            Events["selectTopEvent"].guiActiveEditor = useModelSelectionGUI && moduleNames.Length > 1;

            moduleNames = SingleModelData.getValidSelectionNames(part, bottomModules, bottomNodeNames);
            this.updateUIChooseOptionControl("currentBottom", moduleNames, moduleNames, true, currentBottom);
            Fields["currentBottom"].guiActiveEditor = !useModelSelectionGUI && moduleNames.Length > 1;
            Events["selectBottomEvent"].guiActiveEditor = useModelSelectionGUI && moduleNames.Length > 1;
        }

        private void updateDragCubes()
        {
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        private Transform getRootTransformFor(string name, bool recreate)
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

        private Transform getTopDockRoot(bool recreate) { return getRootTransformFor("SSTU-ST-MSC-TopDock", recreate); }
        private Transform getTopRoot(bool recreate) { return getRootTransformFor("SSTU-ST-MSC-TopRoot", recreate); }
        private Transform getCoreRoot(bool recreate) { return getRootTransformFor("SSTU-ST-MSC-CoreRoot", recreate); }
        private Transform getBottomRoot(bool recreate) { return getRootTransformFor("SSTU-ST-MSC-BottomRoot", recreate); }
        private Transform getBottomDockRoot(bool recreate) { return getRootTransformFor("SSTU-ST-MSC-BottomDock", recreate); }
        private Transform getSolarRoot(bool recreate) { return getRootTransformFor("SSTU-ST-MSC-SolarRoot", recreate); }

        #endregion ENDREGION - Custom Update Methods

    }

    public class SolarData
    {
        public readonly string name;
        public readonly string modelName;
        public readonly string animationName;
        public readonly string pivotNames;
        public readonly string secPivotNames;
        public readonly string sunNames;
        public readonly float energy;
        public readonly bool panelsEnabled;

        private ModelDefinition def;
        private SolarPosition[] positions;
        private SingleModelData[] models;
        private Transform[] rootTransforms;
        
        public SolarData(ConfigNode node)
        {
            name = node.GetStringValue("name");
            modelName = node.GetStringValue("modelName", name);
            def = SSTUModelData.getModelDefinition(modelName);
            ConfigNode solarNode = def.configNode.GetNode("SOLARDATA");
            animationName = solarNode.GetStringValue("animationName");
            pivotNames = solarNode.GetStringValue("pivotNames");
            secPivotNames = solarNode.GetStringValue("secPivotNames");
            sunNames = solarNode.GetStringValue("sunNames");
            energy = solarNode.GetFloatValue("energy");
            panelsEnabled = solarNode.GetBoolValue("enabled");
            energy = node.GetFloatValue("energy", energy);//allow local override of energy
            ConfigNode[] posNodes = node.GetNodes("POSITION");
            int len = posNodes.Length;
            positions = new SolarPosition[len];
            for (int i = 0; i < len; i++)
            {
                positions[i] = new SolarPosition(posNodes[i]);
            }
        }
        
        public void disable()
        {
            int len = rootTransforms.Length;
            for (int i = 0; i < len; i++)
            {                
                GameObject.DestroyImmediate(rootTransforms[i].gameObject);
            }
            rootTransforms = null;
            models = null;
        }
        
        public void enable(Transform root, float yOffset)
        {
            SSTUUtils.destroyChildren(root);
            int len = positions.Length;
            rootTransforms = new Transform[len];
            models = new SingleModelData[len];
            Vector3 pos;
            for (int i = 0; i < len; i++)
            {
                rootTransforms[i] = new GameObject(root.name + "-" + i).transform;
                pos = positions[i].position.CopyVector();
                pos.y += yOffset;
                rootTransforms[i].parent = root;
                rootTransforms[i].position = root.position;
                rootTransforms[i].rotation = root.rotation;
                rootTransforms[i].localPosition = pos;
                rootTransforms[i].Rotate(positions[i].rotation, Space.Self);
                models[i] = new SingleModelData(name);
                models[i].setupModel(rootTransforms[i], ModelOrientation.TOP);
            }
        }

        public void updateModelPosition(float yOffset)
        {
            int len = rootTransforms.Length;
            float yPos = 0;
            Vector3 pos;
            for (int i = 0; i < len; i++)
            {
                yPos = yOffset + positions[i].position.y;
                pos = rootTransforms[i].localPosition;
                pos.y = yPos;
                rootTransforms[i].localPosition = pos;
            }
        }

        public static string[] getNames(SolarData[] data)
        {
            int len = data.Length;
            string[] names = new string[len];
            for (int i = 0; i < len; i++)
            {
                names[i] = data[i].name;
            }
            return names;
        }
        
        public float getCost()
        {
            float cost = def.cost * positions.Length;
            return cost;
        }
        
        public float getMass()
        {
            float mass = def.mass * positions.Length;
            return mass;
        }

    }

    public class SolarPosition
    {
        public Vector3 position;
        public Vector3 rotation;
        public SolarPosition(ConfigNode node)
        {
            position = node.GetVector3("position");
            rotation = node.GetVector3("rotation");
        }
    }

}
