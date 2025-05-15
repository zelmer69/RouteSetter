using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityModManagerNet;
using HarmonyLib;

namespace AutoPilot
{
    public class Main
    {
        public static bool Enabled;
        private static Switcher switcher;

        // Entry point for Unity Mod Manager
        public static void Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnToggle = OnToggle;

            UnityModManager.Logger.Log("Custom radio mode setup successfully.");
            // Initialize the Switcher instance
            switcher = new Switcher();
        }

        // Called when the mod is enabled/disabled in UMM
        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Enabled = value;
            if (Enabled)
            {
                // Set up the custom radio mode
                switcher.SetupRadioMode();
            }

            return true; // Return true if state change is successful
        }
    }
}
