using System;
using UnityEngine;
namespace SSTUTools
{
	public static class SSTUExtensions
	{		
		public static string GetStringValue(this ConfigNode node, String name, String defaultValue)
		{
			String value = node.GetValue(name);
			return value==null? defaultValue : value;
		}

		public static string GetStringValue(this ConfigNode node, String name)
		{
			return GetStringValue (node, name, "");
		}

		public static bool GetBoolValue(this ConfigNode node, String name, bool defaultValue)
		{
			String value = node.GetValue(name);
			if(value==null){return defaultValue;}
			try
			{
				return bool.Parse(value);	
			}
			catch(Exception e)
			{
				MonoBehaviour.print(e.Message);
			}
			return defaultValue;
		}

		public static bool GetBoolValue(this ConfigNode node, String name)
		{
			return GetBoolValue (node, name, false);
		}

		public static float GetFloatValue(this ConfigNode node, String name, float defaultValue)
		{
			String value = node.GetValue(name);
			if(value==null){return defaultValue;}
			try
			{
				return float.Parse(value);	
			}
			catch(Exception e)
			{
				MonoBehaviour.print(e.Message);
			}
			return defaultValue;
		}
		
		public static float GetFloatValue(this ConfigNode node, String name)
		{
			return GetFloatValue (node, name, 0);
		}

		public static double GetDoubleValue(this ConfigNode node, String name, double defaultValue)
		{
			String value = node.GetValue(name);
			if(value==null){return defaultValue;}
			try
			{
				return double.Parse(value);	
			}
			catch(Exception e)
			{
				MonoBehaviour.print(e.Message);
			}
			return defaultValue;
		}
		
		public static double GetDoubleValue(this ConfigNode node, String name)
		{
			return GetDoubleValue (node, name, 0);
		}	

		public static int GetIntValue(this ConfigNode node, String name, int defaultValue)
		{
			String value = node.GetValue(name);
			if(value==null){return defaultValue;}
			try
			{
				return int.Parse(value);	
			}
			catch(Exception e)
			{
				MonoBehaviour.print(e.Message);
			}
			return defaultValue;
		}
		
		public static int GetIntValue(this ConfigNode node, String name)
		{
			return GetIntValue(node, name, 0);
		}

		//TODO
		#warning NEED TO WRITE THIS BLOCK OF CODE...
		public static Vector3 GetVector3(this ConfigNode node, String name, Vector3 defaultValue)
		{
			return defaultValue;
		}

		public static Vector3 GetVector3(this ConfigNode node, String name)
		{
			return GetVector3 (node, name, Vector3.zero);
		}
	}
}

