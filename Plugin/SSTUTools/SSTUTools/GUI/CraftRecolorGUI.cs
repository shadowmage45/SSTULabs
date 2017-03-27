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
            Component.DestroyImmediate(sectionGUI);
        }

    }

    public class SectionRecolorGUI : MonoBehaviour
    {
        private static int graphWidth = 640;
        private static int graphHeight = 250;
        private static int scrollHeight = 480;
        private static int margin = 20;
        private int id = 1;
        private Rect windowRect = new Rect(Screen.width - 900, 40, graphWidth + margin, graphHeight + scrollHeight + margin);
        private Vector2 scrollPos;

        internal SectionRecolorData sectionData;
        internal Action onCloseAction;

        public void OnGUI()
        {
            windowRect = GUI.Window(id, windowRect, drawWindow, "Section Recoloring");
        }

        private void drawWindow(int id)
        {
            GUILayout.BeginVertical();
            scrollPos = GUILayout.BeginScrollView(scrollPos);
           
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow();
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
                sectionData[i] = new SectionRecolorData(names[i], colors[i]);
            }
        }
    }

    public class SectionRecolorData
    {
        public readonly string sectionName;
        public Color color;
        public SectionRecolorData(string name, Color color)
        {
            this.sectionName = name;
            this.color = color;
        }
    }

}
