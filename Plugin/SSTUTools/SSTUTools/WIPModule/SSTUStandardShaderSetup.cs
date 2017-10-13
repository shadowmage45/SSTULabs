using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUStandardShaderSetup : PartModule
    {
        [KSPField]
        public string diffuseTexture = string.Empty;
        [KSPField]
        public string metalTexture = string.Empty;
        [KSPField]
        public string normalTexture = string.Empty;
                

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            init();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            init();
        }

        private void init()
        {
            Shader standardShader = Shader.Find("Standard");
            Material standardMat = new Material(standardShader);
            standardMat.SetTexture("_MainTex", SSTUUtils.findTexture(diffuseTexture, false));
            standardMat.SetTexture("_MetallicGlossMap", SSTUUtils.findTexture(metalTexture, false));
            standardMat.SetTexture("_BumpMap", SSTUUtils.findTexture(normalTexture, true));
            standardMat.EnableKeyword("_NORMALMAP");
            standardMat.EnableKeyword("_METALLICGLOSSMAP");

            Transform tr = part.transform.FindRecursive("model");
            updateTransforms(tr, standardMat);   
        }

        private void updateTransforms(Transform root, Material mat)
        {
            Renderer r = root.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = mat;
            }
            foreach (Transform child in root) { updateTransforms(child, mat); }
        }

    }
}
