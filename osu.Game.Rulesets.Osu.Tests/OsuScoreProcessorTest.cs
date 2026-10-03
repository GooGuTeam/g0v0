// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;

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
