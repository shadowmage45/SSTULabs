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
            string name, shader;
            string[] modelNames;
            string[] excludeMeshes, meshes;
            GameObject go;
            for (int i = 0; i < len; i++)
            {
                textureNode = textureNodes[i];

                name = textureNode.GetStringValue("name");
                modelNames = textureNode.HasValue("model") ? textureNode.GetStringValues("model") : textureNode.GetStringValues("name");
                shader = textureNode.GetStringValue("shader");
                excludeMeshes = textureNode.GetStringValues("excludeMesh");
                meshes = textureNode.GetStringValues("mesh");
                props = ShaderProperty.parse(textureNode);
                int len2 = modelNames.Length;
                for (int k = 0; k < len2; k++)
                {
                    go = GameDatabase.Instance.GetModelPrefab(modelNames[k]);
                    if (go == null)
                    {
                        MonoBehaviour.print("ERROR: Could not locate model: " + modelNames[k] + ".  Could not update shader or textures for model for shader assignment set: "+name);
                        continue;
                    }
                    SSTUTextureUtils.updateModelMaterial(go.transform, excludeMeshes, meshes, shader, props);
                }
            }
        }
    }

}
