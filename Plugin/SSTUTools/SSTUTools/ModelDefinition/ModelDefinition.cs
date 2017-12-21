using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSPShaderTools;
using UnityEngine;

namespace SSTUTools
{

    /// <summary>
    /// Immutable data storage for persistent data about a single 'model'.<para/>
    /// A 'model' is defined by any number of meshes from models in the GameDatabase, along with any function-specific data for the functions of those meshes -- animations, gimbals, rcs, engine, texture sets<para/>
    /// Includes height, volume, mass, cost, tech-limitations, attach-node positions, 
    /// texture-set data, and whether the model is intended for top, center, or bottom stack mounting.
    /// </summary>
    public class ModelDefinition
    {

        /// <summary>
        /// The config node that this ModelDefinition is loaded from.
        /// </summary>
        public readonly ConfigNode configNode;

        /// <summary>
        /// The name of the model in the model database to use, if any.  May be blank if model is a 'dummy' model (no meshes).<para/>
        /// </summary>
        public readonly string modelName;

        /// <summary>
        /// The unique registered name for this model definition.  MUST be unique among all model definitions.
        /// </summary>
        public readonly string name;

        /// <summary>
        /// The display name to use for this model definition.  This will be the value displayed on any GUIs for this specific model def.
        /// </summary>
        public readonly string title;

        /// <summary>
        /// A description of this model definition.  Optional.  Currently unused.
        /// </summary>
        public readonly string description;

        /// <summary>
        /// The name of the PartUpgrade that 'unlocks' this model definition.  If blank, denotes always unlocked.<para/>
        /// If populated, MUST correspond to one of the part-upgrades in the PartModule that this model-definition is used in.
        /// </summary>
        public readonly string upgradeUnlock;

        /// <summary>
        /// The height of this model, as denoted by colliders and/or attach nodes.<para/>
        /// This value is used to determine positioning of other models relative to this model when used in multi-model setups.
        /// </summary>
        public readonly float height = 1;

        /// <summary>
        /// The core diameter of this model.  If upper/lower diameter are unspecified, assume that the model is cylindrical with this diameter across its entire length.
        /// </summary>
        public readonly float diameter = 5;

        /// <summary>
        /// The diameter of the upper attachment point on this model.  Defaults to 'diameter' if unspecified.  Used during model-scale-chaining to determine the model scale to use for adapter models.
        /// </summary>
        public readonly float upperDiameter = 5;

        /// <summary>
        /// The diameter of the lower attachment point on this model.  Defaults to 'diameter' if unspecified.  Used during model-scale-chaining to determine the model scale to use for adapter models.
        /// </summary>
        public readonly float lowerDiameter = 5;

        /// <summary>
        /// The vertical offset applied to the meshes in the model to make the model conform with its specified orientation setup.<para/>
        /// Applied internally during model setup, should not be needed beyond when the model is first created.<para/>
        /// Should not be needed when COMPOUND_MODEL setups are used (transforms should be positioned properly in their defs).
        /// </summary>
        public readonly float verticalOffset = 0;

        /// <summary>
        /// The resource volume that this model definition contains at default scale.  Used to determine the total volume available for resource containers.
        /// </summary>
        public readonly float volume = 0;

        /// <summary>
        /// The 'mass' of this model-definition.  Modular part modules may use this value to adjust the config-specified mass of the part based on what models are selected.
        /// </summary>
        public readonly float mass = 0;

        /// <summary>
        /// The 'cost' of this model-definition.  Modular part modules may use this value to adjust the config-specified cost of the part based on what models are currently selected.
        /// </summary>
        public readonly float cost = 0;

        /// <summary>
        /// The orientation that this model is defined in.  The model setup/origin MUST be setup properly to match the orientation as specified in the model definition.<para/>
        /// Use the 'verticalOffset' function to fix any model positioning to ensure model conforms to specified orientation.
        /// </summary>
        public readonly ModelOrientation orientation = ModelOrientation.CENTRAL;

        /// <summary>
        /// Axis to use when inverting a model when a 'TOP' model is used for slot marked as 'BOTTOM' or 'BOTTOM' models used for a slot marked as 'TOP'.
        /// </summary>
        public readonly Vector3 invertAxis = Vector3.forward;

