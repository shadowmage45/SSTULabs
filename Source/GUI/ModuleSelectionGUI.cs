using System;
using UnityEngine;

namespace SSTUTools
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class ModuleSelectionGUI : MonoBehaviour
    {

        private static int graphWidth = 640;
        private static int graphHeight = 250;
        private static int scrollHeight = 480;
        private static int margin = 20;
        private static int id;
        private static Rect windowRect = new Rect(Screen.width - 900, 40, graphWidth + margin, graphHeight + scrollHeight + margin);
        private static Vector2 scrollPos;
        private static Action<String, bool> modelSelectedCallback;
        private static ModelGUISelection[] adapters;
        private static bool guiOpen = false;
        private static bool shouldClose = false;
        //private static bool displayMass = true;
        //private static bool displayCost = true;
        //private static bool displayVolume = true;

        public static ModuleSelectionGUI INSTANCE;

        public void Start()
        {
            INSTANCE = this;
            GameObject.DontDestroyOnLoad(this);
            id = INSTANCE.GetInstanceID();
        }

        public void OnGUI()
        {
            if (shouldClose)
            {
                closeInternal();
            }
            else if (guiOpen)
            {
                updateGUI();
            }

        }

        /// <summary>
        /// Should be called by the PartModule to open the GUI.
        /// </summary>
        /// <param name="models"></param>
        public static void openGUI(ModelData[] models, float diameter, Action<String, bool> modelSelectedCB)
        {
            if (guiOpen)
            {
                throw new NotSupportedException("Cannot open a GUI when it is already open!");
            }

            EditorLogic editor = EditorLogic.fetch;
            if (editor != null) { editor.Lock(true, true, true, "SSTUModelSelectLock"); }

            adapters = ModelGUISelection.createFromModelData(models, diameter);
            modelSelectedCallback = modelSelectedCB;
            guiOpen = true;
            shouldClose = false;

            UIPartActionController.Instance.Deactivate();
        }

        /// <summary>
        /// Should be called by the PartModule to close the GUI.
        /// This cleans up resources that were initialized when the GUI was opened.
        /// </summary>
        public static void closeGUI()
        {
            shouldClose = true;
        }

        /// <summary>
        /// Should be called by the PartModule to update the GUI on every OnGUI Unity call.
        /// </summary>
        private static void updateGUI()
        {
            windowRect = GUI.Window(id, windowRect, updateWindow, "Model Selection");
            if (shouldClose) { closeInternal(); }
        }

        private static void closeInternal()
        {
            guiOpen = false;
            shouldClose = false;
            adapters = null;
            modelSelectedCallback = null;

            EditorLogic editor = EditorLogic.fetch;
            if (editor != null) { editor.Unlock("SSTUModelSelectLock"); }
            UIPartActionController.Instance.Activate();
        }

        private static void updateWindow(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Model", GUILayout.Width(128));
            GUILayout.Label("Description", GUILayout.Width(240));
            GUILayout.Label("Mass", GUILayout.Width(60));
            GUILayout.Label("Cost", GUILayout.Width(60));
            GUILayout.Label("Volume", GUILayout.Width(60));
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical();
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            addModelControls();
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close"))
            {
                closeGUI();
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        //private static Vector2 pivot = new Vector2();

        private static void addModelControls()
        {
            int len = adapters.Length;
            ModelGUISelection model;
            for (int i = 0; i < len; i++)
            {
                model = adapters[i];
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(model.texture, GUILayout.Width(128), GUILayout.Height(128)))
                {
                    adapterSelected(model);
                }
                GUILayout.Label(model.description, GUILayout.Width(240));
                GUILayout.Label(model.mass, GUILayout.Width(60));
                GUILayout.Label(model.cost, GUILayout.Width(60));
                GUILayout.Label(model.volume, GUILayout.Width(60));
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Internal call from GUI interaction when the 'select adapter' button is pressed for a specific adapter.
        /// Should issue a callback to the opening PartModule with info regarding what adapter was selected,
        /// and then close the GUI?
        /// </summary>
        /// <param name="adapterName"></param>
        private static void adapterSelected(ModelGUISelection selection)
        {
            modelSelectedCallback.Invoke(selection.modelName, true);
        }
    }

    public class ModelGUISelection
    {
        public readonly String modelName;
        public readonly String description;
        public readonly String mass;
        public readonly String cost;
        public readonly String volume;
        public readonly Texture texture;

        public ModelGUISelection(ModelData data, float size)
        {
            modelName = data.name;
            description = data.modelDefinition.title + " - " + data.modelDefinition.description;
            float m = data.mass;
            float c = data.cost;
            float v = data.volume;

            float scale = size / data.modelDefinition.diameter == 0 ? 1 : data.modelDefinition.diameter;

            float pow = Mathf.Pow(scale, 3);
            m *= pow;
            c *= pow;
            v *= pow;

            string suffix;
            suffix = m < 1 ? "kg" : m < 1000 ? "t" : "kt";
            m = m < 1 ? m * 1000 : m < 1000 ? m : m * 0.001f;
            mass = m.ToString("N1")+suffix;

            suffix = c < 1000 ? "" : "k";
            c = c > 1000 ? c * 0.001f : c;
            cost = c.ToString("N1")+suffix;

            suffix = v < 1 ? "l" : v < 1000 ? "kl" : "Ml";
            v = v < 1 ? v * 1000 : v < 1000 ? v : v * 0.001f;
            volume = v.ToString("N1")+suffix;


            if (String.IsNullOrEmpty(data.modelDefinition.icon) || (texture = GameDatabase.Instance.GetTexture(data.modelDefinition.icon, false))==null)
            {
                //TODO do textures need to be destroyed when no longer in use?  Can call unity asset-cleanup on GUI close?
                texture = GameDatabase.Instance.GetTexture("Squad/PartList/SimpleIcons/RDicon_propulsionSystems", false);
            }            
        }

        public static ModelGUISelection[] createFromModelData(ModelData[] data, float diameter)
        {            
            int len = data.Length;
            ModelGUISelection[] selections = new ModelGUISelection[len];
            for (int i = 0; i < len; i++)
            {
                selections[i] = new ModelGUISelection(data[i], diameter);
            }
            return selections;
        }
    }

}
