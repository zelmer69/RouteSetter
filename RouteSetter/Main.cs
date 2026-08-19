using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityModManagerNet;

using HarmonyLib;
using UnityEngine;
using RouteSetter.ETA;

namespace RouteSetter
{
    public class Main
    {
        public static bool Enabled;
        private static Switcher switcher;
        public static RouteSetterSettings Settings;
        
        public static void Load(UnityModManager.ModEntry modEntry)
        {

            Settings = UnityModManagerNet.UnityModManager.ModSettings.Load<RouteSetterSettings>(modEntry);

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            UnityModManager.Logger.Log("Route setter setup successfully.");
            switcher = new Switcher();
            var go = new GameObject("BrakingDistanceEstimatorHost");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<BrakingDistanceEstimator>();
        }
        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Enabled = value;
            if (Enabled)
            {
                switcher.SetupRadioMode();
            }
            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Draw(modEntry);
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            
            Settings.Save(modEntry);
        }
    }
}
