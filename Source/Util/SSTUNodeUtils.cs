using System;
using System.Collections.Generic;
using UnityEngine;
namespace SSTUTools
{

    public class SSTUNodeUtils
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

        public static void test()
        {
            ConfigNode testNode = new ConfigNode("TESTNODE");
            testNode.AddValue("fooName", "fooValue");

            ConfigNode innerTestNode = new ConfigNode("INNERTESTNODE");
            ConfigNode innerInnerTestNode = new ConfigNode("SUPERINNERTESTNODE");
            innerInnerTestNode.AddValue("superFooName", "superFooValue");
            innerTestNode.AddValue("innerFooName", "innerFooValue");
            innerTestNode.AddNode(innerInnerTestNode);
            testNode.AddNode(innerTestNode);

            innerTestNode = new ConfigNode("INNERTESTNODE2");
            innerTestNode.AddValue("innerFooName2", "innerFooValue2");
            innerTestNode.AddValue("innerFooName2-2", "innerFooValue2-2");
            testNode.AddNode(innerTestNode);


            String data = testNode.ToString();
            MonoBehaviour.print("raw node string: " + data);
            ConfigNode reparsedNode = SSTUNodeUtils.parseConfigNode(data);
            MonoBehaviour.print("new node: " + reparsedNode);

            ConfigNode stockReparsedNode = ConfigNode.Parse(data);
            MonoBehaviour.print("stockNewNode: " + stockReparsedNode.nodes[0]);
        }

        private static ConfigNode parseConfigNodeLines(ConfigNode node, List<String> nodeData)
        {
            if (node == null) { node = new ConfigNode("UNKNOWN"); }
            int len = nodeData.Count;
            String line;//current line being parsed			
            String childNodeName = string.Empty;
            bool nodeNameFound = false;
            int openCount = 0;
            List<String> innerNodeData = new List<String>();
            for (int i = 0; i < len; i++)
            {
                line = nodeData[i].Split(new String[] { "//" }, StringSplitOptions.None)[0].Trim();//remove trailing comments (should not be there for ConfigNode based input)				
                if (line.TrimStart().StartsWith("{"))
                {
                    if (!nodeNameFound) { MonoBehaviour.print("NodeParserERROR - open bracket found without preceeding name"); }
                    if (openCount == 0)//no un-closed brackets, this is a base child node, parse it directly
                    {
                        innerNodeData.Clear();
                    }
                    else//must belong to another inner node
                    {
                        innerNodeData.Add(line);
                    }
                    openCount++;

                }
                else if (line.TrimStart().StartsWith("}"))//close bracket
                {
                    if (openCount > 0)//proper config node should never have this false
                    {
                        openCount--;//closed an inner bracket pair
                        if (openCount == 0)//just closed a direct child node, recurse parse it
                        {
                            if (!nodeNameFound)
                            {
                                childNodeName = "UNKNOWN";
                            }
                            ConfigNode innerNode = new ConfigNode(childNodeName);
                            parseConfigNodeLines(innerNode, innerNodeData);
                            node.AddNode(innerNode);
                            //reset inner node vars..
                            innerNodeData.Clear();
                            nodeNameFound = false;
                            childNodeName = string.Empty;
                        }
                        else
                        {
                            innerNodeData.Add(line);
                        }
                    }
                    else
                    {
                        MonoBehaviour.print("NodeParseERROR - close bracket found before any node was opened");
                    }
                }
                else if (line.Contains("="))//use it as a value, either this. or inner node
                {
                    if (openCount == 0)
                    {
                        String[] data = line.Split('=');
                        node.AddValue(data[0].Trim(), data[1].Trim());
                    }
                    else
                    {
                        innerNodeData.Add(line);
                    }

                }
                else if (!string.IsNullOrEmpty(line))//likely an inner node name
                {
                    if (openCount == 0)//base inner node name
                    {
                        nodeNameFound = true;
                        childNodeName = line;
                    }
                    else//belongs to an inner node
                    {
                        innerNodeData.Add(line);
                    }
                }
            }
            if (openCount != 0)
            {
                MonoBehaviour.print("NodeParserERROR - finished parsing with remaining open inner node references");
            }
            return node;
        }


    }
}

