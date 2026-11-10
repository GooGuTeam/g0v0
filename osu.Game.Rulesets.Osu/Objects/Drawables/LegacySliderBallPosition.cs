// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osuTK;

namespace osu.Game.Rulesets.Osu.Objects.Drawables
{
    /// <summary>
    ///     Samples stable's integer-timed movement animations without changing gameplay geometry.
    /// </summary>
    internal class LegacySliderBallPosition
    {
        internal int EndTime { get; private set; }

        internal int EdgeCount => edges.Count;
        internal IReadOnlyList<LegacySliderBallPath.Edge> Edges => edges;
        internal Vector2 Direction { get; private set; }

        internal int Revision { get; private set; }
        internal bool HasActiveMovement { get; private set; }
        private readonly List<LegacySliderBallPath.Edge> edges = new List<LegacySliderBallPath.Edge>();
        private readonly Dictionary<int, double> spanStarts = new Dictionary<int, double>();
        private SliderPath? currentPath;
        private int pathVersion;
        private double startTime;
        private double velocity;
        private int spanCount;
        private bool valid;
        private Vector2 pathOrigin;
        private LegacySliderPath? legacyPath;
        private double[] edgeDurations = Array.Empty<double>();
        private int[] edgeEnds = Array.Empty<int>();
        private int cachedSpan = -1;
        private int cachedSpanStart;
        private int? lastClock;
        private Vector2 lastPosition;

        public bool TryGetPosition(SliderPath path, double start, double speed, int spans, double time, out Vector2 position, Vector2 origin = default)
        {
            if (currentPath != path || pathVersion != path.Version.Value || startTime != start || velocity != speed || spanCount != spans || pathOrigin != origin
                || !ReferenceEquals(legacyPath, path.LegacyPath))
                refresh(path, start, speed, spans, origin);

            position = default;
            double rounded = Math.Round(time);
            if (!valid || !double.IsFinite(rounded) || rounded < int.MinValue || rounded > int.MaxValue)
                return false;

            int clock = (int)rounded;
            HasActiveMovement = clock >= Math.Truncate(startTime) && clock <= EndTime;

            if (lastClock != clock)
            {
                lastPosition = sample(clock);
                lastClock = clock;
            }

            position = lastPosition - pathOrigin;
            return true;
        }

        private void refresh(SliderPath path, double start, double speed, int spans, Vector2 origin)
        {
            Revision++;
            currentPath = path;
            pathVersion = path.Version.Value;
            startTime = start;
            velocity = speed;
            spanCount = spans;
            pathOrigin = origin;
            legacyPath = path.LegacyPath;
            cachedSpan = -1;
            lastClock = null;
            spanStarts.Clear();
            valid = false;

            if (spans <= 0 || !double.IsFinite(start) || start < int.MinValue || start > int.MaxValue
                || !(speed > 0) || !double.IsFinite(speed * 1000))
                return;

            if (!LegacySliderBallPath.Build(path, origin, edges))
                return;

            int count = edges.Count;

            if (edgeDurations.Length != count)
            {
                edgeDurations = new double[count];
                edgeEnds = new int[count];
            }

            // Stable casts cumulative-length differences to float, but its x86 JIT keeps
            // the following multiply at extended precision before the double division.
            // The product of a float and 1000 is exact in double. Rounding it to float here
            // can shift a truncated movement boundary by 1ms and select an unrelated edge.
            double length = 0;

            for (int i = 0; i < count; i++)
            {
                double previous = length;
                length += (edges[i].To - edges[i].From).Length;
                double duration = 1000.0 * (float)(length - previous) / (speed * 1000);
                if (!double.IsFinite(duration) || duration < 0)
                    return;

                edgeDurations[i] = duration;
            }

            spanStarts.Add(0, Math.Truncate(start));
            double finalTime = getSpanStart(spans);
            // Overflowed stable animations are outside this sampler's domain. Keep the existing
            // continuous visual as a fallback rather than hiding the ball or changing lifetimes.
            if (!double.IsFinite(finalTime) || finalTime < int.MinValue || finalTime > int.MaxValue)
                return;

            EndTime = (int)finalTime;
            valid = true;
        }

