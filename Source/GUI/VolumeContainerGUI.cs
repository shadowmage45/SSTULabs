using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class VolumeContainerGUI
    {
        private static Vector2 scrollPos;
        private static Rect windowRect;
        private static int id = 10000;
        private static int containerIndex = 0;
        private static SSTUVolumeContainer module;
        private static ContainerDefinition[] containers;
        private static VolumeRatioEntry[][] resourceEntries;

        private static Rect statWindowRect;
        private static int statId;
        
        public static void openGUI(SSTUVolumeContainer container, ContainerDefinition[] modContainers)
        {            
            if (module != null)
            {
                closeGUI();
            }
            windowRect = new Rect(Screen.width - 900, 40, 800, 600);
            statWindowRect = new Rect(Screen.width - 900 - 200, 40, 200, 600);
            module = container;
            containers = modContainers;
            id = module.GetInstanceID();
            statId = id + 1;
            int len = modContainers.Length;
            resourceEntries = new VolumeRatioEntry[len][];
            for (int i = 0; i < len; i++)
            {
                int len2 = modContainers[i].tankData.Length;
                resourceEntries[i] = new VolumeRatioEntry[len2];
                for (int k = 0; k < len2; k++)
                {
                    resourceEntries[i][k] = new VolumeRatioEntry(modContainers[i].tankData[k]);
                }
            }
        }

        public static void closeGUI()
        {
            module.closeGUI();
            module = null;
            containers = null;
            resourceEntries = null;
        }

        public static void updateGUI()
        {
            windowRect = GUI.Window(id, windowRect, addContainerWindow, "SSTUVolumeContainer");
            statWindowRect = GUI.Window(statId, statWindowRect, addStatWindow, "Stats");
        }

        private static void addStatWindow(int id)
        {
            float m = module.modifiedMass;
            float tm = module.part.mass - m;
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Dry Mass:", GUILayout.Width(100));
            GUILayout.Label(m.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Prop Mass:", GUILayout.Width(100));
            GUILayout.Label(tm.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Dry Cost:", GUILayout.Width(100));
            GUILayout.Label(module.modifiedCost.ToString());
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private static void addContainerWindow(int id)
        {
            GUILayout.BeginVertical();
            string mainLabel = "Current: " + containers[containerIndex].name + " :: " + containers[containerIndex].currentUsableVolume + " / " + containers[containerIndex].currentRawVolume + "l";
            if (containers.Length > 0)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Prev Container", GUILayout.Width(200)) && containerIndex > 0) { containerIndex--; }
                GUILayout.Label(mainLabel);
                if (GUILayout.Button("Next Container", GUILayout.Width(200)) && containerIndex < containers.Length - 1) { containerIndex++; }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label(mainLabel);
            }
            addWindowContainerTypeControls(containers[containerIndex]);
            addWindowFuelTypeControls(containers[containerIndex]);
            addWindowContainerRatioControls(containers[containerIndex]);
            if (GUILayout.Button("Close"))
            {
                closeGUI();
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private static void addWindowContainerTypeControls(ContainerDefinition container)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Select a container type:");
            GUILayout.Label("Current Type: " + container.currentModifier);
            GUILayout.EndHorizontal();
            ContainerModifier[] mods = container.modifiers;
            ContainerModifier mod;
            int len = mods.Length;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < len; i++)
            {
                mod = mods[i];
                if (i > 0 && i % 4 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                if (GUILayout.Button(mod.title, GUILayout.Width(175)))
                {
                    module.containerTypeUpdated(container, mod);
                }
            }
            GUILayout.EndHorizontal();
        }

        private static void addWindowFuelTypeControls(ContainerDefinition container)
        {
            GUILayout.Label("Select an optional pre-filled fuel type:");
            ContainerFuelPreset[] presets = container.fuelPresets;
            ContainerFuelPreset preset;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < presets.Length; i++)
            {
                preset = presets[i];
                if (i > 0 && i % 4 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                if (GUILayout.Button(preset.name, GUILayout.Width(175)))
                {
                    module.containerFuelTypeAdded(container, presets[i]);
                }
            }
            GUILayout.EndHorizontal();
        }

        private static void addWindowContainerRatioControls(ContainerDefinition container)
        {
            GUILayout.Label("Adjust sliders or input into text boxes to customize resource ratios");            
            VolumeRatioEntry[] ratioData = resourceEntries[containerIndex];                        
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Resource", GUILayout.Width(150));
            GUILayout.Label("% of Tank", GUILayout.Width(100));
            GUILayout.Label("Ratio", GUILayout.Width(100));
            GUILayout.Label("Units", GUILayout.Width(100));
            GUILayout.Label("Volume", GUILayout.Width(100));
            GUILayout.EndHorizontal();
            float totalVal = ratioData.Sum(m => m.tankData.ratio);
            int len = ratioData.Length;
            for (int i = 0; i < len; i++)
            {
                GUILayout.BeginHorizontal();
                if (ratioData[i].draw(totalVal))
                {
                    module.containerRatioUpdated(container, ratioData[i].tankData);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

    }

    public class VolumeRatioEntry
    {
        public readonly ContainerVolumeData tankData;
        int prevRatio;
        string textRatio;

        public VolumeRatioEntry(ContainerVolumeData data)
        {
            this.tankData = data;
            this.prevRatio = data.ratio;
            this.textRatio = data.ratio.ToString();
        }

        public bool draw(float totalRatio)
        {
            bool updated = false;
            if (tankData.ratio != prevRatio)
            {
                prevRatio = tankData.ratio;
                textRatio = prevRatio.ToString();
            }
            GUILayout.Label(tankData.name, GUILayout.Width(150));//resource name
            float tankPercent = totalRatio > 0 ? tankData.ratio / totalRatio : 0;
            GUILayout.HorizontalSlider(tankPercent, 0, 1, GUILayout.Width(100));
            //GUILayout.Label(tankPercent + "%", GUILayout.Width(100));
            string textVal = GUILayout.TextField(textRatio, GUILayout.Width(100));
            if (textVal != textRatio)
            {
                textRatio = textVal;
                int parsedTextVal;
                if (int.TryParse(textRatio, out parsedTextVal))
                {
                    tankData.setRatio(parsedTextVal);
                    updated = true;
                }
            }
            float tankUnits = tankData.units;
            GUILayout.Label(tankUnits.ToString(), GUILayout.Width(100));
            float tankVolume = tankData.volume;
            GUILayout.Label(tankVolume.ToString(), GUILayout.Width(100));
            return updated;
        }

    }
}
