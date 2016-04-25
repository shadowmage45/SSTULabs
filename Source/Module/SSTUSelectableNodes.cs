using System;
using UnityEngine;

namespace SSTUTools
{   
    public class SSTUSelectableNodes : PartModule
    {
        [KSPField]
        public String nodeName = "top";

        [KSPField]
        public bool startsEnabled = true;

        [KSPField(isPersistant = true)]
        public bool currentlyEnabled = false;

        [KSPField(isPersistant = true)]
        public bool initialized = false;

        [KSPField]
        public Vector3 nodeDefaultPosition = Vector3.zero;

        [KSPField]
        public Vector3 nodeDefaultOrientation = Vector3.down;
         
        
        [KSPEvent(guiName = "Toggle Node", guiActiveEditor = true)]
        public void toggleNodeEvent()
        {
            toggleNode();
        }

        //[KSPEvent(guiName = "Invert Node", guiActiveEditor = false)]
        //public void invertNodeEvent()
        //{
        //    //TODO
        //}

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            Events["toggleNodeEvent"].guiName = "Toggle " + nodeName+" node";
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                if (!initialized)
                {
                    currentlyEnabled = startsEnabled;
                    initialized = true;
                    AttachNode node = part.findAttachNode(nodeName);
                    if (currentlyEnabled && node == null)
                    {
                        SSTUAttachNodeUtils.createAttachNode(part, nodeName, nodeDefaultPosition, nodeDefaultOrientation, 2);
                    }
                    else if (!currentlyEnabled && node != null && node.attachedPart == null)
                    {
                        SSTUAttachNodeUtils.destroyAttachNode(part, node);
                    }
                    else if (!currentlyEnabled && node != null && node.attachedPart != null)//error, should never occur if things were handled properly
                    {
                        currentlyEnabled = true;
                    }
                }
                else
                {
                    AttachNode node = part.findAttachNode(nodeName);
                    if (currentlyEnabled && node == null)
                    {
                        currentlyEnabled = true;
                        SSTUAttachNodeUtils.createAttachNode(part, nodeName, nodeDefaultPosition, nodeDefaultOrientation, 2);
                    }
                    else if (!currentlyEnabled && node != null && node.attachedPart == null)
                    {
                        currentlyEnabled = false;
                        SSTUAttachNodeUtils.destroyAttachNode(part, node);
                    }
                }
            }
        }

        public void toggleNode()
        {
            AttachNode node = part.findAttachNode(nodeName);
            if (node == null)
            {
                currentlyEnabled = true;
                SSTUAttachNodeUtils.createAttachNode(part, nodeName, nodeDefaultPosition, nodeDefaultOrientation, 2);
            }
            else if (node.attachedPart == null)
            {
                currentlyEnabled = false;
                SSTUAttachNodeUtils.destroyAttachNode(part, node);
            }
        }

        public static void updateNodePosition(Part part, String nodeName, Vector3 pos)
        {
            SSTUSelectableNodes[] modules = part.GetComponents<SSTUSelectableNodes>();
            int len = modules.Length;
            for (int i = 0; i < len; i++)
            {
                if (modules[i].nodeName == nodeName)
                {
                    modules[i].nodeDefaultPosition = pos;
                }
            }
        }
    }
}

