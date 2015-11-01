using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools.Module
{
    class SSTUCustomRadialDecoupler : ModuleAnchoredDecoupler
    {

        [KSPField(isPersistant = true)]
        public float height = 1f;

        [KSPField(isPersistant = true)]
        public float radius = 1.25f;

        [KSPField]
        public float heightIncrement = 1f;

        [KSPField]
        public float radiusIncrement = 0.625f;

        [KSPField(guiActiveEditor = true, guiName = "Height Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
        public float editorHeightExtra;

        [KSPField(guiActiveEditor = true, guiName = "Radius Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
        public float editorRadiusExtra;

        //this is used to determine actual resultant scale from input for radius
        [KSPField]
        public float defaultRadius = 1.25f;

        [KSPField]
        public float maxHeight = 100f;

        [KSPField]
        public float minHeight = 0.5f;

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

        [KSPEvent(guiName = "Height-", guiActiveEditor = true)]
        public void prevHeightEvent()
        {
            editorHeight -= heightIncrement;
            if (editorHeight <= minHeight)
            {
                editorHeight = minHeight;
            }
            updateModule();
        }

        [KSPEvent(guiName = "Height+", guiActiveEditor = true)]
        public void nextHeightEvent()
        {
            editorHeight += heightIncrement;
            if (editorHeight >= maxHeight)
            {
                editorHeight = maxHeight;
            }
            updateModule();
        }

        [KSPEvent(guiName = "Size-", guiActiveEditor = true)]
        public void prevRadiusEvent()
        {
            editorRadius -= radiusIncrement;
            if (editorRadius < minRadius)
            {
                editorRadius = minRadius;
            }
            updateModule();
        }

        [KSPEvent(guiName = "Size+", guiActiveEditor = true)]
        public void nextRadiusEvent()
        {
            editorRadius += radiusIncrement;
            if (editorRadius > maxRadius)
            {
                editorRadius = maxRadius;
            }
            updateModule();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            restoreEditorFields();
            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            locateTransforms();
            updateStats();
            updateModelPositions();
            updateModelScales();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            restoreEditorFields();
        }

        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
        }

        public void onEditorVesselModified(ShipConstruct ship)
        {
            if (!HighLogic.LoadedSceneIsEditor) { return; }
            if (editorRadiusExtra != lastRadiusExtra || editorHeightExtra != lastHeightExtra)
            {
                updateModule();
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
            updateStats();
            updateModelPositions();
            updateModelScales();
            SSTUCustomRadialDecoupler mod;
            foreach (Part p in part.symmetryCounterparts)
            {
                mod = p.GetComponent<SSTUCustomRadialDecoupler>();
                updateModule(mod);
            }
        }

        private void updateModule(SSTUCustomRadialDecoupler mod)
        {
            if (mod != this)
            {
                mod.editorHeight = editorHeight;
                mod.editorRadius = editorRadius;
                mod.editorHeightExtra = editorHeightExtra;
                mod.editorRadiusExtra = editorRadiusExtra;
            }
            mod.updateStats();
            mod.updateModelPositions();
            mod.updateModelScales();
        }

        private void updateStats()
        {
            height = editorHeight + editorHeightExtra * heightIncrement;
            radius = editorRadius + editorRadiusExtra * radiusIncrement;
            lastHeightExtra = editorHeightExtra;
            lastRadiusExtra = editorRadiusExtra;
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
            updateStats();
            float defRad = defaultRadius;
            float curRad = radius;
            return curRad / defRad;
        }

        private void locateTransforms()
        {
            topMountTransform = part.FindModelTransform(topMountName);
            bottomMountTransform = part.FindModelTransform(bottomMountName);
        }

    }

}
