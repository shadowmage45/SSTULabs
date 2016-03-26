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
        [KSPField]
        public bool allowInFlightChange = false;

        //currently selected texture set, by name
        [KSPField(isPersistant = true)]
        public String currentTextureSet = String.Empty;

        //actual texture set names
        private TextureSet[] textureSets;
        
        [KSPEvent(guiActiveEditor = true, guiActive = false, guiName = "Next Texture Set")]
        public void nextTextureSetEvent()
        {
            enableTextureSet(SSTUUtils.findNext(textureSets, m=>m.setName==currentTextureSet, false).setName);
            int index = part.Modules.IndexOf(this);
            foreach (Part p in part.symmetryCounterparts)
            {
                ((SSTUTextureSwitch)p.Modules[index]).enableTextureSet(currentTextureSet);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            initialize();
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        //restores texture set data and either loads default texture set or saved texture set (if any)
        private void initialize()
        {
            loadConfigData();
            TextureSet currentSet = Array.Find(textureSets, m => m.setName == currentTextureSet);
            currentSet.enableFromMeshes(part);
        }

        private void loadConfigData()
        {
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);
            textureSets = TextureSet.loadTextureSets(node.GetNodes("TEXTURESET"));
        }
        
        //enables a specific texture set, by name
        public void enableTextureSet(String name)
        {
            TextureSet ts = Array.Find(textureSets, m => m.setName == name);
            if (ts != null)
            {
                ts.enableFromMeshes(part);
                currentTextureSet = name;
            } 
        }

    }
}

