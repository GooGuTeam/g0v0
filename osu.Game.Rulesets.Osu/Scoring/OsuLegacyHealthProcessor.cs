// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Scoring
{
    public partial class OsuLegacyHealthProcessor : LegacyDrainingHealthProcessor
    {
        private readonly List<Period> spinnerPeriods = new List<Period>();
        private PeriodTracker? noDrainPeriodTracker;
        private double gameplayEndTime;

        public OsuLegacyHealthProcessor(double drainStartTime)
            : base(drainStartTime)
        {
        }

        public override void ApplyBeatmap(IBeatmap beatmap)
        {
            gameplayEndTime = beatmap.HitObjects.Count > 0 ? beatmap.GetLastObjectTime() : 0;
            noDrainPeriodTracker = new PeriodTracker(
                beatmap.Breaks.Select(breakPeriod =>
                    new Period(
                        beatmap.HitObjects
                               .Select(hitObject => hitObject.GetEndTime())
                               .Where(endTime => endTime <= breakPeriod.StartTime)
                               .DefaultIfEmpty(double.MinValue)
                               .Last(),
                        beatmap.HitObjects
                               .Select(hitObject => hitObject.StartTime)
                               .Where(startTime => startTime >= breakPeriod.EndTime)
                               .DefaultIfEmpty(double.MaxValue)
                               .First()
                    )));

            spinnerPeriods.Clear();

            foreach (var spinner in beatmap.HitObjects.OfType<Spinner>().Where(s => s.EndTime > s.StartTime).OrderBy(s => s.StartTime))
            {
                if (spinnerPeriods.Count > 0 && spinner.StartTime <= spinnerPeriods[^1].End)
                    spinnerPeriods[^1] = new Period(spinnerPeriods[^1].Start, Math.Max(spinnerPeriods[^1].End, spinner.EndTime));
                else
                    spinnerPeriods.Add(new Period(spinner.StartTime, spinner.EndTime));
            }

            base.ApplyBeatmap(beatmap);
        }

        protected override void Update()
        {
            double healthBefore = Health.Value;

            base.Update();

            if (spinnerPeriods.Count == 0 || DrainLenience >= 1 || noDrainPeriodTracker?.IsInAny(Time.Current) == true)
                return;

            double lastGameplayTime = Math.Clamp(Time.Current - Time.Elapsed, DrainStartTime, gameplayEndTime);
            double currentGameplayTime = Math.Clamp(Time.Current, DrainStartTime, gameplayEndTime);

            double delta = currentGameplayTime - lastGameplayTime;
            if (delta == 0)
                return;

            double spinningDuration = getSpinningDuration(Math.Min(lastGameplayTime, currentGameplayTime), Math.Max(lastGameplayTime, currentGameplayTime));
            if (spinningDuration == 0)
                return;

            // RulesetOsu.UpdateHp reduces passive HP drain to 0.25x while a spinner is active (without changing CalculateHpDropRate).
            double effectiveDelta = delta - 0.75 * Math.Sign(delta) * spinningDuration;
            Health.Value = healthBefore - DrainRate * effectiveDelta;
        }

        private double getSpinningDuration(double from, double to)
        {
            double total = 0;

            foreach (var period in spinnerPeriods)
            {
                if (period.End <= from)
                    continue;

                if (period.Start >= to)
                    break;

                total += Math.Min(to, period.End) - Math.Max(from, period.Start);
            }

            return total;
        }

        protected override IEnumerable<HitObject> EnumerateTopLevelHitObjects() => Beatmap.HitObjects;

        protected override IEnumerable<HitObject> EnumerateNestedHitObjects(HitObject hitObject)
        {
            switch (hitObject)
            {
                case Slider slider:
                    foreach (var nested in slider.NestedHitObjects)
                        yield return nested;

                    break;

                case Spinner spinner:
                    foreach (var nested in spinner.NestedHitObjects.Where(t => t is not SpinnerBonusTick))
                        yield return nested;

                    break;
            }
        }

        protected override double GetHealthIncreaseFor(HitObject hitObject, HitResult result)
        {
            double increase = 0;

            switch (result)
            {
                case HitResult.SmallTickMiss:
                    return IBeatmapDifficultyInfo.DifficultyRange(Beatmap.Difficulty.DrainRate, -0.02, -0.075, -0.14);

                case HitResult.LargeTickMiss:
                    return IBeatmapDifficultyInfo.DifficultyRange(Beatmap.Difficulty.DrainRate, -0.02, -0.075, -0.14);

                case HitResult.Miss:
                    return IBeatmapDifficultyInfo.DifficultyRange(Beatmap.Difficulty.DrainRate, -0.03, -0.125, -0.2);

                case HitResult.SmallTickHit:
                    // This result always comes from the slider tail, which is judged the same as a repeat.
                    increase = 0.02;
                    break;

                case HitResult.SliderTailHit:
                case HitResult.LargeTickHit:
                    // This result comes from either a slider tick or repeat.
                    increase = hitObject is SliderTick ? 0.015 : 0.02;
                    break;

                case HitResult.Meh:
                    increase = IBeatmapDifficultyInfo.DifficultyRange(Beatmap.Difficulty.DrainRate, 3.2, 0.4, 0.4) / 200.0;
                    break;

                case HitResult.Ok:
                    increase = IBeatmapDifficultyInfo.DifficultyRange(Beatmap.Difficulty.DrainRate, 17.6, 2.2, 2.2) / 200.0;
                    break;

                case HitResult.Great:
                    increase = 0.03;
                    break;

                case HitResult.SmallBonus:
                    increase = 0.0085;
                    break;

                case HitResult.LargeBonus:
                    increase = 0.01;
                    break;
            }

            return HpMultiplierNormal * increase;
        }
    }
}
