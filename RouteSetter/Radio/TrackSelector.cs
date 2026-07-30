using CommsRadioAPI;
using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RouteSetter;
using System.Threading.Tasks;
using UnityEngine;
using DV;
namespace RouteSetter.Radio
{
   
    
    static class TrackSorter
    {

        public static List<StationTrack> SortTracksByStation_LINQ(List<StationTrack> allTracks)
        {
            return allTracks
                .OrderBy(t => t.StationName)
                .ThenBy(t => t.Yard)
                .ThenBy(t => t.Track)
                .ToList();
        }


    }
    internal class TrackSelector : AStateBehaviour
    {
        internal enum SelectorMode { Station, Track }
        private readonly SelectorMode _mode;
        private readonly List<StationTrack> _tracks;
        private readonly List<StationTrack> _stations; // Now a list of unique StationTrack per station
        private readonly int _stationIndex;
        private readonly int _trackIndex;
        private readonly Action<StationTrack> onRouteSelected;
        private readonly Func<AStateBehaviour> parent; // where to go after reporting the result

        public TrackSelector(Action<StationTrack> onRouteSelected, Func<AStateBehaviour> parent, int stationIndex = 0, int trackIndex = 0, List<StationTrack> tracks = null, SelectorMode mode = SelectorMode.Station)
            : base(CreateState(stationIndex, trackIndex, tracks, mode))
        {
            _tracks = tracks != null
                ? TrackSorter.SortTracksByStation_LINQ(tracks)
                : TrackSorter.SortTracksByStation_LINQ(LoadTracks());

            // Unique StationTrack per station (first occurrence)
            _stations = _tracks
                .GroupBy(t => t.StationName)
                .Select(g => g.First())
                .OrderBy(t => t.StationName)
                .ToList();

            _stationIndex = _stations.Count > 0 ? Mathf.Clamp(stationIndex, 0, _stations.Count - 1) : 0;
            _trackIndex = trackIndex;
            _mode = mode;
            this.onRouteSelected = onRouteSelected;
            this.parent = parent;
        }

        private static CommsRadioState CreateState(int stationIndex, int trackIndex, List<StationTrack> tracks, SelectorMode mode)
        {
            var sortedTracks = tracks != null
                ? TrackSorter.SortTracksByStation_LINQ(tracks)
                : TrackSorter.SortTracksByStation_LINQ(LoadTracksStatic());

            var stations = sortedTracks
                .GroupBy(t => t.StationName)
                .Select(g => g.First())
                .OrderBy(t => t.StationName)
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
                var content = $"{selectedStation.StationName}";
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
                var stationTracks = sortedTracks.Where(t => t.StationName == selectedStation.StationName).ToList();
                if (stationTracks.Count == 0)
                    return new CommsRadioState("Select track", "No tracks available", "", LCDArrowState.Off, LEDState.Off, ButtonBehaviourType.Override);

                var selectedTrack = stationTracks[Mathf.Clamp(trackIndex, 0, stationTracks.Count - 1)];
                var content = $"**{selectedTrack.GetTrackName()}**";
                return new CommsRadioState(
                    $"Track for {selectedStation.StationName}",
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
                        return new TrackSelector(onRouteSelected,parent, nextStation, 0, _tracks, SelectorMode.Track);
                    default:
                        return this;
                }
                return new TrackSelector(onRouteSelected, parent, nextStation, 0, _tracks, SelectorMode.Station);
            }
            else // Track mode
            {
                var stationTracks = _tracks.Where(t => t.StationName == _stations[_stationIndex].StationName).ToList();
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
                        RouteSetterDebug.Log($"[RouteSetter] Selected: {stationTracks[_trackIndex].GetFullName()}");
                        onRouteSelected(stationTracks[_trackIndex]);
                        return parent();
                    default:
                        return this;
                }
                return new TrackSelector(onRouteSelected, parent, _stationIndex, nextTrack, _tracks, SelectorMode.Track);
            }
        }

        private List<StationTrack> LoadTracks()
        {
            var list = LoadTracksStatic();
            if (list.Count == 0)
            {
                list.AddRange(new[]
                {
                new StationTrack("000001_[SM]_[Y]_[A-01]"),
                new StationTrack("000002_[SM]_[Y]_[A-02]"),
                new StationTrack("000003_[SM]_[Y]_[A-03]")
            });
            }
            return list;
        }
        private static List<StationTrack> LoadTracksStatic()
        {
            var list = new List<StationTrack>();
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
                    try { list.Add(new StationTrack(id)); }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[RouteSetter] Error parsing track {id}: {ex.Message}");
                    }
                }
            }
            RouteSetterDebug.Log($"[RouteSetter] Loaded {list.Count} tracks");
            return list;
        }
    }
}


