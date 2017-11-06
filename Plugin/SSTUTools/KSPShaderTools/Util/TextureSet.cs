using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KSPShaderTools
{
    public class TextureSet
    {
        //the registered name of this texture set -- MUST be unique (for global sets), or name collisions will occur.
        public readonly String name;
        //the display-title of this texture set, can be non-unique (but for UI purposes should be unique within a given part)
        public readonly string title;
        //the list of mesh->material assignments; each material data contains a list of meshes/excluded-meshes, along with the shaders and textures to apply to each mesh
        public readonly TextureSetMaterialData[] textureData;
        //default mask colors for this texture set
        public readonly RecoloringData[] maskColors;

        public readonly bool supportsRecoloring;
        public readonly int recolorableChannelMask;//1 = main, 2 = secondary, 4 = detail
        public readonly int featureMask;//1 = color, 2 = specular, 4 = metallic, 8 = hardness

        public TextureSet(ConfigNode node)
        {
            name = node.GetStringValue("name");
            title = node.GetStringValue("title", name);
            ConfigNode[] texNodes = node.GetNodes("TEXTURE");
            int len = texNodes.Length;
            textureData = new TextureSetMaterialData[len];
            for (int i = 0; i < len; i++)
            {
                textureData[i] = new TextureSetMaterialData(texNodes[i]);
            }
            supportsRecoloring = node.GetBoolValue("recolorable", false);
            recolorableChannelMask = node.GetIntValue("channelMask", 1 | 2 | 4);
            featureMask = node.GetIntValue("featureMask", 1 | 2 | 4);
            if (node.HasNode("COLORS"))
            {
                ConfigNode colorsNode = node.GetNode("COLORS");
                RecoloringData c1 = new RecoloringData(colorsNode.GetStringValue("mainColor"));
                RecoloringData c2 = new RecoloringData(colorsNode.GetStringValue("secondColor"));
                RecoloringData c3 = new RecoloringData(colorsNode.GetStringValue("detailColor"));
                maskColors = new RecoloringData[] { c1, c2, c3 };
            }
            else
            {
                maskColors = new RecoloringData[3];
                Color white = Color.white;
                maskColors[0] = new RecoloringData(white, 0, 0);
                maskColors[1] = new RecoloringData(white, 0, 0);
                maskColors[2] = new RecoloringData(white, 0, 0);
            }
        }

        public void enable(GameObject root, RecoloringData[] userColors)
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
                Transform[] trs;
                Transform tr;
                Renderer r;
                for (int i = 0; i < len; i++)
                {
                    trs = root.FindChildren(meshes[i]);
                    int len2 = trs.Length;
                    for (int k = 0; k < len2; k++)
                    {
                        tr = trs[k];
                        if (tr == null)
                        {
                            continue;
                        }
                        r = tr.GetComponent<Renderer>();
                        if (r == null)
                        {
                            continue;
                        }
                        updateRenderer(r, shader, props);
                    }
                }
            }
        }

        public static void updateRenderer(Renderer rend, string shader, ShaderProperty[] props)
        {
            updateMaterial(rend.material, shader, props);
        }

        public static void updateMaterial(Material mat, string shader, ShaderProperty[] props)
        {
            if (!String.IsNullOrEmpty(shader))
            {
                Shader s = KSPShaderLoader.getShader(shader);
                if (s != null && s != mat.shader)
                {
                    mat.shader = s;
                }
            }
            updateMaterialProperties(mat, props);
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
        
        public static string[] getTextureSetNames(ConfigNode[] nodes)
        {
            List<string> names = new List<string>();
            string name;
            TextureSet set;
            int len = nodes.Length;
            for (int i = 0; i < len; i++)
            {
                name = nodes[i].GetStringValue("name");
                set = KSPShaderLoader.getTextureSet(name);
                if (set != null) { names.Add(set.name); }
            }
            return names.ToArray();
        }

        public static string[] getTextureSetTitles(ConfigNode[] nodes)
        {
            List<string> names = new List<string>();
            string name;
            TextureSet set;
            int len = nodes.Length;
            for (int i = 0; i < len; i++)
            {
                name = nodes[i].GetStringValue("name");
                set = KSPShaderLoader.getTextureSet(name);
                if (set != null) { names.Add(set.title); }
            }
            return names.ToArray();
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
        }

        public void enable(GameObject root, RecoloringData[] userColors)
        {
            List<ShaderProperty> ps = new List<ShaderProperty>();
            ps.AddRange(props);
            int len = userColors.Length;            
            for (int i = 0; i < len; i++)
            {
                ps.Add(new ShaderPropertyColor("_MaskColor" + (i + 1), userColors[i].getShaderColor()));
            }
            Color metallicInput = new Color();
            if (len > 0) { metallicInput.r = userColors[0].metallic; }            
            if (len > 1) { metallicInput.g = userColors[1].metallic; }
            if (len > 2) { metallicInput.b = userColors[2].metallic; }            
            ps.Add(new ShaderPropertyColor("_MaskMetallic", metallicInput));
            TextureSet.updateModelMaterial(root.transform, excludedMeshes, meshNames, shader, ps.ToArray());
        }

        public void enable(Material mat, RecoloringData[] userColors)
        {
            List<ShaderProperty> ps = new List<ShaderProperty>();
            ps.AddRange(props);
            int len = userColors.Length;
            for (int i = 0; i < len; i++)
            {
                ps.Add(new ShaderPropertyColor("_MaskColor" + (i + 1), userColors[i].getShaderColor()));
            }
            Color metallicInput = new Color();
            if (len > 0) { metallicInput.r = userColors[0].metallic; }
            if (len > 1) { metallicInput.g = userColors[1].metallic; }
            if (len > 2) { metallicInput.b = userColors[2].metallic; }
            ps.Add(new ShaderPropertyColor("_MaskMetallic", metallicInput));
            TextureSet.updateMaterial(mat, shader, ps.ToArray());
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
            Shader shd = KSPShaderLoader.getShader(shdName);
            Material mat = new Material(shd);
            mat.name = name;
            TextureSet.updateMaterialProperties(mat, props);
            return mat;
        }
    }

}