        private Vector2 sample(int clock)
        {
            if (clock < Math.Truncate(startTime)) return edges[0].From;

            if (clock > EndTime) return spanCount % 2 == 0 ? edges[0].From : edges[^1].To;

            if (cachedSpan < 0 || clock <= cachedSpanStart || clock > edgeEnds[^1])
            {
                // Inclusive ends: choose the FIRST span ending at or after this clock tick.
                // At a shared boundary a previous span can win, including after a backward seek.
                int lo = 0;
                int hi = spanCount - 1;

                while (lo < hi)
                {
                    int mid = lo + (hi - lo) / 2;
                    if ((int)getSpanStart(mid + 1) < clock)
                        lo = mid + 1;
                    else
                        hi = mid;
                }

                cachedSpan = lo;
                double accumulated = getSpanStart(lo);
                cachedSpanStart = (int)accumulated;

                for (int i = 0; i < edgeEnds.Length; i++)
                {
                    accumulated += edgeDurations[lo % 2 == 0 ? i : edgeEnds.Length - 1 - i];
                    edgeEnds[i] = (int)accumulated;
                }
            }

            int first = 0;
            int last = edgeEnds.Length - 1;

            while (first < last)
            {
                int mid = first + (last - first) / 2;
                if (edgeEnds[mid] < clock)
                    first = mid + 1;
                else
                    last = mid;
            }

            bool reverse = cachedSpan % 2 != 0;
            int edge = reverse ? edgeEnds.Length - 1 - first : first;
            Vector2 from = reverse ? edges[edge].To : edges[edge].From;
            Vector2 to = reverse ? edges[edge].From : edges[edge].To;
            int begin = first == 0 ? cachedSpanStart : edgeEnds[first - 1];
            int end = edgeEnds[first];
            Direction = to - from;

            // OsuMathHelper's Vector2 overload takes a float endTime and returns the START
            // for a zero-duration animation. pSprite gives the first active movement priority.
            float elapsed = (float)((double)clock - begin);
            float duration = (float)((double)(float)end - begin);
            if (elapsed == 0 || duration == 0 || clock < begin)
                return from;

            // The x86 tween likewise retains the coordinate subtraction until it enters
            // double ApplyEasing. Premature float cancellation loses whole visible pixels
            // on Aspire's huge edges, even when their integer boundaries are correct.
            double progress = (double)elapsed / duration;
            return new Vector2((float)(((double)to.X - from.X) * progress + from.X), (float)(((double)to.Y - from.Y) * progress + from.Y));
        }

        private double getSpanStart(int index)
        {
            if (spanStarts.TryGetValue(index, out double cached))
                return cached;

            double time = advancePairs(spanStarts[0], index / 2);
            if (index % 2 != 0)
                time = advanceSpan(time, false);

            // Seeking through many tiny spans must not grow a per-span cache for the
            // entire beatmap. These are disposable search hints, not the animation list.
            if (spanStarts.Count >= 256)
            {
                double origin = spanStarts[0];
                spanStarts.Clear();
                spanStarts.Add(0, origin);
            }

            spanStarts.Add(index, time);
            return time;
        }

        private double advanceSpan(double time, bool reverse)
        {
            for (int i = 0; i < edgeDurations.Length; i++)
                time += edgeDurations[reverse ? edgeDurations.Length - 1 - i : i];
            return time;
        }

        private double advancePair(double time)
        {
            return advanceSpan(advanceSpan(time, false), true);
        }

        private double advancePairs(double time, int remaining)
        {
            while (remaining > 0)
            {
                double next = advancePair(time);
                remaining--;
                if (next == time || !double.IsFinite(next))
                    return next;

                if (time != 0 && remaining > 0)
                {
                    // Within a double binade every addition uses the same representable grid.
                    // Two equal cycle increments establish the repeat (including ties-to-even).
                    // Skip only strictly inside that binade; never replace cumulative addition
                    // with an ideal geometric span duration, which changes truncated boundaries.
                    double boundary = time > 0 ? Math.ScaleB(1, Math.ILogB(time) + 1) : -Math.ScaleB(1, Math.ILogB(-time));
                    if (boundary == time)
                        boundary *= 0.5;

                    if (next < boundary)
                    {
                        double second = advancePair(next);
                        double increment = next - time;

                        if (second < boundary && second - next == increment)
                        {
                            int skip = (int)Math.Min(remaining, Math.Floor((Math.BitDecrement(boundary) - next) / increment));

                            if (skip > 0)
                            {
                                // Guard the division's final rounding at a binade boundary.
                                if (next + skip * increment >= boundary)
                                    skip--;
                                next += skip * increment;
                                remaining -= skip;
                            }
                        }
                    }
                }

                time = next;
            }

            return time;
        }
    }
}
