using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    class SSTUModularBooster : PartModule
    {
        /* Module setup / function
        Modular Booster -
        Single part booster (radial or stack attached)
        With integrated
          * fuel capacity (single fuel type)
          * jettison motors (switches from vertical to radial depending upon setup)
          * booster motors for SRBs

        Adjustable parameters:
          * Diameter
          * Height (number of segments for SRBs)
          * Nosecone
          * Motor/Mount

        Career Tech limitations for:
          * Initial part unlock
          * Diameters unlock at specified nodes

        ? Model(s) should be initialized through MODEL nodes
        ? Model(s) should be a pre-built model for each length variant (aka ModularFuelTanks setup?) -- would allow re-use of the existing fuel-tank modules for LFBs
        ? Model(s) should be entirely

        */

        #region REGION - KSP Config Variables

        /// <summary>
        /// transforms will be added to the base part model hierarchy based on numberOfEngines.<para></para>
        /// These will only be added during the initial part prefab setup.
        /// </summary>
        [KSPField]
        public String thrustTransformName = String.Empty;

        /// <summary>
        /// Transforms of this name will be added to the model hierarchy for use as jettison motors.  <para></para>
        /// The location and orientation of these transforms will vary depending upon if the part is setup for radial or stack attachment.
        /// </summary>
        [KSPField]
        public String jettisonTransformName = String.Empty;

        /// <summary>
        /// If true, the engine thrust will be scaled with model changes by the parameters below
        /// </summary>
        [KSPField]
        public bool scaleMotorThrust = true;

        /// <summary>
        /// Defaults to using cubic scaling for engine thrust, to maintain constant burn time for scaling vs. resource quantity.
        /// </summary>
        [KSPField]
        public float thrustScalePower = 3;

        /// <summary>
        /// If true, resources will be scaled with model changes by the parameters below
        /// </summary>
        [KSPField]
        public bool scaleResources = true;

        /// <summary>
        /// Determines the scaling power of resources scaling.  Default is cubic scaling, as this represents how volume scales with diameter changes.
        /// </summary>
        [KSPField]
        public float resourceScalePower = 3;

        [KSPField]
        public float minDiameter = 0.625f;

        [KSPField]
        public float maxDiameter = 10f;

        [KSPField]
        public float diameterIncrement = 0.625f;

        [KSPField]
        public bool useRF = false;

        #endregion REGION - KSP Config Variables

        #region REGION - Persistent Variables
        
        [KSPField(isPersistant = true)]
        public String currentNoseName;

        [KSPField(isPersistant = true)]
        public String currentMountName;

        [KSPField(isPersistant = true)]
        public String currentMainName;

        [KSPField(isPersistant = true)]
        public String currentFuelType;

        [KSPField(isPersistant = true)]
        public float currentDiameter = 2.5f;

        //do NOT adjust this through config, or you will mess up your resource updates in the editor; you have been warned
        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        #endregion ENDREGION - Persistent variables

        #region REGION - Private working variables

        private FuelTypeData[] fuelTypes;
        private FuelTypeData fuelTypeData;

        private SingleModelData[] noseModules;
        private SingleModelData[] mountModules;
        private SingleModelData[] mainModules;

        private SingleModelData currentMainModule;
        private SingleModelData currentNoseModule;
        private SingleModelData currentMountModule;

        [Persistent]
        public String configNodeData;

        #endregion ENDREGION - Private working variables

        #region REGION - KSP GUI Interaction Methods

        /// <summary>
        /// Called when user presses the increase diameter button in editor
        /// </summary>
        public void nextDiameterEvent()
        {

        }

        /// <summary>
        /// Called when user presses the decrease diameter button in editor
        /// </summary>
        public void prevDiameterEvent()
        {

        }

        public void nextMainModelEvent()
        {

        }

        public void previousMainModelEvent()
        {

        }

        /// <summary>
        /// Called when user changes between radial and stack attachment setups.  Should initialize repositioning of the jettison transforms according to model setup.
        /// </summary>
        public void radialStackChangeEvent()
        {

        }

        #endregion ENDREGION - KSP GUI Interaction Methods

        #region REGION - Standard KSP Overrides

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasNode("NOSE") || node.HasNode("MOUNT") || node.HasNode ("MAINMODEL") || node.HasNode("FUELTYPE"))
            {
                configNodeData = node.ToString();
            }
            initialize();
        }

        public void Start()
        {

        }

        #endregion ENDREGION - Standard KSP Overrides

        #region REGION - Initialization Methods

        private void initialize()
        {
            loadConfigNodeData();
            
        }

        private void loadConfigNodeData()
        {
            ConfigNode node = SSTUNodeUtils.parseConfigNode(configNodeData);
            //load all fuel type datas
            fuelTypes = FuelTypeData.parseFuelTypeData(node.GetNodes("FUELTYPE"));
            //load all main tank model datas
            mainModules = SingleModelData.parseModels(node.GetNodes("MAINMODEL"));
            currentMainModule = Array.Find(mainModules, m => m.name == currentMainName);
            if (currentMainModule == null)
            {
                currentMainModule = mainModules[0];
                currentMainName = currentMainModule.name;
            }

            //load nose and mount modules from singular CAP data (adapted directly from MFT code)
            ConfigNode[] mountNodes = node.GetNodes("CAP");
            ConfigNode mountNode;
            int length = mountNodes.Length;
            List<CustomFuelTankMount> noseModules = new List<CustomFuelTankMount>();
            List<CustomFuelTankMount> mountModules = new List<CustomFuelTankMount>();
            for (int i = 0; i < length; i++)
            {
                mountNode = mountNodes[i];
                if (mountNode.GetBoolValue("useForNose", true))
                {
                    mountNode.SetValue("nose", "true", true);//add the nose variable to the mount config nodes, set to true, as these are the nose nodes
                    noseModules.Add(new CustomFuelTankMount(mountNode));
                }
                if (mountNode.GetBoolValue("useForMount", true))
                {
                    mountNode.SetValue("nose", "false", true);//add the nose variable to the mount config nodes, set to true, as these are the mount nodes
                    mountModules.Add(new CustomFuelTankMount(mountNode));
                }
            }
            this.noseModules = noseModules.ToArray();
            this.mountModules = mountModules.ToArray();
            currentNoseModule = Array.Find(this.noseModules, m => m.name == currentNoseName);
            if (currentNoseModule == null)
            {
                currentNoseModule = noseModules[0];//not having a mount defined is an error, at least one mount must be defined, crashing at this point is acceptable
                currentNoseName = currentNoseModule.name;
            }
            currentMountModule = Array.Find(this.mountModules, m => m.name == currentMountName);
            if (currentMountModule == null)
            {
                currentMountModule = mountModules[0];//not having a mount defined is an error, at least one mount must be defined, crashing at this point is acceptable
                currentMountName = currentMountModule.name;
            }

            //load fuel type, with error-handling for missing fuel defs
            FuelTypeData fuelType = Array.Find(fuelTypes, m => m.name == currentFuelType);
            if (fuelType == null)
            {
                fuelType = fuelTypes[0];//not having a fuel defined is a setup error. At least one fuel must be defined, crashing at this point is acceptable
                currentFuelType = fuelType.name;
            }            
            fuelTypeData = fuelType;

            SingleModelData mount = Array.Find(this.mountModules, m => m.name == currentMountName);
            SingleModelData nose = Array.Find(this.noseModules, m => m.name == currentNoseName);
            SingleModelData main = Array.Find(this.mainModules, m => m.name == currentMainName);

        }

        #endregion ENDREGION - Initialization Methods
    }
}