        /// <summary>
        /// Container for the faring data for this model definition.  Enabled/disabled, positioning, sizing data.
        /// </summary>
        public readonly ModelFairingData fairingData;

        /// <summary>
        /// Data defining a submodel setup -- a custom model comprised of multiple sub-models, all being treated as a single model-definition.<para/>
        /// All model definitions are mapped to a SUBMODEL setup internally during model creation, even if they only use the basic singular 'modelName=' configuration setup.
        /// This single model from the database becomes a single subModelData entry, using all of the transforms from the specified database model.
        /// </summary>
        public readonly SubModelData[] subModelData;

        /// <summary>
        /// Data defining a compound model setup -- a model that has special handling for vertical-scaling where only some of its transforms scale vertically.<para/>
        /// Can be used in combination with SubModel setup if needed/desired.<para/>
        /// If undefined, the model will use standard scale handling.
        /// </summary>
        public readonly CompoundModelData compoundModelData;

        /// <summary>
        /// Data defining the potential stack attach nodes that can be created/setup by this model definition.  If undefined, the model will have no attach nodes.
        /// </summary>
        public readonly AttachNodeBaseData[] attachNodeData;

        /// <summary>
        /// Data defining the surface attach-node to use for this model definition.  If undefined, it defaults to an attach node on X axis at 'diameter' distance from model origin, with vertical position at model origin.
        /// Only used by 'core' models in modular part setups.
        /// </summary>
        public readonly AttachNodeBaseData surfaceNode;

        /// <summary>
        /// The 'default' texture set for this model definition.  If unspecified, is set to the first available texture set if any are present in the model definition.
        /// </summary>
        public readonly String defaultTextureSet;

        /// <summary>
        /// The texture sets that are applicable to this model definition.  Will be null if no texture sets are defined in the config.
        /// </summary>
        public readonly TextureSet[] textureSets;

        /// <summary>
        /// The animation data that is applicable to this model definition.  Will be null if no animation data is specified in the config.
        /// </summary>
        public readonly AnimationData animationData;

        /// <summary>
        /// The solar layout data that is applicable to this model definition.  Will be null if no solar data is specified in the config.
        /// </summary>
        public readonly ModelSolarData solarData;

        /// <summary>
        /// The model animation constraint data that is applicable to this model definition.  Will be null if no constraint data is specified in the config.
        /// </summary>
        public readonly ModelConstraintData constraintData;

        /// <summary>
        /// The model engine thrust data -- engine min and max thrust for the default model scale.
        /// </summary>
        public readonly ModelEngineThrustData engineThrustData;

        /// <summary>
        /// The engine thrust transform data -- transform name(s), and thrust percentages in the case of non-uniform split thrust transforms.
        /// </summary>
        public readonly ModelEngineTransformData engineTransformData;

        /// <summary>
        /// The RCS position data for this model definition.  If RCS is attached to this model, determines where should it be located.
        /// </summary>
        public readonly ModelRCSData rcsData;

        /// <summary>
        /// List of ModelDefinitions that are valid options for use as an 'upper' adatper/nose option for this model definition.
        /// Used in modular part configurations to auto-determine the current valid adapter selections for any given 'core' model selection.<para/>
        /// If unpopulated, this adatper will have no further options.  Should be left emtpy/unpopulated for pure solar panel and RCS model definitions.
        /// </summary>
        public readonly string[] validUpperModels;

        /// <summary>
        /// List of ModelDefinitions that are valid options for use as an 'lower' adatper/nose option for this model definition.
        /// Used in modular part configurations to auto-determine the current valid adapter selections for any given 'core' model selection.<para/>
        /// If unpopulated, this adatper will have no further options.  Should be left emtpy/unpopulated for pure solar panel and RCS model definitions.
        /// </summary>
        public readonly string[] validLowerModels;

