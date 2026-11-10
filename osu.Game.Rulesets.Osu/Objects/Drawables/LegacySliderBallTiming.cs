// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Osu.Objects.Drawables
{
    internal static class LegacySliderBallTiming
    {
        // The old tick cursor controls animation reversal. Reconstruct it independently of
        // lazer's tick judgements; never move a scoring event to reproduce a visual effect.
        public static bool Build(IReadOnlyList<LegacySliderBallPath.Edge> edges, int start, int end, int spans,
                                 double velocity, double scoringDistance, double spatialLength, List<int> ticks)
        {
            ticks.Clear();
            if (!(scoringDistance > 0) || double.IsNaN(scoringDistance) || !(spatialLength > 0)
                || !double.IsFinite(spatialLength) || !(velocity > 0) || !double.IsFinite(velocity)
                || spans <= 0 || (long)edges.Count * spans > 2000000)
                return false;

            double fullLength = 0;
            foreach (var edge in edges) fullLength += (edge.To - edge.From).Length;
            double distance = Math.Min(scoringDistance, spatialLength);
            double travelled = 0, remainder = 0;

            for (int span = 0; span < spans; span++)
            {
                double remaining = fullLength;
                bool finished = false;

                for (int i = 0; i < edges.Count; i++)
                {
                    int index = span % 2 == 0 ? i : edges.Count - 1 - i;
                    float length = (edges[index].To - edges[index].From).Length;
                    remainder += length;

                    while (remainder >= distance && !finished)
                    {
                        // Bound hostile maps without allocating millions of visual events.
                        if (ticks.Count >= 100000) return false;

                        travelled += distance;
                        remainder -= distance;
                        remaining -= distance;
                        finished = remaining <= 0.01 * velocity * 1000;
                        if (!finished) ticks.Add(timeAt(travelled));
                    }
                }

                travelled += remainder;
                if (ticks.Count >= 100000) return false;

                ticks.Add(timeAt(travelled));

                if (finished) remainder = 0;
                else
                {
                    travelled -= distance - remainder;
                    remainder = distance - remainder;
                }
            }

            if (ticks.Count > 0) ticks[^1] = Math.Max(start + (end - start) / 2, ticks[^1] - 36);
            return true;

            int timeAt(double length) => (int)(start + (float)length / (velocity * 1000) * 1000);
        }
    }
}
