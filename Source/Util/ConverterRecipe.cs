using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SSTUTools
{
    public class ConverterRecipe
    {
        private List<ConverterResourceEntry> inputs = new List<ConverterResourceEntry>();
        private List<ConverterResourceEntry> outputs = new List<ConverterResourceEntry>();

        public ConverterRecipe loadFromNode(ConfigNode node)
        {
            ConfigNode[] inputNodes = node.GetNodes("RECIPEINPUT");
            ConfigNode[] outputNodes = node.GetNodes("RECIPEOUTPUT");
            int len = inputNodes.Length;
            for (int i = 0; i < len; i++)
            {
                inputs.Add(new ConverterResourceEntry().loadFromNode(inputNodes[i]));
            }
            len = outputNodes.Length;
            for (int i = 0; i < len; i++)
            {
                outputs.Add(new ConverterResourceEntry().loadFromNode(outputNodes[i]));
            }
            return this;
        }

        public void process(Part part, float percentage, float time)
        {
            double lowestInput = 1d;
            double lowestOutput = 1d;

            double p;
            int len = inputs.Count;
            for (int i = 0; i < len; i++)
            {
                inputs[i].updateAvailableResource(part);
                p = inputs[i].getAvaiablePercent(percentage);
                if (p < lowestInput) { lowestInput = p; }
            }
            len = outputs.Count;
            for (int i = 0; i < len; i++)
            {
                outputs[i].updateAvailableResourceCapacity(part);
                if (outputs[i].stopIfFull)
                {
                    p = outputs[i].getAvailableCapacityPercent(percentage);
                    if (p < lowestOutput) { lowestOutput = p; }
                }
            }
            float lowest = (float)(lowestInput < lowestOutput ? lowestInput : lowestOutput);
            if (lowest == 0)
            {
                //				MonoBehaviour.print ("input or output percent = 0, nothing to process (no inputs, or no room for outputs)");
                return;
            }
            //MonoBehaviour.print ("Processing recipe with input/output percentages of: " + lowestInput + " :: " + lowestOutput+" for time: "+time+" and raw percent: "+percentage);

            //loop back through inputs and outputs adding/removing resources as per the percentages listed above

            lowest *= time;
            len = inputs.Count;
            for (int i = 0; i < len; i++)
            {
                part.RequestResource(inputs[i].resourceName, lowest * inputs[i].resourceAmount);
                //MonoBehaviour.print("removing input of qty: "+(lowest * inputs[i].resourceAmount));
            }
            len = outputs.Count;
            for (int i = 0; i < len; i++)
            {
                part.RequestResource(outputs[i].resourceName, -lowest * outputs[i].resourceAmount);
                //MonoBehaviour.print("adding output of qty: "+(-lowest * outputs[i].resourceAmount));
            }
        }

        public override string ToString()
        {
            return string.Format("[ConverterRecipe]");
        }
    }

    [Serializable]
    public class ConverterResourceEntry
    {
        //all public fields should be serialized
        public String resourceName = String.Empty;
        public float resourceAmount = 0;
        public bool stopIfFull = false;
        public int resourceDefID = 0;

        //private cached list object to eliminate GC churn on every tick just to find connected resources=\
        private List<PartResource> cacheList = new List<PartResource>();

        //private cached vars for found and empty capacity; only one of these fields will be populated, depending on if the resource is an input or an output
        private double foundAmount = 0;
        private double emptyCapacity = 0;

        //sum functions for Linq selector use
        private static Func<PartResource, double> sumFunc = ((PartResource r) => (r.amount));
        private static Func<PartResource, double> emptyFunc = ((PartResource r) => (r.maxAmount - r.amount));

        public ConverterResourceEntry loadFromNode(ConfigNode node)
        {
            resourceName = node.GetStringValue("resourceName");
            resourceAmount = node.GetFloatValue("resourceAmount");
            stopIfFull = node.GetBoolValue("stopIfFull");
            if (PartResourceLibrary.Instance.resourceDefinitions.Contains(resourceName))
            {
                resourceDefID = PartResourceLibrary.Instance.resourceDefinitions[resourceName].id;
            }
            else
            {
                MonoBehaviour.print("ERROR, could not locate resource definition for name: " + resourceName);
            }
            return this;
        }

        public void updateAvailableResource(Part p)
        {
            p.GetConnectedResources(resourceDefID, ResourceFlowMode.ALL_VESSEL, cacheList);
            foundAmount = Enumerable.Sum<PartResource>(cacheList, sumFunc);
            cacheList.Clear();
        }

        public void updateAvailableResourceCapacity(Part p)
        {
            p.GetConnectedResources(resourceDefID, ResourceFlowMode.ALL_VESSEL, cacheList);
            emptyCapacity = Enumerable.Sum<PartResource>(cacheList, emptyFunc);
            cacheList.Clear();
        }

        public override string ToString()
        {
            return string.Format("[ConverterResourceEntry]");
        }

        public double getAvaiablePercent(float requestPercent)
        {
            double p = foundAmount / (resourceAmount * requestPercent);
            if (p * resourceAmount < 5e-5)//min resource clamp amount
            {
                p = 0;
            }
            return p;
        }

        public double getAvailableCapacityPercent(float requestPercent)
        {
            double p = emptyCapacity / (resourceAmount * requestPercent);
            if (p * resourceAmount < 5e-5)//min resource clamp amount
            {
                p = 0;
            }
            return p;
        }
    }
}
