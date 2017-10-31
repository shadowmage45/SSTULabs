using System;
using System.Collections.Generic;
using UnityEngine;

namespace KSPShaderTools
{

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class KSPShaderLoader : MonoBehaviour
    {
        /*  
         *  Custom Shader Loading for KSP
         *  Includes loading of platform-specific bundles, or 'universal' bundles.  
         *  Bundles to be loaded are determined by config files (KSP_SHADER_BUNDLE)
         *  Each bundle can have multiple shaders in it.
         *  
         *  Shader / Icon shaders are determined by another config node (KSP_SHADER_DATA)
         *  with a key for shader = <shaderName> and iconShader = <iconShaderName>
         *  
         *  Shaders are applied to models in the database through a third config node (KSP_MODEL_SHADER)
         *  --these specify which database-model-URL to apply a specific texture set to (KSP_TEXTURE_SET)
         *  
         *  Texture sets (KSP_TEXTURE_SET) can be referenced in the texture-switch module for run-time texture switching capability.
         *  
         *  
         *  //eve shader loading data -- need to examine what graphics APIs the SSTU shaders are set to build for -- should be able to build 'universal' bundles
         *  https://github.com/WazWaz/EnvironmentalVisualEnhancements/blob/master/Assets/Editor/BuildABs.cs
         *  
         *
         */

        /// <summary>
        /// List of loaded shaders and corresponding icon shader.  Loaded from KSP_SHADER_DATA config nodes.
        /// </summary>
        public static Dictionary<string, ShaderData> loadedShaders = new Dictionary<string, ShaderData>();

        /// <summary>
        /// List of loaded global texture sets.  Loaded from KSP_TEXTURE_SET config nodes.
        /// </summary>
        public static Dictionary<string, TextureSet> loadedTextureSets = new Dictionary<string, TextureSet>();

        private static EventVoid.OnEvent partListLoadedEvent;

        public void Awake()
        {
            DontDestroyOnLoad(this);
            if (partListLoadedEvent == null)
            {
                partListLoadedEvent = new EventVoid.OnEvent(onPartListLoaded);
                GameEvents.OnPartLoaderLoaded.Add(partListLoadedEvent);
            }
        }

        public void OnDestroy()
        {
            GameEvents.OnPartLoaderLoaded.Remove(partListLoadedEvent);
        }

        public void ModuleManagerPostLoad()
        {
            load();
        }

        private static void load()
        {
            MonoBehaviour.print("KSPShaderLoader - Initializing shader and texture set data.");
            Dictionary<string, Shader> dict = new Dictionary<string, Shader>();
            loadBundles(dict);
            buildShaderSets(dict);
            PresetColor.loadColors();
            loadTextureSets();
            applyToModelDatabase();
        }

        private static void onPartListLoaded()
        {
            MonoBehaviour.print("KSPShaderLoader - Updating part icon shaders.");
            applyToPartIcons();
        }

        private static void loadBundles(Dictionary<string, Shader> dict)
        {
            ConfigNode[] shaderNodes = GameDatabase.Instance.GetConfigNodes("KSP_SHADER_BUNDLE");
            int len = shaderNodes.Length;
            for (int i = 0; i < len; i++)
            {
                loadBundle(shaderNodes[i], dict);
            }
        }

        private static void loadBundle(ConfigNode node, Dictionary<String, Shader> shaderDict)
        {
            string assetBundleName = "";
            if (node.HasValue("universal")) { assetBundleName = node.GetStringValue("universal"); }
            else if (Application.platform == RuntimePlatform.WindowsPlayer) { assetBundleName = node.GetStringValue("windows"); }
            else if (Application.platform == RuntimePlatform.LinuxPlayer) { assetBundleName = node.GetStringValue("linux"); }
            else if (Application.platform == RuntimePlatform.OSXPlayer) { assetBundleName = node.GetStringValue("osx"); }
            assetBundleName = KSPUtil.ApplicationRootPath + "GameData/" + assetBundleName;

            // KSP-PartTools built AssetBunldes are in the Web format, 
            // and must be loaded using a WWW reference; you cannot use the
            // AssetBundle.CreateFromFile/LoadFromFile methods unless you 
            // manually compiled your bundles for stand-alone use
            WWW www = CreateWWW(assetBundleName);

            if (!string.IsNullOrEmpty(www.error))
            {
                MonoBehaviour.print("KSPShaderLoader - Error while loading shader AssetBundle: " + www.error);
                return;
            }
            else if (www.assetBundle == null)
            {
                MonoBehaviour.print("KSPShaderLoader - Could not load AssetBundle from WWW - " + www);
                return;
            }

            AssetBundle bundle = www.assetBundle;

            string[] assetNames = bundle.GetAllAssetNames();
            int len = assetNames.Length;
            Shader shader;
            for (int i = 0; i < len; i++)
            {
                if (assetNames[i].EndsWith(".shader"))
                {
                    shader = bundle.LoadAsset<Shader>(assetNames[i]);
                    MonoBehaviour.print("KSPShaderLoader - Loaded Shader: " + shader.name + " :: " + assetNames[i]+" from bundle: "+assetBundleName);
                    shaderDict.Add(shader.name, shader);
                }
            }
            //this unloads the compressed assets inside the bundle, but leaves any instantiated shaders in-place
            bundle.Unload(false);
        }

        private static void buildShaderSets(Dictionary<string, Shader> dict)
        {
            ConfigNode[] shaderNodes = GameDatabase.Instance.GetConfigNodes("KSP_SHADER_DATA");
            ConfigNode node;
            int len = shaderNodes.Length;
            string sName, iName;
            for (int i = 0; i < len; i++)
            {
                node = shaderNodes[i];
                sName = node.GetStringValue("shader");
                iName = node.GetStringValue("iconShader", "KSP/Diffuse");
                Shader shader = dict[sName];
                Shader iconShader = dict[iName];
                ShaderData data = new ShaderData(shader, iconShader);
                loadedShaders.Add(shader.name, data);
            }
        }

        /// <summary>
        /// Creates a WWW URL reference for the input file-path
        /// </summary>
        /// <param name="bundlePath"></param>
        /// <returns></returns>
        private static WWW CreateWWW(string bundlePath)
        {
            try
            {
                string name = Application.platform == RuntimePlatform.WindowsPlayer ? "file:///" + bundlePath : "file://" + bundlePath;
                return new WWW(Uri.EscapeUriString(name));
            }
            catch (Exception e)
            {
                MonoBehaviour.print("Error while creating AssetBundle request: " + e);
                return null;
            }
        }

        private static void loadTextureSets()
        {
            ConfigNode[] setNodes = GameDatabase.Instance.GetConfigNodes("KSP_TEXTURE_SET");
            TextureSet[] sets = TextureSet.parse(setNodes);
            int len = sets.Length;
            for (int i = 0; i < len; i++)
            {
                loadedTextureSets.Add(sets[i].name, sets[i]);
            }
        }

        private static void applyToModelDatabase()
        {
            ConfigNode[] modelShaderNodes = GameDatabase.Instance.GetConfigNodes("KSP_MODEL_SHADER");
            TextureSet set;
            ConfigNode textureNode;
            int len = modelShaderNodes.Length;
            string[] modelNames;
            GameObject model;
            for (int i = 0; i < len; i++)
            {
                textureNode = modelShaderNodes[i];
                set = loadedTextureSets[textureNode.GetStringValue("textureSet")];
                modelNames = textureNode.GetStringValues("model");
                int len2 = modelNames.Length;
                for (int k = 0; k < len2; k++)
                {
                    model = GameDatabase.Instance.GetModelPrefab(modelNames[i]);
                    if (model != null)
                    {
                        set.enable(model, set.maskColors);
                    }
                }
            }
        }

        private static void applyToPartIcons()
        {
            //brute-force method for fixing part icon shaders
            //  iterate through entire loaded parts list
            //      iterate through every transform with a renderer component
            //          if renderer uses a shader in the shader-data-list
            //              replace shader on icon with the 'icon shader' corresponding to the current shader
            Shader iconShader;
            foreach (AvailablePart p in PartLoader.LoadedPartsList)
            {
                bool outputName = false;//only log the adjustment a single time
                Transform pt = p.partPrefab.gameObject.transform;
                Renderer[] ptrs = pt.GetComponentsInChildren<Renderer>();
                foreach (Renderer ptr in ptrs)
                {
                    string ptsn = ptr.sharedMaterial.shader.name;
                    if (loadedShaders.ContainsKey(ptsn))//is a shader that we care about
                    {
                        iconShader = loadedShaders[ptsn].iconShader;
                        if (!outputName)
                        {
                            MonoBehaviour.print("KSPShaderLoader - Adjusting icon shaders for part: " + p.name + " for original shader:" + ptsn + " replacement: " + iconShader.name);
                            outputName = true;
                        }
                        Transform[] ictrs = p.iconPrefab.gameObject.transform.FindChildren(ptr.name);//find transforms from icon with same name
                        foreach (Transform ictr in ictrs)
                        {
                            Renderer itr = ictr.GetComponent<Renderer>();
                            if (itr != null)
                            {
                                itr.sharedMaterial.shader = iconShader;
                            }
                        }
                    }
                }
            }
        }

        public static Shader getShader(string name)
        {
            if (loadedShaders.ContainsKey(name))
            {
                return loadedShaders[name].shader;
            }
            Shader s = GameDatabase.Instance.databaseShaders.Find(m => m.name == name);
            if (s != null)
            {
                return s;
            }
            return Shader.Find(name);
        }

        public static TextureSet getTextureSet(string name)
        {
            TextureSet s = null;
            if (loadedTextureSets.TryGetValue(name, out s))
            {
                return s;
            }
            MonoBehaviour.print("ERROR: Could not locate texture set for name: " + name);
            return null;
        }

        public static TextureSet[] getTextureSets(ConfigNode[] setNodes)
        {
            int len = setNodes.Length;
            TextureSet[] sets = new TextureSet[len];
            for (int i = 0; i < len; i++)
            {
                sets[i] = getTextureSet(setNodes[i].GetStringValue("name"));
            }
            return sets;
        }

    }

