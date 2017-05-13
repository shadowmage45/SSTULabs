﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class SSTUStockInterop : MonoBehaviour
    {

        private static List<Part> dragCubeUpdateParts = new List<Part>();
        private static List<Part> delayedUpdateDragCubeParts = new List<Part>();

        private static bool fireEditorEvent = false;

        public static SSTUStockInterop INSTANCE;

        private int tick = 0;
        
        public void Start()
        {
            INSTANCE = this;
            GameObject.DontDestroyOnLoad(this);
            MonoBehaviour.print("SSTUStockInterop Start");
            GameEvents.OnGameSettingsApplied.Add(new EventVoid.OnEvent(gameSettingsApplied));
            GameEvents.onGameStateLoad.Add(new EventData<ConfigNode>.OnEvent(gameStateLoaded));
        }

        public void OnDestroy()
        {
            MonoBehaviour.print("SSTUStockInterop Destroy");
            GameEvents.OnGameSettingsApplied.Remove(new EventVoid.OnEvent(gameSettingsApplied));
            GameEvents.onGameStateLoad.Remove(new EventData<ConfigNode>.OnEvent(gameStateLoaded));
        }

        public static void addDragUpdatePart(Part part)
        {
            if(part != null && HighLogic.LoadedSceneIsFlight && !dragCubeUpdateParts.Contains(part))
            {
                dragCubeUpdateParts.Add(part);
            }          
        }

        public static void fireEditorUpdate()
        {
            fireEditorEvent = HighLogic.LoadedSceneIsEditor;
        }

        public void FixedUpdate()
        {
            int len = dragCubeUpdateParts.Count;
            if (len>0 && (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight))
            {
                Part p;
                for (int i = 0; i < len; i++)
                {
                    p = dragCubeUpdateParts[i];
                    if (p == null) { continue; }
                    updatePartDragCube(p);
                    if( p.collider == null) { seatFirstCollider(p); }
                }
                dragCubeUpdateParts.Clear();
            }
        }

        private void gameSettingsApplied()
        {
            HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().PartUpgradesInSandbox = HighLogic.CurrentGame.Parameters.CustomParams<SSTUGameSettings>().upgradesInSandboxOverride;
        }

        private void gameStateLoaded(ConfigNode node)
        {
            HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().PartUpgradesInSandbox = HighLogic.CurrentGame.Parameters.CustomParams<SSTUGameSettings>().upgradesInSandboxOverride;
        }

        public void LateUpdate()
        {
            if (HighLogic.LoadedSceneIsEditor && fireEditorEvent)
            {
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
            fireEditorEvent = false;
        }
        
        public void ModuleManagerPostLoad()
        {
            MonoBehaviour.print("Reloading config databases (fuel types, model data, etc...)");
            FuelTypes.INSTANCE.loadConfigData();
            VolumeContainerLoader.loadConfigData();//needs to be loaded after fuel types
            SSTUModelData.loadConfigData();
            SSTUDatabase.loadConfigData();
        }

        private static void seatFirstCollider(Part part)
        {
            Collider[] colliders = part.gameObject.GetComponentsInChildren<Collider>();
            int len = colliders.Length;
            for (int i = 0; i < len; i++)
            {
                if (colliders[i].isTrigger) { continue; }
                if (colliders[i].GetType() == typeof(WheelCollider)) { continue; }
                part.collider = colliders[i];
                break;
            }
        }

        private static void updatePartDragCube(Part part)
        {
            DragCube newDefaultCube = DragCubeSystem.Instance.RenderProceduralDragCube(part);
            newDefaultCube.Weight = 1f;
            newDefaultCube.Name = "Default";
            part.DragCubes.ClearCubes();
            part.DragCubes.Cubes.Add(newDefaultCube);
            part.DragCubes.ResetCubeWeights();
        }
        
        public static void updateEngineThrust(ModuleEngines engine, float minThrust, float maxThrust)
        {
            engine.minThrust = minThrust;
            engine.maxThrust = maxThrust;
            ConfigNode updateNode = new ConfigNode("MODULE");
            updateNode.AddValue("maxThrust", engine.maxThrust);
            updateNode.AddValue("minThrust", engine.minThrust);
            engine.OnLoad(updateNode);
        }

        public static void updatePartHighlighting(Part part)
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { return; }//noop on prefabs
            if (part.HighlightRenderer != null)
            {
                part.HighlightRenderer = null;
                Transform model = part.transform.FindRecursive("model");
                if (model != null)
                {
                    Renderer[] renders = model.GetComponentsInChildren<Renderer>(false);
                    part.HighlightRenderer = new List<Renderer>(renders);
                }
                else
                {
                    part.HighlightRenderer = new List<Renderer>();
                }
                part.RefreshHighlighter();
            }
        }

    }
}
