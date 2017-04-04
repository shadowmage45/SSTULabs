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
                if (baseModelData.ContainsKey(data.name))
                {
                    MonoBehaviour.print("ERROR: Model defs already contains def for name: " + data.name + ".  Please check your configs as this is an error.  The duplicate entry was found in the config node of:\n"+node);
                    continue;
                }
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
        public readonly String title;
        public readonly String description;
        public readonly String icon;
        public readonly string upgradeUnlockName;
        public readonly float height = 1;
        public readonly float volume = 0;
        public readonly float mass = 0;
        public readonly float cost = 0;
        public readonly float diameter = 5;
        public readonly float verticalOffset = 0;
        public readonly bool invertForTop = false;
        public readonly bool invertForBottom = false;
        public readonly Vector3 invertAxis = Vector3.forward;
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
        public readonly TextureSet[] textureSets;

        public ModelDefinition(ConfigNode node)
        {
            configNode = node;
            name = node.GetStringValue("name", String.Empty);
            title = node.GetStringValue("title", name);
            description = node.GetStringValue("description", title);
            icon = node.GetStringValue("icon");
            modelName = node.GetStringValue("modelName", String.Empty);
            upgradeUnlockName = node.GetStringValue("upgradeUnlock", upgradeUnlockName);
            height = node.GetFloatValue("height", height);
            volume = node.GetFloatValue("volume", volume);
            mass = node.GetFloatValue("mass", mass);
            cost = node.GetFloatValue("cost", cost);
            diameter = node.GetFloatValue("diameter", diameter);
            verticalOffset = node.GetFloatValue("verticalOffset", verticalOffset);
            invertForTop = node.GetBoolValue("invertForTop", invertForTop);
            invertForBottom = node.GetBoolValue("invertForBottom", invertForBottom);
            invertAxis = node.GetVector3("invertAxis", invertAxis);
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
            textureSets = new TextureSet[len];
            for (int i = 0; i < len; i++)
            {
                textureSets[i] = new TextureSet(textureSetNodes[i]);
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

        public bool isAvailable(List<String> partUpgrades)
        {
            return String.IsNullOrEmpty(upgradeUnlockName) || partUpgrades.Contains(upgradeUnlockName);
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
            if (modelMeshes.Length > 0)
            {
                Transform[] trs = modelRoot.transform.GetAllChildren();
                List<Transform> toKeep = new List<Transform>();
                List<Transform> toCheck = new List<Transform>();
                int len = trs.Length;
                for (int i = 0; i < len; i++)
                {
                    if (trs[i] == null)
                    {
                        continue;
                    }
                    else if (isActiveMesh(trs[i].name))
                    {
                        toKeep.Add(trs[i]);
                    }
                    else
                    {
                        toCheck.Add(trs[i]);
                    }
                }
                List<Transform> transformsToDelete = new List<Transform>();
                len = toCheck.Count;
                for (int i = 0; i < len; i++)
                {
                    if (!isParent(toCheck[i], toKeep))
                    {
                        transformsToDelete.Add(toCheck[i]);
                    }
                }
                len = transformsToDelete.Count;
                for (int i = 0; i < len; i++)
                {
                    GameObject.DestroyImmediate(transformsToDelete[i].gameObject);
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

        private bool isParent(Transform toCheck, List<Transform> children)
        {
            int len = children.Count;
            for (int i = 0; i < len; i++)
            {
                if (children[i].isParent(toCheck)) { return true; }
            }
            return false;
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
            foreach (TextureSet set in modelDefinition.textureSets)
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

        public bool isAvailable(List<String> partUpgrades)
        {
            return modelDefinition.isAvailable(partUpgrades);
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

        /// <summary>
        /// Creates the transforms and meshes for the model using default positioning and orientation defined by the passed in ModelOrientation parameter
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="orientation"></param>
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

        /// <summary>
        /// Get array of model data names, taking into consideration upgrade-unlocks available on the part module
        /// </summary>
        /// <param name="data"></param>
        /// <param name="module"></param>
        /// <returns></returns>
        public static string[] getAvailableModelNames(ModelData[] data, PartModule module)
        {
            List<string> names = new List<string>();
            int len = data.Length;
            for (int i = 0; i < len; i++)
            {
                if (data[i].isAvailable(module.upgradesApplied))
                {
                    names.Add(data[i].name);
                }
            }
            return names.ToArray();
        }

        /// <summary>
        /// Example Use:
        /// CustomModelData[] tmds = parseModels<TankModelData>(nodes, m => { return new CustomModelData(m); });
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="nodes"></param>
        /// <param name="constructor"></param>
        /// <returns></returns>
        public static T[] parseModels<T>(ConfigNode[] nodes, Func<ConfigNode,T> constructor) where T : ModelData
        {
            int len = nodes.Length;
            T[] ts = new T[len];
            for (int i = 0; i < len; i++)
            {
                ts[i] = constructor.Invoke(nodes[i]);
            }
            return ts;
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
            if (nodeNames.Length == 1 && (nodeNames[0] == "NONE" || nodeNames[0] == "none")) { return; }
            float currentVerticalPosition = this.currentVerticalPosition;
            float offset = getVerticalOffset();
            if (orientation == ModelOrientation.BOTTOM) { offset = -offset; }
            currentVerticalPosition -= offset;

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
                node = part.FindAttachNode(nodeNames[i]);
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
                node = part.FindAttachNode(nodeNames[i]);//this is a node that would be disabled
                if (node == null) { continue; }//already disabled, and that is just fine
                else if (node.attachedPart != null) { return false; }//drat, this node is scheduled for deletion, but has a part attached; cannot delete it, so cannot switch to this mount
            }
            return true;//and if all node checks go okay, return true by default...
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

        public static T[] getValidSelections<T>(Part part, IEnumerable<T> models, string[] nodeNames) where T : SingleModelData
        {
            List<T> validModels = new List<T>();
            foreach (T t in models)
            {
                if(t.canSwitchTo(part, nodeNames))
                {
                    validModels.Add(t);
                }
            }
            return validModels.ToArray();
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
        public void enableTextureSet(String setName, Color[] userColors)
        {
            if (setName == "none" || setName == "default" || String.IsNullOrEmpty(setName))
            {
                return;
            }
            TextureSet mts = Array.Find(modelDefinition.textureSets, m => m.name == setName);
            if ( mts==null )
            {
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
                mts.enable(model, userColors);
            }
        }

        public string getDefaultTextureSet()
        {
            if (isValidTextureSet(modelDefinition.defaultTextureSet)) { return modelDefinition.defaultTextureSet; }
            if (modelDefinition.textureSets.Length > 0) { return modelDefinition.textureSets[0].name; }
            return "default";
        }

        /// <summary>
        /// Creates the transforms and meshes for the model using default positioning and orientation defined by the passed in ModelOrientation parameter
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="orientation"></param>
        public override void setupModel(Transform parent, ModelOrientation orientation)
        {
            setupModel(parent, orientation, false);
        }

        /// <summary>
        /// Creates the transforms and meshes for the model using default positioning and orientation defined by the passed in ModelOrientation parameter
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="orientation"></param>
        /// <param name="reUse"></param>
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

        /// <summary>
        /// Applies the scale and position values to the model.
        /// </summary>
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
                    model.transform.Rotate(modelDefinition.invertAxis, 180, Space.Self);
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
                model.transform.Rotate(modelDefinition.invertAxis, 180, Space.Self);
            }
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
    }

    public class PositionedModelData : SingleModelData
    {

        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;

        public PositionedModelData(ConfigNode node) : base(node)
        {
            position = node.GetVector3("position", Vector3.zero);
            rotation = node.GetVector3("rotation", Vector3.zero);
            scale = node.GetVector3("scale", Vector3.one);
        }

        public PositionedModelData(String name) : base(name)
        {
            position = Vector3.zero;
            rotation = Vector3.zero;
            scale = Vector3.one;
        }
        
        /// <summary>
        /// Update the model's transform scale, position, and rotation based on the values for the current model configuration
        /// </summary>
        public override void updateModel()
        {
            if (model != null)
            {
                model.transform.localScale = new Vector3(currentDiameterScale * baseScale * scale.x, currentHeightScale * baseScale * scale.y, currentDiameterScale * baseScale * scale.z);
                model.transform.localPosition = new Vector3(0, currentVerticalPosition, 0) + position;
                model.transform.localRotation = Quaternion.Euler(rotation);
            }
        }
    }

    /// <summary>
    /// Data that defines how a compound model scales and updates its height with scale changes.
    /// </summary>
    public class CompoundModelData
    {
        //array is sorted by order; 
        CompoundTransformData[] compoundTransformData;

        public void setHeight(float totalHeight, ModelOrientation orientation)
        {
            float staticHeight = 0;//sum height of non-scaleable meshes
            float additionalHeight = totalHeight - staticHeight;//the height that needs to be made up from scaling
            //loop through transform datas by 'order', setting positions and scales linearly up/down the stack.
        }
    }

    public class CompoundTransformData
    {
        public readonly string name;
        public readonly bool canScaleHeight = false;//can this transform scale its height
        public readonly float height;//the height of the meshes attached to this transform
        public readonly float offset;//the vertical offset of the meshes attached to this transform, when translated this amount the top/botom of the meshes will be at transform origin.
        public readonly int order;//the linear index of this transform in a vertical model setup stack

        public CompoundTransformData(ConfigNode node)
        {
            name = node.GetStringValue("name");
            canScaleHeight = node.GetBoolValue("canScale");
            height = node.GetFloatValue("height");
            offset = node.GetFloatValue("offset");
            order = node.GetIntValue("order");
        }
    }

}
