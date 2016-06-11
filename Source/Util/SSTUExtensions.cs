using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

namespace SSTUTools
{
    public static class SSTUExtensions
    {
        #region ConfigNode extension methods

        public static String[] GetStringValues(this ConfigNode node, String name)
        {
            String[] values = node.GetValues(name);
            return values == null ? new String[0] : values;
        }

        public static string GetStringValue(this ConfigNode node, String name, String defaultValue)
        {
            String value = node.GetValue(name);
            return value == null ? defaultValue : value;
        }

        public static string GetStringValue(this ConfigNode node, String name)
        {
            return GetStringValue(node, name, "");
        }

        public static bool[] GetBoolValues(this ConfigNode node, String name)
        {
            String[] values = node.GetValues(name);
            int len = values.Length;
            bool[] vals = new bool[len];            
            for (int i = 0; i < len; i++)
            {
                vals[i] = SSTUUtils.safeParseBool(values[i]);
            }
            return vals;
        }

        public static bool GetBoolValue(this ConfigNode node, String name, bool defaultValue)
        {
            String value = node.GetValue(name);
            if (value == null) { return defaultValue; }
            try
            {
                return bool.Parse(value);
            }
            catch (Exception e)
            {
                MonoBehaviour.print(e.Message);
            }
            return defaultValue;
        }

        public static bool GetBoolValue(this ConfigNode node, String name)
        {
            return GetBoolValue(node, name, false);
        }

        public static float[] GetFloatValues(this ConfigNode node, String name, float[] defaults)
        {
            String baseVal = node.GetStringValue(name);
            if (!String.IsNullOrEmpty(baseVal))
            {
                String[] split = baseVal.Split(new char[] { ',' });
                float[] vals = new float[split.Length];
                for (int i = 0; i < split.Length; i++) { vals[i] = SSTUUtils.safeParseFloat(split[i]); }
                return vals;
            }
            return defaults;
        }

        public static float[] GetFloatValues(this ConfigNode node, String name)
        {
            return GetFloatValues(node, name, new float[] { });
        }

        public static float[] GetFloatValuesCSV(this ConfigNode node, String name)
        {
            return GetFloatValuesCSV(node, name, new float[] { });
        }

        public static float[] GetFloatValuesCSV(this ConfigNode node, String name, float[] defaults)
        {
            float[] values = defaults;
            if (node.HasValue(name))
            {
                string strVal = node.GetStringValue(name);
                string[] splits = strVal.Split(',');
                values = new float[splits.Length];
                for (int i = 0; i < splits.Length; i++)
                {
                    values[i] = float.Parse(splits[i]);
                }
            }
            return values;
        }

        public static float GetFloatValue(this ConfigNode node, String name, float defaultValue)
        {
            String value = node.GetValue(name);
            if (value == null) { return defaultValue; }
            try
            {
                return float.Parse(value);
            }
            catch (Exception e)
            {
                MonoBehaviour.print(e.Message);
            }
            return defaultValue;
        }

        public static float GetFloatValue(this ConfigNode node, String name)
        {
            return GetFloatValue(node, name, 0);
        }

        public static double GetDoubleValue(this ConfigNode node, String name, double defaultValue)
        {
            String value = node.GetValue(name);
            if (value == null) { return defaultValue; }
            try
            {
                return double.Parse(value);
            }
            catch (Exception e)
            {
                MonoBehaviour.print(e.Message);
            }
            return defaultValue;
        }

        public static double GetDoubleValue(this ConfigNode node, String name)
        {
            return GetDoubleValue(node, name, 0);
        }

        public static int GetIntValue(this ConfigNode node, String name, int defaultValue)
        {
            String value = node.GetValue(name);
            if (value == null) { return defaultValue; }
            try
            {
                return int.Parse(value);
            }
            catch (Exception e)
            {
                MonoBehaviour.print(e.Message);
            }
            return defaultValue;
        }

        public static int GetIntValue(this ConfigNode node, String name)
        {
            return GetIntValue(node, name, 0);
        }
        
        public static Vector3 GetVector3(this ConfigNode node, String name, Vector3 defaultValue)
        {
            String value = node.GetValue(name);
            if (value == null)
            {
                return defaultValue;
            }
            String[] vals = value.Split(',');
            if (vals.Length < 3)
            {
                MonoBehaviour.print("ERROR parsing values for Vector3 from input: " + value + ". found less than 3 values, cannot create Vector3");
                return defaultValue;
            }
            return new Vector3((float)SSTUUtils.safeParseDouble(vals[0]), (float)SSTUUtils.safeParseDouble(vals[1]), (float)SSTUUtils.safeParseDouble(vals[2]));
        }

