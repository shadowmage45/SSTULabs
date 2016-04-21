
using System;
using UnityEngine;
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
        
        public void setMainTexture(String texName)
        {
            if (currentMaterial == null) { MonoBehaviour.print("ERROR: Material was null when trying to set diffuse texture: "+texName); }
            currentMaterial.mainTexture = GameDatabase.Instance.GetTexture(texName, false);
            updateModelMaterial();
        }
        
        public void setNormalTexture(String texName)
        {
            if (currentMaterial == null) { MonoBehaviour.print("ERROR: Material was null when trying to set normal texture: " + texName); }
            if (String.IsNullOrEmpty(texName))
            {
                currentMaterial.SetTexture("_BumpMap", null);
            }
            else
            {
                currentMaterial.SetTexture("_BumpMap", GameDatabase.Instance.GetTexture(texName, false));
            }
            updateModelMaterial();
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

