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
        /// Minimum scalar offset that can be applied to vertical scale as compared to horizontal scale.
        /// </summary>
        public readonly float minVerticalScale = 1f;

        /// <summary>
        /// Maximum scalar offset that can be applied to vertical scale as compared to horizontal scale.
        /// </summary>
        public readonly float maxVerticalScale = 1f;

        /// <summary>
        /// The vertical offset applied to the meshes in the model to make the model conform with its specified orientation setup.<para/>
        /// Applied internally during model setup, should not be needed beyond when the model is first created.<para/>
        /// Should not be needed when COMPOUND_MODEL setups are used (transforms should be positioned properly in their defs).
        /// Only used when SUBMODEL is not defined, otherwise submodel data overrides.
        /// </summary>
        public readonly Vector3 positionOffset = Vector3.zero;

        /// <summary>
        /// The Euler XYZ rotation that should be applied to this model to make it conform to Y+=UP / Z+=FWD conventions.
        /// Only used when SUBMODEL is not defined, otherwise submodel data overrides.
        /// </summary>
        public readonly Vector3 rotationOffset = Vector3.zero;

        /// <summary>
        /// The XYZ scale that should be applied to the model.
        /// Only used when SUBMODEL is not defined, otherwise submodel data overrides.
        /// </summary>
        public readonly Vector3 scaleOffset = Vector3.one;

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
        /// The orientation that this model module is defined in.  Calls to 'setPosition' take this into account to position the model properly.  The model setup/origin MUST be setup properly to match the orientation as specified in the model definition.<para/>
        /// Use the 'verticalOffset' function of the ModelDefinition to fix any model positioning to ensure model conforms to specified orientation.
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
        /// Data defining what meshes in the model will be merged into joined meshes.  For cases of compound models that will all share
        /// the same materials, makes for more efficient use of material/rendering and cuts down on the number of GOs in the model tree.<para/>
        /// These meshes are merged whenever the model from this definition is constructed.
        /// </summary>
        public readonly MeshMergeData[] mergeData;

        /// <summary>
        /// Attach node data for the 'top' attach node.  Will only be used if the top of this model is uncovered.
        /// </summary>
        public readonly AttachNodeBaseData topNodeData;

        /// <summary>
        /// Attach node data for the 'bottom' attach node.  Will only be used if the bottom of this model is uncovered.
        /// </summary>
        public readonly AttachNodeBaseData bottomNodeData;

        /// <summary>
        /// Attach node data for the 'body' nodes. Will be used regardless of top/bottom covered status.
        /// </summary>
        public readonly AttachNodeBaseData[] bodyNodeData;

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
        public readonly ModelSolarData solarModuleData;

        /// <summary>
        /// The solar positioning data for attaching solar panels to this model.  Will be null if this model is not a valid target for solar panel attachment.
        /// </summary>
        public readonly ModelAttachablePositionData solarPositionData;

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
        /// The RCS position data for this model definition.  If RCS is attached to this model, determines where should it be located.<para/>
        /// Upper RCS module position should always be in index 0 if multiple modules are present.
        /// </summary>
        public readonly ModelAttachablePositionData[] rcsPositionData;

        /// <summary>
        /// The rcs-module data for use by the RCS thrusters in this model -- thrust, fuel type, ISP.  Will be null if this is not an RCS model.
        /// </summary>
        public readonly ModelRCSModuleData rcsModuleData;

        /// <summary>
        /// Denotes what type of mounting this part has on its upper attach point (top of model).<para/>
        /// These values are checked vs. the 'compatibleLowerProfiles' of the part being attached to this one (and vise-versa).<para/>
        /// The target must accept -all- of the upper profiles specified in this model, or it will not be available as a valid option.
        /// </summary>
        public readonly string[] upperProfiles;

        /// <summary>
        /// Denotes what type of mounting this part has on its lower attach point (bottom of model).<para/>
        /// These values are checked vs. the 'compatibleUpperProfiles' of the part being attached to this one (and vise-versa).<para/>
        /// The target must accept -all- of the upper profiles specified in this model, or it will not be available as a valid option.
        /// </summary>
        public readonly string[] lowerProfiles;

        /// <summary>
        /// List of profiles that this model is compatible with.  May contain multiple profile definitions.<para/>
        /// The compatible profiles must contain -all- of the profiles from the input list, but the compatible list may contain extras.
        /// </summary>
        public readonly string[] compatibleUpperProfiles;

        /// <summary>
        /// List of profiles that this model is compatible with.  May contain multiple profile definitions.<para/>
        /// The compatible profiles must contain -all- of the profiles from the input list, but the compatible list may contain extras.
        /// </summary>
        public readonly string[] compatibleLowerProfiles;

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
            minVerticalScale = node.GetFloatValue("minVerticalScale", minVerticalScale);
            maxVerticalScale = node.GetFloatValue("maxVerticalScale", maxVerticalScale);
            upperDiameter = node.GetFloatValue("upperDiameter", diameter);
            lowerDiameter = node.GetFloatValue("lowerDiameter", diameter);
            if (node.HasValue("verticalOffset"))
            {
                positionOffset = new Vector3(0, node.GetFloatValue("verticalOffset"), 0);
            }
            else
            {
                positionOffset = node.GetVector3("positionOffset", Vector3.zero);
            }
            rotationOffset = node.GetVector3("rotationOffset", rotationOffset);
            scaleOffset = node.GetVector3("scaleOffset", Vector3.one);

            orientation = (ModelOrientation)Enum.Parse(typeof(ModelOrientation), node.GetStringValue("orientation", ModelOrientation.TOP.ToString()));
            invertAxis = node.GetVector3("invertAxis", invertAxis);

            upperProfiles = node.GetStringValues("upperProfile");
            lowerProfiles = node.GetStringValues("lowerProfile");
            compatibleUpperProfiles = node.GetStringValues("compatibleUpperProfile");
            compatibleLowerProfiles = node.GetStringValues("compatibleLowerProfile");
            
            //load sub-model definitions
            ConfigNode[] subModelNodes = node.GetNodes("SUBMODEL");
            int len = subModelNodes.Length;
            if (len == 0)//no defined submodel data, check for regular single model definition, if present, build a submodel definition for it.
            {
                if (!string.IsNullOrEmpty(modelName))
                {
                    SubModelData smd = new SubModelData(modelName, new string[0], string.Empty, positionOffset, rotationOffset, scaleOffset);
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

            if (node.HasNode("MERGEDMODELS"))
            {
                ConfigNode[] mergeNodes = node.GetNodes("MERGEDMODELS");
                len = mergeNodes.Length;
                mergeData = new MeshMergeData[len];
                for (int i = 0; i < len; i++)
                {
                    mergeData[i] = new MeshMergeData(mergeNodes[i]);
                }
            }

            //Load texture set definitions.
            List<TextureSet> textureSetList = new List<TextureSet>();
            //first load any of the global sets that are specified by name
            string[] textureSetNames = node.GetStringValues("textureSet");
            len = textureSetNames.Length;
            for (int i = 0; i < len; i++)
            {
                TextureSet ts = TexturesUnlimitedLoader.getTextureSet(textureSetNames[i]);
                if (ts != null) { textureSetList.Add(ts); }
            }

            //then load any of the model-specific sets
            ConfigNode[] textureSetNodes = node.GetNodes("KSP_TEXTURE_SET");
            len = textureSetNodes.Length;
            textureSets = new TextureSet[len];
            for (int i = 0; i < len; i++)
            {
                textureSetList.Add(new TextureSet(textureSetNodes[i]));
            }
            textureSets = textureSetList.ToArray();
            textureSetList.Clear();
            textureSetList = null;

            //Load the default texture set specification
            defaultTextureSet = node.GetStringValue("defaultTextureSet");
            //if none is defined in the model def, but texture sets are present, set it to the name of the first defined texture set
            if (string.IsNullOrEmpty(defaultTextureSet) && textureSets.Length > 0)
            {
                defaultTextureSet = textureSets[0].name;
            }

            if (node.HasValue("topNode"))
            {
                topNodeData = new AttachNodeBaseData(node.GetStringValue("topNode"));
            }
            else
            {
                float y = height;
                if (orientation == ModelOrientation.CENTRAL) { y *= 0.5f; }
                else if (orientation == ModelOrientation.BOTTOM) { y = 0; }
                topNodeData = new AttachNodeBaseData(0, y, 0, 0, 1, 0, diameter / 1.25f);
            }
            if (node.HasValue("bottomNode"))
            {
                bottomNodeData = new AttachNodeBaseData(node.GetStringValue("bottomNode"));
            }
            else
            {
                float y = -height;
                if (orientation == ModelOrientation.CENTRAL) { y *= 0.5f; }
                else if (orientation == ModelOrientation.TOP) { y = 0; }
                bottomNodeData = new AttachNodeBaseData(0, y, 0, 0, -1, 0, diameter / 1.25f);
            }
            if (node.HasValue("bodyNode"))
            {
                string[] nodeData = node.GetStringValues("bodyNode");
                len = nodeData.Length;
                bodyNodeData = new AttachNodeBaseData[len];
                for (int i = 0; i < len; i++)
                {
                    bodyNodeData[i] = new AttachNodeBaseData(nodeData[i]);
                }
            }

            //load the surface attach node specifications, or create default if none are defined.
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
                solarModuleData = new ModelSolarData(node.GetNode("SOLARDATA"));
            }

            //load solar position data, if present
            if (node.HasNode("SOLARPOSITION"))
            {
                solarPositionData = new ModelAttachablePositionData(node.GetNode("SOLARPOSITION"));
            }

            //load model RCS module data, if present
            if (node.HasNode("RCSDATA"))
            {
                rcsModuleData = new ModelRCSModuleData(node.GetNode("RCSDATA"));
            }

            //load model RCS positioning data, if present
            if (node.HasNode("RCSPOSITION"))
            {
                ConfigNode[] pns = node.GetNodes("RCSPOSITION");
                len = pns.Length;
                rcsPositionData = new ModelAttachablePositionData[len];
                for (int i = 0; i < len; i++)
                {
                    rcsPositionData[i] = new ModelAttachablePositionData(pns[i]);
                }
            }

            //load model engine thrust data, if present
            if (node.HasNode("ENGINE_THRUST"))
            {
                engineThrustData = new ModelEngineThrustData(node.GetNode("ENGINE_THRUST"));
            }

            //load the engine transform data, if present
            if (node.HasNode("ENGINE_TRANSFORM"))
            {
                engineTransformData = new ModelEngineTransformData(node.GetNode("ENGINE_TRANSFORM"));
            }

            //load the fairing data, if present
            if (node.HasNode("FAIRINGDATA"))
            {
                fairingData = new ModelFairingData(node.GetNode("FAIRINGDATA"));
            }
        }

        /// <summary>
        /// Return the list of lower-attachment profiles if the model is used in the input orientation
        /// </summary>
        /// <param name="orientation"></param>
        /// <returns></returns>
        public string[] getLowerProfiles(ModelOrientation orientation)
        {
            return shouldInvert(orientation) ? upperProfiles : lowerProfiles;
        }

        /// <summary>
        /// Return the list of upper-attachment profiles if the model is used in the input orientation
        /// </summary>
        /// <param name="orientation"></param>
        /// <returns></returns>
        public string[] getUpperProfiles(ModelOrientation orientation)
        {
            return shouldInvert(orientation) ? lowerProfiles : upperProfiles;
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
        /// Return true/false if this model should be inverted/rotated based on the input use-orientation and the models config-defined orientation.<para/>
        /// If specified model orientation == CENTER, model will never invert regardless of input value.
        /// </summary>
        /// <param name="orientation"></param>
        /// <returns></returns>
        internal bool shouldInvert(ModelOrientation orientation)
        {
            return (orientation == ModelOrientation.BOTTOM && this.orientation == ModelOrientation.TOP) || (orientation == ModelOrientation.TOP && this.orientation == ModelOrientation.BOTTOM);
        }

        /// <summary>
        /// Returns true/false if every value in 'profiles' is present in 'compatible'.
        /// If even a single value from 'profiles' is not found in 'compatible', return false.
        /// </summary>
        /// <param name="compatible"></param>
        /// <param name="profiles"></param>
        /// <returns></returns>
        private bool canAttach(string[] compatible, string[] profiles)
        {
            bool foundAll = true;
            int len = profiles.Length;
            int len2 = compatible.Length;
            string prof;
            for (int i = 0; i < len; i++)
            {
                prof = profiles[i];
                if (!compatible.Contains(prof))
                {
                    foundAll = false;
                    break;
                }
            }
            return foundAll;
        }

        /// <summary>
        /// Return if the input profiles are compatible with being mounted on the bottom of this model when this model is used in the input orientation.<para/>
        /// E.G. If model specified orientation==TOP, but being used for 'BOTTOM', will actually check the 'upper' profiles list (as that is the attach point that is at the bottom of the model when inverted)
        /// </summary>
        /// <param name="profiles"></param>
        /// <param name="orientation"></param>
        /// <returns></returns>
        internal bool isValidLowerProfile(string[] profiles, ModelOrientation orientation)
        {
            return shouldInvert(orientation) ? canAttach(compatibleUpperProfiles, profiles) : canAttach(compatibleLowerProfiles, profiles);
        }

        /// <summary>
        /// Return if the input profiles are compatible with being mounted on the top of this model when this model is used in the input orientation.<para/>
        /// E.G. If model specified orientation==TOP, but being used for 'BOTTOM', will actually check the 'upper' profiles list (as that is the attach point that is at the bottom of the model when inverted)
        /// </summary>
        /// <param name="profiles"></param>
        /// <param name="orientation"></param>
        /// <returns></returns>
        internal bool isValidUpperProfile(string[] profiles, ModelOrientation orientation)
        {
            return shouldInvert(orientation) ? canAttach(compatibleLowerProfiles, profiles) : canAttach(compatibleUpperProfiles, profiles);
        }

        /// <summary>
        /// Checks to see if this definition can be switched to given the currently occupied attach nodes
        /// </summary>
        /// <param name="part"></param>
        /// <param name="bodyNodeNames"></param>
        /// <param name="orientation"></param>
        /// <param name="endNodeName"></param>
        /// <returns></returns>
        public bool canSwitchTo(Part part, string[] bodyNodeNames, ModelOrientation orientation, bool top, string endNodeName)
        {
            AttachNode node;
            bool valid = true;
            if (!string.IsNullOrEmpty(endNodeName))//this def is responsible for top/bottom attach node
            {
                //attach node from the part
                node = part.FindAttachNode(endNodeName);
                //if attachnode==null or nothing attached, does not currently exist, so it doesn't matter what the model def has setup
                if (node != null && node.attachedPart != null)
                {
                    //node is not null, and has attached part.  Check model def to make sure it has a node data, else set valid to false
                    //determine if top or bottom from input orientation and def orientation
                    //if node data==null, it means def does not support that end node
                    AttachNodeBaseData nodeData = top ? (shouldInvert(orientation) ? bottomNodeData : topNodeData) : (shouldInvert(orientation) ? topNodeData : bottomNodeData);
                    if (nodeData == null) { valid = false; }
                }
            }
            //if already invalid, or has no body node names to manage, return the current 'valid' value
            if (!valid || bodyNodeNames==null || bodyNodeNames.Length==0) { return valid; }//break
            int len = bodyNodeNames.Length;
            for (int i = 0; i < len; i++)
            {
                //attach node from the part
                node = part.FindAttachNode(bodyNodeNames[i]);
                //if attachnode==null or nothing attached, does not currently exist, so it doesn't matter what the model def has setup
                if (node != null && node.attachedPart != null)
                {
                    //this is a node with a part attached, so we need at least one node present in model-def body node array,
                    if (bodyNodeData == null || bodyNodeData.Length==0 || i >= bodyNodeData.Length)
                    {
                        valid = false;
                        break;
                    }
                }
            }
            return valid;
        }
        
        public override string ToString()
        {
            return "ModelDef[ " + name + " ]";
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
            if (string.IsNullOrEmpty(thrustTransformName)) { SSTULog.error("ERROR: THrust transform name was null for model def engine transform data"); }
            gimbalTransformName = node.GetStringValue("gimbalTransform");
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

        public float[] getCombinedSplitThrust(int count)
        {
            if (thrustSplit == null) { return null; }
            int l1 = thrustSplit.Length;
            int l2 = l1 * count;
            float[] retCache = new float[l2];
            for (int i = 0, j = 0; i < count; i++)
            {
                for (int k = 0; k < l1; k++, j++)
                {
                    retCache[j] = thrustSplit[k] / count;
                }
            }
            return retCache;
        }

    }
    
    /// <summary>
    /// Information denoting the solar panel information for a single ModelDefinition.  Should include all suncatcher, pivot, and energy rate data.<para/>
    /// Animation data for deploy animation is handled through existing ModelAnimationData container class.
    /// </summary>
    public class ModelSolarData
    {

        public readonly ModelSolarDataPivot[] pivotDefinitions;
        public readonly ModelSolarDataSuncatcher[] suncatcherDefinitions;
        public readonly bool enabled = true;

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

        public ModelSolarData()
        {
            pivotDefinitions = new ModelSolarDataPivot[0];
            suncatcherDefinitions = new ModelSolarDataSuncatcher[0];
            enabled = false;
        }

        public class ModelSolarDataPivot
        {

            public readonly string transformName;
            public readonly int pivotIndex = 0;
            public readonly float pivotSpeed;
            public readonly Axis rotAxis;
            public readonly Axis sunAxis;
            public readonly int suncatcherOcclusionIndex;

            public ModelSolarDataPivot(ConfigNode node)
            {
                transformName = node.GetStringValue("pivot");
                pivotIndex = node.GetIntValue("pivotIndex", 0);
                pivotSpeed = node.GetFloatValue("pivotSpeed", 1);
                rotAxis = node.getAxis("rotAxis", Axis.YPlus);
                sunAxis = node.getAxis("sunAxis", Axis.ZPlus);
                suncatcherOcclusionIndex = node.GetIntValue("suncatcherIndex");
            }

            public string debugOutput()
            {
                string val = "Created MSDP:" +
                    "\n       pivot: " + transformName +
                    "\n       index: " + pivotIndex +
                    "\n       speed: " + pivotSpeed +
                    "\n       rAxis: " + rotAxis +
                    "\n       sAxis: " + sunAxis +
                    "\n       ocIdx: " + suncatcherOcclusionIndex;
                return val;
            }

        }

        public class ModelSolarDataSuncatcher
        {

            public readonly string transformName;
            public readonly int suncatcherIndex = 0;
            public readonly float rate = 0;
            public readonly Axis sunAxis;

            public ModelSolarDataSuncatcher(ConfigNode node)
            {
                transformName = node.GetStringValue("suncatcher");
                suncatcherIndex = node.GetIntValue("suncatcherIndex", 0);
                rate = node.GetFloatValue("chargeRate", 0);
                sunAxis = node.getAxis("suncatcherAxis", Axis.ZPlus);
            }

            public string debugOutput()
            {
                string val = "Created MSDS:" +
                    "\n       pivot: " + transformName +
                    "\n       index: " + suncatcherIndex +
                    "\n       charg: " + rate +
                    "\n       sAxis: " + sunAxis;
                return val;
            }

        }

    }

    /// <summary>
    /// Information denoting the model-animation-constraint setup for the meshes a single ModelDefinition.  Contains all information for all constraints used by the model.
    /// </summary>
    public class ModelConstraintData
    {
        public ConfigNode constraintNode;
        public ModelConstraintData(ConfigNode node)
        {
            constraintNode = node;
        }        
    }

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
        /// Position of the 'top' of the fairing, relative to model in its defined orientation and scale.
        /// If the model is used in oposite orientation, this value is negated.
        /// </summary>
        public readonly float top = 0f;

        /// <summary>
        /// Position of the 'bottom' of the fairing, relative to model in its defined orientation and scale.
        /// If the model is used in oposite orientation, this value is negated.
        /// </summary>
        public readonly float bottom = 0f;

        public ModelFairingData(ConfigNode node)
        {
            fairingsSupported = node.GetBoolValue("enabled", false);
            top = node.GetFloatValue("top", 0f);
            bottom = node.GetFloatValue("bottom", 0f);
        }

        public float getTop(float scale, bool invert)
        {
            if (invert) { return scale * bottom; }
            return scale * top;
        }

        public float getBottom(float scale, bool invert)
        {
            if (invert) { return scale * top; }
            return scale * bottom;
        }

    }

    /// <summary>
    /// Container for RCS position related data for a standard structural model definition.
    /// </summary>
    public class ModelRCSModuleData
    {

        /// <summary>
        /// The name of the thrust transforms as they are in the model hierarchy.  These will be renamed at runtime to match whatever the RCS module is expecting.
        /// </summary>
        public readonly string thrustTransformName;

        /// <summary>
        /// The thrust of the RCS model at its base scale.
        /// </summary>
        public readonly float rcsThrust;

        public readonly bool enableX, enableY, enableZ, enablePitch, enableYaw, enableRoll;

        public ModelRCSModuleData(ConfigNode node)
        {
            thrustTransformName = node.GetStringValue("thrustTransformName");
            rcsThrust = node.GetFloatValue("thrust");
            enableX = node.GetBoolValue("enableX", true);
            enableY = node.GetBoolValue("enableY", true);
            enableZ = node.GetBoolValue("enableZ", true);
            enablePitch = node.GetBoolValue("enablePitch", true);
            enableYaw = node.GetBoolValue("enableYaw", true);
            enableRoll = node.GetBoolValue("enableRoll", true);
        }

        public float getThrust(float scale)
        {
            return scale * scale * rcsThrust;
        }

        public void renameTransforms(Transform root, string destinationName)
        {
            Transform[] trs = root.FindChildren(thrustTransformName);
            int len = trs.Length;
            for (int i = 0; i < len; i++)
            {
                trs[i].gameObject.name = trs[i].name = destinationName;
            }
            //TODO -- if transform array is null, add a single dummy transform of the given name to stop stock modules' logspam
        }

    }

    /// <summary>
    /// Container for RCS positional data for a model definition. <para/>
    /// Specifies if the model supports RCS block addition, where on the model the blocks are positioned, and if they may have their position adjusted.
    /// </summary>
    public class ModelAttachablePositionData
    {

        /// <summary>
        /// The horizontal offset to apply to each RCS port.  Defaults to the model 'diameter' if unspecified.
        /// </summary>
        public readonly float posX;

        /// <summary>
        /// The vertical neutral position of the RCS ports, relative to the models origin.  If rcs vertical positionining is supported, this MUST be specified as the center of the offset range.
        /// </summary>
        public readonly float posY;

        /// <summary>
        /// The vertical +/- range that the RCS port may be moved through in the model at default model scale.
        /// </summary>
        public readonly float range;

        /// <summary>
        /// Angle to use when offsetting the RCS block along its specified vertical range.  To be used in the case of non cylindrical models that still want to support RCS position adjustment.
        /// </summary>
        public readonly float angle;

        public ModelAttachablePositionData(ConfigNode node)
        {
            posY = node.GetFloatValue("posY", 0);
            posX = node.GetFloatValue("posX", 0);
            range = node.GetFloatValue("range", 0);
            angle = node.GetFloatValue("angle", 0);
        }

        public void getModelPosition(float hScale, float vScale, float vRange, bool invert, out float oRadius, out float oPosY)
        {
            float rads = Mathf.Deg2Rad * angle;
            //position of the center of the offset
            float outX = posX * hScale;
            float outY = posY * vScale;
            if (invert) { outY = -outY; }
            float sRange = vScale * range * vRange;//scaled value to move along vector denoted by 'angle' from
            float xoff = Mathf.Sin(rads);//scale along x axis
            float yoff = Mathf.Cos(rads);//scale along y axis
            outX += xoff * sRange;
            outY += yoff * sRange;
            oRadius = outX;
            oPosY = outY;
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
