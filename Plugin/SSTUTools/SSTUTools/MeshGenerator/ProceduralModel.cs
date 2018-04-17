using System;
using UnityEngine;
using KSPShaderTools;

namespace SSTUTools
{
    public class ProceduralModel
    {
        public String rootName = "PModel";
        public GameObject root;

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
        }
        
        public void recreateModel()
        {
            destroyModel();
            generateModel(root);
        }
        
        public void destroyModel()
        {
            SSTUUtils.destroyChildren(root.transform);
        }

        public void enableTextureSet(string name, RecoloringData[] userColors)
        {
            TextureSet s = TexturesUnlimitedLoader.getTextureSet(name);
            if (s != null)
            {
                s.enable(root.transform, userColors);
            }
        }
    }
}

