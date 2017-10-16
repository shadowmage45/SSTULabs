using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.IO;

namespace SSTUTools
{
    public class SSTUStandardShaderSetup : PartModule
    {
        [KSPField]
        public string diffuseTexture = string.Empty;
        [KSPField]
        public string metalTexture = string.Empty;
        [KSPField]
        public string normalTexture = string.Empty;
        [KSPField]
        public string aoTexture = string.Empty;

        [KSPField]
        public int cubeSize = 256;

        ////the texture we render into in order to update reflection map
        //private RenderTexture envMap;
        //the cubemap used to render reflections
        private Cubemap envMap;
        //the camera game object
        private GameObject cameraObject;
        //the camera used to render reflection probe cubemaps
        private Camera reflectionCamera;
        //the reflection probe -- these should be handled by a main/generic setup on a per-scene basis, rather than a per part/per module setup.  
        private ReflectionProbe rp;
        //the cubemap used to render debug output cube textures (at 4x normal resolution)
        private Cubemap debugCube;

        [KSPEvent(guiActive =true, guiActiveEditor = true, guiName = "Export", guiActiveUncommand =true, guiActiveUnfocused = true)]
        public void exportCube()
        {
            reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
            Color bg = reflectionCamera.backgroundColor;
            reflectionCamera.backgroundColor = Color.clear;
            renderGalaxyBox(debugCube);
            exportCubemap(debugCube, "galaxy");
            renderScaledSpace(debugCube);
            exportCubemap(debugCube, "scaled");
            renderScenery(debugCube);
            exportCubemap(debugCube, "scene");
            renderSkybox(debugCube);
            exportCubemap(debugCube, "skybox");
            renderFullScene(debugCube);
            exportCubemap(debugCube, "full");
            reflectionCamera.clearFlags = CameraClearFlags.Depth;
            reflectionCamera.backgroundColor = bg;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            init();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            init();
        }

        public void Update()
        {
            if (rp != null && envMap != null && reflectionCamera != null && GalaxyCubeControl.Instance != null && ScaledSpace.Instance != null)
            {
                updateReflectionCube();
            }
        }

        private void init()
        {
            Shader standardShader = SSTUDatabase.getShader("SSTU/Standard");
            MonoBehaviour.print("Found standard shader: " + standardShader);
            Material standardMat = new Material(standardShader);
            standardMat.SetTexture("_MainTex", SSTUUtils.findTexture(diffuseTexture, false));
            standardMat.SetTexture("_MetallicGlossMap", SSTUUtils.findTexture(metalTexture, false));
            standardMat.SetTexture("_BumpMap", SSTUUtils.findTexture(normalTexture, true));
            standardMat.SetTexture("_OcclusionMap", SSTUUtils.findTexture(aoTexture, false));
            standardMat.EnableKeyword("_NORMALMAP");
            standardMat.EnableKeyword("_METALLICGLOSSMAP");

            Transform tr = part.transform.FindRecursive("model");
            updateTransforms(tr, standardMat);

            if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
            {
                //add reflection probe if not already present on game-object
                //TODO move this out to a per-vessel / per-scene setup; probes should not actually be moved?
                ReflectionProbe pr = tr.gameObject.GetComponent<ReflectionProbe>();
                if (pr == null) { pr = tr.gameObject.AddComponent<ReflectionProbe>(); }

                pr.type = UnityEngine.Rendering.ReflectionProbeType.Cube;
                pr.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
                pr.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                pr.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                pr.hdr = false;
                pr.size = Vector3.one * 30;
                pr.resolution = cubeSize;
                pr.enabled = true;
                MonoBehaviour.print("Added reflection probe: " + pr);
                rp = pr;

                //setup the reflection camera
                //need to ignore meshes with these shaders:  "Scatterer/OceanWhiteCaps" || "Scatterer/UnderwaterScatter" || "Scatterer/AtmosphericScatter"
                //use texture-replacer camera name, as it cleans up scatterer integration problems
                cameraObject = new GameObject("TRReflectionCamera");
                cameraObject.transform.position = part.transform.position;
                reflectionCamera = cameraObject.AddComponent<Camera>();

                //this is the actual reflection cubemap
                envMap = new Cubemap(cubeSize, TextureFormat.ARGB32, true);

                //this is a cubemap used to render debug reflections for output to file
                debugCube = new Cubemap(cubeSize * 4, TextureFormat.ARGB32, true);
                
                //finally, update the reflections for their current state
                //TODO -- should this be delayed until the first frame update?  (e.g. Unity Update()?)
                updateReflectionCube();
            }
        }

        private void updateTransforms(Transform root, Material mat)
        {
            Renderer r = root.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = mat;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbesAndSkybox;
                MonoBehaviour.print("Set tr: " + root + " to use shader: " + mat.shader + " with keywords: " + SSTUUtils.printArray(mat.shaderKeywords,", "));
            }
            foreach (Transform child in root) { updateTransforms(child, mat); }
        }

