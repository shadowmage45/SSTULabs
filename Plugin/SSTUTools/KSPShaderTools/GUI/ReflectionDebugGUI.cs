using System;
using UnityEngine;

namespace KSPShaderTools
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
            ReflectionManager manager = ReflectionManager.Instance;
            bool galaxy = manager.renderGalaxy;
            bool atmo = manager.renderAtmo;
            bool scaled = manager.renderScaled;
            bool scenery = manager.renderScenery;
            GUILayout.BeginVertical();
            manager.reflectionsEnabled = addButtonRowToggle("Reflections Enabled", manager.reflectionsEnabled);
            manager.renderGalaxy = addButtonRowToggle("Render Galaxy", galaxy);
            manager.renderAtmo = addButtonRowToggle("Render Atmo", atmo);
            manager.renderScaled = addButtonRowToggle("Render Scaled", scaled);
            manager.renderScenery = addButtonRowToggle("Render Scenery", scenery);
            manager.eveInstalled = addButtonRowToggle("Eve Fix", manager.eveInstalled);

            int len = manager.renderStack.Count;
            ReflectionManager.ReflectionPass pass;
            int incIndex = -1;
            int decIndex = -1;
            for (int i = 0; i < len; i++)
            {
                pass = manager.renderStack[i];
                bool minus = addButtonRow(pass.ToString(), "-");
                if (minus) { decIndex = i; }
                bool plus = addButtonRow(pass.ToString(), "+");
                if (plus) { incIndex = i; }
            }
            if (incIndex > 0)
            {
                pass = manager.renderStack[incIndex];
                manager.renderStack.RemoveAt(incIndex);
                manager.renderStack.Insert(incIndex - 1, pass);
            }
            if (decIndex >= 0 && decIndex < len-1)
            {
                pass = manager.renderStack[decIndex];
                manager.renderStack.RemoveAt(decIndex);
                manager.renderStack.Insert(decIndex + 1, pass);
            }

            if (GUILayout.Button("Force Reflection Probe Update"))
            {
                manager.updateReflections(true);
            }
            if (GUILayout.Button("Export Debug Cube Maps"))
            {
                manager.renderDebugCubes();
            }
            if (GUILayout.Button("Export Debug Cube Layer"))
            {
                manager.renderDebugLayers();
            }
            GUILayout.EndVertical();
        }

        private bool addButtonRowToggle(string text, bool value)
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

        private bool addButtonRow(string labelText, string buttonText)
        {
            GUILayout.BeginHorizontal();
            GUILayoutOption width = GUILayout.Width(100);
            GUILayout.Label(labelText, width);
            bool value = GUILayout.Button(buttonText, width);
            GUILayout.EndHorizontal();
            return value;
        }

    }
}
