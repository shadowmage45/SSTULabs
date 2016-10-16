using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SSTUTools
{
    public static class SSTUDatabase
    {

        public static void loadConfigData()
        {
            loadHeatShieldTypes();
            loadShaders();
        }
        
        #region REGION - Modular Heat Shield data
        private static List<HeatShieldType> heatShieldTypesList = new List<HeatShieldType>();
        private static Dictionary<String, HeatShieldType> heatShieldTypesMap = new Dictionary<string, HeatShieldType>();
        private static Dictionary<String, Shader> shaderDict = new Dictionary<string, Shader>();

        private static void loadHeatShieldTypes()
        {
            heatShieldTypesMap.Clear();
            heatShieldTypesList.Clear();
            HeatShieldType shield;
            ConfigNode[] heatShieldNodes = GameDatabase.Instance.GetConfigNodes("SSTU_HEATSHIELD");
            int len = heatShieldNodes.Length;
            for (int i = 0; i < len; i++)
            {
                shield = new HeatShieldType(heatShieldNodes[i]);
                heatShieldTypesMap.Add(shield.name, shield);
                heatShieldTypesList.Add(shield);
            }
        }

        public static HeatShieldType getHeatShieldType(String name) { return heatShieldTypesMap[name]; }

        public static string[] getHeatShieldNames(){return heatShieldTypesList.Select(m => m.name).ToArray();}

        private static void loadShaders()
        {
            shaderDict.Clear();
            MonoBehaviour.print("loading asset bundle shaders");
            SSTUAssetBundleShaderLoader.Load(shaderDict);
        }

        public static Shader getShader(string name)
        {
            Shader shader = null;
            if (!shaderDict.TryGetValue(name, out shader))
            {
                shader = Shader.Find(name);
            }
            return shader;
        }

        #endregion
                
    }
}
