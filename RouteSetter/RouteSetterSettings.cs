using System;
using UnityEngine;
using UnityModManagerNet;

namespace RouteSetter
{
    // Simple settings class for RouteSetter
    [Serializable]
    public class RouteSetterSettings : UnityModManager.ModSettings, IDrawable
    {


        [Draw("Enable Debug Logging")] public bool EnableDebugLogging = false;
        [Draw("Path Draw Distance. \n(Warning anything above 1000 can be heavy on performance!)")] public float DrawDistance= 500f;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public void Draw(UnityModManager.ModEntry modEntry)
        {
            var settings = this;
            UnityModManager.UI.DrawFields(ref settings, modEntry, DrawFieldMask.Public);
        }

        public void OnChange() { }
    }
    public static class RouteSetterDebug
    {
        public static void Log(string message)
        {
            if (Main.Settings.EnableDebugLogging)
            {
                Debug.Log(message);
            }
        }
        public static void LogWarning(string message)
        {
            if (Main.Settings.EnableDebugLogging)
            {
                Debug.LogWarning(message);
            }
         }
    }
}