        public static Vector3 GetVector3(this ConfigNode node, String name)
        {
            String value = node.GetValue(name);
            if (value == null)
            {
                MonoBehaviour.print("ERROR: No value for name: " + name + " found in config node: " + node);
                return Vector3.zero;
            }
            String[] vals = value.Split(',');
            if (vals.Length < 3)
            {
                MonoBehaviour.print("ERROR parsing values for Vector3 from input: " + value + ". found less than 3 values, cannot create Vector3");
                return Vector3.zero;
            }
            return new Vector3((float)SSTUUtils.safeParseDouble(vals[0]), (float)SSTUUtils.safeParseDouble(vals[1]), (float)SSTUUtils.safeParseDouble(vals[2]));
        }

        public static FloatCurve GetFloatCurve(this ConfigNode node, String name, FloatCurve defaultValue = null)
        {
            FloatCurve curve = new FloatCurve();
            if (node.HasNode(name))
            {
                ConfigNode curveNode = node.GetNode(name);
                String[] values = curveNode.GetValues("key");
                int len = values.Length;
                String[] splitValue;
                float a, b, c, d;
                for (int i = 0; i < len; i++)
                {
                    splitValue = Regex.Replace(values[i], @"\s+", " ").Split(' ');
                    if (splitValue.Length > 2)
                    {
                        a = SSTUUtils.safeParseFloat(splitValue[0]);
                        b = SSTUUtils.safeParseFloat(splitValue[1]);
                        c = SSTUUtils.safeParseFloat(splitValue[2]);
                        d = SSTUUtils.safeParseFloat(splitValue[3]);
                        curve.Add(a, b, c, d);
                    }
                    else
                    {
                        a = SSTUUtils.safeParseFloat(splitValue[0]);
                        b = SSTUUtils.safeParseFloat(splitValue[1]);
                        curve.Add(a, b);
                    }
                }
            }
            else if (defaultValue != null)
            {
                foreach (Keyframe f in defaultValue.Curve.keys)
                {
                    curve.Add(f.time, f.value, f.inTangent, f.outTangent);
                }
            }
            else
            {
                curve.Add(0, 0);
                curve.Add(1, 1);
            }
            return curve;
        }

        #endregion

        #region Transform extensionMethods

        public static Transform[] FindModels(this Transform transform, String modelName)
        {
            Transform[] trs = transform.FindChildren(modelName);
            Transform[] trs2 = transform.FindChildren(modelName + "(Clone)");
            Transform[] trs3 = new Transform[trs.Length + trs2.Length];
            int index = 0;
            for (int i = 0; i < trs.Length; i++, index++)
            {
                trs3[index] = trs[i];
            }
            for (int i = 0; i < trs2.Length; i++, index++)
            {
                trs3[index] = trs2[i];
            }
            return trs3;
        }

        public static Transform FindModel(this Transform transform, String modelName)
        {
            Transform tr = transform.FindRecursive(modelName);
            if (tr != null) { return tr; }
            return transform.FindRecursive(modelName + "(Clone)");
        }

        public static Transform[] FindChildren(this Transform transform, String name)
        {
            List<Transform> trs = new List<Transform>();
            if (transform.name == name) { trs.Add(transform); }
            locateTransformsRecursive(transform, name, trs);
            return trs.ToArray();
        }

        private static void locateTransformsRecursive(Transform tr, String name, List<Transform> output)
        {
            foreach (Transform child in tr)
            {
                if (child.name == name) { output.Add(child); }
                locateTransformsRecursive(child, name, output);
            }
        }

        public static Transform FindRecursive(this Transform transform, String name)
        {
            if (transform.name == name) { return transform; }//was the original input transform
            Transform tr = transform.Find(name);//found as a direct child
            if (tr != null) { return tr; }
            foreach(Transform child in transform)
            {
                tr = child.FindRecursive(name);
                if (tr != null) { return tr; }
            }
            return null;
        }

        public static Transform FindOrCreate(this Transform transform, String name)
        {
            Transform newTr = transform.FindRecursive(name);
            if (newTr != null)
            {
                //MonoBehaviour.print("found existing go for: " + name);
                return newTr;
            }
            //MonoBehaviour.print("creating new game object for: " + name);
            //SSTUUtils.recursePrintComponents(transform.gameObject, "");
            GameObject newGO = new GameObject(name);
            newGO.SetActive(true);
            newGO.name = newGO.transform.name = name;
            newGO.transform.NestToParent(transform);
            return newGO.transform;
        }

        #endregion

        #region Vector3 extensionMethods

