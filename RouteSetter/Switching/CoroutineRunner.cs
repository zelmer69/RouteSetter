using System.Collections;
using UnityEngine;

namespace RouteSetter
{
    internal static class CoroutineRunner
    {
        private static GameObject runnerObject;

        public static void StartCoroutine(IEnumerator coroutine)
        {
            if (runnerObject == null)
            {
                runnerObject = new GameObject("CoroutineRunner");
                UnityEngine.Object.DontDestroyOnLoad(runnerObject);
                runnerObject.AddComponent<CoroutineRunnerComponent>();
            }
            runnerObject.GetComponent<CoroutineRunnerComponent>().StartCoroutine(coroutine);
        }

        private class CoroutineRunnerComponent : MonoBehaviour { }
    }
}
