using UnityEngine;
using System;
using KSPShaderTools;

namespace SSTUTools
{
    public class SSTUModelSwitch : PartModule, IPartCostModifier, IPartMassModifier, IContainerVolumeContributor, IRecolorable
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

        [KSPField]
        public bool canAdjustScale = false;

        [KSPField]
        public float minScale = 1;

        [KSPField]
        public float maxScale = 1;

        [KSPField]
        public float incScaleLarge = 0.25f;

        [KSPField]
        public float incScaleSmall = 0.05f;

        [KSPField]
        public float incScaleSlide = 0.005f;

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
        public string managedNodeNames = "none";

        [KSPField]
        public string uiLabel = "Variant";

        /// <summary>
        /// The currently selected variant name.  Also used for the UI control.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Variant"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentModel = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Scale"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true, minValue = 0.25f, maxValue = 1f, incrementLarge = 0.25f, incrementSmall = 0.125f, incrementSlide = 0.025f)]
        public float currentScale = 1.0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Variant Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentTexture = string.Empty;

        [KSPField(isPersistant = true)]
        public string modelPersistentData;

        [KSPField(isPersistant = true)]
        public string animationPersistentData;

        [KSPField(isPersistant = true)]
        public float animationMaxDeploy = 1;

        [Persistent]
        public string configNodeData = string.Empty;

        private float modifiedVolume;
        private float modifiedCost;
        private float modifiedMass;
        private ModelModule<SSTUModelSwitch> models;
        private bool initialized = false;

        [KSPAction("Toggle")]
        public void toggleAnimationAction(KSPActionParam param)
        {
            models.animationModule.onToggleAction(param);
        }

        [KSPEvent(guiName = "Enable", guiActive = true, guiActiveEditor = true)]
        public void enableAnimationEvent()
        {
            models.animationModule.onDeployEvent();
        }

        [KSPEvent(guiName = "Disable", guiActive = true, guiActiveEditor = true)]
        public void disableAnimationEvent()
        {
            models.animationModule.onRetractEvent();
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

            Fields[nameof(currentModel)].guiName = uiLabel;
            Fields[nameof(currentModel)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                models.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.models.setScale(currentScale);
                    m.models.updateModelMeshes();
                    m.models.updateSelections();
                    m.updateMassAndCost();
                    m.updateAttachNodes(true);
                    SSTUModInterop.updateResourceVolume(m.part);
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };

            Fields[nameof(currentScale)].guiActiveEditor = canAdjustScale;
            UI_FloatEdit fe = (UI_FloatEdit)Fields[nameof(currentScale)].uiControlEditor;
            if (fe != null)
            {
                fe.minValue = minScale;
                fe.maxValue = maxScale;
                fe.incrementLarge = incScaleLarge;
                fe.incrementSmall = incScaleSmall;
                fe.incrementSlide = incScaleSlide;
            }
            Fields[nameof(currentScale)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m => 
                {
                    m.models.setScale(currentScale);
                    m.models.updateModelMeshes();
                    m.updateMassAndCost();
                    m.updateAttachNodes(true);
                    SSTUModInterop.updateResourceVolume(m.part);
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
                SSTUStockInterop.fireEditorUpdate();
            };
            Fields[nameof(currentTexture)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                models.textureSetSelected(a, b);
            };
            Fields[nameof(currentTexture)].guiActiveEditor = models.definition.textureSets.Length > 1;
            SSTUStockInterop.fireEditorUpdate();
            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        public override string GetInfo()
        {
            return "This part may have multiple model variants.  Check in the editor for more details.";
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

        //IContainerVolumeContributor override
        public ContainerContribution[] getContainerContributions()
        {
            return new ContainerContribution[] { new ContainerContribution(uiLabel, containerIndex, models.moduleVolume * 1000f) };
        }

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            ModelDefinitionLayoutOptions[] defs = SSTUModelData.getModelDefinitions(node.GetNodes("MODEL"));
            Transform root = part.transform.FindRecursive("model").FindOrCreate(rootTransformName);
            models = new ModelModule<SSTUModelSwitch>(part, this, root, ModelOrientation.TOP, nameof(currentModel), null, nameof(currentTexture), nameof(modelPersistentData), nameof(animationPersistentData), nameof(animationMaxDeploy), nameof(enableAnimationEvent), nameof(disableAnimationEvent));
            models.name = "ModelSwitch";
            models.getSymmetryModule = m => m.models;
            models.getValidOptions = () => defs;
            models.setupModelList(defs);
            models.setupModel();
            models.setScale(currentScale);
            models.updateModelMeshes();
            models.updateSelections();
            updateMassAndCost();
            updateAttachNodes(false);
        }

        private void updateMassAndCost()
        {
            if (models == null) { return; }
            modifiedMass = models.moduleMass;
            modifiedCost = models.moduleCost;
            modifiedVolume = models.moduleVolume;
        }

        private void updateAttachNodes(bool userInput)
        {
            string[] nodeNames = managedNodeNames.Split(',');
            models.updateAttachNodeBody(nodeNames, userInput);
        }

        public string[] getSectionNames()
        {
            return new string[] { uiLabel };
        }

        public RecoloringData[] getSectionColors(string name)
        {
            return models.recoloringData;
        }

        public TextureSet getSectionTexture(string name)
        {
            return models.textureSet;
        }

        public void setSectionColors(string name, RecoloringData[] colors)
        {
            models.setSectionColors(colors);
        }
    }
}
