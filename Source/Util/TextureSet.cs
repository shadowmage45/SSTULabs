using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class TextureSet
    {
        public readonly String setName;
        public TextureData[] textureDatas;

        public TextureSet(ConfigNode node)
        {
            setName = node.GetStringValue("name");
            ConfigNode[] data = node.GetNodes("TEXTUREDATA");
            if (data == null || data.Length == 0) { textureDatas = new TextureData[0]; }
            else
            {
                int len = data.Length;
                textureDatas = new TextureData[len];
                for (int i = 0; i < len; i++)
                {
                    textureDatas[i] = new TextureData(data[i]);
                }
            }
        }

        public void enableFromMeshes(Part part)
        {
            foreach (TextureData data in textureDatas)
            {
                data.enableFromMeshes(part);
            }
        }

        public void enableFromMeshes(Transform tr)
        {
            foreach (TextureData data in textureDatas)
            {
                data.enableFromMeshes(tr);
            }
        }

        public void enableForced(Transform tr, bool recursive)
        {
            foreach (TextureData data in textureDatas)
            {
                data.enableForced(tr, recursive);
            }
        }

        public static TextureSet[] loadTextureSets(ConfigNode[] textureSetNodes)
        {
            int len = textureSetNodes.Length;
            List<TextureSet> sets = new List<TextureSet>();
            TextureSet set;
            for (int i = 0; i < len; i++)
            {
                String name = textureSetNodes[i].GetStringValue("name");
                set = getTextureSet(textureSetNodes[i].GetStringValue("name"));
                if (set == null) { MonoBehaviour.print("ERROR: Could not locate texture set for name: " + name); }
                else { sets.Add(set); }
            }
            return sets.ToArray();
        }

        public static TextureSet getTextureSet(String name)
        {
            ConfigNode[] configNodes = GameDatabase.Instance.GetConfigNodes("SSTU_TEXTURESET");
            ConfigNode setNode = Array.Find(configNodes, m => m.GetStringValue("name") == name);
            if (setNode == null) { return null; }
            return new TextureSet(setNode);
        }
    }

    public class TextureData
    {
        public String[] meshNames;
        public String shaderName;
        public String diffuseTextureName;
        public String normalTextureName;
        public String emissiveTextureName;

        public TextureData(ConfigNode node)
        {
            meshNames = node.GetStringValues("mesh");
            shaderName = node.GetStringValue("shader");
            diffuseTextureName = node.GetStringValue("diffuseTexture");
            normalTextureName = node.GetStringValue("normalTexture");
            emissiveTextureName = node.GetStringValue("emissiveTexture");
        }

        public void enableFromMeshes(Transform root)
        {
            enableTexture(root);
            foreach (String meshName in meshNames)
            {
                Transform[] trs = root.FindChildren(meshName);
                if (trs == null || trs.Length == 0)
                {
                    continue;
                }
                foreach (Transform tr in trs)
                {
                    enableTexture(tr);
                }
            }
        }

        public void enableForced(Transform root, bool recursive)
        {
            enableTexture(root);
            if (recursive)
            {
                foreach (Transform child in root)
                {
                    enableForced(child, true);
                }
            }
        }

        public void enableFromMeshes(Part part)
        {
            foreach (String meshName in meshNames)
            {
                Transform[] trs = part.transform.FindChildren(meshName);
                if (trs == null || trs.Length == 0)
                {
                    continue;
                }
                foreach (Transform tr in trs)
                {
                    enableTexture(tr);
                }
            }
        }

        private void enableTexture(Transform tr)
        {
            if (tr.renderer == null || tr.renderer.material == null)
            {
                //MonoBehaviour.print("ERROR: transform does not contain a renderer for mesh name: " + tr.name);
                return;
            }
            //TODO check / update the shader for the material
            Material m = tr.renderer.material;
            if (!String.IsNullOrEmpty(diffuseTextureName)) { m.mainTexture = GameDatabase.Instance.GetTexture(diffuseTextureName, false); }
            if (!String.IsNullOrEmpty(normalTextureName)) { m.SetTexture("_BumpMap", GameDatabase.Instance.GetTexture(normalTextureName, true)); }
            if (!String.IsNullOrEmpty(emissiveTextureName)) { m.SetTexture("_Emissive", GameDatabase.Instance.GetTexture(emissiveTextureName, false)); }
        }

    }
}

