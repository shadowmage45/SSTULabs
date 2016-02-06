using System;
using UnityEngine;

namespace SSTUTools
{
    /// <summary>
    /// Base clas for those part-modules that have extra config data that needs to be persisted for the lifetime of the module (rather than only present during prefab init)
    /// </summary>
    public class SSTUPartModuleConfigEnabled : PartModule
    {        
        /// <summary>
        /// Persistent config node data.  Field is populated during prefab OnLoad() with the contents of the modules' entire config node that is passed to it.
        /// Persistent data is re-parsed into a config node and passed to the part for reading during OnLoad() and/or OnStart() methods (only passed once, for whichever method is called first).
        /// </summary>
        [Persistent]
        public String configNodeData = String.Empty;
        private bool loadedConfig = false;
        
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { configNodeData = node.ToString(); }
            if (!loadedConfig)
            {
                loadedConfig = true;
                loadConfigData(SSTUConfigNodeUtils.parseConfigNode(configNodeData));
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (!loadedConfig)
            {
                loadedConfig = true;
                loadConfigData(SSTUConfigNodeUtils.parseConfigNode(configNodeData));
            }
        }

        protected virtual void loadConfigData(ConfigNode node)
        {
            throw new NotImplementedException("ERROR: Load config data is not implemented for: " + GetType());
        }

        protected void forceReloadConfig()
        {
            loadConfigData(SSTUConfigNodeUtils.parseConfigNode(configNodeData));
        }
    }
}
