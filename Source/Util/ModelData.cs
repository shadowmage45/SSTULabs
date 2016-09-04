using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{

    /// <summary>
    /// Static loading/management class for ModelBaseData.  This class is responsible for loading ModelBaseData from configs and returning ModelBaseData instances for an input model name. 
    /// </summary>
    public static class SSTUModelData
    {
        private static Dictionary<String, ModelDefinition> baseModelData = new Dictionary<String, ModelDefinition>();
        private static bool defsLoaded = false;

        private static void loadDefs()
        {
            if (defsLoaded) { return; }
            defsLoaded = true;
            ConfigNode[] modelDatas = GameDatabase.Instance.GetConfigNodes("SSTU_MODEL");
            ModelDefinition data;
            foreach (ConfigNode node in modelDatas)
            {
                data = new ModelDefinition(node);
                MonoBehaviour.print("Loading model definition data for name: " + data.name + " with model URL: " + data.modelName);
                baseModelData.Add(data.name, data);
            }
        }

        public static ModelDefinition getModelDefinition(String name)
        {
            if (!defsLoaded) { loadDefs(); }
            ModelDefinition data = null;
            baseModelData.TryGetValue(name, out data);
            return data;
        }

        public static void loadConfigData()
        {
            defsLoaded = false;
            baseModelData.Clear();
            loadDefs();
        }
    }

    /// <summary>
    /// Data storage for persistent data about a single loaded model.
    /// Includes height, volume, mass, cost, tech-limitations, node positions, 
    /// texture-set data, and whether the model is intended for top or bottom stack mounting.
    /// </summary>
    public class ModelDefinition
    {
        public readonly ConfigNode configNode;
        public readonly String modelName;
        public readonly String name;
        public readonly String techLimit = "start";        
        public readonly float height = 1;
        public readonly float volume = 0;
        public readonly float mass = 0;
        public readonly float cost = 0;
        public readonly float diameter = 5;
        public readonly float verticalOffset = 0;
        public readonly bool invertForTop = false;
        public readonly bool invertForBottom = false;
        public readonly bool fairingDisabled = false;
        public readonly float fairingTopOffset = 0;
        public readonly float rcsVerticalPosition = 0;
        public readonly float rcsHorizontalPosition = 0;
        public readonly float rcsVerticalRotation = 0;
        public readonly float rcsHorizontalRotation = 0;
        public readonly SubModelData[] subModelData;
        public readonly AttachNodeBaseData[] attachNodeData;
        public readonly AttachNodeBaseData surfaceNode;
        public readonly String defaultTextureSet;
        public readonly ModelTextureSet[] textureSets;

        public ModelDefinition(ConfigNode node)
        {
            configNode = node;
            name = node.GetStringValue("name", String.Empty);
            modelName = node.GetStringValue("modelName", String.Empty);
            techLimit = node.GetStringValue("techLimit", techLimit);
            height = node.GetFloatValue("height", height);
            volume = node.GetFloatValue("volume", volume);
            mass = node.GetFloatValue("mass", mass);
            cost = node.GetFloatValue("cost", cost);
            diameter = node.GetFloatValue("diameter", diameter);
            verticalOffset = node.GetFloatValue("verticalOffset", verticalOffset);
            invertForTop = node.GetBoolValue("invertForTop", invertForTop);
            invertForBottom = node.GetBoolValue("invertForBottom", invertForBottom);

            fairingDisabled = node.GetBoolValue("fairingDisabled", fairingDisabled);
            fairingTopOffset = node.GetFloatValue("fairingTopOffset", fairingTopOffset);
            rcsVerticalPosition = node.GetFloatValue("rcsVerticalPosition", rcsVerticalPosition);
            rcsHorizontalPosition = node.GetFloatValue("rcsHorizontalPosition", rcsHorizontalPosition);
            rcsVerticalRotation = node.GetFloatValue("rcsVerticalRotation", rcsVerticalRotation);
            rcsHorizontalRotation = node.GetFloatValue("rcsHorizontalRotation", rcsHorizontalRotation);

            ConfigNode[] subModelNodes = node.GetNodes("SUBMODEL");
            int len = subModelNodes.Length;
            subModelData = new SubModelData[len];
            for (int i = 0; i < len; i++)
            {
                subModelData[i] = new SubModelData(subModelNodes[i]);
            }

            defaultTextureSet = node.GetStringValue("defaultTextureSet");

            String[] attachNodeStrings = node.GetValues("node");
            len = attachNodeStrings.Length;
            attachNodeData = new AttachNodeBaseData[len];
            for (int i = 0; i < len; i++)
            {
                attachNodeData[i] = new AttachNodeBaseData(attachNodeStrings[i]);
            }

            ConfigNode[] textureSetNodes = node.GetNodes("TEXTURESET");
            len = textureSetNodes.Length;
            textureSets = new ModelTextureSet[len];
            for (int i = 0; i < len; i++)
            {
                textureSets[i] = new ModelTextureSet(textureSetNodes[i]);
            }
            if (node.HasValue("surface"))
            {
                surfaceNode = new AttachNodeBaseData(node.GetStringValue("surface"));
            }
            else
            {
                String val = (diameter*0.5f) + ",0,0,1,0,0,2";
                surfaceNode = new AttachNodeBaseData(val);
            }
        }

        /// <summary>
        /// Return true if this part should be accessible due to tech-level limitations
        /// UNUSED
        /// </summary>
        /// <returns></returns>
        public bool isAvailable()
        {
            if (String.IsNullOrEmpty(techLimit)) { return true; }
            if (HighLogic.CurrentGame == null) { return true; }
            if (ResearchAndDevelopment.Instance == null) { return true; }
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX) { return true; }
            return SSTUUtils.isTechUnlocked(techLimit);
        }

        public string[] getTextureSetNames()
        {
            int len = textureSets.Length;
            string[] names = new string[len];
            for (int i = 0; i < len; i++)
            {
                names[i] = textureSets[i].name;
            }
            return names;
        }
        
    }

    public class SubModelData
    {
        public readonly string modelURL;
        public readonly string[] modelMeshes;
        public readonly string parent;
        public readonly Vector3 rotation;
        public readonly Vector3 position;
        public readonly Vector3 scale;
        public SubModelData(ConfigNode node)
        {
            modelURL = node.GetStringValue("modelName");
            modelMeshes = node.GetStringValues("transform");
            parent = node.GetStringValue("parent", string.Empty);
            position = node.GetVector3("position", Vector3.zero);
            rotation = node.GetVector3("rotation", Vector3.zero);
            scale = node.GetVector3("scale", Vector3.one);
        }

        public void setupSubmodel(GameObject modelRoot)
        {
            Transform[] trs = modelRoot.transform.GetAllChildren();
            int len = trs.Length;
            for (int i = 0; i < len; i++)
            {
                if (!isActiveMesh(trs[i].name))
                {
                    GameObject.DestroyImmediate(trs[i].gameObject);
                }
            }
        }

        private bool isActiveMesh(string transformName)
        {
            int len = modelMeshes.Length;
            bool found = false;
            for (int i = 0; i < len; i++)
            {
                if (modelMeshes[i] == transformName)
                {
                    found = true;
                    break;
                }
            }
            return found;
        }
    }

    public class ModelTextureSet
    {
        public readonly String name;
        public readonly ModelTextureData[] textureData;

        public ModelTextureSet(ConfigNode node)
        {
            name = node.GetStringValue("name");
            ConfigNode[] texNodes = node.GetNodes("TEXTURE");
            int len = texNodes.Length;
            textureData = new ModelTextureData[len];
            for (int i = 0; i < len; i++)
            {
                textureData[i] = new ModelTextureData(texNodes[i]);
            }
        }

        public void enable(GameObject root)
        {
            foreach (ModelTextureData mtd in textureData)
            {
                mtd.enable(root);
            }
        }
    }

    public class ModelTextureData
    {
        public readonly String diffuseTextureName;
        public readonly String normalTextureName;
        public readonly String emissiveTextureName;
        public readonly String[] meshNames;

        public ModelTextureData(ConfigNode node)
        {
            diffuseTextureName = node.GetStringValue("diffuseTexture");
            normalTextureName = node.GetStringValue("normalTexture");
            meshNames = node.GetStringValues("mesh");
        }

        public void enable(GameObject root)
        {
            Renderer r;
            foreach (String meshName in meshNames)
            {
                Transform tr;
                Transform[] trs = root.transform.FindChildren(meshName);
                int len = trs.Length;
                for (int i = 0; i < len; i++)
                {
                    tr = trs[i];
                    if (tr == null) { continue; }
                    if (tr != null && (r = tr.GetComponent<Renderer>()) != null)
                    {
                        Material m = r.material;
                        if (!String.IsNullOrEmpty(diffuseTextureName)) { m.mainTexture = GameDatabase.Instance.GetTexture(diffuseTextureName, false); }
                        if (!String.IsNullOrEmpty(normalTextureName)) { m.SetTexture("_BumpMap", GameDatabase.Instance.GetTexture(normalTextureName, true)); }
                        if (!String.IsNullOrEmpty(emissiveTextureName)) { m.SetTexture("_Emissive", GameDatabase.Instance.GetTexture(emissiveTextureName, false)); }
                    }
                }
            }
        }

    }

    public enum ModelOrientation
    {
        TOP,
        CENTRAL,
        BOTTOM
    }

    /// <summary>
    /// Abstract live-data class for storage of model data for an active part.
    /// Holds a reference to the peristent non-changable model-data.
    /// Includes basic utility methods for enabling a texture set for this model instance
    /// </summary>
    public class ModelData
    {
        public ModelDefinition modelDefinition;
        public readonly String name;
        public readonly float baseScale = 1f;

        public float volume;
        public float mass;
        public float cost;
        public float minVerticalScale;
        public float maxVerticalScale;
        public float currentDiameterScale = 1f;
        public float currentHeightScale = 1f;
        public float currentDiameter;
        public float currentHeight;
        public float currentVerticalPosition;

        public ModelData(ConfigNode node)
        {
            name = node.GetStringValue("name");
            modelDefinition = SSTUModelData.getModelDefinition(name);
            if (modelDefinition==null)
            {
                MonoBehaviour.print("ERROR: Could not locate model data for name: " + name);
            }
            currentDiameter = modelDefinition.diameter;
            currentHeight = modelDefinition.height;
            volume = node.GetFloatValue("volume", modelDefinition.volume);
            mass = node.GetFloatValue("mass", modelDefinition.mass);
            cost = node.GetFloatValue("cost", modelDefinition.cost);
            minVerticalScale = node.GetFloatValue("minVerticalScale", 1f);
            maxVerticalScale = node.GetFloatValue("maxVerticalScale", 1f);
            baseScale = node.GetFloatValue("scale",  baseScale);
        }

        public ModelData(String name)
        {
            this.name = name;
            modelDefinition = SSTUModelData.getModelDefinition(name);
            if (modelDefinition == null)
            {
                MonoBehaviour.print("ERROR: Could not locate model definition data for name: " + name);
            }
            currentDiameter = modelDefinition.diameter;
            currentHeight = modelDefinition.height;
            volume = modelDefinition.volume;
            mass = modelDefinition.mass;
            cost = modelDefinition.cost;
        }

        public bool isValidTextureSet(String val)
        {
            bool noTextures = modelDefinition.textureSets.Length == 0;
            if (String.IsNullOrEmpty(val))
            {
                return noTextures;
            }
            if (val == modelDefinition.defaultTextureSet) { return true; }
            foreach (ModelTextureSet set in modelDefinition.textureSets)
            {
                if (set.name == val) { return true; }
            }
            return false;
        }

        public void updateScaleForDiameter(float newDiameter)
        {
            float newScale = newDiameter / modelDefinition.diameter;
            updateScale(newScale);
        }

        public void updateScaleForHeightAndDiameter(float newHeight, float newDiameter)
        {
            float newHorizontalScale = newDiameter / modelDefinition.diameter;
            float newVerticalScale = newHeight / modelDefinition.height;
            updateScale(newHorizontalScale, newVerticalScale);
        }

        public void updateScale(float newScale)
        {
            updateScale(newScale, newScale);
        }

        public void updateScale(float newHorizontalScale, float newVerticalScale)
        {
            currentDiameterScale = newHorizontalScale;
            currentHeightScale = newVerticalScale;
            currentHeight = newVerticalScale * modelDefinition.height;
            currentDiameter = newHorizontalScale * modelDefinition.diameter;
        }

        public bool isAvailable()
        {
            return modelDefinition.isAvailable();
        }

        public SSTUAnimData[] getAnimationData(Transform transform)
        {
            SSTUAnimData[] data = null;
            ConfigNode[] nodes = modelDefinition.configNode.GetNodes("ANIMATION");
            int len = nodes.Length;
            data = new SSTUAnimData[len];
            for (int i = 0; i < len; i++)
            {
                data[i] = new SSTUAnimData(nodes[i], transform);
            }
            return data;
        }

        public bool hasAnimation()
        {
            return modelDefinition.configNode.HasNode("ANIMATION");
        }

        /// <summary>
        /// Updates the input texture-control text field with the texture-set names for this model.  Disables field if no texture sets found, enables field if more than one texture set is available.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="fieldName"></param>
        /// <param name="currentTexture"></param>
        public virtual void updateTextureUIControl(PartModule module, string fieldName, string currentTexture)
        {
            string[] names = modelDefinition.getTextureSetNames();
            module.updateUIChooseOptionControl(fieldName, names, names, true, currentTexture);
            module.Fields[fieldName].guiActiveEditor = names.Length > 1;
        }

        public virtual void setupModel(Transform parent, ModelOrientation orientation)
        {
            throw new NotImplementedException();
        }

        public virtual void updateModel()
        {
            throw new NotImplementedException();
        }

        public virtual void destroyCurrentModel()
        {
            throw new NotImplementedException();
        }

        public virtual float getModuleVolume()
        {            
            return currentDiameterScale * currentDiameterScale * currentHeightScale * baseScale * volume;
        }

        public virtual float getModuleMass()
        {
            return mass * currentDiameterScale * currentDiameterScale * currentHeightScale * baseScale;
        }

        public virtual float getModuleCost()
        {
            return cost * currentDiameterScale * currentDiameterScale * currentHeightScale * baseScale;
        }

        public float getVerticalOffset()
        {
            return currentHeightScale * baseScale * modelDefinition.verticalOffset;
        }

        /// <summary>
        /// Updates the internal position reference for the input position
        /// Includes offsetting for the models offset; the input position should be the desired location
        /// of the bottom of the model.
        /// </summary>
        /// <param name="positionOfBottomOfModel"></param>
        public void setPosition(float positionOfBottomOfModel, ModelOrientation orientation = ModelOrientation.TOP)
        {
            float offset = getVerticalOffset();
            if (orientation == ModelOrientation.BOTTOM) { offset = -offset; }
            currentVerticalPosition = positionOfBottomOfModel + offset;
        }

        public static string[] getModelNames(ModelData[] data)
        {
            int len = data.Length;
            string[] vals = new string[len];
            for (int i = 0; i < len; i++)
            {
                vals[i] = data[i].name;
            }
            return vals;
        }
    }
    
    /// <summary>
    /// Live-data container for a single set of model-data, includes utility methods for model setup, updating, and removal
    /// </summary>
    public class SingleModelData : ModelData
    {

        public GameObject model;

        public SingleModelData(ConfigNode node) : base(node)
        {
        }

        public SingleModelData(String name) : base(name)
        {
        }

        /// <summary>
        /// Enables the input texture set name, or default texture for the model if 'none' or 'default' is input for the set name
        /// </summary>
        /// <param name="setName"></param>
        public void enableTextureSet(String setName)
        {
            ModelTextureSet mts = Array.Find(modelDefinition.textureSets, m => m.name == setName);
            if (String.IsNullOrEmpty(setName) || setName == "none" || setName == "default" ||  mts==null)
            {
                if (setName == "none" || setName == "default" || String.IsNullOrEmpty(setName))
                {
                    return;
                }
                MonoBehaviour.print("ERROR: No texture set data for set by name: " + setName + "  --  for model: " + name);
                if (String.IsNullOrEmpty(modelDefinition.defaultTextureSet))
                {
                    MonoBehaviour.print("ERROR: Default texture set was null or empty string, this is a configuration error. " +
                        "Please correct the model definition for model: " +
                        modelDefinition.name + " to add a proper default texture set definition.");
                }
                int len = modelDefinition.textureSets.Length;
                MonoBehaviour.print("Texture sets avaialble for model: "+len +". Default set: "+modelDefinition.defaultTextureSet);                
                for (int i = 0; i < len; i++)
                {
                    MonoBehaviour.print("\n" + modelDefinition.textureSets[i].name);
                }
            }
            else if(model!=null)
            {
                mts.enable(model);
            }
        }

        public string getDefaultTextureSet()
        {
            if (isValidTextureSet(modelDefinition.defaultTextureSet)) { return modelDefinition.defaultTextureSet; }
            if (modelDefinition.textureSets.Length > 0) { return modelDefinition.textureSets[0].name; }
            return "default";
        }

        public override void setupModel(Transform parent, ModelOrientation orientation)
        {
            setupModel(parent, orientation, false);
        }

        public void setupModel(Transform parent, ModelOrientation orientation, bool reUse)
        {
            if (modelDefinition.subModelData.Length <= 0)
            {
                constructSingleModel(parent, orientation, reUse);
            }
            else
            {
                constructSubModels(parent, orientation, reUse);
            }
        }

        public override void updateModel()
        {
            if (model != null)
            {
                model.transform.localScale = new Vector3(currentDiameterScale * baseScale, currentHeightScale * baseScale, currentDiameterScale * baseScale);
                model.transform.localPosition = new Vector3(0, currentVerticalPosition, 0);
            }
        }

        public override void destroyCurrentModel()
        {
            if (model == null) { return; }
            model.transform.parent = null;
            GameObject.Destroy(model);
            model = null;
        }
        
        private void constructSingleModel(Transform parent, ModelOrientation orientation, bool reUse)
        {
            String modelName = modelDefinition.modelName;
            if (String.IsNullOrEmpty(modelName))//no model to setup
            {
                return;
            }
            if (reUse)
            {
                Transform tr = parent.transform.FindModel(modelDefinition.modelName);
                if (tr != null)
                {
                    tr.name = modelDefinition.modelName;
                    tr.gameObject.name = modelDefinition.modelName;
                }
                model = tr == null ? null : tr.gameObject;
            }
            if (!String.IsNullOrEmpty(modelName) && model == null)
            {
                model = SSTUUtils.cloneModel(modelName);
            }
            if (model != null)
            {
                model.transform.NestToParent(parent);
                if ((modelDefinition.invertForTop && orientation == ModelOrientation.TOP) || (modelDefinition.invertForBottom && orientation == ModelOrientation.BOTTOM))
                {
                    model.transform.Rotate(new Vector3(0, 0, 1), 180, Space.Self);
                }
            }
            else
            {
                MonoBehaviour.print("ERROR: Could not locate model for name: " + modelName);
            }
        }

        private void constructSubModels(Transform parent, ModelOrientation orientation, bool reUse)
        {        
            String modelName = modelDefinition.modelName;
            if (String.IsNullOrEmpty(modelName))//no model name given, log user-error for them to correct
            {
                MonoBehaviour.print("ERROR: Could not setup sub-models for ModelDefinition: " + modelDefinition.name + " as no modelName was specified to use as the root transform.");
                MonoBehaviour.print("Please add a modelName to this model definition to enable sub-model creation.");
                return;
            }
            //attempt to re-use the model if it is already present on the part
            if (reUse)
            {
                Transform tr = parent.transform.FindModel(modelDefinition.modelName);
                if (tr != null)
                {
                    tr.name = modelDefinition.modelName;
                    tr.gameObject.name = modelDefinition.modelName;
                }
                model = tr == null ? null : tr.gameObject;
            }
            //not re-used, recreate it entirely from the sub-model data
            if (model == null)
            {
                //create a new GO at 0,0,0 with default orientation and the given model name
                //this will be the 'parent' for sub-model creation
                model = new GameObject(modelName);
                
                SubModelData[] smds = modelDefinition.subModelData;
                SubModelData smd;
                GameObject clonedModel;
                Transform localParent;
                int len = smds.Length;
                //add sub-models to the base model transform
                for (int i = 0; i < len; i++)
                {
                    smd = smds[i];
                    clonedModel = SSTUUtils.cloneModel(smd.modelURL);
                    if (clonedModel == null) { continue;}//TODO log error
                    clonedModel.transform.NestToParent(model.transform);
                    clonedModel.transform.localRotation = Quaternion.Euler(smd.rotation);
                    clonedModel.transform.localPosition = smd.position;
                    clonedModel.transform.localScale = smd.scale;
                    if (!string.IsNullOrEmpty(smd.parent))
                    {
                        localParent = model.transform.FindRecursive(smd.parent);
                        if (localParent != null)
                        {
                            clonedModel.transform.parent = localParent;
                        }
                    }
                    //de-activate any non-active sub-model transforms
                    //iterate through all transforms for the model and deactivate(destroy?) any not on the active mesh list
                    if (smd.modelMeshes.Length > 0)
                    {
                        smd.setupSubmodel(clonedModel);
                    }                    
                }
            }
            //regardless of if it was new or re-used, reset its position and orientation
            model.transform.NestToParent(parent);
            if ((modelDefinition.invertForTop && orientation == ModelOrientation.TOP) || (modelDefinition.invertForBottom && orientation == ModelOrientation.BOTTOM))
            {
                model.transform.Rotate(new Vector3(0, 0, 1), 180, Space.Self);
            }
        }

        /// <summary>
        /// Updates the attach nodes on the part for the input list of attach nodes and the current specified nodes for this model.
        /// Any 'extra' attach nodes from the part will be disabled.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="nodeNames"></param>
        /// <param name="userInput"></param>
        /// <param name="orientation"></param>
        public void updateAttachNodes(Part part, String[] nodeNames, bool userInput, ModelOrientation orientation)
        {
            if (nodeNames.Length == 1 && (nodeNames[0] == "NONE" || nodeNames[0]=="none")) { return; }
            Vector3 basePos = new Vector3(0, currentVerticalPosition, 0);
            AttachNode node = null;
            AttachNodeBaseData data;

            int nodeCount = modelDefinition.attachNodeData.Length;
            int len = nodeNames.Length;

            Vector3 pos = Vector3.zero;
            Vector3 orient = Vector3.up;
            int size = 2;

            bool invert = (orientation == ModelOrientation.BOTTOM && modelDefinition.invertForBottom) || (orientation == ModelOrientation.TOP && modelDefinition.invertForTop);
            for (int i = 0; i < len; i++)
            {
                node = part.findAttachNode(nodeNames[i]);
                if (i < nodeCount)
                {
                    data = modelDefinition.attachNodeData[i];
                    size = data.size;
                    pos = data.position * currentHeightScale;
                    if (invert)
                    {
                        pos.y = -pos.y;
                        pos.x = -pos.x;
                    }
                    pos.y += currentVerticalPosition;
                    orient = data.orientation;
                    if (invert) { orient = -orient; orient.z = -orient.z; }
                    if (node == null)//create it
                    {
                        SSTUAttachNodeUtils.createAttachNode(part, nodeNames[i], pos, orient, size);
                    }
                    else//update its position
                    {
                        SSTUAttachNodeUtils.updateAttachNodePosition(part, node, pos, orient, userInput);
                    }
                }
                else//extra node, destroy
                {
                    if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
                    {
                        SSTUAttachNodeUtils.destroyAttachNode(part, node);
                    }
                }
            }
        }

        /// <summary>
        /// Determine if the number of parts attached to the part will prevent this mount from being applied;
        /// if any node that has a part attached would be deleted, return false.  To be used for GUI validation
        /// to determine what modules are valid 'swap' selections for the current part setup.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="nodeNames"></param>
        /// <returns></returns>
        public bool canSwitchTo(Part part, String[] nodeNames)
        {
            AttachNode node;
            int len = nodeNames.Length;
            for (int i = 0; i < len; i++)
            {
                if (i < modelDefinition.attachNodeData.Length) { continue; }//don't care about those nodes, they will be present
                node = part.findAttachNode(nodeNames[i]);//this is a node that would be disabled
                if (node == null) { continue; }//already disabled, and that is just fine
                else if (node.attachedPart != null) { return false; }//drat, this node is scheduled for deletion, but has a part attached; cannot delete it, so cannot switch to this mount
            }
            return true;//and if all node checks go okay, return true by default...
        }

        public override string ToString()
        {
            return "SINGLEMODEL: " + name;
        }

        /// <summary>
        /// Parse an array of models given an array of ConfigNodes for those models.  Simple utility / convenience method for part initialization.
        /// </summary>
        /// <param name="modelNodes"></param>
        /// <returns></returns>
        public static SingleModelData[] parseModels(ConfigNode[] modelNodes, bool insertDefault = false)
        {
            int len = modelNodes.Length;
            SingleModelData[] datas;

            if (len == 0 && insertDefault)
            {
                datas = new SingleModelData[1];
                datas[0] = new SingleModelData("Mount-None");
                return datas;
            }

            datas = new SingleModelData[len];
            for (int i = 0; i < len; i++)
            {
                datas[i] = new SingleModelData(modelNodes[i]);
            }
            return datas;
        }

        /// <summary>
        /// Find the given ModelData from the input array for the input name.  If not found, returns null.
        /// </summary>
        /// <param name="models"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static SingleModelData findModel(SingleModelData[] models, string name)
        {
            SingleModelData model = null;
            int len = models.Length;
            for (int i = 0; i < len; i++)
            {
                if (models[i].name == name)
                {
                    model = models[i];
                    break;
                }
            }
            return model;
        }

        /// <summary>
        /// Returns an array of valid selectable models given the current part attach-node setup (if parts are attached to enabled nodes)
        /// </summary>
        /// <param name="part"></param>
        /// <param name="models"></param>
        /// <param name="nodeNames"></param>
        /// <returns></returns>
        public static string[] getValidSelectionNames(Part part, SingleModelData[] models, string[] nodeNames)
        {
            List<String> names = new List<String>();
            int len = models.Length;
            for (int i = 0; i < len; i++)
            {
                if (models[i].canSwitchTo(part, nodeNames))
                {
                    names.Add(models[i].name);
                }
            }
            return names.ToArray();
        }

    }
}
