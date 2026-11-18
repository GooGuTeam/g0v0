// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Objects
{
    public class Spinner : OsuHitObject, IHasDuration
    {
        /// <summary>
        /// The RPM required to clear the spinner at ODs [ 0, 5, 10 ].
        /// </summary>
        public static readonly DifficultyRange CLEAR_RPM_RANGE = new DifficultyRange(90, 150, 225);

        /// <summary>
        /// The RPM required to complete the spinner and receive full score at ODs [ 0, 5, 10 ].
        /// </summary>
        public static readonly DifficultyRange COMPLETE_RPM_RANGE = new DifficultyRange(250, 380, 430);

        public double EndTime
        {
            get => StartTime + Duration;
            set => Duration = value - StartTime;
        }

        public double Duration { get; set; }

        /// <summary>
        /// Number of spins required to finish the spinner without miss.
        /// </summary>
        public int SpinsRequired { get; protected set; } = 1;

        /// <summary>
        /// The number of spins required to start receiving bonus score. The first bonus is awarded on this spin count.
        /// </summary>
        public int SpinsRequiredForBonus => SpinsRequired + bonus_spins_gap;

        /// <summary>
        /// The gap between spinner completion and the first bonus-awarding spin.
        /// </summary>
        private const int bonus_spins_gap = 2;

        private const int max_spin_count = int.MaxValue / 4;
        private const int max_half_spins = 10_000;

        /// <summary>
        /// Number of spins available to give bonus, beyond <see cref="SpinsRequired"/>.
        /// </summary>
        public int MaximumBonusSpins { get; protected set; } = 1;

        /// <summary>
        /// Whether spinner judgements and ticks use stable's modern ScoreV1 thresholds.
        /// </summary>
        public bool LegacyScoreV1 { get; private set; }

        /// <summary>
        /// Whether spinner judgements and ticks use stable's modern ScoreV2 thresholds.
        /// This does not change cursor rotation tracking or replay playback.
        /// </summary>
        public bool LegacyScoreV2 { get; private set; }

        public bool LegacySpinnerScoring => LegacyScoreV1 || LegacyScoreV2;

        /// <summary>
        /// Only CL+SV1/CL+SV2 restrict overlapping spinner input to the first active spinner.
        /// </summary>
        public bool ExclusiveLegacyInput { get; internal set; }

        /// <summary>
        /// Whether Classic simulates stable's spinner rotation (<see cref="Drawables.LegacySpinnerRotation"/>) and final judgement.
        /// Scoring ticks are unchanged and still follow the selected score version.
        /// </summary>
        public bool ClassicSpinnerBehaviour { get; internal set; }

        public int LegacyHalfSpinsRequired { get; private set; }

        public void ApplyLegacyScoreV1(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            LegacyScoreV1 = true;
            ApplyDefaults(controlPointInfo, difficulty);
        }

        public void ApplyLegacyScoreV2(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            LegacyScoreV2 = true;
            ApplyDefaults(controlPointInfo, difficulty);
        }

        public override Vector2 StackOffset => Vector2.Zero;

        protected override void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            base.ApplyDefaultsToSelf(controlPointInfo, difficulty);

            // The average RPS required over the length of the spinner to clear the spinner.
            double minRps = IBeatmapDifficultyInfo.DifficultyRange(difficulty.OverallDifficulty, CLEAR_RPM_RANGE) / 60;

            // The RPS required over the length of the spinner to receive full score (all normal + bonus ticks).
            double maxRps = IBeatmapDifficultyInfo.DifficultyRange(difficulty.OverallDifficulty, COMPLETE_RPM_RANGE) / 60;

            double secondsDuration = Duration / 1000;

            // Allow a 0.1ms floating point precision error in the calculation of the duration.
            const double duration_error = 0.0001;

            LegacyHalfSpinsRequired = clampSpinCount(minRps * secondsDuration * 2);
            SpinsRequired = clampSpinCount(minRps * secondsDuration + duration_error);
            int maxTotalSpins = clampSpinCount(maxRps * secondsDuration + duration_error);
            MaximumBonusSpins = Math.Max(0, maxTotalSpins - SpinsRequired - bonus_spins_gap);

            static int clampSpinCount(double value) => double.IsNaN(value) ? 0 : (int)Math.Clamp(value, 0, max_spin_count);
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            base.CreateNestedHitObjects(cancellationToken);

            if (LegacySpinnerScoring)
            {
                // Both stable scoring versions award normal spin score on whole turns,
                // and bonus score on alternating half turns after a 1.5-turn gap.
                long bonusThreshold = (long)LegacyHalfSpinsRequired + 3;
                double rawHalfSpins = Duration * 0.05 / Math.PI;
                int halfSpins = double.IsNaN(rawHalfSpins) ? 0 : (int)Math.Clamp(rawHalfSpins, 0, max_half_spins);

                for (int i = 1; i <= halfSpins; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool bonus = i > bonusThreshold && (i - bonusThreshold) % 2 == 0;
                    if (!bonus && i % 2 != 0)
                        continue;

                    SpinnerTick tick = bonus
                        ? new SpinnerBonusTick { Samples = new[] { CreateHitSampleInfo("spinnerbonus") } }
                        : new SpinnerTick();
                    tick.StartTime = StartTime + i * Math.PI / 0.05;
                    tick.SpinnerDuration = Duration;
                    tick.LegacyHalfSpinIndex = i;
                    AddNested(tick);
                }

                return;
            }

            const int max_total_spins = max_half_spins / 2;
            int totalSpins = (int)Math.Clamp((long)MaximumBonusSpins + SpinsRequired + bonus_spins_gap, 0, max_total_spins);

            for (int i = 0; i < totalSpins; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double startTime = StartTime + (float)(i + 1) / totalSpins * Duration;

                AddNested(i < SpinsRequiredForBonus
                    ? new SpinnerTick { StartTime = startTime, SpinnerDuration = Duration }
                    : new SpinnerBonusTick { StartTime = startTime, SpinnerDuration = Duration, Samples = new[] { CreateHitSampleInfo("spinnerbonus") } });
            }
        }

        public override Judgement CreateJudgement() => new OsuJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;

        public override IList<HitSampleInfo> AuxiliarySamples => CreateSpinningSamples();

        public HitSampleInfo[] CreateSpinningSamples()
        {
            var referenceSample = Samples.FirstOrDefault();

            if (referenceSample == null)
                return Array.Empty<HitSampleInfo>();

            return new[]
            {
                referenceSample.With("spinnerspin")
            };
        }
    }
}
