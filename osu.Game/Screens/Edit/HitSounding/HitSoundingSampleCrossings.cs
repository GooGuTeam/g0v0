// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// Resolves the discrete hit nodes crossed between two clock positions.
    /// </summary>
    /// <remarks>
    /// This is the allocation-free core of <see cref="HitSoundingSamplePlayback"/>'s crossing logic, split out so the boundary
    /// behaviour is directly testable against real <see cref="HitSoundingNode"/>s.
    /// The input must be sorted by <see cref="HitSoundingNode.Time"/>, as provided by <see cref="HitSoundingEditModel.Nodes"/>.
    /// Body / loop nodes are excluded: they have no discrete hit time and must not be played as if they were circles.
    /// </remarks>
    public static class HitSoundingSampleCrossings
    {
        /// <summary>
        /// Appends the nodes crossed when moving from <paramref name="from"/> to <paramref name="to"/>.
        /// </summary>
        /// <remarks>
        /// The from position is treated as already reached and the to position as newly reached. Forward crossings therefore use
        /// <c>(from, to]</c> in ascending order, and backward crossings use <c>[to, from)</c> in descending order.
        /// </remarks>
        /// <param name="nodes">All hit nodes, sorted by time.</param>
        /// <param name="from">The previously displayed position.</param>
        /// <param name="to">The newly displayed position.</param>
        /// <param name="result">The list to append to. It is not cleared.</param>
        public static void Collect(IReadOnlyList<HitSoundingNode> nodes, double from, double to, List<HitSoundingNode> result)
        {
            if (double.IsNaN(from) || double.IsNaN(to) || from == to)
                return;

            if (to > from)
            {
                int index = firstIndexAfter(nodes, from);

                for (; index < nodes.Count; index++)
                {
                    HitSoundingNode node = nodes[index];

                    if (node.Time > to)
                        break;

                    if (node.IsBody)
                        continue;

                    result.Add(node);
                }
            }
            else
            {
                int start = firstIndexAtOrAfter(nodes, to);
                int end = firstIndexAtOrAfter(nodes, from);

                for (int i = end - 1; i >= start; i--)
                {
                    HitSoundingNode node = nodes[i];

                    if (node.IsBody)
                        continue;

                    result.Add(node);
                }
            }
        }

        /// <summary>
        /// Resolves the effective start of a preview crossing, given the position the caller saw and the position already consumed.
        /// </summary>
        /// <remarks>
        /// The caller's movement determines the direction, so a new scrub session after an unrelated seek is handled correctly even
        /// when it starts on the opposite side of the previous session's position. If the tracked position lies inside the caller's
        /// interval, it is used as the start so overlapping successive frames cannot replay a node. If it lies outside, the caller's
        /// position wins: the untracked gap is skipped rather than replayed, which also covers the first frame of a new session.
        /// </remarks>
        /// <param name="from">The start position supplied by the caller.</param>
        /// <param name="trackedPosition">The position already covered by a previous preview call.</param>
        /// <param name="to">The destination position.</param>
        /// <returns>The effective start position.</returns>
        public static double GetPreviewStart(double from, double trackedPosition, double to)
        {
            if (double.IsNaN(from))
                return trackedPosition;

            if (from == to)
                return to;

            if (from < to)
                return trackedPosition >= from && trackedPosition <= to ? trackedPosition : from;

            return trackedPosition >= to && trackedPosition <= from ? trackedPosition : from;
        }

        /// <summary>
        /// Returns the first index whose time is strictly after <paramref name="time"/>.
        /// </summary>
        private static int firstIndexAfter(IReadOnlyList<HitSoundingNode> nodes, double time)
        {
            int low = 0;
            int high = nodes.Count;

            while (low < high)
            {
                int mid = low + (high - low) / 2;

                if (nodes[mid].Time <= time)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }

        /// <summary>
        /// Returns the first index whose time is greater than or equal to <paramref name="time"/>.
        /// </summary>
        private static int firstIndexAtOrAfter(IReadOnlyList<HitSoundingNode> nodes, double time)
        {
            int low = 0;
            int high = nodes.Count;

            while (low < high)
            {
                int mid = low + (high - low) / 2;

                if (nodes[mid].Time < time)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }
    }
}
