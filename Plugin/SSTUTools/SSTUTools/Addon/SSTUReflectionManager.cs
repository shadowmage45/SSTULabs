using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUReflectionManager
    {

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

        public GameObject cameraObject;

        public Camera reflectionCamera;

        public readonly Cubemap skyboxMap;

        public readonly Dictionary<Part, PartReflectionData> partReflectionProbeDict = new Dictionary<Part, PartReflectionData>();
        public readonly Dictionary<Vessel, VesselReflectionData> vesselReflectionProbeDict = new Dictionary<Vessel, VesselReflectionData>();
        
        /// <summary>
        /// The reflection data for inside of the (current) editor.  Should be rebuilt whenever the editor is initialized, closed, or change
        /// </summary>
        public readonly EditorReflectionData editorReflectionData;

        public void OnAwake()
        {
            init();
            //GameEvents.onFlightReady
        }

        public void OnDestroy()
        {
            //TODO proper resource cleanup -- is it even applicable if the lifetime of the class is the same as the lifetime of the application?
        }

        private void init()
        {
            cameraObject = new GameObject("TRReflectionCamera");
            reflectionCamera = cameraObject.AddComponent<Camera>();
        }

        /// <summary>
        /// Add a part to the list of handled parts.
        /// It will be properly placed into the part/vessel handling queue as appropriate for the current game settings.
        /// </summary>
        /// <param name="part"></param>
        public void addPart(Part part)
        {

        }

        /// <summary>
        /// Remove a part from the list of handled parts.
        /// </summary>
        /// <param name="part"></param>
        public void removePart(Part part)
        {

        }

        /// <summary>
        /// Should dump all previous references on scene change.
        /// TODO -- proper cleanup of resources
        /// -- do materials / textures need to be de-referenced/cleaned up in any other fashion (dispose()?)
        /// </summary>
        public void onSceneChanged()
        {

        }

        /// <summary>
        /// Unity per-frame update method.  Should update any reflection maps that need updating.
        /// </summary>
        public void Update()
        {

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

        public class PartReflectionData
        {
            public readonly Part part;
            public readonly ReflectionProbeData probeData;
        }

        public class VesselReflectionData
        {
            public readonly Vessel vessel;
            public readonly ReflectionProbeData probeData;
        }

        public class EditorReflectionData
        {
            public readonly ReflectionProbeData probeData;
        }

        public class ReflectionProbeData
        {
            public readonly ReflectionProbe probe;
            public readonly Cubemap reflectionMap;
            public int updateFace = 0;
            public float lastUpdateTime = 0;
        }

    }
}