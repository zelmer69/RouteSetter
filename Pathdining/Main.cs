using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityModManagerNet;
using UnityEngine;
using DVModApi;
using Pathfinding.Graph;
using Pathfinding;
using DV.Logic.Job;


namespace Pathfinding
{
    public static class GraphHelper
    {
        public static Dictionary<TrackID, RailNode> GetGraph()
        {
            Debug.Log($"[GraphHelper] Reading from Main: main.instancetag is not a thing, generator null? {Main.generator == null}");
            if (Main.generator.Graph.Count == 0)
            {
                Main.generator.GenerateGraph();
            }
            return Main.generator.Graph;
        }
    }
    static class Main
    {
        public static GraphGenerator generator=null;
  
        static void Load()
        {

            
        }
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            
            DVModAPI.Setup(modEntry, FunctionType.OnGameLoad, OnGame);

            return true; 
        }
        private static void OnGame()
        {
            var go = new GameObject("BrakingDistanceEstimatorHost");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Debug.Log("Pathfidning initiated");
            generator = go.AddComponent<GraphGenerator>();
            Debug.Log($"[RouteSetter][Main] OnGame complete. generator null? {generator == null}");
        }
    }
}
