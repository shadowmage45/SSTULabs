using System;

namespace SSTUTools
{
    public class TechLimit
    {
        public static void updateTechLimits(String setName, out float maxDiameter)
        {
            maxDiameter = float.PositiveInfinity;
            if (!SSTUUtils.isResearchGame()) { return; }
            if (HighLogic.CurrentGame == null) { return; }
            maxDiameter = 0;
            ConfigNode[] setNodes = GameDatabase.Instance.GetConfigNodes("TECHLIMITSET");
            int len = setNodes.Length;
            for (int i = 0; i < len; i++)
            {
                if (setNodes[i].GetStringValue("name") == setName)//found the specified limit-set-name
                {                    
                    ConfigNode[] limitNodes = setNodes[i].GetNodes("TECHLIMIT");
                    int setLen = limitNodes.Length;
                    float d;
                    for (int k = 0; k < setLen; k++)
                    {
                        if (SSTUUtils.isTechUnlocked(limitNodes[k].GetStringValue("name")))
                        {
                            d = limitNodes[k].GetFloatValue("diameter");
                            if (d > maxDiameter) { maxDiameter = d; }
                        }
                    }
                    break;
                }
            }
        }
    }
}
