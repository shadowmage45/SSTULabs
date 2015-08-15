using System;
using UnityEngine;
namespace SSTUTools
{
	public static class SSTUExtensions
	{		
		public static string GetStringValue(this ConfigNode node, String name)
		{
			String value = node.GetValue(name);
			return value==null? "" : value;
		}
		public static bool GetBoolValue(this ConfigNode node, String name)
		{
			String value = node.GetValue(name);
			if(value==null){return false;}
			try
			{
				return bool.Parse(value);	
			}
			catch(Exception e)
			{
				MonoBehaviour.print(e.Message);
			}
			return false;
		}
		
		public static float GetFloatValue(this ConfigNode node, String name)
		{
			String value = node.GetValue(name);
			if(value==null){return 0f;}
			try
			{
				return float.Parse(value);	
			}
			catch(Exception e)
			{
				MonoBehaviour.print(e.Message);
			}
			return 0f;
		}
		
		public static double GetDoubleValue(this ConfigNode node, String name)
		{
			String value = node.GetValue(name);
			if(value==null){return 0d;}
			try
			{
				return double.Parse(value);	
			}
			catch(Exception e)
			{
				MonoBehaviour.print(e.Message);
			}
			return 0d;
		}
		
		public static int GetIntValue(this ConfigNode node, String name)
		{
			String value = node.GetValue(name);
			if(value==null){return 0;}
			try
			{
				return int.Parse(value);	
			}
			catch(Exception e)
			{
				MonoBehaviour.print(e.Message);
			}
			return 0;
		}
	}
}

