using System;
using System.Collections.Generic;
using UnityEngine;
namespace SSTUTools
{
	public class FairingPanel
	{
		public GameObject panel;
		public Quaternion baseOrientation;
		
		public FairingPanel(GameObject go)
		{
			panel = go;
			baseOrientation = new Quaternion (go.transform.localRotation.x, go.transform.localRotation.y, go.transform.localRotation.z, go.transform.localRotation.w);
		}
		
		//rotates panel around the x-axis
		public void setRotation(float rotation)
		{
			Quaternion newAngle;
			Quaternion xRotation = Quaternion.AngleAxis(rotation, new Vector3(1,0,0));
			newAngle = baseOrientation * xRotation;
			panel.transform.localRotation = newAngle;
		}
		
		//enable/disable the mesh collider for the panel
		public void enableCollider(bool enabled, bool convex)
		{
			MeshCollider mc = panel.GetComponent<MeshCollider> ();//attempt to get any existing mesh collider
			if (mc == null)
			{
				mc = panel.AddComponent<MeshCollider>();//create a new mesh collider if one did not exist for some reason
			}
			mc.enabled = enabled;
			mc.convex = convex;			
		}
	}
}

