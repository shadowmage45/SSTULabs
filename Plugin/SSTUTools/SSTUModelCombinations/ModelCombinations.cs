using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SSTUModelCombinations
{
    public class ModelCombinations
    {

        private static List<ConfigNode> allRootNodes = new List<ConfigNode>();
        private static List<ConfigNode> partNodes = new List<ConfigNode>();
        private static List<ConfigNode> modularPartNodes = new List<ConfigNode>();
        private static List<ConfigNode> modelDefinitions = new List<ConfigNode>();
        private static Dictionary<string, Model> modelDefinitionsByName = new Dictionary<string, Model>();

        private static List<Model> usedCores = new List<Model>();
        private static List<Model> usedUpper = new List<Model>();
        private static List<Model> usedNoses = new List<Model>();
        private static List<Model> usedLower = new List<Model>();
        private static List<Model> usedMount = new List<Model>();

        private static int foundCombinations = 0;
        private static int incompatibleCount = 0;
        private static int unusedModelsCount = 0;

        private static StreamWriter combinationStream;
        private static StreamWriter incompExceptionStream;
        private static StreamWriter unusedExceptionStream;
        private static StreamWriter logStream;

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                throw new InvalidOperationException("Not enough arguments passed.  Must include relative or absolute path to GameData folder.");
            }
            string gameDataPath = args[0];
            string fullPath = Path.GetFullPath(gameDataPath);
            
            string outputPath = fullPath + "\\..\\output";
            //create the directory in case it didn't previously exist
            Directory.CreateDirectory(outputPath);
            string outputFile = outputPath + "/combinations.txt";
            string incompExceptionsFile = outputPath + "/incompatible.txt";
            string unusedExceptionsFile = outputPath + "/unused.txt";

            logStream = new StreamWriter(new FileStream(outputPath + "/log.txt", FileMode.Create));
            combinationStream = new StreamWriter(new FileStream(outputFile, FileMode.Create));
            incompExceptionStream = new StreamWriter(new FileStream(incompExceptionsFile, FileMode.Create));
            unusedExceptionStream = new StreamWriter(new FileStream(unusedExceptionsFile, FileMode.Create));

            print("Parsing configs from folder: " + fullPath);

            parseDirectory(fullPath);
            findSpecificConfigs();
            findAllCombinations();

            logStream.Flush();
            combinationStream.Flush();
            incompExceptionStream.Flush();
            unusedExceptionStream.Flush();

            print("Examined: " + modularPartNodes.Count + " modular parts.");
            print("Found   : " + foundCombinations + " valid combinations across all modular parts.");
            print("Logged  : " + incompatibleCount + " incompatible combinations");
            print("Logged  : " + unusedModelsCount + " unused models");
            print("Combinations written to: " + Path.GetFullPath(outputFile));
            print("Exceptions written to: " + Path.GetFullPath(incompExceptionsFile));
            print("Finished processing.  Press any key to exit.");
            System.Console.ReadLine();
        }

        private static void parseDirectory(string path)
        {
            string[] dirs = Directory.GetDirectories(path);
            int len = dirs.Length;
            for (int i = 0; i < len; i++)
            {
                parseDirectory(dirs[i]);
            }

            string[] files = Directory.GetFiles(path);
            len = files.Length;
            for (int i = 0; i < len; i++)
            {
                parseFile(files[i]);
            }
        }

        private static void parseFile(string path)
        {
            if (path.EndsWith(".cfg"))
            {
                ConfigNode foundNode = ConfigNode.Load(path);
                if (foundNode != null)
                {
                    allRootNodes.Add(foundNode);
                }
            }            
        }

        private static void findSpecificConfigs()
        {
            ConfigNode[] nodes;
            int len = allRootNodes.Count;
            for (int i = 0; i < len; i++)
            {
                nodes = allRootNodes[i].GetNodes("PART");
                partNodes.AddRange(nodes);
                nodes = allRootNodes[i].GetNodes("SSTU_MODEL");
                modelDefinitions.AddRange(nodes);
            }

            len = partNodes.Count;
            for (int i = 0; i < len; i++)
            {
                ConfigNode partNode = partNodes[i];
                ConfigNode[] moduleNodes = partNode.GetNodes("MODULE");
                int len2 = moduleNodes.Length;
                for (int k = 0; k < len2; k++)
                {
                    if (moduleNodes[k].GetValue("name") == "SSTUModularPart")
                    {
                        moduleNodes[k].AddValue("partName", partNode.GetValue("name"));
                        modularPartNodes.Add(moduleNodes[k]);
                    }
                }
            }
            len = modelDefinitions.Count;
            for (int i = 0; i < len; i++)
            {
                Model model = new Model(modelDefinitions[i]);
                if (!modelDefinitionsByName.ContainsKey(model.name))
                {
                    modelDefinitionsByName.Add(model.name, model);
                }
                else
                {
                    print("ERROR: Duplicate model defintion found for name: " + model.name);
                }                
            }
            print("Found total of: " + partNodes.Count + " PART nodes");
            print("Found total of: " + modularPartNodes.Count + " SSTUModularPart module nodes");
            print("Found total of: " + modelDefinitionsByName.Values.Count + " model definitions");
        }

        public static void print(string value)
        {
            logStream.WriteLine(value);
            System.Console.WriteLine(value);
        }

        private static void findAllCombinations()
        {
            int len = modularPartNodes.Count;
            for (int i = 0; i < len; i++)
            {
                findPartCombinations(modularPartNodes[i].GetValue("partName"), modularPartNodes[i]);
            }
        }

        private static void findPartCombinations(string name, ConfigNode modularPartNode)
        {
            combinationStream.WriteLine("PART>>>>-------------------- " + name + " --------------------<<<<");
            unusedExceptionStream.WriteLine("UNUSED>>>>-------------------- " + name + " --------------------<<<<");
            incompExceptionStream.WriteLine("INCOMP>>>>-------------------- " + name + " --------------------<<<<");
            List<Model> noseModels = new List<Model>();
            List<Model> upperModels = new List<Model>();
            List<Model> coreModels = new List<Model>();
            List<Model> lowerModels = new List<Model>();
            List<Model> mountModels = new List<Model>();

            ConfigNode[] coreNodes = modularPartNode.GetNodes("CORE");
            int len = coreNodes.Length;
            for (int i = 0; i < len; i++)
            {
                string[] names = coreNodes[i].GetValues("model");
                addModels(names, coreModels);
            }
            addModels(modularPartNode.GetNode("NOSE").GetValues("model"), noseModels);
            addModels(modularPartNode.GetNode("UPPER").GetValues("model"), upperModels);
            addModels(modularPartNode.GetNode("LOWER").GetValues("model"), lowerModels);
            addModels(modularPartNode.GetNode("MOUNT").GetValues("model"), mountModels);
            print("Model Counts for part: "+name+" --\ncores : " + coreModels.Count + "\nuppers: " + upperModels.Count + "\nnoses : " + noseModels.Count + "\nlowers: " + lowerModels.Count + "\nmounts: " + mountModels.Count);
            len = coreModels.Count;
            for (int i = 0; i < len; i++)
            {
                findCombinations(coreModels[i], coreModels, upperModels, noseModels, lowerModels, mountModels);
            }
            findUnusedModels("NOSE      ", noseModels, usedNoses);
            findUnusedModels("UPPER     ", upperModels, usedUpper);
            findUnusedModels("CORE      ", coreModels, usedCores);
            findUnusedModels("LOWER     ", lowerModels, usedLower);
            findUnusedModels("MOUNT     ", mountModels, usedMount);
            usedNoses.Clear();
            usedUpper.Clear();
            usedCores.Clear();
            usedLower.Clear();
            usedMount.Clear();
        }

        private static void addModels(string[] names, List<Model> models)
        {
            Model model = null;
            int len = names.Length;
            for (int i = 0; i < len; i++)
            {
                if (modelDefinitionsByName.TryGetValue(names[i], out model))
                {
                    models.Add(model);
                }
                else
                {
                    print("ERROR: Could not locate modeldefinition for name: " + names[i]);
                }
            }
        }

        private static void findCombinations(Model coreModel, List<Model> cores, List<Model> upperModels, List<Model> noseModels, List<Model> lowerModels, List<Model> mountModels)
        {
            combinationStream.WriteLine("CORE>>>>-------------------- " + coreModel.name + " --------------------<<<<");
            int upperLen = upperModels.Count;
            int noseLen = noseModels.Count;
            int lowerLen = lowerModels.Count;
            int mountLen = mountModels.Count;
            Model upperModel;
            Model noseModel;
            Model lowerModel;
            Model mountModel;
            for (int a = 0; a < upperLen; a++)
            {
                upperModel = upperModels[a];
                if (coreModel.isValidUpperProfile(upperModel.getLowerProfiles(ModelOrientation.TOP), ModelOrientation.CENTRAL) && upperModel.isValidLowerProfile(coreModel.getUpperProfiles(ModelOrientation.CENTRAL), ModelOrientation.TOP))
                {
                    for (int b = 0; b < noseLen; b++)
                    {
                        noseModel = noseModels[b];
                        if (upperModel.isValidUpperProfile(noseModel.getLowerProfiles(ModelOrientation.TOP), ModelOrientation.TOP) && noseModel.isValidLowerProfile(upperModel.getUpperProfiles(ModelOrientation.TOP), ModelOrientation.TOP))
                        {
                            for (int c = 0; c < lowerLen; c++)
                            {
                                lowerModel = lowerModels[c];
                                if (coreModel.isValidLowerProfile(lowerModel.getUpperProfiles(ModelOrientation.BOTTOM), ModelOrientation.CENTRAL) && lowerModel.isValidUpperProfile(coreModel.getLowerProfiles(ModelOrientation.CENTRAL), ModelOrientation.BOTTOM))
                                {
                                    for (int d = 0; d < mountLen; d++)
                                    {
                                        mountModel = mountModels[d];
                                        if (lowerModel.isValidLowerProfile(mountModel.getUpperProfiles(ModelOrientation.BOTTOM), ModelOrientation.BOTTOM) && mountModel.isValidUpperProfile(lowerModel.getLowerProfiles(ModelOrientation.BOTTOM), ModelOrientation.BOTTOM))
                                        {
                                            addCombination(noseModel, upperModel, coreModel, lowerModel, mountModel);
                                        }
                                        else
                                        {
                                            logException("LOWER/MOUNT", lowerModel, ModelOrientation.BOTTOM, false, mountModel, ModelOrientation.BOTTOM, true);
                                        }
                                    }
                                }
                                else
                                {
                                    logException("CORE/LOWER ", coreModel, ModelOrientation.CENTRAL, false, lowerModel, ModelOrientation.BOTTOM, true);
                                }
                            }
                        }
                        else
                        {
                            logException("UPPER/NOSE ", upperModel, ModelOrientation.TOP, true, noseModel, ModelOrientation.TOP, false);
                        }
                    }
                }
                else
                {
                    logException("CORE/UPPER ", coreModel, ModelOrientation.CENTRAL, true, upperModel, ModelOrientation.TOP, false);
                }
            }
        }

        private static void addCombination(Model nose, Model upper, Model core, Model lower, Model mount)
        {
            foundCombinations++;
            string output = nose.name + "," + upper.name + "," + core.name + "," + lower.name + "," + mount.name;
            combinationStream.WriteLine(output);
            usedNoses.AddUnique(nose);
            usedUpper.AddUnique(upper);
            usedCores.AddUnique(core);
            usedLower.AddUnique(lower);
            usedMount.AddUnique(mount);
        }

        private static void logException(string slot, Model a, ModelOrientation ao, bool aUpper, Model b, ModelOrientation bo, bool bUpper)
        {
            string[] aProf = aUpper ? a.getUpperProfiles(ao) : a.getLowerProfiles(ao);
            string[] aComp = aUpper ? a.getCompatibleLowerProfiles(ao) : a.getCompatibleUpperProfiles(ao);

            string[] bProf = bUpper ? b.getUpperProfiles(bo) : b.getLowerProfiles(bo);
            string[] bComp = bUpper ? b.getCompatibleLowerProfiles(bo) : b.getCompatibleUpperProfiles(bo);

            string apnf = "";
            string bpnf = "";

            int len = aProf.Length;
            for (int i = 0; i < len; i++)
            {
                if (!bComp.Contains(aProf[i]))
                {
                    if (!string.IsNullOrEmpty(apnf)) { apnf += ","; }
                    apnf += aProf[i];
                }
            }
            len = bProf.Length;
            for (int i = 0; i < len; i++)
            {
                if (!aComp.Contains(bProf[i]))
                {
                    if (!string.IsNullOrEmpty(bpnf)) { bpnf += ","; }
                    bpnf += bProf[i];
                }
            }
            string output = "Incomp Model Exception for slots: "+slot+" Models: " + a.name + " / " + b.name+" ";
            if (!string.IsNullOrEmpty(apnf))
            {
                output += "apnf: " + apnf;
                if (!string.IsNullOrEmpty(bpnf))
                {
                    output += " : ";
                }
            }
            if (!string.IsNullOrEmpty(bpnf))
            {
                output += "bpnf: " + bpnf;
            }
            incompExceptionStream.WriteLine(output);
        }

        private static void logUnusedModelException(string slot, List<Model> unused)
        {
            if (unused.Count <= 0) { return; }
            int len = unused.Count;
            string output = "Unused Model Exception for slot : " + slot+" ";
            for (int i = 0; i < len; i++)
            {
                unusedExceptionStream.WriteLine(output + " Model : "+unused[i].name);
            }
        }

        private static void findUnusedModels(string slot, List<Model> allModels, List<Model> usedModels)
        {
            List<Model> unUsedModels = new List<Model>();
            int len = allModels.Count;
            for (int i = 0; i < len; i++)
            {
                if (!usedModels.Contains(allModels[i])) { unUsedModels.Add(allModels[i]); }
            }
            logUnusedModelException(slot, unUsedModels);
        }

    }

    public class Model
    {

        public readonly string name;
        public readonly string[] upperProfiles;
        public readonly string[] compatibleUpperProfiles;
        public readonly string[] lowerProfiles;
        public readonly string[] compatibleLowerProfiles;
        public readonly ModelOrientation orientation;

        public Model(ConfigNode node)
        {
            name = node.GetValue("name");
            upperProfiles = node.GetValues("upperProfile");
            lowerProfiles = node.GetValues("lowerProfile");
            compatibleUpperProfiles = node.GetValues("compatibleUpperProfile");
            compatibleLowerProfiles = node.GetValues("compatibleLowerProfile");
            if (node.HasValue("orientation"))
            {
                orientation = (ModelOrientation)Enum.Parse(typeof(ModelOrientation), node.GetValue("orientation"));
            }
            else
            {
                System.Console.WriteLine("No orientation data parsed for model def: " + name);
                orientation = ModelOrientation.TOP;
            }
        }

        public string[] getLowerProfiles(ModelOrientation orientation)
        {
            return shouldInvert(orientation) ? upperProfiles : lowerProfiles;
        }

        public string[] getUpperProfiles(ModelOrientation orientation)
        {
            return shouldInvert(orientation) ? lowerProfiles : upperProfiles;
        }

        public string[] getCompatibleUpperProfiles(ModelOrientation orientation)
        {
            return shouldInvert(orientation) ? compatibleLowerProfiles : compatibleUpperProfiles;
        }

        public string[] getCompatibleLowerProfiles(ModelOrientation orientation)
        {
            return shouldInvert(orientation) ? compatibleUpperProfiles : compatibleLowerProfiles;
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

        public static void print(string value)
        {
            System.Console.WriteLine(value);
        }

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

}
