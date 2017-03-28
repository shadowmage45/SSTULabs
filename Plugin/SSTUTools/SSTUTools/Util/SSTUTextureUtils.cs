using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public static class SSTUTextureUtils
    {
        public static void updateModelMaterial(Transform root, string[] excludeMeshes, string[] meshes, string shader, ShaderProperty[] props)
        {
            //black-list, do everything not specified in excludeMeshes array
            if (excludeMeshes != null && excludeMeshes.Length > 0)
            {
                Renderer[] allRends = root.GetComponentsInChildren<Renderer>();
                int len = allRends.Length;
                for (int i = 0; i < len; i++)
                {
                    if (!excludeMeshes.Contains(allRends[i].name))
                    {
                        updateRenderer(allRends[i], shader, props);
                    }
                }
            }
            else if (meshes == null || meshes.Length <= 0)//no validation, do them all
            {
                Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
                int len = rends.Length;
                for (int i = 0; i < len; i++)
                {
                    updateRenderer(rends[i], shader, props);
                }
            }
            else//white-list, only do what is specified by meshes array
            {
                int len = meshes.Length;
                Transform tr;
                Renderer r;
                for (int i = 0; i < len; i++)
                {
                    tr = root.FindRecursive(meshes[i]);
                    if (tr == null) { continue; }
                    r = tr.GetComponent<Renderer>();
                    if (r == null) { continue; }
                    updateRenderer(r, shader, props);
                }
            }
        }

        public static void updateRenderer(Renderer rend, string shader, ShaderProperty[] props)
        {
            Material m = rend.material;
            if (!String.IsNullOrEmpty(shader))
            {
                Shader s = SSTUDatabase.getShader(shader);
                if (s != null && s != m.shader)
                {
                    m.shader = s;
                }
            }
            updateMaterialProperties(m, props);
        }

        public static void updateMaterialProperties(Material m, ShaderProperty[] props)
        {
            if (m == null || props == null || props.Length == 0) { return; }
            int len = props.Length;
            for (int i = 0; i < len; i++)
            {
                props[i].apply(m);
            }
        }
    }

    public abstract class ShaderProperty
    {
        public readonly string name;

        public static ShaderProperty[] parse(ConfigNode node)
        {
            List<ShaderProperty> props = new List<ShaderProperty>();
            //direct property nodes
            ConfigNode[] propNodes = node.GetNodes("PROPERTY");
            int len = propNodes.Length;
            for (int i = 0; i < len; i++)
            {
                if (propNodes[i].HasValue("texture"))
                {
                    props.Add(new ShaderPropertyTexture(propNodes[i]));
                }
                else if (propNodes[i].HasValue("color"))
                {
                    props.Add(new ShaderPropertyColor(propNodes[i]));
                }
                else if (propNodes[i].HasValue("float"))
                {
                    props.Add(new ShaderPropertyFloat(propNodes[i]));
                }
            }
            //simply/lazy texture assignments
            string[] textures = node.GetStringValues("texture");
            len = textures.Length;
            string[] splits;
            string name, tex;
            bool main, nrm;
            for (int i = 0; i < len; i++)
            {
                splits = textures[i].Split(',');
                name = splits[0].Trim();
                tex = splits[1].Trim();
                main = splits[0] == "_MainTex";
                nrm = splits[0] == "_BumpMap";
                props.Add(new ShaderPropertyTexture(name, tex, main, nrm));
            }
            return props.ToArray();
        }

        protected ShaderProperty(ConfigNode node)
        {
            this.name = node.GetStringValue("name");
        }

        protected ShaderProperty(string name)
        {
            this.name = name;
        }

        public void apply(Material mat)
        {
            applyInternal(mat);
        }

        protected abstract void applyInternal(Material mat);

        protected bool checkApply(Material mat)
        {
            if (mat.HasProperty(name))
            {
                return true;
            }
            else
            {
                MonoBehaviour.print("Shader: " + mat.shader + " did not have property: " + name);
            }
            return false;
        }

    }

    public class ShaderPropertyColor : ShaderProperty
    {
        readonly Color color;

        public ShaderPropertyColor(ConfigNode node) : base(node)
        {
            color = node.getColor("color");
        }

        public ShaderPropertyColor(string name, Color color) : base(name)
        {
            this.color = color;
        }

        protected override void applyInternal(Material mat)
        {
            mat.SetColor(name, color);
        }
    }

    public class ShaderPropertyFloat : ShaderProperty
    {
        readonly float val;

        public ShaderPropertyFloat(ConfigNode node) : base(node)
        {
            val = node.GetFloatValue("float");
        }

        public ShaderPropertyFloat(string name, float val) : base(name)
        {
            this.val = val;
        }

        protected override void applyInternal(Material mat)
        {
            if (checkApply(mat))
            {
                mat.SetFloat(name, val);
            }
        }
    }

    public class ShaderPropertyTexture : ShaderProperty
    {
        readonly string textureName;
        readonly bool main;
        readonly bool normal;

        public ShaderPropertyTexture(ConfigNode node) : base(node)
        {
            textureName = node.GetStringValue("texture");
            main = node.GetBoolValue("main");
            normal = node.GetBoolValue("normal");
        }

        public ShaderPropertyTexture(string name, string texture, bool main, bool normal) : base(name)
        {
            this.textureName = texture;
            this.main = main;
            this.normal = normal;
        }

        protected override void applyInternal(Material mat)
        {
            if (checkApply(mat))
            {
                if (main)
                {
                    mat.mainTexture = GameDatabase.Instance.GetTexture(textureName, false);
                }
                else
                {
                    mat.SetTexture(name, GameDatabase.Instance.GetTexture(textureName, normal));
                }
            }
        }
    }
}
