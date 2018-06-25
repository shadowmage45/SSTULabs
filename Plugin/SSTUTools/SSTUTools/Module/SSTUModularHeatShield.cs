using System;
using System.Collections.Generic;
using UnityEngine;
using KSPShaderTools;

namespace SSTUTools
{
    public class SSTUModularHeatShield : PartModule, IPartMassModifier, IPartCostModifier, IRecolorable, IContainerVolumeContributor
    {

        #region REGION - Base Heat Shield Parameters - used on both stand-alone and internally model-switched setups

        [KSPField]
        public string resourceName = "Ablator";

        [KSPField]
        public Vector3 heatShieldVector = Vector3.down;
        
        [KSPField]
        public float ablationStartTemp = 500f;

        [KSPField]
        public float ablationEndTemp = 2500f;
        
        [KSPField]
        public float heatShieldMinDot = 0.2f;

        [KSPField]
        public float heatShieldMaxDot = 0.8f;

        [KSPField]
        public float ablationEfficiency = 6000f;

        [KSPField]
        public bool heatSoak = false;

        [KSPField]
        public bool areaAdjusted = false;

        [KSPField]
        public FloatCurve heatCurve;

        /// <summary>
        /// Used to adjust the volume scaling in the model-module
        /// </summary>
        [KSPField]
        public float resourceScalePower = 2f;

        /// <summary>
        /// Adjusts the ablation rate based on model scale.  ablationMult = pow(scale, ablationScalePower)
        /// </summary>
        [KSPField]
        public float ablationScalePower = 0f;

        /// <summary>
        /// Determines the container index within VolumeContainer to use for resource.
        /// The actual resource is set within the VolumeContainer module config, but volume comes from this module based on model scale and heat shield type.
        /// </summary>
        [KSPField]
        public int containerIndex = 0;

        #endregion

        #region REGION - ModularHeatShield Model Manipulation Config Fields - used on stand-alone parts

        /// <summary>
        /// Determines if the module should trigger drag cube updates.
        /// </summary>
        [KSPField]
        public bool standAlonePart = false;

        [KSPField]
        public float diameterIncrement = 0.625f;

        [KSPField]
        public float minDiameter = 0.625f;

        [KSPField]
        public float maxDiameter = 10f;

        #endregion

        #region REGION - UI and persistent fields

        /// <summary>
        /// Persistent storage and UI selection of shield type.
        /// </summary>
        [KSPField(isPersistant = true, guiName = "Shield Type", guiActiveEditor = true, guiActive = false),
         UI_ChooseOption(options = new string[] { "Light", "Medium", "Heavy", "ExtraHeavy" }, suppressEditorShipModified = true)]
        public String currentShieldType = "Medium";

