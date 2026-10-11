// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Scoring;

namespace osu.Game.Screens.Ranking.Expanded.Accuracy
{
    /// <summary>
    /// Shared angular scale for the modern gauge, grade segments and badges.
    /// Angles are clockwise from twelve o'clock; the lower grades occupy the left semicircle.
    /// Actual ruleset cutoffs are mapped onto these display positions rather than used as angles directly.
    /// </summary>
    internal sealed class V2AccuracyScale
    {
        public const double RING_START = 3;
        public const double RING_END = 350;
        public const float GRADE_RING_SIZE = 1.055f;
        public const float BADGE_PADDING = 33;

        private readonly double[] cutoffs;
        private static readonly double[] angles = { 180, 235, 289, 315, 335, 348, 360 };

        public V2AccuracyScale(double accuracyC, double accuracyB, double accuracyA, double accuracyS, double accuracyX)
        {
            cutoffs = new[] { 0, accuracyC, accuracyB, accuracyA, accuracyS, accuracyX - AccuracyCircle.VIRTUAL_SS_PERCENTAGE, accuracyX };
        }

        public double Map(double accuracy)
        {
            accuracy = double.IsFinite(accuracy) ? Math.Clamp(accuracy, 0, cutoffs[^1]) : 0;

            // Keep the first half of the gauge for low accuracy, then expand the higher grade intervals.
            double midpoint = cutoffs[1] * 0.5;
            if (accuracy <= midpoint)
                return 180 * accuracy / Math.Max(double.Epsilon, midpoint);

            for (int i = 1; i < cutoffs.Length; i++)
            {
                double start = i == 1 ? midpoint : cutoffs[i - 1];
                if (accuracy <= cutoffs[i])
                {
                    double t = (accuracy - start) / Math.Max(double.Epsilon, cutoffs[i] - start);
                    return angles[i - 1] + t * (angles[i] - angles[i - 1]);
                }
            }

            return 360;
        }

        public double RingProgress(double accuracy)
        {
            // SS must close the ring; lower ranks retain the cap gap.
            if (double.IsFinite(accuracy) && accuracy >= cutoffs[^1])
                return 1;

            return Math.Clamp((Map(accuracy) - RING_START) / 360, 0, (RING_END - RING_START) / 360);
        }

        public static double BadgeAngle(ScoreRank rank) => rank switch
        {
            ScoreRank.X or ScoreRank.XH => angles[5],
            ScoreRank.S or ScoreRank.SH => angles[4],
            ScoreRank.A => angles[3],
            ScoreRank.B => angles[2],
            ScoreRank.C => angles[1],
            _ => angles[0],
        };
    }
}
