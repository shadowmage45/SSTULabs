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

        public delegate IEnumerable<T> ValidSelections(IEnumerable<T> allSelections);

        public readonly Part part;
        public readonly PartModule partModule;
        public readonly Transform root;
        public readonly ModelOrientation orientation;
        public SymmetryModule getSymmetryModule;
        public ValidSelections getValidSelections = delegate (IEnumerable<T> allSelections)
        {
            return allSelections;
        };

        public List<T> models = new List<T>();
        public T model;
        public Color[] customColors = new Color[] { Color.clear, Color.clear, Color.clear };

        //string names used to hook into the KSPField data for module fields
        //workaround to needing those exact fields for persistence and UI interaction functions
        private string dataFieldName;
        private string textureFieldName;
        private string modelFieldName;

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

        private string persistentData
        {
            get { return partModule.Fields[dataFieldName].GetValue<string>(partModule); }
            set { partModule.Fields[dataFieldName].SetValue(value, partModule); }
        }

        #region REGION - Constructors and Init Methods

        public ModelModule(Part part, PartModule partModule, Transform root, ModelOrientation orientation, string dataFieldName, string modelFieldName, string textureFieldName)
        {
            this.part = part;
            this.partModule = partModule;
            this.root = root;
            this.orientation = orientation;
            this.dataFieldName = dataFieldName;
            this.modelFieldName = modelFieldName;
            this.textureFieldName = textureFieldName;
            loadPersistentData(persistentData);
        }

        /// <summary>
        /// Initialization method.  May be called to update the available mount list later, but the model must be re-setup afterwards
        /// </summary>
        /// <param name="models"></param>
        public void setupModelList(IEnumerable<T> models)
        {
            this.models.Clear();
            this.models.AddUniqueRange(models);
            if (model != null) { model.destroyCurrentModel(); }
            model = this.models.Find(m => m.name == modelName);
            updateSelections();
        }

        /// <summary>
        /// Initialization method.  Subsequent changes to model should call the modelSelectedXXX methods below.
        /// </summary>
        public void setupModel()
        {
            SSTUUtils.destroyChildrenImmediate(root);
            model = models.Find(m => m.name == modelName);
            model.setupModel(root, orientation);
            if (!model.isValidTextureSet(textureSet))
            {
                textureSet = model.getDefaultTextureSet();
            }
            model.enableTextureSet(textureSet, customColors);
            model.updateTextureUIControl(partModule, textureFieldName, textureSet);
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
            });
        }

        public void modelSelected(string newModel)
        {
            modelName = newModel;
            modelSelected(null, null);//chain to symmetry enabled method
        }

        public void setSectionColors(Color[] colors)
        {
            actionWithSymmetry(m => 
            {
                m.textureSet = textureSet;
                m.customColors = colors;
                m.model.enableTextureSet(m.textureSet, m.customColors);
                m.saveColors(m.customColors);
            });
        }

        public void updateSelections()
        {
            actionWithSymmetry(m =>
            {
                IEnumerable<T> validSelections = m.getValidSelections(models);
                string[] names = SSTUUtils.getNames(validSelections, s => s.name);
                m.partModule.updateUIChooseOptionControl(m.modelFieldName, names, names, true, m.modelName);
            });
        }

        #endregion ENDREGION - GUI Interaction Methods

        #region REGION - Private/Internal methods

        private void actionWithSymmetry(Action<ModelModule<T>> action)
        {
            action(this);
            int index = part.Modules.IndexOf(partModule);
            foreach (Part p in part.symmetryCounterparts)
            {
                action(getSymmetryModule(p.Modules[index]));
            }
        }

        private void loadPersistentData(string data)
        {
            string[] colorSplits = data.Split(';');
            string[] dataSplits;
            int len = colorSplits.Length;
            customColors = new Color[len];
            float r, g, b, a;
            for (int i = 0; i < len; i++)
            {
                dataSplits = colorSplits[i].Split(',');
                r = SSTUUtils.safeParseFloat(dataSplits[0]);
                g = SSTUUtils.safeParseFloat(dataSplits[1]);
                b = SSTUUtils.safeParseFloat(dataSplits[2]);
                a = dataSplits.Length >= 4 ? SSTUUtils.safeParseFloat(dataSplits[3]): 1f;
            }
        }

        private void saveColors(Color[] colors)
        {
            int len = colors.Length;
            string data = string.Empty;
            for (int i = 0; i < len; i++)
            {
                if (i > 0) { data = data + ";"; }
                data = data + colors[i].r + ",";
                data = data + colors[i].g + ",";
                data = data + colors[i].b + ",";
                data = data + colors[i].a;
            }
            persistentData = data;
        }

        #endregion ENDREGION - Private/Internal methods

    }
}
