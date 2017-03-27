using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.UI.Screens;

namespace SSTUTools
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, true)]
    public class SSTULauncher : MonoBehaviour
    {
        private ApplicationLauncherButton recolorButton;
        
        private GameObject guiObject;
        private CraftRecolorGUI gui;

        public void Awake()
        {
            DontDestroyOnLoad(this);
            Texture2D tex;
            if (recolorButton == null)
            {
                tex = GameDatabase.Instance.GetTexture("Squad/PartList/SimpleIcons/RDIcon_fuelSystems-highPerformance", false);
                recolorButton = ApplicationLauncher.Instance.AddModApplication(recolorOpen, recolorClose, null, null, null, null, ApplicationLauncher.AppScenes.SPH|ApplicationLauncher.AppScenes.VAB, tex);
            }
        }

        public void OnDestroy()
        {
            if (recolorButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(recolorButton);
            }
            recolorButton = null;
        }

        public void recolorOpen()
        {
            if (guiObject == null)
            {
                guiObject = new GameObject("SSTURecolorGUI");
                gui = guiObject.AddComponent<CraftRecolorGUI>();
                gui.openGui();
            }
        }

        public void recolorClose()
        {
            if (guiObject != null)
            {
                gui.closeGui();
                gui = null;
                GameObject.Destroy(guiObject);
            }
        }

    }
}
