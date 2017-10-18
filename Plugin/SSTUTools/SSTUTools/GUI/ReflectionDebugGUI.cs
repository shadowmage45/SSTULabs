using System;
using UnityEngine;

namespace SSTUTools
{
    public class ReflectionDebugGUI : MonoBehaviour
    {
        private static Rect windowRect = new Rect(Screen.width - 900, 40, 800, 600);
        private int windowID = 0;

        public void Awake()
        {
            windowID = GetInstanceID();
        }

        public void OnGUI()
        {
            try
            {
                windowRect = GUI.Window(windowID, windowRect, updateWindow, "SSTUReflectionDebug");
            }
            catch (Exception e)
            {
                MonoBehaviour.print("Caught exception while rendering SSTUReflectionDebug GUI");
                MonoBehaviour.print(e.Message);
                MonoBehaviour.print(System.Environment.StackTrace);
            }
        }

        private void updateWindow(int id)
        {
            SSTUReflectionManager manager = SSTUReflectionManager.Instance;
            bool galaxy = manager.renderGalaxy;
            bool atmo = manager.renderAtmo;
            bool scaled = manager.renderScaled;
            bool scenery = manager.renderScenery;
            GUILayout.BeginVertical();
            manager.reflectionsEnabled = addButtonRow("Reflections Enabled", manager.reflectionsEnabled);
            manager.renderGalaxy = addButtonRow("Render Galaxy", galaxy);
            manager.renderAtmo = addButtonRow("Render Atmo", atmo);
            manager.renderScaled = addButtonRow("Render Scaled", scaled);
            manager.renderScenery = addButtonRow("Render Scenery", scenery);
            manager.eveInstalled = addButtonRow("Eve Fix", manager.eveInstalled);
            if (GUILayout.Button("Force Refl update"))
            {
                manager.renderCubes();
            }
            if (GUILayout.Button("Render Debug Cubes"))
            {
                manager.renderDebugCubes();
            }
            GUILayout.EndVertical();
        }

        private bool addButtonRow(string text, bool value)
        {
            GUILayout.BeginHorizontal();
            
            GUILayoutOption width = GUILayout.Width(100);
            GUILayout.Label(text, width);
            GUILayout.Label(value.ToString(), width);
            if (GUILayout.Button("Toggle", width))
            {
                value = !value;
            }
            GUILayout.EndHorizontal();
            return value;
        }

        private bool addButtonRow(string text)
        {
            GUILayout.BeginHorizontal();

            GUILayoutOption width = GUILayout.Width(100);
            GUILayout.Label(text, width);
            bool value = GUILayout.Button("Toggle", width);
            GUILayout.EndHorizontal();
            return value;
        }

    }
}
