using CommsRadioAPI;
using HarmonyLib;
using RouteSetter.Radio;
using System.Runtime.InteropServices.WindowsRuntime;

namespace RouteSetter
{
    internal class RouteSetterState : ARadioState_ListBase
    {
        private static readonly string[] options = { "Select route", "Draw route", "Back" };
        
        public RouteSetterState(int selectedIndex = 0)
        : base("Route Setter", options, selectedIndex)
        {
        }
        protected override ARadioState_ListBase CreateState(int selectedIndex) => new RouteSetterState(selectedIndex);

        protected override AStateBehaviour OnActive(int selectedIndex)
        {
            switch (selectedIndex)
            {
                case 0: return new RouteSettings();
                case 1: return new RouteDisplayMenuState();
                case 2: return new RouteSetterMainState();
                default: throw new System.ArgumentException();
            }
        }

    }
}



