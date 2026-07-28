using CommsRadioAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RouteSetter.Radio
{
    internal abstract class ARadioState_ListBase: AStateBehaviour
    {
        protected readonly int selectedIndex;
        private readonly string[] menuOptions;

        protected ARadioState_ListBase(string title, string[] menuOptions, int selectedIndex)
            : base(new CommsRadioState(
                title,
                string.Join("\n", PrepareMenu(menuOptions, selectedIndex)),
                "Click to confirm",
                LCDArrowState.Off,
                LEDState.Off,
                DV.ButtonBehaviourType.Override))
        {
            this.menuOptions = menuOptions;
            this.selectedIndex = selectedIndex;
        }

        private static string[] PrepareMenu(string[] options, int selectedItem)
        {
            string[] result = new string[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                result[i] = (i == selectedItem) ? "*" + options[i] + "*" : options[i];
            }
            return result;
        }

        // Child must know how to construct a new instance of itself with a new index
        protected abstract ARadioState_ListBase CreateState(int selectedIndex);

        // Child must define what happens when the current selection is activated
        protected abstract AStateBehaviour OnActive(int selectedIndex);

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            switch (action)
            {
                case InputAction.Up:
                    return CreateState((selectedIndex + 1) % menuOptions.Length);
                case InputAction.Down:
                    return CreateState((selectedIndex - 1 + menuOptions.Length) % menuOptions.Length);
                case InputAction.Activate:
                    return OnActive(selectedIndex);
                default:
                    throw new System.ArgumentException();
            }
        }
    }

}
