using System;
using System.Collections.Generic;
using UnityEngine;
namespace SSTUTools
{
	//basic algorithm
	//parse raw input until you find the first non-empty, non-comment line;
	//if it contains a '=', it is a value, add to node as value
	//else it should be a sub-node name
	//parse until the first bracket is found, mark openbracketCount++
	//add lines to list for 'valueData' (may contain sub-nodes)
	//when openbracketCount==closedBracketCount, parse those strings as a new node, add to parent node as node
	//recurse above until done
	
	//Input data must conform to the following format
	//
	//0. node name
	//1. open bracket of base node
	//2 -> n-3. value data (inc. subnodes)
	//n-2 close bracket of base node
	//n-1 empty line
	//lines 2 -> n-3 must not contain any invalid data sequences (such as a value name that starts with an open bracket);
	//tab spacing will be stripped from all lines
	//value entries must conform to the format of "name = value" after being stripped of whitespace
	//
	//Any other formatting will result in undefined behavior
	//(including throwing any/all exceptions)
	//
	//this set of methods is intended to parse the string output from ConfigNode.ToString() and -not- directly parse data from a hand-written config file.
	public class SSTUNodeUtils
	{
		//input is the string output from ConfigNode.ToString()
		//any other input will result in undefined behavior		
		public static ConfigNode parseConfigNode(String input)
		{
			if(true)
			{
				ConfigNode baseCfn = ConfigNode.Parse(input);
				return baseCfn.nodes[0];
			}
			String[] lines = input.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);	
			List<String> baseLines = new List<String>();
			baseLines.AddRange(lines);
			String nodeName = baseLines[0];//grab first line (node name)
			baseLines.RemoveAt (0);//remove first line(node name)
			baseLines.RemoveAt (0);//remove second line(openBracket)
			baseLines.RemoveAt (baseLines.Count-1);//remove last line(emptyLine)
			baseLines.RemoveAt (baseLines.Count-1);//remove last line(closeBracket)
			//what remains is only the inner values for the base config node			
			//if I make these assumptions, it cleans up all following code substantially
			return parseConfigNodeLines(new ConfigNode(nodeName), baseLines);
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
			MonoBehaviour.print ("raw node string: "+data);
			ConfigNode reparsedNode = SSTUNodeUtils.parseConfigNode(data);
			MonoBehaviour.print ("new node: "+reparsedNode);
			
			ConfigNode stockReparsedNode = ConfigNode.Parse(data);
			MonoBehaviour.print ("stockNewNode: "+stockReparsedNode.nodes[0]);
		}
		
		private static ConfigNode parseConfigNodeLines(ConfigNode node, List<String> nodeData)
		{			
			if(node==null){node = new ConfigNode("UNKNOWN");}
			int len = nodeData.Count;
			String line;//current line being parsed			
			String childNodeName = string.Empty;
			bool nodeNameFound = false;
			int openCount = 0;			
			List<String> innerNodeData = new List<String>();
			for(int i = 0; i < len; i++)
			{
				line = nodeData[i].Split(new String[]{"//"}, StringSplitOptions.None)[0].Trim();//remove trailing comments (should not be there for ConfigNode based input)				
				if(line.TrimStart().StartsWith("{"))
				{
					if(!nodeNameFound){MonoBehaviour.print("NodeParserERROR - open bracket found without preceeding name");}
					if(openCount==0)//no un-closed brackets, this is a base child node, parse it directly
					{					
						innerNodeData.Clear();
					}
					else//must belong to another inner node
					{
						innerNodeData.Add (line);
					}
					openCount++;
					
				}
				else if(line.TrimStart().StartsWith("}"))//close bracket
				{
					if(openCount>0)//proper config node should never have this false
					{
						openCount--;//closed an inner bracket pair
						if(openCount==0)//just closed a direct child node, recurse parse it
						{
							if(!nodeNameFound)
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
							innerNodeData.Add (line);
						}
					}
					else
					{
						MonoBehaviour.print("NodeParseERROR - close bracket found before any node was opened");
					}					
				}
				else if(line.Contains("="))//use it as a value, either this. or inner node
				{
					if(openCount==0)
					{
						String[] data = line.Split('=');
						node.AddValue(data[0].Trim(), data[1].Trim());
					}
					else
					{
						innerNodeData.Add (line);
					}
					
				}
				else if(!string.IsNullOrEmpty(line))//likely an inner node name
				{
					if(openCount==0)//base inner node name
					{						
						nodeNameFound = true;
						childNodeName = line;												
					}
					else//belongs to an inner node
					{
						innerNodeData.Add (line);
					}
				}
			}
			if(openCount!=0)
			{
				MonoBehaviour.print ("NodeParserERROR - finished parsing with remaining open inner node references");
			}
			return node;
		}	
		
		
	}
}

