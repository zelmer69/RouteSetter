using System;
using System.Collections.Generic;
using System.Linq;
using DV.OriginShift;
using UnityEngine;

namespace RouteSetter
{
    #region ─── Helper Classes ───────────────────────────────────────────────────────

    /// <summary>
    /// Represents a node in the track graph with directional information
    /// </summary>
    public struct DirectedState : IEquatable<DirectedState>
    {
        public readonly string Current;
        public readonly string Previous;

        public DirectedState(string current, string previous)
        {
            Current = current;
            Previous = previous;
        }

        public bool Equals(DirectedState other) =>
            Current == other.Current && Previous == other.Previous;

        public override bool Equals(object obj) =>
            obj is DirectedState ds && Equals(ds);

        public override int GetHashCode() =>
            (Current?.GetHashCode() ?? 0) * 397 ^ (Previous?.GetHashCode() ?? 0);
    }

    /// <summary>
    /// Optimized binary-heap priority queue implementation
    /// </summary>
    public class MinPriorityQueue<T>
    {
        private readonly List<(T item, float priority)> _heap = new List<(T, float)>();

        public int Count => _heap.Count;

        public void Enqueue(T item, float priority)
        {
            _heap.Add((item, priority));
            int i = _heap.Count - 1;

            // Sift up
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_heap[parent].priority <= _heap[i].priority) break;

