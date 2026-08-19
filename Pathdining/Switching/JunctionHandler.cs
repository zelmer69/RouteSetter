using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pathfinding.Switching
{
    internal class JunctionHandler
    {
        public static bool TrySwitchJunctionToward(RailTrack from, RailTrack to)
        {
            Junction junction = FindConnectingJunction(from, to);
            if (junction == null)
                return false; // tracks aren't directly joined by a junction

            // Is 'to' reachable via one of the switchable branches?
            int targetIndex = -1;
            for (int i = 0; i < junction.outBranches.Count; i++)
            {
                if (junction.outBranches[i].track == to)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex == -1)
            {
                // 'to' is on the single "in" side (the merge end) — nothing to switch
                return junction.inBranch != null && junction.inBranch.track == to;
            }

            // Cycle through branches until the desired one is selected
            int safety = junction.outBranches.Count;
            while (junction.selectedBranch != targetIndex && safety-- > 0)
            {
                junction.Switch(Junction.SwitchMode.REGULAR);
            }

            return junction.selectedBranch == targetIndex;
        }

        private static Junction FindConnectingJunction(RailTrack from, RailTrack to)
        {
            if (from.inJunction != null && JunctionTouches(from.inJunction, to))
                return from.inJunction;

            if (from.outJunction != null && JunctionTouches(from.outJunction, to))
                return from.outJunction;

            return null;
        }

        private static bool JunctionTouches(Junction junction, RailTrack track)
        {
            if (junction.inBranch != null && junction.inBranch.track == track)
                return true;

            foreach (Junction.Branch branch in junction.outBranches)
            {
                if (branch.track == track)
                    return true;
            }

            return false;
        }
    }
}
