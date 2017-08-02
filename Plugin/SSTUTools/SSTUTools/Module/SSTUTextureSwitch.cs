using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    // Resonsible for tracking list of texture switch options, 
    // managing of actual switching of textures,
    // and restoring persistent option on reload.
    // may be controlled through external module (e.g resource or mesh-switch) through the two methods restoreDefaultTexture() and enableTextureSet(String setName)
    public class SSTUTextureSwitch : PartModule, IRecolorable
    {
        [KSPField]
        public bool allowInFlightChange = false;

        [KSPField]
        public string transformName = string.Empty;

        [KSPField]
        public string sectionName = "Recolorable";

        /// <summary>
        /// Current texture set.  ChooseOption UI widget is initialized inside of texture-set-container helper object
        /// </summary>
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentTextureSet = String.Empty;

        /// <summary>
        /// Persistent data storage field used to store custom recoloring data
        /// </summary>
        [KSPField(isPersistant = true)]
        public string persistentData = string.Empty;

        [Persistent]
        public string configNodeData = string.Empty;

        private TextureSetContainer textureSets;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (string.IsNullOrEmpty(configNodeData)) { configNodeData = node.ToString(); }
            initialize();
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            initialize();

            Fields[nameof(currentTextureSet)].uiControlEditor.onFieldChanged = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.currentTextureSet = currentTextureSet;
                    m.textureSets.enableCurrentSet(getModelTransform());
                });
            };
        }

        //restores texture set data and either loads default texture set or saved texture set (if any)
        private void initialize()
        {
            loadConfigData();
        }

        private void loadConfigData()
        {
            if (textureSets != null) { return; }//already initialized from OnLoad (prefab, some in-editor parts)
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            ConfigNode[] setNodes = node.GetNodes("TEXTURESET");
            //MonoBehaviour.print("Set nodes length: " + setNodes.Length + " from node: \n" + node.ToString());
            textureSets = new TextureSetContainer(this, Fields[nameof(currentTextureSet)], Fields[nameof(persistentData)], setNodes);
            if (string.IsNullOrEmpty(currentTextureSet))
            {
                currentTextureSet = setNodes[0].GetValue("name");
            }
            this.updateUIChooseOptionControl(nameof(currentTextureSet), SSTUTextureUtils.getTextureSetNames(setNodes), SSTUTextureUtils.getTextureSetTitles(setNodes), true, currentTextureSet);
            //MonoBehaviour.print("Current texture set: " + currentTextureSet);
            textureSets.enableCurrentSet(getModelTransform());
            //SSTUUtils.recursePrintComponents(part.gameObject, "");
        }

        private Transform getModelTransform()
        {
            return string.IsNullOrEmpty(transformName) ? part.transform.FindRecursive("model") : part.transform.FindRecursive(transformName);
        }

        public string[] getSectionNames()
        {
            return new string[] { sectionName };
        }

        public Color[] getSectionColors(string name)
        {
            return textureSets.customColors;
        }

        public void setSectionColors(string name, Color[] colors)
        {
            textureSets.setCustomColors(colors);
            textureSets.enableCurrentSet(getModelTransform());
        }
    }

    /// <summary>
    /// Support/helper class for stand-alone texture switching (not used with model switching).
    /// Manages loading of texture sets, updating of color persistent data, and applying texture sets to model transforms
    /// </summary>
    public class TextureSetContainer
    {

        private PartModule pm;
        private BaseField textureSetField;
        private BaseField persistentDataField;

        internal Color[] customColors;

        private string currentTextureSet
        {
            get { return (string)textureSetField.GetValue(pm); }
        }

        private string persistentData
        {
            get { return (string)persistentDataField.GetValue(pm); }
            set { persistentDataField.SetValue(value, pm); }
        }

        public TextureSetContainer(PartModule pm, BaseField textureSetField, BaseField persistentDataField, ConfigNode[] textureSetNodes)
        {
            this.pm = pm;
            this.textureSetField = textureSetField;
            this.persistentDataField = persistentDataField;
            loadPersistentData(persistentData);
        }

        public void enableCurrentSet(Transform root)
        {
            TextureSet set = SSTUTextureUtils.getTextureSet(currentTextureSet);
            if (customColors == null || customColors.Length == 0)
            {
                customColors = new Color[3];
                customColors[0] = set.maskColors[0];
                customColors[1] = set.maskColors[1];
                customColors[2] = set.maskColors[2];
            }
            set.enable(root.gameObject, customColors);
            saveColors(customColors);
        }

        public void setCustomColors(Color[] colors)
        {
            customColors = colors;
            saveColors(customColors);
        }

        private void loadPersistentData(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                string[] colorSplits = data.Split(';');
                string[] dataSplits;
                int len = colorSplits.Length;
                customColors = new Color[len];
                float r, g, b, a;
                for (int i = 0; i < len; i++)
                {
                    dataSplits = colorSplits[i].Split(',');
                    r = SSTUUtils.safeParseFloat(dataSplits[0]);
                    g = SSTUUtils.safeParseFloat(dataSplits[1]);
                    b = SSTUUtils.safeParseFloat(dataSplits[2]);
                    a = dataSplits.Length >= 4 ? SSTUUtils.safeParseFloat(dataSplits[3]) : 1f;
                    customColors[i] = new Color(r, g, b, a);
                }
            }
            else
            {
                customColors = new Color[0];
            }
        }

        private void saveColors(Color[] colors)
        {
            if (colors == null || colors.Length == 0) { return; }
            int len = colors.Length;
            string data = string.Empty;
            for (int i = 0; i < len; i++)
            {
                if (i > 0) { data = data + ";"; }
                data = data + colors[i].r + ",";
                data = data + colors[i].g + ",";
                data = data + colors[i].b + ",";
                data = data + colors[i].a;
            }
            persistentData = data;
        }

    }

}

