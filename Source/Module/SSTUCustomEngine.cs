using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	public class SSTUCustomEngine : PartModule, IPartCostModifier
	{

		public float GetModuleCost(float input){return input;}
	}
	
	public class EngineDefinition
	{
		
	}
}

