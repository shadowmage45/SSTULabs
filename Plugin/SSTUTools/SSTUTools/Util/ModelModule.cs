using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSPShaderTools;

namespace SSTUTools
{

    public class ModelModule<T, U> where T : SingleModelData where U : PartModule
    {
        //apparently these are like a class declaration.... the delegate name becomes a new type that can be referenced
        public delegate ModelModule<T, U> SymmetryModule(U m);

        public delegate IEnumerable<T> ValidSelections(IEnumerable<T> allSelections);

        public delegate String[] DisplayNames(IEnumerable<T> validSelections);

        public delegate void PreModelSetup(T model);

        public readonly Part part;
        public readonly PartModule partModule;
        public readonly Transform root;
        public readonly ModelOrientation orientation;
        public SymmetryModule getSymmetryModule;
        public ValidSelections getValidSelections = delegate (IEnumerable<T> allSelections)
        {
            return allSelections;
        };
        public DisplayNames getDisplayNames = delegate (IEnumerable<T> validSelections) 
        {
            return SSTUUtils.getNames(validSelections, m => m.modelDefinition.title);
        };
        public PreModelSetup preModelSetup = delegate (T model) 
        {
            //noop
        };

        public List<T> models = new List<T>();
        public T model;
        public RecoloringData[] customColors = new RecoloringData[0];//zero-length array triggers default color assignment from texture set colors (if present)

        private BaseField dataField;
        private BaseField textureField;
        private BaseField modelField;

        private string textureSet
        {
            get { return textureField==null? "default" : textureField.GetValue<string>(partModule); }
            set { if (textureField != null) { textureField.SetValue(value, partModule); } }
        }

        private string modelName
        {
            get { return modelField.GetValue<string>(partModule); }
            set { modelField.SetValue(value, partModule); }
        }

        private string persistentData
        {
            get { return dataField==null? string.Empty : dataField.GetValue<string>(partModule); }
            set { if (dataField != null) { dataField.SetValue(value, partModule); } }
        }

        public float moduleMass { get { return model.getModuleMass(); } }

        public float moduleCost { get { return model.getModuleCost(); } }

        public float moduleVolume { get { return model.getModuleVolume(); } }

        public float moduleDiameter { get { return model.currentDiameter; } }

        public float moduleHeight { get { return model.currentHeight; } }

        /// <summary>
        /// Sets the position of the model so that the origin of the model is at the input Y coordinate, for the input model orientation.<para/>
        /// TOP = Y == bottom<para/>
        /// CENTRAL = Y == center<para/>
        /// BOTTOM = Y == top<para/>
        /// </summary>
        /// <param name="yPos"></param>
        /// <param name="orientation"></param>
        public void setPosition(float yPos, ModelOrientation orientation = ModelOrientation.TOP) { model.setPosition(yPos, orientation); }

        /// <summary>
        /// Updates the model for the currently specified position and scale
        /// </summary>
        public void updateModel()
        {
            preModelSetup(model);
            model.updateModel();
        }

        public TextureSet currentTextureSet
        {
            get { return model.modelDefinition.findTextureSet(textureSet); }
        }

        #region REGION - Constructors and Init Methods

        public ModelModule(Part part, PartModule partModule, Transform root, ModelOrientation orientation, string dataFieldName, string modelFieldName, string textureFieldName)
        {
            this.part = part;
            this.partModule = partModule;
            this.root = root;
            this.orientation = orientation;
            this.dataField = partModule.Fields[dataFieldName];
            this.modelField = partModule.Fields[modelFieldName];
            this.textureField = partModule.Fields[textureFieldName];
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
            if (model == null)
            {
                MonoBehaviour.print("ERROR: could not locate model for name: "+modelName);
                model = models[0];
                modelName = model.name;
                MonoBehaviour.print("Using first available model: "+ model.name);
            }
            preModelSetup(model);
            model.setupModel(root, orientation);
            bool useDefaultTextureColors = false;
            if (!model.isValidTextureSet(textureSet) && !string.Equals("default", textureSet))
            {
                if (!string.Equals(string.Empty, textureSet))
                {
                    MonoBehaviour.print("Current texture set for model " + model.name + " invalid: " + textureSet + ", clearing colors and assigning default texture set.");
                }
                textureSet = model.getDefaultTextureSet();
                if (!model.isValidTextureSet(textureSet))
                {
                    MonoBehaviour.print("ERROR: Default texture set: " + textureSet + " set for model: " + model.name + " is invalid.  This is a configuration level error in the model definition that needs to be corrected.");
                }
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
                if (textureField != null)
                {
                    m.partModule.updateUIChooseOptionControl(textureField.name, m.model.modelDefinition.getTextureSetNames(), m.model.modelDefinition.getTextureSetTitles(), true, m.textureSet);                    
                }
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
        public void setSectionColors(RecoloringData[] colors)
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
        public void updateSelections(bool updateIfInvalid = false)
        {
            IEnumerable<T> validSelections = getValidSelections(models);
            string[] names = SSTUUtils.getNames(validSelections, s => s.name);
            string[] displays = getDisplayNames(validSelections);
            if (updateIfInvalid && !Array.Exists(names, m => m == modelName))
            {
                modelSelected(names[0]);
            }
            partModule.updateUIChooseOptionControl(modelField.name, names, displays, true, modelName);
            modelField.guiActiveEditor = names.Length > 1;
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
                    customColors = new RecoloringData[3];
                    customColors[0] = ts.maskColors[0];
                    customColors[1] = ts.maskColors[1];
                    customColors[2] = ts.maskColors[2];
                }
                else
                {
                    RecoloringData dummy = new RecoloringData(Color.white, 0, 0);
                    customColors = new RecoloringData[] { dummy, dummy, dummy };
                }
                saveColors(customColors);
            }
            model.enableTextureSet(textureSet, customColors);
            if (textureField != null)
            {
                model.updateTextureUIControl(partModule, textureField.name, textureSet);
            }
            SSTUModInterop.onPartTextureUpdated(part);
        }

        private void actionWithSymmetry(Action<ModelModule<T, U>> action)
        {
            action(this);
            int index = part.Modules.IndexOf(partModule);
            foreach (Part p in part.symmetryCounterparts)
            {
                action(getSymmetryModule((U)p.Modules[index]));
            }
        }

        private void loadPersistentData(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                string[] colorSplits = data.Split(';');
                int len = colorSplits.Length;
                customColors = new RecoloringData[len];
                for (int i = 0; i < len; i++)
                {
                    customColors[i] = new RecoloringData(colorSplits[i]);
                }
            }
            else
            {
                customColors = new RecoloringData[0];
            }
        }

        private void saveColors(RecoloringData[] colors)
        {
            if (colors == null || colors.Length == 0) { return; }
            int len = colors.Length;
            string data = string.Empty;
            for (int i = 0; i < len; i++)
            {
                if (i > 0) { data = data + ";"; }
                data = data + colors[i].getPersistentData();
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