        private void updateReflectionCube()
        {
            reflectionCamera.enabled = true;

            //only clear the depth buffer, this allows for 'layered' rendering
            reflectionCamera.clearFlags = CameraClearFlags.Depth;
            //need to render each face individually due to layering
            int galaxyMask = 1 << 18;
            int atmosphereMask = (1 << 9) | (1 << 23);
            int scaledSpaceMask = 1 << 10;
            int sceneMask = 32784;//no clue...
            float farClip = 100f;
            float nearClip = 0.01f;
            for (int i = 0; i < 6; i++)
            {
                CubemapFace face = (CubemapFace)i;
                nearClip = reflectionCamera.nearClipPlane;
                farClip = 3.0e7f;

                //galaxy
                renderCubeFace(envMap, face, GalaxyCubeControl.Instance.transform.position, galaxyMask, reflectionCamera.nearClipPlane, farClip);

                //atmo
                renderCubeFace(envMap, face, part.transform.position, atmosphereMask, nearClip, farClip);

                //scaled space
                renderCubeFace(envMap, face, ScaledSpace.Instance.transform.position, scaledSpaceMask, nearClip, farClip);

                //scene
                farClip = 2000f;
                renderCubeFace(envMap, face, part.transform.position, sceneMask, nearClip, farClip);
            }
            //TODO convolution on cubemap
            //https://seblagarde.wordpress.com/2012/06/10/amd-cubemapgen-for-physically-based-rendering/

            //update the reflection probes texture
            rp.customBakedTexture = envMap;
            reflectionCamera.enabled = false;

            //TODO -- replace with custom baked skybox...
            //use in areas where other reflection probes don't make sense (space?)
            //RenderSettings.customReflection;
            
        }

        private void renderGalaxyBox(Cubemap envMap)
        {
            reflectionCamera.enabled = true;
            reflectionCamera.farClipPlane = 100000f;
            reflectionCamera.cullingMask = 1 << 18;
            reflectionCamera.gameObject.transform.position = GalaxyCubeControl.Instance.transform.position;
            reflectionCamera.RenderToCubemap(envMap);
            reflectionCamera.enabled = false;
        }

        private void renderScaledSpace(Cubemap envMap)
        {
            reflectionCamera.enabled = true;
            reflectionCamera.transform.position = ScaledSpace.Instance.transform.position;
            reflectionCamera.farClipPlane = 3.0e7f;
            reflectionCamera.cullingMask = (1 << 10);
            reflectionCamera.RenderToCubemap(envMap);
            reflectionCamera.enabled = false;
        }

        private void renderSkybox(Cubemap envMap)
        {
            reflectionCamera.gameObject.transform.position = GalaxyCubeControl.Instance.transform.position;
            renderLayer(envMap, (1 << 9) | (1<<23), 3.0e7f);
        }

        private void renderScenery(Cubemap envMap)
        {
            reflectionCamera.transform.position = part.transform.position;
            renderLayer(envMap, 32784, 2000f);
        }

        private void renderFullScene(Cubemap envMap)
        {
            reflectionCamera.transform.position = part.transform.position;
            renderLayer(envMap, ~0, 3.0e7f);
        }

        private void renderLayer(Cubemap envMap, int layerMask, float farClip)
        {
            reflectionCamera.enabled = true;
            int len = 6;
            for (int i = 0; i < len; i++)
            {
                renderCubeFace(envMap, (CubemapFace)i, reflectionCamera.transform.position, layerMask, reflectionCamera.nearClipPlane, farClip);
            }
            reflectionCamera.enabled = false;
        }

        private void renderCubeFace(Cubemap envMap, CubemapFace face, Vector3 cameraPos, int layerMask, float nearClip, float farClip)
        {
            reflectionCamera.transform.position = cameraPos;
            reflectionCamera.cullingMask = layerMask;
            reflectionCamera.nearClipPlane = nearClip;
            reflectionCamera.farClipPlane = farClip;
            int faceMask = 1 << (int)face;
            reflectionCamera.RenderToCubemap(envMap, faceMask);
        }

        private void exportCubemap(Cubemap envMap, string name)
        {
            Texture2D tex = new Texture2D(envMap.width, envMap.height, TextureFormat.RGB24, false);
            for (int i = 0; i < 6; i++)
            {
                tex.SetPixels(envMap.GetPixels((CubemapFace)i));
                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes("cubeExport/" + name + "-" + i + ".png", bytes);
            }
            GameObject.Destroy(tex);
        }
        
        //private void locateScattererMeshes()
        //{
        //    scatGos.Clear();
        //    GameObject[] allGos = FindObjectsOfType<GameObject>();
        //    int len = allGos.Length;
        //    for (int i = 0; i < len; i++)
        //    {
        //        if (allGos[i] != null && allGos[i].layer == 23)
        //        {
        //            MeshRenderer r = allGos[i].GetComponent<MeshRenderer>();
        //            if (r != null)
        //            {
        //                Material m = r.material;
        //                if (m != null && m.shader != null && (m.shader.name == "Scatterer/OceanWhiteCaps" || m.shader.name == "Scatterer/UnderwaterScatter" || m.shader.name == "Scatterer/AtmosphericScatter"))
        //                {
        //                    scatGos.Add(allGos[i]);
        //                }
        //            }
        //        }
        //    }
        //    MonoBehaviour.print("Found: " + scatGos.Count + " scatterer meshes");
        //}

    }
}
