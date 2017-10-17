using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using System.IO;

namespace SSTUTools
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class SSTUReflectionManager : MonoBehaviour
    {

        #region CONSTANTS

        public const int galaxyMask = 1 << 18;
        public const int atmosphereMask = (1 << 9) | (1 << 23);
        public const int scaledSpaceMask = 1 << 10;
        public const int sceneryMask = (1 << 4) | (1 << 15);
        public const int fullSceneMask = ~0;

        #endregion

        #region CONFIG FIELDS

        /// <summary>
        /// Should static reflection maps be used?
        /// If true, reflection maps will only be rendered a single time on the scene initialization.
        /// If false, reflection maps will be updated at runtime with a frequency/delay specified by further config settings
        /// </summary>
        public bool useStaticMaps = false;

        /// <summary>
        /// Should a reflection probe be added per-part?
        /// If true, reflections are done on a per-part basis.
        /// If false, reflections are done on a per-vessel basis.
        /// </summary>
        public bool perPartMaps = false;

        /// <summary>
        /// Number of frames inbetween reflection map updates.
        /// </summary>
        public int mapUpdateSpacing = 1;

        /// <summary>
        /// Number of faces to happen on any given update.
        /// </summary>
        public int numberOfFaces = 1;

        /// <summary>
        /// Size of the rendered reflection map.  Higher resolutions result in higher fidelity reflections, but at a much higher run-time cost.
        /// Must be a power-of-two size; e.g. 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048.
        /// </summary>
        public int envMapSize = 128;
        
        // Skybox specific settings -- as the skybox is rendered and updated independently from the rest of the scene.
        // It can use different udpate frequency as well as resolution.
        // Rendered skybox includes only the galaxy and atmosphere color (and clouds when EVE is in use?).

        /// <summary>
        /// Should a static skybox image be used, or should it be updated at runtime?
        /// </summary>
        public bool useStaticSkybox = false;

        /// <summary>
        /// Number of frames inbetween updating of the skybox
        /// </summary>
        public int skyboxUpdateSpacing = 180;

        /// <summary>
        /// The number of faces to update on a single frame for the skybox
        /// </summary>
        public int skyboxFaceUpdates = 1;

        /// <summary>
        /// The resolution of the skybox texture
        /// </summary>
        public int skyboxSize = 256;

        #endregion

        #region DEBUG FIELDS

        //set through the reflection debug GUI

        public bool renderGalaxy = true;        
        public bool renderScaled = true;        
        public bool renderAtmo = true;        
        public bool renderScenery = true;

        #endregion

        #region INTERNAL FIELDS

        public GameObject cameraObject;
        public Camera reflectionCamera;

        /// <summary>
        /// Map of vessels and their reflection probe data
        /// </summary>
        public readonly Dictionary<Vessel, VesselReflectionData> vesselReflectionProbeDict = new Dictionary<Vessel, VesselReflectionData>();
        
        /// <summary>
        /// The reflection data for inside of the (current) editor.  Should be rebuilt whenever the editor is initialized, closed, or change
        /// </summary>
        public EditorReflectionData editorReflectionData;

        //Mod interop stuff

        public bool eveInstalled = true;//TODO -- load this value from config
        public CameraAlphaFix eveCameraFix;
        
        //internal data -- event handling, app-launcher button and debug-GUI handling

        private EventData<Vessel>.OnEvent vesselCreateEvent;
        private EventData<Vessel>.OnEvent vesselDestroyedEvent;

        private ReflectionDebugGUI gui;
        private ApplicationLauncherButton debugAppButton;
        private ApplicationLauncherButton controlsAppButton;

        private static SSTUReflectionManager instance;

        public static SSTUReflectionManager Instance
        {
            get
            {
                return instance;
            }
        }

        #endregion

        #region LIFECYCLE METHODS

        public void Awake()
        {
            instance = this;
            init();
            vesselCreateEvent = new EventData<Vessel>.OnEvent(vesselCreated);
            vesselDestroyedEvent = new EventData<Vessel>.OnEvent(vesselDestroyed);
            GameEvents.onVesselCreate.Add(vesselCreateEvent);
            GameEvents.onVesselDestroy.Add(vesselDestroyedEvent);

            Texture2D tex;
            if (debugAppButton == null && (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor))
            {
                tex = GameDatabase.Instance.GetTexture("Squad/PartList/SimpleIcons/RDIcon_fuelSystems-highPerformance", false);
                debugAppButton = ApplicationLauncher.Instance.AddModApplication(debugGuiEnable, debugGuiDisable, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT|ApplicationLauncher.AppScenes.SPH|ApplicationLauncher.AppScenes.VAB, tex);
            }
        }

        private void debugGuiEnable()
        {
            gui = gameObject.AddComponent<ReflectionDebugGUI>();
        }

        public void debugGuiDisable()
        {
            GameObject.Destroy(gui);
            gui = null;
        }

        public void OnDestroy()
        {
            if (instance == this) { instance = null; }
            if (vesselCreateEvent != null)
            {
                GameEvents.onVesselCreate.Remove(vesselCreateEvent);
            }
            if (vesselDestroyedEvent != null)
            {
                GameEvents.onVesselDestroy.Remove(vesselDestroyedEvent);
            }
            if (gui != null)
            {
                GameObject.Destroy(gui);
                gui = null;
            }
            //TODO proper resource cleanup -- is it even applicable if the lifetime of the class is the same as the lifetime of the application?
            //TODO do materials and render textures need to be released?
        }

        #endregion

        #region FUNCTIONAL METHODS

        private void init()
        {
            if (cameraObject == null)
            {
                cameraObject = new GameObject("TRReflectionCamera");
                reflectionCamera = cameraObject.AddComponent<Camera>();
                eveCameraFix = cameraObject.AddComponent<CameraAlphaFix>();
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameObject probeObject = new GameObject("SSTUReflectionProbe");
                ReflectionProbe probe = createProbe(probeObject);
                RenderTexture tex = createTexture(envMapSize);
                editorReflectionData = new EditorReflectionData(new ReflectionProbeData(probe, tex));
            }
            else if (HighLogic.LoadedSceneIsFlight)
            {
                //vessels are added thorugh an event as they are loaded
            }

            //TODO -- replace with custom baked skybox...
            //use in areas where other reflection probes don't make sense (space?)
            //RenderSettings.customReflection = customCubemap;

            //TODO -- pre-bake cubemap to use as the custom skybox in the reflection probe camera; this can be higher res and updated far less often (every couple of seconds?)
        }

        public void vesselCreated(Vessel v)
        {
            GameObject reflect = new GameObject("SSTUReflectionProbe");
            reflect.transform.parent = v.transform;
            reflect.transform.localPosition = Vector3.zero;
            ReflectionProbe probe = createProbe(reflect);
            RenderTexture tex = createTexture(envMapSize);
            ReflectionProbeData data = new ReflectionProbeData(probe, tex);
            VesselReflectionData d = new VesselReflectionData(v, data);
            vesselReflectionProbeDict.Add(v, d);
        }

        public void vesselDestroyed(Vessel v)
        {
            vesselReflectionProbeDict.Remove(v);
        }

        /// <summary>
        /// Unity per-frame update method.  Should update any reflection maps that need updating.
        /// </summary>
        public void Update()
        {
            reflectionCamera.enabled = true;
            reflectionCamera.clearFlags = CameraClearFlags.Depth;
            if (editorReflectionData != null)
            {
                renderCube(editorReflectionData.probeData, Vector3.zero);
            }
            else
            {
                foreach (VesselReflectionData d in vesselReflectionProbeDict.Values)
                {
                    renderCube(d.probeData, d.vessel.transform.position);
                }
            }
            reflectionCamera.enabled = false;

            //TODO convolution on cubemap
            //https://seblagarde.wordpress.com/2012/06/10/amd-cubemapgen-for-physically-based-rendering/

            //TODO conversion of existing textures:
            //https://www.marmoset.co/posts/pbr-texture-conversion/
        }

        #endregion

        #region UPDATE UTILITY METHODS

        private void renderCube(ReflectionProbeData data, Vector3 pos)
        {
            int faces = 63;// (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5);//all faces
            renderPartialCube(editorReflectionData.probeData.reflectionMap, faces, pos);
        }

        private void renderPartialCube(RenderTexture envMap, int faceMask, Vector3 partPos)
        {
            float nearClip = 0.3f;
            float farClip = 3.0e7f;
            for (int i = 0; i < 6; i++)
            {
                if ((i & faceMask) != 0)
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
                        renderCubeFace(envMap, face, partPos, atmosphereMask, nearClip, farClip);
                    }
                    if (renderScaled)
                    {
                        //scaled space
                        renderCubeFace(envMap, face, ScaledSpace.Instance.transform.position, scaledSpaceMask, nearClip, farClip);
                    }
                    if (renderScenery)
                    {
                        //scene
                        eveCameraFix.overwriteAlpha = eveInstalled;
                        renderCubeFace(envMap, face, partPos, sceneryMask, nearClip, farClip);
                        eveCameraFix.overwriteAlpha = false;
                    }
                }
            }
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

        private ReflectionProbe createProbe(GameObject host)
        {
            ReflectionProbe pr = host.AddComponent<ReflectionProbe>();
            pr.type = UnityEngine.Rendering.ReflectionProbeType.Cube;
            pr.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
            pr.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            pr.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            pr.hdr = false;
            pr.size = Vector3.one * 30;
            pr.resolution = envMapSize;
            pr.enabled = true;
            return pr;
        }

        private RenderTexture createTexture(int size)
        {
            RenderTexture tex = new RenderTexture(size, size, 24);
            tex.dimension = UnityEngine.Rendering.TextureDimension.Cube;
            tex.generateMips = true;
            return tex;
        }

        #endregion

        #region DEBUG CUBE RENDERING

        public void renderDebugCubes()
        {
            int size = envMapSize * 4;
            Cubemap map = new Cubemap(size, TextureFormat.ARGB32, false);
            Texture2D exportTex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            exportCubes(map, FlightIntegrator.ActiveVesselFI.Vessel.transform.position);
        }

        private void exportCubes(Cubemap debugCube, Vector3 pos)
        {
            reflectionCamera.enabled = true;
            float nearClip = reflectionCamera.nearClipPlane;
            float farClip = 3.0e7f;

            reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
            Color bg = reflectionCamera.backgroundColor;
            reflectionCamera.backgroundColor = Color.clear;

            renderCube(debugCube, GalaxyCubeControl.Instance.transform.position, galaxyMask, nearClip, farClip);
            exportCubemap(debugCube, "galaxy");
            renderCube(debugCube, ScaledSpace.Instance.transform.position, scaledSpaceMask, nearClip, farClip);
            exportCubemap(debugCube, "scaled");
            renderCube(debugCube, pos, sceneryMask, nearClip, farClip);
            exportCubemap(debugCube, "scene");
            renderCube(debugCube, pos, atmosphereMask, nearClip, farClip);
            exportCubemap(debugCube, "skybox");
            renderCube(debugCube, pos, fullSceneMask, nearClip, farClip);
            exportCubemap(debugCube, "full");
            reflectionCamera.backgroundColor = bg;

            //export the same as the active reflection setup
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
                    renderCubeFace(debugCube, face, pos, atmosphereMask, nearClip, farClip);
                }
                if (renderScenery)
                {
                    //scene
                    renderCubeFace(debugCube, face, pos, sceneryMask, nearClip, farClip);
                }
            }
            exportCubemap(debugCube, "reflect");
            reflectionCamera.enabled = false;
        }

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

        #endregion DEBUG RENDERING

        #region CONTAINER CLASSES

        public class VesselReflectionData
        {
            public readonly Vessel vessel;
            public readonly ReflectionProbeData probeData;
            public VesselReflectionData(Vessel v, ReflectionProbeData data)
            {
                this.vessel = v;
                this.probeData = data;
            }
        }

        public class EditorReflectionData
        {
            public readonly ReflectionProbeData probeData;
            public EditorReflectionData(ReflectionProbeData data)
            {
                this.probeData = data;
            }
        }

        public class ReflectionProbeData
        {
            public readonly ReflectionProbe probe;
            public readonly RenderTexture reflectionMap;
            public int updateFace = 0;
            public float lastUpdateTime = 0;
            public ReflectionProbeData(ReflectionProbe probe, RenderTexture envMap)
            {
                this.probe = probe;
                this.reflectionMap = envMap;
            }
        }

        //from unity post: https://forum.unity.com/threads/render-texture-alpha.2065/
        //potential fix to EVE writing 0 into alpha channel on areas subject to cloud textures
        //this should be somehow ran a single time -after- the last layer of a cube-side is rendered
        public class CameraAlphaFix : MonoBehaviour
        {
            private float alpha = 1.0f;
            private Material mat;
            public bool overwriteAlpha = false;
     
            public void Start()
            {
                mat = new Material(
                    "Shader \"Hidden/Clear Alpha\" {" +
                    "Properties { _Alpha(\"Alpha\", Float)=1.0 } " +
                    "SubShader {" +
                    "    Pass {" +
                    "        ZTest Always Cull Off ZWrite Off" +
                    "        ColorMask A" +
                    "        SetTexture [_Dummy] {" +
                    "            constantColor(0,0,0,[_Alpha]) combine constant }" +
                    "    }" +
                    "}" +
                    "}"
                );
            }

            public void OnPostRender()
            {
                if (overwriteAlpha)
                {
                    overwriteAlphaChannel();
                }
            }

            public void overwriteAlphaChannel()
            {
                GL.PushMatrix();
                GL.LoadOrtho();
                mat.SetFloat("_Alpha", alpha);
                mat.SetPass(0);
                GL.Begin(GL.QUADS);
                GL.Vertex3(0, 0, 0.1f);
                GL.Vertex3(1, 0, 0.1f);
                GL.Vertex3(1, 1, 0.1f);
                GL.Vertex3(0, 1, 0.1f);
                GL.End();
                GL.PopMatrix();
            }

        }

        #endregion

    }
}