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

    public class TextureSet
    {
        public readonly String name;
        public readonly TextureSetMaterialData[] textureData;
        public readonly Color[] maskColors;

        public TextureSet(ConfigNode node)
        {
            name = node.GetStringValue("name");
            ConfigNode[] texNodes = node.GetNodes("TEXTURE");
            int len = texNodes.Length;
            textureData = new TextureSetMaterialData[len];
            for (int i = 0; i < len; i++)
            {
                textureData[i] = new TextureSetMaterialData(texNodes[i]);
            }
            if (node.HasNode("COLORS"))
            {
                ConfigNode colorsNode = node.GetNode("COLORS");
                Color c1 = loadColor(colorsNode.GetStringValue("mainColor"));
                Color c2 = loadColor(colorsNode.GetStringValue("secondColor"));
                Color c3 = loadColor(colorsNode.GetStringValue("detailColor"));
                maskColors = new Color[] { c1, c2, c3 };
            }
            else
            {
                //TODO -- what should the default value for 'no default colors' be?
                maskColors = new Color[0];
            }
        }

        private static Color loadColor(string value)
        {
            if (value.Contains(","))
            {
                return SSTUUtils.parseColorFromBytes(value);
            }
            PresetColor color = PresetColor.getColor(value);
            if (color != null) { return color.color; }
            return Color.white;
        }

        public void enable(GameObject root, Color[] userColors)
        {
            foreach (TextureSetMaterialData mtd in textureData)
            {
                mtd.enable(root, userColors);
            }
        }

        public static TextureSet[] parse(ConfigNode[] nodes)
        {
            int len = nodes.Length;
            TextureSet[] sets = new TextureSet[len];
            for (int i = 0; i < len; i++)
            {
                sets[i] = new TextureSet(nodes[i]);
            }
            return sets;
        }

        /// <summary>
        /// Loads full texture sets from a config node containing only the set name
        /// the full set is loaded from the global set of SSTU_TEXTURESETs
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        public static TextureSet[] loadGlobalTextureSets(ConfigNode[] nodes)
        {
            int len = nodes.Length;
            TextureSet[] sets = new TextureSet[len];
            for (int i = 0; i < len; i++)
            {
                sets[i] = getGlobalTextureSet(nodes[i].GetStringValue("name"));
            }
            return sets;
        }

        /// <summary>
        /// Retrieve a single SSTU_TEXTURESET by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static TextureSet getGlobalTextureSet(string name)
        {
            ConfigNode[] setNodes = GameDatabase.Instance.GetConfigNodes("SSTU_TEXTURESET");
            ConfigNode setNode = Array.Find(setNodes, m => m.GetStringValue("name") == name);
            return new TextureSet(setNode);
        }
    }

    public class TextureSetMaterialData
    {
        public readonly String shader;
        public readonly String[] meshNames;
        public readonly String[] excludedMeshes;
        public readonly ShaderProperty[] props;

        public TextureSetMaterialData(ConfigNode node)
        {
            shader = node.GetStringValue("shader");
            meshNames = node.GetStringValues("mesh");
            excludedMeshes = node.GetStringValues("excludeMesh");
            props = ShaderProperty.parse(node);
            List<ShaderProperty> ps = new List<ShaderProperty>();
            ps.AddRange(props);
            if (node.HasValue("diffuseTexture")) { ps.Add(new ShaderPropertyTexture("_MainTex", node.GetStringValue("diffuseTexture"), true, false)); }
            if (node.HasValue("normalTexture")) { ps.Add(new ShaderPropertyTexture("_BumpMap", node.GetStringValue("normalTexture"), false, true)); }
            if (node.HasValue("specularTexture")) { ps.Add(new ShaderPropertyTexture("_SpecMap", node.GetStringValue("specularTexture"), false, false)); }
            if (node.HasValue("aoTexture")) { ps.Add(new ShaderPropertyTexture("_AOMap", node.GetStringValue("aoTexture"), false, false)); }
            if (node.HasValue("emissiveTexture")) { ps.Add(new ShaderPropertyTexture("_Emissive", node.GetStringValue("emissiveTexture"), false, false)); }
            props = ps.ToArray();
        }

        public void enable(GameObject root, Color[] userColors)
        {
            List<ShaderProperty> ps = new List<ShaderProperty>();
            ps.AddRange(props);
            int len = userColors.Length;
            for (int i = 0; i < len; i++)
            {
                ps.Add(new ShaderPropertyColor("_MaskColor"+(i+1), userColors[i]));
            }            
            SSTUTextureUtils.updateModelMaterial(root.transform, excludedMeshes, meshNames, shader, ps.ToArray());
        }

        public string getPropertyValue(string name)
        {
            ShaderProperty prop = Array.Find(props, m => m.name == name);
            if (prop == null) { return string.Empty; }
            if (prop is ShaderPropertyTexture) { return ((ShaderPropertyTexture)prop).textureName; }
            if (prop is ShaderPropertyFloat) { return ((ShaderPropertyFloat)prop).val.ToString(); }
            if (prop is ShaderPropertyColor) { return ((ShaderPropertyColor)prop).color.ToString(); }
            return string.Empty;
        }

        public Material createMaterial(string name)
        {
            string shdName = string.IsNullOrEmpty(this.shader) ? "KSP/Diffuse" : this.shader;
            Shader shd = SSTUDatabase.getShader(shdName);
            Material mat = new Material(shd);
            mat.name = name;
            SSTUTextureUtils.updateMaterialProperties(mat, props);
            return mat;
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

        //protected abstract string getStringValue();

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
        public readonly Color color;

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
        public readonly float val;

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
        public readonly string textureName;
        public readonly bool main;
        public readonly bool normal;

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
