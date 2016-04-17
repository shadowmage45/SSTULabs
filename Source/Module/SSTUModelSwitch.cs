using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUModelSwitch : PartModule
    {
        //CSV list of currently activated model data
        [KSPField(isPersistant =true)]
        public string persistentConfigData = string.Empty;

        private SSTUModelSwitchData[] modelData;

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            String saveData = string.Empty;
            int len = modelData.Length;
            for (int i = 0; i < len; i++)
            {

            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
        }
    }

    public class SSTUModelSwitchData
    {
        public readonly string name;
        public readonly Vector3 localPosition;
        public readonly Vector3 localRotation;
        public readonly string parentName = "model";
        public readonly string modelName;
        public GameObject model;
        public readonly Part part;
        public readonly ModelDefinition modelData;

        public SSTUModelSwitchData(ConfigNode node, Part part)
        {
            this.part = part;
            name = node.GetStringValue("name");
            localPosition = node.GetVector3("localPosition", Vector3.zero);
            localRotation = node.GetVector3("localRotation", Vector3.zero);
            parentName = node.GetStringValue("parent", parentName);
            modelName = node.GetStringValue("modelName", name);
            
            Transform tr = part.transform.FindRecursive(getBaseTransformName());
            if (tr != null)
            {
                model = tr.gameObject;
                MonoBehaviour.print("SSTUModelSwitch found existing model: " + model);
            }
            modelData = SSTUModelData.getModelDefinition(name);
        }
        
        public void enable()
        {
            Transform tr = part.transform.FindRecursive(getBaseTransformName());
            if (tr == null)
            {
                setupModel();
            }
            else
            {
                MonoBehaviour.print("ERROR: Enabled was called for an already enabled model: " + modelName + " for part: " + part);
            }
        }

        public void disable()
        {
            Transform tr = part.transform.FindRecursive(getBaseTransformName());
            if (tr != null)
            {
                GameObject.DestroyImmediate(tr.gameObject);
            }
            else
            {
                MonoBehaviour.print("ERROR: Disable was called on an already disabled model: " + modelName + " for part: " + part);
            }
        }

        private void setupModel()
        {
            Transform baseTransform = getBaseTransform();
            GameObject model = SSTUUtils.cloneModel(modelName);
            model.transform.NestToParent(baseTransform);
        }

        private string getBaseTransformName() { return name; }

        private Transform getParentTransform() { return part.transform.FindRecursive(parentName); }

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
            }
            baseTransform.localPosition = localPosition;
            baseTransform.localRotation = Quaternion.Euler(localRotation);
            return baseTransform;
        }

        public bool enabled()
        {
            Transform baseTransform = part.transform.FindRecursive(getBaseTransformName());
            return baseTransform != null;
        }
    }
}
