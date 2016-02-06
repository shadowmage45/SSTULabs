using System;
using System.Collections;
using System.Collections.Generic;
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
        public Vector3 nodeDefaultOrientation = Vector3.up;
         
        
        [KSPEvent(guiName = "Toggle Node", guiActiveEditor = true)]
        public void toggleNodeEvent()
        {
            toggleNode();
        }

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
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
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
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }
    }
}