        /// <summary>
        /// Construct the model definition from the data in the input ConfigNode.<para/>
        /// All data constructs MUST conform to the expected format (see documentation), or things will not load properly and the model will likely not work as expected.
        /// </summary>
        /// <param name="node"></param>
        public ModelDefinition(ConfigNode node)
        {
            //load basic model definition values -- data that pertains to every model definition regardless of end-use.
            configNode = node;
            name = node.GetStringValue("name", String.Empty);
            if (string.IsNullOrEmpty(name))
            {
                MonoBehaviour.print("ERROR: Cannot load ModelDefinition with null or empty name.  Full config:\n" + node.ToString());
            }
            title = node.GetStringValue("title", name);
            description = node.GetStringValue("description", title);
            modelName = node.GetStringValue("modelName", String.Empty);
            upgradeUnlock = node.GetStringValue("upgradeUnlock", upgradeUnlock);
            height = node.GetFloatValue("height", height);
            volume = node.GetFloatValue("volume", volume);
            mass = node.GetFloatValue("mass", mass);
            cost = node.GetFloatValue("cost", cost);
            diameter = node.GetFloatValue("diameter", diameter);
            upperDiameter = node.GetFloatValue("upperDiameter", diameter);
            lowerDiameter = node.GetFloatValue("lowerDiameter", diameter);
            verticalOffset = node.GetFloatValue("verticalOffset", verticalOffset);
            orientation = (ModelOrientation)Enum.Parse(typeof(ModelOrientation), node.GetStringValue("orientation", ModelOrientation.TOP.ToString()));
            invertAxis = node.GetVector3("invertAxis", invertAxis);
            
            //load sub-model definitions
            ConfigNode[] subModelNodes = node.GetNodes("SUBMODEL");
            int len = subModelNodes.Length;
            if (len == 0)//no defined submodel data, check for regular single model definition, if present, build a submodel definition for it.
            {
                if (!string.IsNullOrEmpty(modelName))
                {
                    SubModelData smd = new SubModelData(modelName, new string[0], string.Empty, new Vector3(0, verticalOffset, 0), Vector3.zero, Vector3.one);
                    subModelData = new SubModelData[] { smd };
                }
                else//is an empty proxy model with no meshes
                {
                    subModelData = new SubModelData[0];
                }
            }
            else
            {
                subModelData = new SubModelData[len];
                for (int i = 0; i < len; i++)
                {
                    subModelData[i] = new SubModelData(subModelNodes[i]);
                }
            }

            //Load texture set definitions.
            ConfigNode[] textureSetNodes = node.GetNodes("TEXTURESET");
            len = textureSetNodes.Length;
            textureSets = new TextureSet[len];
            for (int i = 0; i < len; i++)
            {
                textureSets[i] = new TextureSet(textureSetNodes[i]);
            }

            //Load the default texture set specification
            defaultTextureSet = node.GetStringValue("defaultTextureSet");
            //if none is defined in the model def, but texture sets are present, set it to the name of the first defined texture set
            if (string.IsNullOrEmpty(defaultTextureSet) && textureSets.Length > 0)
            {
                defaultTextureSet = textureSets[0].name;
            }

            //load the stack attach node specifications
            String[] attachNodeStrings = node.GetValues("node");
            len = attachNodeStrings.Length;
            attachNodeData = new AttachNodeBaseData[len];
            for (int i = 0; i < len; i++)
            {
                attachNodeData[i] = new AttachNodeBaseData(attachNodeStrings[i]);
            }

            //load the surface attach node specifactions, or create default if none are defined.
            if (node.HasValue("surface"))
            {
                surfaceNode = new AttachNodeBaseData(node.GetStringValue("surface"));
            }
            else
            {
                String val = (diameter * 0.5f) + ",0,0,1,0,0,2";
                surfaceNode = new AttachNodeBaseData(val);
            }

            //load compound model data if present
            if (node.HasNode("COMPOUNDMODEL"))
            {
                compoundModelData = new CompoundModelData(node.GetNode("COMPOUNDMODEL"));
            }

            //load model animation data, if present
            if (node.HasNode("ANIMATIONDATA"))
            {
                animationData = new AnimationData(node.GetNode("ANIMATIONDATA"));
            }

            //load model animation constraint data, if present
            if (node.HasNode("CONSTRAINT"))
            {
                constraintData = new ModelConstraintData(node.GetNode("CONSTRAINT"));
            }

            //load model solar panel definition data, if present
            if (node.HasNode("SOLARDATA"))
            {
                solarData = new ModelSolarData(node.GetNode("SOLARDATA"));
            }

            //load model RCS data, if present
            if (node.HasNode("RCSDATA"))
            {
                rcsData = new ModelRCSData(node.GetNode("RCSDATA"));
            }

            //load model engine thrust data, if present
            if (node.HasNode("ENGINETHRUSTDATA"))
            {
                engineThrustData = new ModelEngineThrustData(node.GetNode("ENGINETHRUSTDATA"));
            }

            //load the engine transform data, if present
            if (node.HasNode("ENGINETRANSFORMDATA"))
            {
                engineTransformData = new ModelEngineTransformData(node.GetNode("ENGINETRANSFORMDATA"));
            }

            //load the fairing data, if present
            if (node.HasNode("FAIRINGDATA"))
            {
                fairingData = new ModelFairingData(node.GetNode("FAIRINGDATA"));
            }

            List<string> validAdapters = new List<string>();

            if (node.HasValue("topAdapterGroup"))
            {
                string[] groups = node.GetStringValues("topAdapterGroup");
                len = groups.Length;
                for (int i = 0; i < len; i++)
                {
                    validAdapters.AddUniqueRange(getAdapterGroup(groups[i]));
                }
            }
            if (node.HasValue("topAdapter"))
            {
                string[] adapters = node.GetStringValues("topAdapter");
                validAdapters.AddUniqueRange(adapters);
            }
            validUpperModels = validAdapters.ToArray();

            validAdapters.Clear();
            if (node.HasValue("bottomAdapterGroup"))
            {
                string[] groups = node.GetStringValues("bottomAdapterGroup");
                len = groups.Length;
                for (int i = 0; i < len; i++)
                {
                    validAdapters.AddUniqueRange(getAdapterGroup(groups[i]));
                }
            }
            if (node.HasValue("bottomAdapter"))
            {
                string[] adapters = node.GetStringValues("bottomAdapter");
                validAdapters.AddRange(adapters);
            }
            validLowerModels = validAdapters.ToArray();
        }

