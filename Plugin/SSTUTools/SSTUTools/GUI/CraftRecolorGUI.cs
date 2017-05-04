using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class CraftRecolorGUI : MonoBehaviour
    {
        private static int graphWidth = 500;
        private static int graphHeight = 800;
        private static int margin = 20;
        private static int id;
        private static Rect windowRect = new Rect(Screen.width - 600, 40, graphWidth + margin, graphHeight + margin);
        private static Vector2 scrollPos;
        private static Vector2 presetColorScrollPos;

        private List<ModuleRecolorData> moduleRecolorData = new List<ModuleRecolorData>();
        
        private bool open = false;

        internal Action guiCloseAction;

        private SectionRecolorData sectionData;
        private int colorIndex;
        private string rStr, gStr, bStr, aStr;//string caches of color values//TODO -- set initial state when a section color is selected
        private static Color editingColor;
        private static Color[] storedPattern;
        private static Color storedColor;

        public void Awake()
        {
            id = GetInstanceID();
        }

        internal void openGUIPart(EditorLogic editor, Part part)
        {
            editor.Lock(true, true, true, "SSTURecolorGUILock");
            List<IRecolorable> mods = part.FindModulesImplementing<IRecolorable>();
            foreach (IRecolorable mod in mods)
            {
                ModuleRecolorData data = new ModuleRecolorData((PartModule)mod, mod);
                moduleRecolorData.Add(data);
            }
            open = true;
        }

        internal void closeGui()
        {
            open = false;
            closeSectionGUI();
            moduleRecolorData.Clear();
            sectionData = null;
            EditorLogic editor = EditorLogic.fetch;
            if (editor != null) { editor.Unlock("SSTURecolorGUILock"); }
        }

        public void OnGUI()
        {
            if (open)
            {
                windowRect = GUI.Window(id, windowRect, drawWindow, "Part Recoloring");
            }
        }

        private void drawWindow(int id)
        {
            GUILayout.BeginVertical();
            drawSectionSelectionArea();
            drawSectionRecoloringArea();
            drawPresetColorArea();
            if (GUILayout.Button("Close"))
            {
                open = false;
                guiCloseAction();//call the method in SSTULauncher to close this GUI
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void setupSectionData(SectionRecolorData section, int colorIndex)
        {
            this.sectionData = section;
            this.colorIndex = colorIndex;
            editingColor = sectionData.colors[colorIndex];
            rStr = (editingColor.r * 255f).ToString("F0");
            gStr = (editingColor.g * 255f).ToString("F0");
            bStr = (editingColor.b * 255f).ToString("F0");
            aStr = (editingColor.a * 255f).ToString("F0");
        }

        private void closeSectionGUI()
        {
            sectionData = null;
            editingColor = Color.white;
            rStr = gStr = bStr = aStr = "255";
            colorIndex = 0;
        }

        private void drawSectionSelectionArea()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Main", GUILayout.Width(70));
            GUILayout.Label("Second", GUILayout.Width(70));
            GUILayout.Label("Detail", GUILayout.Width(70));
            GUILayout.EndHorizontal();
            //TODO find the height of 9 lines; no current part should have more than 9 recolorable sections (MUS = 7 in split tank + 2 fairings)
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            int len = moduleRecolorData.Count;
            Color old = GUI.contentColor;
            Color guiColor = old;
            for (int i = 0; i < len; i++)
            {
                int len2 = moduleRecolorData[i].sectionData.Length;
                for (int k = 0; k < len2; k++)
                {
                    GUILayout.BeginHorizontal();
                    for (int m = 0; m < 3; m++)
                    {
                        guiColor = moduleRecolorData[i].sectionData[k].colors[m];
                        guiColor.a = 1;
                        GUI.color = guiColor;
                        if (GUILayout.Button("Recolor", GUILayout.Width(70)))
                        {
                            setupSectionData(moduleRecolorData[i].sectionData[k], m);
                        }
                    }
                    if (sectionData == moduleRecolorData[i].sectionData[k])
                    {
                        GUI.color = Color.red;
                    }
                    else
                    {
                        GUI.color = old;
                    }
                    GUILayout.Label(moduleRecolorData[i].module.part.name + "-" + moduleRecolorData[i].sectionData[k].sectionName);
                    GUILayout.EndHorizontal();
                    GUI.color = old;
                }
            }
            GUILayout.EndScrollView();
        }

        private void drawSectionRecoloringArea()
        {            
            if (sectionData == null)
            {
                return;
            }
            bool updated = false;
            Color color = editingColor;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Editing: ", GUILayout.Width(60));
            GUILayout.Label(moduleRecolorData[0].module.moduleName, GUILayout.Width(200));
            GUILayout.Label(sectionData.sectionName, GUILayout.Width(80));
            GUILayout.Label(getSectionLabel(colorIndex) + " Color", GUILayout.Width(140));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (drawColorInputLine("Red", ref color.r, ref rStr)) { updated = true; }
            if (GUILayout.Button("Load Pattern", GUILayout.Width(120)))
            {
                sectionData.colors[0] = storedPattern[0];
                sectionData.colors[1] = storedPattern[1];
                sectionData.colors[2] = storedPattern[2];
                updated = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (drawColorInputLine("Green", ref color.g, ref gStr)) { updated = true; }
            if (GUILayout.Button("Store Pattern", GUILayout.Width(120)))
            {
                storedPattern = new Color[3];
                storedPattern[0] = sectionData.colors[0];
                storedPattern[1] = sectionData.colors[1];
                storedPattern[2] = sectionData.colors[2];
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (drawColorInputLine("Blue", ref color.b, ref bStr)) { updated = true; }
            if (GUILayout.Button("Load Color", GUILayout.Width(120)))
            {
                color = storedColor;
                updated = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (drawColorInputLine("Specular", ref color.a, ref aStr)) { updated = true; }
            if (GUILayout.Button("Store Color", GUILayout.Width(120)))
            {
                storedColor = color;
            }
            GUILayout.EndHorizontal();

            if (updated)
            {
                editingColor = color;
                sectionData.updateColors();
            }
        }

        private void drawPresetColorArea()
        {
            if (sectionData == null)
            {
                return;
            }
            GUILayout.Label("Select a preset color: ");
            presetColorScrollPos = GUILayout.BeginScrollView(presetColorScrollPos);
            bool update = false;
            Color old = GUI.color;
            Color guiColor = old;
            List<PresetColor> presetColors = PresetColor.getColorList();
            int len = presetColors.Count;
            for (int i = 0; i < len; i++)
            {
                GUILayout.BeginHorizontal();
                guiColor = presetColors[i].color;
                guiColor.a = 1f;
                GUI.color = guiColor;
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    editingColor = presetColors[i].color;
                    rStr = (editingColor.r * 255f).ToString("F0");
                    gStr = (editingColor.g * 255f).ToString("F0");
                    bStr = (editingColor.b * 255f).ToString("F0");
                    aStr = (editingColor.a * 255f).ToString("F0");
                    update = true;
                }
                GUI.color = old;
                GUILayout.Label(presetColors[i].title);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUI.color = old;
            sectionData.colors[colorIndex] = editingColor;
            if (update)
            {
                sectionData.updateColors();
            }
        }

        private bool drawColorInputLine(string label, ref float val, ref string sVal)
        {
            //TODO -- text input validation for numbers only -- http://answers.unity3d.com/questions/18736/restrict-characters-in-guitextfield.html
            // also -- https://forum.unity3d.com/threads/text-field-for-numbers-only.106418/
            GUILayout.Label(label, GUILayout.Width(80));
            bool updated = false;
            float result = val;
            result = GUILayout.HorizontalSlider(val, 0, 1, GUILayout.Width(120));
            if (result != val)
            {
                val = result;
                sVal = (val * 255f).ToString("F0");
                updated = true;
            }
            string textOutput = GUILayout.TextField(sVal, 3, GUILayout.Width(60));
            if (sVal != textOutput)
            {
                sVal = textOutput;
                int iVal;
                if (int.TryParse(textOutput, out iVal))
                {
                    val = iVal / 255f;
                    updated = true;
                }
            }
            return updated;
        }

        private string getSectionLabel(int index)
        {
            switch (index)
            {
                case 0:
                    return "Main";
                case 1:
                    return "Secondary";
                case 2:
                    return "Detail";
                default:
                    return "Unknown";
            }
        }

    }

    public class ModuleRecolorData
    {
        public PartModule module;//must implement IRecolorable
        public IRecolorable iModule;//interface version of module
        public SectionRecolorData[] sectionData;

        public ModuleRecolorData(PartModule module, IRecolorable iModule)
        {
            this.module = module;
            this.iModule = iModule;
            string[] names = iModule.getSectionNames();
            int len = names.Length;
            sectionData = new SectionRecolorData[len];
            for (int i = 0; i < len; i++)
            {
                sectionData[i] = new SectionRecolorData(iModule, names[i], iModule.getSectionColors(names[i]));
            }
        }
    }

    public class SectionRecolorData
    {
        public readonly IRecolorable owner;
        public readonly string sectionName;
        public Color[] colors = new Color[3];

        public SectionRecolorData(IRecolorable owner, string name, Color[] colors)
        {
            this.owner = owner;
            this.sectionName = name;
            this.colors = colors;
        }

        public void updateColors()
        {
            owner.setSectionColors(sectionName, colors);
        }
    }

}
