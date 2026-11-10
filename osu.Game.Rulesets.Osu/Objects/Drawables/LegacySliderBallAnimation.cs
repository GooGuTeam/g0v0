// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Rulesets.Osu.Objects.Drawables
{
    /// <summary>
    ///     pAnimation's phase-preserving reverse setter and integer-clock frame sampler.
    ///     Reverse is supplied before updating the slider's tick cursor, as in stable.
    ///     This class owns visual state only, never judgements or gameplay clocks.
    /// </summary>
    internal sealed class LegacySliderBallAnimation
    {
        public bool Reverse { get; private set; }

        private bool firstFrame = true;
        private double animationStart;
        private double delay;
        private int frames;
        private int lastTick = -1;
        private int frame;

        public static float Rotation(Vector2 direction, bool reverse)
        {
            return ((float)Math.Atan2(direction.Y, direction.X) - (reverse ? (float)Math.PI : 0)) * (180f / (float)Math.PI);
        }

        public void Reset()
        {
            firstFrame = true;
            Reverse = false;
            lastTick = -1;
        }

        public int Update(int clock, int start, int end, int spans, IReadOnlyList<int> ticks, int frameCount, double frameDelay, bool tracking = false)
        {
            if (frameCount < 1 || spans < 1 || !(frameDelay > 0) || !double.IsFinite(frameDelay)) return 0;

            if (frames != frameCount) Reset();
            frames = frameCount;
            int count = ticks.Count / spans;
            bool nextReverse = count > 0 && (((lastTick + count + 1) / count) & 1) == 0;

            if (nextReverse != Reverse && !firstFrame && delay > 0 && animationStart < clock)
            {
                double elapsed = (clock - animationStart) / delay;
                double cycle = Math.Floor(elapsed / frames) * frames;
                double phase = frames - 1 - (elapsed - cycle);
                animationStart = clock - (cycle + phase) * delay;
            }

            Reverse = nextReverse;
            if (frameDelay != delay && !firstFrame && delay > 0 && animationStart < clock)
                animationStart = clock - (clock - animationStart) / delay * frameDelay;
            delay = frameDelay;

            if (clock >= start && clock <= end)
            {
                int lo = 0, hi = Math.Max(0, ticks.Count - 1);

                while (lo < hi)
                {
                    int mid = lo + (hi - lo) / 2;
                    if (ticks[mid] <= clock) lo = mid + 1;
                    else hi = mid;
                }

                int nextTick = lo - 1;
                if (!tracking || nextTick > lastTick) lastTick = nextTick;
            }
            else lastTick = -1;

            if (!firstFrame && clock < animationStart) firstFrame = true;

            if (firstFrame)
            {
                animationStart = start;
                firstFrame = false;
            }

            int index = (int)Math.Max(0, Math.Floor((clock - animationStart) / delay));
            index %= frameCount;
            frame = Reverse ? Math.Max(0, frameCount - 1 - index) : index;
            return frame;
        }
    }
}
