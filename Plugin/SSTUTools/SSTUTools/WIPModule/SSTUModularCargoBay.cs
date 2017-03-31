using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools.WIPModule
{
    public class SSTUModularCargoBay : SSTUModularFuelTank
    {

        [KSPField(isPersistant = true, guiName ="NoseExtLimit", guiActive = true, guiActiveEditor = true),
         UI_FloatRange(minValue = 0.05f, maxValue = 1, stepIncrement = 0.05f, suppressEditorShipModified = true)]
        public float noseAnimLimit = 1f;

        [KSPField(isPersistant = true, guiName = "BodyExtLimit", guiActive = true, guiActiveEditor = true),
         UI_FloatRange(minValue = 0.05f, maxValue = 1, stepIncrement = 0.05f, suppressEditorShipModified = true)]
        public float bodyAnimLimit = 1f;

        [KSPField(isPersistant = true, guiName = "TailExtLimit", guiActive = true, guiActiveEditor = true),
         UI_FloatRange(minValue = 0.05f, maxValue = 1, stepIncrement = 0.05f, suppressEditorShipModified = true)]
        public float tailAnimLimit = 1f;
        
        [KSPField(isPersistant = true)]
        public string noseAnimState = AnimState.STOPPED_START.ToString();

        [KSPField(isPersistant = true)]
        public string bodyAnimState = AnimState.STOPPED_START.ToString();

        [KSPField(isPersistant = true)]
        public string tailAnimState = AnimState.STOPPED_START.ToString();
        
        private AnimationController noseAnimControl;
        private AnimationController bodyAnimControl;
        private AnimationController tailAnimControl;
        
        public void onNoseLimitUpdated(BaseField field, object obj)
        {
            if ((float)obj != noseAnimLimit)
            {
                noseAnimControl.setMaxTime(noseAnimLimit);                
                foreach (Part p in part.symmetryCounterparts)
                {
                    SSTUModularCargoBay mcb = p.GetComponent<SSTUModularCargoBay>();
                    mcb.noseAnimLimit = noseAnimLimit;
                    mcb.noseAnimControl.setMaxTime(noseAnimLimit);
                }
            }
        }
        
        public void onBodyLimitUpdated(BaseField field, object obj)
        {
            if ((float)obj != bodyAnimLimit)
            {
                bodyAnimControl.setMaxTime(bodyAnimLimit);
                foreach (Part p in part.symmetryCounterparts)
                {
                    SSTUModularCargoBay mcb = p.GetComponent<SSTUModularCargoBay>();
                    mcb.bodyAnimLimit = bodyAnimLimit;
                    mcb.bodyAnimControl.setMaxTime(bodyAnimLimit);
                }
            }
        }
        
        public void onTailLimitUpdated(BaseField field, object obj)
        {
            if ((float)obj != tailAnimLimit)
            {
                tailAnimControl.setMaxTime(tailAnimLimit);
                foreach (Part p in part.symmetryCounterparts)
                {
                    SSTUModularCargoBay mcb = p.GetComponent<SSTUModularCargoBay>();
                    mcb.tailAnimLimit = tailAnimLimit;
                    mcb.tailAnimControl.setMaxTime(tailAnimLimit);
                }
            }
        }
        
        [KSPAction("Toggle Nose Deploy")]
        public void toggleNoseDeployAction(KSPActionParam param)
        {
            handleAnimAction(param, noseAnimControl);
        }
        
        [KSPEvent(guiName = "Toggle Nose Deploy", guiActive =true, guiActiveEditor = true)]
        public void toggleNoseDeployEvent()
        {
            handleAnimEvent(noseAnimControl);
            foreach (Part p in part.symmetryCounterparts)
            {
                SSTUModularCargoBay mcb = p.GetComponent<SSTUModularCargoBay>();
                mcb.handleAnimEvent(mcb.noseAnimControl);
            }
        }

        [KSPAction("Toggle Body Deploy")]
        public void toggleBodyDeployAction(KSPActionParam param)
        {
            handleAnimAction(param, bodyAnimControl);
        }

        [KSPEvent(guiName = "Toggle Body Deploy", guiActive = true, guiActiveEditor = true)]
        public void toggleBodyDeployEvent()
        {
            handleAnimEvent(bodyAnimControl);
            foreach (Part p in part.symmetryCounterparts)
            {
                SSTUModularCargoBay mcb = p.GetComponent<SSTUModularCargoBay>();
                mcb.handleAnimEvent(mcb.bodyAnimControl);
            }
        }

        [KSPAction("Toggle Tail Deploy")]
        public void toggleTailDeployAction(KSPActionParam param)
        {
            handleAnimAction(param, tailAnimControl);
        }

        [KSPEvent(guiName = "Toggle Tail Deploy", guiActive = true, guiActiveEditor = true)]
        public void toggleTailDeployEvent()
        {
            handleAnimEvent(tailAnimControl);
            foreach (Part p in part.symmetryCounterparts)
            {
                SSTUModularCargoBay mcb = p.GetComponent<SSTUModularCargoBay>();
                mcb.handleAnimEvent(mcb.tailAnimControl);
            }
        }

        private void handleAnimAction(KSPActionParam param, AnimationController control)
        {
            AnimState s = control.animationState;
            if (param.type == KSPActionType.Activate && (s == AnimState.STOPPED_START || s == AnimState.PLAYING_BACKWARD))
            {
                control.setAnimState(AnimState.PLAYING_FORWARD, false);
            }
            else if (param.type == KSPActionType.Deactivate && (s == AnimState.STOPPED_END || s == AnimState.PLAYING_FORWARD))
            {
                control.setAnimState(AnimState.PLAYING_BACKWARD, false);
            }
        }

        private void handleAnimEvent(AnimationController control)
        {
            AnimState s = control.animationState;
            if (s == AnimState.STOPPED_START || s == AnimState.PLAYING_BACKWARD)
            {
                control.setAnimState(AnimState.PLAYING_FORWARD, false);
            }
            else if (s == AnimState.STOPPED_END || s == AnimState.PLAYING_FORWARD)
            {
                control.setAnimState(AnimState.PLAYING_BACKWARD, false);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            if (noseAnimControl != null)
            {
                noseAnimState = noseAnimControl.animationState.ToString();
                bodyAnimState = bodyAnimControl.animationState.ToString();
                tailAnimState = tailAnimControl.animationState.ToString();
                node.SetValue("noseAnimState", noseAnimState, true);
                node.SetValue("bodyAnimState", bodyAnimState, true);
                node.SetValue("tailAnimState", tailAnimState, true);
            }
        }

        public override string GetInfo()
        {
            return "This cargo bay has configurable height, diameter, nose, and tail options.";
        }

        public void Update()
        {
            if (!initialized) { return; }
            noseAnimControl.updateAnimationState();
            bodyAnimControl.updateAnimationState();
            tailAnimControl.updateAnimationState();
        }
        
        protected override void initialize()
        {
            base.initialize();
            if (noseAnimControl != null) { return; }
            noseAnimControl = new AnimationController();
            bodyAnimControl = new AnimationController();
            tailAnimControl = new AnimationController();
            noseAnimControl.setStateChangeCallback(noseAnimStateChanged);
            bodyAnimControl.setStateChangeCallback(bodyAnimStateChanged);
            tailAnimControl.setStateChangeCallback(tailAnimStateChanged);
            updateNoseAnimControl();
            updateBodyAnimControl();
            updateTailAnimControl();
            Fields["noseAnimLimit"].uiControlEditor.onFieldChanged = onNoseLimitUpdated;
            Fields["noseAnimLimit"].uiControlFlight.onFieldChanged = onNoseLimitUpdated;
            Fields["bodyAnimLimit"].uiControlEditor.onFieldChanged = onBodyLimitUpdated;
            Fields["bodyAnimLimit"].uiControlFlight.onFieldChanged = onBodyLimitUpdated;
            Fields["tailAnimLimit"].uiControlEditor.onFieldChanged = onTailLimitUpdated;
            Fields["tailAnimLimit"].uiControlFlight.onFieldChanged = onTailLimitUpdated;
            updateAirstreamShield();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Fields[nameof(currentNoseType)].uiControlEditor.onFieldChanged += delegate (BaseField a, object b) { updateNoseAnimControl(); };
            Fields[nameof(currentTankType)].uiControlEditor.onFieldChanged += delegate (BaseField a, object b) { updateBodyAnimControl(); };
            Fields[nameof(currentMountType)].uiControlEditor.onFieldChanged += delegate (BaseField a, object b) { updateTailAnimControl(); };
        }

        /// <summary>
        /// Update 'dorsal' and interior attach node(s) (all others handled by base MFT code)
        /// </summary>
        /// <param name="userInput"></param>
        protected override void updateAttachNodes(bool userInput)
        {
            //handle cap/mount/surface attach nodes through base code
            base.updateAttachNodes(userInput);
            //internal dorsal node
            if (tankModule.model.modelDefinition.attachNodeData.Length > 0)
            {                
                AttachNode node = part.FindAttachNode("dorsal");
                if (node == null) { return; }
                AttachNodeBaseData d = tankModule.model.modelDefinition.attachNodeData[0];
                Vector3 pos = d.position * tankModule.model.currentDiameterScale;
                SSTUAttachNodeUtils.updateAttachNodePosition(part, node, pos, d.orientation, userInput);
            }
            //internal front/rear nodes
            float height = tankModule.model.currentHeight + (tankModule.model.modelDefinition.fairingTopOffset * 2f * tankModule.model.currentHeightScale);
            height *= 0.5f;
            AttachNode front = part.FindAttachNode("front");
            if (front != null)
            {
                Vector3 pos = new Vector3(0, height, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, front, pos, Vector3.down, userInput);
            }
            AttachNode rear = part.FindAttachNode("rear");
            if (rear != null)
            {
                Vector3 pos = new Vector3(0, -height, 0);
                SSTUAttachNodeUtils.updateAttachNodePosition(part, rear, pos, Vector3.up, userInput);
            }
        }

        private void updateNoseAnimControl()
        {
            noseAnimControl.clearAnimationData();
            SSTUAnimData[] datas = noseModule.model.getAnimationData(getRootTransform(rootNoseTransformName, false));
            int len = datas.Length;
            for (int i = 0; i < len; i++)
            {
                datas[i].setAnimLayer(1);
                datas[i].setMaxTime(noseAnimLimit, noseAnimControl.animationState);
                noseAnimControl.addAnimationData(datas[i]);
            }
            noseAnimControl.setAnimState(noseAnimState);
            bool enabled = len >= 1;
            BaseEvent evt = Events["toggleNoseDeployEvent"];
            evt.guiActive = evt.guiActiveEditor = enabled;
            BaseAction act = Actions["toggleNoseDeployAction"];
            act.active = enabled;
            BaseField fld = Fields["noseAnimLimit"];
            fld.guiActive = fld.guiActiveEditor = enabled;
            updateNoseAnimUILabels();
        }

        private void updateBodyAnimControl()
        {
            bodyAnimControl.clearAnimationData();
            SSTUAnimData[] datas = tankModule.model.getAnimationData(getRootTransform(rootTransformName, false));
            int len = datas.Length;
            for (int i = 0; i < len; i++)
            {
                datas[i].setAnimLayer(2);
                datas[i].setMaxTime(bodyAnimLimit, bodyAnimControl.animationState);
                bodyAnimControl.addAnimationData(datas[i]);
            }
            bodyAnimControl.setAnimState(bodyAnimState);
            bool enabled = len >= 1;
            BaseEvent evt = Events["toggleBodyDeployEvent"];
            evt.guiActive = evt.guiActiveEditor = enabled;
            BaseAction act = Actions["toggleBodyDeployAction"];
            act.active = enabled;
            BaseField fld = Fields["bodyAnimLimit"];
            fld.guiActive = fld.guiActiveEditor = enabled;
            updateBodyAnimUILabels();
        }

        private void updateTailAnimControl()
        {
            tailAnimControl.clearAnimationData();
            SSTUAnimData[] datas = mountModule.model.getAnimationData(getRootTransform(rootMountTransformName, false));
            int len = datas.Length;
            for (int i = 0; i < len; i++)
            {
                datas[i].setAnimLayer(3);
                datas[i].setMaxTime(tailAnimLimit, bodyAnimControl.animationState);
                tailAnimControl.addAnimationData(datas[i]);
            }
            tailAnimControl.setAnimState(tailAnimState);
            bool enabled = len >= 1;
            BaseEvent evt = Events["toggleTailDeployEvent"];
            evt.guiActive = evt.guiActiveEditor = enabled;
            BaseAction act = Actions["toggleTailDeployAction"];
            act.active = enabled;
            BaseField fld = Fields["tailAnimLimit"];
            fld.guiActive = fld.guiActiveEditor = enabled;
            updateTailAnimUILabels();
        }
        
        private void noseAnimStateChanged(AnimState state)
        {
            noseAnimState = state.ToString();
            updateNoseAnimUILabels();
            updateAirstreamShield();
        }
        
        private void bodyAnimStateChanged(AnimState state)
        {
            bodyAnimState = state.ToString();
            updateBodyAnimUILabels();
            updateAirstreamShield();
        }
        
        private void tailAnimStateChanged(AnimState state)
        {
            tailAnimState = state.ToString();
            updateTailAnimUILabels();
            updateAirstreamShield();
        }

        private void updateNoseAnimUILabels()
        {
            Events["toggleNoseDeployEvent"].guiName = getLabelForState(noseAnimControl.animationState) + " Nose Doors";
        }

        private void updateBodyAnimUILabels()
        {
            Events["toggleBodyDeployEvent"].guiName = getLabelForState(bodyAnimControl.animationState) + " Body Doors";
        }

        private void updateTailAnimUILabels()
        {
            Events["toggleTailDeployEvent"].guiName = getLabelForState(tailAnimControl.animationState) + " Tail Doors";
        }

        private string getLabelForState(AnimState state)
        {
            switch (state)
            {
                case AnimState.STOPPED_START:
                    return "Open";
                case AnimState.STOPPED_END:
                    return "Close";
                case AnimState.PLAYING_FORWARD:
                    return "Close";
                case AnimState.PLAYING_BACKWARD:
                    return "Open";
                default:
                    return "Open";
            }
        }
        
        private void updateAirstreamShield()
        {
            SSTUAirstreamShield shield = part.GetComponent<SSTUAirstreamShield>();
            if (shield == null) { return; }
            //only care about the cargo-bay models for this setup
            //the fairing module should take care of its own airstream shielding setup
            //TODO - set the fairing module to auto-set the shielding dimensions on fairing dimension changes...
            if (noseModule.model.hasAnimation() && noseAnimControl.animationState==AnimState.STOPPED_START)
            {
                float rad = noseModule.model.currentDiameter * 0.5f;
                float bottom = tankModule.model.currentHeight * 0.5f;
                float top = bottom + noseModule.model.currentHeight;
                shield.addShieldArea("MCB-Nose", rad, rad, top, bottom, false, false);
            }
            else
            {
                shield.removeShieldArea("MCB-Nose");
            }
            if (tankModule.model.hasAnimation() && bodyAnimControl.animationState == AnimState.STOPPED_START)
            {
                float rad = tankModule.model.currentDiameter * 0.5f;
                float bottom = -tankModule.model.currentHeight * 0.5f;
                float top = -bottom;
                shield.addShieldArea("MCB-Body", rad, rad, top, bottom, false, false);
            }
            else
            {
                shield.removeShieldArea("MCB-Body");
            }
            if (mountModule.model.hasAnimation() && tailAnimControl.animationState == AnimState.STOPPED_START)
            {
                float rad = tankModule.model.currentDiameter * 0.5f;
                float top = -tankModule.model.currentHeight * 0.5f;
                float bottom = top - mountModule.model.currentHeight;
                shield.addShieldArea("MCB-Tail", rad, rad, top, bottom, false, false);
            }
            else
            {
                shield.removeShieldArea("MCB-Tail");
            }
        }

    }
}
