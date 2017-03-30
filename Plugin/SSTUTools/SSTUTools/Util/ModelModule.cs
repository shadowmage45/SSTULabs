using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class ModelModule<T> where T : SingleModelData
    {

        public readonly Part part;
        public readonly PartModule module;
        public readonly Transform root;
        public List<T> models = new List<T>();
        public T model;
        public Color[] customColors;
        public string textureSet;
        public Vector3 scale;
        private BaseField persistentDataField;
        private BaseField textureField;
        private BaseField modelField;

        public ModelModule(Part part, PartModule module, Transform root, BaseField dataField)
        {
            this.part = part;
            this.module = module;
            this.root = root;
            this.persistentDataField = dataField;
        }

        public void setup(List<T> models, string modelName, string textureSet)
        {
            this.models.Clear();
            this.models.AddUniqueRange(models);
            this.model = this.models.Find(m => m.name == modelName);
            this.textureSet = textureSet;
            this.setupModel(true);
            //external code can then position the model as desired, or call diameter/scale updating code, or w/e
        }

        public void textureSetSelected(BaseField field, System.Object oldValue)
        {
            string setName = field.GetValue<string>(module);
            //TODO
        }

        public void modelSelected(BaseField field, System.Object oldValue)
        {
            model.destroyCurrentModel();
            string modelName = field.GetValue<string>(module);
            model = this.models.Find(m => m.name == modelName);
            setupModel(false);
        }

        public void diameterUpdated(BaseField field, System.Object oldValue)
        {
            float newDiameter = field.GetValue<float>(module);
            //TODO
        }

        public void setSectionColors(Color[] colors)
        {
            //TODO -- better implementation of enabling colors
            this.customColors = colors;//TODO copy colors into private array?
            this.model.enableTextureSet(textureSet, customColors);
        }

        private void setupModel(bool useExisting)
        {
            //TODO clone model, or if useExisting, check for existing model before cloning
            if (!model.isValidTextureSet(textureSet))
            {
                textureSet = model.getDefaultTextureSet();
                textureField.SetValue(textureSet, module);
            }
            model.enableTextureSet(textureSet, customColors);
        }

    }
}