        /// <summary>
        /// Return true/false if this model definition is available given the input 'part upgrades' list.  Checks versus the 'upgradeUnlock' specified in the model definition config.
        /// </summary>
        /// <param name="partUpgrades"></param>
        /// <returns></returns>
        public bool isAvailable(List<String> partUpgrades)
        {
            return String.IsNullOrEmpty(upgradeUnlock) || partUpgrades.Contains(upgradeUnlock);
        }

        /// <summary>
        /// Return a string array containing the names of the texture sets that are available for this model definition.
        /// </summary>
        /// <returns></returns>
        public string[] getTextureSetNames()
        {
            return SSTUUtils.getNames(textureSets, m => m.name);
        }

        /// <summary>
        /// Returns a string array of the UI-label titles for the texture sets for this model definition.<para/>
        /// Returned in the same order as getTextureSetNames(), so they can be used in with basic indexing to map one value to another.
        /// </summary>
        /// <returns></returns>
        public string[] getTextureSetTitles()
        {
            return SSTUUtils.getNames(textureSets, m => m.title);
        }

        /// <summary>
        /// Return the TextureSet data for the input texture set name.<para/>
        /// Returns null if the input texture set name was not found in the currently loaded texture sets for this model definition.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public TextureSet findTextureSet(string name)
        {
            return Array.Find(textureSets, m => m.name == name);
        }

        /// <summary>
        /// Returns the default texture set as defined in the model definition config
        /// </summary>
        /// <returns></returns>
        public TextureSet getDefaultTextureSet()
        {
            return findTextureSet(defaultTextureSet);
        }

        /// <summary>
        /// Return true/false if this model should be inverted/rotated based on the input use-orientation and the models config-defined orientation.
        /// </summary>
        /// <param name="orientation"></param>
        /// <returns></returns>
        internal bool shouldInvert(ModelOrientation orientation)
        {
            return (orientation == ModelOrientation.BOTTOM && this.orientation == ModelOrientation.TOP) || (orientation == ModelOrientation.TOP && this.orientation == ModelOrientation.BOTTOM);
        }

        internal ModelDefinition[] getValidUpperOptions(List<string> partUpgrades)
        {
            return getValidOptions(partUpgrades, validUpperModels);
        }

        internal ModelDefinition[] getValidLowerOptions(List<string> partUpgrades)
        {
            return getValidOptions(partUpgrades, validLowerModels);
        }

