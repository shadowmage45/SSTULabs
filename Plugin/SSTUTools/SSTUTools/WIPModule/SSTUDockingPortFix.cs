using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUDockingPortFix : PartModule
    {

        [KSPField]
        public int portIndex = 0;

        private ModuleDockingNode dockNode;
        private BaseEvent dockNodeUndockEvent;
        private BaseEvent dockNodeUndockSameEvent;
        private BaseEvent dockNodeDecoupleEvent;
        private BaseEvent forceUndockEvent;
        private bool loaded = false;

        [KSPEvent(guiActive = false, guiActiveEditor = false)]
        public void forceUndock()
        {
            MonoBehaviour.print("Force undock!");
            if (dockNode.otherNode == null) { return; }
            dockNode.referenceNode.attachedPart = dockNode.otherNode.part;
            dockNode.Decouple();
            //updateState();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            MonoBehaviour.print("DockPortFix, OnStart() "+GetHashCode());
            GameEvents.onVesselPartCountChanged.Add(new EventData<Vessel>.OnEvent(vesselModified));
            GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(vesselModified));
        }

        public void OnDestroy()
        {
            MonoBehaviour.print("DockPortFix, OnDestroy() " + GetHashCode());
            GameEvents.onVesselPartCountChanged.Remove(new EventData<Vessel>.OnEvent(vesselModified));
            GameEvents.onVesselWasModified.Remove(new EventData<Vessel>.OnEvent(vesselModified));
        }

        public void Start()
        {
            MonoBehaviour.print("DockPortFix, Start() " + GetHashCode());
            if (!HighLogic.LoadedSceneIsFlight) { return; }
            dockNode = part.GetComponents<ModuleDockingNode>()[portIndex];
            dockNodeUndockEvent = dockNode.Events[nameof(dockNode.Undock)];
            dockNodeUndockSameEvent = dockNode.Events[nameof(dockNode.UndockSameVessel)];
            dockNodeDecoupleEvent = dockNode.Events[nameof(dockNode.Decouple)];
            forceUndockEvent = Events[nameof(forceUndock)];
            forceUndockEvent.guiName = "Force " + dockNodeUndockEvent.guiName;
            updateState();
        }

        public void Update()
        {
            if (loaded) { return; }
            if(dockNode==null || dockNode.st_ready== null) { return; }
            loaded = true;
            MonoBehaviour.print("Set dock node ready FSM Update delegate...");
            dockNode.st_ready.OnUpdate = delegate
            {
                //MonoBehaviour.print("MDN ready FSM update : " + dockNode.state);
                if (dockNode.state.Contains("Ready") && (dockNode.otherNode != null || dockNode.vesselInfo != null))
                {

                }
            };
        }

        private void vesselModified(Vessel v)
        {
            MonoBehaviour.print("DockPortFix, vesselModified() " + GetHashCode());
            updateState();
        }

        private void updateState()
        {
            MonoBehaviour.print("dpf updateState()");
            bool docked = dockNode.otherNode != null;
            bool display = (!dockNodeUndockEvent.active || !dockNodeUndockEvent.guiActive) && (!dockNodeDecoupleEvent.active || !dockNodeDecoupleEvent.guiActive) && (!dockNodeUndockSameEvent.active || !dockNodeUndockSameEvent.guiActive);
            bool state = dockNode.referenceNode.attachedPart != part.parent; ;
            forceUndockEvent.guiActive = docked && display && state;
            MonoBehaviour.print("dck/dis/st "+docked+" :: "+display + " :: "+state);
            MonoBehaviour.print("dns: " + dockNode.state);
        }

    }

    //appears to work well enough...
    //[KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class DockingPortFixAddon : MonoBehaviour
    {
        public void Start()
        {
            DontDestroyOnLoad(this);
            GameEvents.onVesselLoaded.Add(new EventData<Vessel>.OnEvent(onVesselLoaded));
        }

        public void OnDestroy()
        {
            GameEvents.onVesselLoaded.Remove(new EventData<Vessel>.OnEvent(onVesselLoaded));
        }

        //loop through parts on vessel
        //  loop through modules on part
        //    for each docking node found
        //      if no existing docking-port-fix module is found
        //        add new SSTUDockingPortFix set to index-in-docking-modules of the docking port being fixed
        // TODO - check for existing dock-port-fix modules?
        private void onVesselLoaded(Vessel v)
        {
            int len = v.Parts.Count;
            int dLen;
            Part p;
            ModuleDockingNode[] mdns;
            ModuleDockingNode mdn;
            ConfigNode dpfNode = new ConfigNode("MODULE");
            dpfNode.AddValue("name", nameof(SSTUDockingPortFix));
            for (int i = 0; i < len; i++)
            {
                p = v.Parts[i];
                if (p == null) { continue; }
                
                mdns = p.GetComponents<ModuleDockingNode>();
                if (mdns == null || mdns.Length == 0) { continue; }
                dLen = mdns.Length;
                for (int k = 0; k < dLen; k++)
                {
                    mdn = mdns[k];
                    dpfNode.SetValue("portIndex", k, true);
                    //dpf = (SSTUDockingPortFix)p.AddModule(dpfNode);
                }
            }
        }

    }
}
