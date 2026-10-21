// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Scoring
{
    public class OsuScoreMultiplierCalculatorV2 : ScoreMultiplierCalculator
    {
        public OsuScoreMultiplierCalculatorV2(ScoreMultiplierContext context)
            : base(context)
        {
            #region Difficulty Reduction

            Single<OsuModEasy>(hasMultiplier: easyMultiplier);
            Single<OsuModNoFail>(hasMultiplier: 0.5);
            Single<OsuModHalfTime>(hasMultiplier: halfTime => halfTimeMultiplier(halfTime.SpeedChange.Value));
            Single<OsuModDaycore>(hasMultiplier: daycore => halfTimeMultiplier(daycore.SpeedChange.Value));

            #endregion

            #region Difficulty Increase

            Single<OsuModHardRock>(hasMultiplier: 1.09);
            // Sudden Death (1.0x)
            // Perfect (1.0x)
            Single<OsuModDoubleTime>(hasMultiplier: doubleTime => doubleTimeMultiplier(doubleTime.SpeedChange.Value));
            Single<OsuModNightcore>(hasMultiplier: nightcore => doubleTimeMultiplier(nightcore.SpeedChange.Value));

            const double blinds_multiplier = 1.24;

            Combination<OsuModHidden, OsuModBlinds>(hasMultiplier: (_, _) => blinds_multiplier);

            Combination<OsuModHidden, OsuModWiggle>(hasMultiplier: (hidden, _) => hiddenMultiplier(hidden, otherModsProvideTimingInfo: true));
            Combination<OsuModHidden, OsuModGrow>(hasMultiplier: (hidden, _) => hiddenMultiplier(hidden, otherModsProvideTimingInfo: true));
            Combination<OsuModHidden, OsuModDeflate>(hasMultiplier: (hidden, deflate) => hiddenMultiplier(hidden, otherModsProvideTimingInfo: true) * deflateMultiplier(deflate));
            Combination<OsuModHidden, OsuModRepel>(hasMultiplier: (hidden, _) => hiddenMultiplier(hidden, otherModsProvideTimingInfo: true));
            Combination<OsuModHidden, OsuModDepth>(hasMultiplier: (hidden, _) => hiddenMultiplier(hidden, otherModsProvideTimingInfo: true));

            Single<OsuModHidden>(hasMultiplier: hidden => hiddenMultiplier(hidden, otherModsProvideTimingInfo: false));

            Combination<OsuModTraceable, OsuModBlinds>(hasMultiplier: (_, _) => blinds_multiplier);
            Single<OsuModTraceable>(hasMultiplier: 1.02);

            Combination<OsuModFlashlight, OsuModFreezeFrame>(hasMultiplier: (flashlight, _) => 1 + (flashlightMultiplier(flashlight) - 1) / 2);
            Single<OsuModFlashlight>(hasMultiplier: flashlightMultiplier);

            Single<OsuModBlinds>(hasMultiplier: blinds_multiplier);
            // Strict Tracking (1.0x)
            // Accuracy Challenge (1.0x)

            #endregion

            #region Conversion

            Single<OsuModTargetPractice>(hasMultiplier: 0.01);
            Single<OsuModDifficultyAdjust>(hasMultiplier: difficultyAdjust => difficultyAdjustMultiplier(difficultyAdjust, Context.BeatmapDifficultyWithoutMods));
            Single<OsuModClassic>(hasMultiplier: classic => classic.ClassicNoteLock.Value ? 0.985 : 0.96);
            Single<OsuModRandom>(hasMultiplier: 0.7);
            // Mirror (1.0x)
            // Alternate (1.0x)
            // Single Tap (1.0x)

            #endregion

            #region Automation

            // Autoplay (1.0x)
            // Cinema (1.0x)
            Single<OsuModRelax>(hasMultiplier: 0.1);
            Single<OsuModAutopilot>(hasMultiplier: 0.1);
            Single<OsuModSpunOut>(hasMultiplier: 0.95);

            #endregion

            #region Fun

            // Transform (1.0x)
            // Wiggle (1.0x)
            // Spin In (1.0x)
            // Grow (1.0x)
            Single<OsuModDeflate>(hasMultiplier: deflateMultiplier);
            Single<ModWindUp>(hasMultiplier: timeRampMultiplier);
            Single<ModWindDown>(hasMultiplier: timeRampMultiplier);
            // Barrel Roll (1.0x)
            Single<OsuModApproachDifferent>(hasMultiplier: 0.7);
            // Muted (1.0x)
            // No Scope (1.0x)
            Single<OsuModMagnetised>(hasMultiplier: magnetised => 0.7 - magnetised.AttractionStrength.Value * 0.6);
            // Repel (1.0x)
            Single<ModAdaptiveSpeed>(hasMultiplier: 0.1);
            // Freeze Frame (1.0x)
            // Bubbles (1.0x)
            Single<OsuModSynesthesia>(hasMultiplier: 0.99);
            // Depth (1.0x)
            // Bloom (1.0x)

            #endregion

            #region System

            // Touch Device (1.0x)
            // Score V2 (1.0x)

            #endregion
        }

        private static double easyMultiplier(OsuModEasy easy)
        {
            // 0.8x base multiplier
            // Reduce by 0.1x per extra life
            double value = 0.8 - Math.Max(0, 0.1 * (easy.Retries.Value - easy.Retries.Default));

            return Math.Max(0.4, value);
        }

        private static double halfTimeMultiplier(double speedChange)
        {
            // 0.2x at 0.5x speed, +0.07x per 0.05x speed increment.
            // Default HT (0.75x) = 0.55
            return (int)(speedChange * 20) / 20.0 * 1.4 - 0.5;
        }

        private static double doubleTimeMultiplier(double speedChange)
        {
            // Floor to the nearest multiple of 0.1.
            double value = (int)(speedChange * 10) / 10.0;

            // 0.01 penalty for non-default rates.
            double penalty = value != 1.5 && value != 1.0 ? 0.01 : 0.0;

            // Linear from 1.0 to 1.46, minus the penalty.
            // Default DT (1.5x) = 1.23
            return (value - 1) * 0.46 + 1 - penalty;
        }

        private static double hiddenMultiplier(OsuModHidden hidden, bool otherModsProvideTimingInfo)
        {
            double value = 1.04;

            if (hidden.OnlyFadeApproachCircles.Value)
                value -= 0.02;

            if (otherModsProvideTimingInfo)
                value -= 0.02;

            return value;
        }

        private static double flashlightMultiplier(OsuModFlashlight flashlight)
        {
            // Multiplier of 1.2x, reduced by 0.02 per 0.1 increase in flashlight size.
            double value = Math.Max(1.02, Math.Min(1.2, 1.2 - 0.2 * (flashlight.SizeMultiplier.Value - 1)));

            if (!flashlight.ComboBasedSize.Value)
                value = 1 + (value - 1) / 5;

            return value;
        }

        private static double difficultyAdjustMultiplier(OsuModDifficultyAdjust difficultyAdjust, IBeatmapDifficultyInfo beatmapDifficulty)
        {
            double selectedCircleSize = difficultyAdjust.CircleSize.Value ?? beatmapDifficulty.CircleSize;
            double selectedDrainRate = difficultyAdjust.DrainRate.Value ?? beatmapDifficulty.DrainRate;
            double selectedOverallDifficulty = difficultyAdjust.OverallDifficulty.Value ?? beatmapDifficulty.OverallDifficulty;
            double selectedApproachRate = difficultyAdjust.ApproachRate.Value ?? beatmapDifficulty.ApproachRate;

            double csDifference = Math.Abs(selectedCircleSize - beatmapDifficulty.CircleSize);
            double hpDifference = Math.Abs(selectedDrainRate - beatmapDifficulty.DrainRate);
            double odDifference = Math.Abs(selectedOverallDifficulty - beatmapDifficulty.OverallDifficulty);
            double arDifference = Math.Abs(selectedApproachRate - beatmapDifficulty.ApproachRate);

            // Per parameter, reduce multiplier by 0.05x per 0.1 change.
            double csMultiplier = Math.Max(0.1, 1.0 - csDifference * 0.5);
            double hpMultiplier = Math.Max(0.1, 1.0 - hpDifference * 0.5);
            double odMultiplier = Math.Max(0.1, 1.0 - odDifference * 0.5);
            double arMultiplier = Math.Max(0.1, 1.0 - arDifference * 0.5);

            return Math.Max(0.1, csMultiplier * hpMultiplier * odMultiplier * arMultiplier);
        }

        private static double timeRampMultiplier(ModTimeRamp timeRamp)
        {
            double minSpeed = Math.Min(timeRamp.InitialRate.Value, timeRamp.FinalRate.Value);
            double maxSpeed = Math.Max(timeRamp.InitialRate.Value, timeRamp.FinalRate.Value);

            double minMultiplier = minSpeed < 1 ? halfTimeMultiplier(minSpeed) : doubleTimeMultiplier(minSpeed);
            double maxMultiplier = maxSpeed < 1 ? halfTimeMultiplier(maxSpeed) : doubleTimeMultiplier(maxSpeed);

            return 0.8 * minMultiplier + 0.2 * maxMultiplier;
        }

        private static double deflateMultiplier(OsuModDeflate deflate)
            => 1.0 - Math.Max(0, 0.02 * (deflate.StartScale.Value - deflate.StartScale.Default));

        public override double CalculateFor(IEnumerable<Mod> mods)
        {
            var modList = mods as IReadOnlyList<Mod> ?? mods.ToList();

            if (modList.Any(m => m is ModScoreV2))
                return calculateForScoreV2(modList);

            if (modList.Any(m => m is ModScoreV1))
                return calculateForScoreV1(modList);

            return base.CalculateFor(modList);
        }

        private static double scoreV1DoubleTimeMultiplier(double speedChange)
        {
            if (speedChange < 1.0)
                return scoreV1HalfTimeMultiplier(speedChange);

            return Math.Round(1.0 + (speedChange - 1.0) * 0.24, 4);
        }

        private static double scoreV1HalfTimeMultiplier(double speedChange)
        {
            if (speedChange > 1.0)
                return scoreV1DoubleTimeMultiplier(speedChange);

            if (speedChange <= 0.75)
                return Math.Round(Math.Max(0.1, speedChange - 0.45), 4);

            return Math.Round(0.30 + (speedChange - 0.75) * 2.8, 4);
        }

        private static double scoreV1TimeRampMultiplier(ModTimeRamp timeRamp)
        {
            double minSpeed = Math.Min(timeRamp.InitialRate.Value, timeRamp.FinalRate.Value);
            double maxSpeed = Math.Max(timeRamp.InitialRate.Value, timeRamp.FinalRate.Value);

            double minMultiplier = minSpeed < 1 ? scoreV1HalfTimeMultiplier(minSpeed) : scoreV1DoubleTimeMultiplier(minSpeed);
            double maxMultiplier = maxSpeed < 1 ? scoreV1HalfTimeMultiplier(maxSpeed) : scoreV1DoubleTimeMultiplier(maxSpeed);

            return Math.Round(0.8 * minMultiplier + 0.2 * maxMultiplier, 4);
        }

        private double calculateForScoreV1(IReadOnlyList<Mod> mods)
        {
            double multiplier = 1.0;

            foreach (var mod in mods)
            {
                switch (mod)
                {
                    case ModScoreV1:
                    case OsuModClassic:
                        break;

                    case OsuModNoFail:
                        multiplier *= 0.5;
                        break;

                    case OsuModEasy:
                        multiplier *= 0.5;
                        break;

                    case OsuModHalfTime halfTime:
                        multiplier *= scoreV1HalfTimeMultiplier(halfTime.SpeedChange.Value);
                        break;

                    case OsuModDaycore daycore:
                        multiplier *= scoreV1HalfTimeMultiplier(daycore.SpeedChange.Value);
                        break;

                    case OsuModHidden:
                        multiplier *= 1.06;
                        break;

                    case OsuModHardRock:
                        multiplier *= 1.06;
                        break;

                    case OsuModDoubleTime doubleTime:
                        multiplier *= scoreV1DoubleTimeMultiplier(doubleTime.SpeedChange.Value);
                        break;

                    case OsuModNightcore nightcore:
                        multiplier *= scoreV1DoubleTimeMultiplier(nightcore.SpeedChange.Value);
                        break;

                    case ModTimeRamp timeRamp:
                        multiplier *= scoreV1TimeRampMultiplier(timeRamp);
                        break;

                    case OsuModFlashlight:
                        multiplier *= 1.12;
                        break;

                    case OsuModSpunOut:
                        multiplier *= 0.9;
                        break;

                    case OsuModRelax:
                    case OsuModAutopilot:
                        multiplier *= 0;
                        break;

                    default:
                        multiplier *= base.CalculateFor([mod]);
                        break;
                }
            }

            return Math.Round(multiplier, 4);
        }

        private static double scoreV2DoubleTimeMultiplier(double speedChange)
        {
            if (speedChange < 1.0)
                return scoreV2HalfTimeMultiplier(speedChange);

            return Math.Round(1.0 + (speedChange - 1.0) * 0.40, 4);
        }

        private static double scoreV2HalfTimeMultiplier(double speedChange)
        {
            if (speedChange > 1.0)
                return scoreV2DoubleTimeMultiplier(speedChange);

            if (speedChange <= 0.75)
                return Math.Round(Math.Max(0.1, speedChange - 0.25), 4);

            return Math.Round(0.50 + (speedChange - 0.75) * 2.0, 4);
        }

        private static double scoreV2TimeRampMultiplier(ModTimeRamp timeRamp)
        {
            double minSpeed = Math.Min(timeRamp.InitialRate.Value, timeRamp.FinalRate.Value);
            double maxSpeed = Math.Max(timeRamp.InitialRate.Value, timeRamp.FinalRate.Value);

            double minMultiplier = minSpeed < 1 ? scoreV2HalfTimeMultiplier(minSpeed) : scoreV2DoubleTimeMultiplier(minSpeed);
            double maxMultiplier = maxSpeed < 1 ? scoreV2HalfTimeMultiplier(maxSpeed) : scoreV2DoubleTimeMultiplier(maxSpeed);

            return Math.Round(0.8 * minMultiplier + 0.2 * maxMultiplier, 4);
        }

        private double calculateForScoreV2(IReadOnlyList<Mod> mods)
        {
            double multiplier = 1.0;

            foreach (var mod in mods)
            {
                switch (mod)
                {
                    case ModScoreV2:
                    case OsuModClassic:
                    case OsuModNoFail:
                        break;

                    case OsuModEasy:
                        multiplier *= 0.5;
                        break;

                    case OsuModHalfTime halfTime:
                        multiplier *= scoreV2HalfTimeMultiplier(halfTime.SpeedChange.Value);
                        break;

                    case OsuModDaycore daycore:
                        multiplier *= scoreV2HalfTimeMultiplier(daycore.SpeedChange.Value);
                        break;

                    case OsuModHidden:
                        multiplier *= 1.06;
                        break;

                    case OsuModHardRock:
                        multiplier *= 1.10;
                        break;

                    case OsuModDoubleTime doubleTime:
                        multiplier *= scoreV2DoubleTimeMultiplier(doubleTime.SpeedChange.Value);
                        break;

                    case OsuModNightcore nightcore:
                        multiplier *= scoreV2DoubleTimeMultiplier(nightcore.SpeedChange.Value);
                        break;

                    case ModTimeRamp timeRamp:
                        multiplier *= scoreV2TimeRampMultiplier(timeRamp);
                        break;

                    case OsuModFlashlight:
                        multiplier *= 1.12;
                        break;

                    case OsuModSpunOut:
                        multiplier *= 0.9;
                        break;

                    case OsuModRelax:
                    case OsuModAutopilot:
                        multiplier *= 0;
                        break;

                    default:
                        multiplier *= base.CalculateFor([mod]);
                        break;
                }
            }

            return Math.Round(multiplier, 4);
        }
    }
}
