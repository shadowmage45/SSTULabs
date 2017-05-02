using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SSTURecolorGUI : PartModule
    {

        private GameObject guiObject;
        private CraftRecolorGUI gui;

        [KSPEvent(guiName ="Open Recoloring GUI", guiActive = false, guiActiveEditor = true)]
        public void recolorGUIEvent()
        {
            if (guiObject == null)
            {
                guiObject = new GameObject("SSTURecolorGUI");
                gui = guiObject.AddComponent<CraftRecolorGUI>();
                gui.openGUIPart(EditorLogic.fetch, part);
                gui.guiCloseAction = recolorClose;
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
