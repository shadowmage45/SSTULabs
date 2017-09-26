using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUModularRCS : PartModule
    {

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Scale"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true, minValue = 0.05f, maxValue = 5f, incrementSmall = 0.25f, incrementLarge = 1f, incrementSlide = 0.05f)]
        public float currentScale = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Standoff"),
            UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentStandoff = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Standoff Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentStandoffTexture = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Fuel Type"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentFuelType = string.Empty;

        [KSPField]
        public string modelName = string.Empty;

        [KSPField]
        public float structureScale = 1f;

        [KSPField]
        public float thrustScalePower = 2;

        [KSPField]
        public bool updateFuel = true;

        [KSPField(isPersistant = true)]
        public string standoffPersistentData;

        [Persistent]
        public string configNodeData;

        private ModelModule<SingleModelData, SSTUModularRCS> standoffModule;
        private ContainerFuelPreset[] fuelTypes;
        private ContainerFuelPreset fuelType;
        public float rcsThrust = -1;
        private float modifiedCost = -1;
        private float modifiedMass = -1;
        private Transform standoffTransform;
        private Transform modelTransform;
        private bool initialized = false;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasNode("STRUCTURE")) { configNodeData = node.ToString(); }
            init();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            init();
            Fields[nameof(currentStandoff)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                standoffModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.updateAttachNodes(true);
                });
            };

            Fields[nameof(currentScale)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.updateModelScale();
                    m.updateRCSThrust();
                    m.updateAttachNodes(true);
                });
            };

            Fields[nameof(currentFuelType)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.updateRCSFuelType();
                });
            };
        }

        public void Start()
        {
            updateRCSFuelType();
            updateRCSThrust();
        }

        private void init()
        {
            if (initialized) { return; }
            initialized = true;
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            standoffTransform = part.transform.FindRecursive("model").FindOrCreate("ModularRCSStandoff");
            standoffTransform.localRotation = Quaternion.Euler(0, 0, 90);//rotate 90' on z-axis, to face along x+/-; this should put the 'top' of the model at 0,0,0
            standoffModule = new ModelModule<SingleModelData, SSTUModularRCS>(part, this, standoffTransform, ModelOrientation.BOTTOM, nameof(standoffPersistentData), nameof(currentStandoff), nameof(currentStandoffTexture));
            standoffModule.getSymmetryModule = m => m.standoffModule;
            standoffModule.setupModelList(ModelData.parseModels<SingleModelData>(node.GetNodes("STRUCTURE"), m => new SingleModelData(m)));
            standoffModule.setupModel();

            modelTransform = part.transform.FindModel(modelName);

            updateModelScale();

            updateAttachNodes(false);

            ConfigNode[] fuelTypeNodes = node.GetNodes("FUELTYPE");
            int len = fuelTypeNodes.Length;
            fuelTypes = new ContainerFuelPreset[len];
            for (int i = 0; i < len; i++)
            {
                fuelTypes[i] = VolumeContainerLoader.getPreset(fuelTypeNodes[i].GetValue("name"));
            }
            fuelType = Array.Find(fuelTypes, m => m.name == currentFuelType);
        }

        private void updateModelScale()
        {
            standoffModule.model.updateScale(currentScale * structureScale);
            standoffModule.updateModel();
            modelTransform.localScale = new Vector3(currentScale, currentScale, currentScale);
        }

        private void updateRCSThrust()
        {
            ModuleRCS rcsModule = part.GetComponent<ModuleRCS>();
            if (rcsModule != null)
            {
                if (rcsThrust < 0) { rcsThrust = rcsModule.thrusterPower; }
                rcsModule.thrusterPower = Mathf.Pow(currentScale, thrustScalePower) * rcsThrust;
            }
        }

        private void updateRCSFuelType()
        {
            if (!updateFuel) { return; }
            ModuleRCS rcsModule = part.GetComponent<ModuleRCS>();
            if (rcsModule != null)
            {
                rcsModule.OnLoad(fuelType.getPropellantNode());
            }
        }

        private void updateAttachNodes(bool userInput)
        {
            AttachNode srf = part.srfAttachNode;
            if (srf != null)
            {
                float standoffHeight = standoffModule.model.currentHeight;
                Vector3 pos = new Vector3(-standoffHeight, 0, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, srf, pos, srf.orientation, userInput);
            }
            AttachNode btm = part.FindAttachNode("bottom");
            if (btm != null)
            {
                float standoffHeight = standoffModule.model.currentHeight;
                Vector3 pos = new Vector3(-standoffHeight, 0, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, btm, pos, btm.orientation, userInput);
            }
        }

    }
}
