using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.IO;
using scatterer;

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
                
        private RenderTexture envMap;
        private Material skyMat;
        private Cubemap cube2;
        private GameObject go;
        private Camera cam;
        private ReflectionProbe rp;

        [KSPEvent(guiActive =true, guiActiveEditor = true, guiName = "Export", guiActiveUncommand =true, guiActiveUnfocused = true)]
        public void exportCube()
        {
            toggleScattererMeshes(false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            Color bg = cam.backgroundColor;
            cam.backgroundColor = Color.clear;
            renderGalaxyBox(cube2);
            exportCubemap(cube2, "galaxy");
            renderScaledSpace(cube2);
            exportCubemap(cube2, "scaled");
            renderScenery(cube2);
            exportCubemap(cube2, "scene");
            renderSkybox(cube2);
            exportCubemap(cube2, "skybox");
            renderFullScene(cube2);
            exportCubemap(cube2, "full");
            cam.clearFlags = CameraClearFlags.Depth;
            cam.backgroundColor = bg;
            dumpScene();
            toggleScattererMeshes(true);
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

        public void LateUpdate()
        {
            if (scatGos.Count == 0) { locateScattererMeshes(); }
            if (rp != null && cube2 != null && cam != null && GalaxyCubeControl.Instance != null && ScaledSpace.Instance != null)
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
            if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
            {
                envMap = new RenderTexture(cubeSize, cubeSize, 24);
                envMap.dimension = UnityEngine.Rendering.TextureDimension.Cube;
                envMap.hideFlags = HideFlags.HideAndDontSave;
                envMap.wrapMode = TextureWrapMode.Clamp;

                go = new GameObject("reflectCam");
                go.transform.position = part.transform.position;
                int mask = 32784;
                cam = go.AddComponent<Camera>();
                cam.cullingMask = mask;
                cam.clearFlags = CameraClearFlags.Depth;
                //cam.backgroundColor = Color.black;
                updateReflectionCube();
                
                cube2 = new Cubemap(cubeSize*4, TextureFormat.ARGB32, true);
                Shader skyboxShader = SSTUDatabase.getShader("Skybox/Cubemap");
                skyMat = new Material(skyboxShader);
                skyMat.SetTexture("_Tex", cube2);
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
            toggleScattererMeshes(false);
            cam.enabled = true;

            //only clear the depth buffer
            cam.clearFlags = CameraClearFlags.Depth;
            //need to render each face individually due to layering
            for (int i = 0; i < 6; i++)
            {
                CubemapFace face = (CubemapFace)i;
                int mask = 1 << i;

                //capture the galaxy
                cam.farClipPlane = 100000f;
                cam.cullingMask = 1 << 18;
                cam.gameObject.transform.position = GalaxyCubeControl.Instance.transform.position;
                cam.RenderToCubemap(envMap, mask);

                //atmosphere
                cam.farClipPlane = 3.0e7f;
                cam.cullingMask = (1 << 9) | (1 << 23);
                cam.gameObject.transform.position = part.transform.position;
                cam.RenderToCubemap(envMap, mask);

                //scaled space
                cam.transform.position = ScaledSpace.Instance.transform.position;
                cam.farClipPlane = 3.0e7f;
                cam.cullingMask = 1 << 10;
                cam.RenderToCubemap(envMap, mask);
                
                //scene
                cam.transform.position = part.transform.position;
                cam.farClipPlane = 6000f;
                cam.cullingMask = 32784;
                cam.RenderToCubemap(envMap, mask);
            }
            
            go.transform.position = part.transform.position;
            rp.customBakedTexture = envMap;
            cam.enabled = false;
            toggleScattererMeshes(true);
        }

        private void renderGalaxyBox(Cubemap envMap)
        {
            cam.enabled = true;
            cam.farClipPlane = 100000f;
            cam.cullingMask = 1 << 18;
            cam.gameObject.transform.position = GalaxyCubeControl.Instance.transform.position;
            cam.RenderToCubemap(envMap);
            cam.enabled = false;
        }

        private void renderScaledSpace(Cubemap envMap)
        {
            cam.enabled = true;
            cam.transform.position = ScaledSpace.Instance.transform.position;
            cam.farClipPlane = 3.0e7f;
            cam.cullingMask = (1 << 10);
            cam.RenderToCubemap(envMap);
            cam.enabled = false;
        }

        private void renderSkybox(Cubemap envMap)
        {
            cam.gameObject.transform.position = GalaxyCubeControl.Instance.transform.position;
            renderLayer(envMap, (1 << 9) | (1<<23), 3.0e7f);
        }

        private void renderScenery(Cubemap envMap)
        {
            cam.transform.position = part.transform.position;
            renderLayer(envMap, 32784, 2000f);
        }

        private void renderFullScene(Cubemap envMap)
        {
            cam.transform.position = part.transform.position;
            renderLayer(envMap, ~0, 3.0e7f);
        }

        private void renderLayer(Cubemap envMap, int layerMask, float farClip)
        {
            cam.enabled = true;
            cam.farClipPlane = farClip;
            cam.cullingMask = layerMask;
            cam.RenderToCubemap(envMap);
            cam.enabled = false;
        }

        private void exportCubemap(Cubemap cube, string name)
        {
            Texture2D tex = new Texture2D(cube.width, cube.height, TextureFormat.RGB24, false);
            for (int i = 0; i < 6; i++)
            {
                tex.SetPixels(cube.GetPixels((CubemapFace)i));
                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes("cubeExport/" + name + "-" + i + ".png", bytes);
            }
            GameObject.Destroy(tex);
        }

        private void dumpScene()
        {
            MonoBehaviour.print("SCENE DUMP LAYER 23 -------------------------------------------- ");
            GameObject[] allGos = FindObjectsOfType<GameObject>();
            int len = allGos.Length;
            for (int i = 0; i < len; i++)
            {
                if (allGos[i] != null && allGos[i].layer == 23)
                {
                    MonoBehaviour.print("found rendered object: " + allGos[i]+" parent: "+allGos[i].transform.parent);
                    SSTUUtils.recursePrintComponents(allGos[i], "");
                }
            }
        }
        
        private List<GameObject> scatGos = new List<GameObject>();

        private void locateScattererMeshes()
        {
            scatGos.Clear();
            GameObject[] allGos = FindObjectsOfType<GameObject>();
            int len = allGos.Length;
            for (int i = 0; i < len; i++)
            {
                if (allGos[i] != null && allGos[i].layer == 23)
                {
                    MeshRenderer r = allGos[i].GetComponent<MeshRenderer>();
                    if (r != null)
                    {
                        Material m = r.material;
                        if (m != null && m.shader != null && (m.shader.name == "Scatterer/OceanWhiteCaps" || m.shader.name == "Scatterer/UnderwaterScatter" || m.shader.name == "Scatterer/AtmosphericScatter"))
                        {
                            scatGos.Add(allGos[i]);
                        }
                    }
                }
            }
            MonoBehaviour.print("Found: " + scatGos.Count + " scatterer meshes");
        }

        private void toggleScattererMeshes(bool enable)
        {
            int len = scatGos.Count;
            for (int i = 0; i < len; i++)
            {
                scatGos[i].layer = enable ? 23 : 15;
            }
        }

    }
}
