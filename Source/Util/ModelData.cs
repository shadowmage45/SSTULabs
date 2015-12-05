using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class ModelData
    {
        public String modelName;//read
        public String name;//read
        public String techLimit = "start";//read
        public float height;//read
        public float volume;//read
        public float mass;//read
        public float cost;//read
        public float diameter;//read
        public float verticalOffset;//read
        public bool invertModel;//read

        //cached values used for... things; updated by the update methods below
        public float currentDiameterScale;
        public float currentHeightScale;
        public float currentDiameter;
        public float currentHeight;
        public float currentVerticalPosition;

        public ModelData(ConfigNode node)
        {
            name = node.GetStringValue("name", String.Empty);
            modelName = node.GetStringValue("modelName", String.Empty);
            techLimit = node.GetStringValue("techLimit", techLimit);
            height = node.GetFloatValue("height", height);
            volume = node.GetFloatValue("volume", volume);
            mass = node.GetFloatValue("mass", mass);
            cost = node.GetFloatValue("cost", cost);
            diameter = node.GetFloatValue("diameter", diameter);
            verticalOffset = node.GetFloatValue("verticalOffset", verticalOffset);
            invertModel = node.GetBoolValue("invertModel", invertModel);
        }

        public void updateScaleForDiameter(float newDiameter)
        {
            float newScale = newDiameter / diameter;
            updateScale(newScale);
        }

        public void updateScaleForHeightAndDiameter(float newHeight, float newDiameter)
        {
            float newHorizontalScale = newDiameter / diameter;
            float newVerticalScale = newHeight / height;
            updateScale(newHorizontalScale, newVerticalScale);
        }

        public void updateScale(float newScale)
        {
            updateScale(newScale, newScale);
        }

        public void updateScale(float newHorizontalScale, float newVerticalScale)
        {
            currentDiameterScale = newHorizontalScale;
            currentHeightScale = newVerticalScale;
            currentHeight = newVerticalScale * height;
            currentDiameter = newHorizontalScale * diameter;
        }

        public bool isAvailable()
        {
            if (String.IsNullOrEmpty(techLimit)) { return true; }
            if (HighLogic.CurrentGame == null) { return true; }
            if (ResearchAndDevelopment.Instance == null) { return true; }
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX) { return true; }
            return SSTUUtils.isTechUnlocked(techLimit);
        }

        public virtual void setupModel(Part part, Transform parent)
        {
            throw new NotImplementedException();
        }

        public virtual void updateModel()
        {
            throw new NotImplementedException();
        }

        public virtual void destroyCurrentModel()
        {
            throw new NotImplementedException();
        }

        public virtual float getModuleVolume()
        {
            return volume * currentDiameterScale * currentDiameterScale * currentHeightScale;
        }

        public virtual float getModuleMass()
        {
            return mass * currentDiameterScale * currentDiameterScale * currentHeightScale;
        }

        public virtual float getModuleCost()
        {
            return cost * currentDiameterScale * currentDiameterScale * currentHeightScale;
        }
    }
    
    public class SingleModelData : ModelData
    {
        public GameObject model;

        public SingleModelData(ConfigNode node) : base(node)
        {
            name = node.GetStringValue("name", String.Empty);
            modelName = node.GetStringValue("modelName", String.Empty);
            height = node.GetFloatValue("height", height);
            volume = node.GetFloatValue("volume", volume);
            diameter = node.GetFloatValue("diameter", diameter);
            verticalOffset = node.GetFloatValue("verticalOffset", verticalOffset);
            invertModel = node.GetBoolValue("invertModel", invertModel);
        }

        public override void setupModel(Part part, Transform parent)
        {            
            if (!String.IsNullOrEmpty(modelName))
            {                
                model = SSTUUtils.cloneModel(modelName);
                if (model != null)
                {
                    model.transform.NestToParent(parent);                    
                    if (invertModel)
                    {
                        model.transform.Rotate(new Vector3(0, 0, 1), 180, Space.Self);
                    }
                }
            }
        }

        public override void updateModel()
        {
            if (model != null)
            {
                model.transform.localScale = new Vector3(currentDiameterScale, currentHeightScale, currentDiameterScale);
                model.transform.localPosition = new Vector3(0, currentVerticalPosition, 0);
            }
        }

        public override void destroyCurrentModel()
        {
            if (model == null) { return; }
            model.transform.parent = null;
            GameObject.Destroy(model);
            model = null;
        }
    }

    public class MountModelData : SingleModelData
    {
        public SSTUEngineMountDefinition mountDefinition;
        public List<AttachNodeData> nodePositions = new List<AttachNodeData>();
        public bool nose = false;        
        public MountModelData(ConfigNode node, bool isNose) : base(node)
        {
            mountDefinition = SSTUEngineMountDefinition.getMountDefinition(name);
            modelName = mountDefinition.modelName;
            height = mountDefinition.height;
            volume = mountDefinition.volume;
            diameter = mountDefinition.defaultDiameter;
            verticalOffset = mountDefinition.verticalOffset;
            invertModel = mountDefinition.invertModel;
            mass = mountDefinition.mountMass;
            nose = isNose;
            if (nose) { invertModel = !invertModel; }
            foreach (AttachNodeData data in mountDefinition.nodePositions)
            {
                AttachNodeData newData = new AttachNodeData(data);
                if (nose) { newData.invert(); }
                nodePositions.Add(newData);
            }
        }
    }
}
