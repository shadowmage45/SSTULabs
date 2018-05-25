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
                groupedNames = nodes[i].GetStringValues("model");
                groupedLayouts = nodes[i].GetStringValues("layout", new string[] { "default" });
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
                    if (def == null)
                    {
                        error("Model definition was null for name: " + groupedNames[k]+". Skipping definition during loading of part");
                    }
                    else
                    {
                        options.Add(new ModelDefinitionLayoutOptions(def, layoutDataList.ToArray()));
                    }
                    layoutDataList.Clear();
                }
            }
            return options.ToArray();
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

}
