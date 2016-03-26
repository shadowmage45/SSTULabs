using System;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUFlagDecal : PartModule
    {

        [KSPField]
        public String transformName = String.Empty;

        [KSPField(isPersistant = true)]
        public bool flagEnabled = true;
        
        [KSPEvent(guiName = "Toggle Flag Visibility", guiActiveEditor = true)]
        public void toggleFlagEvent()
        {
            flagEnabled = !flagEnabled;
            updateFlagTransform();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            updateFlagTransform();
            GameEvents.onMissionFlagSelect.Add(new EventData<string>.OnEvent(this.onFlagChanged));
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            updateFlagTransform();
        }

        private void OnDestroy()
        {
            GameEvents.onMissionFlagSelect.Remove(new EventData<string>.OnEvent(this.onFlagChanged));
        }

        public void onFlagChanged(String flagUrl)
        {
            updateFlagTransform();
        }

        public void updateFlagTransform()
        {
            
            String textureName = part.flagURL;
            if (HighLogic.LoadedSceneIsEditor && String.IsNullOrEmpty(textureName)) { textureName = EditorLogic.FlagURL; }
            if (String.IsNullOrEmpty(textureName) && HighLogic.CurrentGame!=null) { textureName = HighLogic.CurrentGame.flagURL; }
            print("updating flag transform... texture: "+textureName);
            Transform[] trs = part.FindModelTransforms(transformName);
            if (String.IsNullOrEmpty(textureName) || !flagEnabled)
            {
                foreach (Transform tr in trs)
                {
                    if (tr.renderer != null) { tr.renderer.enabled = false; }
                }
            }
            else
            {
                Texture texture = GameDatabase.Instance.GetTexture(textureName, false);
                foreach (Transform tr in trs)
                {
                    if (tr.renderer != null)
                    {
                        tr.renderer.sharedMaterial.mainTexture = texture;
                        tr.renderer.enabled = true;
                    }
                }
            }
        }
    }
}
