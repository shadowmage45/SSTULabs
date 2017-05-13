using System;
using System.Collections.Generic;

namespace SSTUTools
{
    public class SSTUFieldManipulator : PartModule
    {
        //all field datas.  These each get updated at least once when the module initializes
        private List<SSTUFieldManipulationData> fieldDatas = new List<SSTUFieldManipulationData>();
        //datas that should be updated on every GUI tick
        private List<SSTUFieldManipulationData> updateTickDatas = new List<SSTUFieldManipulationData>();
        //datas that should be updated on every LateUdpate tick (after Update ticks)
        private List<SSTUFieldManipulationData> lateTickDatas = new List<SSTUFieldManipulationData>();
        //datas that should be updated on every physics tick
        private List<SSTUFieldManipulationData> fixedTickDatas = new List<SSTUFieldManipulationData>();
        
        /// <summary>
        /// Loads configs and does initial pass on updating values
        /// </summary>
        public void Start()
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
            {
                return;
            }
            loadConfigs();
            updateConfigs(fieldDatas);
        }

        /// <summary>
        /// Updates any field datas that need updating on the Update GUI/render tick
        /// </summary>
        public void Update()
        {
            if (updateTickDatas.Count > 0)
            {
                updateConfigs(updateTickDatas);
            }
        }

        /// <summary>
        /// Updates any field datas that need updating on the LateUpdate (post Update) tick
        /// </summary>
        public void LateUpdate()
        {
            if (lateTickDatas.Count > 0)
            {
                updateConfigs(lateTickDatas);
            }
        }

        /// <summary>
        /// Updates any field datas that need updating on the FixedUpdate physics tick
        /// </summary>
        public void FixedUpdate()
        {
            if (fixedTickDatas.Count > 0)
            {
                updateConfigs(fixedTickDatas);
            }
        }

