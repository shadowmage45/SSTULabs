using UnityEngine;
using UnityEditor;

namespace SSTUPartTools 
{
    public class SSTUPartToolsConfiguration : EditorWindow
    {
        public static string exportPathRoot = "Undefined";

        [MenuItem("SSTU/Part Export Configuration")]
        public static void showWindow()
        {
            EditorWindow.GetWindow(typeof(SSTUPartToolsConfiguration));
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            if (GUILayout.Button("Select Folder"))
            {
                exportPathRoot = EditorUtility.OpenFolderPanel("Select Export Root", Application.dataPath, "");
                EditorPrefs.SetString("SSTUExportPath", exportPathRoot);
            }
            GUILayout.Label("Selected:  "+exportPathRoot);
            EditorGUI.EndChangeCheck();
        }

        public void OnEnable()
        {            
            exportPathRoot = EditorPrefs.GetString("SSTUExportPath", exportPathRoot);
        }

        public void OnDisable()
        {
            EditorPrefs.SetString("SSTUExportPath", exportPathRoot);
        }
    }
}
