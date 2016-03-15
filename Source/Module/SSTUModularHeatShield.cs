using System;
using UnityEngine;
using System.Collections.Generic;

namespace SSTUTools
{
    public class SSTUModularHeatShield : PartModule, IPartMassModifier, IPartCostModifier
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

        [KSPField]
        public String techLimitSet = "Default";

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
        private HeatShieldType[] shieldTypes;
        private float modifiedCost;
        private float modifiedMass;

        private SSTUHeatShield customHeatshieldModule;//TODO
        private ModuleAblator stockHeatshieldModule;//TODO

        //TODO -- make these private after debug and testing
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
            HeatShieldType next = SSTUUtils.findNext(shieldTypes, m => m.name == currentShieldType, false);
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
            updatePartMass();
            updatePartCost();
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
            currentShieldTypeData = Array.Find(shieldTypes, m => m.name == currentShieldType);
            updateModuleStats();
            updatePartResources();
            updatePartMass();
            updatePartCost();
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
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
            {
                prefabMass = part.mass;
            }
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

        public float GetModuleMass(float defaultMass)
        {
            return -defaultMass + modifiedMass;
        }

        public float GetModuleCost(float defaultCost)
        {
            return -defaultCost + modifiedCost;
        }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Initialization

        private void initialize()
        {
            if (mainModelData != null) { return; }
            prefabMass = part.partInfo == null ? part.mass : part.partInfo.partPrefab.mass;
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);
            TechLimit.updateTechLimits(techLimitSet, out techLimitMaxDiameter);            

            ConfigNode modelNode = node.GetNode("MAINMODEL");
            mainModelData = new SingleModelData(modelNode);
            mainModelData.setupModel(part, part.transform.FindRecursive("model"), ModelOrientation.CENTRAL, true);
            setModelDiameter(currentDiameter);
            
            ConfigNode[] typeNodes = node.GetNodes("SHIELDTYPE");
            int len = typeNodes.Length;
            shieldTypes = new HeatShieldType[len];
            for (int i = 0; i < len; i++)
            {
                shieldTypes[i] = new HeatShieldType(typeNodes[i]);
            }
            currentShieldTypeData = Array.Find(shieldTypes, m => m.name == currentShieldType);
            
            updateDragCube();
            updateAttachNodes(false);
            updatePartMass();
            if (!initializedResources && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                updatePartResources();
                initializedResources = true;
            }
            if (!String.IsNullOrEmpty(transformsToRemove))
            {
                SSTUUtils.removeTransforms(part, SSTUUtils.parseCSV(transformsToRemove));
            }
        }

        #endregion ENDREGION - Initialization

        #region REGION - Update Methods

        private void updateFairing()
        {

        }
        
        private void updateModuleStats()
        {
            float scale = mainModelData.currentDiameterScale;
            ablatMult = Mathf.Pow(scale, ablationScalePower) * currentShieldTypeData.ablationMult;
            SSTUHeatShield chs = part.GetComponent<SSTUHeatShield>();
            if (chs != null)
            {
                chs.ablationMult = ablatMult;
                chs.heatCurve = currentShieldTypeData.heatCurve;
            }
        }

        private void updatePartMass()
        {
            float scale = Mathf.Pow(mainModelData.currentDiameterScale, massScalePower);
            modifiedMass = scale * prefabMass;
            part.mass = modifiedMass;
        }

        private void updatePartCost()
        {
            modifiedCost = 10000f;
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
        public float ablationMult;
        public float massMult = 1f;
        public FloatCurve heatCurve;

        public HeatShieldType(ConfigNode node)
        {
            name = node.GetStringValue("name");
            heatCurve = node.GetFloatCurve("heatCurve");
            resourceMult = node.GetFloatValue("resourceMult", resourceMult);
            ablationMult = node.GetFloatValue("ablationMult", ablationMult);
            massMult = node.GetFloatValue("massMult", massMult);
        }
    }
}
