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
        
        [KSPField(isPersistant = true, guiName ="Shield Type", guiActiveEditor = true, guiActive = true),
         UI_ChooseOption(options = new string[] { "Light", "Medium", "Heavy", "ExtraHeavy" }, suppressEditorShipModified =true)]
        public String currentShieldType = "Medium";

        [KSPField(isPersistant =true, guiName ="Diameter", guiActiveEditor = true),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentDiameter = 1.25f;

        [KSPField(isPersistant = true)]
        public bool initializedResources = false;
        
        private SingleModelData mainModelData;
        private float techLimitMaxDiameter;
        private HeatShieldType currentShieldTypeData;
        private HeatShieldType[] shieldTypes;
        private float modifiedCost;
        private float modifiedMass;
        private float prevDiameter;
        private string prevType;
        
        //TODO -- make these private after debug and testing
        [KSPField(guiName = "Abl Mult", guiActiveEditor = true, guiActive = true)]
        public float ablatMult;

        #region REGION - GUI Events / interaction

        public void onDiameterUpdated(BaseField field, object obj)
        {
            if (currentDiameter != prevDiameter)
            {
                prevDiameter = currentDiameter;
                setDiameterFromEditor(currentDiameter, true);
                updateFairing(true);
            }
        }

        public void onTypeUpdated(BaseField field, object obj)
        {
            if (prevType != currentShieldType)
            {
                prevType = currentShieldType;
                setShieldTypeFromEditor(currentShieldType, true);
            }
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
            updatePartCost();
            updateAttachNodes(true);
            updateDragCube();
            updateEditorFields();
            if (updateSymmetry)
            {
                SSTUModularHeatShield mhs;
                foreach (Part p in part.symmetryCounterparts)
                {
                    mhs = p.GetComponent<SSTUModularHeatShield>();
                    mhs.setDiameterFromEditor(newDiameter, false);
                }
            }
        }

        private void setShieldTypeFromEditor(String newType, bool updateSymmetry)
        {
            currentShieldType = newType;
            currentShieldTypeData = Array.Find(shieldTypes, m => m.name == currentShieldType);
            updateModuleStats();
            updatePartResources();
            updatePartCost();
            updateEditorFields();
            if (updateSymmetry)
            {
                SSTUModularHeatShield mhs;
                foreach (Part p in part.symmetryCounterparts)
                {
                    mhs = p.GetComponent<SSTUModularHeatShield>();
                    mhs.setShieldTypeFromEditor(newType, false);
                }
                SSTUStockInterop.fireEditorUpdate();
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
            string[] options = SSTUUtils.getNames(shieldTypes, m => m.name);
            float max = techLimitMaxDiameter < maxDiameter ? techLimitMaxDiameter : maxDiameter;
            this.updateUIChooseOptionControl("currentShieldType", options, options, true, currentShieldType);
            this.updateUIFloatEditControl("currentDiameter", minDiameter, max, diameterIncrement*2f, diameterIncrement, diameterIncrement*0.05f, true, currentDiameter);
            this.Fields["currentShieldType"].uiControlEditor.onFieldChanged = onTypeUpdated;
            this.Fields["currentDiameter"].uiControlEditor.onFieldChanged = onDiameterUpdated;
        }

        public override string GetInfo()
        {
            return base.GetInfo();
        }

        public void Start()
        {
            updateModuleStats();
            updateFairing(false);
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            return -defaultMass + modifiedMass;
        }

        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            return -defaultCost + modifiedCost;
        }
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Initialization

        private void initialize()
        {
            if (mainModelData != null) { return; }
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
            if (!initializedResources && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                updatePartResources();
                initializedResources = true;
            }
            if (!String.IsNullOrEmpty(transformsToRemove))
            {
                SSTUUtils.removeTransforms(part, SSTUUtils.parseCSV(transformsToRemove));
            }
            updateEditorFields();
        }

        #endregion ENDREGION - Initialization

        #region REGION - Update Methods

        private void updateFairing(bool userInput)
        {
            SSTUNodeFairing fairing = part.GetComponent<SSTUNodeFairing>();
            if (fairing == null) { return; }
            fairing.canDisableInEditor = true;
            FairingUpdateData data = new FairingUpdateData();
            data.setTopY(mainModelData.currentHeight*0.5f);
            data.setTopRadius(currentDiameter * 0.5f);
            if (userInput)
            {
                data.setBottomRadius(currentDiameter * 0.5f);
            }
            data.setEnable(true);
            fairing.updateExternal(data);
        }

        private void updateEditorFields()
        {
            prevDiameter = currentDiameter;
            prevType = currentShieldType;
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

        private void updatePartCost()
        {
            float scale = Mathf.Pow(mainModelData.currentDiameterScale, massScalePower);
            modifiedCost = 10000f;
            modifiedMass = scale * part.prefabMass * currentShieldTypeData.massMult;
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
            SSTUModInterop.onPartGeometryUpdate(part, true);
            SSTUStockInterop.fireEditorUpdate();
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
