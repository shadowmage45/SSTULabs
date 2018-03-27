using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSPShaderTools;

namespace SSTUTools
{

    //ModelModule transform hierarchy
    //part(the GO with the Part Component on it)
    //model(the standard Part 'model' transform)
    //    NamedOuterModelTransform -- positioned and scaled by ModularPart code
    //        ModelModule-0 -- positioned relative to parent by ModuleModule, using the ModelLayoutData currently set in the ModelModule -- defaults to 0,0,0 position, 1,1,1 scale, 0,0,0 rotation
    //        ModelModule-n -- second/third/etc -- there will be one entry for every 'position' in the ModelLayoutData config

    /// <summary>
    /// ModelModule is a 'standard class' that can be used from within PartModule code to manage a single active model, as well as any potential alternate model selections.<para/>
    /// Uses ModelDefinition configs to define how the models are setup.<para/>
    /// Includes support for all features that are supported by ModelDefinition (animation, solar, rcs, engine, gimbal, constraints)<para/>
    /// Creation consists of the following setup sequence:<para/>
    /// 1.) Call constructor
    /// 2.) Setup Delegate methods
    /// 3.) Call setupModelList
    /// 4.) Call setupModel
    /// 5.) Call updateSelections
    /// </summary>
    /// <typeparam name="U"></typeparam>
    public class ModelModule<U> where U : PartModule
    {

        #region REGION - Delegate Signatures
        public delegate ModelModule<U> SymmetryModule(U m);
        public delegate ModelDefinitionLayoutOptions[] ValidOptions();
        #endregion ENDREGION - Delegate Signatures

        #region REGION - Immutable fields
        public readonly Part part;
        public readonly U partModule;
        public readonly Transform root;
        public readonly ModelOrientation orientation;
        public readonly int animationLayer = 0;
        public readonly AnimationModule animationModule;
        #endregion ENDREGION - Immutable fields

        #region REGION - Public Delegate Stubs

        /// <summary>
        /// Delegate MUST be populated.
        /// </summary>
        public ValidOptions getValidOptions;

        /// <summary>
        /// Delegate MUST be populated.
        /// </summary>
        public SymmetryModule getSymmetryModule;

        public Func<float> getLayoutPositionScalar = delegate ()
        {
            return 1f;
        };

        public Func<float> getLayoutScaleScalar = delegate ()
        {
            return 1f;
        };

        #endregion ENDREGION - Public Delegate Stubs

        #region REGION - Private working data

        /// <summary>
        /// Internal cached 'name' for this model-module.  Used in error-reporting to more easily tell the difference between various modules in any given part.
        /// </summary>
        private string moduleName = "ModelModule";

        /// <summary>
        /// Local cache to the root transforms of the models used for the layout.  If only a single position in layout, this will be a length=1 array.
        /// Will contain one transform for each position in the layout, with identical ordering to the specification in the layout.
        /// </summary>
        private Transform[] models;

        /// <summary>
        /// Local cache of the recoloring data to use for this module.  Loaded from persistence data if the recoloring persistence field is present.  Auto-saved out to persistence field on color updates.
        /// </summary>
        private RecoloringData[] customColors = new RecoloringData[] { new RecoloringData(Color.white, 1, 1), new RecoloringData(Color.white, 1, 1), new RecoloringData(Color.white, 1, 1) };

        /// <summary>
        /// The -current- model definition.  Pulled from the array of all defs.
        /// </summary>
        private ModelDefinitionLayoutOptions currentLayoutOptions;

        /// <summary>
        /// The -current- model definition.  Cached local access to the def stored in the current layout option.
        /// </summary>
        private ModelDefinition currentDefinition;

        /// <summary>
        /// The -current- model layout in use.  Initialized during setupModels() call, and should always be valid after that point.
        /// </summary>
        private ModelLayoutData currentLayout;

        /// <summary>
        /// Array containing all possible model definitions for this module.
        /// </summary>
        private ModelDefinitionLayoutOptions[] optionsCache;

        /// <summary>
        /// Local reference to the persistent data field used to store custom coloring data for this module.  May be null when recoloring is not used.
        /// </summary>
        private BaseField dataField;

        /// <summary>
        /// Local reference to the persistent data field used to store texture set names for this module.  May be null when texture switching is not used.
        /// </summary>
        private BaseField textureField;

        /// <summary>
        /// Local reference to the persistent data field used to store the current model name for this module.  Must not be null.
        /// </summary>
        private BaseField modelField;

        /// <summary>
        /// Local referenec to the persistent data field used to store the current layout name for this module.  May be null if layouts are unsupported, in which case it will always return 'defualt'.
        /// </summary>
        private BaseField layoutField;

        /// <summary>
        /// Local cached working variables for scale, sizing, mass, and cost.
        /// </summary>
        private float currentHorizontalScale = 1f;
        private float currentVerticalScale = 1f;
        private float currentDiameter;
        private float currentHeight;
        private float currentVerticalPosition;
        private float currentCost;
        private float currentMass;
        private float currentVolume;

        #endregion ENDREGION - Private working data

        #region REGION - BaseField wrappers

        /// <summary>
        /// Wrapper for the BaseField in the PartModule.  Uses reflection, so a bit dirty, but functional and reliable.
        /// </summary>
        private string textureSetName
        {
            get { return textureField == null ? "default" : textureField.GetValue<string>(partModule); }
            set { if (textureField != null) { textureField.SetValue(value, partModule); } }
        }

        /// <summary>
        /// Wrapper for the BaseField in the PartModule.  Uses reflection, so a bit dirty, but functional and reliable.
        /// </summary>
        private string modelName
        {
            get { return modelField.GetValue<string>(partModule); }
            set { modelField.SetValue(value, partModule); }
        }

        /// <summary>
        /// Wrapper for the BaseField in the PartModule.  Uses reflection, so a bit dirty, but functional and reliable.
        /// </summary>
        private string persistentData
        {
            get { return dataField == null ? string.Empty : dataField.GetValue<string>(partModule); }
            set { if (dataField != null) { dataField.SetValue(value, partModule); } }
        }

        /// <summary>
        /// Wrapper for the BaseField in the PartModule.  Uses reflection, so a bit dirty, but functional and reliable.
        /// </summary>
        private string layoutName
        {
            get { return layoutField == null ? "default" : layoutField.GetValue<string>(partModule); }
            set { if (layoutField != null) { layoutField.SetValue(value, partModule); } }
        }

        #endregion ENDREGION - BaseField wrappers

        #region REGION - Convenience wrappers for accessing model definition data for external use

        public string name
        {
            get { return moduleName; }
            set { moduleName = value; }
        }

        /// <summary>
        /// Return true/false if fairings are enabled for this module in its current configuration.
        /// </summary>
        public bool fairingEnabled { get { return definition.fairingData == null ? false : definition.fairingData.fairingsSupported; } }

        /// <summary>
        /// Return true/false if animations are enabled for this module in its current configuration.
        /// </summary>
        public bool animationEnabled { get { return definition.animationData != null; } }

        /// <summary>
        /// Return true/false if this part can be the parent of an RCS module/model.
        /// </summary>
        public bool rcsParentEnabled { get { return definition.rcsData != null; } }

        /// <summary>
        /// Return true/false if this module has engine transform data for its current configuration.
        /// </summary>
        public bool engineTransformEnabled { get { return definition.engineTransformData != null; } }

        /// <summary>
        /// Return true/false if this module has engine thrust data for its current configuration.
        /// </summary>
        public bool engineThrustEnabled { get { return definition.engineThrustData != null; } }

