using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUModularHeatShield : PartModule, IPartMassModifier, IPartCostModifier
    {
        #region REGION - Base Heat Shield Parameters
        [KSPField]
        public String resourceName = "Ablator";

        [KSPField]
        public Vector3 heatShieldVector = Vector3.down;
        
        [KSPField]
        public float ablationStartTemp = 500f;
        
        [KSPField]
        public float heatShieldMinDot = 0.2f;

        [KSPField]
        public float heatShieldMaxDot = 0.8f;

        [KSPField]
        public float ablationEfficiency = 6000f;

        [KSPField]
        public float ablationMult = 1f;

        [KSPField]
        public float fluxMult = 1f;

        [KSPField]
        public bool heatSoak = false;

        [KSPField]
        public bool areaAdjusted = false;
        
        [KSPField]
        public bool autoDebug = false;

        [KSPField]
        public FloatCurve heatCurve;

        [KSPField]
        public float shieldMass = 0.25f;

        #endregion

        #region REGION - ModularHeatShield resizing data

        [KSPField]
        public String modelName = String.Empty;

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
        public float baseResourceQuantity = 200f;
        
        [KSPField]
        public String techLimitSet = "Default";

        [KSPField]
        public bool standAlonePart = false;

        #endregion

        #region REGION - persistent fields

        [KSPField(isPersistant = true, guiName = "Shield Type", guiActiveEditor = true, guiActive = false),
         UI_ChooseOption(options = new string[] { "Light", "Medium", "Heavy", "ExtraHeavy" }, suppressEditorShipModified = true)]
        public String currentShieldType = "Medium";

        [KSPField(isPersistant = true, guiName = "Diameter", guiActiveEditor = true),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentDiameter = 1.25f;

        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        #endregion

        #region REGION - Gui Display fields

        [KSPField(guiActive =true, guiName ="HS Flux")]
        public double guiShieldFlux = 0;
        [KSPField(guiActive = true, guiName = "HS Use")]
        public double guiShieldUse = 0;
        [KSPField(guiActive = true, guiName = "HS Temp")]
        public double guiShieldTemp = 0;
        [KSPField(guiActive = true, guiName = "HS Eff")]
        public double guiShieldEff = 0;

        //[KSPField(guiActive = true, guiName = "Flux/area")]
        //public double fluxPerSquareMeter;
        //[KSPField(guiActive = true, guiName = "Flux/mass")]
        //public double fluxPerTMass;

        #endregion

        #region REGION - private working variables

        //cached vars for heat-shield thermal processing
        private double baseSkinIntMult = 1;
        private double useToFluxMultiplier = 1;
        private PartResource resource;

        //modular heat-shield fields, for updating shield type
        private string[] shieldTypeNames;
        private HeatShieldType currentShieldTypeData;
        private string prevType;
        private float modifiedCost;
        private float modifiedMass;

        //resizable heat-shield fields
        private SingleModelData mainModelData;
        private float techLimitMaxDiameter;
        private float prevDiameter;

        #endregion

        #region REGION - GUI interaction methods

        public void onTypeUpdated(BaseField field, object obj)
        {
            if (prevType != currentShieldType)
            {
                prevType = currentShieldType;
                setShieldTypeFromEditor(currentShieldType, true);
            }
        }

        private void setShieldTypeFromEditor(String newType, bool updateSymmetry)
        {
            currentShieldType = newType;
            currentShieldTypeData = SSTUDatabase.getHeatShieldType(newType);
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

        public void onDiameterUpdated(BaseField field, object obj)
        {            
            if (standAlonePart && currentDiameter != prevDiameter)
            {
                prevDiameter = currentDiameter;
                setDiameterFromEditor(currentDiameter, true);
                updateFairing(true);
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

        #endregion

        #region REGION - Standard KSP Overrides

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
            if (autoDebug)
            {
                PhysicsGlobals.ThermalDataDisplay = true;
            }
            string[] options = shieldTypeNames;
            this.updateUIChooseOptionControl("currentShieldType", options, options, true, currentShieldType);
            this.updateUIFloatEditControl("currentDiameter", minDiameter, maxDiameter, diameterIncrement * 2f, diameterIncrement, diameterIncrement * 0.5f, true, currentDiameter);
            this.Fields["currentShieldType"].uiControlEditor.onFieldChanged = onTypeUpdated;
            this.Fields["currentDiameter"].uiControlEditor.onFieldChanged = onDiameterUpdated;
            this.Fields["currentDiameter"].guiActiveEditor = standAlonePart;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public void Start()
        {
            if (standAlonePart)
            {
                updateFairing(false);
            }
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit){return standAlonePart ? -defaultMass + modifiedMass : modifiedMass;}
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit){return standAlonePart ? -defaultCost + modifiedCost : modifiedCost;}
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        #endregion

        #region REGION - Init and setup

        private void initialize()
        {
            if (heatCurve == null)
            {
                heatCurve = new FloatCurve();
                heatCurve.Add(0, 0.00002f);//very minimal initial ablation factor
                heatCurve.Add(50, 0.00005f);//ramp it up fairly quickly though
                heatCurve.Add(150, 0.00015f);
                heatCurve.Add(500, 0.00050f);
                heatCurve.Add(750, 0.00075f);
                heatCurve.Add(1000, 0.00100f);
                heatCurve.Add(2000, 0.00400f);
                heatCurve.Add(3000, 0.00800f);//generally, things will explode before this point
                heatCurve.Add(10000, 0.05000f);//but just in case, continue the curve up to insane levels
            }
            double hsp = 1;
            double dens = 1;
            if (heatSoak)
            {
                PartResourceDefinition resource = PartResourceLibrary.Instance.GetDefinition(resourceName);
                hsp = resource.specificHeatCapacity;
                dens = resource.density;
            }
            else
            {
                resource = part.Resources[resourceName];
                if (resource != null)
                {
                    hsp = resource.info.specificHeatCapacity;
                    dens = resource.info.density;
                }
                else
                {
                    hsp = PhysicsGlobals.StandardSpecificHeatCapacity;
                    dens = 0.005f;
                }
            }
            useToFluxMultiplier = hsp * ablationEfficiency * dens * ablationMult;
            baseSkinIntMult = part.skinInternalConductionMult;
            
            //stand-alone modular heat-shield setup
            if (standAlonePart)
            {
                if (string.IsNullOrEmpty(modelName))
                {
                    MonoBehaviour.print("SEVERE ERROR: SSTUModularHeatShield could has no model specified for part: " + part.name);
                }

                if (!String.IsNullOrEmpty(transformsToRemove))
                {
                    SSTUUtils.removeTransforms(part, SSTUUtils.parseCSV(transformsToRemove));
                }

                techLimitMaxDiameter = SSTUStockInterop.getTechLimit(techLimitSet);
                if (currentDiameter > techLimitMaxDiameter) { currentDiameter = techLimitMaxDiameter; }
                shieldTypeNames = SSTUDatabase.getHeatShieldNames();

                ConfigNode modelNode = new ConfigNode("MODEL");
                modelNode.AddValue("name", modelName);
                mainModelData = new SingleModelData(modelNode);
                mainModelData.setupModel(part.transform.FindRecursive("model"), ModelOrientation.CENTRAL, true);
                setModelDiameter(currentDiameter);
                updateAttachNodes(false);
                updateDragCube();
                updateEditorFields();
            }

            ConfigNode node = SSTUStockInterop.getPartModuleConfig(this);
            ConfigNode[] typeNodes = node.GetNodes("SHIELDTYPE");
            int len = typeNodes.Length;
            shieldTypeNames = new string[len];
            for (int i = 0; i < len; i++)
            {
                shieldTypeNames[i] = typeNodes[i].GetStringValue("name");
            }
            if (shieldTypeNames.Length == 0) { shieldTypeNames = new string[] { "Medium" }; }
            currentShieldTypeData = SSTUDatabase.getHeatShieldType(currentShieldType);
            heatCurve = currentShieldTypeData.heatCurve;

            updatePartCost();
            if (!initializedResources && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                updatePartResources();
                initializedResources = true;
            }
        }

        #endregion

        #region REGION - Tick Update Methods

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) { return; }
            guiShieldTemp = part.skinTemperature;
            guiShieldFlux = 0;
            guiShieldUse = 0;
            guiShieldEff = 0;
            part.skinInternalConductionMult = baseSkinIntMult;
            updateDebugGuiStatus();
            if (part.atmDensity <= 0) { return; }
            if (part.temperature > part.skinTemperature) { return; }

            Vector3 localFlightDirection = -part.dragVectorDirLocal;
            float dot = Vector3.Dot(heatShieldVector, localFlightDirection);
            if (dot < heatShieldMinDot) { return; }
            //TODO check for occlusion

            float directionalEffectiveness = 0;
            if (dot > heatShieldMaxDot)
            {
                directionalEffectiveness = 1f;
            }
            else
            {
                float minMaxDelta = heatShieldMaxDot - heatShieldMinDot;
                float offset = dot - heatShieldMinDot;
                directionalEffectiveness = offset / minMaxDelta;
            }
            guiShieldEff = directionalEffectiveness;
            float mult = (float)baseSkinIntMult * (1.0f - (0.8f * directionalEffectiveness));
            part.skinInternalConductionMult = mult;
            if (part.skinTemperature > ablationStartTemp)
            {
                double d = part.skinTemperature - ablationStartTemp;
                applyAblation(d, directionalEffectiveness);
            }
        }

        private void updateDebugGuiStatus()
        {            
            bool active = PhysicsGlobals.ThermalDataDisplay;
            Fields["guiShieldTemp"].guiActive = active;
            Fields["guiShieldFlux"].guiActive = active;
            Fields["guiShieldUse"].guiActive = active && !heatSoak;
            Fields["guiShieldEff"].guiActive = active;
            //Fields["fluxPerSquareMeter"].guiActive = active;
            //Fields["fluxPerTMass"].guiActive = active;
        }

        private void applyAblation(double tempDelta, float effectiveness)
        {
            double maxFluxRemoved = heatCurve.Evaluate((float)tempDelta) * fluxMult * effectiveness;
            //fluxPerSquareMeter = part.thermalConvectionFlux / part.skinExposedArea;
            //fluxPerTMass = part.thermalConvectionFlux / (part.skinThermalMass * part.skinExposedAreaFrac);
            if (areaAdjusted)
            {
                maxFluxRemoved *= part.skinExposedArea;
            }
            if (heatSoak)
            {
                part.AddExposedThermalFlux(-maxFluxRemoved);
                guiShieldFlux = maxFluxRemoved;
                guiShieldUse = 0;
            }
            else
            {
                double maxResourceUsed = maxFluxRemoved / useToFluxMultiplier;
                maxResourceUsed *= TimeWarp.fixedDeltaTime; //convert to a per-tick usage amount
                if (maxResourceUsed > resource.amount)
                {
                    maxResourceUsed = resource.amount;
                    maxFluxRemoved = maxResourceUsed * useToFluxMultiplier;
                }
                part.TransferResource(resource.info.id, -maxResourceUsed);
                part.AddExposedThermalFlux(-maxFluxRemoved);
                guiShieldFlux = maxFluxRemoved;
                guiShieldUse = maxResourceUsed;
            }
        }

        #endregion

        #region REGION - Internal Update Methods
        
        private void updateFairing(bool userInput)
        {
            SSTUNodeFairing fairing = part.GetComponent<SSTUNodeFairing>();
            if (fairing == null) { return; }
            fairing.canDisableInEditor = true;
            FairingUpdateData data = new FairingUpdateData();
            data.setTopY(mainModelData.currentHeight * 0.5f);
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
            float scale = mainModelData==null ? 1.0f : mainModelData.currentDiameterScale;
            float ablatMult = Mathf.Pow(scale, ablationScalePower) * currentShieldTypeData.ablationMult;
            ablationMult = ablatMult;
            heatCurve = currentShieldTypeData.heatCurve;
        }

        private void updatePartCost()
        {
            float scale = standAlonePart? Mathf.Pow(mainModelData.currentDiameterScale, massScalePower) : 1;
            if (heatSoak)
            {
                modifiedCost = 0;
            }
            else
            {
                PartResource res = part.Resources[resourceName];
                modifiedCost = ((float)res.maxAmount - baseResourceQuantity) * res.info.unitCost;//the shield cost currently is just the cost of the additional ablator resource
            }            
            modifiedMass = scale * shieldMass * currentShieldTypeData.massMult;
        }

        private void updatePartResources()
        {
            if (heatSoak) { return; }//dont touch resources on heat-soak type setups
            float scale = standAlonePart? Mathf.Pow(mainModelData.currentDiameterScale, resourceScalePower) : 1;
            float amount = baseResourceQuantity * scale * currentShieldTypeData.resourceMult;
            PartResource res = part.Resources[resourceName];
            if (res == null)
            {
                MonoBehaviour.print("SEVERE ERROR: ModularHeatShield could not set resource, as no resource was found in part for name: " + resourceName);
            }
            else
            {
                res.amount = res.maxAmount = amount;
            }
            SSTUModInterop.updatePartResourceDisplay(part);
        }

        private void updateAttachNodes(bool userInput)
        {
            if (!standAlonePart) { return; }
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

        #endregion

    }

    public class HeatShieldType
    {
        public readonly String name;
        public readonly String tech;
        public readonly float resourceMult = 1f;
        public readonly float ablationMult;
        public readonly float massMult = 1f;
        public readonly FloatCurve heatCurve;

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
