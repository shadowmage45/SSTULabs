using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KSPShaderTools
{
    public class SSTURecolorGUI : PartModule, IPartGeometryUpdated, IPartTextureUpdated
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

        //IPartGeometryUpdated callback method
        public void geometryUpdated(Part part)
        {
            if (part == this.part && gui!=null)
            {
                gui.refreshGui(part);
            }
        }

        //IPartTextureUpdated callback method
        public void textureUpdated(Part part)
        {
            if (part == this.part && gui!=null)
            {
                gui.refreshGui(part);
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

        public override string GetInfo()
        {
            return "This part has configurable colors.";
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(editorVesselModified));
        }

        public void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(new EventData<ShipConstruct>.OnEvent(editorVesselModified));
        }

        public void editorVesselModified(ShipConstruct ship)
        {
            if (gui != null)
            {
                gui.refreshGui(part);
            }
        }
    }
}
