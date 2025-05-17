using System.Collections.Generic;
using System.Text;
using UnityEngine;
using CommsRadioAPI;


namespace RouteSetter
{
    internal class SwitchJunctionsStateBehaviour : AStateBehaviour
    {
        private readonly string contextText;
        private readonly StationTrack Destination;
        private readonly string actionText;



        private readonly PathFindingMode _pathMode;

        
        public SwitchJunctionsStateBehaviour(
            string contextText = "Finding route",
            StationTrack destination = default,
            string Actiontext = "Click to confirm",
            PathFindingMode pathMode = PathFindingMode.Dijkstra)
            : base(new CommsRadioState("Switch Junctions", contextText, Actiontext))
        {
            this.contextText = contextText;
            this.Destination = destination;
            this.actionText = Actiontext;
            this._pathMode = pathMode;
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            
            switch (action)
            {
                case InputAction.Down:
                    return new InitialStateBehaviour();
                case InputAction.Activate:
                    return SwitchJunctionsAlongPath(utility.SignalOrigin);
                default:
                    throw new System.ArgumentException();
            }
        }

        private SwitchJunctionsStateBehaviour SwitchJunctionsAlongPath(Transform signalOrigin)
        {
            string error = ValidatePathFinder(signalOrigin);
            if (error != null)
                return new SwitchJunctionsStateBehaviour(error);

            var playerLoco = PlayerManager.LastLoco;
            error = ValidatePlayerLoco(playerLoco);
            if (error != null)
                return new SwitchJunctionsStateBehaviour(error);

            var playerTrack = playerLoco.Bogies[0]?.track;
            error = ValidatePlayerTrack(playerTrack);
            if (error != null)
                return new SwitchJunctionsStateBehaviour(error);

            string startTrackId = PathFinder.GetRailTrackGraphID(playerTrack);
            var destinationNode = Switcher.pathFinder.FindStationTrackByName(Destination.StationName, Destination.Yard, Destination.Track);
            error = ValidateDestinationNode(destinationNode);
            if (error != null)
                return new SwitchJunctionsStateBehaviour(error);

            string destinationTrackId = destinationNode.Id;
            List<string> pathTrackIds = null;
            int uTurnCount = 0;

            switch (_pathMode)
            {
                case PathFindingMode.Dijkstra:
                    pathTrackIds = Switcher.pathFinder.FindShortestPath(startTrackId, destinationTrackId);
                    break;
                case PathFindingMode.DijkstraWithoutUTurns:
                    var result = Switcher.pathFinder.FindShortestPathWithUTurns(startTrackId, destinationTrackId);
                    pathTrackIds = result.path;
                    uTurnCount = result.uTurnCount;
                    break;
                case PathFindingMode.BFS:
                    pathTrackIds = Switcher.pathFinder.FindShortestPathBFS(startTrackId, destinationTrackId);
                    break;
            }

            error = ValidatePath(pathTrackIds);
            if (error != null)
                return new SwitchJunctionsStateBehaviour(error, default, "Click to confirm", _pathMode);


            Switcher.routeDrawer.DisplayRoute(pathTrackIds);
            var trackIndexInPath = BuildTrackIndexInPath(pathTrackIds);
            var junctionTrackIds = CollectJunctionTrackIds(pathTrackIds);

            var (switchesChanged, junctionsUnset, junctionResults) = SetJunctionsAlongPath(junctionTrackIds, trackIndexInPath, pathTrackIds);
            var pathInfo = new StringBuilder();

            pathInfo.AppendLine($"Pathfinding mode: {_pathMode}");
            if (_pathMode == PathFindingMode.DijkstraWithoutUTurns)
                pathInfo.AppendLine($"U-turns  {uTurnCount}");
            pathInfo.AppendLine($"Path length: {pathTrackIds?.Count ?? 0}");
            if (pathTrackIds != null && pathTrackIds.Count > 0)
                pathInfo.AppendLine($"Path: {string.Join(" -> ", pathTrackIds)}");

            string statusMessage = BuildStatusMessage(switchesChanged, junctionsUnset);
            if (junctionResults.Length > 0)
                RouteSetterDebug.Log(junctionResults.ToString());
            RouteSetterDebug.Log($"[RouteSetter] {statusMessage}\n{pathInfo}");

            return new SwitchJunctionsStateBehaviour(
                $"Route:{startTrackId}->{Destination.StationName}-{Destination.Track}info:\n{pathInfo}",
                default,
                $"Happy derailing!"
            );
        }
        private string ValidatePathFinder(Transform signalOrigin)
        {
            if (signalOrigin == null)
                return "Invalid location";
            if (Switcher.pathFinder == null || Switcher.Graph == null)
                return "PathFinder not initialized";
            return null;
        }

