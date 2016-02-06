using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    // Resonsible for tracking list of texture switch options, 
    // managing of actual switching of textures,
    // and restoring persistent option on reload.
    // may be controlled through external module (e.g resource or mesh-switch) through the two methods restoreDefaultTexture() and enableTextureSet(String setName)
    public class SSTUTextureSwitch : PartModule
    {
        //the default texture set to apply, only needed if you want to change the default texture of a mesh from whatever was compiled in the model
        [KSPField]
        public String defaultTextureSet = String.Empty;

        [KSPField]
        public bool allowInFlightChange = false;

        //if should be controlled through external module; disables the built-in switching buttons in favour of allowing the external control source to do it
        [KSPField]
        public bool externalControl = false;

        //currently selected texture set, by name
        [KSPField(isPersistant = true)]
        public String currentTextureSet = String.Empty;

        [Persistent]
        public String configNodeData = String.Empty;

        //actual texture set names
        private String[] textureSetNames;

        [KSPEvent(guiActiveEditor = true, guiActive = false, guiName = "Next Texture Set")]
        public void nextTextureSetEvent()
        {
            enableTextureSet(findNextTextureSet(currentTextureSet, false));
            int index = part.Modules.IndexOf(this);
            foreach (Part p in part.symmetryCounterparts)
            {
                ((SSTUTextureSwitch)p.Modules[index]).enableTextureSet(currentTextureSet);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasNode("TEXTURESET"))
            {
                configNodeData = node.ToString();
            }
            initialize();
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            initialize();
            if (externalControl)
            {
                Events["nextTextureSetEvent"].active = false;
            }
            Events["nextTextureSetEvent"].guiActiveUnfocused = !externalControl && allowInFlightChange;
        }

        //restores texture set data and either loads default texture set or saved texture set (if any)
        private void initialize()
        {
            loadConfigData();
            int len = textureSetNames.Length;
            for (int i = 0; i < len; i++)
            {
                textureSetNames[i] = textureSetNames[i].Trim();
            }
            if (!externalControl)
            {
                if (String.IsNullOrEmpty(currentTextureSet))//uninitialized, use defaults
                {
                    enableTextureSet(defaultTextureSet);
                }
                else
                {
                    enableTextureSet(currentTextureSet);
                }
            }            
        }

        private void loadConfigData()
        {
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);
            ConfigNode[] textureSets = node.GetNodes("TEXTURESET");
            textureSetNames = new String[textureSets.Length];
            for (int i = 0; i < textureSets.Length; i++)
            {
                textureSetNames[i] = textureSets[i].GetStringValue("name");
            }
        }

        //clears the persistent 'enabled texture set' data, and restores 'default texture set' 
        public void restoreDefaultTexture()
        {
            enableTextureSet(defaultTextureSet);
        }

        //enables a specific texture set, by name
        public void enableTextureSet(String name)
        {
            print("enabling texture set: " + name);
            TextureSet ts = TextureSets.INSTANCE.getTextureSet(name);
            if (ts != null)
            {
                ts.enable(part);
            }
            currentTextureSet = name;           
        }

        private String findNextTextureSet(String currentType, bool iterateBackwards)
        {
            int index = -1;
            int len = textureSetNames.Length;
            int iter = iterateBackwards ? -1 : 1;
            for (int i = 0; i < len; i++)
            {
                if (textureSetNames[i].Equals(currentType))
                {
                    index = i;
                }
            }
            if (index == -1)
            {
                MonoBehaviour.print("Could not locate current fuel type, returning first texture set name");
                return textureSetNames[0];
            }
            index += iter;
            if (index < 0) { index += len; }
            if (index >= len) { index -= len; }
            return textureSetNames[index];
        }
    }

}

