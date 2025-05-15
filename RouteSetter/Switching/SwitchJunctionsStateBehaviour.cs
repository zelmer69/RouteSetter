using System.Collections.Generic;
using System.Text;
using UnityEngine;
using CommsRadioAPI;
using DV;
using DV.Player;

namespace AutoPilot
{
    internal class SwitchJunctionsStateBehaviour : AStateBehaviour
    {
        private readonly string contextText;
        private readonly StationTrack Destination;
        private readonly string actionText;

        public SwitchJunctionsStateBehaviour(string contextText = "Finding route", StationTrack destination = default,string Actiontext = "Click to confirm")
            : base(new CommsRadioState("Switch Junctions", contextText,Actiontext))
        {
            this.contextText = contextText;
            this.Destination = destination;
            this.actionText = Actiontext;

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
            var destinationNode = Switcher.pathFinder.FindStationTrackByName(Destination.StationName,Destination.Yard,Destination.Track);
            error = ValidateDestinationNode(destinationNode);
            if (error != null)
                return new SwitchJunctionsStateBehaviour(error);

            string destinationTrackId = destinationNode.id;
            var pathTrackIds = Switcher.pathFinder.FindShortestPath(startTrackId, destinationTrackId);
            error = ValidatePath(pathTrackIds);
            if (error != null)
                return new SwitchJunctionsStateBehaviour(error);

            var trackIndexInPath = BuildTrackIndexInPath(pathTrackIds);
            var junctionTrackIds = CollectJunctionTrackIds(pathTrackIds);

            var (switchesChanged, junctionsUnset, junctionResults) = SetJunctionsAlongPath(junctionTrackIds, trackIndexInPath, pathTrackIds);

            string statusMessage = BuildStatusMessage(switchesChanged, junctionsUnset);

            if (junctionResults.Length > 0)
                Debug.Log(junctionResults.ToString());
            Debug.Log($"[AutoPilot] {statusMessage}");

            return new SwitchJunctionsStateBehaviour($"Route is set from{startTrackId} to {Destination.GetFullNameNoID()}",default, $"Happy derailing!");
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
                if (Switcher.Graph.TryGetValue(trackId, out var trackNode) && trackNode.junction != null)
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
                if (!Switcher.Graph.TryGetValue(junctionTrackId, out var trackNode) || trackNode.junction == null)
                    continue;

                var junction = trackNode.junction;
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
