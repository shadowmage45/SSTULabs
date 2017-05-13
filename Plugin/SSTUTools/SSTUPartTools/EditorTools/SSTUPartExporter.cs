using UnityEngine;
using UnityEditor;
using System.IO;

namespace SSTUPartTools
{
    [AddComponentMenu("SSTU/Part Exporter")]    
    public class SSTUPartExporter : MonoBehaviour
    {
        public string modelName = "NewModel";

        public void exportModel()
        {
            Directory.CreateDirectory("Assets/BuildTemp");
            Directory.CreateDirectory("Assets/Build");
            string full = "Assets/BuildTemp";
            string prefabName = full + "/" + modelName + ".prefab";

            GameObject clone = GameObject.Instantiate(gameObject);
            clone.name = gameObject.name;
            SSTUPartExporter spe = clone.GetComponent<SSTUPartExporter>();
            Component.DestroyImmediate(spe);
            GameObject prefab = PrefabUtility.CreatePrefab(prefabName, clone);

            string path = AssetDatabase.GetAssetPath(prefab);
            AssetImporter ai = AssetImporter.GetAtPath(path);
            MonoBehaviour.print("path: " + path + " :: ai: " + ai.assetBundleName);
            ai.assetBundleName = modelName;

            Directory.CreateDirectory("Assets/Build");
            Directory.CreateDirectory("Assets/GameData");
            AssetBundleBuild[] build = new AssetBundleBuild[1];
            AssetBundleBuild bld = new AssetBundleBuild();
            bld.assetBundleName = modelName;
            bld.assetNames = new string[] { path };
            build[0] = bld;
            BuildPipeline.BuildAssetBundles("Assets/Build", build, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);

            string inName = "Assets/Build/" + modelName.ToLower();
            string outName = "Assets/GameData/" + modelName + ".smf";
            if (File.Exists(outName)) { File.Delete(outName); }
            File.Move(inName, outName);
            GameObject.DestroyImmediate(clone);
        }
    }
}
