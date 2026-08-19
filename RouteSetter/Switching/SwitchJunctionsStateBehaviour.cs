using CommsRadioAPI;
using DV.Logic.Job;
using Pathfinding;
using Pathfinding.Graph;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RouteSetter
{

    internal class SwitchJunctionsStateBehaviour : AStateBehaviour
    {
        private static bool TryFindNodeByTrackID(
        Dictionary<TrackID, RailNode> graph,
        TrackID target,
        out RailNode node,
        out TrackID matchedKey)
            {
            node = null;
            matchedKey = null;

            if (target == null)
                return false;

            foreach (var kvp in graph)
            {
                if (kvp.Key == null)
                    continue;

                if (string.Equals(kvp.Key.FullID, target.FullID, StringComparison.Ordinal))
                {
                    node = kvp.Value;
                    matchedKey = kvp.Key;
                    return true;
                }
            }

            return false;
        }


        public SwitchJunctionsStateBehaviour(
            string contextText = "Finding route",
            TrackID destination = null,
            string actionText = "Click to start")
            : base(new CommsRadioState("Switch Junctions", contextText, actionText))
        {
            Debug.Log($"[RouteSetter][SwitchJunctions] Constructed. contextText='{contextText}', " +
                $"destination={(destination == null ? "null" : destination.FullID)}, actionText='{actionText}'");
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            Debug.Log($"[RouteSetter][SwitchJunctions] OnAction called. action={action}");

            switch (action)
            {
                case InputAction.Down:
                    Debug.Log("[RouteSetter][SwitchJunctions] Down pressed, returning InitialStateBehaviour");
                    return new InitialStateBehaviour();

                case InputAction.Activate:
                    Debug.Log("[RouteSetter][SwitchJunctions] Activate pressed, starting FindPath");
                    return FindPath();

                default:
                    Debug.LogError($"[RouteSetter][SwitchJunctions] Unhandled InputAction: {action}");
                    throw new System.ArgumentException($"Unhandled InputAction: {action}");
            }
        }

        private SwitchJunctionsStateBehaviour FindPath()
        {
            Debug.Log("[RouteSetter][SwitchJunctions] FindPath: start");

            // --- Step 1: get the graph ---
            Dictionary<TrackID, RailNode> graph = null;
            try
            {
                graph = GraphHelper.GetGraph();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RouteSetter][SwitchJunctions] GraphHelper.GetGraph() threw: {ex}");
                return new SwitchJunctionsStateBehaviour("Error: failed to get graph (see log)");
            }

            if (graph == null)
            {
                Debug.LogError("[RouteSetter][SwitchJunctions] GraphHelper.GetGraph() returned null");
                return new SwitchJunctionsStateBehaviour("Error: graph is null");
            }
            Debug.Log($"[RouteSetter][SwitchJunctions] Graph loaded. Node count: {graph.Count}");

            // --- Step 2: check saved start/end tracks ---
            var start = PlayerManager.LastLoco.FrontBogie.track.LogicTrack().ID;
            var end = Switcher.SavedEndTrack;

            Debug.Log($"[RouteSetter][SwitchJunctions] SavedStartTrack={(start == null ? "null" : start.FullID)}, " +
                $"SavedEndTrack={(end == null ? "null" : end.FullID)}");

            if (start == null)
            {
                Debug.LogWarning("[RouteSetter][SwitchJunctions] Aborting: SavedStartTrack is null");
                return new SwitchJunctionsStateBehaviour("Start track not set");
            }
            if (end == null)
            {
                Debug.LogWarning("[RouteSetter][SwitchJunctions] Aborting: SavedEndTrack is null");
                return new SwitchJunctionsStateBehaviour("End track not set");
            }

            // --- Step 3: look up nodes in graph ---
            Debug.Log($"[RouteSetter][SwitchJunctions] Looking up start track '{start.FullID}' in graph " +
                $"(graph contains {graph.Count} keys, TrackID.Equals/GetHashCode override present: " +
                $"{typeof(TrackID).GetMethod("Equals", new[] { typeof(object) }).DeclaringType == typeof(TrackID)})");

            bool foundStart = TryFindNodeByTrackID(graph, start, out var startNode, out var matchedStartKey);
            Debug.Log($"[RouteSetter][SwitchJunctions] Start lookup result: found={foundStart}, node={(foundStart ? startNode?.ToString() : "N/A")}");

            bool foundEnd = TryFindNodeByTrackID(graph, end, out var endNode, out var matchedEndKey);
            Debug.Log($"[RouteSetter][SwitchJunctions] End lookup result: found={foundEnd}, node={(foundEnd ? endNode?.ToString() : "N/A")}");
            if (!foundStart)
            {
                Debug.LogWarning($"[RouteSetter][SwitchJunctions] Start track '{start.FullID}' not found in graph. " +
                    $"Dumping first 10 graph keys for comparison:");
                int i = 0;
                foreach (var key in graph.Keys)
                {
                    if (i++ >= 10) break;
                    Debug.Log($"[RouteSetter][SwitchJunctions]   graph key: {key?.FullID}");
                }
                return new SwitchJunctionsStateBehaviour("Start track not found in graph");
            }
            if (!foundEnd)
            {
                Debug.LogWarning($"[RouteSetter][SwitchJunctions] End track '{end.FullID}' not found in graph");
                return new SwitchJunctionsStateBehaviour("End track not found in graph");
            }

            if (startNode == null)
            {
                Debug.LogError("[RouteSetter][SwitchJunctions] startNode is null despite TryGetValue returning true");
                return new SwitchJunctionsStateBehaviour("Error: start node null");
            }
            if (endNode == null)
            {
                Debug.LogError("[RouteSetter][SwitchJunctions] endNode is null despite TryGetValue returning true");
                return new SwitchJunctionsStateBehaviour("Error: end node null");
            }

            // --- Step 4: run pathfinding ---
            path path = null;
            try
            {
                Debug.Log("[RouteSetter][SwitchJunctions] Calling Pathfiinder.FindPath...");
                 Pathfiinder.FindPathAndSwitchJunctions(start, end);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RouteSetter][SwitchJunctions] Pathfiinder.FindPath threw: {ex}");
                return new SwitchJunctionsStateBehaviour("Error: pathfinding threw (see log)");
            }

            if (path == null)
            {
                Debug.LogError("[RouteSetter][SwitchJunctions] Pathfiinder.FindPath returned null");
                return new SwitchJunctionsStateBehaviour("Error: no path returned");
            }

            Debug.Log($"[RouteSetter][SwitchJunctions] Path found. Nodes null? {path.Nodes == null}, " +
                $"count={path.Nodes?.Length.ToString() ?? "N/A"}, travelTime={path.TravelTime}, avgSpeed={path.AverageSpeed}");

            if (path.Nodes == null || path.Nodes.Length == 0)
            {
                Debug.LogWarning("[RouteSetter][SwitchJunctions] Path has no nodes");
                return new SwitchJunctionsStateBehaviour("Path found but empty");
            }
            string textpath="";
            foreach (var node in path.Nodes) { 
                 textpath = textpath + ""+node.ID.FullID+$"x{node.position.x} y:{node.position.z}->";
            }
            Debug.Log("[RouteSetter][SwitchJunctions] FindPath: success, returning result state");
            return new SwitchJunctionsStateBehaviour($"Path found: {path.Nodes.Length} waypoints: {textpath} ");
        }
    }
}