    public class ShaderData
    {
        public readonly Shader shader;
        public readonly Shader iconShader;

        public ShaderData(Shader shader, Shader iconShader)
        {
            this.shader = shader;
            this.iconShader = iconShader;
        }
    }

    //abstract class defining a shader property that can be loaded from a config-node
    public abstract class ShaderProperty
    {
        public readonly string name;

        public static ShaderProperty[] parse(ConfigNode node)
        {
            List<ShaderProperty> props = new List<ShaderProperty>();
            //direct property nodes
            ConfigNode[] propNodes = node.GetNodes("PROPERTY");
            int len = propNodes.Length;
            for (int i = 0; i < len; i++)
            {
                if (propNodes[i].HasValue("texture"))
                {
                    props.Add(new ShaderPropertyTexture(propNodes[i]));
                }
                else if (propNodes[i].HasValue("color"))
                {
                    props.Add(new ShaderPropertyColor(propNodes[i]));
                }
                else if (propNodes[i].HasValue("float"))
                {
                    props.Add(new ShaderPropertyFloat(propNodes[i]));
                }
            }
            //simply/lazy texture assignments
            string[] textures = node.GetStringValues("texture");
            len = textures.Length;
            string[] splits;
            string name, tex;
            bool main, nrm;
            for (int i = 0; i < len; i++)
            {
                splits = textures[i].Split(',');
                name = splits[0].Trim();
                tex = splits[1].Trim();
                main = splits[0] == "_MainTex";
                nrm = splits[0] == "_BumpMap";
                props.Add(new ShaderPropertyTexture(name, tex, main, nrm));
            }
            return props.ToArray();
        }

