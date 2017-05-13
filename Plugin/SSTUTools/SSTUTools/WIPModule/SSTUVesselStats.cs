using System;
using UnityEngine;

namespace SSTUTools
{
    //[KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class SSTUVesselStats : MonoBehaviour
    {

        public static SSTUVesselStats INSTANCE;

        private static double orbitalPeriod;
        private static double apoapsis;
        private static double periapsis;
        private static double inclination;
        private static double orbitalVelocity;
        private static StageData[] deltaVData;

        private static int graphWidth = 640;
        private static int graphHeight = 250;
        private static int scrollHeight = 480;
        private static int margin = 20;
        private static int id = 0;
        private static bool guiOpen = true;
        private static Rect windowRect = new Rect(Screen.width - 900, 40, graphWidth + margin, graphHeight + scrollHeight + margin);
        private static Vector2 scrollPos = Vector2.zero;

        public void Start()
        {
            INSTANCE = this;
            GameObject.DontDestroyOnLoad(this);
            MonoBehaviour.print("SSTUVesselStats Start");
            guiOpen = true;
        }

        public void OnGUI()
        {
            if (guiOpen)
            {
                GUI.Window(id, windowRect, drawWindow, "VesselStats");
            }
        }

        private void drawWindow(int id)
        {
            if (HighLogic.LoadedSceneIsEditor) { drawEditor(); }
            else if(HighLogic.LoadedSceneIsFlight) { drawFlight(); }
            GUI.DragWindow();
        }

        private void drawEditor()
        {

        }

        private void drawFlight()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.orbit==null) { return; }
            orbitalPeriod = v.orbit.period;
            apoapsis = v.orbit.ApA;
            periapsis = v.orbit.ApR;
            inclination = v.orbit.inclination;
            orbitalVelocity = v.orbit.vel.magnitude;
            GUILayout.BeginVertical();

            //apo
            GUILayout.BeginHorizontal();
            GUILayout.Label("Apoapsis");
            GUILayout.Label(apoapsis.ToString("N2"));
            GUILayout.EndHorizontal();

            //peri
            GUILayout.BeginHorizontal();
            GUILayout.Label("Periapsis");
            GUILayout.Label(periapsis.ToString("N2"));
            GUILayout.EndHorizontal();

            //orbital period
            GUILayout.BeginHorizontal();
            GUILayout.Label("Period");
            GUILayout.Label(orbitalPeriod.ToString("N2"));
            GUILayout.EndHorizontal();

            //vel
            GUILayout.BeginHorizontal();
            GUILayout.Label("Velocity");
            GUILayout.Label(orbitalVelocity.ToString("N2"));
            GUILayout.EndHorizontal();

            //inc
            GUILayout.BeginHorizontal();
            GUILayout.Label("Inclination");
            GUILayout.Label(inclination.ToString("N2"));
            GUILayout.EndHorizontal();

            GUILayout.BeginScrollView(scrollPos);
            drawStageStats();
            GUILayout.EndScrollView();
            if (GUILayout.Button("Update Stats"))
            {
                calculateDeltaV();
            }
            GUILayout.EndVertical();
        }

        private void drawStageStats()
        {

        }

        private void calculateDeltaV()
        {
            //int stages = FlightGlobals.ActiveVessel.parts
        }

    }

    public class StageData
    {
        private int stage;
        private double vacDv;
        private double atmDv;
        private double vacT;
        private double atmT;
        private double vacTwr;
        private double atmTwr;
        private double burnTime;
        private double dryMass;
        private double payloadMass;
        private double fuelMass;
    }

}
