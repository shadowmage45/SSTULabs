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
}
