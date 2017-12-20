using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSPShaderTools;

namespace SSTUTools
{

    public class ModelModule<T, U> where T : SingleModelData where U : PartModule
    {

        public delegate ModelModule<T, U> SymmetryModule(U m);

        public delegate ModelDefinition[] ValidOptions();

        public delegate void PreModelSetup(SingleModelData model);

        public delegate T CreatModel(string name);

        public readonly Part part;
        public readonly U partModule;
        public readonly Transform root;
        public readonly ModelOrientation orientation;

        public ValidOptions getValidOptions;

        public SymmetryModule getSymmetryModule;

        public SymmetryModule getParentModule = delegate (U module) 
        {
            return null;
        };

        public PreModelSetup preModelSetup = delegate (SingleModelData model) 
        {
            //noop
        };

        public CreatModel createModel = delegate (string name) 
        {
            return (T) new SingleModelData(name);
        };

        /// <summary>
        /// Base model definition list, used for stand-alone modules that do not rely on external modules for model validation
        /// </summary>
        public string[] baseOptions;

        public T model;
        public RecoloringData[] customColors = new RecoloringData[0];//zero-length array triggers default color assignment from texture set colors (if present)

        private ModelDefinition[] optionsCache;

        private BaseField dataField;
        private BaseField textureField;
        private BaseField modelField;

        public AnimationModule animationModule;
        private int animationLayer = 0;

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

        #region REGION - Convenience wrappers for model/definition data

        public ModelDefinition modelDefinition { get { return model.modelDefinition; } }

        public ModelRCSData rcsData { get { return model.modelDefinition.rcsData; } }

        public ModelEngineThrustData engineThrustData { get { return model.modelDefinition.engineThrustData; } }

        public ModelEngineTransformData engineTransformData { get { return model.modelDefinition.engineTransformData; } }

        public float moduleMass { get { return model.getModuleMass(); } }

        public float moduleCost { get { return model.getModuleCost(); } }

        public float moduleVolume { get { return model.getModuleVolume(); } }

        public float moduleDiameter { get { return model.currentDiameter; } }

        public float moduleHeight { get { return model.currentHeight; } }

        #endregion ENDREGION - Convenience wrappers for model/definition data

        /// <summary>
        /// Sets the position of the model so that the origin of the model is at the input Y coordinate, for the input model orientation.<para/>
        /// TOP = Y == bottom<para/>
        /// CENTRAL = Y == center<para/>
        /// BOTTOM = Y == top<para/>
        /// </summary>
        /// <param name="yPos"></param>
        /// <param name="orientation"></param>
        public void setPosition(float yPos)
        {
            model.setPosition(yPos, orientation);
        }

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

        /// <summary>
        /// Only a partial constructor.  Need to also call 'setupFields', 'setupModelList', and 'setupModel' before the module will actually be usable.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="partModule"></param>
        /// <param name="root"></param>
        /// <param name="orientation"></param>
        /// <param name="dataFieldName"></param>
        /// <param name="modelFieldName"></param>
        /// <param name="textureFieldName"></param>
        public ModelModule(Part part, U partModule, Transform root, ModelOrientation orientation, 
            string modelPersistenceFieldName, string texturePersistenceFieldName, string recolorPersistenceFieldName, 
            string animationPersistenceFieldName, string deployLimitField, string deployEventName, string retractEventName)
        {
            this.part = part;
            this.partModule = partModule;
            this.root = root;
            this.orientation = orientation;
            this.modelField = partModule.Fields[modelPersistenceFieldName];
            this.textureField = partModule.Fields[texturePersistenceFieldName];
            this.dataField = partModule.Fields[recolorPersistenceFieldName];
            this.animationModule = new AnimationModule(part, partModule, partModule.Fields[animationPersistenceFieldName], partModule.Fields[deployLimitField], partModule.Events[deployEventName], partModule.Events[retractEventName]);
            loadPersistentData(persistentData);
        }

        /// <summary>
        /// Initialization method.  May be called to update the available model list later, but the model must be re-setup afterwards
        /// </summary>
        /// <param name="models"></param>
        public void setupModelList(ModelDefinition[] modelDefs)
        {
            optionsCache = modelDefs;
            if (model != null)
            {
                model.destroyCurrentModel();
            }
            model = null;
            createModel(modelName);
            updateSelections();
        }

        /// <summary>
        /// Initialization method.  Subsequent changes to model should call the modelSelectedXXX methods below.
        /// </summary>
        public void setupModel()
        {
            SSTUUtils.destroyChildrenImmediate(root);
            if (model != null)
            {
                model.destroyCurrentModel();
            }
            createModel(modelName);
            if (model == null)
            {
                loadDefaultModel();
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
            animationModule.setupAnimations(model.getAnimationData(), root, animationLayer);
        }

        #endregion ENDREGION - Constructors and Init Methods

        #region REGION - Update Methods

        public void Update()
        {
            animationModule.Update();
        }

        public void renameRCSThrustTransforms(string destinationName)
        {
            if (modelDefinition.rcsData == null)
            {
                MonoBehaviour.print("ERROR: RCS data is null for model definition: " + modelDefinition.name);
                return;
            }
            modelDefinition.rcsData.renameTransforms(root, destinationName);
        }

        public void renameEngineThrustTransforms(string destinationName)
        {
            if (modelDefinition.engineTransformData == null)
            {
                MonoBehaviour.print("ERROR: Engine transform data is null for model definition: " + modelDefinition.name);
                return;
            }
            modelDefinition.engineTransformData.renameThrustTransforms(root, destinationName);
        }

        #endregion ENDREGION - Update Methods

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
            setupModel();
        }

        /// <summary>
        /// NON-Symmetry enabled method
        /// </summary>
        public void updateSelections(bool updateIfInvalid = false)
        {
            optionsCache = getValidOptions();
            string[] names = SSTUUtils.getNames(optionsCache, s => s.name);
            string[] displays = SSTUUtils.getNames(optionsCache, s => s.title);
            if (updateIfInvalid && !Array.Exists(names, m => m == modelName))
            {
                modelSelected(names[0]);
            }
            partModule.updateUIChooseOptionControl(modelField.name, names, displays, true, modelName);
            modelField.guiActiveEditor = names.Length > 1;
        }

        public ModelDefinition[] getUpperOptions() { return model.modelDefinition.getValidUpperOptions(partModule.upgradesApplied); }

        public ModelDefinition[] getLowerOptions() { return model.modelDefinition.getValidLowerOptions(partModule.upgradesApplied); }

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
        }

        private void loadDefaultModel()
        {
            modelName = optionsCache[0].name;
            model = createModel(modelName);
            if (!model.isValidTextureSet(textureSet))
            {
                textureSet = model.getDefaultTextureSet();
            }
        }

        #endregion ENDREGION - Private/Internal methods

    }
}
