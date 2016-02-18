using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUModelConstraint : PartModule
    {
        public List<SSTUConstraint> constraints = new List<SSTUConstraint>();

        [KSPField]
        public int numOfPasses = 1;

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
            if (node.HasNode("LOOK_CONST") || node.HasNode("POS_CONST") || node.HasNode("LOCKED_CONST"))
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

        public void reInitialize()
        {
            initialize();
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
            constraints.Clear();
            ConfigNode node = SSTUConfigNodeUtils.parseConfigNode(configNodeData);

            ConfigNode[] lookConstraintNodes = node.GetNodes("LOOK_CONST");
            foreach (ConfigNode lcn in lookConstraintNodes)
            {
                loadLookConstraint(lcn);
            }

            ConfigNode[] lockedConstraintNodes = node.GetNodes("LOCKED_CONST");
            foreach (ConfigNode lcn in lockedConstraintNodes)
            {
                loadLockedConstraint(lcn);
            }
            updateConstraints();
        }

        private void updateConstraints()
        {
            for (int i = 0; i < numOfPasses; i++)
            {
                foreach (SSTUConstraint lc in constraints)
                {
                    lc.updateConstraint(i);
                }
            }
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
                lookConst = new SSTULookConstraint(node, movers[i], singleTarget ? targets[0] : targets[i], part);
                constraints.Add(lookConst);
            }
        }

        private void loadLockedConstraint(ConfigNode node)
        {
            String transformName = node.GetStringValue("transformName");
            String targetName = node.GetStringValue("targetName");
            bool singleTarget = node.GetBoolValue("singleTarget", false);
            Transform[] movers = part.FindModelTransforms(transformName);
            Transform[] targets = part.FindModelTransforms(targetName);
            int len = movers.Length;
            SSTULockedConstraint lookConst;
            for (int i = 0; i < len; i++)
            {
                lookConst = new SSTULockedConstraint(node, movers[i], singleTarget ? targets[0] : targets[i], part);
                constraints.Add(lookConst);
            }
        }
    }

    public class SSTUConstraint
    {
        public Transform mover;
        public Transform target;
        public int pass = 0;

        public SSTUConstraint(ConfigNode node, Transform mover, Transform target, Part part)
        {
            this.mover = mover;
            this.target = target;
            pass = node.GetIntValue("pass", pass);
        }

        public void updateConstraint(int pass)
        {
            if (pass == this.pass) { updateConstraintInernal(); }
        }

        protected virtual void updateConstraintInernal()
        {

        }
    }

    public class SSTULookConstraint : SSTUConstraint
    {
        public SSTULookConstraint(ConfigNode node, Transform mover, Transform target, Part part) : base(node, mover, target, part)
        {

        }

        protected override void updateConstraintInernal()
        {
            mover.LookAt(target, mover.up);
        }
    }

    public class SSTULockedConstraint : SSTUConstraint
    {
        public Vector3 lookAxis = Vector3.forward;//z+ in unity
        public Vector3 lockedAxis = Vector3.up;//y+ in unity
        private Quaternion defaultLocalRotation;
        public SSTULockedConstraint(ConfigNode node, Transform mover, Transform target, Part part) : base(node, mover, target, part)
        {
            defaultLocalRotation = mover.localRotation;
            lookAxis = node.GetVector3("lookAxis", lookAxis);
            lockedAxis = node.GetVector3("lockedAxis", lockedAxis);
        }

        protected override void updateConstraintInernal()
        {
            mover.localRotation = defaultLocalRotation;
            Vector3 targetPos = target.position - mover.position;//global target pos
            Vector3 localTargetPos = mover.InverseTransformPoint(target.position);//and in local space, to easier use tracked stuff
            //MonoBehaviour.print("gtp: " + targetPos + " ltp: " + localTargetPos);
                        
            float xRot = 90f + Mathf.Atan2(localTargetPos.z, localTargetPos.x) * Mathf.Rad2Deg;
            float yRot = 90f + Mathf.Atan2(localTargetPos.z, localTargetPos.y) * Mathf.Rad2Deg;
            float zRot = Mathf.Atan2(localTargetPos.y, localTargetPos.x) * Mathf.Rad2Deg;
            //MonoBehaviour.print("xr: " + xRot + " : yr: " + yRot + " : zr: " + zRot);

            if (lookAxis.z != 0)
            {
                if (lockedAxis.x != 0)
                {
                    //locked on x, rotate around Y
                    mover.Rotate(lockedAxis, yRot * -lookAxis.z);
                }
                else if (lockedAxis.y != 0)
                {
                    mover.Rotate(lockedAxis, xRot * lookAxis.z);
                }
            }
            else if (lookAxis.y != 0)
            {
                //TODO
            }
            else if (lookAxis.x != 0)
            {
                //TODO
            }
        }
    }

}

