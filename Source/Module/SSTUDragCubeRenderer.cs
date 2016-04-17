using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSTUTools
{
    public class SSTUDragCubeRenderer : PartModule
    {
        private GameObject cubeGO;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "DC Renders"), UI_Toggle(enabledText = "On", disabledText = "Off", suppressEditorShipModified = true)]
        public bool enableDragCubeRendering = false;

        public void onEnableUpdated(BaseField field, object obj)
        {
            if (enableDragCubeRendering) { enableRendering(); }
            else { disableRendering(); }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Fields["enableDragCubeRendering"].uiControlEditor.onFieldChanged = onEnableUpdated;
            Fields["enableDragCubeRendering"].uiControlFlight.onFieldChanged = onEnableUpdated;
        }

        private void enableRendering()
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                Transform modelBase = part.transform.FindRecursive("model");
                DragCube cube;
                GameObject go;
                int len = part.DragCubes.Cubes.Count;
                Vector3 size;
                Vector3 center;
                Mesh mesh;
                MeshFilter filter;
                MeshRenderer render;
                for (int i = 0; i < len; i++)
                {
                    cube = part.DragCubes.Cubes[i];
                    go = new GameObject("DragCubeRenderer");
                    MeshBuilder mb = new MeshBuilder();
                    size = cube.Size;
                    center = cube.Center;
                    mb.generateCuboid(size, center, Vector2.zero, Vector2.one);
                    mesh = mb.buildMesh();
                    filter = go.AddComponent<MeshFilter>();
                    filter.mesh = mesh;
                    render = go.AddComponent<MeshRenderer>();
                    render.enabled = true;
                    go.transform.NestToParent(modelBase);
                }
            }
        }

        private void disableRendering()
        {
            Transform[] renders = part.transform.FindChildren("DragCubeRenderer");
            int len = renders.Length;
            for (int i = 0; i < len; i++)
            {
                GameObject.Destroy(renders[i].gameObject);
            }
        }
    }
}
