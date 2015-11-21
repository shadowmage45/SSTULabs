using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUModelConstraint : PartModule
    {
        //active look-at constraints
        public List<object> lookConstraints = new List<object>();
        //active position constraints
        public List<object> positionConstraints = new List<object>();

        [Persistent]
        public String configNodeData = String.Empty;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasNode("LOOK_CONST") || node.HasNode("POS_CONST"))
            {
                configNodeData = node.ToString();
            }
            if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
            {
                initialize();
            }
            else
            {
                initializePrefab();
            }
        }

        public void Update()
        {
            updateConstraints();
        }

        private void initializePrefab()
        {
            initialize();
        }

        private void initialize()
        {
            ConfigNode node = SSTUNodeUtils.parseConfigNode(configNodeData);
            ConfigNode[] lookConstraintNodes = node.GetNodes("LOOK_CONST");
            foreach (ConfigNode lcn in lookConstraintNodes)
            {
                loadLookConstraint(lcn);
            }
            updateConstraints();
        }

        private void updateConstraints()
        {
            foreach (SSTULookConstraint lc in lookConstraints)
            {
                updateLookConstraint(lc);
            }
        }

        private void updateLookConstraint(SSTULookConstraint lookConst)
        {
            lookConst.mover.LookAt(lookConst.target, lookConst.mover.up);
        }

        private void loadLookConstraint(ConfigNode node)
        {
            String transformName = node.GetStringValue("transformName");
            String targetName = node.GetStringValue("targetName");
            bool singleTarget = node.GetBoolValue("singleTarget", false);
            Transform[] movers = part.FindModelTransforms(transformName);
            Transform[] targets = part.FindModelTransforms(targetName);
            int len = movers.Length;
            SSTULookConstraint lookConst;
            for (int i = 0; i < len; i++)
            {
                lookConst = new SSTULookConstraint();
                lookConst.target = singleTarget ? targets[0] : targets[i];
                lookConst.mover = movers[i];                
                lookConstraints.Add(lookConst);
            }
        }
    }

    public class SSTULookConstraint
    {
        public Transform mover;
        public Transform target;
        public Vector3 upAxis = Vector3.forward;
    }

    public class SSTUPosConstraint
    {
        public Transform transformA;
        public Transform transformB;
        public float percentFromAToB;
    }
}

