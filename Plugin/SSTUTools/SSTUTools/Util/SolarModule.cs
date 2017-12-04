using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SolarModule<T> : AnimationModule<T> where T : PartModule
    {

        private BaseField rotationPersistence;
        private ModelSolarData[] solarData;

        public SolarModule(Part part, T module, BaseField animationPersistence, BaseField rotationPersistence, BaseEvent deploy, BaseEvent retract) : base(part, module, animationPersistence, null, deploy, retract)
        {
            this.rotationPersistence = rotationPersistence;
        }

        public void setupSolarPanelData(ModelSolarData[] data)
        {
            throw new NotImplementedException();
        }

        public void solarFixedUpdate()
        {
            throw new NotImplementedException();
        }

    }

    public class SuncatcherData
    {
        public readonly Transform transform;
        public readonly float ecPerSecond;
    }
}
