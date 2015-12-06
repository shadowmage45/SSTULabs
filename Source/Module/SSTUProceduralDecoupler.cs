using UnityEngine;
using System;
namespace SSTUTools
{
    public class SSTUProceduralDecoupler : ModuleDecouple, IPartCostModifier, IPartMassModifier
    {
        #region fields
        [KSPField]
        public string diffuseTextureName = "UNKNOWN";

        [KSPField]
        public string normalTextureName = "UNKNOWN";

        [KSPField]
        public bool canAdjustRadius = false;

        [KSPField]
        public bool canAdjustThickness = false;

        [KSPField]
        public bool canAdjustHeight = false;

        [KSPField(isPersistant = true)]
        public float radius = 0.625f;

        [KSPField(isPersistant = true)]
        public float height = 0.1f;

        [KSPField(isPersistant = true)]
        public float thickness = 0.1f;

        [KSPField(guiActiveEditor = true, guiName = "Rad Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
        public float radiusExtra;

        [KSPField(guiActiveEditor = true, guiName = "Height Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
        public float heightExtra;

        [KSPField(guiActiveEditor = true, guiName = "Thick Adj"), UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, maxValue = 1)]
        public float thicknessExtra;

        [KSPField]
        public float capHeight = 0f;

        [KSPField]
        public float maxPanelHeight = 1f;

        [KSPField]
        public int cylinderSides = 24;

        [KSPField]
        public float radiusAdjust = 0.625f;

        [KSPField]
        public float heightAdjust = 0.1f;

        [KSPField]
        public float thicknessAdjust = 0.1f;

        [KSPField]
        public float minRadius = 0.3125f;

        [KSPField]
        public float maxRadius = 5f;

        [KSPField]
        public float minThickness = 0.1f;

        [KSPField]
        public float maxThickness = 5f;

        [KSPField]
        public float minHeight = 0.1f;

        [KSPField]
        public float maxHeight = 0.5f;

        [KSPField]
        public float massPerCubicMeter = 0.4f;

        [KSPField]
        public float costPerCubicMeter = 50000f;

        [KSPField]
        public float forcePerKg = 0.8f;

        public float modifiedMass = 0;

        public float modifiedCost = 0;

        public float volume = 0;

        private ProceduralCylinderModel model;

        private float editorRadius;
        private float editorHeight;
        private float editorThickness;
        private float lastRadiusExtra;
        private float lastHeightExtra;
        private float lastThicknessExtra;

        #endregion

        #region KSP GUI Actions/Events

        [KSPEvent(guiName = "Radius +", guiActiveEditor = true)]
        public void increaseRadius()
        {
            editorRadius += radiusAdjust;
            if (editorRadius > maxRadius) { editorRadius = maxRadius; }
            recreateModel();
        }

        [KSPEvent(guiName = "Radius -", guiActiveEditor = true)]
        public void decreaseRadius()
        {
            editorRadius -= radiusAdjust;
            if (editorRadius < minRadius) { editorRadius = minRadius; }
            recreateModel();
        }

        [KSPEvent(guiName = "Height +", guiActiveEditor = true)]
        public void increaseHeight()
        {
            editorHeight += heightAdjust;
            if (editorHeight > maxHeight) { editorHeight = maxHeight; }
            recreateModel();
            updateAttachNodePositions(true);
        }

        [KSPEvent(guiName = "Height -", guiActiveEditor = true)]
        public void decreaseHeight()
        {
            editorHeight -= heightAdjust;
            if (editorHeight < minHeight) { editorHeight = minHeight; }
            recreateModel();
            updateAttachNodePositions(true);
        }

        [KSPEvent(guiName = "Thickness +", guiActiveEditor = true)]
        public void increaseThickness()
        {
            editorThickness += thicknessAdjust;
            if (editorThickness > maxThickness) { editorThickness = maxThickness; }
            recreateModel();
        }

        [KSPEvent(guiName = "Thickness -", guiActiveEditor = true)]
        public void decreaseThickness()
        {
            editorThickness -= thicknessAdjust;
            if (editorThickness < minThickness) { editorThickness = minThickness; }
            recreateModel();
        }

        #endregion

        #region KSP Lifecycle and KSP Overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
            {
                restoreEditorFields();
                prepModel();
            }
        }

        public override string GetInfo()
        {
            model.destroyModel();
            SSTUUtils.destroyChildren(part.FindModelTransform("model"));//remove the original empty proxy model and any created models
            model = null;
            return "This part has configurable diameter, height, thickness, and ejection force.";
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            restoreEditorFields();
            prepModel();
            updateGuiState();
            Fields["ejectionForce"].guiName = "Ejection Force";
            Fields["ejectionForce"].guiActiveEditor = true;
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(onEditorVesselModified));
            }
        }

        public void onEditorVesselModified(ShipConstruct ship)
        {
            if (lastRadiusExtra != radiusExtra || lastHeightExtra != heightExtra || lastThicknessExtra != thicknessExtra)
            {
                lastRadiusExtra = radiusExtra;
                lastHeightExtra = heightExtra;
                lastThicknessExtra = thicknessExtra;
                recreateModel();
                updatePhysicalAttributes();
                updateAttachNodePositions(true);
                updateDragCube();
                updateDecouplerForce();
                updateGuiState();
            }
        }