        protected ShaderProperty(ConfigNode node)
        {
            this.name = node.GetStringValue("name");
        }

        protected ShaderProperty(string name)
        {
            this.name = name;
        }

        public void apply(Material mat)
        {
            applyInternal(mat);
        }

        protected abstract void applyInternal(Material mat);

        //protected abstract string getStringValue();

        protected bool checkApply(Material mat)
        {
            if (mat.HasProperty(name))
            {
                return true;
            }
            else
            {
                MonoBehaviour.print("Shader: " + mat.shader + " did not have property: " + name);
            }
            return false;
        }

    }

    public class ShaderPropertyColor : ShaderProperty
    {
        public readonly Color color;

        public ShaderPropertyColor(ConfigNode node) : base(node)
        {
            color = node.GetColorFromFloatCSV("color");
        }

        public ShaderPropertyColor(string name, Color color) : base(name)
        {
            this.color = color;
        }

        protected override void applyInternal(Material mat)
        {
            mat.SetColor(name, color);
        }
    }

    public class ShaderPropertyFloat : ShaderProperty
    {
        public readonly float val;

        public ShaderPropertyFloat(ConfigNode node) : base(node)
        {
            val = node.GetFloatValue("float");
        }

        public ShaderPropertyFloat(string name, float val) : base(name)
        {
            this.val = val;
        }