        internal ModelDefinition[] getValidOptions(List<string> partUpgrades, string[] defNames)
        {
            List<ModelDefinition> defs = new List<ModelDefinition>();
            ModelDefinition def;
            int len = validUpperModels.Length;
            for (int i = 0; i < len; i++)
            {
                def = SSTUModelData.getModelDefinition(defNames[i]);
                if (def == null)
                {
                    MonoBehaviour.print("ERROR: Could not locate model definition for name: " + defNames[i]+" while loading valid adapters for: "+name);
                    continue;
                }
                if (def.isAvailable(partUpgrades))
                {
                    defs.Add(def);
                }
            }
            return defs.ToArray();
        }

        internal static string[] getAdapterGroup(string name)
        {
            ConfigNode[] groupNodes = GameDatabase.Instance.GetConfigNodes("ADAPTER_GROUP");
            int len = groupNodes.Length;
            ConfigNode groupNode;
            string groupName;
            for (int i = 0; i < len; i++)
            {
                groupNode = groupNodes[i];
                groupName = groupNode.GetStringValue("name");
                if (groupName == name)
                {
                    return groupNode.GetStringValues("definition");
                }
            }
            return new string[0];
        }

    }

    //TODO
    /// <summary>
    /// Information for a single model definition that specifies engine thrust transforms -- essentially just transform name(s).
    /// </summary>
    public class ModelEngineTransformData
    {

        public readonly string thrustTransformName;
        public readonly string gimbalTransformName;
        public readonly float gimbalAdjustmentRange;//how far the gimbal can be adjusted from reference while in the editor
        public readonly float gimbalFlightRange;//how far the gimbal may be actuated while in flight from the adjusted reference angle

        public ModelEngineTransformData(ConfigNode node)
        {
            thrustTransformName = node.GetStringValue("thrustTransform");
            gimbalTransformName = node.GetStringValue("gimbalTransformName");
            gimbalAdjustmentRange = node.GetFloatValue("gimbalAdjustRange", 0);
            gimbalFlightRange = node.GetFloatValue("gimbalFlightRange", 0);
        }

        public void renameThrustTransforms(Transform root, string destinationName)
        {
            Transform[] trs = root.FindChildren(thrustTransformName);
            int len = trs.Length;
            for (int i = 0; i < len; i++)
            {
                trs[i].gameObject.name = trs[i].name = destinationName;
            }
        }

        public void renameGimbalTransforms(Transform root, string destinationName)
        {
            Transform[] trs = root.FindChildren(gimbalTransformName);
            int len = trs.Length;
            for (int i = 0; i < len; i++)
            {
                trs[i].gameObject.name = trs[i].name = destinationName;
            }
        }

        //TODO
        public void updateGimbalModule(Part part)
        {
            //TODO
        }

    }

    //TODO
    /// <summary>
    /// Information for a single model definition that specifies engine thrust information<para/>
    /// Min, Max, and per-transform percentages.
    /// </summary>
    public class ModelEngineThrustData
    {

        public readonly float maxThrust;
        public readonly float minThrust;
        public readonly float[] thrustSplit;

        public ModelEngineThrustData(ConfigNode node)
        {
            minThrust = node.GetFloatValue("minThrust", 0);
            maxThrust = node.GetFloatValue("maxThrust", 1);
            thrustSplit = node.GetFloatValuesCSV("thrustSplit", new float[] { 1.0f });
        }

        //TODO
        public float[] getCombinedSplitThrust(int count)
        {
            return null;
        }

    }

    //TODO
    /// <summary>
    /// Information for a single ModelDefinition that specifies what solar panel ModelDefinitions are valid options, as well as specifying the layouts available.
    /// </summary>
    public class ModelSolarLayoutData
    {
        //TODO
        //somewhere down the line, this should be structured about like an 'EngineLayout'
        //the solar options selection in the GUI should allow for selecting a type of solar panel (the model), and its layout (the solar layout)
        //can combine into a single slider, or have individual sliders?
    }

    //TODO
    /// <summary>
    /// Information denoting the solar panel information for a single ModelDefinition.  Should include all suncatcher, pivot, and energy rate data.<para/>
    /// Animation data for deploy animation is handled through existing ModelAnimationData container class.
    /// </summary>
    public class ModelSolarData
    {

