using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools.Module
{
    class SSTUCustomRadialDecoupler : ModuleAnchoredDecoupler
    {

        [KSPField(isPersistant = true)]
        public float height = 2f;

        [KSPField(isPersistant = true)]
        public float radius = 1.25f;

        [KSPField]
        public float heightIncrement = 1f;

        [KSPField]
        public float radiusIncrement = 0.625f;

        [KSPField(guiActiveEditor = true, guiName = "Height Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.05f, maxValue = 0.95f)]
        public float editorHeightExtra;

        [KSPField(guiActiveEditor = true, guiName = "Radius Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.05f, maxValue = 0.95f)]
        public float editorRadiusExtra;

        //this is used to determine actual resultant scale from input for radius
        //should match the model default scale geometry being used...
        [KSPField]
        public float modelRadius = 1.25f;

        [KSPField]
        public float minHeight = 0.5f;

        [KSPField]
        public float maxHeight = 100f;

        [KSPField]
        public float minRadius = 0.625f;

        [KSPField]
        public float maxRadius = 12.5f;

        public float editorHeight;

        public float editorRadius;

        public float lastHeightExtra;

        public float lastRadiusExtra;

        [KSPField]
        public String topMountName = "SC-RBDC-MountUpper";

        [KSPField]
        public String bottomMountName = "SC-RBDC-MountLower";

        private Transform topMountTransform;

        private Transform bottomMountTransform;

        [Persistent]
        public String configNodeData = String.Empty;

        private TechLimitHeightDiameter[] techLimits;
        // tech limit values are updated every time the part is initialized in the editor; ignored otherwise
        private float techLimitMaxHeight;
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

        [KSPEvent(guiName = "Size-", guiActiveEditor = true)]
        public void prevRadiusEvent()
        {
            setRadiusFromEditor(radius - radiusIncrement, true);
        }

        [KSPEvent(guiName = "Size+", guiActiveEditor = true)]
        public void nextRadiusEvent()
        {
            setRadiusFromEditor(radius + radiusIncrement, true);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            ConfigNode node = SSTUNodeUtils.parseConfigNode(configNodeData);
            ConfigNode[] limitNodes = node.GetNodes("TECHLIMIT");
            int len = limitNodes.Length;
            techLimits = new TechLimitHeightDiameter[len];
            for (int i = 0; i < len; i++) { techLimits[i] = new TechLimitHeightDiameter(limitNodes[i]); }

            updateTechLimits();
            if (radius * 2 > techLimitMaxDiameter) { radius = techLimitMaxDiameter * 0.5f; }
            if (height > techLimitMaxHeight) { height = techLimitMaxHeight; }

            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            locateTransforms();
            updateModelPositions();
            updateModelScales();
            restoreEditorFields();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
            {
                configNodeData = node.ToString();
            }
        }

        public override string GetInfo()
        {
            return "This part has configurable diameter, height, and ejection force.  Includes separation motors for the attached payload.";
        }

        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
        }

        public void onEditorVesselModified(ShipConstruct ship)
        {
            if (!HighLogic.LoadedSceneIsEditor) { return; }
            if (editorRadiusExtra != lastRadiusExtra)
            {
                float newRadius = editorRadius + (editorRadiusExtra * radiusIncrement);
                setRadiusFromEditor(newRadius, true);
            }
            if (editorHeightExtra != lastHeightExtra)
            {
                float newHeight = editorHeight + (editorHeightExtra * heightIncrement);
                setHeightFromEditor(newHeight, true);
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
        private void restoreEditorFields()
        {
            float div, whole, extra;

            div = radius / radiusIncrement;
            whole = (int)div;
            extra = div - whole;
            editorRadius = whole * radiusIncrement;
            editorRadiusExtra = extra;
            lastRadiusExtra = editorRadiusExtra;

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
            return radius / modelRadius;
        }

        private void locateTransforms()
        {
            topMountTransform = part.FindModelTransform(topMountName);
            bottomMountTransform = part.FindModelTransform(bottomMountName);
        }

        private void setHeightFromEditor(float newHeight, bool updateSymmetry)
        {
            if (newHeight > maxHeight) { newHeight = maxHeight; }
            if (newHeight < minHeight) { newHeight = minHeight; }
            if (SSTUUtils.isResearchGame() && newHeight > techLimitMaxHeight) { newHeight = techLimitMaxHeight; }
            height = newHeight;
            restoreEditorFields();
            updateModule();
            if (updateSymmetry)
            {
                foreach(Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomRadialDecoupler>().setHeightFromEditor(newHeight, false);
                }
            }
        }

        private void setRadiusFromEditor(float newRadius, bool updateSymmetry)
        {
            if (newRadius > maxRadius) { newRadius = maxRadius; }
            if (newRadius < minRadius) { newRadius = minRadius; }
            if (SSTUUtils.isResearchGame() && newRadius * 2 > techLimitMaxDiameter) { newRadius = techLimitMaxDiameter * 0.5f; }
            radius = newRadius;
            restoreEditorFields();
            updateModule();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomRadialDecoupler>().setRadiusFromEditor(newRadius, false);
                }
            }
        }

        /// <summary>
        /// Update the tech limitations for this part
        /// </summary>
        private void updateTechLimits()
        {
            techLimitMaxDiameter = float.PositiveInfinity;
            techLimitMaxHeight = float.PositiveInfinity;
            if (!SSTUUtils.isResearchGame()) { return; }
            if (HighLogic.CurrentGame == null) { return; }
            techLimitMaxDiameter = 0;
            techLimitMaxHeight = 0;
            foreach (TechLimitHeightDiameter limit in techLimits)
            {
                if (limit.isUnlocked())
                {
                    if (limit.maxDiameter > techLimitMaxDiameter) { techLimitMaxDiameter = limit.maxDiameter; }
                    if (limit.maxHeight > techLimitMaxHeight) { techLimitMaxHeight = limit.maxHeight; }
                }
            }
        }
    }

}
