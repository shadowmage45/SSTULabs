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
        public float resourceScalePower = 3f;

        [KSPField]
        public float fluxScalePower = 3f;

        [KSPField]
        public float ablationScalePower = 3f;

        [KSPField]
        public float resourceQuantity = 200f;

        [KSPField(isPersistant = true, guiName ="Shield Type", guiActiveEditor = true, guiActive = true)]
        public String currentShieldType;

        [KSPField(isPersistant =true, guiName ="Diameter", guiActiveEditor = true)]
        public float currentDiameter = 1.25f;

        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        public float prefabMass;

        private SingleModelData mainModelData;
        private float techLimitMaxDiameter;
        private HeatShieldType currentShieldTypeData;
        private HeatShieldType[] shieldTypeDatas;
        private float modifiedCost;
        private float modifiedMass;

        private SSTUHeatShield customHeatshieldModule;//TODO
        private ModuleAblator stockHeatshieldModule;//TODO

        //TODO -- make these private after debug and testing
        [KSPField(guiName = "Flux Mult", guiActiveEditor = true, guiActive = true)]
        public float fluxMult;

        [KSPField(guiName = "Abl Mult", guiActiveEditor = true, guiActive = true)]
        public float ablatMult;

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

        [KSPEvent(guiName = "Next Shield Type", guiActiveEditor = true)]
        public void nextShieldTypeEvent()
        {
            HeatShieldType next = SSTUUtils.findNext(shieldTypeDatas, m => m.name == currentShieldType, false);
            setShieldTypeFromEditor(next.name, true);
        }

        private void setDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {
            if (newDiameter < minDiameter) { newDiameter = minDiameter; }
            if (newDiameter > maxDiameter) { newDiameter = maxDiameter; }
            if (newDiameter > techLimitMaxDiameter) { newDiameter = techLimitMaxDiameter; }
            currentDiameter = newDiameter;
            setModelDiameter(currentDiameter);
            updateModuleStats();
            updatePartResources();
            updateAttachNodes(true);
            updateDragCube();
            if (updateSymmetry)
            {
                SSTUModularHeatShield mhs;
                foreach (Part p in part.symmetryCounterparts)
                {
                    mhs = p.GetComponent<SSTUModularHeatShield>();
                    mhs.setDiameterFromEditor(newDiameter, false);
                }
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }

        private void setShieldTypeFromEditor(String newType, bool updateSymmetry)
        {
            currentShieldType = newType;            
            updateModuleStats();
            updatePartResources();
            if (updateSymmetry)
            {
                SSTUModularHeatShield mhs;
                foreach (Part p in part.symmetryCounterparts)
                {
                    mhs = p.GetComponent<SSTUModularHeatShield>();
                    mhs.setShieldTypeFromEditor(newType, false);
                }
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
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
            return base.GetInfo();
        }

        public void Start()
        {
            updateModuleStats();
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

            ConfigNode[] typeNodes = node.GetNodes("SHIELDTYPE");
            int len = typeNodes.Length;
            shieldTypeDatas = new HeatShieldType[len];
            for (int i = 0; i < len; i++)
            {
                shieldTypeDatas[i] = new HeatShieldType(typeNodes[i]);
            }
            currentShieldTypeData = Array.Find(shieldTypeDatas, m => m.name == currentShieldType);

            updateDragCube();
            if (!initializedResources && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                updatePartResources();
                initializedResources = true;
            }
            updateAttachNodes(false);
            if (!String.IsNullOrEmpty(transformsToRemove))
            {
                SSTUUtils.removeTransforms(part, SSTUUtils.parseCSV(transformsToRemove));
            }
        }

        #endregion ENDREGION - Initialization

        #region REGION - Update Methods

        //TODO
        private void updateModuleStats()
        {
            fluxMult = Mathf.Pow(currentShieldTypeData.fluxMult, fluxScalePower);
        }

        private void updatePartMass()
        {

        }

        private void updatePartCost()
        {

        }

        private void updatePartResources()
        {
            float amount = resourceQuantity;
            float scale = Mathf.Pow(mainModelData.currentDiameterScale, resourceScalePower);
            amount = amount * scale * currentShieldTypeData.resourceMult;

            SSTUResourceList list = new SSTUResourceList();
            list.addResource(resourceName, amount);
            list.setResourcesToPart(part, true);
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

    public class HeatShieldType
    {
        public readonly String name;
        public float resourceMult = 1f;
        public float fluxMult = 1f;
        public float ablationMult;
        public float massMult = 1f;

        public HeatShieldType(ConfigNode node)
        {
            name = node.GetStringValue("name");
            resourceMult = node.GetFloatValue("resourceMult", resourceMult);
            fluxMult = node.GetFloatValue("fluxMult", fluxMult);
            ablationMult = node.GetFloatValue("ablationMult", ablationMult);
            massMult = node.GetFloatValue("massMult", massMult);
        }
    }
}
