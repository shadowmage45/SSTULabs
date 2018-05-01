using System;
using System.Collections.Generic;
using UnityEngine;
using KSPShaderTools;
using static SSTUTools.SSTULog;

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
                log("Loading model definition data for name: " + data.name + " with model URL: " + data.modelName);
                if (baseModelData.ContainsKey(data.name))
                {
                    error("Model defs already contains def for name: " + data.name + ".  Please check your configs as this is an error.  The duplicate entry was found in the config node of:\n"+node);
                    continue;
                }
                baseModelData.Add(data.name, data);
            }
        }

        public static void loadConfigData()
        {
            defsLoaded = false;
            baseModelData.Clear();
            loadDefs();
        }

        /// <summary>
        /// Find a single model definition by name.  Returns null if not found.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static ModelDefinition getModelDefinition(String name)
        {
            if (!defsLoaded) { loadDefs(); }
            ModelDefinition data = null;
            baseModelData.TryGetValue(name, out data);
            return data;
        }

        /// <summary>
        /// Return a group of model definition layout options by model definition name.
        /// </summary>
        /// <param name="names"></param>
        /// <returns></returns>
        public static ModelDefinition[] getModelDefinitions(string[] names)
        {
            List<ModelDefinition> defs = new List<ModelDefinition>();
            int len = names.Length;
            for (int i = 0; i < len; i++)
            {
                ModelDefinition def = getModelDefinition(names[i]);
                if (def != null)
                {
                    defs.AddUnique(def);
                }
                else
                {
                    error("Could not locate model defintion for name: " + names[i]);
                }
            }
            return defs.ToArray();
        }

        /// <summary>
        /// Create a group of model definition layout options by model definition name, with default (single position) layouts.
        /// </summary>
        /// <param name="names"></param>
        /// <returns></returns>
        public static ModelDefinitionLayoutOptions[] getModelDefinitionLayouts(string[] names)
        {
            List<ModelDefinitionLayoutOptions> defs = new List<ModelDefinitionLayoutOptions>();
            int len = names.Length;
            for (int i = 0; i < len; i++)
            {
                ModelDefinition def = getModelDefinition(names[i]);
                if (def != null)
                {
                    defs.Add(new ModelDefinitionLayoutOptions(def));
                }
                else
                {
                    error("Could not locate model defintion for name: " + names[i]);
                }
            }
            return defs.ToArray();
        }

        /// <summary>
        /// Create a group of model definition layout sets.  Loads the model definitions + their supported layout configurations.
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        public static ModelDefinitionLayoutOptions[] getModelDefinitions(ConfigNode[] nodes)
        {
            int len = nodes.Length;

            List<ModelDefinitionLayoutOptions> options = new List<ModelDefinitionLayoutOptions>();
            List<ModelLayoutData> layoutDataList = new List<ModelLayoutData>();
            ModelDefinition def;

            string[] groupedNames;
            string[] groupedLayouts;
            int len2;

            for (int i = 0; i < len; i++)
            {
                //because configNode.ToString() reverses the order of values, and model def layouts are always loaded from string-cached config nodes
                //we need to reverse the order of the model and layout names during parsing
                groupedNames = nodes[i].GetStringValues("model", true);
                groupedLayouts = nodes[i].GetStringValues("layout", new string[] { "default" }, true);
                len2 = groupedNames.Length;
                for (int k = 0; k < len2; k++)
                {
                    def = SSTUModelData.getModelDefinition(groupedNames[k]);
                    layoutDataList.AddRange(ModelLayout.findLayouts(groupedLayouts));
                    if (nodes[i].HasValue("position") || nodes[i].HasValue("rotation") || nodes[i].HasValue("scale"))
                    {
                        Vector3 pos = nodes[i].GetVector3("position", Vector3.zero);
                        Vector3 scale = nodes[i].GetVector3("scale", Vector3.one);
                        Vector3 rot = nodes[i].GetVector3("rotation", Vector3.zero);
                        ModelPositionData mpd = new ModelPositionData(pos, scale, rot);
                        ModelLayoutData custom = new ModelLayoutData("default", new ModelPositionData[] { mpd });
                        if (layoutDataList.Exists(m => m.name == "default"))
                        {
                            ModelLayoutData del = layoutDataList.Find(m => m.name == "default");
                            layoutDataList.Remove(del);
                        }
                        layoutDataList.Add(custom);
                    }
                    options.Add(new ModelDefinitionLayoutOptions(def, layoutDataList.ToArray()));
                    layoutDataList.Clear();
                }
            }
            return options.ToArray();
        }

    }

    /// <summary>
    /// Solar panel model data.  Includes capability to use multiple positions, but models may only come from a single ModelDefinition
    /// </summary>
    public class SolarModelData
    {

        public ConfigNode getSolarData()
        {
            ConfigNode mergedNode = new ConfigNode("SOLARDATA");
            ConfigNode[] prevPanelNodes = null;// modelDefinition.solarData.GetNodes("PANEL");
            int len2 = prevPanelNodes.Length;
            for (int i = 0; i < len2; i++)
            {
                mergedNode.AddNode(prevPanelNodes[i]);
            }
            ConfigNode[] panelNodes;
            int len = 0;// positions.Length;
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
            int idx = input.GetIntValue("mainPivotIndex", 0);
            int str = input.GetIntValue("mainPivotStride", 1);
            idx += str;
            input.RemoveValue("mainPivotIndex");
            input.SetValue("mainPivotIndex", idx, true);

            idx = input.GetIntValue("secondPivotIndex", 0);
            str = input.GetIntValue("secondPivotStride", 1);
            idx += str;
            input.RemoveValue("secondPivotIndex");
            input.SetValue("secondPivotIndex", idx, true);

            ConfigNode[] suncatcherNodes = input.GetNodes("SUNCATCHER");
            int len = suncatcherNodes.Length;
            for (int i = 0; i < len; i++)
            {
                idx = suncatcherNodes[i].GetIntValue("suncatcherIndex", 0);
                str = suncatcherNodes[i].GetIntValue("suncatcherStride", 1);
                idx += str;
                suncatcherNodes[i].RemoveValue("suncatcherIndex");
                suncatcherNodes[i].SetValue("suncatcherIndex", idx, true);
            }
        }
    }

    /// <summary>
    /// Wrapper class for a single solar-panel position, as used in the SolarModelData class.
    /// Denotes position, rotation, and scale for a solar-panel model, with position relative to the part origin.
    /// </summary>
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
    
    /// <summary>
    /// Data for an srb nozzle, including gimbal adjustment data and ISP curve adjustment.
    /// </summary>
    public class SRBNozzleData
    {
        //private Quaternion[] gimbalDefaultOrientations;
        //private Transform[] thrustTransforms;
        //private Transform[] gimbalTransforms;

        //public void setupTransforms(string moduleThrustTransformName, string moduleGimbalTransformName, string rcsTransformName)
        //{
        //    Transform[] origThrustTransforms = model.transform.FindChildren(this.thrustTransformName);
        //    int len = origThrustTransforms.Length;
        //    for (int i = 0; i < len; i++)
        //    {
        //        origThrustTransforms[i].name = origThrustTransforms[i].gameObject.name = moduleThrustTransformName;
        //    }
        //    Transform[] origGimbalTransforms = model.transform.FindChildren(this.gimbalTransformName);
        //    len = origGimbalTransforms.Length;
        //    gimbalDefaultOrientations = new Quaternion[len];
        //    for (int i = 0; i < len; i++)
        //    {
        //        origGimbalTransforms[i].name = origGimbalTransforms[i].gameObject.name = moduleGimbalTransformName;
        //        gimbalDefaultOrientations[i] = origGimbalTransforms[i].localRotation;
        //    }
        //    Transform[] origRcsTransforms = model.transform.FindChildren(this.rcsThrustTransformName);
        //    len = origRcsTransforms.Length;
        //    for (int i = 0; i < len; i++)
        //    {
        //        origRcsTransforms[i].name = origRcsTransforms[i].gameObject.name = rcsTransformName;
        //    }
        //    this.gimbalTransforms = origGimbalTransforms;
        //    this.thrustTransforms = origThrustTransforms;
        //}

        ///// <summary>
        ///// Resets the gimbal to its default orientation, and then applies newRotation to it as a direct rotation around the input world axis
        ///// </summary>
        ///// <param name="partGimbalTransform"></param>
        ///// <param name="newRotation"></param>
        //public void updateGimbalRotation(Vector3 worldAxis, float newRotation)
        //{
        //    int len = gimbalTransforms.Length;
        //    for (int i = 0; i < len; i++)
        //    {
        //        gimbalTransforms[i].localRotation = gimbalDefaultOrientations[i];
        //        gimbalTransforms[i].Rotate(worldAxis, -newRotation, Space.World);
        //    }
        //}
    }

    public class ISDCModelData
    {
        //public readonly string name;
        //public readonly int numberOfEngines = 4;
        //public readonly float[] engineRotations;
        //public readonly string engineThrustTransformName = string.Empty;
        //public readonly string engineTransformName = "SSTU/Assets/SC-ENG-ULLAGE-A";

        //public string moduleThrustTransformName;
        //public float engineScale = 1f;//this is relative to the scale set by the diamter/etc
        //public bool invertEngines = false;

        //public ISDCModelData(ConfigNode node)
        //{
        //    name = node.GetStringValue("name");
        //    numberOfEngines = modelDefinition.configNode.GetIntValue("numberOfEngines");
        //    engineRotations = modelDefinition.configNode.GetFloatValuesCSV("engineRotations");
        //    engineThrustTransformName = modelDefinition.configNode.GetStringValue("engineThrustTransformName");
        //    engineTransformName = modelDefinition.configNode.GetStringValue("engineTransformName");
        //}

        //public override void updateModel()
        //{
        //    //update the 'base scale' for the fairing diameter in updateModel()
        //    base.updateModel();

        //    //loop through the existing thrust transforms, and set them to the name expected by the ModuleEngines
        //    Transform[] thrustTransforms = this.model.transform.FindChildren(engineThrustTransformName);
        //    foreach (Transform tr in thrustTransforms)
        //    {
        //        tr.gameObject.name = moduleThrustTransformName;
        //    }

        //    //now go through the actual engine models (each defined as sub-models), and set their rotations, positions, and local scales appropriately.
        //    Transform[] modelTransforms = this.model.transform.FindChildren(engineTransformName);
        //    int len = modelTransforms.Length;
        //    float rotOffset;
        //    if (len != numberOfEngines || len != engineRotations.Length)
        //    {
        //        MonoBehaviour.print("ERROR: ISDC Model def -- mismatch between number of engines specified, engine rotations, and actual models found.");
        //    }
        //    Vector3 pos;
        //    float yPos = engineScale * modelDefinition.height * 0.5f;
        //    for (int i = 0; i < len; i++)
        //    {
        //        rotOffset = engineRotations[i] + (invertEngines ? 180f : 0f);
        //        modelTransforms[i].localScale = Vector3.one * engineScale;
        //        modelTransforms[i].rotation = Quaternion.Euler(0, rotOffset, invertEngines ? 180 : 0);
        //        pos = modelTransforms[i].localPosition;
        //        pos.y = yPos;
        //        modelTransforms[i].localPosition = pos;
        //    }
        //}

    }

}
