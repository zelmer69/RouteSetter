using CommsRadioAPI;

namespace RouteSetter
{
    internal class InitialStateBehaviour : AStateBehaviour
    {
        private int selectedIndex = 0;
        private static readonly string[] menuOptions = { "Set Route", "Route Display" };

        public InitialStateBehaviour(int selectedIndex = 1)
            : base(new CommsRadioState(
                "Route Setter",
                menuOptions[selectedIndex],
                "Click to confirm",
                LCDArrowState.Off,
                LEDState.Off,
                DV.ButtonBehaviourType.Regular))
        {
            this.selectedIndex = selectedIndex;
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            switch (action)
            {
                case InputAction.Up:
                    return new InitialStateBehaviour((selectedIndex + 1) % menuOptions.Length);
                case InputAction.Down:
                    return new InitialStateBehaviour((selectedIndex - 1 + menuOptions.Length) % menuOptions.Length);
                case InputAction.Activate:
                    if (selectedIndex == 0)
                        return new DestSelector(0);
                    else
                        return new RouteDisplayMenuState();
                default:
                    throw new System.ArgumentException();
            }
        }
    }
}