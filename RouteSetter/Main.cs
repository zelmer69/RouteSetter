using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityModManagerNet;

using HarmonyLib;
using UnityEngine;

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