                // Swap with parent
                (_heap[parent], _heap[i]) = (_heap[i], _heap[parent]);
                i = parent;
            }
        }

        public (T item, float priority) Dequeue()
        {
            if (_heap.Count == 0) throw new InvalidOperationException("Queue is empty");

            var root = _heap[0];
            var last = _heap[_heap.Count - 1];
            _heap.RemoveAt(_heap.Count - 1);

            if (_heap.Count == 0) return root;

            // Place last element at root and sift down
            _heap[0] = last;
            int i = 0;

            while (true)
            {
                int left = 2 * i + 1;
                int right = left + 1;
                int smallest = i;

                if (left < _heap.Count && _heap[left].priority < _heap[smallest].priority)
                    smallest = left;

                if (right < _heap.Count && _heap[right].priority < _heap[smallest].priority)
                    smallest = right;

                if (smallest == i) break;

                // Swap with smallest child
                (_heap[i], _heap[smallest]) = (_heap[smallest], _heap[i]);
                i = smallest;
            }

            return root;
        }

        public bool IsEmpty() => _heap.Count == 0;

        public void Clear() => _heap.Clear();
    }

    /// <summary>
    /// Represents a node in the rail network
    /// </summary>
    public class TrackNode
    {
        public string Id { get; }
        public Vector3 Position { get; }
        public RailTrack Track { get; set; }
        public Junction Junction { get; set; }
        public List<string> Neighbors { get; set; } = new List<string>();

        public TrackNode(string id, Vector3 position)
        {
            Id = id;
            Position = position;
        }
    }

    #endregion

    /// <summary>
    /// Available pathfinding modes
    /// </summary>
    public enum PathFindingMode
    {
        Dijkstra,               // Classic shortest path
        DijkstraWithoutUTurns,  // No U-turns or Y-turns allowed
        BFS                     // Breadth-first search (fastest to compute)
    }

    /// <summary>
    /// Pathfinding system for rail networks
    /// </summary>
    public class PathFinder : MonoBehaviour
    {
        private const float UTURN_PENALTY = 3_000_000f;
        private const int RECENT_TRACK_MEMORY = 5; // How many tracks to remember for U-turn detection

        // The graph of track nodes
        public Dictionary<string, TrackNode> Graph { get; private set; } = new Dictionary<string, TrackNode>();

        // Stores junction connection information
        public Dictionary<string, List<(string trackId, int branchIndex)>> JunctionConnections { get; private set; } =
            new Dictionary<string, List<(string trackId, int branchIndex)>>();

        #region ─── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the rail network graph
        /// </summary>
        public Dictionary<string, TrackNode> Generate()
        {
            Graph.Clear();
            JunctionConnections.Clear();

            // Get all rail tracks in the scene
            var allTracks = UnityEngine.Object.FindObjectsOfType<RailTrack>();
            var idMap = BuildTrackIdMap(allTracks);

            Debug.Log($"[PathFinder] Found {allTracks.Length} rail tracks in the world");

            // Build the graph
            CreateTrackNodes(allTracks, idMap);
            WireTrackConnectionsAndJunctions(allTracks, idMap);
            MakeEdgesBidirectional();

            int junctionCount = Graph.Values.Count(n => n.Junction != null);
            Debug.Log($"[PathFinder] Generated graph with {Graph.Count} nodes and {junctionCount} junctions");
            Debug.Log($"[PathFinder] Junction connections database has {JunctionConnections.Count} entries");

            return Graph;
        }

        /// <summary>
        /// Finds a station track by name, yard, and yard track
        /// </summary>
        public TrackNode FindStationTrackByName(string stationName, string yard, string yardTrack)
        {
            foreach (var kvp in Graph)
            {
                string key = kvp.Key;
                if (key.Contains(stationName) && key.Contains(yardTrack) && key.Contains(yard))
                    return kvp.Value;
            }

            Debug.LogError($"PathFinder::FindStationTrackByName – no track found for station='{stationName}', yard='{yard}', yardTrack='{yardTrack}'");
            return null;
        }

        /// <summary>
        /// Method 1: Classic Dijkstra - finds shortest path ignoring U-turns
        /// </summary>
        public List<string> FindShortestPath(string start, string goal)
        {
            if (!ValidatePathEndpoints(start, goal))
                return null;

            var distances = new Dictionary<string, float>();
            var previous = new Dictionary<string, string>();
            var priorityQueue = new MinPriorityQueue<string>();
            var visited = new HashSet<string>();

            // Initialize distances
            foreach (var node in Graph.Keys)
                distances[node] = float.MaxValue;

            distances[start] = 0;
            priorityQueue.Enqueue(start, 0);

            Debug.Log($"[PathFinder] Finding shortest path from {start} to {goal}");

            while (!priorityQueue.IsEmpty())
            {
                var (currentNode, currentDistance) = priorityQueue.Dequeue();

                // Skip if we've already processed this node or found a better path
                if (visited.Contains(currentNode) || currentDistance > distances[currentNode])
                    continue;

                visited.Add(currentNode);

                // If we reached the goal, we're done
                if (currentNode == goal)
                    break;

                foreach (var neighbor in Graph[currentNode].Neighbors)
                {
                    if (!Graph.ContainsKey(neighbor))
                        continue;

                    float edgeCost = CalculateEdgeCost(currentNode, neighbor);
                    float newDistance = distances[currentNode] + edgeCost;

                    if (newDistance < distances[neighbor])
                    {
                        distances[neighbor] = newDistance;
                        previous[neighbor] = currentNode;
                        priorityQueue.Enqueue(neighbor, newDistance);
                    }
                }
            }

            return ReconstructPath(previous, start, goal);
        }

        /// <summary>
        /// Comprehensive solution to find shortest path without U-turns or Y-turns
        /// </summary>
        public List<string> FindShortestPathNoUTurnsOrYTurns(string start, string goal)
        {
            if (!ValidatePathEndpoints(start, goal))
                return null;

            // Key data structures
            var distanceMap = new Dictionary<(string current, string history), float>();
            var previousMap = new Dictionary<(string current, string history), (string previous, string prevHistory)>();
            var priorityQueue = new MinPriorityQueue<(string current, string history)>();

            // Track visited states to avoid cycling
            var visitedStates = new HashSet<(string current, string history)>();

            // Start with no history
            var startState = (start, "");
            distanceMap[startState] = 0;
            priorityQueue.Enqueue(startState, 0);

            bool pathFound = false;
            (string current, string history) finalState = default;

            // How many recent tracks to consider for U-turn detection
            const int HISTORY_LENGTH = 5;

            Debug.Log($"[PathFinder] Finding non-U-turn path from {start} to {goal}");

            while (!priorityQueue.IsEmpty())
            {
                var dequeued = priorityQueue.Dequeue();
                var currentState = dequeued.item;
                var currentDistance = dequeued.priority;

                string currentNode = currentState.current;
                string history = currentState.history;

                // Skip if we've already processed this state or found a better path
                if (visitedStates.Contains(currentState) ||
                    !distanceMap.TryGetValue(currentState, out var storedDistance) ||
                    storedDistance < currentDistance)
                    continue;

                visitedStates.Add(currentState);

                // If we reached the goal, we're done
                if (currentNode == goal)
                {
                    finalState = currentState;
                    pathFound = true;
                    break;
                }

                // Parse history string into a list of recent tracks
                var recentTracks = history.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                // Process neighbors
                foreach (var neighborId in Graph[currentNode].Neighbors)
                {
                    if (!Graph.ContainsKey(neighborId))
                        continue;

                    // CRITICAL: Skip self-loops completely (same track appearing twice)
                    if (neighborId == currentNode)
                        continue;

                    // Don't go back to the immediately previous node
                    if (recentTracks.Count > 0 && neighborId == recentTracks[0])
                        continue;

                    // Skip if this would cause a U-turn (track is in recent history, except for returning to start if needed)
                    if (neighborId != start && neighborId != goal && recentTracks.Contains(neighborId))
                        continue;

                    // Calculate edge cost
                    float transitionCost = CalculateEdgeCost(currentNode, neighborId);

                    // Apply penalties for junction branch switching
                    if (Graph[currentNode].Junction != null && recentTracks.Count > 0)
                    {
                        if (JunctionConnections.TryGetValue(currentNode, out var connections))
                        {
                            var inBranch = connections.FirstOrDefault(x => x.trackId == recentTracks[0]).branchIndex;
                            var outBranch = connections.FirstOrDefault(x => x.trackId == neighborId).branchIndex;

                            // Apply severe penalty if switching between different branches of a junction
                            if (inBranch >= 0 && outBranch >= 0 && inBranch != outBranch)
                                transitionCost += UTURN_PENALTY;
                        }
                    }

                    // Create new history for next state - keep the most recent N tracks
                    var newHistoryList = new List<string>(HISTORY_LENGTH + 1) { currentNode };
                    newHistoryList.AddRange(recentTracks.Take(HISTORY_LENGTH - 1));
                    string newHistory = string.Join(",", newHistoryList);

                    var nextState = (neighborId, newHistory);
                    float newDistance = currentDistance + transitionCost;

                    if (!distanceMap.TryGetValue(nextState, out var oldDistance) || newDistance < oldDistance)
                    {
                        distanceMap[nextState] = newDistance;
                        previousMap[nextState] = (currentNode, history);
                        priorityQueue.Enqueue(nextState, newDistance);
                    }
                }
            }

            if (!pathFound)
            {
                Debug.LogWarning($"[PathFinder] Failed to find path without U-turns, falling back to standard Dijkstra");
                return FindShortestPath(start, goal);
            }

            // Reconstruct the path
            var path = ReconstructDirectedPathImproved(previousMap, finalState, start);

            // Validate the path has no duplicates
            ValidateAndFixPath(path);

            return path;
        }
        /// <summary>
        /// Reconstructs a path from previous states with history
        /// </summary>
        private List<string> ReconstructDirectedPathImproved(
            Dictionary<(string current, string history), (string previous, string prevHistory)> previous,
            (string current, string history) goalState,
            string start)
        {
            var path = new List<string>();
            var current = goalState;

            while (true)
            {
                path.Insert(0, current.current);

                // If we've reached the start with no history, we're done
                if (current.current == start && string.IsNullOrEmpty(current.history))
                    break;

                if (!previous.TryGetValue(current, out var prev))
                    return null;

                current = (prev.previous, prev.prevHistory);
            }

            Debug.Log($"[PathFinder] Improved directed path found: {string.Join(" -> ", path)}");

            // Enhance the path with junction data
            EnhancePathWithJunctionData(path);

            return path;
        }
        /// <summary>
        /// Method 3: BFS - fastest algorithm but not necessarily shortest path
        /// </summary>
        public List<string> FindShortestPathBFS(string start, string goal)
        {
            if (!ValidatePathEndpoints(start, goal))
                return null;

            var previous = new Dictionary<string, string>();
            var visited = new HashSet<string> { start };
            var queue = new Queue<string>();
            queue.Enqueue(start);

            Debug.Log($"[PathFinder] BFS search from {start} to {goal}");

            while (queue.Count > 0)
            {
                var currentNode = queue.Dequeue();

                if (currentNode == goal)
                    break;

                foreach (var neighbor in Graph[currentNode].Neighbors)
                {
                    if (!Graph.ContainsKey(neighbor))
                        continue;

                    if (visited.Add(neighbor))
                    {
                        previous[neighbor] = currentNode;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return ReconstructPath(previous, start, goal);
        }
        /// <summary>
        /// Counts the number of U-turns in a path
        /// </summary>
        private int CountUTurns(List<string> path)
        {
            if (path == null || path.Count <= 1)
                return 0;

            int uTurnCount = 0;

            // Count immediate duplicates
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (path[i] == path[i + 1])
                    uTurnCount++;
            }

            // Check for revisits within a short window
            var recentTracks = new Queue<string>();
            foreach (var track in path)
            {
                if (recentTracks.Contains(track))
                    uTurnCount++;

                recentTracks.Enqueue(track);
                if (recentTracks.Count > RECENT_TRACK_MEMORY)
                    recentTracks.Dequeue();
            }

            return uTurnCount;
        }
        /// <summary>
        /// Returns the path with additional information about U-turns
        /// </summary>
        public (List<string> path, int uTurnCount) FindShortestPathWithUTurns(string start, string goal)
        {
            var path = FindShortestPathNoUTurnsOrYTurns(start, goal);
            if (path == null)
                return (null, 0);

            int uTurnCount = CountUTurns(path);
            return (path, uTurnCount);
        }

        /// <summary>
        /// Determines which branch to use at a junction based on the path
        /// </summary>
        public int GetBranchForJunction(string junctionTrackId, List<string> path)
        {
            if (string.IsNullOrEmpty(junctionTrackId) || path == null || path.Count < 2)
                return -1;

            int junctionPosition = path.IndexOf(junctionTrackId);
            if (junctionPosition < 0 || junctionPosition >= path.Count - 1)
                return -1;

            string nextTrackId = path[junctionPosition + 1];

            // Check if junction connections are available
            if (!JunctionConnections.TryGetValue(junctionTrackId, out var connections))
                return -1;

            // Direct connection found
            foreach (var (trackId, branchIndex) in connections)
            {
                if (trackId == nextTrackId && branchIndex >= 0)
                    return branchIndex;
            }

            // Try to infer the best branch
            int bestBranch = -1;
            int bestScore = -1;

            // Look for branches in the rest of the path
            foreach (var (branchTrackId, branchIndex) in connections.Where(c => c.branchIndex >= 0))
            {
                if (branchIndex < 0)
                    continue;

                int branchPosition = path.IndexOf(branchTrackId);
                if (branchPosition > junctionPosition)
                {
                    // Prioritize closer branches
                    int score = 1000 - (branchPosition - junctionPosition);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestBranch = branchIndex;
                    }
                    continue;
                }

                // Look for neighbors of this branch in the path
                if (!Graph.TryGetValue(branchTrackId, out var branchNode))
                    continue;

                foreach (string neighborId in branchNode.Neighbors)
                {
                    int neighborPosition = path.IndexOf(neighborId);
                    if (neighborPosition > junctionPosition)
                    {
                        int score = 500 - (neighborPosition - junctionPosition);
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

        /// <summary>
        /// Gets rail track ID for the graph
        /// </summary>
        public static string GetRailTrackGraphID(RailTrack track)
        {
            return track == null ? null : track.GetInstanceID() + "_" + track.name;
        }

        #endregion

        #region ─── Private Helper Methods ────────────────────────────────────────────
        /// <summary>
        /// Validates and fixes a path to ensure it has no U-turns
        /// </summary>
        private void ValidateAndFixPath(List<string> path)
        {
            if (path == null || path.Count < 3)
                return;

            // Check for duplicate track sequences (indicating U-turns)
            for (int i = 0; i < path.Count - 1; i++)
            {
                // Remove immediate duplicates
                if (path[i] == path[i + 1])
                {
                    Debug.LogWarning($"[PathFinder] Removing duplicate track: {path[i]}");
                    path.RemoveAt(i + 1);
                    i--; // Recheck this position
                    continue;
                }

                // Check for loops (A->B->C->A) which indicate a Y-turn
                if (i < path.Count - 3)
                {
                    // If first and fourth elements match, this is a loop
                    if (path[i] == path[i + 3])
                    {
                        Debug.LogWarning($"[PathFinder] Removing Y-turn loop: {path[i]} -> {path[i + 1]} -> {path[i + 2]} -> {path[i]}");
                        path.RemoveRange(i + 1, 3);
                        i--; // Recheck this position
                    }
                }
            }
        }

        /// <summary>
        /// Validates that start and goal endpoints exist in the graph
        /// </summary>
        private bool ValidatePathEndpoints(string start, string goal)
        {
            if (string.IsNullOrEmpty(start) || string.IsNullOrEmpty(goal))
            {
                Debug.LogError("Start or goal node is null or empty");
                return false;
            }

            if (!Graph.ContainsKey(start))
            {
                Debug.LogError($"Start node '{start}' not found in graph");
                return false;
            }

            if (!Graph.ContainsKey(goal))
            {
                Debug.LogError($"Goal node '{goal}' not found in graph");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Builds a mapping of RailTrack objects to their string IDs
        /// </summary>
        private Dictionary<RailTrack, string> BuildTrackIdMap(RailTrack[] allTracks)
        {
            var idMap = new Dictionary<RailTrack, string>(allTracks.Length);

            foreach (var track in allTracks)
            {
                if (track == null)
                    continue;

                string id = GetRailTrackGraphID(track);
                idMap[track] = id;
            }

            return idMap;
        }

        /// <summary>
        /// Creates track nodes for all rail tracks
        /// </summary>
        private void CreateTrackNodes(RailTrack[] allTracks, Dictionary<RailTrack, string> idMap)
        {
            foreach (var track in allTracks)
            {
                if (track == null)
                    continue;

                string id = idMap[track];
                Vector3 nodePosition;

                // Get track position from point set if available, or fall back to transform position
                var pointSet = track.GetUnkinkedPointSet();
                if (pointSet != null && pointSet.points.Length > 0)
                {
                    // Add OriginShift for correct world position
                    nodePosition = (Vector3)pointSet.points[pointSet.points.Length / 2].position + OriginShift.currentMove;
                }
                else
                {
                    nodePosition = track.transform.position;
                }

                var node = new TrackNode(id, nodePosition) { Track = track };
                Graph[id] = node;
            }
        }

        /// <summary>
        /// Sets up track connections and junction information
        /// </summary>
        private void WireTrackConnectionsAndJunctions(RailTrack[] allTracks, Dictionary<RailTrack, string> idMap)
        {
            foreach (var track in allTracks)
            {
                if (track == null)
                    continue;

                string trackId = idMap[track];
                var node = Graph[trackId];
                var neighbors = new List<string>();

                // Add branch connections
                AddBranchIfValid(track.inBranch, idMap, neighbors);
                AddBranchIfValid(track.outBranch, idMap, neighbors);

                // Process junctions
                if (track.inJunction != null)
                {
                    ProcessJunction(track.inJunction, track, idMap, neighbors, trackId);
                    node.Junction = track.inJunction;
                }

                if (track.outJunction != null)
                {
                    ProcessJunction(track.outJunction, track, idMap, neighbors, trackId);
                    if (node.Junction == null)
                        node.Junction = track.outJunction;
                }

                node.Neighbors = neighbors;
            }
        }

        /// <summary>
        /// Adds a branch connection if it's valid
        /// </summary>
        private void AddBranchIfValid(Junction.Branch branch, Dictionary<RailTrack, string> idMap, List<string> neighbors)
        {
            if (branch?.track == null)
                return;

            if (idMap.TryGetValue(branch.track, out var branchId))
                neighbors.Add(branchId);
        }

        /// <summary>
        /// Makes all connections bidirectional to ensure proper pathfinding
        /// </summary>
        private void MakeEdgesBidirectional()
        {
            // For each node
            foreach (var kvp in Graph)
            {
                string nodeId = kvp.Key;
                TrackNode node = kvp.Value;

                // For each of its neighbors
                foreach (var neighborId in node.Neighbors.ToList())
                {
                    // If the neighbor exists and doesn't point back to this node, add the connection
                    if (Graph.TryGetValue(neighborId, out var neighborNode) &&
                        !neighborNode.Neighbors.Contains(nodeId))
                    {
                        neighborNode.Neighbors.Add(nodeId);
                    }
                }
            }
        }

        /// <summary>
        /// Processes a junction to set up connections and branch information
        /// </summary>
        private void ProcessJunction(Junction junction, RailTrack track, Dictionary<RailTrack, string> idMap,
                                   List<string> neighbors, string trackId)
        {
            if (junction == null || junction.outBranches == null)
                return;

            // Ensure junction connections entry exists
            if (!JunctionConnections.ContainsKey(trackId))
                JunctionConnections[trackId] = new List<(string, int)>();

            // Process outbound branches
            for (int i = 0; i < junction.outBranches.Count; i++)
            {
                var branch = junction.outBranches[i];
                if (branch?.track == null)
                    continue;

                if (!idMap.TryGetValue(branch.track, out var branchId))
                    continue;

                neighbors.Add(branchId);
                JunctionConnections[trackId].Add((branchId, i));
            }

            // Process inbound branch
            if (junction.inBranch?.track != null && idMap.TryGetValue(junction.inBranch.track, out var inBranchId))
            {
                neighbors.Add(inBranchId);
                JunctionConnections[trackId].Add((inBranchId, -1));
            }
        }

        /// <summary>
        /// Calculates the cost of traversing from one node to another
        /// </summary>
        private float CalculateEdgeCost(string fromId, string toId)
        {
            if (!Graph.TryGetValue(fromId, out var fromNode) || !Graph.TryGetValue(toId, out var toNode))
                return float.MaxValue;

            // Base cost is the track length
            double cost = toNode.Track.LogicTrack().length;

            // Apply occupied track penalty if applicable
            if (toNode.Track.LogicTrack().OccupiedLength != 0)
                cost *= toNode.Track.LogicTrack().OccupiedLength*100;

            return (float)cost;
        }

        /// <summary>
        /// Reconstructs a path from previous node mapping
        /// </summary>
        private List<string> ReconstructPath(Dictionary<string, string> previous, string start, string goal)
        {
            // If there's no path to the goal and start isn't the goal, return null
            if (start != goal && !previous.ContainsKey(goal))
                return null;

            var path = new List<string>();
            string current = goal;

            while (current != start)
            {
                path.Insert(0, current);
                if (!previous.TryGetValue(current, out current))
                    return null;
            }

            path.Insert(0, start);
            Debug.Log($"[PathFinder] Path found: {string.Join(" -> ", path)}");

            // Enhance the path with junction data
            EnhancePathWithJunctionData(path);

            return path;
        }

        /// <summary>
        /// Reconstructs a path from directed states
        /// </summary>
        private List<string> ReconstructDirectedPath(Dictionary<DirectedState, DirectedState> previous, DirectedState goalState, string start)
        {
            var path = new List<string>();
            var current = goalState;

            while (true)
            {
                path.Insert(0, current.Current);

                // If we've reached the start node with no previous, we're done
                if (current.Current == start && current.Previous == null)
                    break;

                if (!previous.TryGetValue(current, out current))
                    return null;
            }

            Debug.Log($"[PathFinder] Directed path found: {string.Join(" -> ", path)}");

            // Enhance the path with junction data
            EnhancePathWithJunctionData(path);

            return path;
        }

        /// <summary>
        /// Enhances a path with junction information for route setting
        /// </summary>
        private void EnhancePathWithJunctionData(List<string> path)
        {
            if (path == null || path.Count < 2)
                return;

            int enhancedCount = 0;

            for (int i = 0; i < path.Count - 1; i++)
            {
                string currentId = path[i];
                string nextId = path[i + 1];

                if (!Graph.TryGetValue(currentId, out var node) || node.Junction == null)
                    continue;

                if (!JunctionConnections.TryGetValue(currentId, out var connections))
                    continue;

                // Check if we have a direct junction connection
                if (connections.Any(c => c.trackId == nextId && c.branchIndex >= 0))
                {
                    enhancedCount++;
                    continue;
                }

                // Look ahead in the path for branches
                foreach (var (branchTrackId, branchIndex) in connections.Where(c => c.branchIndex >= 0))
                {
                    int branchPosition = path.IndexOf(branchTrackId);
                    if (branchPosition > i)
                    {
                        enhancedCount++;
                        break;
                    }

                    // Check branch neighbors
                    if (!Graph.TryGetValue(branchTrackId, out var branchNode))
                        continue;

                    foreach (string neighborId in branchNode.Neighbors)
                    {
                        int neighborPosition = path.IndexOf(neighborId);
                        if (neighborPosition > i)
                        {
                            enhancedCount++;
                            break;
                        }
                    }
                }
            }

            if (enhancedCount > 0)
                Debug.Log($"[PathFinder] Enhanced {enhancedCount} path segments with junction data");
        }

        #endregion
    }
}