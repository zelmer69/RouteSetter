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
        private TrackID selectedtrack;

        public SelectStart(int selectedIndex = 0, TrackID startTrack = null)
            : base("Select start", injectNames(options, ResolveTrack(startTrack)), selectedIndex)
        {
            var resolved = ResolveTrack(startTrack);
            selectedtrack = resolved;
            if (resolved != null && !string.IsNullOrEmpty(resolved.yardId))
            {
                Debug.Log("selected track:" + resolved.FullID);
                Switcher.SavedStartTrack = resolved;
            }
        }


        private static TrackID ResolveTrack(TrackID passedTrack) =>
            passedTrack != null && !string.IsNullOrEmpty(passedTrack.yardId) ? passedTrack : Switcher.SavedStartTrack;

        
        static string[] injectNames(string[] source, TrackID track)
        {
            string[] result = (string[])source.Clone();
            result[0] = result[0] + ":" + (PlayerManager.LastLoco ? PlayerManager.LastLoco.FrontBogie.track.ToString() : "No loco");
            result[1] = result[1] + ":" + (track != null && !string.IsNullOrEmpty(track.yardId) ? track.yardId + track.yardId : "none");
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
