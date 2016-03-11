using System;
using UnityEngine;
using System.Collections.Generic;

namespace SSTUTools
{
    public class SSTUModularHeatShield : PartModule
    {

        [KSPField]
        public String modelName = "Squad/Parts/Aero/HeatShield/HeatShield3";

        [KSPField]
        public String resourceName = "Ablator";

        [KSPField]
        public String transformsToRemove = String.Empty;
            
        [KSPField]
        public float diameterIncrement = 0.625f;

        [KSPField]
        public float minDiameter = 0.625f;

        [KSPField]
        public float maxDiameter = 10f;

        [KSPField]
        public float massScalePower = 3f;

        [KSPField]
        public float ablatorScalePower = 3f;

        [KSPField(isPersistant =true, guiName ="Diameter", guiActiveEditor = true)]
        public float currentDiameter = 1.25f;

        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        private SingleModelData mainModelData;
        private float techLimitMaxDiameter;

        private SSTUHeatShield customHeatshieldModule;//TODO
        private ModuleAblator stockHeatshieldModule;//TODO

        #region REGION - GUI Events / interaction

        [KSPEvent(guiName = "Diameter ++", guiActiveEditor = true)]
        public void nextDiameterEvent()
        {
            setDiameterFromEditor(currentDiameter + diameterIncrement, true);
        }

        [KSPEvent(guiName = "Diameter --", guiActiveEditor = true)]
        public void prevDiameterEvent()
        {
            setDiameterFromEditor(currentDiameter - diameterIncrement, true);
        }

        private void setDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {
            //TODO bounds and tech-limit checks
            if (newDiameter < minDiameter) { newDiameter = minDiameter; }
            if (newDiameter > maxDiameter) { newDiameter = maxDiameter; }
            if (newDiameter > techLimitMaxDiameter) { newDiameter = techLimitMaxDiameter; }
            currentDiameter = newDiameter;
            setModelDiameter(currentDiameter);
            updateModuleStats();
            updateAttachNodes(true);
            if (updateSymmetry)
            {
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
                SSTUModularHeatShield mhs;
                foreach (Part p in part.symmetryCounterparts)
                {
                    mhs = p.GetComponent<SSTUModularHeatShield>();
                    mhs.setDiameterFromEditor(newDiameter, false);
                }
            }
            //TODO symmetry updates
        }

        #endregion ENDREGION - GUI Events / interaction

        #region REGION - Standard KSP Overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            initialize();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        public override string GetInfo()
        {
            //TODO clear existing resources
            return base.GetInfo();
        }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Initialization

        private void initialize()
        {
            if (mainModelData != null) { return; }
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);
            TechLimitDiameter.updateTechLimits(TechLimitDiameter.loadTechLimits(node.GetNodes("TECHLIMIT")), out techLimitMaxDiameter);
            ConfigNode modelNode = node.GetNode("MAINMODEL");
            mainModelData = new SingleModelData(modelNode);
            mainModelData.setupModel(part, part.transform.FindRecursive("model"), ModelOrientation.CENTRAL, true);
            setModelDiameter(currentDiameter);
            updateDragCube();
            //TODO tech limit loading
            if (!initializedResources && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                initializeResources();
                initializedResources = true;
            }
            else if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
            {
                //TODO handle this, removal during getinfo call; so that the resources are present on the icon part for the setup of the icon part
                //initializeResources();
            }
            updateAttachNodes(false);
            updateModuleStats();
            if (!String.IsNullOrEmpty(transformsToRemove))
            {
                SSTUUtils.removeTransforms(part, SSTUUtils.parseCSV(transformsToRemove));
            }
        }

        //TODO
        private void initializeResources()
        {

        }

        #endregion ENDREGION - Initialization

        #region REGION - Update Methods

        //TODO
        private void updateModuleStats()
        {

        }
        
        private void updateAttachNodes(bool userInput)
        {
            float height = mainModelData.currentHeight;
            AttachNode topNode = part.findAttachNode("top");
            if (topNode != null)
            {
                Vector3 topNodePos = new Vector3(0, height * 0.5f, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, topNode, topNodePos, topNode.orientation, userInput);
            }
            AttachNode bottomNode = part.findAttachNode("bottom");
            if (bottomNode != null)
            {
                Vector3 botNodePos = new Vector3(0, -height * 0.5f, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, bottomNode, botNodePos, bottomNode.orientation, userInput);
            }
        }

        private void setModelDiameter(float diameter)
        {
            mainModelData.updateScaleForDiameter(diameter);
            mainModelData.currentVerticalPosition = mainModelData.currentHeight * 0.5f + mainModelData.modelDefinition.verticalOffset * mainModelData.currentHeightScale;
            mainModelData.updateModel();
        }

        private void updateDragCube()
        {
            if(HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                SSTUModInterop.onPartGeometryUpdate(part, true);
            }
        }

        #endregion ENDREGION - Update Methods
    }
}
