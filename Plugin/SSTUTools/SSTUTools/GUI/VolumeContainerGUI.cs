using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class VolumeContainerGUI
    {
        private static Vector2 scrollPos;
        private static Rect windowRect = new Rect(Screen.width - 900, 40, 800, 600);
        private static int id = 10000;
        private static int containerIndex = 0;
        public static SSTUVolumeContainer module;
        private static ContainerDefinition[] containers;
        private static VolumeRatioEntry[][] resourceEntries;

        private static Rect statWindowRect = new Rect(Screen.width - 900 - 250, 40, 250, 300);
        private static int statId;
        
        public static void openGUI(SSTUVolumeContainer container, ContainerDefinition[] modContainers)
        {
            containerIndex = 0;
            module = container;
            int len = modContainers.Length;
            List<ContainerDefinition> availContainers = new List<ContainerDefinition>();
            for (int i = 0; i < len; i++)
            {
                if (modContainers[i].guiAvailable)// && modContainers[i].rawVolume > 0)
                {
                    availContainers.Add(modContainers[i]);
                }
            }
            containers = availContainers.ToArray();
            id = module.GetInstanceID();
            statId = id + 1;
            len = containers.Length;
            //if nothing is available to adjust, do not open the window
            if (len <= 0)
            {
                closeGUI();
                return;
            }
            resourceEntries = new VolumeRatioEntry[len][];
            string[] names;
            PartResourceDefinition def;
            for (int i = 0; i < len; i++)
            {
                names = containers[i].getResourceNames();
                int len2 = names.Length;
                resourceEntries[i] = new VolumeRatioEntry[len2];
                for (int k = 0; k < len2; k++)
                {
                    def = PartResourceLibrary.Instance.GetDefinition(names[k]);
                    resourceEntries[i][k] = new VolumeRatioEntry(containers[i], names[k], def.displayName, containers[i].getResourceUnitRatio(names[k]));
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
            try
            {
                windowRect = GUI.Window(id, windowRect, addContainerWindow, "SSTUVolumeContainer");
                statWindowRect = GUI.Window(statId, statWindowRect, addStatWindow, "Stats For This Container");
            }
            catch (Exception e)
            {
                MonoBehaviour.print("Caught exception while rendering VolumeContainer GUI");
                MonoBehaviour.print(e.Message);
                MonoBehaviour.print(System.Environment.StackTrace);
            }
        }

        private static void addStatWindow(int id)
        {
            float vu = containers[containerIndex].usableVolume;
            float vt = containers[containerIndex].rawVolume;
            float vl = vt - vu;
            float md = containers[containerIndex].containerMass;
            float mp = containers[containerIndex].resourceMass;
            float mt = md + mp;
            float mpt = module.part.mass;
            float cd = containers[containerIndex].containerCost;
            float cp = containers[containerIndex].resourceCost;
            float ct = cd + cp;
            GUILayout.BeginVertical();
                        
            GUILayout.BeginHorizontal();
            GUILayout.Label("Usable Vol:", GUILayout.Width(100));
            GUILayout.Label(vu.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Vol:", GUILayout.Width(100));
            GUILayout.Label(vt.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Tankage Vol:", GUILayout.Width(100));
            GUILayout.Label(vl.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Dry Mass:", GUILayout.Width(100));
            GUILayout.Label(md.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Prop Mass:", GUILayout.Width(100));
            GUILayout.Label(mp.ToString());
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Mass:", GUILayout.Width(100));
            GUILayout.Label(mt.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Part Mass:", GUILayout.Width(100));
            GUILayout.Label(mpt.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Dry Cost:", GUILayout.Width(100));
            GUILayout.Label(cd.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Res Cost:", GUILayout.Width(100));
            GUILayout.Label(cp.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Cost:", GUILayout.Width(100));
            GUILayout.Label(ct.ToString());
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private static void addContainerWindow(int id)
        {
            GUILayout.BeginVertical();
            string mainLabel = "Current: " + containers[containerIndex].name + " :: " + containers[containerIndex].usableVolume + " / " + containers[containerIndex].rawVolume + "l";
            if (containers.Length > 1)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Prev Container", GUILayout.Width(200)))
                {
                    containerIndex--;
                    if (containerIndex < 0) { containerIndex = containers.Length - 1; }
                }
                GUILayout.Label(mainLabel);
                if (GUILayout.Button("Next Container", GUILayout.Width(200)))
                {
                    containerIndex++;
                    if (containerIndex >= containers.Length) { containerIndex = 0; }
                }
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
            GUILayout.Label("Current Type: " + container.currentModifier.name);
            GUILayout.EndHorizontal();
            ContainerModifier[] mods = container.modifiers;
            ContainerModifier mod;
            int len = mods.Length;
            int index = 0;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < len; i++)
            {
                mod = mods[i];
                if (!mod.isAvailable(module))
                {
                    continue;
                }
                if (index > 0 && index % 4 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                if (GUILayout.Button(mod.title, GUILayout.Width(175)))
                {
                    module.containerTypeUpdated(container, mod, true);
                }
                index++;
            }
            GUILayout.EndHorizontal();
        }

        private static void addWindowFuelTypeControls(ContainerDefinition container)
        {
            GUILayout.Label("Fuel Types -- Click to add ratio, CTRL click to set ratio, SHIFT click to subtract ratio");
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
                    if (ctrlPressed())//ctrl == set fuel type
                    {                        
                        container.module.setFuelPreset(container, presets[i], true);
                    }
                    else if (shiftPressed())
                    {
                        container.module.subtractPresetRatios(container, presets[i], true);
                    }
                    else
                    {
                        container.module.addPresetRatios(container, presets[i], true);
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        private static bool ctrlPressed()
        {            
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        }

        private static bool shiftPressed() { return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); }

        private static void addWindowContainerRatioControls(ContainerDefinition container)
        {
            GUILayout.Label("Adjust ratio through input box to change resource ratios");
            VolumeRatioEntry[] ratioData = resourceEntries[containerIndex];                
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Resource", GUILayout.Width(150));
            GUILayout.Label("Unit Ratio", GUILayout.Width(100));
            GUILayout.Label("Units", GUILayout.Width(80));
            GUILayout.Label("Volume", GUILayout.Width(80));
            GUILayout.Label("Mass", GUILayout.Width(80));
            GUILayout.Label("Cost", GUILayout.Width(80));
            GUILayout.Label("% of Tank", GUILayout.Width(80));
            GUILayout.Label("Fill %", GUILayout.Width(80));
            GUILayout.EndHorizontal();
            int len = ratioData.Length;
            for (int i = 0; i < len; i++)
            {
                GUILayout.BeginHorizontal();
                ratioData[i].draw();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        public static void updateGuiData()
        {
            int len = resourceEntries.Length;
            for (int i = 0; i < len; i++)
            {
                VolumeRatioEntry[] ratioData = resourceEntries[i];
                int len2 = ratioData.Length;
                for (int k = 0; k < len2; k++)
                {
                    ratioData[k].updateCachedValues();
                }
            }
        }

    }

    public class VolumeRatioEntry
    {
        ContainerDefinition container;
        public readonly string resourceName;
        public readonly string displayName;
        private int prevRatio;
        private string textRatio;

        private float units;
        private float volume;
        private float resourceMass;
        private float cost;
        private float percent;
        private float fillPercent;

        public VolumeRatioEntry(ContainerDefinition container, string resourceName, string displayName, int startRatio)
        {
            this.container = container;
            this.resourceName = resourceName;
            this.displayName = displayName;
            this.prevRatio = startRatio;
            this.textRatio = prevRatio.ToString();
            this.fillPercent = container.getResourceFillPercent(resourceName);
            updateCachedValues();
        }

        public bool draw()
        {
            bool update = false;
            int currentUnitRatio = container.getResourceUnitRatio(resourceName);
            float currentVolumeRatio = container.getResourceVolumeRatio(resourceName);
            float totalVolumeRatio = container.totalVolumeRatio;
            if (currentUnitRatio != prevRatio)//was updated externally...
            {
                prevRatio = currentUnitRatio;
                textRatio = prevRatio.ToString();
                update = true;
            }
            GUILayout.Label(displayName, GUILayout.Width(150));
            string textVal = GUILayout.TextField(textRatio, GUILayout.Width(100));
            if (textVal != textRatio)
            {
                textRatio = textVal;
                int parsedTextVal;
                if (int.TryParse(textRatio, out parsedTextVal))
                {
                    prevRatio = parsedTextVal;
                    container.module.setResourceRatio(container, resourceName, parsedTextVal, true);
                    update = true;
                }
            }
            
            GUILayout.Label(units.ToString(), GUILayout.Width(80));
            GUILayout.Label(volume.ToString(), GUILayout.Width(80));            
            GUILayout.Label(resourceMass.ToString(), GUILayout.Width(80));            
            GUILayout.Label(cost.ToString(), GUILayout.Width(80));            
            GUILayout.Label(percent.ToString(), GUILayout.Width(80));
            float val = GUILayout.HorizontalSlider(fillPercent, 0, 1, GUILayout.Width(80));
            if (val != fillPercent)
            {
                fillPercent = val;
                container.module.setResourceFillPercent(container, resourceName, fillPercent);
                update = true;
            }
            return update;
        }

        public void updateCachedValues()
        {
            units = container.getResourceUnits(resourceName);
            volume = container.getResourceVolume(resourceName);
            resourceMass = container.getResourceMass(resourceName);
            cost = container.getResourceCost(resourceName);
            percent = container.usableVolume<=0? 0 : container.getResourceVolume(resourceName) / container.usableVolume;
            fillPercent = container.getResourceFillPercent(resourceName);
        }
    }

}
