using UnityEngine;
using UnityEditor;
using System.Collections;

public class AssetBundleCompiler
{
    [MenuItem ("Assets/Build AssetBundles OSX")]
    static void BuildAllAssetBundlesOSX ()
    {
        BuildPipeline.BuildAssetBundles ("Assets/AssetBundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneOSXUniversal);
    }
	
	[MenuItem ("Assets/Build AssetBundles Lin")]
    static void BuildAllAssetBundlesLin ()
    {
        BuildPipeline.BuildAssetBundles ("Assets/AssetBundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneLinux);
    }
	
	[MenuItem ("Assets/Build AssetBundles Win32")]
    static void BuildAllAssetBundlesWin32 ()
    {
        BuildPipeline.BuildAssetBundles ("Assets/AssetBundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
    }
	
	[MenuItem ("Assets/Build AssetBundles Win64")]
    static void BuildAllAssetBundlesWin64 ()
    {
        BuildPipeline.BuildAssetBundles ("Assets/AssetBundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
    }
}
