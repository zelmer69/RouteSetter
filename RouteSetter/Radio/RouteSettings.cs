using CommsRadioAPI;
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
        private StationTrack selectedtrack;

        static string[] UpdateOptions(string[] toUpdate, StationTrack track)
        {
            string[] updated = new string[toUpdate.Length];
            switch (Switcher.StartTrackSetting)
            {
                case StartTrackType.LastLoco:
                    updated[0] = toUpdate[0] + ": from Loco";
                    break;
                case StartTrackType.SelectedTrack:
                    updated[0] = toUpdate[0] + ":" + Switcher.SavedStartTrack.StationName;
                    break;
            }
            updated[1] = toUpdate[1] + ":" +
                (!string.IsNullOrEmpty(track.StationName) ? track.StationName : "none");
            updated[2] = toUpdate[2];
            updated[3] = toUpdate[3];
            return updated;
        }

        private static StationTrack ResolveEndTrack(StationTrack passedTrack) =>
            !string.IsNullOrEmpty(passedTrack.StationName) ? passedTrack : Switcher.SavedEndTrack;

        public RouteSettings(int selectedIndex = 0, StationTrack selected = new StationTrack())
            : base("Select route", UpdateOptions(options, ResolveEndTrack(selected)), selectedIndex)
        {
            this.selectedtrack = ResolveEndTrack(selected);
            if (!string.IsNullOrEmpty(this.selectedtrack.StationName))
                Switcher.SavedEndTrack = this.selectedtrack; // if you want it persisted, mirroring SavedStartTrack
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
