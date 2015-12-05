using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{

    
    public class SSTUModularParachute : PartModule
    {
        /*
        Parachute setup:

        Need to find way to re-use a single parachute model, perhaps doing the animation manually?
        --Would require defining the difference in scale stuff between a drogue and full parachute
        --Could even have all of the caps defined in config nodes

        DROGUECHUTE
        {
            name = Generic //reference to definition for the model (height, diameter, drag stats)
            localPosition = 0, 1, 0 //position in model
            localRotation = 0, 0, 0 //rotation of the model
            retractedScale = 0.001, 0.001, 0.001
            deployedScale = 0.5, 1.5, 0.5
            deploySpeed = 1
        }
        MAINCHUTE
        {        
            name = Generic //reference to definition for the model (height, diameter, drag stats)
            localPosition = 0, 1, 0 //position in model
            localRotation = 0, 0, 0 //rotation of the model
            retractedScale = 0.001, 0.001, 0.001
            deployedScale = 1, 1, 1
            deploySpeed = 1
        }

        SSTU_PARACHUTE
        {
            name = Generic
            height = 30
            diameter = 20            
        }

        OnLoad() -

        OnStart() -

        FixedUpdate -    
    
        */

        [Persistent]
        public String configNodeData = String.Empty;

        private bool initialized = false;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { configNodeData = node.ToString(); }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
        }

        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
        }

        private void restoreModels()
        {

        }
    }

    public class SSTUParachuteDefinition
    {
        public SSTUParachuteDefinition(ConfigNode node)
        {

        }
    }
}
