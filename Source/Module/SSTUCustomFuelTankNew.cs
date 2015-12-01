using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{

    public class SSTUCustomFuelTankNew : PartModule, IPartCostModifier
    {
        //CSV string of fuel types
        [KSPField]
        public String fuelTypes = String.Empty;

        [KSPField]
        public String defaultTankName = String.Empty;

        [KSPField]
        public String defaultTopCapName = String.Empty;

        [KSPField]
        public String defaultBottomCapName = String.Empty;

        [KSPField]
        public String defaultFuelType = String.Empty;

        [KSPField]
        public String prefabTankName = String.Empty;

        [KSPField]
        public String topNodeGroupName = "top";

        [KSPField]
        public String bottomNodeGroupName = "bottom";

        [KSPField]
        public bool canChangeInFlight = false;

        [KSPField]
        public float tankDiameter = 5f;

        //if set to true, disables many internal checks/updates/UI fields/methods in favor of using RF to set fuels.  Also enables basic on-volume-changed callback to RF for tank type changes.
        [KSPField]
        public bool useRF = false;

        //persistent data storage for config node data -- workaround for KSP never allowing access to base node data after prefab construction
        [Persistent]
        public String configNodeData = String.Empty;

        //persistent data storage for currently set values for fuel type, main tank, and top/bottom cap names
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Fuel Type")]
        public String currentFuelType = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Main Tank")]
        public String mainTankName = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Top Cap")]
        public String topCapName = String.Empty;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Bottom Cap")]
        public String bottomCapName = String.Empty;

        private MainTankDef mainTankDef;
        private TankCapDef topCapDef;
        private TankCapDef bottomCapDef;
        private NodeGroup topNodeGroup;
        private NodeGroup bottomNodeGroup;
        private List<String> fuelTypeList = new List<String>();
        private List<MainTankDef> mainTankDefs = new List<MainTankDef>();
        private List<TankCapDef> topCapDefs = new List<TankCapDef>();
        private List<TankCapDef> bottomCapDefs = new List<TankCapDef>();

        //calculated stats from updateTankStats() -- used by other methods and to display info to user
        [KSPField(guiActiveEditor = true, guiName = "Tank Cost")]
        public float tankCost = 0;
        [KSPField(guiActiveEditor = true, guiName = "Tank Dry Mass")]
        public float tankDryMass = 0;
        [KSPField(guiActiveEditor = true, guiName = "Tank Usable Vol. (m^3)")]
        public float tankVolume = 0;

        [KSPEvent(guiName = "Jettison Contents", guiActive = false, guiActiveEditor = false, guiActiveUnfocused = false)]
        public void jettisonContentsEvent()
        {
            emptyTankContents();
        }

        [KSPEvent(guiName = "Next Fuel Type", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextFuelEvent()
        {
            if (canChangeTank())
            {
                setFuelType(findNextFuelType(currentFuelType, false), true);
            }
        }

        [KSPEvent(guiName = "Prev Main Tank Type", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void prevTankEvent()
        {
            MainTankDef mtd = findNextAvailableDef(mainTankDef, mainTankDefs, true);
            if (mtd != null)
            {
                setMainVariant(mtd, true);
            }
        }

        [KSPEvent(guiName = "Next Main Tank Type", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextTankEvent()
        {
            MainTankDef mtd = findNextAvailableDef(mainTankDef, mainTankDefs, false);
            if (mtd != null)
            {
                setMainVariant(mtd, true);
            }
        }

        [KSPEvent(guiName = "Prev Top Cap Type", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void prevTopEvent()
        {
            TankCapDef tcd = findNextAvailableDef(topCapDef, topCapDefs, true);
            if (tcd != null)
            {
                setTopVariant(tcd, true);
            }
        }

        [KSPEvent(guiName = "Next Top Cap Type", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextTopEvent()
        {
            TankCapDef tcd = findNextAvailableDef(topCapDef, topCapDefs, false);
            if (tcd != null)
            {
                setTopVariant(tcd, true);
            }
        }

        [KSPEvent(guiName = "Prev Bottom Cap Type", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void prevBottomEvent()
        {
            TankCapDef bcd = findNextAvailableDef(bottomCapDef, bottomCapDefs, true);
            if (bcd != null)
            {
                setBottomVariant(bcd, true);
            }
        }

        [KSPEvent(guiName = "Next Bottom Cap Type", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextBottomEvent()
        {
            TankCapDef bcd = findNextAvailableDef(bottomCapDef, bottomCapDefs, false);
            if (bcd != null)
            {
                setBottomVariant(bcd, true);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasNode("TANK"))//only prefab instance config node should contain this data...but whatever, grab it whenever it is present
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

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (mainTankDef == null)//uninitialized
            {
                initialize();
            }
            if (canChangeInFlight)
            {
                Events["nextFuelEvent"].guiActive = true;
                Events["jettisonContentsEvent"].active = Events["jettisonContentsEvent"].guiActive = true;
            }
            if (useRF)
            {
                Events["nextFuelEvent"].active = false;
                Fields["tankCost"].guiActiveEditor = false;
                Fields["tankDryMass"].guiActiveEditor = false;
                Fields["tankVolume"].guiActiveEditor = false;
            }
        }

        public override string GetInfo()
        {
            if (mainTankDef != null) { mainTankDef.disable(part); }
            mainTankDef = null;
            if (topCapDef != null) { topCapDef.disable(part); }
            topCapDef = null;
            if (bottomCapDef != null) { bottomCapDef.disable(part); }
            bottomCapDef = null;
            return "This fuel tank has configurable height and top and bottom nosecones.";
        }

        public float GetModuleCost(float defaultCost)
        {
            return tankCost;
        }

        private void removeExistingModels()
        {
            Transform tr = part.FindModelTransform("model");
            SSTUUtils.destroyChildren(tr);
        }

        private bool canChangeTank()
        {
            if (useRF) { return false; }
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (!canChangeInFlight) { return false; }
                foreach (PartResource res in part.Resources.list)
                {
                    if (res.amount > 0) { return false; }
                }
                return true;
            }
            return true;
        }

        private void emptyTankContents()
        {
            //TODO add in delayed timer, enforce button must be pressed twice in twenty seconds in order to trigger
            foreach (PartResource res in part.Resources.list)
            {
                res.amount = 0;
            }
        }

        private void initialize()
        {
            loadPersistentConfigData();
            removeExistingModels();
            if (String.IsNullOrEmpty(mainTankName))
            {
                initializeTankDefaults();
            }
            else
            {
                restoreSavedTankData();
            }
        }

        private void initializePrefab()
        {
            loadPersistentConfigData();
            prefabTankName = String.IsNullOrEmpty(prefabTankName) ? defaultTankName : prefabTankName;
            mainTankDef = findDef(prefabTankName, mainTankDefs);
            topCapDef = findDef(defaultTopCapName, topCapDefs);
            bottomCapDef = findDef(defaultBottomCapName, bottomCapDefs);
            enableTankDef(mainTankDef, mainTankDefs);
            enableTankDef(topCapDef, topCapDefs);
            enableTankDef(bottomCapDef, bottomCapDefs);
            currentFuelType = defaultFuelType;

            updateCapsAndNodes();
            updateTankStats();

            //now clear resources and such... otherwise it creates havok when parts are cloned from the prefab; could potentially just clear them whenever the part is initialized, prior to setting of fuel type
            part.Resources.list.Clear();
            PartResource[] resources = part.GetComponents<PartResource>();
            int len = resources.Length;
            for (int i = 0; i < len; i++)
            {
                GameObject.Destroy(resources[i]);
            }
        }

        private void restoreSavedTankData()
        {
            mainTankDef = findDef(mainTankName, mainTankDefs);
            topCapDef = findDef(topCapName, topCapDefs);
            bottomCapDef = findDef(bottomCapName, bottomCapDefs);
            enableTankDef(mainTankDef, mainTankDefs);
            enableTankDef(topCapDef, topCapDefs);
            enableTankDef(bottomCapDef, bottomCapDefs);
            updateCapsAndNodes();
            updateTankStats();
            updateDragCube();
        }

        private void initializeTankDefaults()
        {
            mainTankDef = findDef(defaultTankName, mainTankDefs);
            topCapDef = findDef(defaultTopCapName, topCapDefs);
            bottomCapDef = findDef(defaultBottomCapName, bottomCapDefs);
            mainTankName = mainTankDef.tankName;
            topCapName = topCapDef.tankName;
            bottomCapName = bottomCapDef.tankName;
            enableTankDef(mainTankDef, mainTankDefs);
            enableTankDef(topCapDef, topCapDefs);
            enableTankDef(bottomCapDef, bottomCapDefs);
            currentFuelType = defaultFuelType;
            updateCapsAndNodes();
            updateTankStats();
            updatePartResources();
            updateDragCube();
        }

        private void loadPersistentConfigData()
        {
            mainTankDefs.Clear();
            topCapDefs.Clear();
            bottomCapDefs.Clear();

            ConfigNode node = SSTUNodeUtils.parseConfigNode(configNodeData);

            float mainMaxHeight = 0;
            float bottomMaxHeight = 0;
            float topMaxHeight = 0;

            ConfigNode[] mainTankNodes = node.GetNodes("TANK");
            if (mainTankNodes == null) { print("ERROR -- no nodes defined for main tank setups, bad things are about to happen"); }
            MainTankDef mtd;
            foreach (ConfigNode mtn in mainTankNodes)
            {
                mtd = new MainTankDef(mtn, part);
                if (mtd.tankHeight > mainMaxHeight) { mainMaxHeight = mtd.tankHeight; }
                mainTankDefs.Add(mtd);
            }

            ConfigNode[] nodeGroupNodes = node.GetNodes("NODEGROUP");
            if (nodeGroupNodes == null) { print("ERROR -- no node groups defined, bad things are about to happen"); }
            foreach (ConfigNode ngn in nodeGroupNodes)
            {
                String n = ngn.GetStringValue("name");
                if (n.Equals(topNodeGroupName)) { topNodeGroup = new NodeGroup(ngn); }
                else if (n.Equals(bottomNodeGroupName)) { bottomNodeGroup = new NodeGroup(ngn); }
            }

            ConfigNode[] capNodes = node.GetNodes("CAP");
            if (capNodes == null) { print("ERROR -- no nodes defined for tank cap setups, bad things are about to happen"); }

            TankCapDef tcd;
            foreach (ConfigNode capNode in capNodes)
            {
                tcd = new TankCapDef(capNode, part, false, topNodeGroup);
                if (tcd.tankHeight > topMaxHeight) { topMaxHeight = tcd.tankHeight; }
                topCapDefs.Add(tcd);
                tcd = new TankCapDef(capNode, part, true, bottomNodeGroup);
                if (tcd.tankHeight > bottomMaxHeight) { bottomMaxHeight = tcd.tankHeight; }
                bottomCapDefs.Add(tcd);
            }

            String[] fuelTypes = this.fuelTypes.Split(new String[] { "," }, StringSplitOptions.None);
            foreach (String val in fuelTypes)
            {
                fuelTypeList.Add(val.Trim());
            }
        }

        private void updateTankStats()
        {
            //update tank mass, cost, and volume from the currently selected tank and caps
            tankVolume = mainTankDef.tankVolume + topCapDef.tankVolume + bottomCapDef.tankVolume;
            if (useRF)
            {
                SSTUUtils.updateRealFuelsPartVolume(part, tankVolume);
            }
            else
            {
                SSTUFuelType fuelType = SSTUFuelTypes.INSTANCE.getFuelType(currentFuelType);
                if (fuelType != null)
                {
                    tankVolume -= fuelType.tankageVolumeLoss * tankVolume;//subtract tankage loss from raw volume to derive usable volume, all other calculations will use 'usable volume'
                    tankDryMass = fuelType.tankageMassFactor * tankVolume;//tankage mass based off of total volume			
                    tankCost = fuelType.costPerDryTon * tankDryMass + fuelType.getResourceCost(tankVolume);
                }
                part.mass = tankDryMass;
            }
        }

        private void updatePartResources()
        {
            if (useRF)
            {
                return;
            }
            SSTUFuelType type = SSTUFuelTypes.INSTANCE.getFuelType(currentFuelType);
            if (type != null)
            {
                type.setResourcesInPart(part, tankVolume, !HighLogic.LoadedSceneIsFlight);
            }
        }

        private void updateDragCube()
        {
            DragCube newCube = DragCubeSystem.Instance.RenderProceduralDragCube(part);
            newCube.Name = "Default";
            part.DragCubes.ClearCubes();
            part.DragCubes.Cubes.Add(newCube);
            part.DragCubes.ResetCubeWeights();
            part.DragCubes.SetCubeWeight("Default", 1f);
        }

        private void updateCapsAndNodes()
        {
            float topHeight = mainTankDef.tankHeight * 0.5f;
            float bottomHeight = -topHeight;
            topCapDef.updateCapHeight(part, topHeight);
            bottomCapDef.updateCapHeight(part, bottomHeight);
        }

        private void setFuelType(String fuelType, bool updateSymmetry)
        {
            currentFuelType = fuelType;
            updateTankStats();
            updatePartResources();
            if (updateSymmetry)
            {
                SSTUCustomFuelTankNew tank = null;
                foreach (Part p in part.symmetryCounterparts)
                {
                    tank = p.GetComponent<SSTUCustomFuelTankNew>();
                    if (tank == null) { continue; }
                    tank.setFuelType(fuelType, false);
                }
            }
        }

        private void setMainVariant(MainTankDef def, bool updateSymmetry)
        {
            if (mainTankDef != null) { mainTankDef.disable(part); }
            mainTankName = def.tankName;
            mainTankDef = def;
            enableTankDef(def, mainTankDefs);
            updateCapsAndNodes();
            updateTankStats();
            updatePartResources();
            updateDragCube();
            updateTextureSet(updateSymmetry);
            if (updateSymmetry)
            {
                SSTUCustomFuelTankNew tank = null;
                foreach (Part p in part.symmetryCounterparts)
                {
                    tank = p.GetComponent<SSTUCustomFuelTankNew>();
                    if (tank == null) { continue; }
                    tank.setMainVariant(tank.findDef<MainTankDef>(def.tankName, tank.mainTankDefs), false);
                }
            }
        }

        private void setTopVariant(TankCapDef def, bool updateSymmetry)
        {
            if (topCapDef != null) { topCapDef.disable(part); }
            topCapName = def.tankName;
            topCapDef = def;
            enableTankDef(def, topCapDefs);
            updateCapsAndNodes();
            updateTankStats();
            updatePartResources();
            updateDragCube();
            updateTextureSet(updateSymmetry);
            if (updateSymmetry)
            {
                SSTUCustomFuelTankNew tank = null;
                foreach (Part p in part.symmetryCounterparts)
                {
                    tank = p.GetComponent<SSTUCustomFuelTankNew>();
                    if (tank == null) { continue; }
                    tank.setTopVariant(tank.findDef<TankCapDef>(def.tankName, tank.topCapDefs), false);
                }
            }
        }

        private void setBottomVariant(TankCapDef def, bool updateSymmetry)
        {
            if (bottomCapDef != null) { bottomCapDef.disable(part); }
            bottomCapName = def.tankName;
            bottomCapDef = def;
            enableTankDef(def, bottomCapDefs);
            updateCapsAndNodes();
            updateTankStats();
            updatePartResources();
            updateDragCube();
            updateTextureSet(updateSymmetry);
            if (updateSymmetry)
            {
                SSTUCustomFuelTankNew tank = null;
                foreach (Part p in part.symmetryCounterparts)
                {
                    tank = p.GetComponent<SSTUCustomFuelTankNew>();
                    if (tank == null) { continue; }
                    tank.setBottomVariant(tank.findDef<TankCapDef>(def.tankName, tank.bottomCapDefs), false);
                }
            }
        }

        private void updateTextureSet(bool updateSymmetry)
        {
            SSTUTextureSwitch[] texSwitches = part.GetComponents<SSTUTextureSwitch>();
            if (texSwitches == null || texSwitches.Length == 0)
            {
                return;
            }
            foreach (SSTUTextureSwitch ts in texSwitches)
            {
                ts.enableTextureSet(ts.currentTextureSet);
            }
            if (updateSymmetry)
            {
                SSTUCustomFuelTankNew tank = null;
                foreach (Part p in part.symmetryCounterparts)
                {
                    tank = p.GetComponent<SSTUCustomFuelTankNew>();
                    if (tank == null) { continue; }
                    tank.updateTextureSet(false);
                }
            }
        }

        private void enableTankDef<T>(T def, List<T> defs)
            where T : TankDef
        {
            foreach (T d in defs)
            {
                if (d == def) { continue; }
                d.disable(part);
            }
            def.enable(part);
        }

        private T findNextAvailableDef<T>(T current, List<T> list, bool iterateBackwards)
            where T : TankDef
        {
            int index = -1;
            int len = list.Count;
            int iter = iterateBackwards ? -1 : 1;

            for (int i = 0; i < len; i++)
            {
                if (list[i] == current)
                {
                    index = i;
                    break;
                }
            }

            int c = 0;
            for (int i = 1; i < len; i++)//start at index offset 1, to skip 'current', only iterate for 'len' iterations, to prevent infinite wraparound
            {
                c = i * iter + index;
                if (c < 0) { c += len; }//wrap backwards
                if (c >= len) { c -= len; }//wrap forwards
                if (list[c].canApply(part))
                {
                    return list[c];//return first valid from list in given search direction
                }
            }

            return null;//return null for no other variants available
        }

        private T findDef<T>(String name, List<T> list)
            where T : TankDef
        {
            foreach (TankDef t in list)
            {
                if (t.tankName.Equals(name))
                {
                    return (T)t;
                }
            }
            print("ERROR: Could not locate tank/cap by name of: " + name);
            return null;
        }

        private String findNextFuelType(String currentType, bool iterateBackwards)
        {
            int index = -1;
            int len = fuelTypeList.Count;
            int iter = iterateBackwards ? -1 : 1;
            for (int i = 0; i < len; i++)
            {
                if (fuelTypeList[i].Equals(currentType))
                {
                    index = i;
                    break;
                }
            }
            if (index == -1)
            {
                MonoBehaviour.print("Could not locate current fuel type, returning first fuel type");
                return fuelTypeList[0];
            }
            index += iter;
            if (index < 0) { index += len; }
            if (index >= len) { index -= len; }
            return fuelTypeList[index];
        }

    }    
    
}

