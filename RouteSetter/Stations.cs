using System;
using System.Collections.Generic;
using System.Linq;
using CommsRadioAPI;
using DV;
using DV.Logic.Job;
using HarmonyLib;
using UnityEngine;

namespace AutoPilot
{
    struct StationTrack
    {
        public string ID;
        public string StationName;
        public string Yard;
        public string Track;

        public string GetFullName() => $"{ID}_{StationName}_{Yard}_{Track}";
        public string GetFullNameNoID()
        {
            if (Track.Contains("--")) return $"{ID}_{StationName}_{Yard}_{Track}";
            return $"{StationName}_{Yard}_{Track}";
        }

        public StationTrack(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                throw new ArgumentException("FullName cannot be null or empty.", nameof(fullName));

            var parts = fullName.Split('_');
            if (parts.Length < 4)
                throw new FormatException($"Track ID doesn't have enough parts: {fullName}");

            ID = parts[0];
            StationName = parts[1];
            Yard = parts[2];
            Track = parts.Length > 4 ? string.Join("_", parts.Skip(3)) : parts[3];

            Debug.Log($"[AutoPilot] Parsed track: ID={ID}, Station={StationName}, Yard={Yard}, Track={Track}");
        }

    }
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

    internal class DestSelector : AStateBehaviour
    {
        private readonly List<StationTrack> _tracks;
        private readonly int _index;
        
        public DestSelector(int index = 0, List<StationTrack> tracks = null)
            : base(CreateState(index, tracks))
        {
            

                    _tracks = tracks != null
            ? TrackSorter.SortTracksByStation_LINQ(tracks)
            : TrackSorter.SortTracksByStation_LINQ(LoadTracks());
            _index = _tracks.Count > 0
                ? Mathf.Clamp(index, 0, _tracks.Count - 1)
                : 0;
            Debug.Log($"[AutoPilot] DestSelector initialized at index {_index}/{_tracks.Count - 1}");
        }

        // Static helper to build the radio state
        private static CommsRadioState CreateState(int index, List<StationTrack> tracks)
        {
            
            var list = tracks ?? LoadTracksStatic();
            int i = list.Count > 0 ? Mathf.Clamp(index, 0, list.Count - 1) : 0;
            string content = list.Count > 0 ? list[i].GetFullNameNoID() : "No stations available";
            var newstate = new CommsRadioState(
            "Select destination",
            content,
            "Click to confirm", // actionText
            LCDArrowState.Off,
            LEDState.Off,
            ButtonBehaviourType.Override
            );
            return newstate;
        }

        // Load instance tracks
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

        // Static track loader for state creation
        private static List<StationTrack> LoadTracksStatic()
        {
            var list = new List<StationTrack>();
            var graph = Switcher.pathFinder?.graph;
            if (graph == null)
            {
                Debug.LogWarning("[AutoPilot] Pathfinder graph is null");
                return list;
            }

            foreach (var id in graph.Keys)
            {
                if (!string.IsNullOrEmpty(id) && id.Contains("]_"))
                {
                    try { list.Add(new StationTrack(id)); }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AutoPilot] Error parsing track {id}: {ex.Message}");
                    }
                }
            }
            Debug.Log($"[AutoPilot] Loaded {list.Count} tracks");
            return list;
        }

        public override AStateBehaviour OnAction(CommsRadioUtility util, InputAction action)
        {
            Debug.Log($"[AutoPilot] Action: {action}");
            if (_tracks.Count == 0) return this;

            int next = _index;
            switch (action)
            {
                case InputAction.Up:
                    next = (_index + 1) % _tracks.Count;
                    break;
                case InputAction.Down:
                    next = (_index - 1 + _tracks.Count) % _tracks.Count;
                    break;
                case InputAction.Activate:
                    Debug.Log($"[AutoPilot] Selected: {_tracks[_index].GetFullName()}");
                    return new SwitchJunctionsStateBehaviour("SelctedStation:" + _tracks[_index].GetFullNameNoID(), _tracks[_index]);
                default:
                    return this;
            }

            return new DestSelector(next, _tracks);
        }
    }
}