        public float GetModuleCost(float defaultCost)
        {
            return modifiedCost;
        }

        public float GetModuleMass(float defaultMass)
        {
            return -defaultMass + modifiedMass;
        }

        #endregion

        #region model updating/generation/regeneration

        public void restoreEditorFields()
        {
            float div = radius / radiusAdjust;
            float whole = (int)div;
            float extra = div - whole;
            editorRadius = whole * radiusAdjust;
            radiusExtra = extra;
            lastRadiusExtra = radiusExtra;

            div = height / heightAdjust;
            whole = (int)div;
            extra = div - whole;
            editorHeight = whole * heightAdjust;
            heightExtra = extra;
            lastHeightExtra = heightExtra;

            div = thickness / thicknessAdjust;
            whole = (int)div;
            extra = div - whole;
            editorThickness = whole * thicknessAdjust;
            thicknessExtra = extra;
            lastThicknessExtra = thicknessExtra;
        }

        public void prepModel()
        {
            if (model != null)
            {
                return;
            }
            Transform tr = part.transform.FindRecursive("model");
            SSTUUtils.destroyChildren(tr);//remove the original empty proxy model, and any models that may have been attached during prefab init
            model = new ProceduralCylinderModel();
            updateModelParameters();
            setModelParameters();
            model.setMaterial(SSTUUtils.loadMaterial(diffuseTextureName, normalTextureName));
            model.setMeshColliderStatus(true, false);
            model.createModel();
            model.setParent(tr);
            updatePhysicalAttributes();
            updateDecouplerForce();
            updateDragCube();
            SSTUUtils.updatePartHighlighting(part);
        }

        public void recreateModel()
        {
            updateModelParameters();
            setModelParameters();
            model.recreateModel();
            updatePhysicalAttributes();
            updateDecouplerForce();
            updateDragCube();
            SSTUUtils.updatePartHighlighting(part);
        }

        private void updateModelParameters()
        {
            lastRadiusExtra = radiusExtra;
            lastHeightExtra = heightExtra;
            lastThicknessExtra = thicknessExtra;
            radius = editorRadius + (radiusExtra * radiusAdjust);
            height = editorHeight + (heightExtra * heightAdjust);
            thickness = editorThickness + (thicknessExtra * thicknessAdjust);
        }

        private void setModelParameters()
        {
            model.setModelParameters(radius, height, thickness, capHeight, maxPanelHeight, cylinderSides);
        }

        public void updateGuiState()
        {
            Events["increaseRadius"].guiActiveEditor = Events["decreaseRadius"].guiActiveEditor = canAdjustRadius;
            Events["increaseHeight"].guiActiveEditor = Events["decreaseHeight"].guiActiveEditor = canAdjustHeight;
            Events["increaseThickness"].guiActiveEditor = Events["decreaseThickness"].guiActiveEditor = canAdjustThickness;
            Fields["radiusExtra"].guiActiveEditor = canAdjustRadius;
            Fields["heightExtra"].guiActiveEditor = canAdjustHeight;
            Fields["thicknessExtra"].guiActiveEditor = canAdjustThickness;
        }

        public void updateAttachNodePositions(bool userInput)
        {
            float h = (height + heightExtra * heightAdjust) * 0.5f;
            AttachNode topNode = part.findAttachNode("top");
            if (topNode != null)
            {
                SSTUUtils.updateAttachNodePosition(part, topNode, new Vector3(topNode.position.x, h, topNode.position.z), topNode.orientation, userInput);
            }
            AttachNode bottomNode = part.findAttachNode("bottom");
            if (bottomNode != null)
            {
                SSTUUtils.updateAttachNodePosition(part, bottomNode, new Vector3(bottomNode.position.x, -h, bottomNode.position.z), bottomNode.orientation, userInput);
            }
        }

        public void updatePhysicalAttributes()
        {
            float r = radius;
            float h = height;
            float t = thickness;
            float innerCylVolume = 0;
            float outerCylVolume = 0;
            float innerCylRadius = (r) - (t);
            float outerCylRadius = (r);
            innerCylVolume = (float)Math.PI * innerCylRadius * innerCylRadius * h;
            outerCylVolume = (float)Math.PI * outerCylRadius * outerCylRadius * h;
            volume = outerCylVolume - innerCylVolume;
            modifiedMass = volume * massPerCubicMeter;
            modifiedCost = volume * costPerCubicMeter;
            part.mass = modifiedMass;
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }

        //TODO
        public void updateDragCube()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return;//NOOP in editor
            }
            //TODO - do drag cubes even matter for non-physics enabled parts?
            //TODO - basically just re-render the drag cube
        }

        private void updateDecouplerForce()
        {
            ejectionForce = forcePerKg * (modifiedMass * 1000f);
        }

        #endregion
    }
}

