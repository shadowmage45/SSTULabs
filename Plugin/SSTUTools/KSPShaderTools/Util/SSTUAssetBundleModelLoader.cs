using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using System.Collections;
using System.IO;

namespace SSTUTools
{
    [DatabaseLoaderAttrib((new string[] { "smf" }))]
    public class SMFBundleDefinitionReader : DatabaseLoader<GameObject>
    {
        public override IEnumerator Load(UrlDir.UrlFile urlFile, FileInfo file)
        {
            // KSP-PartTools built AssetBunldes are in the Web format, 
            // and must be loaded using a WWW reference; you cannot use the 
            // AssetBundle.CreateFromFile/LoadFromFile methods unless you 
            // manually compiled your bundles for stand-alone use
            WWW www = CreateWWW(urlFile.fullPath);
            //not sure why the yield statement here, have not investigated removing it.
            yield return www;

            if (!string.IsNullOrEmpty(www.error))
            {
                MonoBehaviour.print("Error while loading AssetBundle model: " + www.error+" for url: "+urlFile.url+" :: "+urlFile.fullPath);
                yield break;
            }
            else if (www.assetBundle == null)
            {
                MonoBehaviour.print("Could not load AssetBundle from WWW - " + www);
                yield break;
            }

            AssetBundle bundle = www.assetBundle;

            //TODO clean up linq
            string modelName = bundle.GetAllAssetNames().FirstOrDefault(assetName => assetName.EndsWith("prefab"));
            AssetBundleRequest abr = bundle.LoadAssetAsync<GameObject>(modelName);
            while (!abr.isDone) { yield return abr; }//continue to yield until the asset load has returned from the loading thread
            if (abr.asset == null)//if abr.isDone==true and asset is null, there was a major error somewhere, likely file-system related
            {
                MonoBehaviour.print("ERROR: Failed to load model from asset bundle!");
                yield break;
            }
            GameObject model = GameObject.Instantiate((GameObject)abr.asset);//make a copy of the asset
            setupModelTextures(urlFile.root, model);
            this.obj = model;
            this.successful = true;
            //this unloads the compressed assets inside the bundle, but leaves any instantiated models in-place
            bundle.Unload(false);
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

        private static void setupModelTextures(UrlDir dir, GameObject model)
        {
            Renderer[] renders = model.GetComponentsInChildren<Renderer>(true);
            Material m;
            List<Material> adjustedMaterials = new List<Material>();
            foreach (Renderer render in renders)
            {
                m = render.sharedMaterial;
                if (adjustedMaterials.Contains(m)) { continue; }//already fixed that material (many are shared across transforms), so skip it
                else { adjustedMaterials.Add(m); }
                replaceShader(m, m.shader.name);
                replaceTexture(m, "_MainTex", false);
                replaceTexture(m, "_SpecMap", false);
                replaceTexture(m, "_MetallicGlossMap", false);
                replaceTexture(m, "_BumpMap", true);
                replaceTexture(m, "_Emissive", false);
                replaceTexture(m, "_AOMap", false);
            }
        }

        private static void replaceShader(Material m, string name)
        {
            m.shader = KSPShaderTools.KSPShaderLoader.getShader(name);
        }

        private static void replaceTexture(Material m, string name, bool nrm = false)
        {
            Texture tex = m.GetTexture(name);
            if (tex != null && !string.IsNullOrEmpty(tex.name))
            {
                Texture newTex = findTexture(tex.name, nrm);
                if (newTex != null)
                {
                    m.SetTexture(name, newTex);
                }
            }
        }

        private static Texture2D findTexture(string name, bool nrm = false)
        {
            //TODO clean up foreach
            foreach (GameDatabase.TextureInfo t in GameDatabase.Instance.databaseTexture)
            {
                if (t.file.url.EndsWith(name))
                {
                    if (nrm) { return t.normalMap; }
                    return t.texture;
                }
            }
            return null;
        }
        
    }

}
