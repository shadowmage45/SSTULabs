using System;
using UnityEngine;

namespace SSTUTools
{
	public class SSTUTransformHack : PartModule
	{
		
		[KSPField]
		public string newTransformName = "NewTransform";
		[KSPField]
		public Vector3 newTransformPos = new Vector3(0,0,0);
		[KSPField]
		public Vector3 newTransformZAxis = new Vector3(0,0,1);
		[KSPField]
		public Vector3 newTransformYAxis = new Vector3(0,1,1);
		
		public SSTUTransformHack ()
		{
		}
		
		public override void OnStart (PartModule.StartState state)
		{
			base.OnStart (state);
		}
	}
}

