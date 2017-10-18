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

        public bool reflectionsEnabled = false;

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

        private bool renderedEditor = false;
        
        private EventData<Vessel>.OnEvent vesselCreateEvent;
        private EventData<Vessel>.OnEvent vesselDestroyedEvent;

        private ReflectionDebugGUI gui;
        private static ApplicationLauncherButton debugAppButton;

        private static Shader skyboxShader;

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
            MonoBehaviour.print("SSTUReflectionManager Awake()");
            instance = this;
            init();
            vesselCreateEvent = new EventData<Vessel>.OnEvent(vesselCreated);
            vesselDestroyedEvent = new EventData<Vessel>.OnEvent(vesselDestroyed);
            GameEvents.onVesselCreate.Add(vesselCreateEvent);
            GameEvents.onVesselDestroy.Add(vesselDestroyedEvent);

            Texture2D tex;
            if (debugAppButton == null)
            {
                //create a new button
                tex = GameDatabase.Instance.GetTexture("Squad/PartList/SimpleIcons/RDIcon_fuelSystems-highPerformance", false);
                debugAppButton = ApplicationLauncher.Instance.AddModApplication(debugGuiEnable, debugGuiDisable, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.VAB, tex);
            }
            else
            {
                //reseat callback refs to the ones from THIS instance of the KSPAddon (old refs were stale, pointing to methods for a deleted class instance)
                debugAppButton.onEnable = debugGuiEnable;
                debugAppButton.onDisable = debugGuiDisable;
            }
        }

        /// <summary>
        /// Unity per-frame update method.  Should update any reflection maps that need updating.
        /// </summary>
        public void Update()
        {
            if (!reflectionsEnabled) { return; }
            renderCubes();

            //TODO convolution on cubemap
            //https://seblagarde.wordpress.com/2012/06/10/amd-cubemapgen-for-physically-based-rendering/
            //http://codeflow.org/entries/2011/apr/18/advanced-webgl-part-3-irradiance-environment-map/
            //https://developer.nvidia.com/gpugems/GPUGems2/gpugems2_chapter10.html
            //https://gist.github.com/Farfarer/5664694
            //https://codegists.com/code/render-cubemap-unity/

            //TODO conversion of existing textures:
            //https://www.marmoset.co/posts/pbr-texture-conversion/

            //https://forum.unity.com/threads/directly-draw-a-cubemap-rendertexture.296236/
            //In function Graphics.SetRenderTarget(), you can select cubemap face. 

            //convolution processing:
            //1.) CPU based
            //      Render to standard Cubemap
            //      Sample and convolve in CPU
            //      Update Cubemap from convolved data
            //      
            //2.) GPU based
            //      Shader has single Cubemap input from raw rendered (no MIPs)
            //      Shader samples cubemap, renders out to standard surface rendertexture, one face at a time
            //      Recompose the 6x render textures back into a single Cubemap (with MIPs)

            //debug code below here
            //if (true) { return; }
            ////GPU based convolution
            //int mipLevel = 0;
            //Material mat;
            //RenderTexture staticMap = null;//the cubemap that the camera renders into; mipmaps disabled for single LOD / base sample
            //RenderTexture convoluted = null;//the output cubemap that we will render into
            //mat.SetTexture("_Input", staticMap);
            //mat.SetFloat("_Level", mipLevel);
            ////run convolution pass on each face
            //for (int face = 0; face < 6; face++)
            //{
            //    Graphics.SetRenderTarget(convoluted, mipLevel, (CubemapFace)face);
            //    //Graphics.SetRenderTarget()
            //    mat.SetFloat("_Face", face);
            //    GL.PushMatrix();
            //    GL.LoadOrtho();
            //    mat.SetPass(0);
            //    GL.Begin(GL.QUADS);
            //    GL.Vertex3(0, 0, 0.5f);
            //    GL.Vertex3(1, 0, 0.5f);
            //    GL.Vertex3(1, 1, 0.5f);
            //    GL.Vertex3(0, 1, 0.5f);
            //    GL.End();
            //    GL.PopMatrix();
            //}
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
            MonoBehaviour.print("SSTUReflectionManager OnDestroy()");
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
            MonoBehaviour.print("SSTUReflectionManager init()");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("TRReflectionCamera");
                reflectionCamera = cameraObject.AddComponent<Camera>();
                eveCameraFix = cameraObject.AddComponent<CameraAlphaFix>();
                reflectionCamera.enabled = false;
                MonoBehaviour.print("SSTUReflectionManager created camera: "+reflectionCamera);
            }
            if (skyboxShader == null)
            {
                skyboxShader = SSTUDatabase.getShader("SSTU/Skybox/Cubemap");
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameObject probeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                probeObject.name = "SSTUReflectionProbe";
                Material probeMat = new Material(skyboxShader);
                ReflectionProbe probe = createProbe(probeObject);
                probe.size = new Vector3(50, 150, 50);
                RenderTexture tex = createTexture(envMapSize);
                editorReflectionData = new EditorReflectionData(new ReflectionProbeData(probeObject, probeMat, probe, tex));
                MonoBehaviour.print("SSTUReflectionManager created editor reflection data: " + probeObject + " :: " + probe + " :: " + tex + " :: "+editorReflectionData);
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

        public void vesselCreated(Vessel vessel)
        {
            GameObject probeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            probeObject.name = "SSTUReflectionProbe";
            probeObject.transform.parent = vessel.transform;
            probeObject.transform.localPosition = Vector3.zero;
            probeObject.layer = 26;//wheel collider ignore
            Material probeMat = new Material(skyboxShader);
            VesselReflectionData d = new VesselReflectionData(vessel, new ReflectionProbeData(probeObject, probeMat, createProbe(probeObject), createTexture(envMapSize)));
            vesselReflectionProbeDict.Add(vessel, d);
            MonoBehaviour.print("SSTUReflectionManager vesselCreated() : " + vessel+" :: "+d);
        }

        public void vesselDestroyed(Vessel v)
        {
            MonoBehaviour.print("SSTUReflectionManager vesselDestroyed() : " + v);
            vesselReflectionProbeDict.Remove(v);
        }

        public void renderCubes()
        {
            reflectionCamera.enabled = true;
            reflectionCamera.clearFlags = CameraClearFlags.Depth;
            if (editorReflectionData != null)
            {
                if (!renderedEditor)
                {
                    renderedEditor = true;
                    renderCube(editorReflectionData.probeData, new Vector3(0, 10, 0));
                }
            }
            else
            {
                foreach (VesselReflectionData d in vesselReflectionProbeDict.Values)
                {
                    if (d.vessel.loaded)
                    {
                        renderCube(d.probeData, d.vessel.transform.position);
                    }
                }
            }
            reflectionCamera.enabled = false;
        }

        #endregion

        #region UPDATE UTILITY METHODS

        private void renderCube(ReflectionProbeData data, Vector3 pos)
        {
            int faces = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5);//all faces
            renderPartialCube(data.renderedCube, faces, pos);
            data.skyboxMateral.SetTexture("_Tex", data.renderedCube);            
            data.probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            data.probe.cullingMask = 1<<26;//wheelColliderIgnore layer
            data.probe.RenderProbe();
        }

        private void renderPartialCube(RenderTexture envMap, int faceMask, Vector3 partPos)
        {
            float nearClip = 0.3f;
            float farClip = 3.0e7f;
            for (int i = 0; i < 6; i++)
            {
                int face = 1 << i;
                if (renderGalaxy)
                {
                    //galaxy
                    renderCubeFace(envMap, face, GalaxyCubeControl.Instance.transform.position, galaxyMask, nearClip, farClip);
                }
                //TODO -- scaled and atmo need to be rendered in oposite order while in orbit
                if (renderScaled)
                {
                    //scaled space
                    renderCubeFace(envMap, face, ScaledSpace.Instance.transform.position, scaledSpaceMask, nearClip, farClip);
                }
                if (renderAtmo)
                {
                    //atmo
                    renderCubeFace(envMap, face, partPos, atmosphereMask, nearClip, farClip);
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

        private void renderCubeFace(RenderTexture envMap, int faceMask, Vector3 cameraPos, int layerMask, float nearClip, float farClip)
        {
            cameraSetup(cameraPos, layerMask, nearClip, farClip);
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
            Vector3 pos = HighLogic.LoadedSceneIsEditor ? new Vector3(0, 10, 0) : FlightIntegrator.ActiveVesselFI.Vessel.transform.position;
            exportCubes(map, pos);
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
                    eveCameraFix.overwriteAlpha = eveInstalled;
                    renderCubeFace(debugCube, face, pos, sceneryMask, nearClip, farClip);
                    eveCameraFix.overwriteAlpha = false;
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
            public readonly GameObject reflectionSphere;//also the owner of the probe
            public readonly ReflectionProbe probe;
            public readonly RenderTexture renderedCube;
            public readonly Material skyboxMateral;
            public ReflectionProbeData(GameObject sphere, Material mat, ReflectionProbe probe, RenderTexture envMap)
            {
                this.reflectionSphere = sphere;
                this.skyboxMateral = mat;
                this.probe = probe;
                this.renderedCube = envMap;
            }
        }

        //from unity post: https://forum.unity.com/threads/render-texture-alpha.2065/
        //potential fix to EVE writing 0 into alpha channel on areas subject to cloud textures
        //this should be somehow ran a single time -after- the last layer of a cube-side is rendered
        //had to move to a pre-compiled shader as apparently run-time compilation is completely unsupported now
        public class CameraAlphaFix : MonoBehaviour
        {
            private float alpha = 1.0f;
            private Material mat;
            public bool overwriteAlpha = true;
     
            public void Start()
            {
                Shader setAlpha = SSTUDatabase.getShader("SSTU/SetAlpha");
                mat = new Material(setAlpha);
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
                GL.Vertex3(0, 0, 0.5f);
                GL.Vertex3(1, 0, 0.5f);
                GL.Vertex3(1, 1, 0.5f);
                GL.Vertex3(0, 1, 0.5f);
                GL.End();
                GL.PopMatrix();
            }

        }

        #endregion

    }
}