        public readonly ModelSolarDataPivot[] pivotDefinitions;
        public readonly ModelSolarDataSuncatcher[] suncatcherDefinitions;

        public ModelSolarData(ConfigNode node)
        {
            ConfigNode[] panelNodes = node.GetNodes("PIVOT");
            int len = panelNodes.Length;
            pivotDefinitions = new ModelSolarDataPivot[len];
            for (int i = 0; i < len; i++)
            {
                pivotDefinitions[i] = new ModelSolarDataPivot(panelNodes[i]);
            }

            ConfigNode[] suncatcherNodes = node.GetNodes("SUNCATCHER");
            len = suncatcherNodes.Length;
            suncatcherDefinitions = new ModelSolarDataSuncatcher[len];
            for (int i = 0; i < len; i++)
            {
                suncatcherDefinitions[i] = new ModelSolarDataSuncatcher(suncatcherNodes[i]);
            }
        }

        public PanelData[] createPanelData(int numberOfPanels)
        {
            return null;
        }

        public class ModelSolarDataPivot
        {

            public readonly string transformName;
            public readonly int pivotIndex = 0;
            public readonly int pivotStride = 1;
            public readonly float pivotSpeed;
            public readonly Axis rotAxis;
            public readonly Axis sunAxis;
            public readonly int suncatcherOcclusionIndex;

            public ModelSolarDataPivot(ConfigNode node)
            {
                pivotIndex = node.GetIntValue("pivotIndex", 0);
                pivotStride = node.GetIntValue("pivotStride", 1);
                pivotSpeed = node.GetFloatValue("pivotSpeed", 1);
                rotAxis = node.getAxis("rotAxis", Axis.YPlus);
                sunAxis = node.getAxis("sunAxis", Axis.ZPlus);
                suncatcherOcclusionIndex = node.GetIntValue("suncatcherIndex");
            }

        }

        public class ModelSolarDataSuncatcher
        {

            public readonly string transformName;
            public readonly int suncatcherIndex = 0;
            public readonly int suncatcherStride = 1;
            public readonly float rate = 0;

            public ModelSolarDataSuncatcher(ConfigNode node)
            {
                transformName = node.GetStringValue("suncatcher");
                suncatcherIndex = node.GetIntValue("suncatcherIndex", 0);
                suncatcherStride = node.GetIntValue("suncatcherStride", 1);
                rate = node.GetFloatValue("rate", 0);
            }

        }

    }

    //TODO
    /// <summary>
    /// Information denoting the model-animation-constraint setup for the meshes a single ModelDefinition.  Contains all information for all constraints used by the model.
    /// </summary>
    public class ModelConstraintData
    {
        //TODO
        public ModelConstraintData(ConfigNode node)
        {
        }        
    }

    //TODO
    /// <summary>
    /// Information pertaining to a single ModelDefinition, defining how NodeFairings are configured for the model at its default scale.
    /// </summary>
    public class ModelFairingData
    {

        /// <summary>
        /// Are fairings supported on this model?
        /// </summary>
        public readonly bool fairingsSupported = false;

        /// <summary>
        /// Are fairings inverted on this model?  If true, the user-adjustable diameter will be the 'top'.
        /// </summary>
        public readonly bool fairingInverted = false;

        /// <summary>
        /// An offset from the models origin point to where the fairing attaches.  Positive values denote Y+, negative values denote Y-
        /// </summary>
        public readonly float fairingOffsetFromOrigin = 0f;

        //TODO
        public ModelFairingData(ConfigNode node)
        {

        }

    }

    //TODO
    /// <summary>
    /// Container for RCS position related data for a standard structural model definition.
    /// </summary>
    public class ModelRCSData
    {

        /// <summary>
        /// Does this model-definition support RCS model use?<para/>
        /// If false, the RCS options will be disabled if it is linked to the slot that this model-definition occupies.
        /// If true, and the part supports RCS options, the RCS selection and position adjustment controls will be available.
        /// </summary>
        public readonly bool rcsEnabled = false;

        /// <summary>
        /// The horizontal offset to apply to each RCS port.  Defaults to the model 'diameter' if unspecified.
        /// </summary>
        public readonly float rcsHorizontalPosition;

        /// <summary>
        /// The vertical neutral position of the RCS ports, relative to the models origin.  If rcs vertical positionining is supported, this MUST be specified as the center of the offset range.
        /// </summary>
        public readonly float rcsVerticalPosition;

