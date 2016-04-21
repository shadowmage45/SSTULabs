using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUModelSwitch : PartModule
    {        
        /// <summary>
        /// Amount of persistent volume that is untouched by the ModelSwitch module; regardless of current part setup the volume will always be at least this amount (unless a model has negative volume?)
        /// </summary>
        [KSPField]
        public float baseVolume = 100f;

        /// <summary>
        /// The index of the base container within the SSTUVolumeContainer that holds the 'baseVolume'
        /// </summary>
        [KSPField]
        public int baseContainerIndex = 0;

        //CSV list of currently activated model data
        [KSPField(isPersistant = true)]
        public string persistentConfigData = string.Empty;

        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        private SSTUModelSwitchData[] modelData;

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "EnableStuff")]
        public void enableEvent()
        {
            int len = modelData.Length;
            for (int i = 0; i < len; i++)
            {
                modelData[i].enable();
            }
            updateContainerVolume();
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "DisableStuff")]
        public void disableEvent()
        {
            int len = modelData.Length;
            for (int i = 0; i < len; i++)
            {
                modelData[i].disable();
            }
            updateContainerVolume();
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
            updatePersistentData();
        }

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor && !initializedResources)
            {
                initializedResources = true;
                updateContainerVolume();
            }
        }
        
        private void updatePersistentData()
        {
            if (modelData == null) { return; }//--KSP calling 'save' before OnLoad and OnStart
            String saveData = string.Empty;
            int len = modelData.Length;
            for (int i = 0; i < len; i++)
            {
                if (i > 0) { saveData = saveData + ","; }
                saveData = saveData + modelData[i].getPersistentData();
            }
            persistentConfigData = saveData;
        }

        private void loadPersistentData()
        {
            if (String.IsNullOrEmpty(persistentConfigData)) { return; }
            string data = persistentConfigData;
            string[] split = data.Split(',');
            int len = split.Length;
            for (int i = 0; i < len; i++)
            {
                modelData[i].loadPersistentData(int.Parse(split[i]));
            }
        }

        private void initialize()
        {
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(this);
            ConfigNode[] modelNodes = node.GetNodes("MODEL");
            int len = modelNodes.Length;
            modelData = new SSTUModelSwitchData[len];
            for (int i = 0; i < len; i++)
            {
                modelData[i] = new SSTUModelSwitchData(modelNodes[i], this.part);
            }
            loadPersistentData();
            len = modelData.Length;
            for (int i = 0; i < len; i++)
            {
                modelData[i].initialize();
            }
        }

        private void updateContainerVolume()
        {
            SSTUVolumeContainer container = part.GetComponent<SSTUVolumeContainer>();
            if (container == null) { return; }//TODO spew error msg
            MonoBehaviour.print("updating container percents");
            int len = container.numberOfContainers;
            float[] percents = new float[len];
            float total = calcTotalVolume();
            MonoBehaviour.print("total calced volume: " + total);
            float val;
            for (int i = 0; i < len; i++)
            {
                val = calcVolume(i);
                percents[i] = val / total;
                MonoBehaviour.print("volume for container index: " + i + " :: " + val+"  percent: "+percents[i]);
            }
            container.setContainerPercents(percents, total);
        }

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
    }

    public class SSTUModelSwitchData
    {
        public readonly string name;
        public readonly string parentName = "model";
        public readonly string groupName = "main";
        public readonly int containerIndex;
        public readonly Vector3 localPosition;
        public readonly Vector3 localRotation;

        public readonly Part part;
        public readonly ModelDefinition modelDefinition;

        private bool currentlyEnabled;
        
        public SSTUModelSwitchData(ConfigNode node, Part owner)
        {
            name = node.GetStringValue("name");
            parentName = node.GetStringValue("parent", parentName);
            groupName = node.GetStringValue("group", groupName);
            containerIndex = node.GetIntValue("containerIndex", 0);
            localPosition = node.GetVector3("localPosition", Vector3.zero);
            localRotation = node.GetVector3("localRotation", Vector3.zero);
            currentlyEnabled = node.GetBoolValue("enabled", currentlyEnabled);//load the default config specified enabled value; in case a model should start enabled
            part = owner;
            modelDefinition = SSTUModelData.getModelDefinition(name);
            if (modelDefinition == null) { throw new NullReferenceException("Could not locate model data for name: "+name);}
        }

        internal void loadPersistentData(int val) { currentlyEnabled = val==1; }

        internal int getPersistentData() { return currentlyEnabled ? 1 : 0; }

        internal void initialize()
        {
            if (currentlyEnabled) { enable(); }
            else { disable(); }//just in case it was enabled somehow in prefab or through MODEL nodes??
        }

        public float volume { get { return modelDefinition.volume * 1000f; } }//adjust from model m^3 to liters (as resources are defined in liters)

        public float mass { get { return modelDefinition.mass; } }

        public float cost { get { return modelDefinition.cost; } }

        public bool enabled { get { return currentlyEnabled; } }        
        
        public void enable()
        {
            Transform tr = part.transform.FindRecursive(getBaseTransformName());
            if (tr == null)
            {
                setupModel();
            }
            else
            {
                MonoBehaviour.print("ERROR: Enabled was called for an already enabled model: " + name + " for part: " + part);
            }
            currentlyEnabled = true;
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
                MonoBehaviour.print("ERROR: Disable was called on an already disabled model: " + name + " for part: " + part);
            }
            currentlyEnabled = false;
        }

        private void setupModel()
        {
            Transform baseTransform = getBaseTransform();
            GameObject model = SSTUUtils.cloneModel(modelDefinition.modelName);
            model.transform.NestToParent(baseTransform);
        }

        private string getBaseTransformName() { return groupName+"-"+name; }

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
                baseTransform = newObj.transform;
            }
            baseTransform.localPosition = localPosition;
            baseTransform.localRotation = Quaternion.Euler(localRotation);
            return baseTransform;
        }
    }
}
