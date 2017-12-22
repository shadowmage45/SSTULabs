using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSPShaderTools;

namespace SSTUTools
{

    public class ModelModule<U> where U : PartModule
    {

        #region REGION - Delegate Signatures
        public delegate ModelModule<U> SymmetryModule(U m);
        public delegate ModelDefinition[] ValidOptions();
        #endregion ENDREGION - Delegate Signatures

        #region REGION - Immutable fields
        public readonly Part part;
        public readonly U partModule;
        public readonly Transform root;
        public readonly ModelOrientation orientation;
        public readonly int animationLayer = 0;
        public AnimationModule animationModule;
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

        /// <summary>
        /// Delegate can optionally be populated to override default behavior.
        /// </summary>
        public SymmetryModule getParentModule = delegate (U module)
        {
            return null;
        };

        #endregion ENDREGION - Public Delegate Stubs

        #region REGION - Private working data

        private Transform[] models;

        private ModelLayoutData currentLayout;

        private RecoloringData[] customColors = new RecoloringData[] { new RecoloringData(Color.white, 1, 1), new RecoloringData(Color.white, 1, 1), new RecoloringData(Color.white, 1, 1) };

        private ModelDefinition modelDefinition;

        private ModelDefinition[] optionsCache;

        private BaseField dataField;
        private BaseField textureField;
        private BaseField modelField;

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

        #endregion ENDREGION - BaseField wrappers

        #region REGION - Convenience wrappers for model definition data for external use

        public bool fairingEnabled { get { return modelDefinition.fairingData == null ? false : modelDefinition.fairingData.fairingsSupported; } }

        public bool animationEnabled { get { return modelDefinition.animationData != null; } }

        public bool engineTransformEnabled { get { return modelDefinition.engineTransformData != null; } }

        public bool engineThrustEnabled { get { return modelDefinition.engineThrustData != null; } }

        //TODO -- create specific gimbal transform data holder class
        public bool engineGimalEnabled { get { return modelDefinition.engineTransformData != null; } }

        public bool solarEnabled { get { return modelDefinition.solarData != null; } }

        public ModelDefinition definition { get { return modelDefinition; } }

        public TextureSet textureSet { get { return modelDefinition.findTextureSet(textureSetName); } }

        public ModelLayoutData layout { get { return layout; } }

        public float moduleMass { get { return currentMass; } }

        public float moduleCost { get { return currentCost; } }

        public float moduleVolume { get { return currentVolume; } }

        public float moduleDiameter { get { return currentDiameter; } }

        public float moduleHeight { get { return currentHeight; } }

        public float moduleHorizontalScale { get { return currentHorizontalScale; } }

        public float moduleVerticalScale { get { return currentVerticalScale; } }

        public float modulePosition { get { return currentVerticalPosition; } }

        public float moduleTop { get { return 0f; } }

        public float moduleCenter { get { return moduleTop - 0.5f * moduleHeight; } }

        public float moduleBottom { get { return moduleTop - moduleHeight; } }

        public float moduleFairingOffset { get { return modelDefinition.fairingData == null ? 0 : modelDefinition.fairingData.fairingOffsetFromOrigin; } }

        public RecoloringData[] recoloringData { get { return customColors; } }

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
            currentLayout = new ModelLayoutData("default", new ModelPositionData[] { new ModelPositionData(Vector3.zero, Vector3.one, Vector3.zero)});
            loadColors(persistentData);
        }

        /// <summary>
        /// Initialization method.  May be called to update the available model list later; if the currently selected model is invalid, it will be set to the first model in the list.
        /// </summary>
        /// <param name="models"></param>
        public void setupModelList(ModelDefinition[] modelDefs)
        {
            optionsCache = modelDefs;
            if (!Array.Exists(optionsCache, m => m.name == modelName))
            {
                modelName = optionsCache[0].name;
            }
            setupModel();
            updateSelections();
        }

        /// <summary>
        /// Initialization method.  Creates the model transforms, and sets their position and scale to the current config values.<para/>
        /// Initializes texture set, including 'defualts' handling.  Initializes animation module with the animation data for the current model.<para/>
        /// Only for use during part initialization.  Subsequent changes to model should call the modelSelectedXXX methods.
        /// </summary>
        public void setupModel()
        {
            SSTUUtils.destroyChildrenImmediate(root);
            constructModels();
            positionModels();
            setupTextureSet();
            animationModule.setupAnimations(definition.animationData, root, animationLayer);
        }

        #endregion ENDREGION - Constructors and Init Methods

