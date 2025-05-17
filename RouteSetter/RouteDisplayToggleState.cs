using CommsRadioAPI;

namespace RouteSetter
{
    internal class RouteDisplayMenuState : AStateBehaviour
    {
        public RouteDisplayMenuState()
            : base(new CommsRadioState(
                "Draw Line On Track",
                Switcher.RouteDisplayEnabled ? "Show : *ON*  OFF \n Click to confirm" : "Show: ON *OFF* \n Click to confirm",
                "Scroll to toggle",
                LCDArrowState.Off,
                LEDState.Off,
                DV.ButtonBehaviourType.Override))
        { }

        public override AStateBehaviour OnAction(CommsRadioUtility util, InputAction action)
        {
            switch (action)
            {
                case InputAction.Activate:
                    
                    return new InitialStateBehaviour(0);
                case InputAction.Down:
                    Switcher.RouteDisplayEnabled = !Switcher.RouteDisplayEnabled;
                    return new RouteDisplayMenuState();
                case InputAction.Up:
                    Switcher.RouteDisplayEnabled = !Switcher.RouteDisplayEnabled;
                    return new RouteDisplayMenuState();
                default:
                    return this;
            }
        }
    }
}