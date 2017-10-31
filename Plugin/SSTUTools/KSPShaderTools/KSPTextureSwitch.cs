using System;
using System.Collections.Generic;
using UnityEngine;

namespace KSPShaderTools
{
    // Resonsible for tracking list of texture switch options, 
    // managing of actual switching of textures,
    // and restoring persistent option on reload.
    // may be controlled through external module (e.g resource or mesh-switch) through the two methods restoreDefaultTexture() and enableTextureSet(String setName)
    public class KSPTextureSwitch : PartModule, IRecolorable
    {

        [KSPField]
        public bool allowInFlightChange = false;

        [KSPField]
        public string transformName = string.Empty;

        [KSPField]
        public string sectionName = "Recolorable";

        [KSPField]
        public bool canChangeInFlight = false;

        /// <summary>
        /// Current texture set.  ChooseOption UI widget is initialized inside of texture-set-container helper object
        /// </summary>
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Texture Set"),
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
            Callback<BaseField, System.Object> onChangeAction = delegate (BaseField a, System.Object b)
            {
                this.actionWithSymmetry(m =>
                {
                    m.currentTextureSet = currentTextureSet;
                    m.textureSets.enableCurrentSet(getModelTransforms());
                });
            };
            BaseField field = Fields[nameof(currentTextureSet)];
            field.uiControlEditor.onFieldChanged = onChangeAction;
            field.uiControlFlight.onFieldChanged = onChangeAction;
            field.guiActive = canChangeInFlight;
            if (textureSets.textureSets.Length <= 1)
            {
                field.guiActive = field.guiActiveEditor = false;
            }
        }

        //restores texture set data and either loads default texture set or saved texture set (if any)
        private void initialize()
        {
            loadConfigData();
        }

        private void loadConfigData()
        {
            if (textureSets != null)
            {
                //already initialized from OnLoad (prefab, some in-editor parts)
                return;
            }
            ConfigNode node = Utils.parseConfigNode(configNodeData);
            ConfigNode[] setNodes = node.GetNodes("TEXTURESET");
            textureSets = new TextureSetContainer(this, Fields[nameof(currentTextureSet)], Fields[nameof(persistentData)], setNodes);
            if (string.IsNullOrEmpty(currentTextureSet))
            {
                currentTextureSet = setNodes[0].GetValue("name");
            }
            this.updateUIChooseOptionControl(nameof(currentTextureSet), textureSets.getTextureSetNames(), textureSets.getTextureSetTitles(), true, currentTextureSet);
            textureSets.enableCurrentSet(getModelTransforms());
            Fields[nameof(currentTextureSet)].guiName = sectionName + " Texture";
        }

        private Transform[] getModelTransforms()
        {
            return string.IsNullOrEmpty(transformName) ? part.transform.FindChildren("model") : part.transform.FindChildren(transformName);
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
            textureSets.enableCurrentSet(getModelTransforms());
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

        internal TextureSet[] textureSets;

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
            this.textureSets = KSPShaderLoader.getTextureSets(textureSetNodes);
        }

        public void enableCurrentSet(Transform[] roots)
        {
            TextureSet set = Array.Find(textureSets, m => m.name == currentTextureSet);
            if (set == null)
            {
                MonoBehaviour.print("ERROR: KSPTextureSwitch could not locate texture set for name: " + currentTextureSet);
            }
            if (customColors == null || customColors.Length == 0)
            {
                customColors = new Color[3];
                customColors[0] = set.maskColors[0];
                customColors[1] = set.maskColors[1];
                customColors[2] = set.maskColors[2];
            }
            int len = roots.Length;
            for (int i = 0; i < len; i++)
            {
                set.enable(roots[i].gameObject, customColors);
            }
            saveColors(customColors);
        }

        public void enableCurrentSet(Transform root)
        {
            TextureSet set = Array.Find(textureSets, m => m.name == currentTextureSet);
            if (set == null)
            {
                MonoBehaviour.print("ERROR: KSPTextureSwitch could not locate texture set for name: " + currentTextureSet);
            }
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

        public string[] getTextureSetNames()
        {
            int len = textureSets.Length;
            string[] names = new string[len];
            for (int i = 0; i < len; i++)
            {
                names[i] = textureSets[i].name;
            }
            return names;
        }

        public string[] getTextureSetTitles()
        {
            int len = textureSets.Length;
            string[] names = new string[len];
            for (int i = 0; i < len; i++)
            {
                names[i] = textureSets[i].title;
            }
            return names;
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
                    r = Utils.safeParseFloat(dataSplits[0]);
                    g = Utils.safeParseFloat(dataSplits[1]);
                    b = Utils.safeParseFloat(dataSplits[2]);
                    a = dataSplits.Length >= 4 ? Utils.safeParseFloat(dataSplits[3]) : 1f;
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

