// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class OsuScoreProcessorTest
    {
        private OsuScoreProcessor scoreProcessor = null!;
        private IBeatmap beatmap = null!;

        [SetUp]
        public void SetUp()
        {
            scoreProcessor = new OsuScoreProcessor();
            beatmap = new TestBeatmap(new OsuRuleset().RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new HitCircle(),
                    new HitCircle(),
                    new HitCircle(),
                    new HitCircle(),
                }
            };
        }

        [Test]
        public void TestScoreV1ActivationViaModScoreV1()
        {
            scoreProcessor.Mods.Value = new Mod[] { new OsuModScoreV1() };
            Assert.That(scoreProcessor.ScoreV1Active, Is.True);
            Assert.That(scoreProcessor.ScoreV2Active, Is.False);
        }

        [Test]
        public void TestClassicModDoesNotActivateScoreV1()
        {
            scoreProcessor.Mods.Value = new Mod[] { new OsuModClassic() };
            Assert.That(scoreProcessor.ScoreV1Active, Is.False);
            Assert.That(scoreProcessor.ScoreV2Active, Is.False);
        }

        [Test]
        public void TestScoreV1WithClassicMod()
        {
            scoreProcessor.Mods.Value = new Mod[] { new OsuModScoreV1(), new OsuModClassic() };
            Assert.That(scoreProcessor.ScoreV1Active, Is.True);
            Assert.That(scoreProcessor.ScoreV2Active, Is.False);
        }

        [Test]
        public void TestScoreV2PrecedenceOverScoreV1()
        {
            scoreProcessor.Mods.Value = new Mod[] { new OsuModScoreV2(), new OsuModScoreV1() };
            Assert.That(scoreProcessor.ScoreV2Active, Is.True);
            Assert.That(scoreProcessor.ScoreV1Active, Is.False);
        }

        [Test]
        public void TestScoreV1CalculationHitCircles()
        {
            scoreProcessor.Mods.Value = new Mod[] { new OsuModScoreV1() };
            scoreProcessor.ApplyBeatmap(beatmap);

            // Hit 1: combo 0 -> 1, base 300, comboScore = 0
            applyJudgement(beatmap.HitObjects[0], HitResult.Great);
            Assert.That(scoreProcessor.TotalScore.Value, Is.EqualTo(300));
            Assert.That(scoreProcessor.Combo.Value, Is.EqualTo(1));

            // Hit 2: combo 1 -> 2, base 300, comboScore = (1 - 1) * ... = 0
            applyJudgement(beatmap.HitObjects[1], HitResult.Great);
            Assert.That(scoreProcessor.TotalScore.Value, Is.EqualTo(600));
            Assert.That(scoreProcessor.Combo.Value, Is.EqualTo(2));

            // Hit 3: combo 2 -> 3, base 300, comboScore = (2 - 1) * (300 / 25 * diffMultiplier)
            // BeatmapPeppyStars for test beatmap (default difficulty with 4 objects):
            applyJudgement(beatmap.HitObjects[2], HitResult.Great);
            long hit3Score = scoreProcessor.TotalScore.Value;
            Assert.That(hit3Score, Is.GreaterThan(900));

            // Hit 4: combo 3 -> 4, base 300, comboScore = 2 * (12 * diffMultiplier)
            applyJudgement(beatmap.HitObjects[3], HitResult.Great);
            long hit4Score = scoreProcessor.TotalScore.Value;
            long hit4Delta = hit4Score - hit3Score;
            long hit3Delta = hit3Score - 600;
            // The combo portion of hit 4 is 2x of hit 3's combo portion:
            // hit3Delta = 300 + comboPart, hit4Delta = 300 + 2 * comboPart
            Assert.That(hit4Delta - 300, Is.EqualTo((hit3Delta - 300) * 2));
        }

        [Test]
        public void TestScoreV1PopulateScoreSetsLegacyTotalScore()
        {
            scoreProcessor.Mods.Value = new Mod[] { new OsuModScoreV1() };
            scoreProcessor.ApplyBeatmap(beatmap);

            applyJudgement(beatmap.HitObjects[0], HitResult.Great);

            var scoreInfo = new ScoreInfo();
            scoreProcessor.PopulateScore(scoreInfo);

            Assert.That(scoreInfo.TotalScore, Is.EqualTo(300));
            Assert.That(scoreInfo.LegacyTotalScore, Is.EqualTo(300));
            Assert.That(scoreProcessor.GetDisplayScore(ScoringMode.Standardised), Is.EqualTo(300));
            Assert.That(scoreProcessor.GetDisplayScore(ScoringMode.Classic), Is.EqualTo(300));
        }

        [Test]
        public void TestScoreV1SliderComponents()
        {
            var slider = new Slider { ClassicSliderBehaviour = true };
            var head = new SliderHeadCircle();
            var tick = new SliderTick();
            var tail = new SliderTailCircle(slider) { ClassicSliderBehaviour = true };

            var sliderBeatmap = new TestBeatmap(new OsuRuleset().RulesetInfo)
            {
                HitObjects = new List<HitObject> { slider }
            };

            scoreProcessor.Mods.Value = new Mod[] { new OsuModScoreV1() };
            scoreProcessor.ApplyBeatmap(sliderBeatmap);

            // Head gives 30 points
            applyJudgement(head, HitResult.LargeTickHit);
            Assert.That(scoreProcessor.TotalScore.Value, Is.EqualTo(30));

            // Tick gives 10 points
            applyJudgement(tick, HitResult.LargeTickHit);
            Assert.That(scoreProcessor.TotalScore.Value, Is.EqualTo(40));

            // Tail gives 30 points
            applyJudgement(tail, HitResult.SmallTickHit);
            Assert.That(scoreProcessor.TotalScore.Value, Is.EqualTo(70));

            // Slider itself gives 300 points + combo score
            applyJudgement(slider, HitResult.Great);
            Assert.That(scoreProcessor.TotalScore.Value,
                Is.EqualTo(370 + 2 * 12 * LegacyScoreUtils.CalculateDifficultyPeppyStars(sliderBeatmap)));
        }

        [Test]
        public void TestScoreV1SpinnerBonus()
        {
            var spinner = new Spinner();
            var tick = new SpinnerTick();
            var bonusTick = new SpinnerBonusTick();

            var spinnerBeatmap = new TestBeatmap(new OsuRuleset().RulesetInfo)
            {
                HitObjects = new List<HitObject> { spinner }
            };

            scoreProcessor.Mods.Value = new Mod[] { new OsuModScoreV1() };
            scoreProcessor.ApplyBeatmap(spinnerBeatmap);

            // Spinner tick gives 100 points
            applyJudgement(tick, HitResult.SmallBonus);
            Assert.That(scoreProcessor.TotalScore.Value, Is.EqualTo(100));

            // Spinner bonus tick gives 1100 points
            applyJudgement(bonusTick, HitResult.LargeBonus);
            Assert.That(scoreProcessor.TotalScore.Value, Is.EqualTo(1200));
        }

        [TestCase(false, false, 100, 500, 600)]
        [TestCase(false, true, 100, 500, 720)]
        [TestCase(true, false, 100, 1100, 1200)]
        public void TestLegacySpinnerBonusAndRewind(bool scoreV1, bool doubleTime, int normalAward, int bonusAward, long totalScore)
        {
            scoreProcessor.Mods.Value = doubleTime
                ? new Mod[] { new OsuModScoreV2(), new OsuModDoubleTime() }
                : new Mod[] { scoreV1 ? new OsuModScoreV1() : new OsuModScoreV2() };
            scoreProcessor.ApplyBeatmap(beatmap);

            var normal = new SpinnerTick();
            var bonus = new SpinnerBonusTick();
            var normalResult = new JudgementResult(normal, normal.CreateJudgement()) { Type = HitResult.SmallBonus };
            var bonusResult = new JudgementResult(bonus, bonus.CreateJudgement()) { Type = HitResult.LargeBonus };
            scoreProcessor.ApplyResult(normalResult);
            Assert.That(scoreProcessor.CurrentBonusPortion, Is.EqualTo(normalAward));
            scoreProcessor.ApplyResult(bonusResult);
            Assert.That(scoreProcessor.CurrentBonusPortion, Is.EqualTo(normalAward + bonusAward));
            Assert.That(scoreProcessor.TotalScore.Value, Is.EqualTo(totalScore));
            Assert.That(scoreProcessor.Combo.Value, Is.Zero);
            Assert.That(scoreProcessor.Accuracy.Value, Is.EqualTo(1));
            Assert.That(scoreProcessor.GetScoreProcessorStatistics().ComboPortion, Is.Zero);

            scoreProcessor.RevertResult(bonusResult);
            Assert.That(scoreProcessor.CurrentBonusPortion, Is.EqualTo(normalAward));
            scoreProcessor.RevertResult(normalResult);
            Assert.That(scoreProcessor.CurrentBonusPortion, Is.Zero);
            Assert.That(scoreProcessor.TotalScore.Value, Is.Zero);
            scoreProcessor.ApplyResult(normalResult);
            scoreProcessor.ApplyResult(bonusResult);
            Assert.That(scoreProcessor.TotalScore.Value, Is.EqualTo(totalScore));

            scoreProcessor.RevertResult(bonusResult);
            bonusResult.Type = HitResult.IgnoreMiss;
            scoreProcessor.ApplyResult(bonusResult);
            Assert.That(scoreProcessor.CurrentBonusPortion, Is.EqualTo(normalAward));
        }

        [Test]
        public void TestNonLegacySpinnerBonusUnchanged()
        {
            scoreProcessor.ApplyBeatmap(beatmap);
            applyJudgement(new SpinnerTick(), HitResult.SmallBonus);
            applyJudgement(new SpinnerBonusTick(), HitResult.LargeBonus);
            Assert.That(scoreProcessor.CurrentBonusPortion, Is.EqualTo(60));
        }

        [TestCase(1667, 6, 9, 14)]
        [TestCase(2000, 5, 10, 15)]
        [TestCase(100, 0, 0, 5)]
        public void TestScoreV2SpinnerThresholds(double duration, float overallDifficulty, int requiredHalfSpins, int firstBonusHalfSpin)
        {
            var spinner = new Spinner { StartTime = 1000, Duration = duration };
            var spinnerBeatmap = new TestBeatmap(new OsuRuleset().RulesetInfo)
            {
                HitObjects = new List<HitObject> { spinner },
                Difficulty = { OverallDifficulty = overallDifficulty }
            };
            spinner.ApplyDefaults(spinnerBeatmap.ControlPointInfo, spinnerBeatmap.Difficulty);
            Assert.That(spinner.LegacySpinnerScoring, Is.False);

            new OsuModScoreV2().ApplyToBeatmap(spinnerBeatmap);
            Assert.That(spinner.LegacyScoreV2, Is.True);
            Assert.That(spinner.LegacyScoreV1, Is.False);
            Assert.That(spinner.LegacyHalfSpinsRequired, Is.EqualTo(requiredHalfSpins));

            var ticks = spinner.NestedHitObjects.Cast<SpinnerTick>().ToArray();
            int maximumHalfSpins = (int)(duration * 0.05 / System.Math.PI);
            var expectedIndices = Enumerable.Range(1, maximumHalfSpins)
                                            .Where(i => i % 2 == 0 || (i >= firstBonusHalfSpin && (i - firstBonusHalfSpin) % 2 == 0));
            Assert.That(ticks.Select(t => t.LegacyHalfSpinIndex), Is.EqualTo(expectedIndices));
            Assert.That(ticks.Where(t => t is SpinnerBonusTick).All(t => t.LegacyHalfSpinIndex >= firstBonusHalfSpin && (t.LegacyHalfSpinIndex - firstBonusHalfSpin) % 2 == 0), Is.True);

            foreach (var tick in ticks)
                Assert.That(tick.StartTime, Is.EqualTo(spinner.StartTime + tick.LegacyHalfSpinIndex * System.Math.PI / 0.05));

            var originalTicks = ticks.Select(t => (t.GetType(), t.LegacyHalfSpinIndex, t.StartTime)).ToArray();
            new OsuModScoreV2().ApplyToBeatmap(spinnerBeatmap);
            Assert.That(spinner.NestedHitObjects.Cast<SpinnerTick>().Select(t => (t.GetType(), t.LegacyHalfSpinIndex, t.StartTime)), Is.EqualTo(originalTicks));
        }

        [TestCase(HitResult.Miss, HitResult.Meh, false, 25667, 25667)]
        [TestCase(HitResult.Miss, HitResult.Meh, true, 27207, 25667)]
        [TestCase(HitResult.Ok, HitResult.Great, false, 219338, 219338)]
        [TestCase(HitResult.Ok, HitResult.Great, true, 232499, 219338)]
        public void TestScoreV2RoundsCompleteFormula(HitResult thirdResult, HitResult fourthResult, bool hidden, long expectedScore, long expectedScoreWithoutMods)
        {
            scoreProcessor.Mods.Value = hidden
                ? new Mod[] { new OsuModScoreV2(), new OsuModHidden() }
                : new Mod[] { new OsuModScoreV2() };
            scoreProcessor.ApplyBeatmap(beatmap);

            Assert.That(scoreProcessor.MaximumComboPortion, Is.EqualTo(1500));
            applyJudgement(beatmap.HitObjects[0], HitResult.Miss);
            applyJudgement(beatmap.HitObjects[1], HitResult.Miss);
            applyJudgement(beatmap.HitObjects[2], thirdResult);

            long partialScore = scoreProcessor.TotalScore.Value;
            var finalResult = new JudgementResult(beatmap.HitObjects[3], beatmap.HitObjects[3].CreateJudgement()) { Type = fourthResult };
            scoreProcessor.ApplyResult(finalResult);

            // stable rounds the full modded formula once, without truncating the unmodded
            // score or rounding it before applying the Hidden multiplier.
            Assert.That(scoreProcessor.TotalScore.Value, Is.EqualTo(expectedScore));
            Assert.That(scoreProcessor.TotalScoreWithoutMods.Value, Is.EqualTo(expectedScoreWithoutMods));

            scoreProcessor.RevertResult(finalResult);
            Assert.That(scoreProcessor.TotalScore.Value, Is.EqualTo(partialScore));
            scoreProcessor.ApplyResult(finalResult);
            Assert.That(scoreProcessor.TotalScore.Value, Is.EqualTo(expectedScore));
        }

        [TestCase(HitResult.SmallTickHit, 2)]
        [TestCase(HitResult.SmallTickMiss, 1)]
        public void TestScoreV2TailComboAndRewind(HitResult tailResult, int expectedCombo)
        {
            var slider = new TestSlider();
            scoreProcessor.Mods.Value = new Mod[] { new OsuModScoreV2() };
            scoreProcessor.ApplyBeatmap(beatmap);
            applyJudgement(new HitCircle(), HitResult.Great);
            var tail = new JudgementResult(slider.TailCircle, slider.TailCircle.CreateJudgement()) { Type = tailResult };
            scoreProcessor.ApplyResult(tail);

            var parent = new JudgementResult(slider, slider.CreateJudgement()) { Type = HitResult.Ok };
            scoreProcessor.ApplyResult(parent);
            Assert.That(scoreProcessor.Combo.Value, Is.EqualTo(expectedCombo));
            Assert.That(scoreProcessor.HighestCombo.Value, Is.EqualTo(expectedCombo));
            Assert.That(parent.ComboAfterJudgement, Is.EqualTo(expectedCombo));
            double comboPortion = scoreProcessor.GetScoreProcessorStatistics().ComboPortion;

            scoreProcessor.RevertResult(parent);
            Assert.That(scoreProcessor.Combo.Value, Is.EqualTo(1));
            Assert.That(scoreProcessor.HighestCombo.Value, Is.EqualTo(1));
            scoreProcessor.ApplyResult(parent);
            Assert.That(scoreProcessor.Combo.Value, Is.EqualTo(expectedCombo));
            Assert.That(scoreProcessor.GetScoreProcessorStatistics().ComboPortion, Is.EqualTo(comboPortion));

            // Rewinding the tail must also remove its missed status, so a subsequently
            // successful tail restores both the parent combo and its combo-weighted score.
            scoreProcessor.RevertResult(parent);
            scoreProcessor.RevertResult(tail);
            tail.Type = HitResult.SmallTickHit;
            scoreProcessor.ApplyResult(tail);
            scoreProcessor.ApplyResult(parent);
            Assert.That(scoreProcessor.Combo.Value, Is.EqualTo(2));
            Assert.That(scoreProcessor.HighestCombo.Value, Is.EqualTo(2));
            Assert.That(parent.ComboAfterJudgement, Is.EqualTo(2));
            Assert.That(scoreProcessor.GetScoreProcessorStatistics().ComboPortion, Is.EqualTo(330 + 36 + 120));
        }

        [TestCase(0, 50)]
        [TestCase(0, 150)]
        [TestCase(0, 250)]
        [TestCase(0, 350)]
        [TestCase(1, 50)]
        [TestCase(1, 150)]
        [TestCase(1, 250)]
        [TestCase(1, 350)]
        [TestCase(2, 50)]
        [TestCase(2, 150)]
        [TestCase(2, 250)]
        [TestCase(2, 350)]
        public void TestScoreV2SliderSimulationOrdering(int repeatCount, double length)
        {
            var slider = new Slider
            {
                StartTime = 1000,
                ClassicSliderBehaviour = true,
                ClassicSliderJudgement = true,
                RepeatCount = repeatCount,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2((float)length, 0) }, length)
            };
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { SliderMultiplier = 1, SliderTickRate = 1 });
            var sliderBeatmap = new TestBeatmap(new OsuRuleset().RulesetInfo)
            {
                HitObjects = new List<HitObject> { slider }
            };

            scoreProcessor.Mods.Value = new Mod[] { new OsuModScoreV2() };
            scoreProcessor.ApplyBeatmap(sliderBeatmap);

            // stable simulates all 30-point checkpoints first, then all 10-point ticks,
            // then the parent at the final combo without incrementing it again.
            int checkpoints = repeatCount + 2;
            int ticks = slider.NestedHitObjects.OfType<SliderTick>().Count();
            int combo = 0;
            double expectedMaximum = 0;
            for (int i = 0; i < checkpoints; i++)
                expectedMaximum += 30 * (1 + ++combo / 10.0);

            for (int i = 0; i < ticks; i++)
                expectedMaximum += 10 * (1 + ++combo / 10.0);

            expectedMaximum += 300 * (1 + combo / 10.0);

            Assert.That(scoreProcessor.MaximumComboPortion, Is.EqualTo(expectedMaximum).Within(0.000001));
            Assert.That(scoreProcessor.MaximumCombo, Is.EqualTo(checkpoints + ticks));

            // Live scoring must still use chronological checkpoint ordering and must not
            // inherit the simulation-only tick combo - 2 adjustment.
            combo = 0;
            double expectedLive = 0;

            foreach (var nested in slider.NestedHitObjects)
            {
                double baseScore = nested is SliderTick ? 10 : 30;
                expectedLive += baseScore * (1 + ++combo / 10.0);
                applyJudgement(nested, nested.Judgement.MaxResult);
            }

            expectedLive += 300 * (1 + combo / 10.0);
            applyJudgement(slider, HitResult.Great);

            Assert.That(scoreProcessor.GetScoreProcessorStatistics().ComboPortion, Is.EqualTo(expectedLive).Within(0.000001));
            Assert.That(scoreProcessor.Combo.Value, Is.EqualTo(checkpoints + ticks));
        }

        private class TestSlider : Slider
        {
            public TestSlider()
            {
                ClassicSliderBehaviour = true;
                TailCircle = new SliderTailCircle(this) { ClassicSliderBehaviour = true };
            }
        }

        private void applyJudgement(HitObject hitObject, HitResult type)
        {
            var result = new JudgementResult(hitObject, hitObject.CreateJudgement())
            {
                Type = type
            };
            scoreProcessor.ApplyResult(result);
        }
    }
}
