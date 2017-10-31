using System;
using UnityEngine;
using KSPShaderTools;

namespace SSTUTools
{
    public class ProceduralModel
    {
        public String rootName = "PModel";
        public GameObject root;
        protected Material currentMaterial;

        protected bool meshColliderEnabled = false;
        protected bool meshColliderConvex = false;
        
        protected virtual void generateModel(GameObject root)
        {
            throw new NotImplementedException("Cannot call generateModel() on base ProceduralModel; must utilize sublcasses for implementation!");
        }
        
        public void setParent(Transform tr)
        {
            root.transform.NestToParent(tr);
        }
        
        public void createModel()
        {
            root = new GameObject(rootName);
            generateModel(root);
            updateModelMaterial();
        }
        
        public void recreateModel()
        {
            destroyModel();
            generateModel(root);
            updateModelMaterial();
        }
        
        public void destroyModel()
        {
            SSTUUtils.destroyChildren(root.transform);
        }
        
        public void setMaterial(Material mat)
        {
            currentMaterial = mat;
            updateModelMaterial();
        }

        public void enableTextureSet(string name, Color[] userColors)
        {
            TextureSet s = KSPShaderLoader.getTextureSet(name);
            if (s != null)
            {
                s.enable(root, userColors);
            }
        }
        
        protected void updateModelMaterial()
        {
            if (root != null)
            {
                SSTUUtils.setMaterialRecursive(root.transform, currentMaterial);
            }
        }
    }
}

