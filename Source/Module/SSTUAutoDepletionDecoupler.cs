using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools.WIPModule
{

    public class SSTUAutoDepletionDecoupler : ModuleDecouple
    {
        [KSPField]
        public String resourceName;

        [KSPField]
        public float decoupleDelay = 0;

        [KSPField]
        public float activationDelay = 0;

        [KSPField]
        public float resourceMin = 0.005f;
        
        private float remainingDelay = 0;
        private float remainingEngDelay = 0;
        private PartResource resource;
        private MultiModeEngine engineSwitch;

        [KSPAction("Jettison")]
        public void jettisonAction(KSPActionParam param)
        {
            activateDecouple();
        }

        [KSPEvent(guiName = "Jettison", guiActive = true, guiActiveUncommand = true, guiActiveUnfocused = true)]
        public void jettisonEvent()
        {
            activateDecouple();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            resource = part.Resources[resourceName];
            if (isDecoupled)
            {
                Events["jettisonEvent"].active = false;
                Actions["jettisonAction"].active = false;
            }            
        }

        public void Start()
        {
            engineSwitch = part.GetComponent<MultiModeEngine>();
        }

        public void FixedUpdate()
        {            
            if (HighLogic.LoadedSceneIsFlight && resource!=null)
            {
                if (remainingEngDelay > 0)
                {
                    remainingEngDelay -= TimeWarp.fixedDeltaTime;
                    if (remainingEngDelay <= 0)
                    {
                        activateEngine();
                    }
                }
                else if (remainingDelay > 0)//already triggered, just counting down until release
                {
                    remainingDelay -= TimeWarp.fixedDeltaTime;
                    if (remainingDelay <= 0)
                    {
                        activateDecouple();
                    }
                }
                else if (!isDecoupled)
                {
                    if (resource.amount <= resourceMin)
                    {
                        if (decoupleDelay > 0)
                        {
                            remainingDelay = decoupleDelay;
                        }
                        else
                        {
                            activateDecouple();
                        }
                    }
                }
            }
        }

        private void activateDecouple()
        {
            if (!isDecoupled)
            {
                if (activationDelay > 0)
                {
                    remainingEngDelay = activationDelay;
                }
                else
                {
                    activateEngine();
                }
                Decouple();
                Events["jettisonEvent"].active = false;
                Actions["jettisonAction"].active = false;
            }
        }

        private void activateEngine()
        {
            if (engineSwitch != null)
            {
                engineSwitch.Events["ModeEvent"].Invoke();
                String id = engineSwitch.secondaryEngineID;
                ModuleEnginesFX engFx = null;
                foreach (PartModule module in part.Modules)
                {
                    engFx = module as ModuleEnginesFX;
                    if (engFx != null && engFx.engineID == id)
                    {
                        engFx.Activate();
                    }
                }
            }
        }
    }
}
