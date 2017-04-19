using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools.Module
{
    public class SSTUModularFuelTankRCS : SSTUModularFuelTank
    {

        /// <summary>
        /// Whether the RCS should be positioned on the mount (using the mount defined locations and orientations),
        /// or if it should be positioned on the tank and the UI position and rotation sliders enabled.
        /// </summary>
        [KSPField]
        public bool rcsOnMount = true;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "RCS Model"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentRCSModule = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "RCS Texture"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public string currentRCSTexture = string.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "RCS Pos"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentRCSVert = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "RCS Rot"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentRCSRot = 0f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "RCS Size"),
         UI_FloatEdit(sigFigs = 4, suppressEditorShipModified = true)]
        public float currentRCSScale = 1f;

        //persistent data field for RCS module, saves texture set custom colors
        [KSPField(isPersistant =true)]
        public string rcsModuleData = string.Empty;

        private ModelModule<RCSModelData> rcsModule;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
        }

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);
            rcsModule.model.updateRCSModule(part);
        }

        protected override void initialize()
        {
            base.initialize();
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            rcsModule = new ModelModule<RCSModelData>(part, this, getRootTransform("MFT-RCS", true), ModelOrientation.CENTRAL, nameof(rcsModuleData), nameof(currentRCSModule), nameof(currentRCSTexture));
            rcsModule.setupModelList(SingleModelData.parseModels<RCSModelData>(node.GetNodes("RCS"), m => new RCSModelData(m)));
            updateRCSModule();
            rcsModule.setupModel();
        }

        private void updateRCSModule()
        {
            //TODO -- adapt all of these for 'rcsOnMount=true' functionality -- use parameters from mount module and model definition
            rcsModule.model.symmetry = 2;//TODO
            rcsModule.model.rotationOffset = currentRCSRot;
            rcsModule.model.setPosition(currentRCSVert, ModelOrientation.CENTRAL);
            rcsModule.model.spacingRadius = currentTankDiameter * 0.5f;
        }

    }

    public class RCSModelData : SingleModelData
    {

        public string rcsThrustTransformName;

        public float rcsThrust;

        public GameObject[] rcsModels;

        public int symmetry = 4;

        public float spacingRadius = 1f;

        public float rotationOffset = 0f;

        public RCSModelData(ConfigNode node) : base(node)
        {
            rcsThrustTransformName = node.GetStringValue("thrustTransform");
            rcsThrust = node.GetFloatValue("thrust");
        }

        public override void setupModel(Transform parent, ModelOrientation orientation)
        {
            if (symmetry < 2) { symmetry = 2; }
            SSTUUtils.destroyChildrenImmediate(parent);            
            model = new GameObject("RCSModels");
            model.transform.NestToParent(parent);
            int len = symmetry;
            rcsModels = new GameObject[len];
            for (int i = 0; i < len; i++)
            {
                rcsModels[i] = SSTUUtils.cloneModel(modelDefinition.modelName);
                rcsModels[i].transform.NestToParent(model.transform);
            }
        }

        public override void updateModel()
        {
            float spacingRotation = 360f / symmetry;
            float x, z;
            float angle = 0;
            for (int i = 0; i < symmetry; i++)
            {
                angle = (i * spacingRotation + rotationOffset) * Mathf.Deg2Rad;
                x = Mathf.Cos(angle) * spacingRadius;
                z = Mathf.Sin(angle) * spacingRadius;
                rcsModels[i].transform.localPosition = new Vector3(x, currentVerticalPosition, z);
                rcsModels[i].transform.Rotate(0, i * spacingRotation + modelDefinition.rcsVerticalRotation, 0, Space.Self);
            }
        }

        /// <summary>
        /// Update the stats of the ModuleRCS for the current scale and transform names.  Should be called from Start() or on user-initiated module changes.
        /// Will add/remove ModuleRCS from the part as needed
        /// </summary>
        /// <param name="part"></param>
        public void updateRCSModule(Part part)
        {
            ModuleRCS rcs = part.GetComponent<ModuleRCS>();
            if (rcs == null)
            {
                return;
            }
            rcs.thrusterTransformName = rcsThrustTransformName;
            rcs.thrusterPower = rcsThrust * currentDiameterScale * currentDiameterScale;
            PartModule.StartState state = HighLogic.LoadedSceneIsFlight ? PartModule.StartState.Flying : PartModule.StartState.Editor;
            rcs.OnStart(state);//causes RCS module to update its transform cache and effects
        }

        private void removeRCSModule(Part part)
        {

        }

        private void addRCSModule(Part part)
        {

        }

    }

}
