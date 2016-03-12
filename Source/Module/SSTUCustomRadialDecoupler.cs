using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools.Module
{
    class SSTUCustomRadialDecoupler : PartModule
    {

        [KSPField(isPersistant = true, guiName = "Height", guiActiveEditor = true)]
        public float height = 2f;

        [KSPField(isPersistant = true, guiName = "Diameter", guiActiveEditor = true)]
        public float diameter = 1.25f;

        [KSPField]
        public float heightIncrement = 1f;

        [KSPField]
        public float diameterIncrement = 0.625f;

        [KSPField(guiActiveEditor = true, guiName = "Height Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.05f, maxValue = 0.95f)]
        public float editorHeightExtra;

        [KSPField(guiActiveEditor = true, guiName = "Diameter Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.05f, maxValue = 0.95f)]
        public float editorDiameterAdjust;

        [KSPField(guiName ="Raw Thrust", guiActive =true, guiActiveEditor =true)]
        public float guiEngineThrust = 0f;

        /// <summary>
        /// If true, resource updates will be sent to the RealFuels/ModularFuelTanks ModuleFuelTanks module, if present (if not present, it will not update anything).
        /// </summary>
        [KSPField]
        public bool useRF = false;

        //this is used to determine actual resultant scale from input for radius
        //should match the model default scale geometry being used...
        [KSPField]
        public float modelDiameter = 2.5f;

        /// <summary>
        /// The volume of resources that the part contains at its default model scale (e.g. modelRadius listed above)
        /// </summary>
        [KSPField]
        public float resourceVolume = 0.125f;

        /// <summary>
        /// The thrust of the engine module at default model scale
        /// </summary>
        [KSPField]
        public float engineThrust = 600f;

        /// <summary>
        /// Should thrust scale on square, cube, or some other power?  Default is cubic to match the fuel quantity
        /// </summary>
        [KSPField]
        public float thrustScalePower = 2;

        [KSPField]
        public float minHeight = 0.5f;

        [KSPField]
        public float maxHeight = 100f;

        [KSPField]
        public float minDiameter = 0.625f;

        [KSPField]
        public float maxDiameter = 10f;

        [KSPField]
        public String topMountName = "SC-RBDC-MountUpper";

        [KSPField]
        public String bottomMountName = "SC-RBDC-MountLower";

        [KSPField(isPersistant = true)]
        public bool initializedResources = false;
        
        [KSPField]
        public String techLimitSet = "Default";

        private float editorHeight;
        private float editorDiameter;
        private float lastHeightExtra;
        private float lastRadiusExtra;

        private Transform topMountTransform;
        private Transform bottomMountTransform;
        
        private FuelTypeData fuelType;

        // tech limit values are updated every time the part is initialized in the editor; ignored otherwise
        private float techLimitMaxDiameter;

        [KSPEvent(guiName = "Height-", guiActiveEditor = true)]
        public void prevHeightEvent()
        {
            setHeightFromEditor(height - heightIncrement, true);
        }

        [KSPEvent(guiName = "Height+", guiActiveEditor = true)]
        public void nextHeightEvent()
        {
            setHeightFromEditor(height + heightIncrement, true);
        }

        [KSPEvent(guiName = "Diameter ++", guiActiveEditor = true)]
        public void nextDiameterEvent()
        {
            setDiameterFromEditor(diameter + diameterIncrement, true);
        }

        [KSPEvent(guiName = "Diameter --", guiActiveEditor = true)]
        public void prevRadiusEvent()
        {
            setDiameterFromEditor(diameter - diameterIncrement, true);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);
            TechLimit.updateTechLimits(techLimitSet, out techLimitMaxDiameter);
            if (diameter > techLimitMaxDiameter) { diameter = techLimitMaxDiameter; }
            
            fuelType = new FuelTypeData(node.GetNode("FUELTYPE"));

            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            locateTransforms();
            updateModelPositions();
            updateModelScales();

            if (!initializedResources && (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
            {
                initializedResources = true;
                updatePartResources();
            }
            updateEngineThrust();

            updateEditorFields();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasValue("radius")) { diameter = node.GetFloatValue("radius") * 2f; }
        }

        public void Start()
        {
            updateEngineThrust();
        }

        public override string GetInfo()
        {
            return "This part has configurable diameter, height, and ejection force.  Includes separation motors for the attached payload.  Motor thrust and resource volume scale with part size.";
        }

        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
        }

        public void onEditorVesselModified(ShipConstruct ship)
        {
            if (!HighLogic.LoadedSceneIsEditor) { return; }
            if (editorDiameterAdjust != lastRadiusExtra)
            {
                float newDiameter = editorDiameter + (editorDiameterAdjust * diameterIncrement);
                setDiameterFromEditor(newDiameter, true);
            }
            if (editorHeightExtra != lastHeightExtra)
            {
                float newHeight = editorHeight + (editorHeightExtra * heightIncrement);
                setHeightFromEditor(newHeight, true);
            }
        }

        private void setHeightFromEditor(float newHeight, bool updateSymmetry)
        {
            if (newHeight > maxHeight) { newHeight = maxHeight; }
            if (newHeight < minHeight) { newHeight = minHeight; }
            height = newHeight;
            updateEditorFields();
            updateModule();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomRadialDecoupler>().setHeightFromEditor(newHeight, false);
                }
            }
        }

        private void setDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {
            if (newDiameter > maxDiameter) { newDiameter = maxDiameter; }
            if (newDiameter < minDiameter) { newDiameter = minDiameter; }
            if (newDiameter > techLimitMaxDiameter) { newDiameter = techLimitMaxDiameter; }
            diameter = newDiameter;
            updateEditorFields();
            updatePartResources();
            updateEngineThrust();
            updateModule();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomRadialDecoupler>().setDiameterFromEditor(newDiameter, false);
                }
            }
        }

        private void updateTransformYPos(Transform t, float pos)
        {            
            Vector3 partSpacePosition = part.transform.InverseTransformPoint(t.position);
            partSpacePosition.y = pos;
            t.position = part.transform.TransformPoint(partSpacePosition);
        }

        private void setModelScale(Transform t, float scale)
        {
            t.localScale = new Vector3(scale, scale, scale); ;
        }

        //restores the editor field values for radius/height
        private void updateEditorFields()
        {
            float div, whole, extra;

            div = diameter / diameterIncrement;
            whole = (int)div;
            extra = div - whole;
            editorDiameter = whole * diameterIncrement;
            editorDiameterAdjust = extra;
            lastRadiusExtra = editorDiameterAdjust;

            div = height / heightIncrement;
            whole = (int)div;
            extra = div - whole;
            editorHeight = whole * heightIncrement;
            editorHeightExtra = extra;
            lastHeightExtra = editorHeightExtra;
        }

        private void updateModule()
        {
            updateModelPositions();
            updateModelScales();
        }

        private void updateModelPositions()
        {
            float half = height * 0.5f;
            updateTransformYPos(topMountTransform, half);
            updateTransformYPos(bottomMountTransform, -half);
        }

        private void updateModelScales()
        {
            float scale = getScale();
            setModelScale(topMountTransform, scale);
            setModelScale(bottomMountTransform, scale);
        }

        private float getScale()
        {
            return diameter / modelDiameter;
        }

        private void locateTransforms()
        {
            topMountTransform = part.FindModelTransform(topMountName);
            bottomMountTransform = part.FindModelTransform(bottomMountName);
        }

        private void updateEngineThrust()
        {
            float currentScale = getCurrentModelScale();
            float thrustScalar = Mathf.Pow(currentScale, thrustScalePower);
            float maxThrust = engineThrust * thrustScalar;
            guiEngineThrust = maxThrust;
            ConfigNode updateNode = new ConfigNode("MODULE");
            updateNode.AddValue("maxThrust", maxThrust);
            ModuleEngines[] engines = part.GetComponents<ModuleEngines>();
            foreach (ModuleEngines engine in engines)
            {
                engine.maxThrust = maxThrust;
                engine.Load(updateNode);
            }
        }

        private void updatePartResources()
        {
            float resourceScalar = Mathf.Pow(getCurrentModelScale(), thrustScalePower);
            float currentVolume = resourceVolume * resourceScalar;
            if (useRF)
            {
                SSTUModInterop.onPartFuelVolumeUpdate(part, currentVolume);
            }
            else
            {
                SSTUResourceList res = fuelType.getResourceList(currentVolume);
                res.setResourcesToPart(part, HighLogic.LoadedSceneIsEditor);
            }
        }

        private float getCurrentModelScale()
        {
            return diameter / modelDiameter;
        }
    }

}
