using System;

namespace SSTUTools
{
    public class SSTUEventRename : PartModule
    {
        
        private bool hasOnTick = false;
        private RenameEntry[] renameEntries;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);
            ConfigNode[] renameEntryNodes = node.GetNodes("RENAME");
            ConfigNode[] disableEntryNodes = node.GetNodes("DISABLE");

            int len = renameEntryNodes.Length;
            renameEntries = new RenameEntry[len];
            for (int i = 0; i < len; i++)
            {
                renameEntries[i] = new RenameEntry(renameEntryNodes[i]);
            }
        }

        public void Start()
        {
            foreach (RenameEntry entry in renameEntries)
            {
                if (!entry.onTick) { entry.update(part); }
            }
        }

        public void LateUpdate()
        {
            if (hasOnTick)
            {
                foreach (RenameEntry entry in renameEntries)
                {
                    if (entry.onTick) { entry.update(part); }
                }
            }
        }

        private class RenameEntry
        {
            public int moduleIndex;
            public bool isAction = false;
            public bool onTick = false;
            public String eventName = String.Empty;
            public String newGuiName = String.Empty;

            public RenameEntry(ConfigNode node)
            {
                moduleIndex = node.GetIntValue("moduleIndex");
                isAction = node.GetBoolValue("isAction");
                onTick = node.GetBoolValue("onTick");
                eventName = node.GetStringValue("eventName");
                newGuiName = node.GetStringValue("newGuiName");
            }

            public void update(Part part)
            {
                PartModule module = part.Modules[moduleIndex];
                if (isAction)
                {
                    module.Actions[eventName].guiName = newGuiName;
                }
                else
                {
                    module.Events[eventName].guiName = newGuiName;
                }
            }
        }
    }
}
