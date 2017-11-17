using System;
using System.Collections.Generic;
using UnityEngine;
using KSPShaderTools;

namespace SSTUTools
{
    public class SSTUModularHeatShield : PartModule, IPartMassModifier, IPartCostModifier, IRecolorable
    {
        #region REGION - Base Heat Shield Parameters
        [KSPField]
        public String resourceName = "Ablator";

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
        public float tempSmoothing = 0.5f;

        [KSPField]
        public bool heatSoak = false;

        [KSPField]
        public bool areaAdjusted = false;

        [KSPField]
        public FloatCurve heatCurve;

        [KSPField]
        public float shieldMass = 0.25f;

        #endregion

        #region REGION - ModularHeatShield resizing data

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
        public bool standAlonePart = false;

        #endregion

        #region REGION - persistent fields

        [KSPField(isPersistant = true, guiName = "Shield Type", guiActiveEditor = true, guiActive = false),
         UI_ChooseOption(options = new string[] { "Light", "Medium", "Heavy", "ExtraHeavy" }, suppressEditorShipModified = true)]
        public String currentShieldType = "Medium";

        [KSPField(isPersistant = true, guiName = "Diameter", guiActiveEditor = true),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentDiameter = 1.25f;

        [KSPField(isPersistant = true, guiName = "Shield Model", guiActiveEditor = true, guiActive = false),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentShieldModel = string.Empty;

        [KSPField(isPersistant = true, guiName = "Shield Texture", guiActiveEditor = true, guiActive = false),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentShieldTexture = string.Empty;

        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        [KSPField(isPersistant = true)]
        public string modelPersistentData = string.Empty;

        [Persistent]
        public string configNodeData = string.Empty;

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
        private ModelModule<SingleModelData, SSTUModularHeatShield> model;

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
            this.Fields[nameof(currentShieldType)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b) 
            {
                this.actionWithSymmetry(m => 
                {
                    if (m != this) { m.currentShieldType = currentShieldType; }
                    m.currentShieldTypeData = Array.Find(m.shieldTypeData, s => s.baseType.name == m.currentShieldType);
                    m.updateModuleStats();
                    m.updatePartResources();
                    m.updatePartCost();
                });
            };
            this.Fields[nameof(currentDiameter)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m => 
                {
                    if (m != this) { m.currentDiameter = currentDiameter; }
                    m.model.model.updateScaleForDiameter(currentDiameter);
                    m.model.model.setPosition(0, ModelOrientation.CENTRAL);
                    m.model.updateModel();
                    m.updateFairing(true);
                    m.updateModuleStats();
                    m.updatePartResources();
                    m.updatePartCost();
                    m.updateAttachNodes(true);
                    m.updateDragCube();
                });
            };
            this.Fields[nameof(currentDiameter)].guiActiveEditor = standAlonePart;
            this.Fields[nameof(currentShieldModel)].guiActiveEditor = false;
            this.Fields[nameof(currentShieldTexture)].guiActiveEditor = standAlonePart && model.model.modelDefinition.textureSets.Length > 1;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
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

        public string[] getSectionNames()
        {
            return new string[] { "HeatShield" };
        }

        public RecoloringData[] getSectionColors(string name)
        {
            return model==null ? null : model.customColors;
        }

        public TextureSet getSectionTexture(string name)
        {
            return model==null? null : model.currentTextureSet;
        }

        public void setSectionColors(string name, RecoloringData[] colors)
        {
            if (model != null) { model.setSectionColors(colors)};
        }

        #endregion

        #region REGION - Init and setup

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
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
            fluxPerResourceUnit = hsp * ablationEfficiency * dens;
            baseSkinIntMult = part.skinInternalConductionMult;
            baseCondMult = part.heatConductivity;

            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);

            //stand-alone modular heat-shield setup
            if (standAlonePart)
            {
                ConfigNode[] modelNodes = node.GetNodes("MODEL");
                model = new ModelModule<SingleModelData, SSTUModularHeatShield>(part, this, part.transform.FindRecursive("model"), ModelOrientation.CENTRAL, nameof(modelPersistentData), nameof(currentShieldModel), nameof(currentShieldTexture));                
                model.setupModelList(SingleModelData.parseModels(modelNodes));
                model.setupModel();
                model.model.updateScaleForDiameter(currentDiameter);
                model.setPosition(0, ModelOrientation.CENTRAL);
                model.model.updateModel();
                updateAttachNodes(false);
                updateDragCube();
            }

            ConfigNode[] typeNodes = node.GetNodes("SHIELDTYPE");
            shieldTypeData = HeatShieldTypeData.load(typeNodes);
            currentShieldTypeData = Array.Find(shieldTypeData, m => m.baseType.name == currentShieldType);
            updateModuleStats();

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
            SSTUNodeFairing fairing = part.GetComponent<SSTUNodeFairing>();
            if (fairing == null) { return; }
            fairing.canDisableInEditor = true;
            FairingUpdateData data = new FairingUpdateData();
            data.setTopY(model.model.currentHeight * 0.5f + model.model.getFairingOffset());
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
            float scale = getScale();
            float ablatMult = Mathf.Pow(scale, ablationScalePower) * currentShieldTypeData.ablationMult;
            ablationMult = ablatMult;
            ablationStartTemp = currentShieldTypeData.ablationStart;
            ablationEndTemp = currentShieldTypeData.ablationEnd;
            heatCurve = currentShieldTypeData.heatCurve;
        }

        private void updatePartCost()
        {
            float scale = Mathf.Pow(getScale(), massScalePower);
            if (heatSoak)
            {
                modifiedCost = 0;
            }
            else
            {
                PartResource res = part.Resources[resourceName];
                modifiedCost = ((float)res.maxAmount - baseResourceQuantity) * res.info.unitCost;//the shield cost currently is just the cost of the additional ablator resource
                modifiedCost += scale * (part.partInfo == null ? 0 : part.partInfo.cost);
            }
            modifiedMass = scale * shieldMass * currentShieldTypeData.massMult;
        }

        private void updatePartResources()
        {
            if (heatSoak) { return; }//dont touch resources on heat-soak type setups
            float scale = Mathf.Pow(getScale(), resourceScalePower);
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
            float height = model.model.currentHeight;
            AttachNode topNode = part.FindAttachNode("top");
            if (topNode != null)
            {
                Vector3 topNodePos = new Vector3(0, height * 0.5f, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, topNode, topNodePos, topNode.orientation, userInput);
            }
            AttachNode bottomNode = part.FindAttachNode("bottom");
            if (bottomNode != null)
            {
                Vector3 botNodePos = new Vector3(0, -height * 0.5f, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, bottomNode, botNodePos, bottomNode.orientation, userInput);
            }
        }

        private void updateDragCube()
        {
            SSTUModInterop.onPartGeometryUpdate(part, true);
            SSTUStockInterop.fireEditorUpdate();
        }

        private float getScale()
        {
            return model == null ? 1.0f : model.model.currentDiameterScale;
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
