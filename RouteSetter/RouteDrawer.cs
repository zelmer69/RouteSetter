using UnityEngine;
using DV;
using System.Collections.Generic;
namespace RouteSetter
{

    /// <summary>
    /// Template component that can be added to any GameObject in Unity.
    /// </summary>
    [DisallowMultipleComponent]
    public class RouteDrawer : MonoBehaviour
    {
        private TrainCar Loco = null;
        Dictionary<string, TrackNode> Graph = null;
        List<string> CurrentPath = new List<string>();
        private float maxDrawDistance = Main.Settings.DrawDistance; // Maximum distance to draw lines

        // Called when the script instance is being loaded.
        private void Awake()
        {

            // Initialization code here
        }

        // Called before the first frame update
        private void Start()
        {
            // Startup logic here
        }

        public void DisplayRoute(List<string> Path)
        {
            Graph = Switcher.Graph;
            Loco = PlayerManager.LastLoco;
            CurrentPath = Path;

        }
        public void DrawPathInGame(List<string> path)
        {
            if (Loco == null) return;
            if (Loco.derailed) return;

            if (Graph == null) return;
            if (path == null || path.Count <= 1)
                return;

            // 1) Get loco in world space & into origin-relative coords
            var loco = Loco;
            if (loco == null)
                return;
            Vector3d locoLocal = new Vector3d(loco.transform.position)
                                  - new Vector3d(DV.OriginShift.OriginShift.currentMove);

            // 2) Fetch the kinked points for the current track (path[0])
            if (!Switcher.pathFinder.Graph.TryGetValue(path[0], out var currentNode)
                || currentNode.Track == null)
                return;
            var pts = currentNode.Track.GetKinkedPointSet()?.points;
            if (pts == null || pts.Length < 2)
                return;

            // 3) Find nearest point on that set
            int nearest = FindNearestPoint(pts, locoLocal);
            int last = pts.Length - 1;

            // 4) Decide which end of this track is the junction to path[1]
            //    If the next track shares at this track's start, junctionIndex = 0
            //    Otherwise it must be at this track's end → junctionIndex = last
            int junctionIndex;
            {
                // peek at the very first track-point-set of path[1]
                if (Switcher.pathFinder.Graph.TryGetValue(path[1], out var nextNode)
                    && nextNode.Track != null)
                {
                    var nextPts = nextNode.Track.GetKinkedPointSet()?.points;
                    if (nextPts != null && nextPts.Length > 0)
                    {
                        // compare world-space coords of each end:
                        Vector3 worldStart = (Vector3)pts[0].position + DV.OriginShift.OriginShift.currentMove;
                        Vector3 worldEnd = (Vector3)pts[last].position + DV.OriginShift.OriginShift.currentMove;
                        Vector3 nextWorld = (Vector3)nextPts[0].position + DV.OriginShift.OriginShift.currentMove;

                        // whichever end is *closer* to nextTrack's first kink is the junction.
                        double d0 = Vector3.Distance(worldStart, nextWorld);
                        double d1 = Vector3.Distance(worldEnd, nextWorld);
                        junctionIndex = (d0 <= d1) ? 0 : last;
                    }
                    else junctionIndex = last;
                }
                else junctionIndex = last;
            }

            // 5) Draw *just* from nearest → junctionIndex in green
            if (nearest != junctionIndex)
            {
                int step = (junctionIndex > nearest) ? +1 : -1;
                for (int i = nearest; i != junctionIndex; i += step)
                {
                    int nextI = i + step;
                    Vector3 a = (Vector3)pts[i].position + DV.OriginShift.OriginShift.currentMove;
                    Vector3 b = (Vector3)pts[nextI].position + DV.OriginShift.OriginShift.currentMove;

                    // Check if this segment is within draw distance
                    if (Vector3.Distance(loco.transform.position, a) <= maxDrawDistance ||
                        Vector3.Distance(loco.transform.position, b) <= maxDrawDistance)
                    {
                        CreateLineRenderer(a, b, Color.green, 0.1f);
                    }
                }
            }

            // 6) Draw all the *other* tracks on the path in cyan
            for (int j = 1; j < path.Count - 1; j++)
            {
                if (!Switcher.pathFinder.Graph.TryGetValue(path[j], out var node)
                    || node.Track == null)
                    continue;
                var set = node.Track.GetKinkedPointSet();

                if (set?.points.Length < 2)
                    continue;

                for (int k = 0; k < set.points.Length - 1; k++)
                {
                    // Check if this segment is within draw distance from the locomotive
                    Vector3 a = (Vector3)set.points[k].position + DV.OriginShift.OriginShift.currentMove;
                    if (Vector3.Distance(loco.transform.position, a) > maxDrawDistance) continue;

                    Vector3 b = (Vector3)set.points[k + 1].position + DV.OriginShift.OriginShift.currentMove;
                    CreateLineRenderer(a, b, Color.cyan, 0.1f);
                }
            }
        }
        private int FindNearestPoint(DV.PointSet.EquiPointSet.Point[] fromSet, Vector3d positionTo)
        {
            if (fromSet == null || fromSet.Length == 0)
                return 0;

            int nearestIndex = 0;
            double minDistance = double.MaxValue;

            for (int i = 0; i < fromSet.Length; i++)
            {
                double distance = Vector3d.Distance(fromSet[i].position, positionTo);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }
        private float updateInterval = 2f; // seconds
        private float timeSinceLastUpdate = 0f;

        private void Update()
        {
            if (!Switcher.RouteDisplayEnabled) return;
            timeSinceLastUpdate += Time.deltaTime;
            if (timeSinceLastUpdate >= updateInterval)
            {
                timeSinceLastUpdate = 0f;
                if (Graph != null && CurrentPath != null && Loco != null && !Loco.derailed)
                {
                    var currentTrack = Loco.Bogies[0]?.track;
                    string currentTrackId = PathFinder.GetRailTrackGraphID(currentTrack);

                    // If we can't identify current track, don't update the path
                    if (string.IsNullOrEmpty(currentTrackId))
                    {
                        DrawPathInGame(CurrentPath);
                        return;
                    }

                    // Don't modify the path if the current track isn't in it
                    if (!CurrentPath.Contains(currentTrackId))
                    {
                        DrawPathInGame(CurrentPath);
                        return;
                    }

                    // Get locomotive's position on the track
                    Vector3d locoLocal = new Vector3d(Loco.transform.position)
                                      - new Vector3d(DV.OriginShift.OriginShift.currentMove);

                    // Find all occurrences of the current track ID in the path
                    List<int> trackOccurrences = new List<int>();
                    for (int i = 0; i < CurrentPath.Count; i++)
                    {
                        if (CurrentPath[i] == currentTrackId)
                            trackOccurrences.Add(i);
                    }

                    if (trackOccurrences.Count == 0)
                    {
                        // This shouldn't happen due to the earlier check
                        DrawPathInGame(CurrentPath);
                        return;
                    }

                    // If there's only one occurrence, it's simple
                    if (trackOccurrences.Count == 1)
                    {
                        // Only remove previous tracks if we're not at the beginning
                        if (trackOccurrences[0] > 0)
                        {
                            // Keep the current track in the path
                            CurrentPath.RemoveRange(0, trackOccurrences[0]);
                        }
                    }
                    else
                    {
                        // Multiple occurrences - U-turn scenario
                        // We need to find which occurrence best matches our current position
                        int bestMatchIndex = 0;
                        double bestMatchDistance = double.MaxValue;

                        // Get current kinked points of the track
                        if (Switcher.pathFinder.Graph.TryGetValue(currentTrackId, out var trackNode) &&
                            trackNode.Track != null)
                        {
                            var points = trackNode.Track.GetKinkedPointSet()?.points;
                            if (points != null && points.Length > 0)
                            {
                                // Find nearest point on track to locomotive
                                int nearestPtIdx = FindNearestPoint(points, locoLocal);

                                // For each occurrence, check which one makes most sense
                                // based on our progress through the path
                                foreach (int occurrenceIdx in trackOccurrences)
                                {
                                    // If this isn't the first track in the path, check if this occurrence
                                    // connects properly with the previous track
                                    if (occurrenceIdx > 0)
                                    {
                                        string prevTrackId = CurrentPath[occurrenceIdx - 1];
                                        if (Switcher.pathFinder.Graph.TryGetValue(prevTrackId, out var prevNode) &&
                                            prevNode.Track != null)
                                        {
                                            var prevPoints = prevNode.Track.GetKinkedPointSet()?.points;
                                            if (prevPoints != null && prevPoints.Length > 0)
                                            {
                                                // If locomotive is closer to the junction with the previous track,
                                                // this occurrence is more likely the correct one
                                                Vector3d prevEndPos = prevPoints[prevPoints.Length - 1].position;
                                                int junction = 0; // Junction index (start or end of track)
                                                double d0 = Vector3d.Distance(points[0].position, prevEndPos);
                                                double d1 = Vector3d.Distance(points[points.Length - 1].position, prevEndPos);
                                                junction = (d0 <= d1) ? 0 : points.Length - 1;

                                                // Compare locomotive position to junction position
                                                double distToJunction = Vector3d.Distance(points[junction].position, locoLocal);
                                                double distFromJunctionToEnd = (junction == 0) ?
                                                    Vector3d.Distance(points[0].position, points[points.Length - 1].position) :
                                                    Vector3d.Distance(points[points.Length - 1].position, points[0].position);

                                                // Calculate progress ratio through the track
                                                // (lower means closer to junction with previous track)
                                                double progressRatio = distToJunction / distFromJunctionToEnd;

                                                // If locomotive is in first half of track from junction, 
                                                // this is likely the correct occurrence
                                                if (progressRatio < 0.5 && progressRatio < bestMatchDistance)
                                                {
                                                    bestMatchDistance = progressRatio;
                                                    bestMatchIndex = occurrenceIdx;
                                                }
                                            }
                                        }
                                    }
                                    // If it's the first track or we couldn't determine based on junctions,
                                    // just use the earliest occurrence as default
                                    else if (bestMatchDistance == double.MaxValue)
                                    {
                                        bestMatchIndex = occurrenceIdx;
                                        bestMatchDistance = 0;
                                    }
                                }
                            }
                        }

                        // Only remove tracks if we're not at the beginning of the path
                        // and we're confident about the track occurrence
                        if (bestMatchIndex > 0)
                        {
                            CurrentPath.RemoveRange(0, bestMatchIndex);
                        }
                    }

                    DrawPathInGame(CurrentPath);
                }
            }
        }

        // Called when the object is destroyed
        private void OnDestroy()
        {
            // Cleanup logic here
        }
        private void CreateLineRenderer(Vector3 start, Vector3 end, Color color, float width)
        {
            var lineObj = new GameObject("PathLine");
            var lineRenderer = lineObj.AddComponent<LineRenderer>();

            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);

            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.useWorldSpace = true;

            // Optionally, destroy the line after a certain duration
            GameObject.Destroy(lineObj, 2f);
        }
    }
}