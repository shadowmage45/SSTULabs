using System;
using UnityEngine;

namespace SSTUTools
{
    public class TechLimit
    {
        public static void updateTechLimits(String setName, out float maxDiameter)
        {
            maxDiameter = float.PositiveInfinity;
            if (!SSTUUtils.isResearchGame()) { MonoBehaviour.print("Not a research game, exiting tech limit checks"); return; }
            if (HighLogic.CurrentGame == null) {MonoBehaviour.print("current game is null, exiting tech limit checks"); return; }
            maxDiameter = 0;
            ConfigNode[] setNodes = GameDatabase.Instance.GetConfigNodes("TECHLIMITSET");
            int len = setNodes.Length;
            string techName;
            for (int i = 0; i < len; i++)
            {
                if (setNodes[i].GetStringValue("name") == setName)//found the specified limit-set-name
                {                    
                    ConfigNode[] limitNodes = setNodes[i].GetNodes("TECHLIMIT");
                    int setLen = limitNodes.Length;
                    float d;
                    for (int k = 0; k < setLen; k++)
                    {
                        techName = limitNodes[k].GetStringValue("name");
                        MonoBehaviour.print("examining tech node: " + techName);
                        if (SSTUUtils.isTechUnlocked(limitNodes[k].GetStringValue("name")))
                        {
                            MonoBehaviour.print("tech is unlocked");
                            d = limitNodes[k].GetFloatValue("diameter");
                            if (d > maxDiameter) { maxDiameter = d; }
                        }
                        else
                        {
                            MonoBehaviour.print("tech is not unlocked");
                        }
                    }
                    break;
                }
            }
        }
    }
}
