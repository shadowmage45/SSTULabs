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
        public Color[] customColors = new Color[0];//zero-length array triggers default color assignment from texture set colors (if present)

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
            if (models == null) { MonoBehaviour.print("ERROR: model list was null!  Models must be populated after module construction."); }
            model = models.Find(m => m.name == modelName);
            if (model == null) { MonoBehaviour.print("ERROR: could not locate model for name: "+modelName); }
            model.setupModel(root, orientation);
            bool useDefaultTextureColors = false;
            if (!model.isValidTextureSet(textureSet))
            {
                MonoBehaviour.print("Current texture set for model "+model.name+" invalid: "+textureSet+", clearing colors and assigning default texture set.");
                textureSet = model.getDefaultTextureSet();
                useDefaultTextureColors = true;
                if (SSTUGameSettings.persistRecolor())
                {
                    useDefaultTextureColors = false;
                }
            }
            if (customColors == null || customColors.Length == 0)
            {
                useDefaultTextureColors = true;
            }
            applyTextureSet(textureSet, useDefaultTextureColors);
        }

        #endregion ENDREGION - Constructors and Init Methods

        #region REGION - GUI Interaction Methods - With symmetry updating

        /// <summary>
        /// Symmetry-enabled method.  Should only be called when symmetry updates are desired.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="oldValue"></param>
        public void textureSetSelected(BaseField field, System.Object oldValue)
        {
            bool defaultColors = !SSTUGameSettings.persistRecolor();
            actionWithSymmetry(m => 
            {
                m.textureSet = textureSet;
                m.applyTextureSet(m.textureSet, defaultColors);
                m.partModule.updateUIChooseOptionControl(textureFieldName, m.model.modelDefinition.getTextureSetNames(), m.model.modelDefinition.getTextureSetNames(), true, m.textureSet);
                m.partModule.Fields[textureFieldName].guiActiveEditor = m.model.modelDefinition.textureSets.Length > 1;
            });
        }

        /// <summary>
        /// Symmetry-enabled method.  Should only be called when symmetry updates are desired.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="oldValue"></param>
        public void modelSelected(BaseField field, System.Object oldValue)
        {
            actionWithSymmetry(m => 
            {
                m.loadModel(modelName);
                m.setupModel();
            });
        }

        /// <summary>
        /// Symmetry enabled
        /// </summary>
        /// <param name="colors"></param>
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

        /// <summary>
        /// NON-Symmetry enabled method.
        /// </summary>
        /// <param name="newModel"></param>
        public void modelSelected(string newModel)
        {
            loadModel(newModel);
            setupModel();
        }

        /// <summary>
        /// NON-Symmetry enabled method
        /// </summary>
        public void updateSelections()
        {
            IEnumerable<T> validSelections = getValidSelections(models);
            string[] names = SSTUUtils.getNames(validSelections, s => s.name);
            partModule.updateUIChooseOptionControl(modelFieldName, names, names, true, modelName);
        }

        #endregion ENDREGION - GUI Interaction Methods

        #region REGION - Private/Internal methods

        private void applyTextureSet(string setName, bool useDefaultColors)
        {
            if (!model.isValidTextureSet(setName))
            {
                setName = model.getDefaultTextureSet();
                textureSet = setName;
            }
            if (useDefaultColors)
            {
                TextureSet ts = Array.Find(model.modelDefinition.textureSets, m => m.name == setName);
                if (ts!=null && ts.maskColors != null && ts.maskColors.Length > 0)
                {
                    customColors = new Color[3];
                    customColors[0] = ts.maskColors[0];
                    customColors[1] = ts.maskColors[1];
                    customColors[2] = ts.maskColors[2];
                }
                else
                {
                    customColors = new Color[] { Color.white, Color.white, Color.white };
                }
                saveColors(customColors);
            }
            model.enableTextureSet(textureSet, customColors);
            model.updateTextureUIControl(partModule, textureFieldName, textureSet);
        }

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
            if (!string.IsNullOrEmpty(data))
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
                    a = dataSplits.Length >= 4 ? SSTUUtils.safeParseFloat(dataSplits[3]) : 1f;
                    customColors[i] = new Color(r, g, b, a);
                }
            }
            else
            {
                customColors = new Color[] { Color.white, Color.white, Color.white };
            }
        }

        private void saveColors(Color[] colors)
        {
            if (colors == null || colors.Length == 0) { return; }
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

        private void loadModel(string name)
        {
            modelName = name;
            if (model != null)
            {
                model.destroyCurrentModel();
            }
            model = models.Find(s => s.name == modelName);
            if (model == null)
            {
                loadDefaultModel();
            }
            setupModel();
        }

        private void loadDefaultModel()
        {
            model = getValidSelections(models).First();
            modelName = model.name;
            if (!model.isValidTextureSet(textureSet))
            {
                textureSet = model.getDefaultTextureSet();
            }
        }

        #endregion ENDREGION - Private/Internal methods

    }
}
