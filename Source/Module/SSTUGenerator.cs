using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	public class SSTUGenerator : PartModule
	{

		[KSPField]
		public String resourceName;
		[KSPField]
		public float resourceAmount;
		[KSPField]
		public bool halfLifeDecay;
		[KSPField]
		public float halfLifeYears;
		[KSPField]
		public int engineModuleIndex;

		//private config vars
		[SerializeField]
		private long lastUpdateTime;

		private int resourceID;

		public override void OnStart (StartState state)
		{
			base.OnStart (state);
		}

		public override void OnLoad (ConfigNode node)
		{
			base.OnLoad (node);
			if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
			{
				//prefabInit
			}
			else
			{
				//part is being reloaded from in-flight/editor/etc
			}
		}


	}
}

