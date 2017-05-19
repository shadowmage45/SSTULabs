using System;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUFlagDecal : PartModule, IPartGeometryUpdated
    {

        [KSPField]
        public String transformName = String.Empty;

        [KSPField(isPersistant = true)]
        public bool flagEnabled = true;
        
        [KSPEvent(guiName = "Toggle Flag Visibility", guiActiveEditor = true)]
        public void toggleFlagEvent()
        {
            onFlagToggled(true);
        }

        private void onFlagToggled(bool updateSymmetry)
        {
            flagEnabled = !flagEnabled;
            updateFlagTransform();
            if (updateSymmetry)
            {
                int index = part.Modules.IndexOf(this);
                foreach (Part p in part.symmetryCounterparts) { ((SSTUFlagDecal)p.Modules[index]).onFlagToggled(false); }
            }
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

        //IPartGeometryUpdated callback method
        public void geometryUpdated(Part part)
        {
            if (part == this.part)
            {
                updateFlagTransform();
            }
        }

        public void updateFlagTransform()
        {
            
            String textureName = part.flagURL;
            if (HighLogic.LoadedSceneIsEditor && String.IsNullOrEmpty(textureName)) { textureName = EditorLogic.FlagURL; }
            if (String.IsNullOrEmpty(textureName) && HighLogic.CurrentGame!=null) { textureName = HighLogic.CurrentGame.flagURL; }
            Transform[] trs = part.FindModelTransforms(transformName);
            if (String.IsNullOrEmpty(textureName) || !flagEnabled)
            {
                Renderer r;
                Transform t;
                int len = trs.Length;
                for (int i = 0; i < len; i++)
                {
                    t = trs[i];
                    if ((r = t.GetComponent<Renderer>()) != null)
                    {
                        r.enabled = false;
                    }
                }
            }
            else
            {
                Texture texture = GameDatabase.Instance.GetTexture(textureName, false);
                Renderer r;
                Transform t;
                int len = trs.Length;
                for (int i = 0; i < len; i++)
                {
                    t = trs[i];
                    if ((r = t.GetComponent<Renderer>()) != null)
                    {
                        r.material.mainTexture = texture;
                        r.enabled = true;
                    }
                }
            }
        }
    }
}
