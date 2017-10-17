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

        [KSPField(guiActive = true), UI_Toggle(requireFullControl = false)]
        public bool renderGalaxy = true;

        [KSPField(guiActive = true), UI_Toggle(requireFullControl = false)]
        public bool renderScaled = true;

        [KSPField(guiActive = true), UI_Toggle(requireFullControl = false)]
        public bool renderAtmo = true;

        [KSPField(guiActive = true), UI_Toggle(requireFullControl = false)]
        public bool renderScenery = true;

        [KSPField(guiActive = true), UI_FloatEdit(requireFullControl = false, minValue = 0.3f, maxValue = 5f, incrementLarge = 1f, incrementSmall = 0.1f, incrementSlide = 0.05f, sigFigs = 4)]
        public float nearClip = 0.3f;

        [KSPField(guiActive = true), UI_FloatEdit(requireFullControl = false, minValue = 100f, maxValue = 3.0e7f, incrementLarge = 100000f, incrementSmall = 10000f, incrementSlide = 100f)]
        public float farClip = 10000f;

        public const int galaxyMask = 1 << 18;
        public const int atmosphereMask = (1 << 9) | (1 << 23);
        public const int scaledSpaceMask = 1 << 10;
        public const int sceneryMask = (1<<4) | (1<<15);
        public const int fullSceneMask = ~0;

        ////the texture we render into in order to update reflection map
        //private RenderTexture envMap;
        //the cubemap used to render reflections
        private RenderTexture envMap;
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
            if (shadows != null) { shadows.SetActive(true); }
            reflectionCamera.enabled = true;
            //float nearClip = reflectionCamera.nearClipPlane;
            //float farClip = 3.0e7f;

            reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
            Color bg = reflectionCamera.backgroundColor;
            reflectionCamera.backgroundColor = Color.clear;

            renderCube(debugCube, GalaxyCubeControl.Instance.transform.position, galaxyMask, nearClip, farClip);
            exportCubemap(debugCube, "galaxy");
            renderCube(debugCube, ScaledSpace.Instance.transform.position, scaledSpaceMask, nearClip, farClip);
            exportCubemap(debugCube, "scaled");
            renderCube(debugCube, part.transform.position, sceneryMask, nearClip, farClip);
            exportCubemap(debugCube, "scene");
            renderCube(debugCube, part.transform.position, atmosphereMask, nearClip, farClip);
            exportCubemap(debugCube, "skybox");
            renderCube(debugCube, part.transform.position, fullSceneMask, nearClip, farClip);
            exportCubemap(debugCube, "full");
            reflectionCamera.backgroundColor = bg;

            //export the same as the reflection
            reflectionCamera.clearFlags = CameraClearFlags.Depth;
            for (int i = 0; i < 6; i++)
            {
                CubemapFace face = (CubemapFace)i;
                                
                if (renderGalaxy)
                {
                    //galaxy
                    renderCubeFace(debugCube, face, GalaxyCubeControl.Instance.transform.position, galaxyMask, nearClip, farClip);
                }
                if (renderScaled)
                {
                    //scaled space
                    renderCubeFace(debugCube, face, ScaledSpace.Instance.transform.position, scaledSpaceMask, nearClip, farClip);
                }
                if (renderAtmo)
                {
                    //atmo
                    renderCubeFace(debugCube, face, part.transform.position, atmosphereMask, nearClip, farClip);
                }
                if (renderScenery)
                {
                    //scene
                    renderCubeFace(debugCube, face, part.transform.position, sceneryMask, nearClip, farClip);
                }
            }
            exportCubemap(debugCube, "reflect");

            reflectionCamera.enabled = false;
            if (shadows != null) { shadows.SetActive(false); }
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
                if (shadows == null) { locateEveShadowProjector(); }
                updateReflectionCube();
            }
        }

        private GameObject shadows;

        private void locateEveShadowProjector()
        {
            GameObject shad = GameObject.Find("EVE ShadowProjector");
            if (shad != null)
            {
                MonoBehaviour.print("Located eve shadows: " + shad);
                shadows = shad;
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
                reflectionCamera.nearClipPlane = 0.1f;

                //this is the actual reflection cubemap
                envMap = new RenderTexture(cubeSize, cubeSize, 24);
                envMap.dimension = UnityEngine.Rendering.TextureDimension.Cube;
                envMap.generateMips = true;

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
            reflectionCamera.clearFlags = CameraClearFlags.Depth;
            if (shadows != null) { shadows.SetActive(false); }

            //only clear the depth buffer, this allows for 'layered' rendering
            //need to render each face individually due to layering 
            //float nearClip = reflectionCamera.nearClipPlane;
            //float farClip = 3.0e7f;
            for (int i = 0; i < 6; i++)
            {
                CubemapFace face = (CubemapFace)i;

                if (renderGalaxy)
                {
                    //galaxy
                    renderCubeFace(envMap, face, GalaxyCubeControl.Instance.transform.position, galaxyMask, nearClip, farClip);
                }
                if (renderAtmo)
                {
                    //atmo
                    renderCubeFace(envMap, face, part.transform.position, atmosphereMask, nearClip, farClip);
                }
                if (renderScaled)
                {
                    //scaled space
                    renderCubeFace(envMap, face, ScaledSpace.Instance.transform.position, scaledSpaceMask, nearClip, farClip);
                }
                if (renderScenery)
                {
                    //scene
                    renderCubeFace(envMap, face, part.transform.position, sceneryMask, nearClip, farClip);
                }
            }
            //TODO convolution on cubemap
            //https://seblagarde.wordpress.com/2012/06/10/amd-cubemapgen-for-physically-based-rendering/

            //update the reflection probes texture
            rp.customBakedTexture = envMap;
            reflectionCamera.enabled = false;
            if (shadows != null) { shadows.SetActive(true); }
        }

        private void renderCubeFace(RenderTexture envMap, CubemapFace face, Vector3 cameraPos, int layerMask, float nearClip, float farClip)
        {
            cameraSetup(cameraPos, layerMask, nearClip, farClip);
            int faceMask = 1 << (int)face;
            reflectionCamera.RenderToCubemap(envMap, faceMask);
        }

        private void cameraSetup(Vector3 pos, int mask, float near, float far)
        {
            reflectionCamera.transform.position = pos;
            reflectionCamera.cullingMask = mask;
            reflectionCamera.nearClipPlane = near;
            reflectionCamera.farClipPlane = far;
        }

        //debug methods using cube-map for ability to export

        private void renderCubeFace(Cubemap envMap, CubemapFace face, Vector3 cameraPos, int layerMask, float nearClip, float farClip)
        {
            cameraSetup(cameraPos, layerMask, nearClip, farClip);
            int faceMask = 1 << (int)face;
            reflectionCamera.RenderToCubemap(envMap, faceMask);
        }

        private void renderCube(Cubemap envMap, Vector3 cameraPos, int layerMask, float nearClip, float farClip)
        {
            cameraSetup(cameraPos, layerMask, nearClip, farClip);
            reflectionCamera.RenderToCubemap(envMap);
        }

        private void exportCubemap(Cubemap envMap, string name)
        {
            Texture2D tex = new Texture2D(envMap.width, envMap.height, TextureFormat.ARGB32, false);
            for (int i = 0; i < 6; i++)
            {
                tex.SetPixels(envMap.GetPixels((CubemapFace)i));
                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes("cubeExport/" + name + "-" + i + ".png", bytes);
            }
            GameObject.Destroy(tex);
        }

    }
}