        /// <summary>
        /// The vertical +/- range that the RCS port may be moved through in the model at default model scale.
        /// </summary>
        public readonly float rcsVerticalRange;

        /// <summary>
        /// Angle to use when offsetting the RCS block along its specified vertical range.  To be used in the case of non cylindrical models that still want to support RCS position adjustment.
        /// </summary>
        public readonly float rcsVerticalAngle;

        /// <summary>
        /// The name of the thrust transforms as they are in the model hierarchy.  These will be renamed at runtime to match whatever the RCS module is expecting.
        /// </summary>
        public readonly string thrustTransformName;

        //TODO
        public ModelRCSData(ConfigNode node)
        {

        }

        public void renameTransforms(Transform root, string destinationName)
        {
            Transform[] trs = root.FindChildren(thrustTransformName);
            int len = trs.Length;
            for (int i = 0; i < len; i++)
            {
                trs[i].gameObject.name = trs[i].name = destinationName;
            }
        }

    }

    /// <summary>
    /// Simple enum defining the cardinal axis' of a transform.<para/>
    /// Used with Transform extension methods to return a vector for the specified axis (local or world-space).
    /// </summary>
    public enum Axis
    {
        XPlus,
        XNeg,
        YPlus,
        YNeg,
        ZPlus,
        ZNeg
    }

    /// <summary>
    /// Simple enum defining how a the meshes of a model are oriented relative to their root transform.<para/>
    /// ModelModule uses this information to position the model and attach nodes properly.
    /// </summary>
    public enum ModelOrientation
    {

        /// <summary>
        /// Denotes that a model is setup for use as a 'nose' or 'top' part, with the origin at the bottom of the model.<para/>
        /// Will be rotated 180 degrees around origin when used in a slot denoted for 'bottom' style models.<para/>
        /// Will be offset vertically downwards by half of its height when used in a slot denoted for 'central' models.<para/>
        /// </summary>
        TOP,

        /// <summary>
        /// Denotes that a model is setup for use as a 'central' part, with the origin in the center of the model.<para/>
        /// Will be offset upwards by half of its height when used in a slot denoted for 'top' style models.<para/>
        /// Will be offset downwards by half of its height when used in a slot denoted for 'bottom' style models.<para/>
        /// </summary>
        CENTRAL,

        /// <summary>
        /// Denotes that a model is setup for use as a 'bottom' part, with the origin located at the top of the model.<para/>
        /// Will be rotated 180 degrees around origin when used in a slot denoted for 'top' style models.<para/>
        /// Will be offset vertically upwards by half of its height when used in a slot denoted for 'central' models.<para/>
        /// </summary>
        BOTTOM
    }

    /// <summary>
    /// Stores the data defining a single animation for a single model definition.
    /// Should include animation name, parent transform name (of the animation component), default speed
    /// </summary>
    public class ModelAnimationData
    {

        /// <summary>
        /// The name of the AnimationClip.
        /// </summary>
        public readonly string animationName;

        /// <summary>
        /// A multiplier applied to the speed of the animation as it was compiled.
        /// </summary>
        public readonly float speed;

        /// <summary>
        /// Is this animation of type LOOP?  Used in compound animation setups to determine what is the 'intro' and what are the 'loop' animation types.
        /// </summary>
        public readonly bool isLoop;

        /// <summary>
        /// Used to increment the base animation layer that is passed into the ModelModule animation handling, in the case of a single model-definition requiring multiple layers.
        /// This must also be accounted for in the PartModule setup itself, or the layers might overrun into values used by other model slots.
        /// </summary>
        public readonly int layerOffset;//offset applied to the 'base' layer

        public ModelAnimationData(ConfigNode node)
        {
            animationName = node.GetStringValue("name");
            speed = node.GetFloatValue("speed", 1f);
            isLoop = node.GetBoolValue("loop", false);
        }

        public static ModelAnimationData[] parseAnimationData(ConfigNode[] nodes)
        {
            int len = nodes.Length;
            ModelAnimationData[] data = new ModelAnimationData[len];
            for (int i = 0; i < len; i++)
            {
                data[i] = new ModelAnimationData(nodes[i]);
            }
            return data;
        }
    }

}
