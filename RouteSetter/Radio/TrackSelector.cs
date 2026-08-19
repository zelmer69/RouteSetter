using CommsRadioAPI;
using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RouteSetter;
using System.Threading.Tasks;
using UnityEngine;
using DV;

namespace RouteSetter.Radio
{
    static class TrackIDParser
    {
        // Matches TrackID.RailTrackGameObjectID format:
        // [Y]_[{yardId}]_[{subYardId}-{orderNumber}-{trackType}]
        private static readonly Regex Pattern = new Regex(
            @"\[Y\]_\[(?<yard>[^\]]+)\]_\[(?<sub>[^-\]]*)-(?<order>[^-\]]+)-(?<type>[^-\]]+)\]",
            RegexOptions.Compiled);

        public static bool TryParse(string raw, out TrackID trackId)
        {
            trackId = null;
            if (string.IsNullOrEmpty(raw)) return false;

            var m = Pattern.Match(raw);
            if (!m.Success) return false;

            try
            {
                trackId = new TrackID(
                    m.Groups["yard"].Value,
                    m.Groups["sub"].Value,
                    m.Groups["order"].Value,
                    m.Groups["type"].Value
                );
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RouteSetter] Error constructing TrackID from '{raw}': {ex.Message}");
                return false;
            }
        }
    }

    static class TrackSorter
    {
        public static List<TrackID> SortTracksByStation_LINQ(List<TrackID> allTracks)
        {
            return allTracks
                .OrderBy(t => t.yardId)
                .ThenBy(t => t.TrackPartOnly)
                .ToList();
        }
    }

    internal class TrackSelector : AStateBehaviour
    {
        internal enum SelectorMode { Station, Track }
        private readonly SelectorMode _mode;
        private readonly List<TrackID> _tracks;
        private readonly List<TrackID> _stations; // Unique TrackID per station (yardId)
        private readonly int _stationIndex;
        private readonly int _trackIndex;
        private readonly Action<TrackID> onRouteSelected;
        private readonly Func<AStateBehaviour> parent; // where to go after reporting the result

        public TrackSelector(Action<TrackID> onRouteSelected, Func<AStateBehaviour> parent, int stationIndex = 0, int trackIndex = 0, List<TrackID> tracks = null, SelectorMode mode = SelectorMode.Station)
            : base(CreateState(stationIndex, trackIndex, tracks, mode))
        {
            _tracks = tracks != null
                ? TrackSorter.SortTracksByStation_LINQ(tracks)
                : TrackSorter.SortTracksByStation_LINQ(LoadTracks());

            // Unique TrackID per station (first occurrence)
            _stations = _tracks
                .GroupBy(t => t.yardId)
                .Select(g => g.First())
                .OrderBy(t => t.yardId)
                .ToList();

            _stationIndex = _stations.Count > 0 ? Mathf.Clamp(stationIndex, 0, _stations.Count - 1) : 0;
            _trackIndex = trackIndex;
            _mode = mode;
            this.onRouteSelected = onRouteSelected;
            this.parent = parent;
        }

        private static CommsRadioState CreateState(int stationIndex, int trackIndex, List<TrackID> tracks, SelectorMode mode)
        {
            var sortedTracks = tracks != null
                ? TrackSorter.SortTracksByStation_LINQ(tracks)
                : TrackSorter.SortTracksByStation_LINQ(LoadTracksStatic());

            var stations = sortedTracks
                .GroupBy(t => t.yardId)
                .Select(g => g.First())
                .OrderBy(t => t.yardId)
                .ToList();

            if (mode == SelectorMode.Station)
            {
                if (stations.Count == 0)
                    return new CommsRadioState(
                        "Select station",
                        "No stations available",
                        "",
                        LCDArrowState.Off,
                        LEDState.Off,
                        ButtonBehaviourType.Override
                    );

                var selectedStation = stations[Mathf.Clamp(stationIndex, 0, stations.Count - 1)];
                var content = $"{selectedStation.yardId}";
                return new CommsRadioState(
                    "Select station",
                    content,
                    "Click to confirm ",
                    LCDArrowState.Off,
                    LEDState.Off,
                    ButtonBehaviourType.Override
                );
            }
            else // Track mode
            {
                if (stations.Count == 0)
                    return new CommsRadioState("Select track", "No tracks available", "", LCDArrowState.Off, LEDState.Off, ButtonBehaviourType.Override);

                var selectedStation = stations[stationIndex];
                var stationTracks = sortedTracks.Where(t => t.yardId == selectedStation.yardId).ToList();
                if (stationTracks.Count == 0)
                    return new CommsRadioState("Select track", "No tracks available", "", LCDArrowState.Off, LEDState.Off, ButtonBehaviourType.Override);

                var selectedTrack = stationTracks[Mathf.Clamp(trackIndex, 0, stationTracks.Count - 1)];
                var content = $"**{selectedTrack.SignIDTrackPart}**";
                return new CommsRadioState(
                    $"Track for {selectedStation.yardId}",
                    content,
                    "Click to confirm track",
                    LCDArrowState.Off,
                    LEDState.Off,
                    ButtonBehaviourType.Override
                );
            }
        }

        public override AStateBehaviour OnAction(CommsRadioUtility util, InputAction action)
        {
            if (_tracks.Count == 0 || _stations.Count == 0) return this;

            if (_mode == SelectorMode.Station)
            {
                int nextStation = _stationIndex;
                switch (action)
                {
                    case InputAction.Up:
                        nextStation = (_stationIndex + 1) % _stations.Count;
                        break;
                    case InputAction.Down:
                        nextStation = (_stationIndex - 1 + _stations.Count) % _stations.Count;
                        break;
                    case InputAction.Activate:
                        // Enter track selection mode for this station
                        return new TrackSelector(onRouteSelected, parent, nextStation, 0, _tracks, SelectorMode.Track);
                    default:
                        return this;
                }
                return new TrackSelector(onRouteSelected, parent, nextStation, 0, _tracks, SelectorMode.Station);
            }
            else // Track mode
            {
                var stationTracks = _tracks.Where(t => t.yardId == _stations[_stationIndex].yardId).ToList();
                if (stationTracks.Count == 0) return this;
                int nextTrack = _trackIndex;
                switch (action)
                {
                    case InputAction.Up:
                        nextTrack = (nextTrack + 1) % stationTracks.Count;
                        break;
                    case InputAction.Down:
                        nextTrack = (nextTrack - 1 + stationTracks.Count) % stationTracks.Count;
                        break;
                    case InputAction.Activate:
                        RouteSetterDebug.Log($"[RouteSetter] Selected: {stationTracks[_trackIndex].FullID}");
                        onRouteSelected(stationTracks[_trackIndex]);
                        return parent();
                    default:
                        return this;
                }
                return new TrackSelector(onRouteSelected, parent, _stationIndex, nextTrack, _tracks, SelectorMode.Track);
            }
        }

        private List<TrackID> LoadTracks()
        {
            var list = LoadTracksStatic();
            if (list.Count == 0)
            {
                list.AddRange(new[]
                {
                    new TrackID("SM", "A", "01", "Y"),
                    new TrackID("SM", "A", "02", "Y"),
                    new TrackID("SM", "A", "03", "Y")
                });
            }
            return list;
        }

        private static List<TrackID> LoadTracksStatic()
        {
            var list = new List<TrackID>();
            var graph = Switcher.pathFinder?.Graph;
            if (graph == null)
            {
                RouteSetterDebug.LogWarning("[RouteSetter] Pathfinder graph is null");
                return list;
            }

            foreach (var id in graph.Keys)
            {
                if (!string.IsNullOrEmpty(id) && id.Contains("]_"))
                {
                    if (TrackIDParser.TryParse(id, out var trackId))
                    {
                        list.Add(trackId);
                    }
                    else
                    {
                        Debug.LogError($"[RouteSetter] Error parsing track {id}: no match for TrackID format");
                    }
                }
            }
            RouteSetterDebug.Log($"[RouteSetter] Loaded {list.Count} tracks");
            return list;
        }
    }
}