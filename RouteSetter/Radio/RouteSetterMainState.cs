using CommsRadioAPI;

namespace RouteSetter
{
    internal class RouteSetterMainState : AStateBehaviour
    {
        public RouteSetterMainState()
            : base(new CommsRadioState(
                "Route Setter",
                "",
                "Click confirm",
                LCDArrowState.Off,
                LEDState.Off,
                DV.ButtonBehaviourType.Regular))
        {
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            switch (action)
            {
                case InputAction.Activate:
                    return new RouteSetterState(0);
                default:
                    throw new System.ArgumentException();
            }
        }
    }
}