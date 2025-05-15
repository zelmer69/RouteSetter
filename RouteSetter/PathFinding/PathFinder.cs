using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AutoPilot
{
    public class PathFinder : MonoBehaviour
    {
        public Dictionary<string, TrackNode> graph = new Dictionary<string, TrackNode>();
        public Dictionary<string, List<(string trackId, int branchIndex)>> junctionConnections =
            new Dictionary<string, List<(string trackId, int branchIndex)>>();

        public TrackNode FindStationTrackByName(string stationName, string yard, string yardTrack)
        {
            foreach (var kvp in graph)
            {
                string key = kvp.Key;
                if (key.Contains(  stationName ) && key.Contains(yardTrack)&&key.Contains(yard))
                    return kvp.Value;
            }
            Debug.LogError($"PathFinder::FindStationTrackByName – no track found for station='{stationName}', yardTrack='{yardTrack}'");
            return null;
        }

        public static string GetRailTrackGraphID(RailTrack track)
        {
            return track == null ? null : track.GetInstanceID() + "_" + track.name;
        }

        public Dictionary<string, TrackNode> Generate()
        {
            graph.Clear();
            junctionConnections.Clear();

            var allTracks = UnityEngine.Object.FindObjectsOfType<RailTrack>();
            var idMap = BuildIdMap(allTracks);

            Debug.Log($"[AutoPilot] Found {allTracks.Length} rail tracks in the world");

            CreateTrackNodes(allTracks, idMap);
            WireTrackNeighborsAndJunctions(allTracks, idMap);
            MakeEdgesBidirectional();

            int junctionCount = graph.Values.Count(n => n.junction != null);
            Debug.Log($"[AutoPilot] Generated graph with {graph.Count} nodes and {junctionCount} junctions");
            Debug.Log($"[AutoPilot] Junction connections database has {junctionConnections.Count} entries");

            return graph;
        }

        private Dictionary<RailTrack, string> BuildIdMap(RailTrack[] allTracks)
        {
            var idMap = new Dictionary<RailTrack, string>(allTracks.Length);
            foreach (var railTrack in allTracks)
            {
                if (railTrack == null)
                    continue;
                string id = GetRailTrackGraphID(railTrack);
                idMap[railTrack] = id;
            }
            return idMap;
        }

        private void CreateTrackNodes(RailTrack[] allTracks, Dictionary<RailTrack, string> idMap)
        {
            foreach (var railTrack in allTracks)
            {
                if (railTrack == null)
                    continue;
                string id = idMap[railTrack];
                graph[id] = new TrackNode(id, railTrack.transform.position) { track = railTrack };
            }
        }

        private void WireTrackNeighborsAndJunctions(RailTrack[] allTracks, Dictionary<RailTrack, string> idMap)
        {
            foreach (var railTrack in allTracks)
            {
                if (railTrack == null)
                    continue;

                string fromId = idMap[railTrack];
                var node = graph[fromId];
                var neighbors = new List<string>();

                AddBranchIfPresent(railTrack.inBranch, idMap, neighbors);
                AddBranchIfPresent(railTrack.outBranch, idMap, neighbors);

                if (railTrack.inJunction != null)
                {
                    ProcessJunction(railTrack.inJunction, railTrack, idMap, neighbors, fromId);
                    node.junction = railTrack.inJunction;
                }
                else if (railTrack.outJunction != null)
                {
                    ProcessJunction(railTrack.outJunction, railTrack, idMap, neighbors, fromId);
                    if (node.junction == null)
                        node.junction = railTrack.outJunction;
                }

                node.neighbors = neighbors;
            }
        }

        private void AddBranchIfPresent(Junction.Branch branch, Dictionary<RailTrack, string> idMap, List<string> neighbors)
        {
            if (branch?.track == null)
                return;
            if (idMap.TryGetValue(branch.track, out var branchId))
                neighbors.Add(branchId);
        }

        private void MakeEdgesBidirectional()
        {
            foreach (var kv in graph)
            {
                var fromId = kv.Key;
                foreach (var toId in kv.Value.neighbors)
                {
                    if (graph.ContainsKey(toId) && !graph[toId].neighbors.Contains(fromId))
                        graph[toId].neighbors.Add(fromId);
                }
            }
        }

        private void ProcessJunction(Junction junction, RailTrack track, Dictionary<RailTrack, string> idMap,
                                     List<string> neighbors, string trackId)
        {
            if (junction == null || junction.outBranches == null)
                return;

            string junctionId = trackId;
            if (!junctionConnections.ContainsKey(junctionId))
                junctionConnections[junctionId] = new List<(string, int)>();

            for (int i = 0; i < junction.outBranches.Count; i++)
            {
                var branch = junction.outBranches[i];
                if (branch?.track == null)
                    continue;
                if (!idMap.TryGetValue(branch.track, out var branchId))
                    continue;

                neighbors.Add(branchId);
                junctionConnections[junctionId].Add((branchId, i));
            }

            if (junction.inBranch?.track != null && idMap.TryGetValue(junction.inBranch.track, out var inBranchId))
            {
                neighbors.Add(inBranchId);
                junctionConnections[junctionId].Add((inBranchId, -1));
            }
        }

        public List<string> FindShortestPath(string start, string goal)
        {
            if (!IsValidPathEndpoints(start, goal))
                return null;

            var prev = new Dictionary<string, string>();
            var dist = new Dictionary<string, float>();
            var unvisited = new HashSet<string>(graph.Keys);

            foreach (var node in graph.Keys)
                dist[node] = float.MaxValue;
            dist[start] = 0f;

            Debug.Log($"[AutoPilot] FindShortestPath from {start} to {goal}");

            while (unvisited.Count > 0)
            {
                string current = GetClosestUnvisited(dist, unvisited);
                if (current == null || dist[current] == float.MaxValue)
                    break;
                if (current == goal)
                    break;

                unvisited.Remove(current);

                foreach (var neighbor in graph[current].neighbors)
                {
                    if (!graph.ContainsKey(neighbor))
                        continue;

                    float edgeCost = Vector3.Distance(graph[current].position, graph[neighbor].position);
                    if (graph[current].junction != null || graph[neighbor].junction != null)
                        edgeCost += 10f;

                    float alt = dist[current] + edgeCost;
                    if (alt < dist[neighbor])
                    {
                        dist[neighbor] = alt;
                        prev[neighbor] = current;
                    }
                }
            }

            if (!prev.ContainsKey(goal) && start != goal)
                return null;

            return ReconstructPath(prev, start, goal);
        }

        private bool IsValidPathEndpoints(string start, string goal)
        {
            if (string.IsNullOrEmpty(start) || string.IsNullOrEmpty(goal))
            {
                Debug.LogError("Start or goal node is null or empty.");
                return false;
            }
            if (!graph.ContainsKey(start) || !graph.ContainsKey(goal))
            {
                Debug.LogError($"Invalid endpoints: start={start}, goal={goal}");
                return false;
            }
            return true;
        }

        private List<string> ReconstructPath(Dictionary<string, string> prev, string start, string goal)
        {
            var path = new List<string>();
            string nodeCursor = goal;
            while (nodeCursor != start)
            {
                path.Insert(0, nodeCursor);
                nodeCursor = prev[nodeCursor];
            }
            path.Insert(0, start);

            Debug.Log($"[AutoPilot] Path found: {string.Join(" -> ", path)}");
            return path;
        }

        private string GetClosestUnvisited(Dictionary<string, float> dist, HashSet<string> unvisited)
        {
            string best = null;
            float bestDist = float.MaxValue;
            foreach (var u in unvisited)
            {
                if (dist[u] < bestDist)
                {
                    bestDist = dist[u];
                    best = u;
                }
            }
            return best;
        }

        public List<string> FindShortestPathBFS(string start, string goal)
        {
            if (!IsValidPathEndpoints(start, goal))
                return null;

            var prev = new Dictionary<string, string>();
            var visited = new HashSet<string> { start };
            var queue = new Queue<string>();
            queue.Enqueue(start);

            Debug.Log($"[AutoPilot] BFS from {start} to {goal}");

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == goal)
                    break;
                foreach (var nb in graph[cur].neighbors)
                {
                    if (visited.Add(nb))
                    {
                        prev[nb] = cur;
                        queue.Enqueue(nb);
                    }
                }
            }
            if (start != goal && !prev.ContainsKey(goal))
                return null;

            var path = new List<string>();
            string cursor = goal;
            while (true)
            {
                path.Insert(0, cursor);
                if (cursor == start) break;
                if (!prev.TryGetValue(cursor, out cursor))
                    return null;
            }
            Debug.Log($"[AutoPilot] Path: {string.Join(" -> ", path)}");

            // Only log summary for enhanced path
            EnhancePathWithJunctionData(path);

            return path;
        }

        private void EnhancePathWithJunctionData(List<string> path)
        {
            if (path == null || path.Count < 2)
                return;

            int enhancedCount = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                string currentId = path[i];
                string nextId = path[i + 1];

                if (!graph.TryGetValue(currentId, out var node) || node.junction == null)
                    continue;

                if (!junctionConnections.TryGetValue(currentId, out var connections))
                    continue;

                if (connections.Any(c => c.trackId == nextId && c.branchIndex >= 0))
                {
                    enhancedCount++;
                    continue;
                }

                foreach (var (branchTrackId, branchIndex) in connections.Where(c => c.branchIndex >= 0))
                {
                    int branchTrackPosInPath = path.IndexOf(branchTrackId);
                    if (branchTrackPosInPath > i)
                    {
                        enhancedCount++;
                        break;
                    }

                    if (!graph.TryGetValue(branchTrackId, out var branchNode))
                        continue;

                    foreach (string neighborId in branchNode.neighbors)
                    {
                        int neighborPosInPath = path.IndexOf(neighborId);
                        if (neighborPosInPath > i)
                        {
                            enhancedCount++;
                            break;
                        }
                    }
                }
            }
            if (enhancedCount > 0)
                Debug.Log($"[AutoPilot] Enhanced {enhancedCount} path segments with junction data.");
        }

        public int GetBranchForJunction(string junctionTrackId, List<string> path)
        {
            if (string.IsNullOrEmpty(junctionTrackId) || path == null || path.Count < 2)
                return -1;

            int posInPath = path.IndexOf(junctionTrackId);
            if (posInPath < 0 || posInPath >= path.Count - 1)
                return -1;

            string nextTrackId = path[posInPath + 1];

            if (!junctionConnections.TryGetValue(junctionTrackId, out var connections))
                return -1;

            foreach (var (trackId, branchIndex) in connections)
            {
                if (trackId == nextTrackId && branchIndex >= 0)
                    return branchIndex;
            }

            int bestBranch = -1;
            int bestScore = -1;

            foreach (var (branchTrackId, branchIndex) in connections.Where(c => c.branchIndex >= 0))
            {
                if (branchIndex < 0)
                    continue;

                int branchPosInPath = path.IndexOf(branchTrackId);
                if (branchPosInPath > posInPath)
                {
                    int score = 1000 - (branchPosInPath - posInPath);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestBranch = branchIndex;
                    }
                    continue;
                }

                if (!graph.TryGetValue(branchTrackId, out var branchNode))
                    continue;

                foreach (string neighborId in branchNode.neighbors)
                {
                    int neighborPosInPath = path.IndexOf(neighborId);
                    if (neighborPosInPath > posInPath)
                    {
                        int score = 500 - (neighborPosInPath - posInPath);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestBranch = branchIndex;
                        }
                        break;
                    }
                }
            }

            return bestBranch;
        }
    }
}