        #region REGION - Update Methods

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
                MonoBehaviour.print("ERROR: RCS data is null for model definition: " + definition.name);
                return;
            }
            definition.rcsData.renameTransforms(root, destinationName);
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
                MonoBehaviour.print("ERROR: Engine transform data is null for model definition: " + definition.name);
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
                MonoBehaviour.print("ERROR: Engine transform data is null for model definition: " + definition.name);
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
            int len = currentLayout.positions.Length;
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
            if (Array.Exists(optionsCache, m => m.name == newModel))
            {
                modelName = newModel;
                setupModel();
            }
            else
            {
                MonoBehaviour.print("ERROR: No model definition found for input name: " + newModel);
            }
        }

        /// <summary>
        /// NON-Symmetry enabled method.<para/>
        /// Updates the UI controls for the currently available models specified through setupModelList.
        /// </summary>
        public void updateSelections()
        {
            string[] names = SSTUUtils.getNames(optionsCache, s => s.name);
            string[] displays = SSTUUtils.getNames(optionsCache, s => s.title);
            partModule.updateUIChooseOptionControl(modelField.name, names, displays, true, modelName);
            modelField.guiActiveEditor = names.Length > 1;
        }

        /// <summary>
        /// NON-symmetry enabled method.
        /// Set the current model layout.  Should only be called after model list is populated, as it will (attempt to) re-setup the models for the new layout.
        /// </summary>
        /// <param name="mld"></param>
        public void setModelLayout(ModelLayoutData mld)
        {
            this.currentLayout = mld;
            this.setupModel();
        }
        
        /// <summary>
        /// NON-symmetry enabled method.
        /// Updates the current models with the current scale and position data.
        /// </summary>
        public void updateModelMeshes()
        {
            updateModelScaleAndPosition();
        }

        public ModelDefinition[] getUpperOptions() { return modelDefinition.getValidUpperOptions(partModule.upgradesApplied); }

        public ModelDefinition[] getLowerOptions() { return modelDefinition.getValidLowerOptions(partModule.upgradesApplied); }

        public void setScaleForDiameter(float newDiameter)
        {
            float newScale = newDiameter / definition.diameter;
            setScale(newScale);
        }

        public void setScaleForHeightAndDiameter(float newHeight, float newDiameter)
        {
            float newHorizontalScale = newDiameter / definition.diameter;
            float newVerticalScale = newHeight / definition.height;
            setScale(newHorizontalScale, newVerticalScale);
        }

        public void setScale(float newScale)
        {
            setScale(newScale, newScale);
        }

        public void setScale(float newHorizontalScale, float newVerticalScale)
        {
            currentHorizontalScale = newHorizontalScale;
            currentVerticalScale = newVerticalScale;
            currentHeight = newVerticalScale * definition.height;
            currentDiameter = newHorizontalScale * definition.diameter;
        }

        #endregion ENDREGION - GUI Interaction Methods

        #region REGION - Private/Internal methods

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
            partModule.updateUIChooseOptionControl(textureField.name, names, definition.getTextureSetTitles(), true, textureSetName);
            textureField.guiActiveEditor = names.Length > 1;
        }

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
                textureSet.enable(root.gameObject, customColors);
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
            int len = currentLayout.positions.Length;
            for (int i = 0; i < len; i++)
            {
                positionModel(i);
            }
        }

        /// <summary>
        /// Update the position of a single model, by index, for the currently configured ModelLayoutData.
        /// </summary>
        /// <param name="index"></param>
        private void positionModel(int index)
        {
            Transform model = models[index];
            ModelPositionData mpd = currentLayout.positions[index];
            model.transform.localPosition = mpd.localPosition;
            model.transform.localRotation = Quaternion.Euler(mpd.localRotation);
            model.transform.localScale = mpd.localScale;
        }

        /// <summary>
        /// Constructs all of the models for the current ModelDefinition and ModelLayoutData
        /// </summary>
        private void constructModels()
        {
            int len = currentLayout.positions.Length;
            models = new Transform[len];
            for (int i = 0; i < len; i++)
            {
                models[i] = new GameObject("ModelModule-" + i).transform;
                models[i].NestToParent(root);
                constructSubModels(models[i]);
                positionModel(i);
            }
            if (definition.shouldInvert(orientation))
            {
                root.transform.localRotation = Quaternion.Euler(definition.invertAxis * 180f);
            }
        }
        
        /// <summary>
        /// Constructs a single model instance from the model definition, parents it to the input transform.<para/>
        /// Does not position or orient the created model; positionModel(index) should be called to update its position for the current ModelLayoutData configuration
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
                    MonoBehaviour.print("ERROR: Could not clone model for url: " + smd.modelURL + " while constructing meshes for model definition" + definition.name);
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
                if (smd.modelMeshes.Length > 0)
                {
                    smd.setupSubmodel(clonedModel);
                }
            }
        }
        
        /// <summary>
        /// Applies the current module position and scale values to the root transform of the ModelModule.  Does not adjust rotation.
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
        
        //TODO
        private float getVerticalOffset()
        {
            return 0f;
        }

        //TODO
        private bool isValidTextureSet(String val)
        {
            bool noTextures = definition.textureSets.Length == 0;
            if (String.IsNullOrEmpty(val))
            {
                return noTextures;
            }
            if (val == definition.defaultTextureSet) { return true; }
            foreach (TextureSet set in definition.textureSets)
            {
                if (set.name == val) { return true; }
            }
            return false;
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

        #endregion ENDREGION - Private/Internal methods

    }

    /// <summary>
    /// Class denoting a the transforms to use from a single database model.  Allows for combining multiple entire models, and/or transforms from models, all into a single active/usable Model
    /// </summary>
    public class SubModelData
    {

        public readonly string modelURL;
        public readonly string[] modelMeshes;
        public readonly string parent;
        public readonly Vector3 rotation;
        public readonly Vector3 position;
        public readonly Vector3 scale;

        public SubModelData(ConfigNode node)
        {
            modelURL = node.GetStringValue("modelName");
            modelMeshes = node.GetStringValues("transform");
            parent = node.GetStringValue("parent", string.Empty);
            position = node.GetVector3("position", Vector3.zero);
            rotation = node.GetVector3("rotation", Vector3.zero);
            scale = node.GetVector3("scale", Vector3.one);
        }

        public SubModelData(string modelURL, string[] meshNames, string parent, Vector3 pos, Vector3 rot, Vector3 scale)
        {
            this.modelURL = modelURL;
            this.modelMeshes = meshNames;
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

    /// <summary>
    /// Data that defines how a single model is positioned inside a ModelModule when more than one internal model is desired.
    /// This position/scale/rotation data is applied in addition to the base ModelModule position/scale data.
    /// </summary>
    public struct ModelPositionData
    {

        /// <summary>
        /// The local position of a single model, relative to the model-modules position
        /// </summary>
        public readonly Vector3 localPosition;

        /// <summary>
        /// The local scale of a single model, relative to the model-modules scale
        /// </summary>
        public readonly Vector3 localScale;

        /// <summary>
        /// The local rotation to be applied to a single model (euler x,y,z)
        /// </summary>
        public readonly Vector3 localRotation;

        public ModelPositionData(ConfigNode node)
        {
            localPosition = node.GetVector3("position", Vector3.zero);
            localScale = node.GetVector3("scale", Vector3.one);
            localRotation = node.GetVector3("rotation", Vector3.zero);
        }

        public ModelPositionData(Vector3 pos, Vector3 scale, Vector3 rotation)
        {
            localPosition = pos;
            localScale = scale;
            localRotation = rotation;
        }

        public Vector3 scaledPosition(Vector3 scale)
        {
            return Vector3.Scale(localPosition, scale);
        }

        public Vector3 scaledPosition(float scale)
        {
            return localPosition * scale;
        }

        public Vector3 scaledScale(Vector3 scale)
        {
            return Vector3.Scale(localScale, scale);
        }

        public Vector3 scaledScale(float scale)
        {
            return localScale * scale;
        }

    }

    /// <summary>
    /// Named model position layout data structure.  Used to store positions of models, for use in ModelModule setup.<para/>
    /// Defined independently of the models that may use them, stored and referenced globally/game-wide.<para/>
    /// A single ModelLayoutData may be used by multiple ModelDefinitions, and a single ModelDefinition may have different ModelLayoutDatas applied to it by the controlling part-module.
    /// </summary>
    public struct ModelLayoutData
    {

        public readonly string name;
        public readonly ModelPositionData[] positions;
        public ModelLayoutData(ConfigNode node)
        {
            name = node.GetStringValue("name");
            ConfigNode[] posNodes = node.GetNodes("POSITION");
            int len = posNodes.Length;
            positions = new ModelPositionData[len];
            for (int i = 0; i < len; i++)
            {
                positions[i] = new ModelPositionData(posNodes[i]);
            }
        }
        public ModelLayoutData(string name, ModelPositionData[] positions)
        {
            this.name = name;
            this.positions = positions;
        }

    }

}
