using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text;

namespace SSTUTools
{
    public class SSTUFieldManipulator : PartModule
    {
        //all field datas.  These each get updated at least once when the module initializes
        private List<SSTUFieldData> fieldDatas = new List<SSTUFieldData>();
        //datas that should be updated on every GUI tick
        private List<SSTUFieldData> updateTickDatas = new List<SSTUFieldData>();
        //datas that should be updated on every LateUdpate tick (after Update ticks)
        private List<SSTUFieldData> lateTickDatas = new List<SSTUFieldData>();
        //datas that should be updated on every physics tick
        private List<SSTUFieldData> fixedTickDatas = new List<SSTUFieldData>();
        
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
            SSTUFieldData fieldData;
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
                        fieldData = new SSTUFieldData(fieldNode, module);
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

        private void updateConfigs(List<SSTUFieldData> fieldDatas)
        {
            int len = fieldDatas.Count;
            for (int i = 0; i < len; i++)
            {
                fieldDatas[i].updateEnabledStatus(HighLogic.LoadedSceneIsEditor);
                fieldDatas[i].updateName();
            }
        }
    }

    public class SSTUFieldData
    {
        private String fieldName;
        private String newGuiName;
        private PartModule module;
        private FieldType fieldType = FieldType.FIELD;
        public UpdateType updateType = UpdateType.ONCE;
        private ActiveType flightActiveType = ActiveType.NO_CHANGE;
        private ActiveType editorActiveType = ActiveType.NO_CHANGE;

        public SSTUFieldData(ConfigNode node, PartModule module)
        {
            this.module = module;
            fieldName = node.GetStringValue("name");
            newGuiName = node.GetStringValue("newGUIName");
            fieldType = (FieldType)Enum.Parse(typeof(FieldType), node.GetStringValue("fieldType", fieldType.ToString()), true);
            updateType = (UpdateType)Enum.Parse(typeof(UpdateType), node.GetStringValue("updateType", updateType.ToString()), true);
            flightActiveType = (ActiveType)Enum.Parse(typeof(ActiveType), node.GetStringValue("flightActiveType", flightActiveType.ToString()), true);
            editorActiveType = (ActiveType)Enum.Parse(typeof(ActiveType), node.GetStringValue("editorActiveType", editorActiveType.ToString()), true);
        }

        /// <summary>
        /// Update the field GUI name for this datas backing field
        /// </summary>
        public void updateName()
        {
            if (String.IsNullOrEmpty(newGuiName)) { return; }
            switch (fieldType)
            {
                case FieldType.FIELD:
                    module.Fields[fieldName].guiName = newGuiName;
                    break;
                case FieldType.EVENT:
                    module.Events[fieldName].guiName = newGuiName;
                    break;
                case FieldType.ACTION:
                    module.Actions[fieldName].guiName = newGuiName;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Updates the enabled/disabled/visible status for the backing field
        /// </summary>
        /// <param name="editor"></param>
        public void updateEnabledStatus(bool editor)
        {
            if (editor && editorActiveType == ActiveType.NO_CHANGE) { return; }
            else if (!editor && flightActiveType == ActiveType.NO_CHANGE) { return; }

            MonoBehaviour.print("updating field for name: "+fieldName+" for fieldType: "+fieldType);

            ActiveType type = editor ? editorActiveType : flightActiveType;
            bool enable = type == ActiveType.ACTIVE;

            switch (fieldType)
            {
                case FieldType.FIELD:
                    module.Fields[fieldName].guiActive = module.Fields[fieldName].guiActiveEditor = enable;
                    break;
                case FieldType.EVENT:
                    module.Events[fieldName].active = enable;
                    break;
                case FieldType.ACTION:
                    module.Actions[fieldName].active = enable;
                    break;
                default:
                    break;
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
