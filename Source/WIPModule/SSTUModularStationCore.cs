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
        private SingleModelData topDockModule;
        private SingleModelData topModule;
        private SingleModelData coreModule;
        private SingleModelData bottomModule;
        private SingleModelData bottomDockModule;

        private ModuleDockingNode topDockPartModule;
        private ModuleDockingNode bottomDockPartModule;

        private Transform topDockTransform;
        private Transform topControlTransform;
        private Transform bottomDockTransform;
        private Transform bottomControlTransform;

        #endregion ENDREGION - Private working vars

        //TODO symmetry counterpart updates for all of these
        #region REGION - GUI Methods

        private void onTopDockChanged(BaseField field, System.Object obj)
        {
            SingleModelData prev = topDockModule;
            topDockModule.destroyCurrentModel();
            topDockModule = SingleModelData.findModel(topDockModules, currentTopDock);
            topDockModule.setupModel(getTopDockRoot(false), ModelOrientation.TOP);
            onModelChanged(prev, topDockModule);
            updateDockingModules(true);
        }

        private void onTopChanged(BaseField field, System.Object obj)
        {
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
        }

        private void onCoreChanged(BaseField field, System.Object obj)
        {
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
        }
        
        private void onBottomChanged(BaseField field, System.Object obj)
        {
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
        }
        
        private void onBottomDockChanged(BaseField field, System.Object obj)
        {
            SingleModelData prev = bottomDockModule;
            bottomDockModule.destroyCurrentModel();
            bottomDockModule = SingleModelData.findModel(bottomDockModules, currentBottomDock);
            bottomDockModule.setupModel(getBottomDockRoot(false), ModelOrientation.BOTTOM);
            onModelChanged(prev, bottomDockModule);
            updateDockingModules(true);
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
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(this);

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

            topDockModule = SingleModelData.findModel(topDockModules, currentTopDock);
            topModule = SingleModelData.findModel(topModules, currentTop);
            coreModule = SingleModelData.findModel(coreModules, currentCore);
            bottomModule = SingleModelData.findModel(bottomModules, currentBottom);
            bottomDockModule = SingleModelData.findModel(bottomDockModules, currentBottomDock);
            if (!initializedDefaults)
            {
                currentTopTexture = topModule.getDefaultTextureSet();
                currentCoreTexture = coreModule.getDefaultTextureSet();
                currentBottomTexture = bottomModule.getDefaultTextureSet();
            }
            restoreModels();
            updateModulePositions();
            updateMass();
            updateCost();
            updateAttachNodes(false);
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
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
            Fields["currentTop"].guiActiveEditor = topModules.Length > 1;

            names = SingleModelData.getModelNames(coreModules);
            this.updateUIChooseOptionControl("currentCore", names, names, true, currentCore);
            Fields["currentCore"].uiControlEditor.onFieldChanged = onCoreChanged;
            Fields["currentCore"].guiActiveEditor = coreModules.Length > 1;

            names = SingleModelData.getValidSelectionNames(part, bottomModules, bottomNodeNames);
            this.updateUIChooseOptionControl("currentBottom", names, names, true, currentBottom);
            Fields["currentBottom"].uiControlEditor.onFieldChanged = onBottomChanged;
            Fields["currentBottom"].guiActiveEditor = bottomModules.Length > 1;

            names = SingleModelData.getModelNames(bottomDockModules);
            this.updateUIChooseOptionControl("currentBottomDock", names, names, true, currentBottomDock);
            Fields["currentBottomDock"].uiControlEditor.onFieldChanged = onBottomDockChanged;
            Fields["currentBottomDock"].guiActiveEditor = bottomDockModules.Length > 1;

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

            Vector3 pos = new Vector3(0, topDockY + topDockModule.currentHeight, 0);
            topDockTransform.localPosition = pos;
            topControlTransform.localPosition = pos;

            pos = new Vector3(0, bottomDockY - bottomDockModule.currentHeight, 0);
            bottomDockTransform.localPosition = pos;
            bottomControlTransform.localPosition = pos;
            //SSTUUtils.recursePrintComponents(part.gameObject, "");
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
            
            topModule.enableTextureSet(currentTopTexture);
            coreModule.enableTextureSet(currentCoreTexture);
            bottomModule.enableTextureSet(currentBottomTexture);

            //control transforms need to exist during part initialization
            //else things will explode if one of those transforms is the reference transform when a vessel is reloaded
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
        }

        //TODO save out the data from the enabled modules, restore to that module if it is still enabled when it is all said and done;
        //  this should fix up any lack of persistence due to removing the modules entirely each time the setup is changed to enforce indexing
        private void updateDockingModules(bool start)
        {
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
            updateTockDockModule(start);
            updateBottomDockModule(start);
        }

        //TODO load docking module config from sub-config nodes in the module node
        private void updateTockDockModule(bool start)
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
                topModuleNode.AddValue("captureRange", 0.1f);
                topDockPartModule = (ModuleDockingNode)part.AddModule(topModuleNode);
                if (start) { topDockPartModule.OnStart(StartState.Editor); }
                topDockPartModule.referenceNode = part.findAttachNode(topDockNode);
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
                bottomModuleNode.AddValue("captureRange", 0.1f);
                bottomDockPartModule = (ModuleDockingNode)part.AddModule(bottomModuleNode);
                if (start) { bottomDockPartModule.OnStart(StartState.Editor); }
                bottomDockPartModule.referenceNode = part.findAttachNode(bottomDockNode);
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
            if (topModules.Length > 1)
            {
                string[] moduleNames = SingleModelData.getValidSelectionNames(part, topModules, topNodeNames);
                this.updateUIChooseOptionControl("currentTop", moduleNames, moduleNames, true, currentTop);
            }
            if (bottomModules.Length > 1)
            {
                string[] moduleNames = SingleModelData.getValidSelectionNames(part, bottomModules, bottomNodeNames);
                this.updateUIChooseOptionControl("currentBottom", moduleNames, moduleNames, true, currentBottom);
            }
        }

        private void updateDragCubes()
        {
            SSTUStockInterop.addDragUpdatePart(part);
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

        #endregion ENDREGION - Custom Update Methods

    }

}
