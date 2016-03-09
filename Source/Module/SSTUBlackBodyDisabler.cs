using UnityEngine;

namespace SSTUTools.Module
{
    public class SSTUBlackBodyDisabler : PartModule
    {
        private MaterialColorUpdater mcu;
        //hack to fix 'glowing parts' when heatshield is really the only thing that should be glowing

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            mcu = new MaterialColorUpdater(part.transform.FindRecursive("model"), PhysicsGlobals.TemperaturePropertyID);
        }

        public void LateUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                mcu.Update(Color.black);
            }
        }
    }
}
