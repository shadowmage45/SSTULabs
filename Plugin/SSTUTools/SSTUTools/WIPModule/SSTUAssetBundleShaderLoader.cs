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

        public static void PartListLoaded()
        {
            MonoBehaviour.print("Updating part icons with fixed shaders.");
            applyToPartIcons();
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
            GameObject model;
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
                    model = GameDatabase.Instance.GetModelPrefab(modelNames[k]);
                    if (model == null)
                    {
                        MonoBehaviour.print("ERROR: Could not locate model: " + modelNames[k] + ".  Could not update shader or textures for model for shader assignment set: "+name);
                        continue;
                    }
                    SSTUTextureUtils.updateModelMaterial(model.transform, excludeMeshes, meshes, shader, props);
                }
            }
        }

        public static void applyToPartIcons()
        {
            //this is going to get ugly....
            /*
            First option is brute force, see implementation below.
            iterate through entire parts list
                iterate through get children (renderer)
                    if mesh uses SSTU shader
                        find identical transform on the icon and swap it to the SSTU-icon shader
            */
            /*
            Second option is??  (nothing currently)
            Need to know
            1.)  What parts to operate on.  
                    Not all parts, and not all SSTU parts, nor just the modular parts (solar panels, other stand-alones) 
                    Cannot use modular-part code as a singular fix, would only be a partial solution
            2.)  What transforms/meshes/renderers need to be swapped.
                    Even on parts that need shader swapping, not all renderers will need to be swapped.
            3.)  Eventually it might be necessary to include a dictionary of shaders to swap from-> to
                    In the case of incompatible features across shaders.  
                    Currently the masked shader supports all of the solar-shader features needed for icon rendering (no back-face lighting needed for icons...)
            */
            List<string> shaderNames = new List<string>();
            shaderNames.Add("SSTU/Masked");
            shaderNames.Add("SSTU/SolarShader");
            Shader iconShader = SSTUDatabase.getShader("SSTU/MaskedIcon");
            foreach (AvailablePart p in PartLoader.LoadedPartsList)
            {
                bool outputName = false;
                Transform pt = p.partPrefab.gameObject.transform;
                Renderer[] ptrs = pt.GetComponentsInChildren<Renderer>();
                foreach (Renderer ptr in ptrs)
                {
                    string ptsn = ptr.sharedMaterial.shader.name;
                    if (shaderNames.Contains(ptsn))//is a shader that we care about
                    {
                        if (!outputName)
                        {
                            MonoBehaviour.print("Adjusting icon shaders for part: " + p.name+" for shader:"+ptsn+" tr: "+ptr);
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

    }

}
