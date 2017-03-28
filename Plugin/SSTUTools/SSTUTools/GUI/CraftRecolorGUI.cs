using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class CraftRecolorGUI : MonoBehaviour
    {
        private static int graphWidth = 640;
        private static int graphHeight = 250;
        private static int scrollHeight = 480;
        private static int margin = 20;
        private static int id;
        private static Rect windowRect = new Rect(Screen.width - 900, 40, graphWidth + margin, graphHeight + scrollHeight + margin);
        private static Vector2 scrollPos;

        private List<ModuleRecolorData> moduleRecolorData = new List<ModuleRecolorData>();

        private SectionRecolorGUI sectionGUI;

        private bool open = false;

        internal Action guiCloseAction;

        public void Awake()
        {
            id = GetInstanceID();
        }

        internal void openGui()
        {
            //TODO populate module data for parts on editor craft
            List<Part> uniqueParts = new List<Part>();
            foreach (Part p in EditorLogic.fetch.ship.Parts)
            {
                if (p.symmetryCounterparts == null || p.symmetryCounterparts.Count == 0)
                {
                    uniqueParts.Add(p);
                }
                else
                {
                    bool found = false;
                    foreach (Part p1 in p.symmetryCounterparts)
                    {
                        if (uniqueParts.Contains(p1))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        uniqueParts.Add(p);
                    }
                }
            }
            foreach (Part p in uniqueParts)
            {
                List<IRecolorable> mods = p.FindModulesImplementing<IRecolorable>();
                foreach (IRecolorable mod in mods)
                {
                    ModuleRecolorData data = new ModuleRecolorData((PartModule)mod, mod);
                    moduleRecolorData.Add(data);
                }
            }
            open = true;
        }

        internal void closeGui()
        {
            open = false;
            closeSectionGUI();
            moduleRecolorData.Clear();
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
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            int len = moduleRecolorData.Count;
            Color old = GUI.contentColor;
            for (int i = 0; i < len; i++)
            {
                int len2 = moduleRecolorData[i].sectionData.Length;
                for (int k = 0; k < len2; k++)
                {
                    GUILayout.BeginHorizontal();
                    GUI.color = moduleRecolorData[i].sectionData[k].color;
                    if (GUILayout.Button("Recolor") && sectionGUI == null)
                    {
                        openSectionGUI(moduleRecolorData[i].sectionData[k]);
                    }
                    GUI.color = old;
                    GUILayout.Label(moduleRecolorData[i].module + "-" + moduleRecolorData[i].sectionData[k].sectionName);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close"))
            {
                open = false;
                guiCloseAction();//call the method in SSTULauncher to close this GUI
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void openSectionGUI(SectionRecolorData section)
        {
            sectionGUI = gameObject.AddComponent<SectionRecolorGUI>();
            sectionGUI.sectionData = section;
            sectionGUI.onCloseAction = closeSectionGUI;
        }

        private void closeSectionGUI()
        {
            Component.Destroy(sectionGUI);
        }

    }

    public class SectionRecolorGUI : MonoBehaviour
    {
        private static int windowWidth = 320;
        private static int windowHeight = 240;
        private int id = 1;
        private Rect windowRect = new Rect(Screen.width - 600, 40, windowWidth, windowHeight);

        //TODO load the preset colors from a config defined list of presets
        private Color[] presetColors;

        internal SectionRecolorData sectionData;
        internal Action onCloseAction;

        private float r, g, b, a;

        public void Awake()
        {
            id = GetInstanceID();

            ConfigNode node = GameDatabase.Instance.GetConfigNodes("SSTU_COLOR_PRESETS")[0];
            string[] colors = node.GetStringValues("color");
            int len = colors.Length;
            presetColors = new Color[len];
            for (int i = 0; i < len; i++)
            {
                presetColors[i] = colorFromString(colors[i]);
            }
        }

        public void OnGUI()
        {
            windowRect = GUI.Window(id, windowRect, drawWindow, "Section Recoloring");
        }

        private void drawWindow(int id)
        {
            GUILayout.BeginVertical();

            //red slider
            GUILayout.BeginHorizontal();
            GUILayout.Label("Red", GUILayout.Width(80));
            r = GUILayout.HorizontalSlider(sectionData.color.r, 0, 1, GUILayout.Width(100));
            if (r != sectionData.color.r)
            {
                sectionData.color.r = r;
                sectionData.updateColor();
            }
            GUILayout.TextField(sectionData.color.r.ToString(), GUILayout.Width(80));
            GUILayout.EndHorizontal();

            //green
            GUILayout.BeginHorizontal();
            GUILayout.Label("Green", GUILayout.Width(80));
            g = GUILayout.HorizontalSlider(sectionData.color.g, 0, 1, GUILayout.Width(100));
            if (g != sectionData.color.g)
            {
                sectionData.color.g = g;
                sectionData.updateColor();
            }
            GUILayout.TextField(sectionData.color.g.ToString(), GUILayout.Width(80));
            GUILayout.EndHorizontal();

            //blue
            GUILayout.BeginHorizontal();
            GUILayout.Label("Blue", GUILayout.Width(80));
            b = GUILayout.HorizontalSlider(sectionData.color.b, 0, 1, GUILayout.Width(100));
            if (b != sectionData.color.b)
            {
                sectionData.color.b = b;
                sectionData.updateColor();
            }
            GUILayout.TextField(sectionData.color.b.ToString(), GUILayout.Width(80));
            GUILayout.EndHorizontal();

            //alpha
            GUILayout.BeginHorizontal();
            GUILayout.Label("Alpha", GUILayout.Width(80));
            a = GUILayout.HorizontalSlider(sectionData.color.a, 0, 1, GUILayout.Width(100));
            if (a != sectionData.color.a)
            {
                sectionData.color.a = a;
                sectionData.updateColor();
            }
            GUILayout.TextField(sectionData.color.b.ToString(), GUILayout.Width(80));
            GUILayout.EndHorizontal();

            GUILayout.Label("Preset Colors", GUILayout.ExpandWidth(true));

            Color old = GUI.color;
            int len = presetColors.Length;
            int row = 0, column = 0;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < len; i++)
            {
                GUI.color = presetColors[i];
                if(GUILayout.Button("Preset", GUILayout.Width(50)))
                {
                    sectionData.color = presetColors[i];
                    sectionData.updateColor();
                }
                column++;
                if (column >= 4)
                {
                    column = 0;
                    row++;
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();
            GUI.color = old;

            bool shouldClose = false;
            if (GUILayout.Button("Close"))
            {
                shouldClose = true;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();

            if (shouldClose)
            {
                onCloseAction();
            }
        }

        private static Color colorFromBytes(int r, int g, int b, int a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        private static Color colorFromString(string val)
        {
            string[] split = val.Split(',');
            int r = int.Parse(split[0]);
            int g = int.Parse(split[1]);
            int b = int.Parse(split[2]);
            int a = int.Parse(split[3]);
            return colorFromBytes(r, g, b, a);
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
            Color[] colors = iModule.getSectionColors();
            int len = names.Length;
            sectionData = new SectionRecolorData[len];
            for (int i = 0; i < len; i++)
            {
                sectionData[i] = new SectionRecolorData(iModule, names[i], colors[i]);
            }
        }

        public void updateColors()
        {
            int len = sectionData.Length;
            for (int i = 0; i < len; i++)
            {
                iModule.setSectionColor(sectionData[i].sectionName, sectionData[i].color);
            }
        }
    }

    public class SectionRecolorData
    {
        public readonly IRecolorable owner;
        public readonly string sectionName;
        public Color color;

        public SectionRecolorData(IRecolorable owner, string name, Color color)
        {
            this.owner = owner;
            this.sectionName = name;
            this.color = color;
        }

        public void updateColor()
        {
            owner.setSectionColor(sectionName, color);
        }
    }

}
