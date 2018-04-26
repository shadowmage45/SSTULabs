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

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Block"),
            UI_ChooseOption(suppressEditorShipModified =true)]
        public string currentModel;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Block Texture"),
            UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentTexture = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Structure"),
            UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentStructure = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Structure Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentStructureTexture = string.Empty;

        [KSPField]
        public float thrustScalePower = 2f;

        [KSPField]
        public bool allowRescale = true;

        [KSPField(isPersistant = true)]
        public string modelPersistentData;

        [KSPField(isPersistant = true)]
        public string structurePersistentData;

        [Persistent]
        public string configNodeData;

        /// <summary>
        /// Container for the RCS block model -- only supports a single selection
        /// </summary>
        private ModelModule<SSTUModularRCS> rcsBlockModule;

        /// <summary>
        /// Container for the structural standoff model(s) - must contain at least one entry
        /// </summary>
        private ModelModule<SSTUModularRCS> standoffModule;
        
        /// <summary>
        /// The cached/base thrust of the ModuleRCS, used to determine proper run-time thrust when part is scaled.<para/>
        /// Public in order to support serialization.
        /// </summary>
        public float rcsThrust = -1;
        private float modifiedCost = -1;
        private float modifiedMass = -1;
        private Transform standoffTransform;
        private Transform modelTransform;
        private bool initialized = false;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            init();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            init();

            void modelChangeAction(SSTUModularRCS m)
            {
                m.updateModelScale();
                m.updateRCSThrust();
                m.updateAttachNodes(true);
                m.updateMassAndCost();
                SSTUModInterop.onPartGeometryUpdate(m.part, true);                
            };

            Fields[nameof(currentModel)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                standoffModule.modelSelected(a, b);                
                this.actionWithSymmetry(m =>
                {
                    modelChangeAction(m);
                });
            };

            Fields[nameof(currentStructure)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                standoffModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    modelChangeAction(m);
                });
            };

            Fields[nameof(currentScale)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    if (m != this) { m.currentScale = currentScale; }
                    modelChangeAction(m);
                });
            };

            Fields[nameof(currentScale)].guiActiveEditor = allowRescale;
        }

        public void Start()
        {
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

            modelTransform = part.transform.FindRecursive("model").FindOrCreate("ModularRCSModel");
            rcsBlockModule = new ModelModule<SSTUModularRCS>(part, this, modelTransform, ModelOrientation.CENTRAL, nameof(currentModel), null, nameof(currentTexture), nameof(modelPersistentData), null, null, null, null);
            rcsBlockModule.getSymmetryModule = m => m.rcsBlockModule;
            rcsBlockModule.setupModelList(SSTUModelData.getModelDefinitions(node.GetNodes("MODEL")));
            rcsBlockModule.setupModel();

            standoffTransform = part.transform.FindRecursive("model").FindOrCreate("ModularRCSStandoff");
            standoffTransform.localRotation = Quaternion.Euler(0, 0, 90);//rotate 90' on z-axis, to face along x+/-; this should put the 'top' of the model at 0,0,0
            standoffModule = new ModelModule<SSTUModularRCS>(part, this, standoffTransform, ModelOrientation.TOP, nameof(currentStructure), null, nameof(structurePersistentData), nameof(currentStructureTexture), null, null, null, null);
            standoffModule.getSymmetryModule = m => m.standoffModule;
            standoffModule.setupModelList(SSTUModelData.getModelDefinitions(node.GetNodes("STRUCTURE")));
            standoffModule.setupModel();
                       

            updateModelScale();
            updateMassAndCost();

            updateAttachNodes(false);
        }

        private void updateModelScale()
        {
            if (modelTransform != null)
            {
                modelTransform.localScale = new Vector3(currentScale, currentScale, currentScale);
            }
            standoffModule.setScaleForDiameter(currentScale * structureScale);
            float position = -standoffModule.moduleHeight - structureOffset * currentScale;
            standoffModule.setPosition(position, ModelOrientation.TOP);
            standoffModule.updateModelMeshes();
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

        private void updateAttachNodes(bool userInput)
        {
            if (modelTransform == null) { return; }
            AttachNode srf = part.srfAttachNode;
            if (srf != null)
            {
                float standoffHeight = standoffModule.moduleHeight + currentScale * structureOffset;
                Vector3 pos = new Vector3(standoffHeight, 0, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, srf, pos, srf.orientation, userInput);
            }
            AttachNode btm = part.FindAttachNode("bottom");
            if (btm != null)
            {
                float standoffHeight = standoffModule.moduleHeight + currentScale * structureOffset;
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
