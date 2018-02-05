using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{

    public static class ModelLayout
    {

        private static Dictionary<string, ModelLayoutData> layouts = new Dictionary<string, ModelLayoutData>();

        private static bool loaded = false;

        public static void load()
        {
            layouts.Clear();
            ConfigNode[] layoutNodes = GameDatabase.Instance.GetConfigNodes("MODEL_LAYOUT");
            int len = layoutNodes.Length;
            for (int i = 0; i < len; i++)
            {
                ModelLayoutData mld = new ModelLayoutData(layoutNodes[i]);
                layouts.Add(mld.name, mld);
            }
            loaded = true;
        }

        public static ModelLayoutData findLayout(string name)
        {
            if (!loaded)
            {
                load();
            }
            ModelLayoutData mld;
            layouts.TryGetValue(name, out mld);
            return mld;
        }

        public static ModelLayoutData[] findLayouts(string[] names)
        {
            int len = names.Length;
            ModelLayoutData[] mlds = new ModelLayoutData[len];
            for (int i = 0; i < len; i++)
            {
                mlds[i] = findLayout(names[i]);
            }
            return mlds;
        }

        public static ModelLayoutData getDefaultLayout()
        {
            if (!layouts.ContainsKey("default"))
            {
                ModelLayoutData d = new ModelLayoutData("default", new ModelPositionData[] { new ModelPositionData(Vector3.zero, Vector3.one, Vector3.zero) });
                layouts.Add("default", d);
            }
            return layouts["default"];
        }

    }

    /// <summary>
    /// Data that defines how a single model is positioned inside a ModelModule when more than one internal model is desired.
    /// This position/scale/rotation data is applied to sub-models in addition to the base ModelModule position/scale data.
    /// </summary>
    public struct ModelPositionData
    {

        /// <summary>
        /// The local position of a single model, relative to the model-modules position
        /// </summary>
        public readonly Vector3 localPosition;

        /// <summary>
        /// The local scale of a single model, relative to the model-modules scale
        /// </summary>
        public readonly Vector3 localScale;

        /// <summary>
        /// The local rotation to be applied to a single model (euler x,y,z)
        /// </summary>
        public readonly Vector3 localRotation;

        public ModelPositionData(ConfigNode node)
        {
            localPosition = node.GetVector3("position", Vector3.zero);
            localScale = node.GetVector3("scale", Vector3.one);
            localRotation = node.GetVector3("rotation", Vector3.zero);
        }

        public ModelPositionData(Vector3 pos, Vector3 scale, Vector3 rotation)
        {
            localPosition = pos;
            localScale = scale;
            localRotation = rotation;
        }

        public Vector3 scaledPosition(Vector3 scale)
        {
            return Vector3.Scale(localPosition, scale);
        }

        public Vector3 scaledPosition(float scale)
        {
            return localPosition * scale;
        }

        public Vector3 scaledScale(Vector3 scale)
        {
            return Vector3.Scale(localScale, scale);
        }

        public Vector3 scaledScale(float scale)
        {
            return localScale * scale;
        }

    }

    /// <summary>
    /// Named model position layout data class.  Used to store positions of models, for use in ModelModule setup.<para/>
    /// Defined independently of the models that may use them, stored and referenced globally/game-wide.<para/>
    /// A single ModelLayoutData may be used by multiple ModelDefinitions, and a single ModelDefinition may have different ModelLayoutDatas applied to it by the controlling part-module.
    /// </summary>
    public class ModelLayoutData
    {

        public readonly string name;
        public readonly string title;
        public readonly ModelPositionData[] positions;
        public ModelLayoutData(ConfigNode node)
        {
            name = node.GetStringValue("name");
            title = node.GetStringValue("title", name);
            ConfigNode[] posNodes = node.GetNodes("POSITION");
            int len = posNodes.Length;
            positions = new ModelPositionData[len];
            for (int i = 0; i < len; i++)
            {
                positions[i] = new ModelPositionData(posNodes[i]);
            }
        }

        public ModelLayoutData(string name, ModelPositionData[] positions)
        {
            this.name = name;
            this.positions = positions;
        }

    }

    /// <summary>
    /// Container class for ModelModule that manages the layout options for each definition within that module.
    /// </summary>
    public class ModuleLayoutOptions
    {
        private Dictionary<string, ModelLayoutOptions> modelLayouts = new Dictionary<string, ModelLayoutOptions>();

        public ModuleLayoutOptions()
        {

        }

        public void setDefinitionLayouts(string defName, string[] layoutNames)
        {
            if (modelLayouts.ContainsKey(defName))
            {
                MonoBehaviour.print("ERROR: Layout data already exists for definition: " + defName + ".  Clearing existing data in favor of new incoming data.");
                modelLayouts.Remove(defName);
            }
            int len = layoutNames.Length;
            for (int i = 0; i < len; i++)
            {
                ModelLayoutOptions mlo = new ModelLayoutOptions(layoutNames);
                modelLayouts.Add(defName, mlo);
            }
        }

        /// <summary>
        /// Return string array contianing the unique registry names of the layouts for the input model definition.
        /// </summary>
        /// <param name="defName"></param>
        /// <returns></returns>
        public string[] getLayoutNames(string defName)
        {
            return modelLayouts[defName].getLayoutNames();
        }

        /// <summary>
        /// Return string array containing the display titles of the layouts for the input model definition.<para/>
        /// The returned array will be of the same length and ordering as the array returned by 'getLayoutNames()'
        /// </summary>
        /// <param name="defName"></param>
        /// <returns></returns>
        public string[] getLayoutTitles(string defName)
        {
            return modelLayouts[defName].getLayoutTitles();
        }

        /// <summary>
        /// Get both layout names and layout titles for the input model definition name.
        /// </summary>
        /// <param name="defName"></param>
        /// <param name="names"></param>
        /// <param name="titles"></param>
        public void getLayoutInfo(string defName, out string[] names, out string[] titles)
        {
            ModelLayoutOptions mlo = modelLayouts[defName];
            names = mlo.getLayoutNames();
            titles = mlo.getLayoutTitles();
        }

        /// <summary>
        /// Returns the input layout if valid, else returns the first (default) layout for the input model definition.
        /// </summary>
        /// <param name="defName"></param>
        /// <param name="current"></param>
        /// <returns></returns>
        public ModelLayoutData getInitialLayout(string defName, ModelLayoutData current)
        {
            ModelLayoutOptions mlo = modelLayouts[defName];
            ModelLayoutData mld = current;
            if (!mlo.isValidLayout(current.name))
            {
                return ModelLayout.findLayout(mlo.getDefaultLayout());
            }
            return mld;
        }

    }

    public class ModelLayoutOptions
    {

        public readonly ModelDefinition definition;
        public readonly ModelLayoutData[] layouts;

        public ModelLayoutOptions(string modelDef, string[] layoutNames)
        {
            this.definition = SSTUModelData.getModelDefinition(modelDef);
            this.layouts = ModelLayout.
            this.layoutNames = layouts;
            if (this.layoutNames == null || this.layoutNames.Length < 1)
            {
                throw new InvalidOperationException("ERROR: No valid layout data specified.");
            }
        }

        public string getDefaultLayout()
        {
            return layoutNames[0];
        }

        public bool isValidLayout(string name)
        {
            return layoutNames.Exists(m => m == name);
        }

        public string[] getLayoutNames()
        {
            return layoutNames;
        }

        public string[] getLayoutTitles()
        {
            if (layoutTitles == null)
            {
                int len = layoutNames.Length;
                layoutTitles = new string[len];
                for (int i = 0; i < len; i++)
                {
                    layoutTitles[i] = ModelLayout.findLayout(layoutNames[i]).title;
                }
            }
            return layoutTitles;
        }

    }

}
