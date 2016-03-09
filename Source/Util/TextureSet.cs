using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class TextureSets
    {
        public static readonly TextureSets INSTANCE = new TextureSets();
        private TextureSets() { }

        private Dictionary<String, TextureSet> textureSets = new Dictionary<String, TextureSet>();

        private bool defsLoaded = false;

        private void loadTextureSets()
        {
            if (defsLoaded) { return; }
            defsLoaded = true;
            ConfigNode[] configNodes = GameDatabase.Instance.GetConfigNodes("SSTU_TEXTURESET");
            if (configNodes == null) { return; }
            TextureSet textureSet;
            foreach (ConfigNode node in configNodes)
            {
                textureSet = new TextureSet(node);
                textureSets.Add(textureSet.setName, textureSet);
                MonoBehaviour.print("Loaded texture set definition for: " + textureSet.setName);
            }
        }

        public TextureSet getTextureSet(String name)
        {
            loadTextureSets();
            TextureSet s = null;
            textureSets.TryGetValue(name, out s);
            return s;
        }
    }

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
            TextureSet[] sets = new TextureSet[len];
            for (int i = 0; i < len; i++)
            {
                String name = textureSetNodes[i].GetStringValue("name");
                sets[i] = TextureSets.INSTANCE.getTextureSet(textureSetNodes[i].GetStringValue("name"));
                if (sets[i] == null) { MonoBehaviour.print("ERROR: Could not locate texture set for name: " + name); }
            }
            return sets;
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
                Transform[] trs = part.FindModelTransforms(meshName);
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
            Renderer r = tr.renderer;
            //TODO check / update the shader for the material
            Material m = r.material;
            if (!String.IsNullOrEmpty(diffuseTextureName)) { m.mainTexture = GameDatabase.Instance.GetTexture(diffuseTextureName, false); }
            if (!String.IsNullOrEmpty(normalTextureName)) { m.SetTexture("_BumpMap", GameDatabase.Instance.GetTexture(normalTextureName, true)); }
            if (!String.IsNullOrEmpty(emissiveTextureName)) { m.SetTexture("_Emissive", GameDatabase.Instance.GetTexture(emissiveTextureName, false)); }
        }

    }
}

