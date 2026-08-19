using CommsRadioAPI;
using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RouteSetter.Radio
{
    internal class RouteSettings : ARadioState_ListBase
    {
        private static readonly string[] options = { "Start", "End","confrim", "Back" };
        private TrackID selectedtrack;

        private static TrackID ResolveEndTrack(TrackID passedTrack) =>
            passedTrack != null && !string.IsNullOrEmpty(passedTrack.yardId)
                ? passedTrack
                : Switcher.SavedEndTrack; // this can still be null!

        public RouteSettings(int selectedIndex = 0, TrackID selected = null)
            : base("Select route", UpdateOptions(options, ResolveEndTrack(selected)), selectedIndex)
        {
            this.selectedtrack = ResolveEndTrack(selected);
            if (this.selectedtrack != null && !string.IsNullOrEmpty(this.selectedtrack.yardId))
                Switcher.SavedEndTrack = this.selectedtrack;
        }

        static string[] UpdateOptions(string[] toUpdate, TrackID track)
        {
            string[] updated = new string[toUpdate.Length];
            switch (Switcher.StartTrackSetting)
            {
                case StartTrackType.LastLoco:
                    updated[0] = toUpdate[0] + ": from Loco";
                    break;
                case StartTrackType.SelectedTrack:
                    // Switcher.SavedStartTrack could also be null — guard here too
                    updated[0] = toUpdate[0] + ":" + (Switcher.SavedStartTrack?.yardId ?? "none");
                    break;
            }
            updated[1] = toUpdate[1] + ":" +
                (track != null && !string.IsNullOrEmpty(track.yardId) ? track.yardId : "none");
            updated[2] = toUpdate[2];
            updated[3] = toUpdate[3];
            return updated;
        }

        protected override ARadioState_ListBase CreateState(int selectedIndex) =>
            new RouteSettings(selectedIndex, selectedtrack);

        protected override AStateBehaviour OnActive(int selectedIndex)
        {
            switch (selectedIndex)
            {
                case 0: return new SelectStart();
                case 1:
                    return new TrackSelector(
                        onRouteSelected: track => selectedtrack = track,
                        parent: () => new RouteSettings(1, selectedtrack)
                    );
                case 2: return new SwitchJunctionsStateBehaviour("select mode", Switcher.SavedEndTrack);
                case 3: return new RouteSetterMainState();
                default: throw new System.ArgumentException();
            }
        }
    }
}
