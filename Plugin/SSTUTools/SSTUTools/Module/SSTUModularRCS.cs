using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSPShaderTools;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUModularRCS : PartModule, IPartCostModifier, IPartMassModifier, IRecolorable
    {

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Scale"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true, minValue = 0.05f, maxValue = 5f, incrementSmall = 0.25f, incrementLarge = 1f, incrementSlide = 0.05f)]
        public float currentScale = 1f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Block"),
         UI_ChooseOption(suppressEditorShipModified =true)]
        public string currentModel = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Block Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentTexture = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Structure"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentStructure = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Structure Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentStructureTexture = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Structure Layout"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentStructureLayout;

        [KSPField]
        public string rcsThrustTransformName = string.Empty;

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

        private float modifiedCost = -1;
        private float modifiedMass = -1;

        private Transform standoffTransform;
        private Transform modelTransform;
        private bool initialized = false;

        //----------------------------------------------------Standard lifecycle methods--------------------------------------------
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

            Action<SSTUModularRCS> modelChangeAction = delegate (SSTUModularRCS m)
            {
                m.updateModelScale();
                m.updateAttachNodes(true);
                m.updateMassAndCost();
                SSTUModInterop.onPartGeometryUpdate(m.part, true);                
            };

            Fields[nameof(currentModel)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                rcsBlockModule.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    modelChangeAction(m);
                    m.updateRCSThrust();
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

        //----------------------------------------------------IPartXModifier interface methos--------------------------------------------
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

        //----------------------------------------------------IRecolorable interface methos--------------------------------------------
        public string[] getSectionNames()
        {
            return new string[] { "RCS Block" , "Standoff Structure"};
        }

        public RecoloringData[] getSectionColors(string name)
        {
            switch (name)
            {
                case "RCS Block":
                    return rcsBlockModule.recoloringData;
                case "Standoff Structure":
                    return standoffModule.recoloringData;
            }
            return rcsBlockModule.recoloringData;
        }

        public TextureSet getSectionTexture(string name)
        {
            switch (name)
            {
                case "RCS Block":
                    return rcsBlockModule.textureSet;
                case "Standoff Structure":
                    return standoffModule.textureSet;
            }
            return rcsBlockModule.textureSet;
        }

        public void setSectionColors(string name, RecoloringData[] colors)
        {
            switch (name)
            {
                case "RCS Block":
                    rcsBlockModule.setSectionColors(colors);
                    break;
                case "Standoff Structure":
                    standoffModule.setSectionColors(colors);
                    break;
            }
        }

        private void init()
        {
            if (initialized) { return; }
            initialized = true;
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            ModelDefinitionLayoutOptions[] blocks = SSTUModelData.getModelDefinitions(node.GetNodes("MODEL"));
            ModelDefinitionLayoutOptions[] structs = SSTUModelData.getModelDefinitions(node.GetNodes("STRUCTURE"));

            modelTransform = part.transform.FindRecursive("model").FindOrCreate("ModularRCSModel");
            rcsBlockModule = new ModelModule<SSTUModularRCS>(part, this, modelTransform, ModelOrientation.CENTRAL, nameof(currentModel), null, nameof(currentTexture), nameof(modelPersistentData), null, null, null, null);
            rcsBlockModule.name = "RCSBlock";
            standoffModule.setModuleOrientation(Axis.YPlus, Axis.ZPlus);
            rcsBlockModule.getSymmetryModule = m => m.rcsBlockModule;
            rcsBlockModule.setupModelList(blocks);
            rcsBlockModule.getValidOptions = () => blocks;
            rcsBlockModule.setupModel();
            rcsBlockModule.updateSelections();

            standoffTransform = part.transform.FindRecursive("model").FindOrCreate("ModularRCSStandoff");
            standoffModule = new ModelModule<SSTUModularRCS>(part, this, standoffTransform, ModelOrientation.TOP, nameof(currentStructure), nameof(currentStructureLayout), nameof(currentStructureTexture), nameof(structurePersistentData), null, null, null, null);
            standoffModule.name = "Standoff";
            standoffModule.setModuleOrientation(Axis.YPlus, Axis.ZPlus);
            standoffModule.getSymmetryModule = m => m.standoffModule;
            standoffModule.setupModelList(structs);
            standoffModule.getValidOptions = () => structs;
            standoffModule.setupModel();
            standoffModule.updateSelections();

            updateModelScale();
            updateMassAndCost();
            rcsBlockModule.renameRCSThrustTransforms(rcsThrustTransformName);

            updateAttachNodes(false);
        }

        private void updateModelScale()
        {
            rcsBlockModule.setScale(currentScale);
            rcsBlockModule.updateModelMeshes();
            standoffModule.setDiameterFromAbove(rcsBlockModule.moduleDiameter);
            //standoffModule.setPosition(rcsBlockModule.moduleBottom - standoffModule.moduleHeight, ModelOrientation.TOP);
            standoffModule.updateModelMeshes();
        }

        private void updateRCSThrust()
        {
            rcsBlockModule.renameRCSThrustTransforms(rcsThrustTransformName);
            ModuleRCS rcsModule = part.GetComponent<ModuleRCS>();
            if (rcsModule != null)
            {
                ModelRCSModuleData data = rcsBlockModule.layoutOptions.definition.rcsModuleData;
                float thrust = data.getThrust(rcsBlockModule.moduleVerticalScale);
                rcsModule.thrusterPower = thrust;
            }
        }

        private void updateAttachNodes(bool userInput)
        {
            float standoffBottomZ = standoffModule.moduleBottom;
            Vector3 pos = new Vector3(standoffBottomZ, 0, 0);
            AttachNode srfNode = part.srfAttachNode;
            if (srfNode != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, srfNode, pos, srfNode.orientation, userInput);
            }
            AttachNode bottomNode = part.FindAttachNode("bottom");
            if (bottomNode != null)
            {
                SSTUAttachNodeUtils.updateAttachNodePosition(part, bottomNode, pos, bottomNode.orientation, userInput);
            }
        }

        private void updateMassAndCost()
        {
            modifiedMass = standoffModule.moduleMass;
            modifiedCost = standoffModule.moduleCost;
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
