using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUModularRCS : PartModule, IPartCostModifier, IPartMassModifier
    {

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Scale"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true, minValue = 0.05f, maxValue = 5f, incrementSmall = 0.25f, incrementLarge = 1f, incrementSlide = 0.05f)]
        public float currentScale = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Structure"),
            UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentStructure = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Structure Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentStructureTexture = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Fuel Type"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentFuelType = string.Empty;

        [KSPField]
        public string modelName = string.Empty;

        [KSPField]
        public float structureScale = 1f;

        [KSPField]
        public float thrustScalePower = 2f;

        [KSPField]
        public float structureOffset = 0f;

        [KSPField]
        public bool updateFuel = true;

        [KSPField]
        public bool scaleGUIActive = true;

        [KSPField(isPersistant = true)]
        public string structurePersistentData;

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
            Fields[nameof(currentStructure)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                standoffModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.updateModelScale();
                    m.updateAttachNodes(true);
                    m.updateMassAndCost();
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
            };

            Fields[nameof(currentScale)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentScale = currentScale; }
                    m.updateModelScale();
                    m.updateRCSThrust();
                    m.updateAttachNodes(true);
                    m.updateMassAndCost();
                    SSTUModInterop.onPartGeometryUpdate(m.part, true);
                });
            };

            Fields[nameof(currentFuelType)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentFuelType = currentFuelType; }
                    m.fuelType = Array.Find(m.fuelTypes, s => s.name == m.currentFuelType);
                    m.updateRCSFuelType();
                });
            };

            Fields[nameof(currentFuelType)].guiActiveEditor = updateFuel && fuelTypes.Length > 1;
            string[] names = SSTUUtils.getNames(fuelTypes, m => m.name);
            this.updateUIChooseOptionControl(nameof(currentFuelType), names, names, true, currentFuelType);

            Fields[nameof(currentScale)].guiActiveEditor = scaleGUIActive;
        }

        public void Start()
        {
            updateRCSFuelType();
            updateRCSThrust();
        }

        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            float scaleMod = (defaultCost * currentScale * currentScale) - defaultCost;
            return modifiedCost + scaleMod;
        }

        public ModifierChangeWhen GetModuleCostChangeWhen()
        {
            return ModifierChangeWhen.CONSTANTLY;
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            float scaleMod = (defaultMass * currentScale * currentScale) - defaultMass;
            return modifiedMass + scaleMod;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen()
        {
            return ModifierChangeWhen.CONSTANTLY;
        }

        private void init()
        {
            if (initialized) { return; }
            initialized = true;
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            standoffTransform = part.transform.FindRecursive("model").FindOrCreate("ModularRCSStandoff");
            standoffTransform.localRotation = Quaternion.Euler(0, 0, 90);//rotate 90' on z-axis, to face along x+/-; this should put the 'top' of the model at 0,0,0
            standoffModule = new ModelModule<SingleModelData, SSTUModularRCS>(part, this, standoffTransform, ModelOrientation.TOP, nameof(currentStructure), nameof(structurePersistentData), nameof(currentStructureTexture), null, null, null, null);
            standoffModule.getSymmetryModule = m => m.standoffModule;
            standoffModule.setupModelList(ModelData.parseModels<SingleModelData>(node.GetNodes("STRUCTURE"), m => new SingleModelData(m)));
            standoffModule.setupModel();

            if (string.IsNullOrEmpty(modelName))
            {
                MonoBehaviour.print("ERROR: SSTUModularRCS - null/empty modelName in module config.");
            }
            modelTransform = part.transform.FindModel(modelName);

            updateModelScale();
            updateMassAndCost();

            updateAttachNodes(false);

            ConfigNode[] fuelTypeNodes = node.GetNodes("FUELTYPE");
            int len = fuelTypeNodes.Length;
            fuelTypes = new ContainerFuelPreset[len];
            for (int i = 0; i < len; i++)
            {
                fuelTypes[i] = VolumeContainerLoader.getPreset(fuelTypeNodes[i].GetValue("name"));
            }
            fuelType = Array.Find(fuelTypes, m => m.name == currentFuelType);
            if (fuelType == null && (fuelTypes != null && fuelTypes.Length > 0))
            {
                MonoBehaviour.print("ERROR: SSTUModularRCS - currentFuelType was null for value: " + currentFuelType);
                fuelType = fuelTypes[0];
                currentFuelType = fuelType.name;
                MonoBehaviour.print("Assigned default fuel type of: " + currentFuelType + ".  This is likely a config error that needs to be corrected.");
            }
            else if (fuelTypes == null || fuelTypes.Length < 1)
            {
                //TODO -- handle cases of disabled fuel switching
                MonoBehaviour.print("ERROR: SSTUModularRCS - No fuel type definitions found.");
            }
        }

        private void updateModelScale()
        {
            if (modelTransform != null)
            {
                modelTransform.localScale = new Vector3(currentScale, currentScale, currentScale);
            }
            standoffModule.model.updateScaleForDiameter(currentScale * structureScale);
            float position = -standoffModule.moduleHeight - structureOffset * currentScale;
            standoffModule.setPosition(position, ModelOrientation.TOP);
            standoffModule.updateModel();
        }

        private void updateRCSThrust()
        {
            if (modelTransform == null) { return; }//don't adjust thrust when not responsible for model scaling
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
            updateRCSFuelType(fuelType, part);
        }

        private void updateAttachNodes(bool userInput)
        {
            if (modelTransform == null) { return; }
            AttachNode srf = part.srfAttachNode;
            if (srf != null)
            {
                float standoffHeight = standoffModule.model.currentHeight + currentScale * structureOffset;
                Vector3 pos = new Vector3(standoffHeight, 0, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, srf, pos, srf.orientation, userInput);
            }
            AttachNode btm = part.FindAttachNode("bottom");
            if (btm != null)
            {
                float standoffHeight = standoffModule.model.currentHeight + currentScale * structureOffset;
                Vector3 pos = new Vector3(standoffHeight, 0, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, btm, pos, btm.orientation, userInput);
            }
        }

        private void updateMassAndCost()
        {
            modifiedMass = standoffModule.moduleMass;
            modifiedCost = standoffModule.moduleCost;
        }

        public static void updateRCSFuelType(string fuelType, Part part)
        {
            ContainerFuelPreset fuelTypeData = VolumeContainerLoader.getPreset(fuelType);
            if (fuelTypeData != null)
            {
                updateRCSFuelType(fuelTypeData, part);
            }
        }

        public static void updateRCSFuelType(ContainerFuelPreset fuelType, Part part)
        {
            ModuleRCS[] modules = part.GetComponents<ModuleRCS>();
            int len = modules.Length;
            ModuleRCS rcsModule;
            for (int i = 0; i < len; i++)
            {
                rcsModule = modules[i];
                rcsModule.propellants.Clear();
                ConfigNode pNode = fuelType.getPropellantNode(ResourceFlowMode.ALL_VESSEL_BALANCE);
                rcsModule.OnLoad(pNode);
            }
        }

        public static void updateRCSModules(Part part, bool enabled, float rcsPower, bool pitch, bool yaw, bool roll, bool x, bool y, bool z)
        {
            ModuleRCS[] modules = part.GetComponents<ModuleRCS>();
            int len = modules.Length;
            for (int i = 0; i < len; i++)
            {
                modules[i].moduleIsEnabled = enabled;
                modules[i].thrusterPower = rcsPower;
                modules[i].enablePitch = pitch;
                modules[i].enableYaw = yaw;
                modules[i].enableRoll = roll;
                modules[i].enableX = x;
                modules[i].enableY = y;
                modules[i].enableZ = z;
                modules[i].thrusterTransforms.Clear();//clear, in case it is holding refs to the old ones that were just unparented/destroyed
                modules[i].OnStart(StartState.Editor);//force update of fx/etc
            }

            //this mess was used to update the thruster effects in ModularUpperStage
            //but appears that 90% of it is uneccessary?
            //or perhaps it just requires that ModularRCSFX be used for proper effects setup?

            //ModuleRCS[] modules = part.GetComponents<ModuleRCS>();
            //int len = modules.Length;
            //for (int i = 0; i < len; i++)
            //{
            //    part.fxGroups.RemoveAll(m => modules[i].thrusterFX.Contains(m));
            //    part.fxGroups.ForEach(m => MonoBehaviour.print(m.name));
            //    modules[i].thrusterFX.ForEach(m => m.fxEmitters.ForEach(s => GameObject.Destroy(s.gameObject)));
            //    modules[i].thrusterFX.Clear();
            //    modules[i].thrusterTransforms.Clear();//clear, in case it is holding refs to the old ones that were just unparented/destroyed
            //    modules[i].OnStart(StartState.Editor);//force update of fx/etc
            //    modules[i].DeactivateFX();//doesn't appear to work
            //                              //TODO -- clean up this mess of linked stuff
            //    modules[i].thrusterFX.ForEach(m =>
            //    {
            //        m.setActive(false);
            //        m.SetPower(0);
            //        m.fxEmitters.ForEach(s => s.enabled = false);
            //    });
            //}
        }
    }
}