        //TODO -- create specific gimbal transform data holder class
        /// <summary>
        /// Return true/false if this module has gimbal transform data for its current configuration.
        /// </summary>
        public bool engineGimalEnabled { get { return definition.engineTransformData != null; } }

        /// <summary>
        /// Return true/false if this module has solar panel data for its current configuration.
        /// </summary>
        public bool solarEnabled { get { return definition.solarData != null; } }

        /// <summary>
        /// Return the currently 'active' model definition.
        /// </summary>
        public ModelDefinition definition { get { return currentDefinition; } }

        /// <summary>
        /// Return the currently active texture set from the currently active model definition.
        /// </summary>
        public TextureSet textureSet { get { return definition.findTextureSet(textureSetName); } }

        /// <summary>
        /// Return the currently active model layout.
        /// </summary>
        public ModelLayoutData layout { get { return currentLayoutOptions.layouts.Find(m=>m.name==layoutName); } }

        /// <summary>
        /// Return the currently active layout options for the current model definition.
        /// </summary>
        public ModelDefinitionLayoutOptions layoutOptions { get { return currentLayoutOptions; } }

        /// <summary>
        /// Return the current mass for this module slot.  Includes adjustments from the definition mass based on the current scale.
        /// </summary>
        public float moduleMass { get { return currentMass; } }

        /// <summary>
        /// Return the current cost for this module slot.  Includes adjustments from the definition cost based on the current scale.
        /// </summary>
        public float moduleCost { get { return currentCost; } }

        /// <summary>
        /// Return the current usable resource volume for this module slot.  Includes adjustments from the definition volume based on the current scale.
        /// </summary>
        public float moduleVolume { get { return currentVolume; } }

        /// <summary>
        /// Return the current diameter of the model in this module slot.  This is the base diamter as specified in the model definition, modified by the currently specified scale.
        /// </summary>
        public float moduleDiameter { get { return currentDiameter; } }

        /// <summary>
        /// Return the current upper-mounting diamter of the model in this module slot.  This value is to be used for sizing/scaling of any module slot used for an upper-adapter/nose option for this slot.
        /// </summary>
        public float moduleUpperDiameter { get { return (definition.shouldInvert(orientation) ? definition.lowerDiameter : definition.upperDiameter) * currentHorizontalScale; } }

        /// <summary>
        /// Return the current lower-mounting diamter of the model in this module slot.  This value is to be used for sizing/scaling of any module slot used for a lower-adapter/mount option for this slot.
        /// </summary>
        public float moduleLowerDiameter { get { return (definition.shouldInvert(orientation) ? definition.upperDiameter : definition.lowerDiameter) * currentHorizontalScale; } }

        /// <summary>
        /// Return the current height of the model in this module slot.  Based on the definition specified height and the current vertical scale.
        /// </summary>
        public float moduleHeight { get { return currentHeight; } }

        /// <summary>
        /// Return the current x/z scaling used by the model in this module slot.
        /// </summary>
        public float moduleHorizontalScale { get { return currentHorizontalScale; } }

        /// <summary>
        /// Return the current y scaling used by the model in this module slot.
        /// </summary>
        public float moduleVerticalScale { get { return currentVerticalScale; } }

        /// <summary>
        /// Return the current origin position of the Y corrdinate of this module, in part-centric space.<para/>
        /// A value of 0 denotes origin is at the parts' origin/COM.
        /// </summary>
        public float modulePosition { get { return currentVerticalPosition; } }

        //TODO -- currently returns 0 -- needs to return a value based on current model orienation and specified 'position'
        /// <summary>
        /// Return the Y coordinate of the top-most point in the model in part-centric space, as defined by model-height in the model definition and modified by current model scale, 
        /// </summary>
        public float moduleTop { get { return 0f; } }

        /// <summary>
        /// Return the Y coordinate of the physical 'center' of this model in part-centric space.
        /// </summary>
        public float moduleCenter { get { return moduleTop - 0.5f * moduleHeight; } }

        /// <summary>
        /// Returns the Y coordinate of the bottom of this model in part-centric space.
        /// </summary>
        public float moduleBottom { get { return moduleTop - moduleHeight; } }

        /// <summary>
        /// Returns an offset that can be applied to the 'position' of this model that will specify the correct start point for a fairing. 
        /// The returned offset will be correct for the currently configured orientation,
        /// such that you can always use 'module.modulePosition + module.moduleFairingOffset' to attain the proper Y coordinate for fairing attachment.
        /// </summary>
        public float moduleFairingOffset { get { return definition.fairingData == null ? 0 : definition.fairingData.fairingOffsetFromOrigin; } }

        /// <summary>
        /// Return the currently configured custom color data for this module slot.
        /// </summary>
        public RecoloringData[] recoloringData { get { return customColors; } }

        /// <summary>
        /// Return the transforms that represent the root transforms for the models in this module slot.  Under normal circumstaces (standard single model layout), this should return an array of a single transform.
        /// </summary>
        public Transform[] moduleModelTransforms { get { return models; } }

        #endregion ENDREGION - Convenience wrappers for model definition data for external use

        #region REGION - Constructors and Init Methods

        /// <summary>
        /// Only a partial constructor.  Need to also call at least 'setupModelList' and/or 'setupModel' before the module will actually be usable.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="partModule"></param>
        /// <param name="root"></param>
        /// <param name="orientation"></param>
        /// <param name="dataFieldName"></param>
        /// <param name="modelFieldName"></param>
        /// <param name="textureFieldName"></param>
        public ModelModule(Part part, U partModule, Transform root, ModelOrientation orientation,
            string modelPersistenceFieldName, string layoutPersistenceFieldName, string texturePersistenceFieldName, string recolorPersistenceFieldName,
            string animationPersistenceFieldName, string deployLimitField, string deployEventName, string retractEventName)
        {
            this.part = part;
            this.partModule = partModule;
            this.root = root;
            this.orientation = orientation;
            this.modelField = partModule.Fields[modelPersistenceFieldName];
            this.layoutField = partModule.Fields[layoutPersistenceFieldName];
            this.textureField = partModule.Fields[texturePersistenceFieldName];
            this.dataField = partModule.Fields[recolorPersistenceFieldName];
            this.animationModule = new AnimationModule(part, partModule, animationPersistenceFieldName, deployLimitField, deployEventName, retractEventName);
            loadColors(persistentData);
        }

        /// <summary>
        /// Initialization method.  May be called to update the available model list later; if the currently selected model is invalid, it will be set to the first model in the list.<para/>
        /// Wraps the input array of model defs with a default single-position layout option wrapper.
        /// </summary>
        /// <param name="models"></param>
        public void setupModelList(ModelDefinition[] modelDefs)
        {
            if (modelDefs.Length <= 0)
            {
                MonoBehaviour.print("ERROR: No models found for: " + getErrorReportModuleName());
            }
            int len = modelDefs.Length;
            ModelDefinitionLayoutOptions[] models = new ModelDefinitionLayoutOptions[len];
            for (int i = 0; i < len; i++)
            {
                models[i] = new ModelDefinitionLayoutOptions(modelDefs[i]);
            }
            setupModelList(models);
        }