        /// <summary>
        /// Persistent storage and UI selection of model diameter
        /// </summary>
        [KSPField(isPersistant = true, guiName = "Diameter", guiActiveEditor = true),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentDiameter = 1.25f;

        /// <summary>
        /// Persistent storage and UI selection of model
        /// </summary>
        [KSPField(isPersistant = true, guiName = "Shield Model", guiActiveEditor = true, guiActive = false),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentShieldModel = string.Empty;

        /// <summary>
        /// Persistent storage and UI selection of texture set.
        /// </summary>
        [KSPField(isPersistant = true, guiName = "Shield Texture", guiActiveEditor = true, guiActive = false),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentShieldTexture = string.Empty;

        /// <summary>
        /// Persistent storage for recoloring data.
        /// </summary>
        [KSPField(isPersistant = true)]
        public string modelPersistentData = string.Empty;

        [Persistent]
        public string configNodeData = string.Empty;

        #endregion

        #region REGION - Gui Display fields

        [KSPField(guiActive =true, guiName ="HS Flux")]
        public double guiShieldFlux = 0;
        [KSPField(guiActive = true, guiName = "HS Use", guiUnits ="%")]
        public double guiShieldUse = 0;
        [KSPField(guiActive = true, guiName = "HS Temp", guiUnits ="k")]
        public double guiShieldTemp = 0;
        [KSPField(guiActive = true, guiName = "HS Eff", guiUnits ="%")]
        public double guiShieldEff = 0;

        #endregion

        #region REGION - private working variables

        private double ablationMult = 1f;

        //cached vars for heat-shield thermal processing
        private double baseSkinIntMult = 1;
        private double baseCondMult = 1;

        /// <summary>
        /// Pre-calculated value that can be used to convert between flux and resource units.  Each unit of resource will remove this much flux over a duration of 1s.  So... kW?<para />
        /// flux = resourceUnits * useToFluxMultiplier / TimeWarp.fixedDeltaTime;<para/>
        /// units = (flux / useToFluxMultiplier) * TimeWarp.fixedDeltaTime;
        /// </summary>
        private double fluxPerResourceUnit = 1;

        private PartResource resource;

        //modular heat-shield fields, for updating shield type
        private HeatShieldTypeData[] shieldTypeData;
        private HeatShieldTypeData currentShieldTypeData;
        private float modifiedCost;
        private float modifiedMass;

        //resizable heat-shield fields
        private ModelModule<SSTUModularHeatShield> model;

        private bool initialized = false;

        #endregion

        #region REGION - Standard KSP Overrides

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
            string[] options = SSTUUtils.getNames(shieldTypeData, m => m.baseType.name);
            this.updateUIChooseOptionControl(nameof(currentShieldType), options, options, true, currentShieldType);
            this.updateUIFloatEditControl(nameof(currentDiameter), minDiameter, maxDiameter, diameterIncrement * 2f, diameterIncrement, diameterIncrement * 0.05f, true, currentDiameter);
            Fields[nameof(currentShieldType)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                this.actionWithSymmetry(m => 
                {
                    if (m != this) { m.currentShieldType = currentShieldType; }
                    m.currentShieldTypeData = Array.Find(m.shieldTypeData, s => s.baseType.name == m.currentShieldType);
                    m.updateModuleStats();
                    m.updatePartCost();
                    SSTUModInterop.updateResourceVolume(part);
                });
            };

            //UI functions for stand-alone part
            Fields[nameof(currentDiameter)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m => 
                {
                    if (m != this) { m.currentDiameter = currentDiameter; }
                    m.model.setScaleForDiameter(currentDiameter);
                    m.model.setPosition(0);
                    m.model.updateModelMeshes();
                    m.updateFairing(true);
                    m.updateModuleStats();
                    m.updatePartCost();
                    m.updateAttachNodes(true);
                    m.updateDragCube();
                    SSTUModInterop.updateResourceVolume(part);
                });
            };
            Fields[nameof(currentShieldModel)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                model.modelSelected(a, b);
                this.actionWithSymmetry(m =>
                {
                    m.model.setScaleForDiameter(currentDiameter);
                    m.model.setPosition(0);
                    m.model.updateModelMeshes();
                    m.updateFairing(true);
                    m.updateModuleStats();
                    m.updatePartCost();
                    m.updateAttachNodes(true);
                    m.updateDragCube();
                    SSTUModInterop.updateResourceVolume(part);
                });
            };
            Fields[nameof(currentShieldTexture)].uiControlEditor.onFieldChanged = model.textureSetSelected;
            Fields[nameof(currentDiameter)].guiActiveEditor = standAlonePart;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
        }

        public void Start()
        {
            updatePartCost();
            updateFairing(false);
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return standAlonePart ? -defaultMass + modifiedMass : modifiedMass; }
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) { return standAlonePart ? -defaultCost + modifiedCost : modifiedCost; }
        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        public string[] getSectionNames()
        {
            return new string[] { "HeatShield" };
        }

        public RecoloringData[] getSectionColors(string name)
        {
            return model == null ? null : model.recoloringData;
        }

        public TextureSet getSectionTexture(string name)
        {
            return model == null? null : model.textureSet;
        }

        public void setSectionColors(string name, RecoloringData[] colors)
        {
            model.setSectionColors(colors);
        }

