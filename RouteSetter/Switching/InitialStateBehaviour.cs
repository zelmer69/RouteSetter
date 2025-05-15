using CommsRadioAPI;

namespace AutoPilot
{
    internal class InitialStateBehaviour : AStateBehaviour
    {
        
        public InitialStateBehaviour()
            : base(new CommsRadioState("Automatic switch setter", "Sets route from your last loco to desired destanation","Click to confirm")) { }
        
        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            
            switch (action)
            {
                
                case InputAction.Activate:
                    DestSelector destSelector= new DestSelector(0);
                    
                    return destSelector;
                default:
                    throw new System.ArgumentException();
            }
        }
    }
}
