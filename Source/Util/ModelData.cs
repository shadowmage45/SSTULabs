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

        public static void reloadData()
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
            fairingTopOffset = node.GetFloatValue("fairingTopOffset");
            rcsVerticalPosition = node.GetFloatValue("rcsVerticalPosition", rcsVerticalPosition);
            rcsHorizontalPosition = node.GetFloatValue("rcsHorizontalPosition", rcsHorizontalPosition);
            rcsVerticalRotation = node.GetFloatValue("rcsVerticalRotation", rcsVerticalRotation);
            rcsHorizontalRotation = node.GetFloatValue("rcsHorizontalRotation", rcsHorizontalRotation);

            defaultTextureSet = node.GetStringValue("defaultTextureSet");

            String[] attachNodeStrings = node.GetValues("node");
            int len = attachNodeStrings.Length;
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

        public float getScaledHeight(float vertScale)
        {
            return vertScale * height;
        }

        public float getScaledDiameter(float horizScale)
        {
            return horizScale * diameter;
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
            foreach (String mesh in meshNames)
            {
                Transform tr = root.transform.FindRecursive(mesh);
                if (tr == null) { continue; }
                if (tr != null && (r=tr.GetComponent<Renderer>()) != null)
                {
                    Material m = r.material;
                    if (!String.IsNullOrEmpty(diffuseTextureName)) { m.mainTexture = GameDatabase.Instance.GetTexture(diffuseTextureName, false); }
                    if (!String.IsNullOrEmpty(normalTextureName)) { m.SetTexture("_BumpMap", GameDatabase.Instance.GetTexture(normalTextureName, true)); }
                    if (!String.IsNullOrEmpty(emissiveTextureName)) { m.SetTexture("_Emissive", GameDatabase.Instance.GetTexture(emissiveTextureName, false)); }
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

        public float volume;
        public float minVerticalScale;
        public float maxVerticalScale;
        public float currentDiameterScale;
        public float currentHeightScale;
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
            volume = node.GetFloatValue("volume", modelDefinition.volume);
            minVerticalScale = node.GetFloatValue("minVerticalScale", 1f);
            maxVerticalScale = node.GetFloatValue("maxVerticalScale", 1f);
        }

        public String getNextTextureSetName(String currentSetName, bool iterateBackwards)
        {
            ModelTextureSet set = SSTUUtils.findNext(modelDefinition.textureSets, m => m.name == currentSetName, iterateBackwards);
            String newSetName = set==null ? "none" : set.name;
            return newSetName;
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

        public virtual void setupModel(Part part, Transform parent, ModelOrientation orientation)
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
            return currentDiameterScale * currentDiameterScale * currentHeightScale * volume;
        }

        public virtual float getModuleMass()
        {
            return modelDefinition.mass * currentDiameterScale * currentDiameterScale * currentHeightScale;
        }

        public virtual float getModuleCost()
        {
            return modelDefinition.cost * currentDiameterScale * currentDiameterScale * currentHeightScale;
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
                    MonoBehaviour.print("Default texture set was null or empty string, this is a configuration error. " +
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

        public override void setupModel(Part part, Transform parent, ModelOrientation orientation)
        {
            setupModel(part, parent, orientation, false);
        }

        public void setupModel(Part part, Transform parent, ModelOrientation orientation, bool reUse)
        {
            String modelName = modelDefinition.modelName;
            if (String.IsNullOrEmpty(modelName))//no model to setup
            {
                return;
            }
            if (reUse)
            {
                MonoBehaviour.print("attempting to re-use existing model: " + modelDefinition.modelName);
                Transform tr = part.transform.FindModel(modelDefinition.modelName);
                if (tr != null)
                {
                    MonoBehaviour.print("Found existing model for re-use: "+tr.name);
                    tr.name = modelDefinition.modelName;
                    tr.gameObject.name = modelDefinition.modelName;
                }
                else
                {
                    MonoBehaviour.print("Did not find existing model, will clone and add new model");
                }
                model = tr == null ? null : tr.gameObject;
            }
            if (!String.IsNullOrEmpty(modelName) && model==null)
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

        public override void updateModel()
        {
            if (model != null)
            {
                model.transform.localScale = new Vector3(currentDiameterScale, currentHeightScale, currentDiameterScale);
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

        public static SingleModelData[] parseModels(ConfigNode[] modelNodes)
        {
            int len = modelNodes.Length;
            SingleModelData[] datas = new SingleModelData[len];
            for (int i = 0; i < len; i++)
            {
                datas[i] = new SingleModelData(modelNodes[i]);
            }
            return datas;
        }
    }

    /// <summary>
    /// Live data class for an engine/tank mount model, includes reference to the engine-mount definition.
    /// Should be used by anything that needs support for multiple attach nodes and/or RCS positioning.
    /// Includes utility methods to update attach node positions based on an input length/vertical offset.
    /// </summary>
    public class MountModelData : SingleModelData
    {    
        
        public MountModelData(ConfigNode node) : base(node)
        {            

        }
        
        public void updateAttachNodes(Part part, String[] nodeNames, bool userInput, ModelOrientation orientation)
        {
            MonoBehaviour.print("Model updating attach nodes; names length: " + nodeNames.Length);
            for (int i = 0; i < nodeNames.Length;i++) { MonoBehaviour.print("node name: " + nodeNames[i]); }
            if (nodeNames.Length == 1 && nodeNames[0] == "NONE") { MonoBehaviour.print("Early exit from node update due to name==NONE"); return; }            
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
                    if (invert) { orient = -orient; }
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
        /// if any node that has a part attached would be deleted, return false
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
    }
}