        public ContainerContribution[] getContainerContributions()
        {
            float volume = model==null? 0 : model.moduleVolume * 1000f;//raw volume from model definition, adjusted for scale
            if (currentShieldTypeData != null)
            {
                volume *= currentShieldTypeData.resourceMult;
            }
            return new ContainerContribution[] { new ContainerContribution("mhs", containerIndex, volume) };
        }

        #endregion

        #region REGION - Init and setup

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            double hsp = 1;
            double dens = 1;

            PartResourceDefinition resource = PartResourceLibrary.Instance.GetDefinition(resourceName);
            hsp = resource.specificHeatCapacity;
            dens = resource.density;

            fluxPerResourceUnit = hsp * ablationEfficiency * dens;
            baseSkinIntMult = part.skinInternalConductionMult;
            baseCondMult = part.heatConductivity;

            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);

            Transform mhsRoot = part.transform.FindRecursive("model").FindOrCreate("SSTU-MHS-Root");
            ConfigNode[] modelNodes = node.GetNodes("MODELS");
            ModelDefinitionLayoutOptions[] models = SSTUModelData.getModelDefinitions(modelNodes);
            model = new ModelModule<SSTUModularHeatShield>(part, this, mhsRoot, ModelOrientation.CENTRAL, nameof(currentShieldModel), null, nameof(currentShieldTexture), nameof(modelPersistentData), null, null, null, null);
            model.getSymmetryModule = (m) => m.model;
            model.setupModelList(models);
            model.setupModel();
            model.setScaleForDiameter(currentDiameter);
            model.setPosition(0);
            model.updateModelMeshes();
            model.updateSelections();
            model.volumeScalar = resourceScalePower;
            model.massScalar = resourceScalePower;
            if (standAlonePart)
            {
                updateDragCube();
                updateAttachNodes(false);
            }
            SSTUModInterop.updateResourceVolume(part);
            ConfigNode[] typeNodes = node.GetNodes("SHIELDTYPE");
            shieldTypeData = HeatShieldTypeData.load(typeNodes);
            currentShieldTypeData = Array.Find(shieldTypeData, m => m.baseType.name == currentShieldType);
            updateModuleStats();
            updatePartCost();
            SSTUModInterop.onPartGeometryUpdate(part, false);
            SSTUModInterop.updateResourceVolume(part);
            SSTUStockInterop.fireEditorUpdate();//update for mass/cost/etc.
        }

        #endregion

        #region REGION - Tick Update Methods

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) { return; }
            double skinTemp = part.skinTemperature;
            guiShieldTemp = skinTemp;
            guiShieldFlux = 0;
            guiShieldUse = 0;
            guiShieldEff = 0;
            part.skinInternalConductionMult = baseSkinIntMult;
            part.heatConductivity = baseCondMult;
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
            float mult =  (1.0f - (0.8f * directionalEffectiveness));
            part.skinInternalConductionMult = mult * baseSkinIntMult;
            part.heatConductivity = mult * baseCondMult;
            if (skinTemp > ablationStartTemp)
            {
                //convert input value to 0-1 domain
                double d = skinTemp - ablationStartTemp;
                d /= ablationEndTemp;
                d = UtilMath.Clamp(d, 0, 1);
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
        }

        private void applyAblation(double tempDelta, float effectiveness)
        {
            double maxFluxRemoved = heatCurve.Evaluate((float)tempDelta);
            maxFluxRemoved = UtilMath.Clamp(maxFluxRemoved, 0, 1);
            //guiDebugPeakFlux = Math.Max(guiDebugPeakFlux, part.thermalConvectionFlux);
            //guiDebugPeakHeat = Math.Max(guiDebugPeakHeat, part.skinTemperature);
            //guiDebugPeakG = Math.Max(guiDebugPeakG, vessel.geeForce);
            //guiDebugFluxPerSqMeter = part.thermalConvectionFlux / part.skinExposedArea;
            //guiDebugMassPerSqMeter = vessel.GetTotalMass() / part.skinExposedArea;
            //guiDebugEfficiency = ablationEfficiency;
            //guiDebugAblMult = ablationMult;
            //guiDebugHCInput = tempDelta;
            //guiDebugHCOutput = maxFluxRemoved;
                        
            maxFluxRemoved *= effectiveness * ablationMult;
            if (areaAdjusted)
            {
                maxFluxRemoved *= part.skinExposedArea;
            }
            //if (PhysicsGlobals.ThermalDataDisplay)
            //{
                //double pTemp = part.skinTemperature;
                //double ext = vessel.externalTemperature;
                //double shk = part.ptd.postShockExtTemp;
                //double atm = vessel.atmosphericTemperature;
                //double alt = vessel.altitude;
                //double dens = vessel.atmDensity;
                //double vel = vessel.velocityD.magnitude;
                //double mach = vessel.mach;
                //double dyn = vessel.dynamicPressurekPa;
                //double pExp = part.skinExposedArea;
                //double convFlux = part.thermalConvectionFlux;
                //double condFlux = part.thermalConductionFlux;
                //double radFlux = part.ptd.radiationFlux;                
                //double tFluxPrev = part.thermalExposedFluxPrevious;
                //double hsFlux = maxFluxRemoved;
                //double skin = part.ptd.skinConductionFlux;
                //double skinin = part.ptd.skinInteralConductionFlux;
                //double skinskin = part.ptd.skinSkinConductionFlux;

                //string fString = "MHSDebug | tmp: {0,6:0000.00} | ext: {1,6:0000.00} | shk: {2,6:0000.00} | atm: {3,6:0000.00} | alt: {4,6:00000.0} | dns: {5,6:00.0000}"+
                //    " | vel: {6,6:0000.00} | mch: {7,6:0000.00} | dyn: {8,6:00.0000} | exp: {9,6:00.0000} | cnv: {10,6:0000.00} | cnd: {11,6:0000.00} | rad: {12,6:0000.00}"+
                //    " | prv: {13,6:0000.00} | hsf: {14,6:0000.00} | skn: {15,6:0000.00} | snn: {16,6:0000.00} | skk: {17,6:0000.00} |";
                //MonoBehaviour.print(string.Format(fString, pTemp, ext, shk, atm, alt, dens, vel, mach, dyn, pExp, convFlux, condFlux, radFlux, tFluxPrev, hsFlux, skin, skinin, skinskin));
            //}
            if (heatSoak)
            {
                part.AddExposedThermalFlux(-maxFluxRemoved);
                guiShieldFlux = maxFluxRemoved;
                guiShieldUse = 0;
            }
            else
            {
                if (resource == null)
                {
                    resource = part.Resources[resourceName];
                }
                double maxResourceUsed = maxFluxRemoved / (fluxPerResourceUnit * currentShieldTypeData.efficiencyMult);
                maxResourceUsed *= TimeWarp.fixedDeltaTime; //convert to a per-tick usage amount
                if (maxResourceUsed > resource.amount)//didn't have enough ablator for the full tick, calculate partial use
                {
                    maxResourceUsed = resource.amount;//use all of the ablator
                    //re-calculate the flux-removed from the ablator used
                    //as ablator use in in 'per-tick' quantities, need to re-convert out to per-second
                    maxFluxRemoved = maxResourceUsed * (fluxPerResourceUnit * currentShieldTypeData.efficiencyMult) / TimeWarp.fixedDeltaTime;
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
            if (!standAlonePart) { return; }
            SSTUNodeFairing fairing = part.GetComponent<SSTUNodeFairing>();
            if (fairing == null) { return; }
            fairing.canDisableInEditor = true;
            FairingUpdateData data = new FairingUpdateData();
            data.setTopY(model.fairingTop);
            //bottom-y set by 'snap to node' functionality in nodefairing
            data.setTopRadius(currentDiameter * 0.5f);
            if (userInput)
            {
                data.setBottomRadius(currentDiameter * 0.5f);
            }
            data.setEnable(true);
            fairing.updateExternal(data);
        }

        private void updateModuleStats()
        {
            float scale = model.moduleHorizontalScale;
            float ablatMult = Mathf.Pow(scale, ablationScalePower) * currentShieldTypeData.ablationMult;
            ablationMult = ablatMult;
            ablationStartTemp = currentShieldTypeData.ablationStart;
            ablationEndTemp = currentShieldTypeData.ablationEnd;
            heatCurve = currentShieldTypeData.heatCurve;
        }

        private void updatePartCost()
        {
            modifiedMass = model.moduleMass * currentShieldTypeData.massMult;
            modifiedCost = model.moduleCost;
        }

        private void updateAttachNodes(bool userInput)
        {
            if (!standAlonePart) { return; }
            float height = model.moduleHeight;
            model.updateAttachNodeTop("top", userInput);
            model.updateAttachNodeBottom("bottom", userInput);
        }

        private void updateDragCube()
        {
            SSTUModInterop.onPartGeometryUpdate(part, true);
            SSTUStockInterop.fireEditorUpdate();
        }

        #endregion

    }

    public class HeatShieldTypeData
    {
        public readonly HeatShieldType baseType;
        public readonly float resourceMult = 1f;
        public readonly float ablationStart;
        public readonly float ablationEnd;
        public readonly float ablationMult;
        public readonly float massMult = 1f;
        public readonly FloatCurve heatCurve;
        public readonly float efficiencyMult;

        public HeatShieldTypeData(ConfigNode node)
        {
            string typeName = node.GetValue("name");
            baseType = SSTUDatabase.getHeatShieldType(typeName);
            resourceMult = node.GetFloatValue("resourceMult", baseType.resourceMult);
            ablationStart = node.GetFloatValue("ablationStart", baseType.ablationStart);
            ablationEnd = node.GetFloatValue("ablationEnd", baseType.ablationEnd);
            ablationMult = node.GetFloatValue("ablationMult", baseType.ablationMult);
            massMult = node.GetFloatValue("massMult", baseType.massMult);
            heatCurve = node.GetFloatCurve("heatCurve", baseType.heatCurve);
            efficiencyMult = node.GetFloatValue("efficiencyMult", 1.0f);
        }

        public static HeatShieldTypeData[] load(ConfigNode[] nodes)
        {
            int len = nodes.Length;
            HeatShieldTypeData[] data = new HeatShieldTypeData[len];
            for (int i = 0; i < len; i++)
            {
                data[i] = new HeatShieldTypeData(nodes[i]);
            }
            return data;
        }
    }

    public class HeatShieldType
    {
        public readonly String name;
        public readonly float resourceMult = 1f;
        public readonly float ablationStart = 500f;
        public readonly float ablationEnd = 2500f;
        public readonly float ablationMult;
        public readonly float massMult = 1f;
        public readonly FloatCurve heatCurve;

        public HeatShieldType(ConfigNode node)
        {
            name = node.GetStringValue("name");
            if (node.HasNode("heatCurve"))
            {
                heatCurve = node.GetFloatCurve("heatCurve");
            }
            else
            {
                heatCurve = new FloatCurve();
                heatCurve.Add(0.000f, 0.0000000f, 0.00f, 0.00f);
                heatCurve.Add(0.155f, 0.0166667f, 0.08f, 0.08f);
                heatCurve.Add(0.175f, 0.0444444f);
                heatCurve.Add(0.265f, 0.8333333f);
                heatCurve.Add(0.295f, 0.8888889f, 0.12f, 0.12f);
                heatCurve.Add(1.000f, 1.0000000f, 0.00f, 0.00f);
            }
            resourceMult = node.GetFloatValue("resourceMult", resourceMult);
            ablationStart = node.GetFloatValue("ablationStart", ablationStart);
            ablationEnd = node.GetFloatValue("ablationEnd", ablationEnd);
            ablationMult = node.GetFloatValue("ablationMult", ablationMult);
            massMult = node.GetFloatValue("massMult", massMult);
        }
    }

}
