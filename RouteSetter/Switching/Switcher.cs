using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DV.Player;
using CommsRadioAPI;
using DV;
using DV.Logic.Job;

namespace RouteSetter
{
    enum StartTrackType
    {
        LastLoco =0,
        SelectedTrack=1
    }

    internal class Switcher
    {
        public static Dictionary<string, TrackNode> Graph;
        public Camera playerCamera;
        public static PathFinder pathFinder;
        public static RouteDrawer routeDrawer;
        public static bool RouteDisplayEnabled { get; set; } = true; // default: enabled
        public static TrackID SavedStartTrack { get; set; } = new TrackID("", "", "", "");
        public static TrackID SavedEndTrack { get; set; } = new TrackID("", "", "", "");
        public static StartTrackType StartTrackSetting { get; set; } = StartTrackType.LastLoco; 
        public void SetupPathFindingMode()
        {
            playerCamera = PlayerManager.PlayerCamera;
            pathFinder = playerCamera.gameObject.AddComponent<PathFinder>();
            routeDrawer = playerCamera.gameObject.AddComponent<RouteDrawer>();
            pathFinder.Generate();
            Graph = pathFinder.Graph;
            if (pathFinder.Graph == null)
                Debug.LogError("RouteSetterMod::Switcher -> Unable to generate network");
        }

        internal TrainCar GetTrainCar()
        {
            if (playerCamera == null)
                return null;
            return PlayerManager.LastLoco;
        }

        public void SetupRadioMode()
        {
            var commsRadioController = UnityEngine.Object.FindObjectOfType<CommsRadioController>();
            if (commsRadioController == null)
            {
                Debug.LogError("CommsRadioController is not initialized. Delaying radio mode setup.");
                CoroutineRunner.StartCoroutine(WaitForCommsRadioController());
                return;
            }
            CommsRadioMode mode = CommsRadioMode.Create(new RouteSetterMainState(), new Color(0, 0, 0));

            RouteSetterDebug.Log("Custom radio mode registered successfully.");
            SetupPathFindingMode();
        }

        private IEnumerator WaitForCommsRadioController()
        {
            while (UnityEngine.Object.FindObjectOfType<CommsRadioController>() == null)
                yield return null;
            SetupRadioMode();
        }
    }
}