using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SSTURecolorGUI : PartModule
    {

        private static GameObject guiObject;
        private static CraftRecolorGUI gui;

        [KSPEvent(guiName ="Open Recoloring GUI", guiActive = false, guiActiveEditor = true)]
        public void recolorGUIEvent()
        {
            bool open = true;
            if (guiObject != null)
            {
                //apparently delegates can/do use reference/memory location ==, which is exactl what is needed in this situation
                if (gui.guiCloseAction == recolorClose)
                {
                    open = false;
                }
                //kill existing GUI before opening new one
                gui.guiCloseAction();
                GameObject.Destroy(guiObject);
                guiObject = null;
            }
            if (open)
            {
                guiObject = new GameObject("SSTURecolorGUI");
                gui = guiObject.AddComponent<CraftRecolorGUI>();
                gui.openGUIPart(part);
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
