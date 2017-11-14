using UnityEngine;
using System;

namespace SSTUTools
{
    public class SSTUModelSwitch2 : PartModule, IPartCostModifier, IPartMassModifier, IContainerVolumeContributor
    {
        /// <summary>
        /// If populated, the model-switch added mesh will be parented, in model-local space, to the transform specified in this field.
        /// </summary>
        [KSPField]
        public string parentTransformName = string.Empty;

        /// <summary>
        /// Added either to root model transform, or to the 'parent' transform specified above.
        /// This transform serves as a container for the ModelSwitch added models, 
        /// and allows for clean management of meshes added through the ModelSwitch module.
        /// </summary>
        [KSPField]
        public string rootTransformName = "ModelSwitchRoot";

        /// <summary>
        /// Index of the container in VolumeContainer that this model will control the volume of
        /// </summary>
        [KSPField]
        public int containerIndex = 0;

        /// <summary>
        /// Module-specific suffix added to root transform names to allow for retrieving of transform from prefab when multiple ModelSwitch modules are present on a part.
        /// </summary>
        [KSPField]
        public int moduleID = 0;

        /// <summary>
        /// Should this module zero out the config cost of the part, relying on the variant definition for cost, or should it add variant definition cost to the config cost?
        /// </summary>
        [KSPField]
        public bool subtractCost = false;

        /// <summary>
        /// Should this module zero out the config mass of the part, relying on the variant definition for mass, or should it add variant definition mass to the config mass?
        /// </summary>
        [KSPField]
        public bool subtractMass = false;

        /// <summary>
        /// CSV of what attach-nodes this model-switch is responsible for.  These nodes will be assigned to the node-position values specified in the MODEL_DATA, relative to the
        /// position of the model in the hierarchy.
        /// </summary>
        [KSPField]
        public string managedNodeNames = string.Empty;

        /// <summary>
        /// The currently selected variant name.  Also used for the UI control.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Variant"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentModel = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Variant Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentTexture = string.Empty;

        [KSPField(isPersistant = true)]
        public string modelPersistentData;

        [Persistent]
        public string configNodeData = string.Empty;

        private float modifiedVolume;
        private float modifiedCost;
        private float modifiedMass;
        private ModelModule<PositionedModelData, SSTUModelSwitch2> models;
        private bool initialized = false;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();            
        }

        public override string GetInfo()
        {
            return "This part may have multiple model variants.  Check in the editor for more details.";
        }

        public void Start()
        {
            //TODO update resource volume for current setup
            SSTUVolumeContainer vc = part.GetComponent<SSTUVolumeContainer>();
            if (vc != null)
            {

            }
        }

        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            return subtractCost ? -defaultCost + modifiedCost : modifiedCost;
        }

        public ModifierChangeWhen GetModuleCostChangeWhen()
        {
            return ModifierChangeWhen.CONSTANTLY;
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            return subtractMass ? -defaultMass + modifiedMass : modifiedMass;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen()
        {
            return ModifierChangeWhen.CONSTANTLY;
        }
        
        public int[] getContainerIndices()
        {
            return new int[] { containerIndex };
        }

        public float[] getContainerVolumes()
        {
            return new float[] { models.moduleVolume };
        }

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            models = new ModelModule<PositionedModelData, SSTUModelSwitch2>(part, this, part.transform.FindRecursive("model"), ModelOrientation.TOP, nameof(modelPersistentData), nameof(currentModel), nameof(currentTexture));
            models.getSymmetryModule = m => m.models;
            models.setupModelList(ModelData.parseModels<PositionedModelData>(node.GetNodes("MODEL"), m => new PositionedModelData(m)));
            models.setupModel();
            updateMassAndCost();
        }

        private void updateMassAndCost()
        {
            if (models == null) { return; }
            modifiedMass = models.model.getModuleMass();
            modifiedCost = models.model.getModuleCost();
        }
    }
}
