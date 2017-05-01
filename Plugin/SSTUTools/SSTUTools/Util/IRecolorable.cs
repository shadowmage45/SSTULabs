using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public interface IRecolorable
    {
        string[] getSectionNames();
        Color[] getSectionColors(string name);
        void setSectionColors(string name, Color[] colors);
    }

    public class PresetColor
    {
        private static List<PresetColor> colorList = new List<PresetColor>();
        private static Dictionary<String, PresetColor> presetColors = new Dictionary<string, PresetColor>();

        public readonly string name;
        public readonly string title;
        public readonly Color color;

        public PresetColor(ConfigNode node)
        {
            name = node.GetStringValue("name");
            title = node.GetStringValue("title");
            color = node.getColorFromByteValues("color");
        }

        internal static void loadColors()
        {
            colorList.Clear();
            presetColors.Clear();
            PresetColor color;
            ConfigNode[] colorNodes = GameDatabase.Instance.GetConfigNodes("SSTU_COLOR_PRESET");
            int len = colorNodes.Length;
            for (int i = 0; i < len; i++)
            {
                color = new PresetColor(colorNodes[i]);
                colorList.Add(color);
                presetColors.Add(color.name, color);
            }
        }

        public static PresetColor getColor(string name)
        {
            if (!presetColors.ContainsKey(name)) { MonoBehaviour.print("ERROR: No Color data for name: " + name); }
            return presetColors[name];
        }
         
        public static List<PresetColor> getColorList() { return colorList; }

    }
}
