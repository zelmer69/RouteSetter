using CommsRadioAPI;
using DV;
using System;
using UnityEngine;

namespace RouteSetter
{
    internal class PathModeSelector : AStateBehaviour
    {
        private static readonly PathFindingMode[] Modes = (PathFindingMode[])Enum.GetValues(typeof(PathFindingMode));
        private readonly int _index;
        private readonly StationTrack _destination;

        public PathModeSelector(int index = 0, StationTrack destination = default)
            : base(CreateState(index))
        {
            _index = Mathf.Clamp(index, 0, Modes.Length - 1);
            _destination = destination;
        }

        private static CommsRadioState CreateState(int index)
        {
            string[] modeNames = { "Shortest Path", "Shortest Path (Avoid U-turns)", "Shortest Path (Better performance)" };
            string content = modeNames[index];
            return new CommsRadioState(
                "Select Path Mode",
                content,
                "Click to confirm",
                LCDArrowState.Off,
                LEDState.Off,
                ButtonBehaviourType.Override
            );
        }

        public override AStateBehaviour OnAction(CommsRadioUtility util, InputAction action)
        {
            int next = _index;
            switch (action)
            {
                case InputAction.Up:
                    next = (_index + 1) % Modes.Length;
                    break;
                case InputAction.Down:
                    next = (_index - 1 + Modes.Length) % Modes.Length;
                    break;
                case InputAction.Activate:
                    return new SwitchJunctionsStateBehaviour("Finding route", _destination, "Click to confirm", Modes[_index]);
                default:
                    return this;
            }
            return new PathModeSelector(next, _destination);
        }
    }

}