        protected override void applyInternal(Material mat)
        {
            if (checkApply(mat))
            {
                mat.SetFloat(name, val);
            }
        }
    }

    public class ShaderPropertyTexture : ShaderProperty
    {
        public readonly string textureName;
        public readonly bool main;
        public readonly bool normal;

        public ShaderPropertyTexture(ConfigNode node) : base(node)
        {
            textureName = node.GetStringValue("texture");
            main = node.GetBoolValue("main");
            normal = node.GetBoolValue("normal");
        }

        public ShaderPropertyTexture(string name, string texture, bool main, bool normal) : base(name)
        {
            this.textureName = texture;
            this.main = main;
            this.normal = normal;
        }

        protected override void applyInternal(Material mat)
        {
            if (checkApply(mat))
            {
                if (main)
                {
                    mat.mainTexture = GameDatabase.Instance.GetTexture(textureName, false);
                }
                else
                {
                    mat.SetTexture(name, GameDatabase.Instance.GetTexture(textureName, normal));
                }
            }
        }
    }
    
    public class PresetColor
    {
        private static List<PresetColor> colorList = new List<PresetColor>();
        private static Dictionary<String, PresetColor> presetColors = new Dictionary<string, PresetColor>();

        public readonly string name;
        public readonly string title;
        public readonly Color color;

        public PresetColor(ConfigNode node)
        {
            name = node.GetStringValue("name");
            title = node.GetStringValue("title");
            color = node.GetColorFromByteCSV("color");
        }

        internal static void loadColors()
        {
            colorList.Clear();
            presetColors.Clear();
            PresetColor color;
            ConfigNode[] colorNodes = GameDatabase.Instance.GetConfigNodes("KSP_COLOR_PRESET");
            int len = colorNodes.Length;
            for (int i = 0; i < len; i++)
            {
                color = new PresetColor(colorNodes[i]);
                colorList.Add(color);
                presetColors.Add(color.name, color);
            }
        }

        public static PresetColor getColor(string name)
        {
            if (!presetColors.ContainsKey(name)) { MonoBehaviour.print("ERROR: No Color data for name: " + name); }
            return presetColors[name];
        }

        public static List<PresetColor> getColorList() { return colorList; }

    }

}