        private string ValidatePlayerLoco(TrainCar playerLoco)
        {
            if (playerLoco == null)
                return "No locomotive found";
            return null;
        }

        private string ValidatePlayerTrack(RailTrack playerTrack)
        {
            if (playerTrack == null)
                return "Locomotive not on track";
            return null;
        }

        private string ValidateDestinationNode(TrackNode destinationNode)
        {
            if (destinationNode == null)
                return "Destination track not found";
            return null;
        }

        private string ValidatePath(List<string> pathTrackIds)
        {
            if (pathTrackIds == null || pathTrackIds.Count < 2)
                return "Unable to find path";
            return null;
        }

        private Dictionary<string, int> BuildTrackIndexInPath(List<string> pathTrackIds)
        {
            var trackIndexInPath = new Dictionary<string, int>();
            for (int i = 0; i < pathTrackIds.Count; i++)
                trackIndexInPath[pathTrackIds[i]] = i;
            return trackIndexInPath;
        }

        private List<string> CollectJunctionTrackIds(List<string> pathTrackIds)
        {
            var junctionTrackIds = new List<string>();
            foreach (var trackId in pathTrackIds)
            {
                if (Switcher.Graph.TryGetValue(trackId, out var trackNode) && trackNode.Junction != null)
                    junctionTrackIds.Add(trackId);
            }
            return junctionTrackIds;
        }

        private (int switchesChanged, int junctionsUnset, StringBuilder junctionResults) SetJunctionsAlongPath(
            List<string> junctionTrackIds,
            Dictionary<string, int> trackIndexInPath,
            List<string> pathTrackIds)
        {
            int switchesChanged = 0;
            int junctionsUnset = 0;
            var junctionResults = new StringBuilder();

            foreach (var junctionTrackId in junctionTrackIds)
            {
                if (!Switcher.Graph.TryGetValue(junctionTrackId, out var trackNode) || trackNode.Junction == null)
                    continue;

                var junction = trackNode.Junction;
                int pathIndex = trackIndexInPath[junctionTrackId];
                if (pathIndex >= pathTrackIds.Count - 1)
                    continue;

                int targetBranchIndex = Switcher.pathFinder.GetBranchForJunction(junctionTrackId, pathTrackIds);
                if (targetBranchIndex < 0)
                {
                    junctionsUnset++;
                    junctionResults.AppendLine($" - Junction {junctionTrackId}: Could not determine correct branch");
                    continue;
                }

                if (junction.selectedBranch == targetBranchIndex)
                    continue;

                if (TrySetJunctionBranch(junction, targetBranchIndex))
                {
                    switchesChanged++;
                }
                else
                {
                    junctionsUnset++;
                    junctionResults.AppendLine($" - Junction {junctionTrackId}: Failed to set to branch {targetBranchIndex} (current: {junction.selectedBranch})");
                }
            }

            return (switchesChanged, junctionsUnset, junctionResults);
        }

        private bool TrySetJunctionBranch(Junction junction, int targetBranchIndex)
        {
            int initialBranch = junction.selectedBranch;
            int attempts = 0;
            int maxAttempts = junction.outBranches.Count * 2;

            while (junction.selectedBranch != targetBranchIndex && attempts < maxAttempts)
            {
                int before = junction.selectedBranch;
                junction.Switch(Junction.SwitchMode.REGULAR);
                int after = junction.selectedBranch;
                attempts++;

                if (before == after || (attempts >= junction.outBranches.Count && junction.selectedBranch == initialBranch))
                    break;
            }

            return junction.selectedBranch == targetBranchIndex;
        }

        private string BuildStatusMessage(int switchesChanged, int junctionsUnset)
        {
            if (switchesChanged > 0)
            {
                var msg = $"Path updated: set {switchesChanged} switch(es).";
                if (junctionsUnset > 0)
                    msg += $" ({junctionsUnset} switch(es) could not be set correctly)";
                return msg;
            }
            if (junctionsUnset == 0)
                return "All switches already correct.";
            return "Route set";

        }
    }
}