        public static Vector3 CopyVector(this Vector3 input)
        {
            return new Vector3(input.x, input.y, input.z);
        }

        #endregion

        #region PartModule extensionMethods

        public static void updateUIFloatEditControl(this PartModule module, string fieldName, float min, float max, float incLarge, float incSmall, float incSlide, bool forceUpdate, float forceVal)
        {
            UI_FloatEdit widget = null;
            if (HighLogic.LoadedSceneIsEditor)
            {
                widget = (UI_FloatEdit)module.Fields[fieldName].uiControlEditor;
            }
            else if (HighLogic.LoadedSceneIsFlight)
            {
                widget = (UI_FloatEdit)module.Fields[fieldName].uiControlFlight;
            }
            else
            {
                return;
            }
            if (widget == null)
            {
                return;
            }
            widget.minValue = min;
            widget.maxValue = max;
            widget.incrementLarge = incLarge;
            widget.incrementSmall = incSmall;
            widget.incrementSlide = incSlide;
            if (forceUpdate && widget.partActionItem!=null)
            {
                UIPartActionFloatEdit ctr = (UIPartActionFloatEdit)widget.partActionItem;
                var t = widget.onFieldChanged;//temporarily remove the callback
                widget.onFieldChanged = null;
                ctr.incSmall.onToggle.RemoveAllListeners();
                ctr.incLarge.onToggle.RemoveAllListeners();
                ctr.decSmall.onToggle.RemoveAllListeners();
                ctr.decLarge.onToggle.RemoveAllListeners();
                ctr.slider.onValueChanged.RemoveAllListeners();
                ctr.Setup(ctr.Window, module.part, module, HighLogic.LoadedSceneIsEditor ? UI_Scene.Editor : UI_Scene.Flight, widget, module.Fields[fieldName]);
                widget.onFieldChanged = t;//re-seat callback
            }
        }

        public static void updateUIFloatEditControl(this PartModule module, string fieldName, float newValue)
        {
            UI_FloatEdit widget = null;
            if (HighLogic.LoadedSceneIsEditor)
            {
                widget = (UI_FloatEdit)module.Fields[fieldName].uiControlEditor;
            }
            else if (HighLogic.LoadedSceneIsFlight)
            {
                widget = (UI_FloatEdit)module.Fields[fieldName].uiControlFlight;
            }
            else
            {
                return;
            }
            if (widget == null)
            {
                return;
            }
            BaseField field = module.Fields[fieldName];
            field.SetValue(newValue, field.host);
            if (widget.partActionItem != null)//force widget re-setup for changed values; this will update the GUI value and slider positions/internal cached data
            {
                UIPartActionFloatEdit ctr = (UIPartActionFloatEdit)widget.partActionItem;
                var t = widget.onFieldChanged;//temporarily remove the callback; we don't need an event fired when -we- are the ones editing the value...            
                widget.onFieldChanged = null;
                ctr.incSmall.onToggle.RemoveAllListeners();
                ctr.incLarge.onToggle.RemoveAllListeners();
                ctr.decSmall.onToggle.RemoveAllListeners();
                ctr.decLarge.onToggle.RemoveAllListeners();
                ctr.slider.onValueChanged.RemoveAllListeners();
                ctr.Setup(ctr.Window, module.part, module, HighLogic.LoadedSceneIsEditor ? UI_Scene.Editor : UI_Scene.Flight, widget, module.Fields[fieldName]);
                widget.onFieldChanged = t;//re-seat callback
            }
        }

        public static void updateUIChooseOptionControl(this PartModule module, string fieldName, string[] options, string[] display, bool forceUpdate, string forceVal="")
        {
            if (display.Length == 0 && options.Length > 0) { display = new string[] { "NONE" }; }
            if (options.Length == 0) { options = new string[] { "NONE" }; }
            UI_ChooseOption widget = null;
            if (HighLogic.LoadedSceneIsEditor)
            {
                widget = (UI_ChooseOption)module.Fields[fieldName].uiControlEditor;
            }
            else if (HighLogic.LoadedSceneIsFlight)
            {
                widget = (UI_ChooseOption)module.Fields[fieldName].uiControlFlight;
            }
            else { return; }
            if (widget == null) { return; }
            widget.display = display;
            widget.options = options;
            if (forceUpdate && widget.partActionItem != null)
            {
                UIPartActionChooseOption control = (UIPartActionChooseOption)widget.partActionItem;
                var t = widget.onFieldChanged;
                widget.onFieldChanged = null;
                int index = Array.IndexOf(options, forceVal);
                control.slider.minValue = 0;
                control.slider.maxValue = options.Length - 1;
                control.slider.value = index;
                control.OnValueChanged(0);
                widget.onFieldChanged = t;
            }
        }
        