        /// <summary>
        /// Initialization method.  May be called to update the available model list later; if the currently selected model is invalid, it will be set to the first model in the list.
        /// </summary>
        /// <param name="models"></param>
        public void setupModelList(ModelDefinitionLayoutOptions[] modelDefs)
        {
            optionsCache = modelDefs;
            if (modelDefs.Length <= 0)
            {
                MonoBehaviour.print("ERROR: No models found for: " + getErrorReportModuleName());
            }
            if (!Array.Exists(optionsCache, m => m.definition.name == modelName))
            {
                MonoBehaviour.print("ERROR: Currently configured model name: " + modelName + " was not located while setting up: "+getErrorReportModuleName());
                modelName = optionsCache[0].definition.name;
                MonoBehaviour.print("Now using model: " + modelName + " for: "+getErrorReportModuleName());
            }
        }

        /// <summary>
        /// Initialization method.  Creates the model transforms, and sets their position and scale to the current config values.<para/>
        /// Initializes texture set, including 'defualts' handling.  Initializes animation module with the animation data for the current model.<para/>
        /// Only for use during part initialization.  Subsequent changes to model should call the modelSelectedXXX methods.
        /// </summary>
        public void setupModel()
        {
            SSTUUtils.destroyChildrenImmediate(root);
            currentLayoutOptions = Array.Find(optionsCache, m => m.definition.name == modelName);
            if (currentLayoutOptions == null)
            {
                MonoBehaviour.print("ERROR: Could not locate model definition for: " + modelName + " for " + getErrorReportModuleName());
            }
            currentDefinition = currentLayoutOptions.definition;
            currentLayout = currentLayoutOptions.getLayout(layoutName);
            if (!currentLayoutOptions.isValidLayout(layoutName))
            {
                MonoBehaviour.print("Existing layout: "+layoutName+" for " + getErrorReportModuleName() + " was null.  Assigning default layout: " + currentLayoutOptions.getDefaultLayout().name);
                layoutName = currentLayoutOptions.getDefaultLayout().name;
            }
            constructModels();
            positionModels();
            setupTextureSet();
            if (animationEnabled)
            {
                animationModule.setupAnimations(definition.animationData, root, animationLayer);
            }
            else
            {
                animationModule.disableAnimations();
            }
            updateModuleStats();
        }

        #endregion ENDREGION - Constructors and Init Methods

        #region REGION - Update Methods

        /// <summary>
        /// Unity lifecycle per-frame 'Update' method.  Used to update animation handling.
        /// </summary>
        public void Update()
        {
            animationModule.Update();
        }

        /// <summary>
        /// If the model definition contains rcs-thrust-transform data, will rename the model's rcs thrust transforms to match the input 'destinationName'.<para/>
        /// This allows for the model's transforms to be properly found by the ModuleRCS when it is (re)initialized.
        /// </summary>
        /// <param name="destinationName"></param>
        public void renameRCSThrustTransforms(string destinationName)
        {
            if (definition.rcsData == null)
            {
                MonoBehaviour.print("ERROR: RCS data is null for model definition: " + definition.name+" for: "+getErrorReportModuleName());
                return;
            }
            definition.rcsModuleData.renameTransforms(root, destinationName);
        }

        /// <summary>
        /// If the model definition contains engine-thrust-transform data, will rename the model's engine thrust transforms to match the input 'destinationName'.<para/>
        /// This allows for the model's transforms to be properly found by the ModuleEngines when it is (re)initialized.
        /// </summary>
        /// <param name="destinationName"></param>
        public void renameEngineThrustTransforms(string destinationName)
        {
            if (definition.engineTransformData == null)
            {
                MonoBehaviour.print("ERROR: Engine transform data is null for model definition: " + definition.name + " for: "+getErrorReportModuleName());
                return;
            }
            definition.engineTransformData.renameThrustTransforms(root, destinationName);
        }

        /// <summary>
        /// If the model definition contains gimbal-transform data, will rename the model's gimbal transforms to match the input 'destinationName'.<para/>
        /// This allows for the model's transforms to be properly found by the ModuleGimbal when it is (re)initialized.
        /// </summary>
        /// <param name="destinationName"></param>
        public void renameGimbalTransforms(string destinationName)
        {
            if (definition.engineTransformData == null)
            {
                MonoBehaviour.print("ERROR: Engine transform data is null for model definition: " + definition.name+" for: "+getErrorReportModuleName());
                return;
            }
            definition.engineTransformData.renameGimbalTransforms(root, destinationName);
        }

