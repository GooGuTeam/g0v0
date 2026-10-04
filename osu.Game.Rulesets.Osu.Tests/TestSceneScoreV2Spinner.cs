// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneScoreV2Spinner : OsuTestScene
    {
        private const double spinner_start = 1500;
        private const double spinner_duration = 1667;

        private readonly ManualClock source = new ManualClock { Rate = 1 };
        private FramedClock clock = null!;
        private DrawableSpinner drawableSpinner = null!;

        [TestCase(false, 0, HitResult.Miss)]
        [TestCase(false, 1, HitResult.Miss)]
        [TestCase(false, 2, HitResult.Meh)]
        [TestCase(false, 7, HitResult.Meh)]
        [TestCase(false, 8, HitResult.Ok)]
        [TestCase(false, 9, HitResult.Ok)]
        [TestCase(false, 10, HitResult.Great)]
        [TestCase(true, 0, HitResult.Miss)]
        [TestCase(true, 1, HitResult.Miss)]
        [TestCase(true, 2, HitResult.Meh)]
        [TestCase(true, 7, HitResult.Meh)]
        [TestCase(true, 8, HitResult.Ok)]
        [TestCase(true, 9, HitResult.Ok)]
        [TestCase(true, 10, HitResult.Great)]
        public void TestHalfTurnJudgement(bool scoreV1, int halfSpins, HitResult expected)
        {
            createSpinner(scoreV1);
            rotate(halfSpins * 180);
            AddAssert("progress uses nine required half turns", () => drawableSpinner.Progress, () => Is.EqualTo(Math.Min(1, halfSpins / 9f)));
            finishSpinner();
            AddAssert("stable judgement threshold", () => drawableSpinner.Result.Type, () => Is.EqualTo(expected));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestNoRequiredHalfTurns(bool scoreV1)
        {
            createSpinner(scoreV1, duration: 100, overallDifficulty: 0);
            AddAssert("short spinner implicitly complete", () => drawableSpinner.Progress, () => Is.EqualTo(1));
            finishSpinner();
            AddAssert("short spinner great", () => drawableSpinner.Result.Type, () => Is.EqualTo(HitResult.Great));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestEvenHalfTurnBonusThreshold(bool scoreV1)
        {
            createSpinner(scoreV1);
            rotate(359);
            assertHits(0, 0);
            rotate(1);
            assertHits(1, 0);
            rotate(11 * 180);
            assertHits(6, 0);
            AddAssert("no displayed bonus before fourteenth half turn", () => drawableSpinner.CurrentBonusScore, () => Is.Zero);
            rotate(180);
            assertHits(6, 1);
            AddAssert("stable displayed bonus", () => drawableSpinner.CurrentBonusScore, () => Is.EqualTo(1000));
            rotate(360);
            assertHits(6, 2);
            AddAssert("displayed bonus increments by one thousand", () => drawableSpinner.CurrentBonusScore, () => Is.EqualTo(2000));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestOddHalfTurnBonusRefreshesCounter(bool scoreV1)
        {
            // OD 5 over two seconds requires ten half turns, so the first bonus is at fifteen.
            createSpinner(scoreV1, duration: 2000, overallDifficulty: 5);
            int counterChanges = 0;
            AddStep("observe counter", () => drawableSpinner.CompletedFullSpins.BindValueChanged(_ => counterChanges++));
            rotate(14 * 180);
            assertHits(7, 0);
            AddAssert("seven full turns", () => drawableSpinner.CompletedFullSpins.Value, () => Is.EqualTo(7));
            AddStep("reset change count", () => counterChanges = 0);
            rotate(180);
            assertHits(7, 1);
            AddAssert("still seven full turns", () => drawableSpinner.CompletedFullSpins.Value, () => Is.EqualTo(7));
            AddAssert("odd half turn refreshed counter", () => counterChanges, () => Is.EqualTo(1));
            AddAssert("bonus visible immediately", () => drawableSpinner.CurrentBonusScore, () => Is.EqualTo(1000));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestBonusCounterRewindAndReapply(bool scoreV1)
        {
            createSpinner(scoreV1, duration: 2000, overallDifficulty: 5);
            rotate(14 * 180);
            AddStep("advance for bonus half turn", () =>
            {
                source.CurrentTime = spinner_start + 11;
                clock.ProcessFrame();
            });
            rotate(180);
            assertHits(7, 1);
            AddAssert("bonus displayed", () => drawableSpinner.CurrentBonusScore, () => Is.EqualTo(1000));

            AddStep("rewind the bonus half turn", () =>
            {
                source.CurrentTime = spinner_start + 1;
                clock.ProcessFrame();
                drawableSpinner.RotationTracker.AddRotation(-180);
            });
            assertHits(7, 0);
            AddAssert("bonus counter rewound", () => drawableSpinner.CurrentBonusScore, () => Is.Zero);
            AddAssert("full turns unchanged by half turn rewind", () => drawableSpinner.CompletedFullSpins.Value, () => Is.EqualTo(7));

            AddStep("resume bonus half turn", () =>
            {
                source.CurrentTime = spinner_start + 11;
                clock.ProcessFrame();
            });
            rotate(180);
            assertHits(7, 1);
            AddAssert("bonus counter restored", () => drawableSpinner.CurrentBonusScore, () => Is.EqualTo(1000));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestBonusCounterResetOnNewSpinner(bool scoreV1)
        {
            createSpinner(scoreV1);
            rotate(14 * 180);
            AddAssert("initial bonus displayed", () => drawableSpinner.CurrentBonusScore, () => Is.EqualTo(1000));
            AddStep("apply new spinner", () => drawableSpinner.Apply(prepareSpinner(scoreV1, spinner_duration, 6)));
            AddAssert("new spinner rotation reset", () => drawableSpinner.Result.TotalRotation, () => Is.Zero);
            AddAssert("new spinner bonus reset", () => drawableSpinner.CurrentBonusScore, () => Is.Zero);
            AddAssert("new spinner full turns reset", () => drawableSpinner.CompletedFullSpins.Value, () => Is.Zero);
        }

        private void createSpinner(bool scoreV1, double duration = spinner_duration, float overallDifficulty = 6)
        {
            AddStep($"create ScoreV{(scoreV1 ? 1 : 2)} spinner with manual clock", () =>
            {
                source.CurrentTime = spinner_start + 1;
                clock = new FramedClock(source);
                clock.ProcessFrame();
                var playfield = new TestPlayfield
                {
                    Clock = clock,
                    ProcessCustomClock = false
                };
                playfield.Add(drawableSpinner = new DrawableSpinner(prepareSpinner(scoreV1, duration, overallDifficulty)));
                Child = playfield;
            });
            AddAssert("starts without displayed bonus", () => drawableSpinner.CurrentBonusScore, () => Is.Zero);
            AddAssert("maximum displayed bonus from generated awards", () => drawableSpinner.MaximumBonusScore,
                () => Is.EqualTo(1000 * drawableSpinner.HitObject.NestedHitObjects.OfType<SpinnerBonusTick>().Count()));
        }

        private static Spinner prepareSpinner(bool scoreV1, double duration, float overallDifficulty)
        {
            var spinner = new Spinner
            {
                StartTime = spinner_start,
                Duration = duration,
                Position = new Vector2(256, 192)
            };
            var controlPoints = new ControlPointInfo();
            var difficulty = new BeatmapDifficulty { OverallDifficulty = overallDifficulty };

            if (scoreV1)
                spinner.ApplyLegacyScoreV1(controlPoints, difficulty);
            else
                spinner.ApplyLegacyScoreV2(controlPoints, difficulty);

            return spinner;
        }

        private void rotate(float degrees)
        {
            AddStep($"rotate {degrees} degrees", () =>
            {
                float remaining = degrees;

                while (remaining > 0)
                {
                    float delta = Math.Min(180, remaining);
                    drawableSpinner.RotationTracker.AddRotation(delta);
                    remaining -= delta;
                }
            });
        }

        private void assertHits(int normal, int bonus)
        {
            AddAssert($"{normal} normal awards", () => drawableSpinner.NestedHitObjects.Count(t => t.HitObject is SpinnerTick and not SpinnerBonusTick && t.IsHit), () => Is.EqualTo(normal));
            AddAssert($"{bonus} bonus awards", () => drawableSpinner.NestedHitObjects.Count(t => t.HitObject is SpinnerBonusTick && t.IsHit), () => Is.EqualTo(bonus));
        }

        private partial class TestPlayfield : Playfield
        {
        }

        private void finishSpinner() => AddStep("advance past spinner end", () =>
        {
            source.CurrentTime = drawableSpinner.HitObject.EndTime + 1;
            clock.ProcessFrame();
        });
    }
}