        public static void updateUIScaleEditControl(this PartModule module, string fieldName, float[] intervals, float[] increments, bool forceUpdate, float forceValue=0)
        {
            UI_ScaleEdit widget = null;
            if (HighLogic.LoadedSceneIsEditor)
            {
                widget = (UI_ScaleEdit)module.Fields[fieldName].uiControlEditor;
            }
            else if (HighLogic.LoadedSceneIsFlight)
            {
                widget = (UI_ScaleEdit)module.Fields[fieldName].uiControlFlight;
            }
            else
            {
                return;
            }
            if (widget == null)
            {
                return;
            }
            widget.intervals = intervals;
            widget.incrementSlide = increments;
            if (forceUpdate && widget.partActionItem != null)
            {
                UIPartActionScaleEdit ctr = (UIPartActionScaleEdit)widget.partActionItem;
                var t = widget.onFieldChanged;
                widget.onFieldChanged = null;
                ctr.inc.onToggle.RemoveAllListeners();
                ctr.dec.onToggle.RemoveAllListeners();
                ctr.slider.onValueChanged.RemoveAllListeners();
                ctr.Setup(ctr.Window, module.part, module, HighLogic.LoadedSceneIsEditor ? UI_Scene.Editor : UI_Scene.Flight, widget, module.Fields[fieldName]);
                widget.onFieldChanged = t;
            }
        }

        public static void updateUIScaleEditControl(this PartModule module, string fieldName, float min, float max, float increment, bool flight, bool editor, bool forceUpdate, float forceValue = 0)
        {
            BaseField field = module.Fields[fieldName];
            if (increment <= 0)//div/0 error
            {
                field.guiActive = false;
                field.guiActiveEditor = false;
                return;
            }
            float seg = (max - min) / increment;
            int numOfIntervals = (int)Math.Round(seg) + 1;
            float sliderInterval = increment * 0.05f;
            float[] intervals = new float[numOfIntervals];
            float[] increments = new float[numOfIntervals];
            UI_Scene scene = HighLogic.LoadedSceneIsFlight ? UI_Scene.Flight : UI_Scene.Editor;
            if (numOfIntervals <= 1)//not enough data...
            {
                field.guiActive = false;
                field.guiActiveEditor = false;
                MonoBehaviour.print("ERROR: Not enough data to create intervals: " + min + " : " + max + " :: " + increment); 
            }
            else
            {
                field.guiActive = flight;
                field.guiActiveEditor = editor;
                intervals = new float[numOfIntervals];
                increments = new float[numOfIntervals];
                for (int i = 0; i < numOfIntervals; i++)
                {
                    intervals[i] = min + (increment * (float)i);
                    increments[i] = sliderInterval;
                }
                module.updateUIScaleEditControl(fieldName, intervals, increments, forceUpdate, forceValue);
            }
        }

        public static void updateUIScaleEditControl(this PartModule module, string fieldName, float value)
        {
            UI_ScaleEdit widget = null;
            if (HighLogic.LoadedSceneIsEditor)
            {
                widget = (UI_ScaleEdit)module.Fields[fieldName].uiControlEditor;
            }
            else if (HighLogic.LoadedSceneIsFlight)
            {
                widget = (UI_ScaleEdit)module.Fields[fieldName].uiControlFlight;
            }
            else
            {
                return;
            }
            if (widget == null || widget.partActionItem==null)
            {
                return;
            }
            UIPartActionScaleEdit ctr = (UIPartActionScaleEdit)widget.partActionItem;
            var t = widget.onFieldChanged;
            widget.onFieldChanged = null;
            ctr.inc.onToggle.RemoveAllListeners();
            ctr.dec.onToggle.RemoveAllListeners();
            ctr.slider.onValueChanged.RemoveAllListeners();
            ctr.Setup(ctr.Window, module.part, module, HighLogic.LoadedSceneIsEditor ? UI_Scene.Editor : UI_Scene.Flight, widget, module.Fields[fieldName]);
            widget.onFieldChanged = t;
        }

        #endregion

        public static String Print(this FloatCurve curve)
        {
            String output = "";
            foreach (Keyframe f in curve.Curve.keys)
            {
                output = output + "\n" + f.time + " " + f.value + " " + f.inTangent + " " + f.outTangent;
            }
            return output;
        }

        public static void logDebug(this MonoBehaviour module, String message)
        {
            MonoBehaviour.print("SSTU-DEBUG: " + message);
        }

        public static void logError(this MonoBehaviour module, String message)
        {
            MonoBehaviour.print("SSTU-ERROR: " + message);
        }
    }
}

