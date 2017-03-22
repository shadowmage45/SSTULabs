using UnityEngine;
using System;

namespace SSTUTools
{
    public class SSTUModelSwitch2 : PartModule, IPartCostModifier, IPartMassModifier
    {

        /// <summary>
        /// Index of the container in VolumeContainer that this model will influence the volume of
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
        /// The currently selected variant name.  Also used for the UI control.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Variant"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentModel = string.Empty;

        [Persistent]
        public string configNodeData = string.Empty;

        private float modifiedVolume;
        private float modifiedCost;
        private float modifiedMass;
        private PositionedModelData[] modelData;
        private PositionedModelData activeModel;
        private bool initialized = false;

        private void modelSelected(BaseField field, object obj)
        {
            this.actionWithSymmetry(delegate (SSTUModelSwitch2 module)
            {
                module.setActiveModel(this.currentModel);
            });
        }

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
            string[] names = SingleModelData.getModelNames(modelData);
            this.updateUIChooseOptionControl("currentModel", names, names, true, currentModel);
            Fields["currentModel"].uiControlEditor.onFieldChanged = modelSelected;
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
                //TODO
                //really, the VC needs to poll other modules for a given interface, or method, or ??
                // and use the volume returned by that for each container index to determine
                // actual volume for each container
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

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            ConfigNode[] nodes = node.GetNodes("MODEL");
            modelData = ModelData.parseModels<PositionedModelData>(nodes, m => new PositionedModelData(m));
            setActiveModel(currentModel);
            updateMassAndCost();
        }

        private void updateMassAndCost()
        {
            if (activeModel == null) { return; }
            modifiedMass = activeModel.getModuleMass();
            modifiedCost = activeModel.getModuleCost();
        }

        private void setActiveModel(string newModelName)
        {
            if (activeModel != null) { activeModel.destroyCurrentModel(); }
            activeModel = Array.Find(modelData, m => m.name == newModelName);
            currentModel = activeModel.name;
            Transform root = part.transform.FindRecursive("model").FindOrCreate("SSTUModelSwitchRoot-" + moduleID);
            activeModel.setupModel(root, ModelOrientation.TOP);
            activeModel.updateScale(1.0f);
            activeModel.setPosition(0f, ModelOrientation.TOP);
            activeModel.updateModel();
        }
    }
}
