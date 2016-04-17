using System;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUDecalSwitch : PartModule
    {
        [KSPField]
        public string decalMeshName;

        [KSPField(isPersistant = true)]
        public string currentDecal = string.Empty;

        private Transform[] meshes;        
        private SSTUDecal[] possibleDecals;

        [KSPEvent(guiName ="Next Decal", guiActiveEditor = true)]
        public void nextDecalEvent()
        {
            SSTUDecal next = SSTUUtils.findNext(possibleDecals, m => m.name == currentDecal, false);
            currentDecal = next.name;
            foreach (Transform mesh in meshes)
            {
                next.enable(mesh);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        private void initialize()
        {
            meshes = part.transform.FindChildren(decalMeshName);
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);
            ConfigNode[] decalNodes = node.GetNodes("DECAL");
            int len = decalNodes.Length;
            possibleDecals = new SSTUDecal[len];
            for (int i = 0; i < len; i++)
            {
                possibleDecals[i] = new SSTUDecal(decalNodes[i]);
            }
            SSTUDecal currentDecalObj = Array.Find(possibleDecals, m => m.name == currentDecal);
            if (currentDecalObj == null && len > 0)
            {
                currentDecalObj = possibleDecals[0];
                currentDecal = currentDecalObj.name;
            }
            else if(currentDecalObj==null)
            {
                MonoBehaviour.print("ERROR: NO decals found to load for part: " + part.name);
            }
            foreach (Transform mesh in meshes)
            {
                currentDecalObj.enable(mesh);
            }
        }
    }

    public class SSTUDecal
    {
        public readonly string name;
        public readonly string texture;
        public SSTUDecal(ConfigNode node)
        {
            name = node.GetStringValue("name");
            texture = node.GetStringValue("texture");
        }

        public void enable(Transform transform)
        {
            Renderer renderer = transform.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (string.IsNullOrEmpty(texture))
                {
                    renderer.enabled = false;
                }
                else
                {
                    Texture tex = SSTUUtils.findTexture(texture, false);
                    renderer.material.mainTexture = tex;
                    renderer.enabled = true;
                }
            }
        }
    }

}
