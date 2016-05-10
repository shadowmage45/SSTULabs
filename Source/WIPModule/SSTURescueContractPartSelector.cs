using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Contracts;
using Contracts.Parameters;
using Contracts.Templates;

namespace SSTUTools
{
    //[KSPAddon(KSPAddon.Startup.Instantly | KSPAddon.Startup.EveryScene, false)]
    class SSTURescueContractPartSelector : MonoBehaviour
    {

        public void Start()
        {
            GameEvents.onNewVesselCreated.Add(new EventData<Vessel>.OnEvent(OnVesselCreated));
        }

        public void OnDestroy()
        {
            GameEvents.onNewVesselCreated.Remove(new EventData<Vessel>.OnEvent(OnVesselCreated));
        }

        public void OnVesselCreated(Vessel vessel)
        {
            MonoBehaviour.print("New vessel created!!");
            //vessel.ro
            RecoverAsset[] currentContracts = ContractSystem.Instance.GetCurrentContracts<RecoverAsset>();
            int len = currentContracts.Length;
            for (int i = 0; i < len; i++)
            {
                if (currentContracts[i].ContractState == Contract.State.Active)
                {

                }
            }            
        }

    }
}
