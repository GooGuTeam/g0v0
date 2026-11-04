// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Ensures that <see cref="HitObject"/>s are hit in order of appearance. The classic note lock.
    /// <remarks>
    /// Hits will be blocked until the previous <see cref="HitObject"/>s have been judged.
    /// </remarks>
    /// </summary>
    public class LegacyHitPolicy : IHitPolicy
    {
        public IHitObjectContainer? HitObjectContainer { get; set; }

        private readonly double hittableRange;

        public LegacyHitPolicy(double hittableRange = OsuHitWindows.MISS_WINDOW)
        {
            this.hittableRange = hittableRange;
        }

        internal static bool IsOverlappingSuccessor(DrawableSlider previous, DrawableHitCircle next)
            => previous.HeadCircle.Judged
               && previous.HitObject.StartTime < next.HitObject.StartTime
               && next.HitObject.StartTime < previous.HitObject.EndTime;

        internal bool ShouldYieldSliderHeadInput(DrawableSliderHead previous)
        {
            if (!previous.Judged || HitObjectContainer == null) return false;
            double time = previous.Clock.CurrentTime;
            foreach (var drawable in HitObjectContainer.AliveObjects)
            {
                DrawableHitCircle? next = drawable switch
                {
                    DrawableSlider slider => slider.HeadCircle,
                    DrawableHitCircle circle => circle,
                    _ => null,
                };
                if (next == null) continue;
                if (next.Judged || !next.IsPresent || !next.HitArea.IsHovered
                    || !IsOverlappingSuccessor(previous.DrawableSlider, next)) continue;
                HitResult result = next.HitObject.HitWindows.ResultFor(time - next.HitObject.StartTime);
                if (result != HitResult.None && CheckHittable(next, time, result) == ClickAction.Hit)
                    return true;
            }
            return false;
        }

        public void HandleHit(DrawableHitObject hitObject)
        {
        }

        public virtual ClickAction CheckHittable(DrawableHitObject hitObject, double time, HitResult result)
        {
            if (HitObjectContainer == null)
                throw new InvalidOperationException($"{nameof(HitObjectContainer)} should be set before {nameof(CheckHittable)} is called.");

            // Slider heads are nested drawables; the alive list contains their parent slider.
            // Resolve the parent so traversal stops at the target instead of checking later objects.
            DrawableHitObject target = hitObject is DrawableSliderHead head ? head.DrawableSlider : hitObject;
            var aliveObjects = HitObjectContainer.AliveObjects.ToList();
            int index = aliveObjects.IndexOf(target);

            if (index > 0)
            {
                var previousHitObject = (DrawableOsuHitObject)aliveObjects[index - 1];
                bool previousStillBlocks = !(previousHitObject is DrawableSlider previousSlider
                    && hitObject is DrawableHitCircle nextCircle && IsOverlappingSuccessor(previousSlider, nextCircle));
                if (previousHitObject.HitObject.StackHeight > 0 && !previousHitObject.AllJudged && previousStillBlocks)
                    return ClickAction.Ignore;
            }

            if (result == HitResult.None)
                return ClickAction.Shake;

            foreach (DrawableHitObject testObject in aliveObjects)
            {
                if (testObject.AllJudged)
                    continue;

                // if we found the object being checked, we can move on to the final timing test.
                if (testObject == target)
                    break;

                // for all other objects, we check for validity and block the hit if any are still valid.
                // 3ms of extra leniency to account for slightly unsnapped objects.
                if (testObject.HitObject.GetEndTime() + 3 < hitObject.HitObject.StartTime)
                    return ClickAction.Shake;
            }

            return Math.Abs(hitObject.HitObject.StartTime - time) < hittableRange ? ClickAction.Hit : ClickAction.Shake;
        }
    }
}
