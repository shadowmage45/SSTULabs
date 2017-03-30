using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{

    public class ModelModule<T> where T : SingleModelData
    {
        //apparently these are like a class declaration.... the delegate name becomes a new type that can be referenced
        public delegate ModelModule<T> SymmetryModule(PartModule m);

        public readonly Part part;
        public readonly PartModule partModule;
        public SymmetryModule getSymmetryModule;
        public readonly Transform root;

        public List<T> models = new List<T>();
        public T model;
        public Color[] customColors;
        public Vector3 scale;

        private string dataFieldName;
        private string textureFieldName;
        private string modelFieldName;
        private string diameterFieldName;
        private string vScaleFieldName;

        private string textureSet
        {
            get { return partModule.Fields[textureFieldName].GetValue<string>(partModule); }
            set { partModule.Fields[textureFieldName].SetValue(value, partModule); }
        }

        private string modelName
        {
            get { return partModule.Fields[modelFieldName].GetValue<string>(partModule); }
            set { partModule.Fields[modelFieldName].SetValue(value, partModule); }
        }

        private float diameter
        {
            get { return partModule.Fields[diameterFieldName].GetValue<float>(partModule); }
            set { partModule.Fields[diameterFieldName].SetValue(value, partModule); }
        }

        private string persistentData
        {
            get { return partModule.Fields[dataFieldName].GetValue<string>(partModule); }
            set { partModule.Fields[dataFieldName].SetValue(value, partModule); }
        }

        #region REGION - Constructors and Init Methods

        public ModelModule(Part part, PartModule partModule, Transform root, string dataFieldName)
        {
            this.part = part;
            this.partModule = partModule;
            this.root = root;
            this.dataFieldName = dataFieldName;
        }

        public void setup(List<T> models, string modelNameField, string modelTextureField, string modelDiameterField)
        {
            this.models.Clear();
            this.models.AddUniqueRange(models);
            this.modelFieldName = modelNameField;
            this.textureFieldName = modelTextureField;
            this.diameterFieldName = modelDiameterField;
            this.model = this.models.Find(m => m.name == modelName);
            this.setupModel();
            //external code can then position the model as desired, or call diameter/scale updating code, or w/e
        }

        public void setup(T[] models, string modelNameField, string modelTextureField, string modelDiameterField)
        {
            this.setup(new List<T>(models), modelNameField, modelTextureField, modelDiameterField);
        }

        #endregion ENDREGION - Constructors and Init Methods

        #region REGION - GUI Interaction Methods - With symmetry updating

        public void textureSetSelected(BaseField field, System.Object oldValue)
        {
            actionWithSymmetry(m => 
            {
                m.textureSet = textureSet;
                m.customColors = customColors;
                m.model.enableTextureSet(m.textureSet, m.customColors);
                m.partModule.updateUIChooseOptionControl(textureFieldName, m.model.modelDefinition.getTextureSetNames(), m.model.modelDefinition.getTextureSetNames(), true, m.textureSet);
                m.partModule.Fields[textureFieldName].guiActiveEditor = m.model.modelDefinition.textureSets.Length > 1;
            });
        }

        public void modelSelected(BaseField field, System.Object oldValue)
        {
            actionWithSymmetry(m => 
            {
                m.modelName = modelName;
                m.model.destroyCurrentModel();
                m.model = m.models.Find(s => s.name == m.modelName);
                m.setupModel();
                m.partModule.updateUIChooseOptionControl(modelFieldName, SSTUUtils.getNames(models, s => s.name), SSTUUtils.getNames(models, s=>s.name), true, m.modelName);
            });
        }

        public void modelSelected(string newModel)
        {
            modelName = newModel;
            modelSelected(null, null);//chain to symmetry enabled method
        }

        public void diameterUpdated(BaseField field, System.Object oldValue)
        {
            actionWithSymmetry(m => 
            {
                m.diameter = diameter;
                m.diameterUpdated(m.diameter);
                m.model.updateScaleForDiameter(m.diameter);
            });
        }

        public void diameterUpdated(float newDiameter)
        {
            diameter = newDiameter;
            diameterUpdated(null, null);//chain to symmetry enabled method
        }

        public void setSectionColors(Color[] colors)
        {
            actionWithSymmetry(m => 
            {
                m.textureSet = textureSet;
                m.customColors = colors;
                m.model.enableTextureSet(m.textureSet, m.customColors);
            });
        }

        #endregion ENDREGION - GUI Interaction Methods

        #region REGION - Private/Internal methods

        private void setupModel()
        {
            //TODO clone model, or if useExisting, check for existing model before cloning
            if (!model.isValidTextureSet(textureSet))
            {
                textureSet = model.getDefaultTextureSet();
            }
            model.enableTextureSet(textureSet, customColors);
        }

        private void actionWithSymmetry(Action<ModelModule<T>> action)
        {
            action(this);
            int index = part.Modules.IndexOf(partModule);
            foreach (Part p in part.symmetryCounterparts)
            {
                ModelModule<T> module = getSymmetryModule(p.Modules[index]);
                action(module);
            }
        }

        #endregion ENDREGION - Private/Internal methods

    }
}
