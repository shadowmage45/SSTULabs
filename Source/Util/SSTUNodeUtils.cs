using System;
using System.Collections.Generic;
using UnityEngine;
namespace SSTUTools
{

    public class SSTUConfigNodeUtils
    {
        //input is the string output from ConfigNode.ToString()
        //any other input will result in undefined behavior		
        public static ConfigNode parseConfigNode(String input)
        {
            ConfigNode baseCfn = ConfigNode.Parse(input);
            if (baseCfn == null) { MonoBehaviour.print("Base config node was null!!\n" + input); }
            else if (baseCfn.nodes.Count <= 0) { MonoBehaviour.print("Base config node has no nodes!!\n" + input); }
            return baseCfn.nodes[0];
        }
    }
}

