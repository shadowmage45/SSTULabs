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
            string[] names;
            for (int i = 0; i < len; i++)
            {
                names = modContainers[i].getResourceNames();
                int len2 = names.Length;
                resourceEntries[i] = new VolumeRatioEntry[len2];
                for (int k = 0; k < len2; k++)
                {
                    resourceEntries[i][k] = new VolumeRatioEntry(modContainers[i], names[k], modContainers[i].getResourceRatio(names[k]));
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
            string mainLabel = "Current: " + containers[containerIndex].name + " :: " + containers[containerIndex].usableVolume + " / " + containers[containerIndex].rawVolume + "l";
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
            GUILayout.Label("Current Type: " + container.getCurrentModifier().name);
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
                    container.addPresetRatios(presets[i]);
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
            float totalVal = container.totalRatio;
            int len = ratioData.Length;
            for (int i = 0; i < len; i++)
            {
                GUILayout.BeginHorizontal();
                ratioData[i].draw();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

    }

    public class VolumeRatioEntry
    {
        ContainerDefinition container;
        public readonly string resourceName;
        int prevRatio;
        string textRatio;

        public VolumeRatioEntry(ContainerDefinition container, string resourceName, int startRatio)
        {
            this.container = container;
            this.resourceName = resourceName;
            this.prevRatio = startRatio;
            this.textRatio = prevRatio.ToString();
        }

        public void draw()
        {
            int currentRatio = container.getResourceRatio(resourceName);
            int totalRatio = container.totalRatio;
            if (currentRatio != prevRatio)//was updated externally...
            {
                prevRatio = currentRatio;
                textRatio = prevRatio.ToString();
            }
            GUILayout.Label(resourceName, GUILayout.Width(150));//resource name
            float tankPercent = totalRatio > 0 ? currentRatio / totalRatio : 0;
            GUILayout.HorizontalSlider(tankPercent, 0, 1, GUILayout.Width(100));
            string textVal = GUILayout.TextField(textRatio, GUILayout.Width(100));
            if (textVal != textRatio)
            {
                textRatio = textVal;
                int parsedTextVal;
                if (int.TryParse(textRatio, out parsedTextVal))
                {
                    prevRatio = parsedTextVal;
                    container.setResourceRatio(resourceName, parsedTextVal);
                }
            }
            float tankUnits = container.getResourceUnits(resourceName);
            GUILayout.Label(tankUnits.ToString(), GUILayout.Width(100));
            float tankVolume = container.getResourceVolume(resourceName);
            GUILayout.Label(tankVolume.ToString(), GUILayout.Width(100));
        }

    }
}
