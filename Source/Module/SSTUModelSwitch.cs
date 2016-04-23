using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SSTUTools
{

    public class SSTUModelSwitch : PartModule, IPartMassModifier, IPartCostModifier
    {        
        /// <summary>
        /// Amount of persistent volume that is untouched by the ModelSwitch module; regardless of current part setup the volume will always be at least this amount (unless a model has negative volume?)
        /// </summary>
        [KSPField]
        public float baseVolume = 100f;

        /// <summary>
        /// The index of the base container within the SSTUVolumeContainer that holds the 'baseVolume'; this may differ from the 'baseContainerIndex' for the volumeContainerModule
        /// </summary>
        [KSPField]
        public int baseContainerIndex = 0;

        [KSPField]
        public bool subtractMass = false;

        [KSPField]
        public bool subtractCost = false;

        [KSPField(guiActiveEditor =true, guiName = "EditGroup"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string guiGroupSelection;

        [KSPField(guiActiveEditor = true, guiName = "GroupModel"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string guiModelSelection;

        //CSV list of currently activated model data
        [KSPField(isPersistant = true)]
        public string persistentConfigData = string.Empty;

        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        private float modifiedMass;
        private float modifiedCost;
        private string[] controlledNodes;
        private ModelSwitchData[] modelData;
        private ModelSwitchGroup[] modelGroups;
        private Dictionary<string, ModelSwitchGroup> groupsByName = new Dictionary<string, ModelSwitchGroup>();
                
        public void onGroupUpdated(BaseField field, object obj)
        {
            //NOOP?
            updateGui();
        }

        public void onModelUpdated(BaseField field, object obj)
        {
            ModelSwitchGroup group = Array.Find(modelGroups, m => m.name == guiGroupSelection);
            group.enable(guiModelSelection);
            updateGui();
            guiModelSelection = group.enabledModel.name;
            updateContainerVolume();
            updateMassAndCost();
            updateDragCube();
            updateAttachNodes(true);
            updatePersistentData();
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            updatePersistentData();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor && !initializedResources)
            {
                initializedResources = true;
                updateContainerVolume();
            }
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return subtractMass ? -defaultMass + modifiedMass : modifiedMass; }
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) { return subtractCost ? -defaultCost + modifiedCost : modifiedCost; }
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        /// <summary>
        /// Locate the modelSwitchGroup for the input name -- UNSAFE - will KNFE for invalid name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public ModelSwitchGroup findGroup(string name) { return groupsByName[name]; }

        /// <summary>
        /// Update the persistent data field with the current configuration for this module; this is a set of enabled/disabled flags for each groups models
        /// </summary>
        private void updatePersistentData()
        {
            if (modelData == null) { return; }//--KSP calling 'save' before OnLoad and OnStart
            String saveData = string.Empty;
            int len = modelGroups.Length;
            for (int i = 0; i < len; i++)
            {
                if (i > 0) { saveData = saveData + ":"; }
                saveData = saveData + modelGroups[i].save();
            }
            persistentConfigData = saveData;
        }

        /// <summary>
        /// Initialize this module - load config data, restore persistent data, setup gui fields
        /// </summary>
        private void initialize()
        {
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(this);
            controlledNodes = node.GetStringValues("controlledNode");
            //load model groups, initializing a default 'Main' group if none are defined
            ConfigNode[] groupNodes = node.GetNodes("GROUP");
            int len = groupNodes.Length;
            if (len == 0)//create default group
            {
                len = 1;
                groupNodes = new ConfigNode[1];
                groupNodes[0] = new ConfigNode("GROUP");
                groupNodes[0].AddValue("name", "Main");
            }
            modelGroups = new ModelSwitchGroup[len];
            ModelSwitchGroup group;
            for (int i = 0; i < len; i++)
            {
                group = new ModelSwitchGroup(groupNodes[i], this);
                modelGroups[i] = group;
                groupsByName.Add(group.name, group);
            }

            //load model definitions, initializing them with the model group
            ConfigNode[] modelNodes = node.GetNodes("MODEL");
            len = modelNodes.Length;
            modelData = new ModelSwitchData[len];
            string groupName;
            for (int i = 0; i < len; i++)
            {
                groupName = modelNodes[i].GetStringValue("group", "Main");
                modelData[i] = new ModelSwitchData(modelNodes[i], part, groupsByName[groupName]);
            }

            //pre-initialize model groups; this sets up their parent/children relations and loads default enabled/disabled status into the model definitions
            len = modelGroups.Length;
            for (int i = 0; i < len; i++)
            {
                modelGroups[i].preInitialize();
            }

            //load persistent data for groups; this will restore the settings for enabled/disabled for each model definition
            string data = persistentConfigData;
            if (!string.IsNullOrEmpty(data))
            {
                string[] split = data.Split(':');
                len = split.Length;
                for (int i = 0; i < len; i++)
                {
                    modelGroups[i].load(split[i]);
                }
            }

            //initialize root model groups; they will recursively enable children if they should be enabled
            len = modelGroups.Length;
            for (int i = 0; i < len; i++)
            {
                if (String.IsNullOrEmpty(modelGroups[i].parentGroup))
                {
                    modelGroups[i].initializeRoot();
                }
            }

            //initialize gui selection field to the first available group
            guiGroupSelection = Array.Find(modelGroups, m => m.isAvailable()).name;
            //update ui option arrays for the given group and its current model
            updateGui();
            //setup ui callbacks for group/model changed
            Fields["guiGroupSelection"].uiControlEditor.onFieldChanged = onGroupUpdated;
            Fields["guiModelSelection"].uiControlEditor.onFieldChanged = onModelUpdated;
            //update persistent data to the currently setup state; this updates the persistent data for a freshly-initialized part
            updatePersistentData();
            updateMassAndCost();
            updateAttachNodes(false);
            updateDragCube();
        }
        
        /// <summary>
        /// Update the currently avaialble selection options and enabled/disabled status of the group and model selection widgets
        /// </summary>
        private void updateGui()
        {            
            ModelSwitchGroup[] availableGroups = modelGroups.Where(m => m.isAvailable()).ToArray();
            int len = availableGroups.Length;
            string[] groupNames = new string[len];
            for (int i = 0; i < len; i++)
            {
                groupNames[i] = availableGroups[i].name;
            }
            ModelSwitchGroup group = Array.Find(modelGroups, m => m.name == guiGroupSelection);
            guiModelSelection = group.enabledModel.name;
            if (groupNames.Length > 1)
            {
                this.updateUIChooseOptionControl("guiGroupSelection", groupNames, groupNames, true, guiGroupSelection);
            }
            string[] modelNames = group.getModelNames();
            if (modelNames.Length > 1)
            {
                this.updateUIChooseOptionControl("guiModelSelection", modelNames, modelNames, true, guiModelSelection);
            }            
            Fields["guiGroupSelection"].guiActiveEditor = groupNames.Length > 1;
            Fields["guiModelSelection"].guiActiveEditor = modelNames.Length > 1;
            Fields["guiModelSelection"].guiName = groupNames.Length > 1 ? "GroupModel" : "Variant";
        }

        /// <summary>
        /// Updates the associated VolumeContainer with any changes to part volume from this module
        /// </summary>
        private void updateContainerVolume()
        {
            SSTUVolumeContainer container = part.GetComponent<SSTUVolumeContainer>();
            if (container == null) { return; }//TODO spew error msg
            int len = container.numberOfContainers;
            float[] percents = new float[len];
            float total = calcTotalVolume();
            float val;
            for (int i = 0; i < len; i++)
            {
                val = calcVolume(i);
                percents[i] = val / total;
            }
            container.setContainerPercents(percents, total);
        }

        /// <summary>
        /// Calculates the total allocated volume for a specific container index
        /// </summary>
        /// <param name="containerIndex"></param>
        /// <returns></returns>
        private float calcVolume(int containerIndex)
        {
            float val = 0;
            int len = modelData.Length;
            for (int i = 0; i < len; i++)
            {
                if (modelData[i].enabled && modelData[i].containerIndex == containerIndex)
                {
                    val += modelData[i].volume;
                }
            }
            if (containerIndex == baseContainerIndex) { val += baseVolume; }
            return val;
        }

        /// <summary>
        /// Calculates the total allocated volume for ALL VolumeContianer sub-containers
        /// </summary>
        /// <returns></returns>
        private float calcTotalVolume()
        {
            float val = 0;
            int len = modelData.Length;
            for (int i = 0; i < len; i++)
            {
                if (modelData[i].enabled)
                {
                    val += modelData[i].volume;
                }
            }
            val += baseVolume;
            return val;
        }

        //TODO
        /// <summary>
        /// Update the cached mass and cost for the -models- that are currently enabled; resources and tankage are handled by VolumeContainer
        /// </summary>
        private void updateMassAndCost()
        {
            //TODO
        }
        
        private void updateAttachNodes(bool userInput)
        {
            int len = modelGroups.Length;
            List<string> enabledNodeNames = new List<string>();
            AttachNode attachNode;
            ModelSwitchGroup group;
            ModelSwitchData model;
            for (int i = 0; i < len; i++)
            {
                group = modelGroups[i];
                model = group.enabledModel;
                if (!controlledNodes.Contains(group.parentNode)) { continue; }//not a node that we should touch...
                if (model == null || model.suppressNode) { continue; }
                enabledNodeNames.Add(group.parentNode);                
                Vector3 pos = group.getModelRootTransform().position;
                MonoBehaviour.print("model root position: " + pos);
                pos = part.transform.InverseTransformPoint(pos);
                Vector3 rotation = group.getModelRootTransform().up;
                MonoBehaviour.print("updating attach node position for node name: " + group.parentNode+" partLocalPos: "+pos);
                attachNode = part.findAttachNode(group.parentNode);
                if (attachNode == null)
                {
                    attachNode = SSTUAttachNodeUtils.createAttachNode(part, group.parentNode, pos, rotation, 2);
                }
                else
                {
                    SSTUAttachNodeUtils.updateAttachNodePosition(part, attachNode, pos, rotation, userInput);
                }
            }
            List<AttachNode> attachNodes = new List<AttachNode>();
            attachNodes.AddRange(part.attachNodes);
            len = attachNodes.Count;
            for (int i = 0; i < len; i++)
            {
                attachNode = attachNodes[i];
                if (!controlledNodes.Contains(attachNode.id)) { continue; }//not a node that we should touch...
                if (attachNode.attachedPart==null && !enabledNodeNames.Contains(attachNode.id))
                {
                    MonoBehaviour.print("destroying attach node for name: " + attachNode.id);
                    SSTUAttachNodeUtils.destroyAttachNode(part, attachNode);
                }
            }
        }
        
        private void updateDragCube()
        {
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

    }

    public class ModelSwitchData
    {
        public readonly string name;
        public readonly string modelName;
        public readonly string groupName = "Main";
        public readonly int containerIndex;
        public readonly Vector3 localPosition;
        public readonly Vector3 localRotation;
        public readonly float scale = 1f;
        public readonly bool suppressNode = true;
        public readonly ModelNodeData[] nodes;
        public readonly ModelDefinition modelDefinition;
        public readonly Part part;
        public readonly ModelSwitchGroup group;

        public bool enabled;
        
        public ModelSwitchData(ConfigNode node, Part owner, ModelSwitchGroup group)
        {
            name = node.GetStringValue("name");
            modelName = node.GetStringValue("modelName", name);
            groupName = node.GetStringValue("group", groupName);
            containerIndex = node.GetIntValue("containerIndex", 0);
            localPosition = node.GetVector3("localPosition", Vector3.zero);
            localRotation = node.GetVector3("localRotation", Vector3.zero);
            scale = node.GetFloatValue("scale", scale);
            nodes = ModelNodeData.load(node.GetStringValues("node"));
            suppressNode = nodes.Length > 0;
            modelDefinition = SSTUModelData.getModelDefinition(modelName);
            if (modelDefinition == null) { throw new NullReferenceException("Could not locate model data for name: " + modelName + " :: " + name); }
            this.part = owner;
            this.group = group;
            this.group.add(this);
        }

        public float volume { get { return modelDefinition.volume * 1000f; } }//adjust from model definition m^3 to resource definition liters

        public float mass { get { return modelDefinition.mass; } }

        public float cost { get { return modelDefinition.cost; } }

        internal void loadPersistentData(string val) { enabled = int.Parse(val) == 1; }

        internal int getPersistentData() { return enabled ? 1 : 0; }

        internal void initialize()
        {
            MonoBehaviour.print("initializing model..." + name);
            if (enabled) { group.enable(this); }
        }

        internal void enable()
        {
            MonoBehaviour.print("Enabling model: " + name + " : " + modelName + " :: " + groupName);
            Transform tr = part.transform.FindRecursive(getBaseTransformName());
            if (tr == null)
            {
                setupModel();
            }
            else
            {
                MonoBehaviour.print("ERROR: Enabled was called for an already enabled model: " + name + " for part: " + part);
            }
            enabled = true;
        }

        internal void disable()
        {
            MonoBehaviour.print("Disabling model: " + name + " : " + modelName + " :: " + groupName);
            if (String.IsNullOrEmpty(modelDefinition.modelName)) { return; }//NOOP for null model
            Transform tr = part.transform.FindRecursive(getBaseTransformName());
            if (tr != null)
            {
                GameObject.DestroyImmediate(tr.gameObject);
            }
            else
            {
                MonoBehaviour.print("ERROR: Disable was called on an already disabled model: " + name + " for part: " + part);
            }
            enabled = false;
        }

        private void setupModel()
        {
            if (!String.IsNullOrEmpty(modelDefinition.modelName))
            {
                Transform baseTransform = getBaseTransform();
                GameObject model = SSTUUtils.cloneModel(modelDefinition.modelName);
                model.transform.NestToParent(baseTransform);
                model.transform.localScale = new Vector3(scale, scale, scale);
            }
        }

        private string getBaseTransformName() { return groupName+"-"+name; }

        private Transform getParentTransform() { return group.getModelRootTransform(); }

        /// <summary>
        /// Get (and/or create) the base transform for this model to reside upon
        /// Base transform is parented to the 'parent' transform (defaults to 'model')
        /// Base transform has local position and rotation applied to it; model is then parented to this new base transform
        /// </summary>
        /// <returns></returns>
        private Transform getBaseTransform()
        {
            Transform parent = getParentTransform();
            Transform baseTransform = parent.transform.FindRecursive(getBaseTransformName());
            if (baseTransform == null)
            {
                GameObject newObj = new GameObject(getBaseTransformName());
                newObj.transform.NestToParent(parent);
                baseTransform = newObj.transform;
            }
            baseTransform.localPosition = localPosition;
            baseTransform.localRotation = Quaternion.Euler(localRotation);
            return baseTransform;
        }
    }

    public class ModelSwitchGroup
    {
        public readonly string name;
        public readonly string defaultModel;
        public readonly string parentGroup;
        public readonly string parentNode;

        public readonly SSTUModelSwitch owner;
        public readonly ModelNode[] modelNodes;

        private List<ModelSwitchData> modelData = new List<ModelSwitchData>();
        private Dictionary<string, ModelSwitchData> modelDataMap = new Dictionary<string, ModelSwitchData>();        
        private ModelSwitchGroup parent;
        private List<ModelSwitchGroup> children = new List<ModelSwitchGroup>();        
        private Dictionary<string, ModelNode> nodeMap = new Dictionary<string, ModelNode>();

        private bool enabled;
        private ModelSwitchData currentEnabledModel;
        private GameObject modelRoot;

        public ModelSwitchGroup(ConfigNode node, SSTUModelSwitch module)
        {
            this.owner = module;
            this.name = node.GetStringValue("name");
            this.defaultModel = node.GetStringValue("defaultModel");
            this.parentGroup = node.GetStringValue("parentGroup");
            this.parentNode = node.GetStringValue("parentNode");
            string[] nodeNames = node.GetStringValues("node");
            int len = nodeNames.Length;
            modelNodes = new ModelNode[len];
            for (int i = 0; i < len; i++)
            {
                modelNodes[i] = new ModelNode(nodeNames[i], this);
                nodeMap.Add(modelNodes[i].name, modelNodes[i]);
            }
        }

        /// <summary>
        /// initialize the default values for model-data; this occurs prior to load()
        /// </summary>
        internal void preInitialize()
        {
            if (!string.IsNullOrEmpty(parentGroup))
            {
                parent = owner.findGroup(parentGroup);
                if (parent == null) { MonoBehaviour.print("parent was null"); }
                parent.addChild(this);
            }
        }

        /// <summary>
        /// if has an enabled model, build model root GO; called after load() so that the data is current
        /// </summary>
        internal void initializeRoot()
        {
            currentEnabledModel = modelData.Find(m => m.enabled);
            if (currentEnabledModel == null)//initialize default
            {
                currentEnabledModel = modelData.Find(m => m.name == defaultModel);
                if (currentEnabledModel == null)
                {
                    //if it is still null... that is a config error and needs to be corrected
                    MonoBehaviour.print("ERROR: Could not locate default model for group: " + name + " model: " + defaultModel);
                }
                currentEnabledModel.enabled = true;                
            }
            enabled = true;
            modelRoot = new GameObject("Root-" + name);
            modelRoot.transform.NestToParent(owner.transform.FindRecursive("model"));
            setupNodesForModel(currentEnabledModel);
            initializeModels();
        }

        internal void load(string data)
        {
            string[] splits = data.Split(',');
            int len = splits.Length;
            for (int i = 1; i < len; i++)
            {
                modelData[i - 1].loadPersistentData(splits[i]);
            }
        }

        internal string save()
        {
            string val = name;
            int len = modelData.Count;
            for (int i = 0; i < len; i++)
            {
                val = val + "," + modelData[i].getPersistentData();
            }
            return val;
        }

        internal Transform getModelRootTransform() { return modelRoot.transform; }

        internal void add(ModelSwitchData data) { this.modelData.Add(data); this.modelDataMap.Add(data.name, data); }

        internal ModelSwitchData get(string name) { return modelDataMap[name]; }

        internal ModelSwitchData get(int index) { return modelData[index]; }

        internal ModelSwitchData enabledModel { get { return currentEnabledModel; } }

        internal void enable(string modelName) { enable(modelData.Find(m => m.name == modelName)); }

        internal void enable(ModelSwitchData data)
        {
            if (!enabled)
            {
                MonoBehaviour.print("ERROR: Attempt to enable a model on a disabled model group.  Model will not be enabled.");
                return;
            }
            foreach (ModelSwitchData d in this.modelData)
            {
                if (d != data) { d.disable(); }
            }
            currentEnabledModel = data;
            currentEnabledModel.enable();
            setupNodesForModel(currentEnabledModel);
        }

        /// <summary>
        /// Return true if this group should be available for GUI selection for editing; depends on if parent group is null or enabled
        /// </summary>
        /// <returns></returns>
        internal bool isAvailable()
        {
            return enabled && modelData.Count > 1;
        }

        /// <summary>
        /// Return an array containing the names of the models for this group; to be used to populate GUI selection data
        /// </summary>
        /// <returns></returns>
        internal string[] getModelNames()
        {
            return modelData.Select(m => m.name).ToArray();
        }

        /// <summary>
        /// Enable this model group; enable on default model, links up models to nodes, upwards-recurse enable children groups
        /// </summary>
        internal void enableGroup()
        {
            MonoBehaviour.print("Enabling model group: " + name);
            if (enabled) { return; }
            enabled = true;
            modelRoot = new GameObject("Root-" + name);
            if (parent == null)
            {
                MonoBehaviour.print("ERROR: Attempt to enable a group at runtime with no parent defined.  This should never happen.  Bad things are likely to occur soon.");
                //TODO -- yah.. this should be unpossible
            }
            else
            {
                ModelSwitchData model = modelData.Find(m => m.enabled == true);//for cases where data was loaded from persistence
                if (model == null)
                {
                    model = enableDefaultModel();//otherwise just enable the default from the config
                }
                ModelNode node = parent.findNode(parentNode);
                if (node == null) { MonoBehaviour.print("ERROR: Node was null for name: "+parentNode+" for group: "+name); }
                modelRoot.transform.NestToParent(node.transform);
                setupNodesForModel(model);
                initializeModels();
            }
        }

        /// <summary>
        /// Disable this model group; recurse through children and disable them, delete all models, delete all model nodes
        /// </summary>
        internal void disableGroup()
        {
            MonoBehaviour.print("Disabling group: "+name);
            int len = children.Count;
            for (int i = 0; i < len; i++)
            {
                children[i].disableGroup();
            }
            len = modelData.Count;
            for (int i = 0; i < len; i++)
            {
                modelData[i].disable();
            }
            len = modelNodes.Length;
            for (int i = 0; i < len; i++)
            {
                modelNodes[i].disableNode();
            }
            if (modelRoot != null)
            {
                GameObject.DestroyImmediate(modelRoot);
                modelRoot = null;
            }
            enabled = false;
            currentEnabledModel = null;
        }

        /// <summary>
        /// enable/disable nodes depending on if they are enabled/disabled for the currently enabled model
        /// </summary>
        /// <param name="data"></param>
        private void setupNodesForModel(ModelSwitchData data)
        {
            //first check existing nodes and remove any that are not present in new model
            int len = modelNodes.Length;
            for (int i = 0; i < len; i++)
            {
                if (Array.Find(data.nodes, m => m.name == modelNodes[i].name) == null)//model data contains no def for node; disable it
                {
                    modelNodes[i].disableNode();
                }
            }
            //position all active nodes
            len = data.nodes.Length;
            ModelNode node;
            ModelNodeData nodeData;
            for (int i = 0; i < len; i++)
            {
                nodeData = data.nodes[i];
                node = Array.Find(modelNodes, m => m.name == nodeData.name);
                node.enableNode(nodeData, modelRoot);
            }
        }

        /// <summary>
        /// build currently enabled model, ensure no others are present
        /// </summary>
        private void initializeModels()
        {
            int len = modelData.Count;
            for (int i = 0; i < len; i++)
            {
                modelData[i].initialize();
            }
        }

        /// <summary>
        /// Update the enabled/disabled status for all models, setting the default model to enabled==true<para/>
        /// Does not create the actual models, merely updates their status; models are created with the initializeModels() call
        /// </summary>
        private ModelSwitchData enableDefaultModel()
        {
            int len = modelData.Count;
            ModelSwitchData data = null;
            for (int i = 0; i < len; i++)
            {
                modelData[i].enabled = modelData[i].name == defaultModel;
                if (modelData[i].enabled)
                {
                    data = modelData[i];
                }
            }
            return data;
        }

        private void addChild(ModelSwitchGroup group)
        {
            children.Add(group);
            nodeMap[group.parentNode].addChild(group);
        }

        private ModelNode findNode(string name) { return Array.Find(modelNodes, m => m.name == name); }

    }

    public class ModelNodeData
    {
        public readonly string name;
        public readonly Vector3 position;
        public readonly Vector3 rotation;
        public readonly bool createAttachNode;
        
        public ModelNodeData(string nodeDef)
        {
            string[] split = nodeDef.Split(',');
            name = split[0];
            position = new Vector3(float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3]));
            rotation = new Vector3(float.Parse(split[4]), float.Parse(split[5]), float.Parse(split[6]));
            createAttachNode = int.Parse(split[7]) == 1;
        }
        
        public static ModelNodeData[] load(string[] vals)
        {
            int len = vals.Length;
            ModelNodeData[] data = new ModelNodeData[len];
            for (int i = 0; i < len; i++)
            {
                data[i] = new ModelNodeData(vals[i]);
            }
            return data;
        }
    }

    public class ModelNode
    {
        public readonly string name;
        private ModelSwitchGroup parent;
        private List<ModelSwitchGroup> children = new List<ModelSwitchGroup>();

        private ModelNodeData currentData;
        private GameObject node;

        public ModelNode(string name, ModelSwitchGroup parent)
        {
            this.name = name;
            this.parent = parent;
        }

        public Transform transform { get { return node == null ? null : node.transform; } }

        public ModelNodeData data { get { return currentData; } }
        
        public void addChild(ModelSwitchGroup group) { children.Add(group); }

        public void enableNode(ModelNodeData data, GameObject root)
        {
            if (node == null)
            {
                node = new GameObject("ModelNode-" + name);
            }
            else
            {
                MonoBehaviour.print("ERROR: Attempt to enable an already enabled node, name: " + name + " parent: " + parent);
            }
            currentData = data;
            node.transform.NestToParent(root.transform);
            node.transform.localPosition = data.position;
            node.transform.localRotation = Quaternion.Euler(data.rotation);
            int len = children.Count;
            for (int i = 0; i < len; i++)
            {
                children[i].enableGroup();
            }
        }

        public void disableNode()
        {
            int len = children.Count;
            for (int i = 0; i < len; i++)
            {
                children[i].disableGroup();
            }
            if (node != null)
            {
                GameObject.DestroyImmediate(node);
                node = null;
            }
        }
    }
}
