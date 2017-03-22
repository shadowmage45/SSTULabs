using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUAssetBundleShaderLoader
    {

        public static void Load(Dictionary<String, Shader> shaderDict)
        {
            String shadersPath = KSPUtil.ApplicationRootPath + "GameData/SSTU/Shaders";
            String assetBundleName = "sstushaders";
            if (Application.platform == RuntimePlatform.WindowsPlayer) { assetBundleName += "-x64"; }
            else if (Application.platform == RuntimePlatform.LinuxPlayer) { assetBundleName += "-lin"; }
            else if (Application.platform == RuntimePlatform.OSXPlayer) { assetBundleName += "-osx"; }
            assetBundleName += ".ssf";
            // KSP-PartTools built AssetBunldes are in the Web format, 
            // and must be loaded using a WWW reference; you cannot use the 
            // AssetBundle.CreateFromFile/LoadFromFile methods unless you 
            // manually compiled your bundles for stand-alone use
            WWW www = CreateWWW(shadersPath + "/" + assetBundleName);

            if (!string.IsNullOrEmpty(www.error))
            {
                MonoBehaviour.print("Error while loading AssetBundle model: " + www.error);
                //yield break;
            }
            else if (www.assetBundle == null)
            {
                MonoBehaviour.print("Could not load AssetBundle from WWW - " + www);
                //yield break;
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
                    MonoBehaviour.print("Loaded shader: " + shader.name + " :: " + assetNames[i]);
                    shaderDict.Add(shader.name, shader);
                }
            }
            //this unloads the compressed assets inside the bundle, but leaves any instantiated models in-place
            bundle.Unload(false);
            applyToModelDatabase();
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

        public static void applyToModelDatabase()
        {
            ConfigNode[] textureNodes = GameDatabase.Instance.GetConfigNodes("SSTU_SHADER");
            ConfigNode textureNode;
            ShaderProperty[] props;
            int len = textureNodes.Length;
            string name, shader, diff, nrm, spec, glow, ao;
            string[] excludeMeshes, meshes;
            for (int i = 0; i < len; i++)
            {
                textureNode = textureNodes[i];
                name = textureNode.GetStringValue("name");
                shader = textureNode.GetStringValue("shader");
                diff = textureNode.GetStringValue("diffuse");
                nrm = textureNode.GetStringValue("normal");
                spec = textureNode.GetStringValue("specular");
                glow = textureNode.GetStringValue("emissive");
                ao = textureNode.GetStringValue("occlusion");
                excludeMeshes = textureNode.GetStringValues("excludeMesh");
                meshes = textureNode.GetStringValues("mesh");
                props = ShaderProperty.parse(textureNode);

                GameObject go = GameDatabase.Instance.GetModelPrefab(name);
                if (go == null)
                {
                    MonoBehaviour.print("ERROR: Could not locate game object for model: " + name + ".  Could not update shader or textures for model");
                    continue;
                }
                MonoBehaviour.print("Updating shader for model: " + name);
                updateModelMaterial(go.transform, excludeMeshes, meshes, shader, diff, nrm, spec, glow, ao, props);
            }
        }

        public static void updateModelMaterial(Transform root, string[] excludeMeshes, string[] meshes, string shader, string diffuse, string normal, string specular, string emissive, string occlusion, ShaderProperty[] props)
        {
            //black-list, do everything not specified in excludeMeshes array
            if (excludeMeshes != null && excludeMeshes.Length > 0)
            {
                Renderer[] allRends = root.GetComponentsInChildren<Renderer>();
                int len = allRends.Length;
                for (int i = 0; i < len; i++)
                {
                    if (!excludeMeshes.Contains(allRends[i].name))
                    {
                        updateRenderer(allRends[i], shader, diffuse, normal, specular, emissive, occlusion, props);
                    }
                }
            }
            else if (meshes == null || meshes.Length <= 0)//no validation, do them all
            {
                Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
                int len = rends.Length;
                for (int i = 0; i < len; i++)
                {
                    updateRenderer(rends[i], shader, diffuse, normal, specular, emissive, occlusion, props);
                }
            }
            else//white-list, only do what is specified by meshes array
            {
                int len = meshes.Length;
                Transform tr;
                Renderer r;
                for (int i = 0; i < len; i++)
                {
                    tr = root.FindRecursive(meshes[i]);
                    if (tr == null) { continue; }
                    r = tr.GetComponent<Renderer>();
                    if (r == null) { continue; }
                    updateRenderer(r, shader, diffuse, normal, specular, emissive, occlusion, props);
                }
            }
        }

        public static void updateRenderer(Renderer rend, string shader, string diffuse, string normal, string specular, string emissive, string occlusion, ShaderProperty[] props)
        {
            Material m = rend.material;
            if (!String.IsNullOrEmpty(shader) && shader != m.shader.name) { m.shader = SSTUDatabase.getShader(shader); }
            if (!String.IsNullOrEmpty(diffuse)) { m.mainTexture = GameDatabase.Instance.GetTexture(diffuse, false); }
            if (!String.IsNullOrEmpty(normal)) { m.SetTexture("_BumpMap", GameDatabase.Instance.GetTexture(normal, true)); }
            if (!String.IsNullOrEmpty(specular)) { m.SetTexture("_SpecMap", GameDatabase.Instance.GetTexture(specular, false)); }
            if (!String.IsNullOrEmpty(emissive)) { m.SetTexture("_Emissive", GameDatabase.Instance.GetTexture(emissive, false)); }
            if (!String.IsNullOrEmpty(occlusion)) { m.SetTexture("_AOMap", GameDatabase.Instance.GetTexture(occlusion, false)); }
            updateMaterialProperties(m, props);
        }

        public static void updateMaterialProperties(Material m, ShaderProperty[] props)
        {
            if (m == null || props == null || props.Length == 0) { return; }
            int len = props.Length;
            for (int i = 0; i < len; i++)
            {
                props[i].apply(m);
            }
        }
    }

    public class ShaderProperty
    {
        public readonly string name;
        public readonly float floatVal;
        public readonly Color colorVal;
        public readonly bool fVal;

        public static ShaderProperty[] parse(ConfigNode node)
        {
            ShaderProperty[] props;
            ConfigNode[] propNodes = node.GetNodes("PROPERTY");
            int len = propNodes.Length;
            props = new ShaderProperty[len];
            for (int i = 0; i < len; i++)
            {
                props[i] = new ShaderProperty(propNodes[i]);
            }
            return props;
        }

        public ShaderProperty(ConfigNode node)
        {
            name = node.GetStringValue("name");
            if (node.HasValue("float"))
            {
                fVal = true;
                floatVal = node.GetFloatValue("float");
            }
            else
            {
                fVal = false;
                colorVal = node.getColor("color");
            }
        }

        public void apply(Material mat)
        {
            if (mat.HasProperty(name))
            {
                if (fVal)
                {
                    mat.SetFloat(name, floatVal);
                }
                else
                {
                    mat.SetColor(name, colorVal);
                }
            }
            else
            {
                MonoBehaviour.print("Shader: " + mat.shader + " did not have property: " + name);
            }
        }
    }

}
