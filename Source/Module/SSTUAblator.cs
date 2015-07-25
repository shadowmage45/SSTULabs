using System;
using System.Collections.Generic;
using UnityEngine;
namespace SSTUTools
{	
	//	custom re-implementation of heat shield ablator module
	//	customized to allow for only shading specific mesh objects rather than the entire part
	//	which should in turn allow for a much more accurate representation of the heat shield
	//	on a command pod.
	//  code based on: https://github.com/NathanKell/DeadlyReentry/blob/master/Source/DeadlyReentry.cs
	public class SSTUAblator : PartModule
	{

		#region KSP PartModule config fields
		//standard vars from stock ModuleAblator

		[KSPField]
		public float lossConst = 1.0f;
		
		[KSPField]
		public float pyrolysisLossFactor = 10.0f;
		
		[KSPField]
		public string ablativeResource = string.Empty;
		
		[KSPField]
		public float lossExp;
		
		[KSPField]
		public float ablationTempThresh = 600.0f;
		
		[KSPField]
		public float charMax = 0.85f;
		
		[KSPField]
		public float charMin = 0;
		
		[KSPField]
		public float reentryConductivity = 0.01f;
		
		[KSPField]
		public float charAlpha = 0.8f;

		//the name of the mesh object to effect with the ablator shader
		[KSPField]
		public String meshNames = String.Empty;

		#endregion

		#region private working vars

		//quick-reference to the ablator resource for the part
		private PartResource ablatorResource;

		//quick-reference to the set of mesh renderers for the given mesh name and child meshes
		private Renderer[] partRenderers;

		//ordinal of the negative Y direction drag-cube face;
		//this face is used to determine heat-shield occlusion and update conduction factor
		private int dragFace = (int)DragCube.DragFace.YN;	

		private float ablatorTempReductionCapacity;

		private float partOriginalConductivity;

		private float ablatorLocalResourceDensity;

		private float ablatorInverseResourceDensity;

		private float ablatorLoss;

		private float thermalFlux;

		private int shaderReferenceConstant = Shader.PropertyToID("_BurnColor");

		#endregion

		public SSTUAblator ()
		{

		}

		#region KSP overrides

		public override void OnStart (StartState state)
		{
			base.OnStart (state);
			initResource ();									//find ablator resource in part
			initMeshes ();										//load mesh from mesh names specified in config
			updateHeatShieldColor ();							//update heat-shield color to appropriate color from stored ablator amount
		}
		
		public void FixedUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready)		//only update if in active flight scene and init was successful
			{
				return;											//early exit if not in flight scene
			}
			updateAblatorLoss();
			updateHeatShieldColor ();						//update heat shield material color every game tick
		}

		#endregion

		#region private update methods
		
		private void initMeshes()
		{
			String[] names = meshNames.Split (',');
			List<Renderer> renders = new List<Renderer> ();
			Renderer[] rendersArray;
			foreach (String name in names)
			{
				rendersArray = part.FindModelComponents<Renderer>(name);
				renders.AddRange(rendersArray);
			}
			partRenderers = renders.ToArray ();
		}

		private void initResource()
		{
			partOriginalConductivity = (float)part.heatConductivity;
			if (part.Resources.Contains (ablativeResource))
			{
				ablatorResource = part.Resources[ablativeResource];

				ablatorTempReductionCapacity = pyrolysisLossFactor * (float)ablatorResource.info.specificHeatCapacity;
				ablatorLocalResourceDensity = ablatorResource.info.density;
				ablatorInverseResourceDensity = 1.0f / ablatorLocalResourceDensity;

			}
		}

		private void updateAblatorLoss()
		{
			ablatorLoss = 0;
			thermalFlux = 0;

			if (ablatorResource == null)
			{
				return;
			}

			//sets current conductivity based on if lower face of heat-shield is occluded or not
			if ((double)part.DragCubes.AreaOccluded [dragFace] > 0.1 * (double)part.DragCubes.WeightedArea[dragFace])
			{
				part.heatConductivity = reentryConductivity;
			}
			else
			{
				part.heatConductivity = partOriginalConductivity;
			}

			if (part.skinTemperature > ablationTempThresh)
			{
				float currentAblatorAmount = (float)this.ablatorResource.amount;
				
				if (currentAblatorAmount > 0.0)
				{	
					ablatorLoss = lossConst * (float)Math.Exp (lossExp / part.skinTemperature);

					if (ablatorLoss > 0.0f)
					{		
						ablatorLoss *= currentAblatorAmount;
						ablatorResource.amount -= ablatorLoss * TimeWarp.fixedDeltaTime;						
						ablatorLoss *= ablatorLocalResourceDensity;
						thermalFlux = ablatorLoss * ablatorTempReductionCapacity;
						float tempReduction = (float)((double)thermalFlux * part.skinThermalMassRecip * part.skinExposedMassMult);
						part.skinTemperature = Math.Max (part.skinTemperature - tempReduction, PhysicsGlobals.SpaceTemperature);
					}					
				}
			}
		}

		private void updateHeatShieldColor()
		{
			if (partRenderers == null || partRenderers.Length == 0)
			{
				return;
			}
			float ablatorPercentage, charDelta, charAmount;
			ablatorPercentage = (float)ablatorResource.amount / (float)ablatorResource.maxAmount;
			charDelta = this.charMax - this.charMin;
			charAmount = this.charMin + (charDelta * ablatorPercentage);
			Color color = new Color(charAmount, charAmount, charAmount, charAlpha);
			for(int i = 0; i < partRenderers.Length; i++)
			{
				partRenderers[i].material.SetColor(shaderReferenceConstant, color);
			}
		}

		#endregion
	}
}

