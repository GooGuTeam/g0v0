// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class TestSceneOsuLegacyHealthProcessor
    {
        [Test]
        public void TestNoBreak()
        {
            OsuLegacyHealthProcessor hp = new OsuLegacyHealthProcessor(-1000);
            hp.ApplyBeatmap(new Beatmap<OsuHitObject>
            {
                HitObjects =
                {
                    new HitCircle { StartTime = 0 },
                    new HitCircle { StartTime = 2000 }
                }
            });

            Assert.That(hp.DrainRate, Is.EqualTo(1.4E-5).Within(0.1E-5));
        }

        [Test]
        public void TestSingleBreak()
        {
            OsuLegacyHealthProcessor hp = new OsuLegacyHealthProcessor(-1000);
            hp.ApplyBeatmap(new Beatmap<OsuHitObject>
            {
                HitObjects =
                {
                    new HitCircle { StartTime = 0 },
                    new HitCircle { StartTime = 2000 }
                },
                Breaks =
                {
                    new BreakPeriod(500, 1500)
                }
            });

            Assert.That(hp.DrainRate, Is.EqualTo(4.3E-5).Within(0.1E-5));
        }

        [Test]
        public void TestOverlappingBreak()
        {
            OsuLegacyHealthProcessor hp = new OsuLegacyHealthProcessor(-1000);
            hp.ApplyBeatmap(new Beatmap<OsuHitObject>
            {
                HitObjects =
                {
                    new HitCircle { StartTime = 0 },
                    new HitCircle { StartTime = 2000 }
                },
                Breaks =
                {
                    new BreakPeriod(500, 1400),
                    new BreakPeriod(750, 1500),
                }
            });

            Assert.That(hp.DrainRate, Is.EqualTo(4.3E-5).Within(0.1E-5));
        }

        [Test]
        public void TestSequentialBreak()
        {
            OsuLegacyHealthProcessor hp = new OsuLegacyHealthProcessor(-1000);
            hp.ApplyBeatmap(new Beatmap<OsuHitObject>
            {
                HitObjects =
                {
                    new HitCircle { StartTime = 0 },
                    new HitCircle { StartTime = 2000 }
                },
                Breaks =
                {
                    new BreakPeriod(500, 1000),
                    new BreakPeriod(1000, 1500),
                }
            });

            Assert.That(hp.DrainRate, Is.EqualTo(4.3E-5).Within(0.1E-5));
        }

        [TestCase(0f, HitResult.Meh, 3.2 / 200.0)]
        [TestCase(2.5f, HitResult.Meh, 1.8 / 200.0)]
        [TestCase(5f, HitResult.Meh, 0.4 / 200.0)]
        [TestCase(8f, HitResult.Meh, 0.4 / 200.0)]
        [TestCase(0f, HitResult.Ok, 17.6 / 200.0)]
        [TestCase(2.5f, HitResult.Ok, 9.9 / 200.0)]
        [TestCase(5f, HitResult.Ok, 2.2 / 200.0)]
        [TestCase(8f, HitResult.Ok, 2.2 / 200.0)]
        public void TestLowDrainRateHit50AndHit100RecoverySlope(float drainRate, HitResult hitResult, double expectedIncrease)
        {
            OsuLegacyHealthProcessor hp = new OsuLegacyHealthProcessor(0);
            hp.ApplyBeatmap(new Beatmap<OsuHitObject>
            {
                Difficulty = new BeatmapDifficulty { DrainRate = drainRate }
            });

            hp.Health.Value = 0.2;

            var circle = new HitCircle();
            hp.ApplyResult(new OsuJudgementResult(circle, circle.CreateJudgement()) { Type = hitResult });

            Assert.That(hp.Health.Value - 0.2, Is.EqualTo(expectedIncrease).Within(1e-12));
        }
    }
}
