using UnityEngine;
using UnityEditor;
using System.Collections;

public class AssetBundleCompiler
{	
	[MenuItem ("Assets/Build Selected AssetBundle Win64")]
    static void BuildAssetBundleWin64 ()
    {
        exportAssetBundle(BuildTarget.StandaloneWindows64);
    }

    [MenuItem("Assets/Build Selected AssetBundle OSX")]
    static void BuildAssetBundleOSX()
    {
        exportAssetBundle(BuildTarget.StandaloneOSXUniversal);
    }

    [MenuItem("Assets/Build Selected AssetBundle Linux")]
    static void BuildAssetBundleLinux()
    {
        exportAssetBundle(BuildTarget.StandaloneLinux);
    }

    private static void exportAssetBundle(BuildTarget target)
    {
        string path = EditorUtility.SaveFilePanel("Build Asset Bundle", "Assets", "NewAssetBundle", "assetbundle");
        string directory = path.Substring(0, path.LastIndexOf('/'));
        string name = path.Substring(path.LastIndexOf('/') + 1);
        Object[] selection = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);
        AssetBundleBuild build = new AssetBundleBuild();
        build.assetBundleName = name;
        build.assetNames = new string[selection.Length];
        int len = selection.Length;
        for (int i = 0; i < len; i++)
        {
            build.assetNames[i] = AssetDatabase.GetAssetPath((UnityEngine.Object)selection[i]);
        }
        BuildPipeline.BuildAssetBundles(directory, new AssetBundleBuild[] { build }, BuildAssetBundleOptions.None, target);
    }

}
