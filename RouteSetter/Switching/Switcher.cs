using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DV.Player;
using CommsRadioAPI;
using DV;

namespace AutoPilot
{
    internal class Switcher
    {
        public static Dictionary<string, TrackNode> Graph;
        public Camera playerCamera;
        public static PathFinder pathFinder;

        public void SetupPathFindingMode()
        {
            playerCamera = PlayerManager.PlayerCamera;
            pathFinder = playerCamera.gameObject.AddComponent<PathFinder>();
            pathFinder.Generate();
            Graph = pathFinder.graph;
            if (pathFinder.graph == null)
                Debug.LogError("AutoPilotMod::Switcher -> Unable to generate network");
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
            CommsRadioMode mode = CommsRadioMode.Create(new InitialStateBehaviour(), new Color(0, 0, 0));

            Debug.Log("Custom radio mode registered successfully.");
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