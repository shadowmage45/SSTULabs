using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class ThrustCurveEditorGUI : MonoBehaviour
    {
        private static int graphWidth = 640;
        private static int graphHeight = 250;
        private static int scrollHeight = 480;
        private static int margin = 20;

        private static int presetWidth = 200;
        private static int presetHeight = scrollHeight + graphHeight;

        private static ThrustCurveEditorGUI activeGUI;

        private static int id;
        private static SSTUEngineThrustCurveGUI module;
        private static FloatCurve curve;
        private static Texture2D texture;        
        private static Rect windowRect = new Rect(Screen.width - 900, 40, graphWidth + margin, graphHeight + scrollHeight + margin);
        private static Vector2 scrollPos;
        private static List<FloatCurveEntry> curveData = new List<FloatCurveEntry>();
        private static ThrustCurvePreset[] presets;
        private static string presetName;

        private static Rect presetWindowRect = new Rect(Screen.width - 900 - presetWidth - margin, 40, presetWidth + margin, presetHeight + margin);
        private static bool presetWindowOpen = false;

        public static void openGUI(SSTUEngineThrustCurveGUI srbModule, string preset, FloatCurve inputCurve)
        {
            module = srbModule;
            id = module.GetInstanceID();
            MonoBehaviour.print("ThrustCurveEditor-input curve: " + curve + "\n" + SSTUUtils.printFloatCurve(curve));
            presetName = preset;
            setupCurveData(inputCurve);
            texture = new Texture2D(graphWidth, graphHeight);
            updateGraphTexture();
            loadPresets();
            if (activeGUI == null)
            {
                activeGUI = srbModule.gameObject.AddComponent<ThrustCurveEditorGUI>();
                SSTULog.debug("Created new gui object: " + activeGUI);
            }
        }

        private static void setupCurveData(FloatCurve curve)
        {
            curveData.Clear();
            int len = curve.Curve.length;
            FloatCurveEntry entry;            
            for (int i = 0; i < len; i++)
            {
                entry = new FloatCurveEntry(curve.Curve[i].time, curve.Curve[i].value, curve.Curve[i].inTangent, curve.Curve[i].outTangent);
                curveData.Add(entry);
            }
        }

        public static void closeGUI()
        {
            sortKeys(true);
            updateFloatCurve();
            module.thrustCurveGuiClosed(presetName, curve);
            //module.closeGui(curve, presetName);
            MonoBehaviour.Destroy(texture);
            curve = null;
            module = null;
            presets = null;
            curveData.Clear();
            MonoBehaviour.Destroy(activeGUI);
        }

        public static void updateGUI()
        {
            windowRect = GUI.Window(id, windowRect, updateGraphWindow, "SRB Thrust Curve Editor");
            if (presetWindowOpen)
            {
                presetWindowRect = GUI.Window(id + 1, presetWindowRect, updatePresetWindow, "Curve Presets");
            }
        }

        private static void updateGraphWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Box(texture);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Preset", GUILayout.Width(200)))
            {
                presetWindowOpen = true;
            }
            if (GUILayout.Button("Clear Data", GUILayout.Width(200)))
            {
                clearData();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Key", GUILayout.Width(100));
            GUILayout.Label("Value", GUILayout.Width(100));
            GUILayout.Label("In", GUILayout.Width(100));
            GUILayout.Label("Out", GUILayout.Width(100));
            GUILayout.EndHorizontal();
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            FloatCurveEntry data;
            int removeIndex = -1;
            FloatCurveEntry added = null;
            bool updateGraph = false;
            int len = curveData.Count;
            if (len == 0)
            {
                GUILayout.Label("No curve data available");
                if (GUILayout.Button("Add New Data Point"))
                {
                    added = new FloatCurveEntry(0, 0, 1, 1);
                }
            }
            for (int i = 0; i < len; i++)
            {
                GUILayout.BeginHorizontal();
                data = curveData[i];
                data.stringValues[0] = GUILayout.TextField(data.stringValues[0], GUILayout.Width(100));
                data.stringValues[1] = GUILayout.TextField(data.stringValues[1], GUILayout.Width(100));
                data.stringValues[3] = GUILayout.TextField(data.stringValues[3], GUILayout.Width(100));
                data.stringValues[2] = GUILayout.TextField(data.stringValues[2], GUILayout.Width(100));
                if (GUILayout.Button("Delete", GUILayout.Width(100)))
                {
                    removeIndex = i;
                }
                if (GUILayout.Button("Copy", GUILayout.Width(100)))
                {
                    added = new FloatCurveEntry(data.values.x, data.values.y, data.values.z, data.values.w);
                }
                if (data.updateValues()) { updateGraph = true; }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Sort Keys"))
            {
                sortKeys(false);
            }
            if (GUILayout.Button("Close GUI"))
            {
                closeGUI();
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
            if (removeIndex >= 0 && len > 1)//do not remove last key
            {
                curveData.RemoveAt(removeIndex);
                updateGraph = true;
            }
            if (added != null)
            {
                curveData.Add(added);
                updateGraph = true;
            }
            if (updateGraph)
            {
                presetName = "";
                updateGraphTexture();
            }
        }

        private static void updatePresetWindow(int id)
        {
            GUILayout.BeginVertical();
            int len = presets.Length;
            for (int i = 0; i < len; i++)
            {
                if (GUILayout.Button(presets[i].name))
                {
                    loadPresetCurve(presets[i]);
                }
            }
            if (GUILayout.Button("Close"))
            {
                presetWindowOpen = false;
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private static void loadPresetCurve(ThrustCurvePreset preset)
        {   
            setupCurveData(preset.curve);
            updateGraphTexture();
            presetName = preset.name;
        }
        
        private static void clearData()
        {
            curveData.Clear();
            updateGraphTexture();
        }

        private static void sortKeys(bool descend)
        {
            if (descend)//0->1
            {
                curveData.Sort(delegate (FloatCurveEntry a, FloatCurveEntry b) { return a.values.x > b.values.x ? 1 : a.values.x < b.values.x ? -1 : 0; });
            }
            else//1->0
            {
                curveData.Sort(delegate (FloatCurveEntry a, FloatCurveEntry b) { return a.values.x < b.values.x ? 1 : a.values.x > b.values.x ? -1 : 0; });
            }            
        }

        private static void updateFloatCurve()
        {
            curve = new FloatCurve();
            int len = curveData.Count;
            Vector4 val;
            for (int i = 0; i < len; i++)
            {
                val = curveData[i].values;
                curve.Add(val.x, val.y, val.z, val.w);
            }
        }

        private static void updateGraphTexture()
        {
            updateFloatCurve();
            //clear texture to black
            Color[] pixelColors = new Color[graphWidth * graphHeight];            
            for (int i = 0; i < pixelColors.Length; i++)
            {
                pixelColors[i] = Color.black;
            }
            texture.SetPixels(0, 0, graphWidth, graphHeight, pixelColors);

            //plot from the current float curve
            float fx, fy;
            int px, py;
            for (int i = 0; i < graphWidth; i++)
            {
                px = graphWidth - 1 - i;//invert x-axis so it goes from 1 -> 0
                fx = ((float)i / (float)graphWidth);
                fy = curve.Evaluate(fx);//should be a 0-1 value
                py = Mathf.RoundToInt((float)(graphHeight-1) * fy);//convert it to a pixel position relative to the size of the texture
                texture.SetPixel(px, py, Color.green);//update the pixel color
            }
            texture.Apply();
        }

        private static void loadPresets()
        {
            ConfigNode[] presetNodes = GameDatabase.Instance.GetConfigNodes("SSTU_THRUSTCURVE");
            ThrustCurvePreset preset;            
            int len = presetNodes.Length;
            presets = new ThrustCurvePreset[len];
            for (int i = 0; i < len; i++)
            {
                preset = new ThrustCurvePreset(presetNodes[i]);
                presets[i] = preset;
            }
        }

        public void OnGUI()
        {
            updateGUI();
        }

    }

    public class ThrustCurvePreset
    {
        public string name;
        public FloatCurve curve;
        public ThrustCurvePreset(ConfigNode node)
        {
            name = node.GetStringValue("name");
            curve = node.GetFloatCurve("curve");
        }
    }

    public class FloatCurveEntry
    {
        public Vector4 values;
        public string[] stringValues;

        public FloatCurveEntry(float key, float val, float inTan, float outTan)
        {
            values = new Vector4(key, val, inTan, outTan);
            stringValues = new string[] { values.x.ToString(), values.y.ToString(), values.z.ToString(), values.w.ToString() };
        }
        
        public bool updateValues()
        {
            float val;
            bool updated = false;
            if (float.TryParse(stringValues[0], out val))
            {
                if (val != values.x) { updated = true; }
                values.x = val;
            }
            if (float.TryParse(stringValues[1], out val))
            {
                if (val != values.y) { updated = true; }
                values.y = val;
            }
            if (float.TryParse(stringValues[2], out val))
            {
                if (val != values.z) { updated = true; }
                values.z = val;
            }
            if (float.TryParse(stringValues[3], out val))
            {
                if (val != values.w) { updated = true; }
                values.w = val;
            }
            return updated;
        }
    }
}
