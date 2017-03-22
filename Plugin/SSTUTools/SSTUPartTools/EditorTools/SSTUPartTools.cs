using UnityEngine;
using UnityEditor;

namespace SSTUPartTools
{
    [CustomEditor(typeof(SSTUPartExporter))]
    public class SSTUPartTools : Editor
    {

        private static string configFilePath = "/../SSTUPartTools.cfg";
        private static string tempPath = "C:/Users/John/Documents/PBRTest/Assets/TempGarbage";
        private static string gameDataPath = "/../GameData";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();            
            if (GUILayout.Button("Export Model"))
            {
                SSTUPartExporter exporter = (SSTUPartExporter)target;
                exporter.exportModel();
            }
            if (GUILayout.Button("Export Textures(WIP)"))
            {
                MonoBehaviour.print("Not Yet Implemented");
            }
        }
    }

}
