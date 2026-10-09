// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Expanded.Accuracy;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class V2AccuracyScaleTest
    {
        [TestCase(0.7, 235, ScoreRank.C)]
        [TestCase(0.8, 289, ScoreRank.B)]
        [TestCase(0.9, 315, ScoreRank.A)]
        [TestCase(0.95, 335, ScoreRank.S)]
        [TestCase(0.99, 348, ScoreRank.X)]
        public void TestBadgeAndSegmentShareAngle(double accuracy, double angle, ScoreRank rank)
        {
            var scale = new V2AccuracyScale(0.7, 0.8, 0.9, 0.95, 1);
            Assert.That(scale.Map(accuracy), Is.EqualTo(angle).Within(0.0001));
            Assert.That(V2AccuracyScale.BadgeAngle(rank), Is.EqualTo(angle));
        }

        [Test]
        public void TestRulesetSpecificCutoffs()
        {
            var scale = new V2AccuracyScale(0.85, 0.9, 0.94, 0.98, 1);
            Assert.That(scale.Map(0.85), Is.EqualTo(235));
            Assert.That(scale.Map(0.98), Is.EqualTo(335));
        }

        [Test]
        public void TestProgressIsMonotonicAndClosesAtSS()
        {
            var scale = new V2AccuracyScale(0.7, 0.8, 0.9, 0.95, 1);
            double previous = 0;

            for (int i = 0; i <= 1000; i++)
            {
                double progress = scale.RingProgress(i / 1000.0);
                Assert.That(progress, Is.InRange(previous, 1));
                previous = progress;
            }

            Assert.That(previous, Is.EqualTo(1));
            Assert.That(scale.RingProgress(0.999) * 360 + V2AccuracyScale.RING_START,
                Is.EqualTo(V2AccuracyScale.RING_END).Within(0.0001));
        }

        [TestCase(1)]
        [TestCase(0.98)]
        public void TestSSClosesRingAtRulesetCutoff(double accuracyX)
        {
            var scale = new V2AccuracyScale(0.7, 0.8, 0.9, 0.95, accuracyX);
            Assert.That(scale.RingProgress(accuracyX), Is.EqualTo(1));
            Assert.That(scale.RingProgress(accuracyX - 0.001), Is.LessThan(1));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1)]
        [TestCase(2)]
        public void TestInvalidAccuracyIsBounded(double accuracy)
        {
            var scale = new V2AccuracyScale(0.7, 0.8, 0.9, 0.95, 1);
            Assert.That(scale.RingProgress(accuracy), Is.InRange(0, 1));
        }

        [Test]
        public void TestArcTerminalColoursAreDistinct()
        {
            var start = V2AccuracyRing.GetArcColour(0);
            var end = V2AccuracyRing.GetArcColour(1);
            Assert.That(start.B, Is.GreaterThan(end.B));
            Assert.That(start.R, Is.LessThan(end.R));
        }
    }
}