        /// <summary>
        /// Return an array populated with copies of the solar data for this model, one copy for each model position in the current layout.
        /// </summary>
        /// <returns></returns>
        public ModelSolarData[] getSolarData()
        {
            int len = layout.positions.Length;
            ModelSolarData[] msds = new ModelSolarData[len];
            for (int i = 0; i < len; i++)
            {
                msds[i] = definition.solarData;
            }
            return msds;
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
            actionWithSymmetry(m =>
            {
                m.textureSetName = textureSetName;
                m.applyTextureSet(m.textureSetName, !SSTUGameSettings.persistRecolor());
                if (m.textureField != null)
                {
                    m.partModule.updateUIChooseOptionControl(m.textureField.name, m.definition.getTextureSetNames(), m.definition.getTextureSetTitles(), true, m.textureSetName);
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
        /// Symmetry-enabled method.  Should only be called when symmetry updates are desired.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="oldValue"></param>
        public void layoutSelected(BaseField field, System.Object oldValue)
        {
            actionWithSymmetry(m =>
            {
                if (m != this) { m.layoutName = layoutName; }
                m.layoutSelected(m.layoutName);
            });
        }

        /// <summary>
        /// Symmetry enabled.  Updates the current persistent color data, and reapplies the textures/color data to the models materials.
        /// </summary>
        /// <param name="colors"></param>
        public void setSectionColors(RecoloringData[] colors)
        {
            actionWithSymmetry(m =>
            {
                m.textureSetName = textureSetName;
                m.customColors = colors;
                m.enableTextureSet();
                m.saveColors(m.customColors);
            });
        }

        /// <summary>
        /// NON-Symmetry enabled method.<para/>
        /// Sets the currently selected model name to the input model, and setup
        /// </summary>
        /// <param name="newModel"></param>
        public void modelSelected(string newModel)
        {
            if (Array.Exists(optionsCache, m => m.definition.name == newModel))
            {
                modelName = newModel;
                setupModel();
            }
            else
            {
                MonoBehaviour.print("ERROR: No model definition found for input name: " + newModel+ " for: "+getErrorReportModuleName());
            }
        }

        /// <summary>
        /// NON-Symmetry enabled method.  Sets the current layout and updates models for current layout.  Uses current vertical position/all other current position data.
        /// </summary>
        /// <param name="newLayout"></param>
        public void layoutSelected(string newLayout)
        {
            if (!currentLayoutOptions.isValidLayout(newLayout))
            {
                newLayout = currentLayoutOptions.getDefaultLayout().name;
                MonoBehaviour.print("ERROR: Could not find layout definition by name: " + newLayout + " using default layout for model: " + getErrorReportModuleName());
            }
            layoutName = newLayout;
            currentLayout = currentLayoutOptions.getLayout(newLayout);
            setupModel();
            updateSelections();
        }

        /// <summary>
        /// NON-Symmetry enabled method.<para/>
        /// Updates the UI controls for the currently available models specified through setupModelList.<para/>
        /// Also updates the texture-set selection widget options and visibility (only if texture set backing field is not null)
        /// </summary>
        public void updateSelections()
        {
            ModelDefinitionLayoutOptions[] availableOptions = getValidOptions();
            MonoBehaviour.print("Updating selections for: " + getErrorReportModuleName() + " found: " + availableOptions.Length + " options.");
            MonoBehaviour.print(SSTUUtils.printArray(SSTUUtils.getNames(availableOptions, m => m.definition.name), ","));
            string[] names = SSTUUtils.getNames(availableOptions, s => s.definition.name);
            string[] displays = SSTUUtils.getNames(availableOptions, s => s.definition.title);
            partModule.updateUIChooseOptionControl(modelField.name, names, displays, true, modelName);
            modelField.guiActiveEditor = names.Length > 1;
            //updates the texture set selection for the currently configured model definition, including disabling of the texture-set selection UI when needed
            if (textureField != null)
            {
                partModule.updateUIChooseOptionControl(textureField.name, definition.getTextureSetNames(), definition.getTextureSetTitles(), true, textureSetName);
            }
            if (layoutField != null)
            {
                ModelDefinitionLayoutOptions mdlo = optionsCache.Find(m => m.definition == definition);
                string[] layoutNames = mdlo.getLayoutNames();
                string[] layoutTitles = mdlo.getLayoutTitles();
                partModule.updateUIChooseOptionControl(layoutField.name, layoutNames, layoutTitles, true, layoutName);
                layoutField.guiActiveEditor = layoutField.guiActiveEditor && currentLayout.positions.Length > 1;
            }
        }
        
        /// <summary>
        /// NON-symmetry enabled method.
        /// Updates the current models with the current scale and position data.
        /// </summary>
        public void updateModelMeshes()
        {
            updateModelScaleAndPosition();
        }

        /// <summary>
        /// Updates the diamter/scale values so that the upper-diameter of this model matches the input diamter
        /// </summary>
        /// <param name="newDiameter"></param>
        public void setDiameterFromAbove(float newDiameter)
        {
            float baseUpperDiameter = definition.shouldInvert(orientation) ? definition.lowerDiameter : definition.upperDiameter;
            float scale = newDiameter / baseUpperDiameter;
            setScale(scale);
        }

        /// <summary>
        /// Updates the diamter/scale values so that the lower-diameter of this model matches the input diamter
        /// </summary>
        /// <param name="newDiameter"></param>
        public void setDiameterFromBelow(float newDiameter)
        {
            float baseLowerDiameter = definition.shouldInvert(orientation) ? definition.upperDiameter : definition.lowerDiameter;
            float scale = newDiameter / baseLowerDiameter;
            setScale(scale);
        }

        /// <summary>
        /// Updates the diameter/scale values so that the core-diameter of this model matches the input diameter
        /// </summary>
        /// <param name="newDiameter"></param>
        public void setScaleForDiameter(float newDiameter)
        {
            float newScale = newDiameter / definition.diameter;
            setScale(newScale);
        }

        /// <summary>
        /// Updates the current internal scale values for the input diameter and height values.
        /// </summary>
        /// <param name="newHeight"></param>
        /// <param name="newDiameter"></param>
        public void setScaleForHeightAndDiameter(float newHeight, float newDiameter)
        {
            float newHorizontalScale = newDiameter / definition.diameter;
            float newVerticalScale = newHeight / definition.height;
            setScale(newHorizontalScale, newVerticalScale);
        }

        /// <summary>
        /// Updates the current internal scale values for the input scale.  Sets x,y,z scale to the input value specified.
        /// </summary>
        /// <param name="newScale"></param>
        public void setScale(float newScale)
        {
            setScale(newScale, newScale);
        }

        /// <summary>
        /// Updates the current internal scale values for the input scales.  Updates x,z with the 'horizontal scale' and updates 'y' with the 'vertical scale'.
        /// </summary>
        /// <param name="newHorizontalScale"></param>
        /// <param name="newVerticalScale"></param>
        public void setScale(float newHorizontalScale, float newVerticalScale)
        {
            currentHorizontalScale = newHorizontalScale;
            currentVerticalScale = newVerticalScale;
            currentHeight = newVerticalScale * definition.height;
            currentDiameter = newHorizontalScale * definition.diameter;
            updateModuleStats();
        }

        #endregion ENDREGION - GUI Interaction Methods

        #region REGION - Private/Internal methods

        /// <summary>
        /// Update the cached volume, mass, and cost values for the currently configured model setup.  Must be called anytime that model definition or scales are changed.
        /// </summary>
        private void updateModuleStats()
        {
            float scalar = moduleHorizontalScale * moduleHorizontalScale * moduleVerticalScale;
            currentMass = definition.mass * scalar;
            currentCost = definition.cost * scalar;
            currentVolume = definition.volume * scalar;
        }

        /// <summary>
        /// Load custom colors from persistent color data.  Creates default array of colors if no data is present persistence.
        /// </summary>
        /// <param name="data"></param>
        private void loadColors(string data)
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
                customColors = new RecoloringData[3];
                customColors[0] = new RecoloringData(Color.white, 1, 1);
                customColors[1] = new RecoloringData(Color.white, 1, 1);
                customColors[2] = new RecoloringData(Color.white, 1, 1);
            }
        }

        /// <summary>
        /// Save the current custom color data to persistent data in part-module.
        /// </summary>
        /// <param name="colors"></param>
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

        /// <summary>
        /// Updates the input texture-control text field with the texture-set names for this model.  Disables field if no texture sets found, enables field if more than one texture set is available.
        /// </summary>
        public void updateTextureUIControl()
        {
            if (textureField == null) { return; }
            string[] names = definition.getTextureSetNames();
            string[] titles = definition.getTextureSetTitles();
            partModule.updateUIChooseOptionControl(textureField.name, names, titles, true, textureSetName);
            textureField.guiActiveEditor = names.Length > 1;
        }

        //TODO rewrite for new offset handling
        /// <summary>
        /// Updates the internal position reference for the input position
        /// Includes offsetting for the models offset; the input position should be the desired location
        /// of the bottom of the model.
        /// </summary>
        /// <param name="positionOfBottomOfModel"></param>
        public virtual void setPosition(float positionOfBottomOfModel, ModelOrientation orientation = ModelOrientation.TOP)
        {
            float offset = getVerticalOffset();
            if (orientation == ModelOrientation.BOTTOM) { offset = -offset; }
            else if (orientation == ModelOrientation.CENTRAL) { offset += currentHeight * 0.5f; }
            currentVerticalPosition = positionOfBottomOfModel + offset;
        }

        //TODO rewrite for new offset handling
        /// <summary>
        /// Updates the attach nodes on the part for the input list of attach nodes and the current specified nodes for this model.
        /// Any 'extra' attach nodes from the part will be disabled.
        /// </summary>
        /// <param name="nodeNames"></param>
        /// <param name="userInput"></param>
        public void updateAttachNodes(String[] nodeNames, bool userInput)
        {
            if (nodeNames == null || nodeNames.Length < 1) { return; }
            if (nodeNames.Length == 1 && (nodeNames[0] == "NONE" || nodeNames[0] == "none")) { return; }
            float currentVerticalPosition = this.currentVerticalPosition;
            float offset = getVerticalOffset();
            if (orientation == ModelOrientation.BOTTOM) { offset = -offset; }
            currentVerticalPosition -= offset;

            AttachNode node = null;
            AttachNodeBaseData data;

            int nodeCount = definition.attachNodeData.Length;
            int len = nodeNames.Length;

            Vector3 pos = Vector3.zero;
            Vector3 orient = Vector3.up;
            int size = 4;

            bool invert = definition.shouldInvert(orientation);
            for (int i = 0; i < len; i++)
            {
                node = part.FindAttachNode(nodeNames[i]);
                if (i < nodeCount)
                {
                    data = definition.attachNodeData[i];
                    size = Mathf.RoundToInt(data.size * currentHorizontalScale);
                    pos = data.position * currentVerticalScale;
                    if (invert)
                    {
                        pos.y = -pos.y;
                        pos.x = -pos.x;
                    }
                    pos.y += currentVerticalPosition;
                    orient = data.orientation;
                    if (invert) { orient = -orient; orient.z = -orient.z; }
                    if (node == null)//create it
                    {
                        SSTUAttachNodeUtils.createAttachNode(part, nodeNames[i], pos, orient, size);
                    }
                    else//update its position
                    {
                        SSTUAttachNodeUtils.updateAttachNodePosition(part, node, pos, orient, userInput);
                    }
                }
                else//extra node, destroy
                {
                    if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
                    {
                        SSTUAttachNodeUtils.destroyAttachNode(part, node);
                    }
                }
            }
        }

        /// <summary>
        /// Applies the currently selected texture set.  Does not validate anything.
        /// </summary>
        /// <param name="setName"></param>
        private void enableTextureSet()
        {
            if (string.IsNullOrEmpty(textureSetName) || textureSetName == "none" )
            {
                return;
            }
            TextureSet textureSet = this.textureSet;
            if (textureSet != null)
            {
                textureSet.enable(root, customColors);
            }
        }

        /// <summary>
        /// Initialization method.  Validates the current texture set selection, assigns default set if current selection is invalid.
        /// </summary>
        private void setupTextureSet()
        {
            bool useDefaultTextureColors = false;
            if (!isValidTextureSet(textureSetName))
            {
                TextureSet def = definition.getDefaultTextureSet();
                textureSetName = def == null ? "none" : def.name;
                if (!isValidTextureSet(textureSetName))
                {
                    MonoBehaviour.print("ERROR: Default texture set: " + textureSetName + " set for model: " + definition.name + " is invalid.  This is a configuration level error in the model definition that needs to be corrected.  Bad things are about to happen....");
                }
                useDefaultTextureColors = true;
            }
            else if (customColors == null || customColors.Length == 0)
            {
                useDefaultTextureColors = true;
            }
            applyTextureSet(textureSetName, useDefaultTextureColors);
        }

        /// <summary>
        /// Updates recoloring data for the input texture set, applies the texture set to the model, updates UI controls for the current texture set selection.<para/>
        /// Should be called whenever a new model is selected, or when a new texture set for the current model is chosen.
        /// </summary>
        /// <param name="setName"></param>
        /// <param name="useDefaultColors"></param>
        private void applyTextureSet(string setName, bool useDefaultColors)
        {
            textureSetName = setName;
            TextureSet textureSet = this.textureSet;
            if (useDefaultColors || textureSet == null)
            {
                if (textureSet != null && textureSet.maskColors != null && textureSet.maskColors.Length > 0)
                {
                    customColors = new RecoloringData[3];
                    customColors[0] = textureSet.maskColors[0];
                    customColors[1] = textureSet.maskColors[1];
                    customColors[2] = textureSet.maskColors[2];
                }
                else//invalid colors or texture set, create default placeholder color array
                {
                    RecoloringData placeholder = new RecoloringData(Color.white, 1, 1);
                    customColors = new RecoloringData[] { placeholder, placeholder, placeholder };
                }
                saveColors(customColors);
            }
            enableTextureSet();
            updateTextureUIControl();
            SSTUModInterop.onPartTextureUpdated(part);
        }
        
        /// <summary>
        /// Loops through the individual model instances and updates their position, rotation, and scale, for the currently configured ModelLayoutData
        /// </summary>
        private void positionModels()
        {
            int len = layout.positions.Length;
            float posScalar = getLayoutPositionScalar();
            float scaleScalar = getLayoutScaleScalar();
            float rotation = getFacingRotation();
            //TODO -- might not need this
            //invert the rotation around Y-axis when model itself is inverted
            if (definition.shouldInvert(orientation))
            {
                rotation = -rotation;
            }
            for (int i = 0; i < len; i++)
            {
                Transform model = models[i];
                ModelPositionData mpd = layout.positions[i];
                model.transform.localPosition = mpd.localPosition * posScalar;
                model.transform.localRotation = Quaternion.Euler(mpd.localRotation + Vector3.up * rotation);
                model.transform.localScale = mpd.localScale * scaleScalar;
            }
        }

        /// <summary>
        /// Constructs all of the models for the current ModelDefinition and ModelLayoutData
        /// </summary>
        private void constructModels()
        {
            //reset the orientation on the root transform, in case it was rotated by previous invert/etc
            root.transform.localRotation = Quaternion.identity;
            MonoBehaviour.print("Creating models for layout: " + currentLayoutOptions);
            MonoBehaviour.print("layout: " + layoutName);
            //create model array with length based on the positions defined in the ModelLayoutData
            int len = layout.positions.Length;
            models = new Transform[len];
            for (int i = 0; i < len; i++)
            {
                models[i] = new GameObject("ModelModule-" + i).transform;
                models[i].NestToParent(root);
                constructSubModels(models[i]);
            }
            //figure out the rotation needed in order to make this model conform to the Z-Plus = Forward standard
            //as well as its currently used orientation in cases where it is used on top or bottom.
            bool shouldInvert = definition.shouldInvert(orientation);
            Vector3 rotation = shouldInvert ? definition.invertAxis * 180f : Vector3.zero;            
            root.transform.localRotation = Quaternion.Euler(rotation);
        }
        
        /// <summary>
        /// Constructs a single model instance from the model definition, parents it to the input transform.<para/>
        /// Does not position or orient the created model; positionModels() should be called to update its position for the current ModelLayoutData configuration
        /// </summary>
        /// <param name="parent"></param>
        private void constructSubModels(Transform parent)
        {
            SubModelData[] smds = definition.subModelData;
            SubModelData smd;
            GameObject clonedModel;
            Transform localParent;
            int len = smds.Length;
            //add sub-models to the input model transform
            for (int i = 0; i < len; i++)
            {
                smd = smds[i];
                clonedModel = SSTUUtils.cloneModel(smd.modelURL);
                if (clonedModel == null)
                {
                    MonoBehaviour.print("ERROR: Could not clone model for url: " + smd.modelURL + " while constructing meshes for model definition" + definition.name+" for: "+getErrorReportModuleName());
                    continue;
                }
                clonedModel.transform.NestToParent(parent.transform);
                clonedModel.transform.localRotation = Quaternion.Euler(smd.rotation);
                clonedModel.transform.localPosition = smd.position;
                clonedModel.transform.localScale = smd.scale;
                if (!string.IsNullOrEmpty(smd.parent))
                {
                    localParent = parent.transform.FindRecursive(smd.parent);
                    if (localParent != null)
                    {
                        clonedModel.transform.parent = localParent;
                    }
                }
                //de-activate any non-active sub-model transforms
                //iterate through all transforms for the model and deactivate(destroy?) any not on the active mesh list
                smd.setupSubmodel(clonedModel);
            }
            if (definition.mergeData != null)
            {
                MeshMergeData[] md = definition.mergeData;
                len = md.Length;
                for (int i = 0; i < len; i++)
                {
                    md[i].mergeMeshes(parent);
                }
            }
        }
        
        /// <summary>
        /// Applies the current module position and scale values to the root transform of the ModelModule.  Does not adjust rotation or handle multi-model positioning setup for layouts.
        /// </summary>
        private void updateModelScaleAndPosition()
        {
            int len = models.Length;
            if (definition.compoundModelData != null)
            {
                definition.compoundModelData.setHeightFromScale(definition, root.gameObject, currentHorizontalScale, currentVerticalScale, definition.orientation);
            }
            else
            {
                root.transform.localScale = new Vector3(currentHorizontalScale, currentVerticalScale, currentHorizontalScale);
            }
            root.transform.localPosition = new Vector3(0, currentVerticalPosition, 0);
        }
        
        /// <summary>
        /// Return the current vertical offset applied to the model, adjusted for model scale, model orientation, and module-slot orientation.
        /// </summary>
        /// <returns></returns>
        private float getVerticalOffset()
        {
            //raw offset, unadjusted for orientation
            float offset = 0;
            if (orientation == ModelOrientation.TOP)
            {
                if (definition.orientation == ModelOrientation.TOP)
                {
                    offset = definition.verticalOffset * currentVerticalScale;
                }
                else if (definition.orientation == ModelOrientation.CENTRAL)
                {
                    offset = (definition.verticalOffset + definition.height * 0.5f) * currentVerticalScale;
                }
                else if (definition.orientation == ModelOrientation.BOTTOM)
                {
                    offset = -definition.verticalOffset * currentVerticalScale;
                }
            }
            else if (orientation == ModelOrientation.CENTRAL)
            {
                if (definition.orientation == ModelOrientation.TOP)
                {
                    offset = (definition.verticalOffset - definition.height * 0.5f) * currentVerticalScale;
                }
                else if (definition.orientation == ModelOrientation.CENTRAL)
                {
                    offset = definition.verticalOffset * currentVerticalScale;
                }
                else if (definition.orientation == ModelOrientation.BOTTOM)
                {
                    offset = (definition.verticalOffset + definition.height * 0.5f) * currentVerticalScale;
                }
            }
            else if (orientation == ModelOrientation.BOTTOM)
            {
                if (definition.orientation == ModelOrientation.TOP)
                {
                    offset = -definition.verticalOffset * currentVerticalScale;
                }
                else if (definition.orientation == ModelOrientation.CENTRAL)
                {
                    offset = (definition.verticalOffset - definition.height * 0.5f) * currentVerticalScale;
                }
                else if (definition.orientation == ModelOrientation.BOTTOM)
                {
                    offset = definition.verticalOffset * currentVerticalScale;
                }
            }
            return offset;
        }

        /// <summary>
        /// Return true/false if the input texture set name is a valid texture set for this model definition.
        /// <para/>
        /// If the model does not contain any defined texture sets, return true if the input name is 'default' or 'none'
        /// otherwise, examine the array of texture sets and return true/false depending on if the input name was found
        /// in the defined sets.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private bool isValidTextureSet(String val)
        {
            if (definition.textureSets.Length == 0)
            {
                return val == "none" || val == "default";
            }
            return definition.textureSets.Exists(m => m.name == val);
        }

        /// <summary>
        /// Determine if the number of parts attached to the part will prevent this mount from being applied;
        /// if any node that has a part attached would be deleted, return false.  To be used for GUI validation
        /// to determine what modules are valid 'swap' selections for the current part setup.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="nodeNames"></param>
        /// <returns></returns>
        private bool canSwitchTo(Part part, String[] nodeNames)
        {
            AttachNode node;
            int len = nodeNames.Length;
            for (int i = 0; i < len; i++)
            {
                if (i < definition.attachNodeData.Length) { continue; }//don't care about those nodes, they will be present
                node = part.FindAttachNode(nodeNames[i]);//this is a node that would be disabled
                if (node == null) { continue; }//already disabled, and that is just fine
                else if (node.attachedPart != null) { return false; }//drat, this node is scheduled for deletion, but has a part attached; cannot delete it, so cannot switch to this mount
            }
            return true;//and if all node checks go okay, return true by default...
        }

        /// <summary>
        /// Internal utility method to allow accessing of symmetry ModelModules' in symmetry parts/part-modules
        /// </summary>
        /// <param name="action"></param>
        private void actionWithSymmetry(Action<ModelModule<U>> action)
        {
            action(this);
            int index = part.Modules.IndexOf(partModule);
            foreach (Part p in part.symmetryCounterparts)
            {
                action(getSymmetryModule((U)p.Modules[index]));
            }
        }

        /// <summary>
        /// Return the number of degrees that the model needs to be rotated around the Y axis to conform to the Z+ = forward standard
        /// </summary>
        /// <returns></returns>
        private float getFacingRotation()
        {
            float rotation = 0f;
            switch (definition.facing)
            {
                case Axis.XPlus:
                    rotation -= 90f;//TODO might be +90
                    break;
                case Axis.XNeg:
                    rotation += 90f;//TODO might be -90
                    break;
                case Axis.YPlus:
                    //undefined
                    break;
                case Axis.YNeg:
                    //undefined
                    break;
                case Axis.ZPlus:
                    //noop
                    break;
                case Axis.ZNeg:
                    rotation += 180f;
                    break;
                default:
                    break;
            }
            return rotation;
        }

        /// <summary>
        /// Return a string representing the module name and other debug related information.  Used in error logging.
        /// </summary>
        /// <returns></returns>
        private string getErrorReportModuleName()
        {
            return "ModelModule: " + moduleName + " using model: " +definition+ " in orientation: " + orientation + " in module: " + partModule + " in part: " + part;
        }

        #endregion ENDREGION - Private/Internal methods

        #region REGION - Module Linking

        /// <summary>
        /// Return an array with containing the models that are valid options for use as upper-adapters for the currently
        /// selected/enabled model definition.
        /// </summary>
        /// <param name="inputOptions"></param>
        /// <returns></returns>
        public ModelDefinitionLayoutOptions[] getValidUpperModels(ModelDefinitionLayoutOptions[] inputOptions, ModelOrientation otherModelOrientation)
        {
            List<ModelDefinitionLayoutOptions> validDefs = new List<ModelDefinitionLayoutOptions>();
            ModelDefinitionLayoutOptions def;
            int len = inputOptions.Length;
            for (int i = 0; i < len; i++)
            {
                def = inputOptions[i];
                if (definition.isValidUpperProfile(def.definition.getLowerProfiles(otherModelOrientation), orientation))
                {
                    validDefs.Add(def);
                }
            }
            return validDefs.ToArray();
        }

        /// <summary>
        /// Return an array with containing the models that are valid options for use as lower-adapters for the currently
        /// selected/enabled model definition.
        /// </summary>
        /// <param name="defs"></param>
        /// <returns></returns>
        public ModelDefinitionLayoutOptions[] getValidLowerModels(ModelDefinitionLayoutOptions[] defs, ModelOrientation otherModelOrientation)
        {
            List<ModelDefinitionLayoutOptions> validDefs = new List<ModelDefinitionLayoutOptions>();
            ModelDefinitionLayoutOptions def;
            int len = defs.Length;
            for (int i = 0; i < len; i++)
            {
                def = defs[i];
                if (definition.isValidLowerProfile(def.definition.getUpperProfiles(otherModelOrientation), orientation))
                {
                    validDefs.Add(def);
                }
            }
            return validDefs.ToArray();
        }

        /// <summary>
        /// Returns if the input model definition is valid for being attached to the upper of this module, using the input orientation for the input definition
        /// </summary>
        /// <param name="def"></param>
        /// <param name="otherModelOrientation"></param>
        /// <returns></returns>
        public bool isValidUpper(ModelDefinition def, ModelOrientation otherModelOrientation)
        {
            return definition.isValidUpperProfile(def.getLowerProfiles(otherModelOrientation), orientation);
        }

        /// <summary>
        /// Return if the input model-module is configured properly to be used as an upper attach option for this current modules configuration.
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        public bool isValidUpper(ModelModule<U> module)
        {
            return isValidUpper(module.definition, module.orientation);
        }

        /// <summary>
        /// Returns if the input model definition is vaild for being attached to the lower of this module, using the input orientation for the input definition
        /// </summary>
        /// <param name="def"></param>
        /// <param name="otherModelOrientation"></param>
        /// <returns></returns>
        public bool isValidLower(ModelDefinition def, ModelOrientation otherModelOrientation)
        {
            return definition.isValidLowerProfile(def.getUpperProfiles(otherModelOrientation), orientation);
        }

        /// <summary>
        /// Return if the input model-module is configured properly to be used as a lower attach option for this current modules configuration.
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        public bool isValidLower(ModelModule<U> module)
        {
            return isValidLower(module.definition, module.orientation);
        }

        /// <summary>
        /// Returns the first model found from the input module that is valid to be used for an upper attachment for this module
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        public ModelDefinition findFirstValidUpper(ModelModule<U> module)
        {
            int len = module.optionsCache.Length;
            for (int i = 0; i < len; i++)
            {
                if (isValidUpper(module.optionsCache[i].definition, module.orientation))
                {
                    return module.optionsCache[i].definition;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the first model found from the input module that is valid to be used for a lower attachment for this module
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        public ModelDefinition findFirstValidLower(ModelModule<U> module)
        {
            int len = module.optionsCache.Length;
            for (int i = 0; i < len; i++)
            {
                if (isValidLower(module.optionsCache[i].definition, module.orientation))
                {
                    return module.optionsCache[i].definition;
                }
            }
            return null;
        }

        #endregion ENDREGION - Module Linking

    }

    //TODO -- these classes really belong alongside the ModelDefintion class
    //TODO -- move these all somewhere more logical

    /// <summary>
    /// Class denoting a the transforms to use from a single database model.  Allows for combining multiple entire models, and/or transforms from models, all into a single active/usable Model
    /// </summary>
    public class SubModelData
    {

        public readonly string modelURL;
        public readonly string[] modelMeshes;
        public readonly string[] renameMeshes;
        public readonly string parent;
        public readonly Vector3 rotation;
        public readonly Vector3 position;
        public readonly Vector3 scale;

        public SubModelData(ConfigNode node)
        {
            modelURL = node.GetStringValue("modelName");
            modelMeshes = node.GetStringValues("transform");
            renameMeshes = node.GetStringValues("rename");
            parent = node.GetStringValue("parent", string.Empty);
            position = node.GetVector3("position", Vector3.zero);
            rotation = node.GetVector3("rotation", Vector3.zero);
            scale = node.GetVector3("scale", Vector3.one);
        }

        public SubModelData(string modelURL, string[] meshNames, string parent, Vector3 pos, Vector3 rot, Vector3 scale)
        {
            this.modelURL = modelURL;
            this.modelMeshes = meshNames;
            this.renameMeshes = new string[0];
            this.parent = parent;
            this.position = pos;
            this.rotation = rot;
            this.scale = scale;
        }

        public void setupSubmodel(GameObject modelRoot)
        {
            if (modelMeshes.Length > 0)
            {
                Transform[] trs = modelRoot.transform.GetAllChildren();
                List<Transform> toKeep = new List<Transform>();
                List<Transform> toCheck = new List<Transform>();
                int len = trs.Length;
                for (int i = 0; i < len; i++)
                {
                    if (trs[i] == null)
                    {
                        continue;
                    }
                    else if (isActiveMesh(trs[i].name))
                    {
                        toKeep.Add(trs[i]);
                    }
                    else
                    {
                        toCheck.Add(trs[i]);
                    }
                }
                List<Transform> transformsToDelete = new List<Transform>();
                len = toCheck.Count;
                for (int i = 0; i < len; i++)
                {
                    if (!isParent(toCheck[i], toKeep))
                    {
                        transformsToDelete.Add(toCheck[i]);
                    }
                }
                len = transformsToDelete.Count;
                for (int i = 0; i < len; i++)
                {
                    GameObject.DestroyImmediate(transformsToDelete[i].gameObject);
                }
            }
            if (renameMeshes.Length > 0)
            {
                string[] split;
                string oldName, newName;
                int len = renameMeshes.Length;
                for (int i = 0; i < len; i++)
                {
                    split = renameMeshes[i].Split(',');
                    if (split.Length < 2)
                    {
                        MonoBehaviour.print("ERROR: Mesh rename format invalid, must specify <oldName>,<newName>");
                        continue;
                    }
                    oldName = split[0].Trim();
                    newName = split[1].Trim();
                    Transform[] trs = modelRoot.transform.FindChildren(oldName);
                    int len2 = trs.Length;
                    for (int k = 0; k < len2; k++)
                    {
                        trs[k].name = newName;
                    }
                }
            }
        }

        private bool isActiveMesh(string transformName)
        {
            int len = modelMeshes.Length;
            bool found = false;
            for (int i = 0; i < len; i++)
            {
                if (modelMeshes[i] == transformName)
                {
                    found = true;
                    break;
                }
            }
            return found;
        }

        private bool isParent(Transform toCheck, List<Transform> children)
        {
            int len = children.Count;
            for (int i = 0; i < len; i++)
            {
                if (children[i].isParent(toCheck)) { return true; }
            }
            return false;
        }
    }

    /// <summary>
    /// Data class for specifying which meshes should be merged into singular mesh instances.
    /// For use in game-object reduction for models composited from many sub-meshes.
    /// </summary>
    public class MeshMergeData
    {

        /// <summary>
        /// The name of the transform to parent the merged meshes into.
        /// </summary>
        public readonly string parentTransform;

        /// <summary>
        /// The name of the transform to merge the specified meshes into.  If this transform is not present, it will be created.  
        /// Will be parented to 'parentTransform' if that field is populated, else it will become the 'root' transform in the model.
        /// </summary>
        public readonly string targetTransform;

        /// <summary>
        /// The names of the meshes to merge into the target transform.
        /// </summary>
        public readonly string[] meshNames;

        public MeshMergeData(ConfigNode node)
        {
            parentTransform = node.GetStringValue("parent", string.Empty);
            targetTransform = node.GetStringValue("target", "MergedMesh");
            meshNames = node.GetStringValues("mesh");
        }

        /// <summary>
        /// Given the input root transform for a fully assembled model (e.g. from sub-model-data),
        /// locate any transforms that should be merged, merge them into the specified target transform,
        /// and parent them to the specified parent transform (or root if NA).
        /// </summary>
        /// <param name="root"></param>
        public void mergeMeshes(Transform root)
        {
            //find target transform
            //create if it doesn't exist
            Transform target = root.FindRecursive(targetTransform);
            if (target == null)
            {
                target = new GameObject(targetTransform).transform;
                target.NestToParent(root);
            }

            //locate mesh filter on target transform
            //add a new one if not already present
            MeshFilter mf = target.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = target.gameObject.AddComponent<MeshFilter>();
                mf.mesh = new Mesh();
            }

            Material material = null;

            //merge meshes into singular mesh object
            //copy material/rendering settings from one of the original meshes
            List<CombineInstance> cis = new List<CombineInstance>();
            CombineInstance ci;
            Transform[] trs;
            int len = meshNames.Length;
            int trsLen;
            MeshFilter mm;
            for (int i = 0; i < len; i++)
            {
                trs = root.FindChildren(meshNames[i]);
                trsLen = trs.Length;
                for (int k = 0; k < trsLen; k++)
                {
                    //locate mesh filter from specified mesh(es)
                    mm = trs[k].GetComponent<MeshFilter>();
                    //if mesh did not exist, skip it 
                    //TODO log error on missing mesh on specified transform
                    if (mm == null) { continue; }
                    ci = new CombineInstance();
                    ci.mesh = mm.sharedMesh;
                    ci.transform = trs[k].localToWorldMatrix;
                    cis.Add(ci);
                    //if we don't currently have a reference to a material, grab a ref to/copy of the shared material
                    //for the current mesh(es).  These must all use the same materials
                    if (material == null)
                    {
                        Renderer mr = trs[k].GetComponent<Renderer>();
                        material = mr.material;//grab a NON-shared material reference
                    }
                }
            }
            mf.mesh.CombineMeshes(cis.ToArray());

            //update the material for the newly combined mesh
            //add mesh-renderer component if necessary
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = target.gameObject.AddComponent<MeshRenderer>();
            }
            renderer.sharedMaterial = material;

            //parent the new output GO to the specified parent
            //or parent target transform to the input root if no parent is specified
            if (!string.IsNullOrEmpty(parentTransform))
            {
                Transform parent = root.FindRecursive(parentTransform);
            }
            else
            {
                target.parent = root;
            }
        }

    }

    /// <summary>
    /// Data that defines how a compound model scales and updates its height with scale changes.
    /// </summary>
    public class CompoundModelData
    {
        /*
            Compound Model Definition and Manipulation

            Compound Model defines the following information for all transforms in the model that need position/scale updated:
            * total model height - combined height of the model at its default diameter.
            * height - of the meshes of the transform at default scale
            * canScaleHeight - if this particular transform is allowed to scale its height
            * index - index of the transform in the model, working from origin outward.
            * v-scale axis -- in case it differs from Y axis

            Updating the height on a Compound Model will do the following:
            * Inputs - vertical scale, horizontal scale
            * Calculate the desired height from the total model height and input vertical scale factor
            * Apply horizontal scaling directly to all transforms.  
            * Apply horizontal scale factor to the vertical scale for non-v-scale enabled meshes (keep aspect ratio of those meshes).
            * From total desired height, subtract the height of non-scaleable meshes.
            * The 'remainderTotalHeight' is then divided proportionately between all remaining scale-able meshes.
            * Calculate needed v-scale for the portion of height needed for each v-scale-able mesh.
         */
        CompoundModelTransformData[] compoundTransformData;

        public CompoundModelData(ConfigNode node)
        {
            ConfigNode[] trNodes = node.GetNodes("TRANSFORM");
            int len = trNodes.Length;
            compoundTransformData = new CompoundModelTransformData[len];
            for (int i = 0; i < len; i++)
            {
                compoundTransformData[i] = new CompoundModelTransformData(trNodes[i]);
            }
        }

        public void setHeightExplicit(ModelDefinition def, GameObject root, float dScale, float height, ModelOrientation orientation)
        {
            float vScale = height / def.height;
            setHeightFromScale(def, root, dScale, vScale, orientation);
        }

        public void setHeightFromScale(ModelDefinition def, GameObject root, float dScale, float vScale, ModelOrientation orientation)
        {
            float desiredHeight = def.height * vScale;
            float staticHeight = getStaticHeight() * dScale;
            float neededScaleHeight = desiredHeight - staticHeight;

            //iterate through scaleable transforms, calculate total height of scaleable transforms; use this height to determine 'percent share' of needed scale height for each transform
            int len = compoundTransformData.Length;
            float totalScaleableHeight = 0f;
            for (int i = 0; i < len; i++)
            {
                totalScaleableHeight += compoundTransformData[i].canScaleHeight ? compoundTransformData[i].height : 0f;
            }

            float pos = 0f;//pos starts at origin, is incremented according to transform height along 'dir'
            float dir = orientation == ModelOrientation.BOTTOM ? -1f : 1f;//set from model orientation, either +1 or -1 depending on if origin is at botom or top of model (ModelOrientation.TOP vs ModelOrientation.BOTTOM)
            float localVerticalScale = 1f;
            Transform[] trs;
            int len2;
            float percent, scale, height;

            for (int i = 0; i < len; i++)
            {
                percent = compoundTransformData[i].canScaleHeight ? compoundTransformData[i].height / totalScaleableHeight : 0f;
                height = percent * neededScaleHeight;
                scale = height / compoundTransformData[i].height;

                trs = root.transform.FindChildren(compoundTransformData[i].name);
                len2 = trs.Length;
                for (int k = 0; k < len2; k++)
                {
                    trs[k].localPosition = compoundTransformData[i].vScaleAxis * (pos + compoundTransformData[i].offset * dScale);
                    if (compoundTransformData[i].canScaleHeight)
                    {
                        pos += dir * height;
                        localVerticalScale = scale;
                    }
                    else
                    {
                        pos += dir * dScale * compoundTransformData[i].height;
                        localVerticalScale = dScale;
                    }
                    trs[k].localScale = getScaleVector(dScale, localVerticalScale, compoundTransformData[i].vScaleAxis);
                }
            }
        }

        /// <summary>
        /// Returns a vector representing the 'localScale' of a transform, using the input 'axis' as the vertical-scale axis.
        /// Essentially returns axis*vScale + ~axis*hScale
        /// </summary>
        /// <param name="sHoriz"></param>
        /// <param name="sVert"></param>
        /// <param name="axis"></param>
        /// <returns></returns>
        private Vector3 getScaleVector(float sHoriz, float sVert, Vector3 axis)
        {
            if (axis.x < 0) { axis.x = 1; }
            if (axis.y < 0) { axis.y = 1; }
            if (axis.z < 0) { axis.z = 1; }
            return (axis * sVert) + (getInverseVector(axis) * sHoriz);
        }

        /// <summary>
        /// Kind of like a bitwise inversion for a vector.
        /// If the input has any value for x/y/z, the output will have zero for that variable.
        /// If the input has zero for x/y/z, the output will have a one for that variable.
        /// </summary>
        /// <param name="axis"></param>
        /// <returns></returns>
        private Vector3 getInverseVector(Vector3 axis)
        {
            Vector3 val = Vector3.one;
            if (axis.x != 0) { val.x = 0; }
            if (axis.y != 0) { val.y = 0; }
            if (axis.z != 0) { val.z = 0; }
            return val;
        }

        /// <summary>
        /// Returns the sum of non-scaleable transform heights from the compound model data.
        /// </summary>
        /// <returns></returns>
        private float getStaticHeight()
        {
            float val = 0f;
            int len = compoundTransformData.Length;
            for (int i = 0; i < len; i++)
            {
                if (!compoundTransformData[i].canScaleHeight) { val += compoundTransformData[i].height; }
            }
            return val;
        }

    }

    /// <summary>
    /// Data class for a single transform in a compound-transform-enabled model.
    /// </summary>
    public class CompoundModelTransformData
    {
        public readonly string name;
        public readonly bool canScaleHeight = false;//can this transform scale its height
        public readonly float height;//the height of the meshes attached to this transform, at scale = 1
        public readonly float offset;//the vertical offset of the meshes attached to this transform, when translated this amount the top/botom of the meshes will be at transform origin.
        public readonly int order;//the linear index of this transform in a vertical model setup stack
        public readonly Vector3 vScaleAxis = Vector3.up;

        public CompoundModelTransformData(ConfigNode node)
        {
            name = node.GetStringValue("name");
            canScaleHeight = node.GetBoolValue("canScale");
            height = node.GetFloatValue("height");
            offset = node.GetFloatValue("offset");
            order = node.GetIntValue("order");
            vScaleAxis = node.GetVector3("axis", Vector3.up);
        }
    }

}