        /// <summary>
        /// Loads the field manipulation configs from the configs for each specific module
        /// </summary>
        private void loadConfigs()
        {
            if (part.partInfo == null) { return; }

            ConfigNode partNode = part.partInfo.partConfig;

            ConfigNode[] moduleNodes = partNode.GetNodes("MODULE");
            int moduleNodesLength = moduleNodes.Length;

            PartModule module;
            ConfigNode[] fieldNodes;
            ConfigNode fieldNode;
            SSTUFieldManipulationData fieldData = null;
            int fieldNodesLength;

            for (int i = 0; i < moduleNodesLength; i++)
            {
                fieldNodes = moduleNodes[i].GetNodes("SSTU_FIELDDATA");
                fieldNodesLength = fieldNodes.Length;
                if (fieldNodesLength > 0)
                {
                    module = part.Modules[i];
                    for (int k = 0; k < fieldNodesLength; k++)
                    {
                        fieldNode = fieldNodes[k];
                        fieldData = SSTUFieldManipulationData.createNew(fieldNode, module);
                        fieldDatas.Add(fieldData);
                        switch (fieldData.updateType)
                        {
                            case UpdateType.UPDATE:
                                updateTickDatas.Add(fieldData);
                                break;
                            case UpdateType.FIXED:
                                fixedTickDatas.Add(fieldData);
                                break;
                            case UpdateType.LATE:
                                lateTickDatas.Add(fieldData);
                                break;
                            case UpdateType.ONCE:
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }

        private void updateConfigs(List<SSTUFieldManipulationData> fieldDatas)
        {
            int len = fieldDatas.Count;
            for (int i = 0; i < len; i++)
            {
                fieldDatas[i].updateEnabledStatus(HighLogic.LoadedSceneIsEditor);
                fieldDatas[i].updateName();
            }
        }
    }

    public class SSTUFieldManipulationData
    {
        public readonly String fieldName;
        public readonly String newGuiName;
        public readonly PartModule module;
        public readonly UpdateType updateType = UpdateType.ONCE;
        public readonly FieldType fieldType = FieldType.FIELD;
        public readonly ActiveType flightActiveType = ActiveType.NO_CHANGE;
        public readonly ActiveType editorActiveType = ActiveType.NO_CHANGE;

        public SSTUFieldManipulationData(ConfigNode node, PartModule module)
        {
            fieldName = node.GetStringValue("name");
            newGuiName = node.GetStringValue("newGuiName");
            this.module = module;
            updateType = (UpdateType)Enum.Parse(typeof(UpdateType), node.GetStringValue("updateType", updateType.ToString()), true);
            fieldType = (FieldType)Enum.Parse(typeof(FieldType), node.GetStringValue("fieldType", fieldType.ToString()), true);
            flightActiveType = (ActiveType)Enum.Parse(typeof(ActiveType), node.GetStringValue("flightActiveType", flightActiveType.ToString()), true);
            editorActiveType = (ActiveType)Enum.Parse(typeof(ActiveType), node.GetStringValue("editorActiveType", editorActiveType.ToString()), true);
        }

        /// <summary>
        /// Update the field GUI name for this datas backing field
        /// </summary>
        public virtual void updateName()
        {

        }

        /// <summary>
        /// Updates the enabled/disabled/visible status for the backing field
        /// </summary>
        /// <param name="editor"></param>
        public virtual void updateEnabledStatus(bool editor)
        {

        }

        public static SSTUFieldManipulationData createNew(ConfigNode node, PartModule module)
        {
            SSTUFieldManipulationData fieldData;
            FieldType type;            
            type = (FieldType)Enum.Parse(typeof(FieldType), node.GetStringValue("fieldType", "field"), true);
            switch (type)
            {
                case FieldType.FIELD:
                    fieldData = new SSTUFieldData(node, module);
                    break;
                case FieldType.EVENT:
                    fieldData = new SSTUEventData(node, module);
                    break;
                case FieldType.ACTION:
                    fieldData = new SSTUActionData(node, module);
                    break;
                default:
                    fieldData = new SSTUFieldData(node, module);
                    break;
            }
            return fieldData;
        }
    }

    public class SSTUFieldData : SSTUFieldManipulationData
    {
        public readonly BaseField field;
        public SSTUFieldData(ConfigNode node, PartModule module) : base(node, module)
        {
            field = module.Fields[fieldName];
            if (field == null)
            {
                throw new NullReferenceException("ERROR: Could not locate event for name: " + fieldName + " in module: " + module + " in part: " + module.part);
            }
        }

        public override void updateEnabledStatus(bool editor)
        {
            if (editor && editorActiveType == ActiveType.NO_CHANGE) { return; }
            else if (!editor && flightActiveType == ActiveType.NO_CHANGE) { return; }
            ActiveType type = editor ? editorActiveType : flightActiveType;
            bool enable = type == ActiveType.ACTIVE;
            field.guiActive = field.guiActiveEditor = enable;            
        }

        public override void updateName()
        {
            if (!string.IsNullOrEmpty(newGuiName))
            {
                field.guiName = newGuiName;
            }
        }
    }

    public class SSTUEventData : SSTUFieldManipulationData
    {
        public readonly BaseEvent evt;
        public SSTUEventData(ConfigNode node, PartModule module) : base(node, module)
        {
            evt = module.Events[fieldName];
            if (evt == null)
            {
                throw new NullReferenceException("ERROR: Could not locate event for name: " + fieldName + " in module: " + module + " in part: " + module.part);
            }
        }

        public override void updateEnabledStatus(bool editor)
        {
            if (editor && editorActiveType == ActiveType.NO_CHANGE) { return; }
            else if (!editor && flightActiveType == ActiveType.NO_CHANGE) { return; }
            ActiveType type = editor ? editorActiveType : flightActiveType;
            bool enable = type == ActiveType.ACTIVE;
            evt.guiActive = evt.guiActiveEditor = enable;
        }

        public override void updateName()
        {
            if (!string.IsNullOrEmpty(newGuiName))
            {
                evt.guiName = newGuiName;
            }
        }
    }

    public class SSTUActionData : SSTUFieldManipulationData
    {
        public readonly BaseAction act;
        public SSTUActionData(ConfigNode node, PartModule module) : base(node, module)
        {
            act = module.Actions[fieldName];
            if (act == null)
            {
                throw new NullReferenceException("ERROR: Could not locate action for name: " + fieldName + " in module: " + module + " in part: " + module.part);
            }
        }

        public override void updateEnabledStatus(bool editor)
        {
            if (editor && editorActiveType == ActiveType.NO_CHANGE) { return; }
            else if (!editor && flightActiveType == ActiveType.NO_CHANGE) { return; }
            ActiveType type = editor ? editorActiveType : flightActiveType;
            bool enable = type == ActiveType.ACTIVE;
            act.active = enable;
        }

        public override void updateName()
        {
            if (!string.IsNullOrEmpty(newGuiName))
            {
                act.guiName = newGuiName;
            }
        }
    }

    public enum FieldType
    {
        FIELD,
        EVENT,
        ACTION           
    }

    public enum UpdateType
    {
        ONCE,
        UPDATE,
        FIXED,
        LATE
    }

    public enum ActiveType
    {
        NO_CHANGE,
        ACTIVE,
        INACTIVE
    }

}
