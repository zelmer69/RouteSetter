using CommsRadioAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DV.Logic.Job;
using UnityEngine;

namespace RouteSetter.Radio
{
    internal class SelectStart : ARadioState_ListBase
    {
        private static readonly string[] options = { "Current Loco", "Select track", "Back" };
        private StationTrack selectedtrack;

        public SelectStart(int selectedIndex = 0, StationTrack startTrack = new StationTrack())
            : base("Select start", injectNames(options, ResolveTrack(startTrack)), selectedIndex)
        {
            var resolved = ResolveTrack(startTrack);
            selectedtrack = resolved;

            if (!string.IsNullOrEmpty(resolved.StationName))
            {
                Debug.Log("selected track:" + resolved.GetFullName());
                Switcher.SavedStartTrack = resolved;
            }
        }


        private static StationTrack ResolveTrack(StationTrack passedTrack) =>
            !string.IsNullOrEmpty(passedTrack.StationName) ? passedTrack : Switcher.SavedStartTrack;

        static string[] injectNames(string[] source, StationTrack track)
        {
            string[] result = (string[])source.Clone();
            result[0] = result[0] + ":" + (PlayerManager.LastLoco ? PlayerManager.LastLoco.FrontBogie.track.ToString() : "No loco");
            result[1] = result[1] + ":" + (!string.IsNullOrEmpty(track.StationName) ? track.StationName + track.Track : "none");
            return result;
        }

        protected override ARadioState_ListBase CreateState(int selectedIndex) =>
            new SelectStart(selectedIndex, selectedtrack);

        protected override AStateBehaviour OnActive(int selectedIndex)
        {
            switch (selectedIndex)
            {
                case 0: return new RouteSettings();
                case 1:
                    return new TrackSelector(
                        onRouteSelected: track => selectedtrack = track,
                        parent: () => new SelectStart(0, selectedtrack)
                    );
                case 2: return new RouteSettings();
                default: throw new System.ArgumentException();
            }
        }
    }
}
