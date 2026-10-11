// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class FullComboScoreInfoTest
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TestRestoresComboWithoutTurningLowerJudgementsIntoPerfects(int scoringVersion)
        {
            var score = createScore();
            score.Mods = scoringVersion switch
            {
                1 => new Mod[] { new OsuModScoreV1() },
                2 => new Mod[] { new OsuModScoreV2() },
                _ => Array.Empty<Mod>(),
            };
            score.LegacyTotalScore = 123456;
            double maximum = scoringVersion == 0 ? 32100 : 30000;
            double gained = scoringVersion == 0 ? 720 : 600;

            var fc = FullComboScoreInfo.Create(score)!;
            Assert.That(fc, Is.Not.Null);
            Assert.That(fc.MaxCombo, Is.EqualTo(130));
            Assert.That(fc.Statistics[HitResult.Miss], Is.Zero);
            Assert.That(fc.Statistics[HitResult.LargeTickMiss], Is.Zero);
            Assert.That(fc.Statistics[HitResult.Great], Is.EqualTo(90));
            Assert.That(fc.Statistics[HitResult.Ok], Is.EqualTo(8));
            Assert.That(fc.Statistics[HitResult.Meh], Is.EqualTo(2));
            Assert.That(fc.Statistics[HitResult.LargeTickHit], Is.EqualTo(20));
            Assert.That(fc.Statistics[HitResult.SliderTailHit], Is.EqualTo(10));
            Assert.That(fc.Statistics[HitResult.LargeBonus], Is.EqualTo(7));
            Assert.That(fc.Accuracy, Is.EqualTo(0.91 + gained / maximum).Within(1e-10));
            Assert.That(fc.Accuracy, Is.LessThan(1));
            Assert.That(fc.LegacyTotalScore, Is.Null);
            Assert.That(fc.PP, Is.Null);
            Assert.That(score.MaxCombo, Is.EqualTo(70));
            Assert.That(score.Statistics[HitResult.Miss], Is.EqualTo(2));
            Assert.That(score.Statistics[HitResult.Great], Is.EqualTo(88));
            Assert.That(score.LegacyTotalScore, Is.EqualTo(123456));
            Assert.That(score.Accuracy, Is.EqualTo(0.91));
        }

        [Test]
        public void TestFullComboKeepsAccuracy()
        {
            var score = createScore();
            score.Statistics[HitResult.Great] += score.Statistics[HitResult.Miss];
            score.Statistics[HitResult.Miss] = 0;
            score.Statistics[HitResult.LargeTickMiss] = 0;
            score.Statistics[HitResult.LargeTickHit] = 20;
            score.MaxCombo = score.GetMaximumAchievableCombo();
            Assert.That(FullComboScoreInfo.Create(score)!.Accuracy, Is.EqualTo(score.Accuracy));
        }

        [Test]
        public void TestUnavailableAndIncompleteScores()
        {
            var score = createScore();
            score.Passed = false;
            Assert.That(FullComboScoreInfo.Create(score), Is.Null);
            score.Passed = true;
            score.Rank = ScoreRank.F;
            Assert.That(FullComboScoreInfo.Create(score), Is.Null);
            score.Rank = ScoreRank.A;
            score.Statistics[HitResult.Great]--;
            Assert.That(FullComboScoreInfo.Create(score), Is.Null);
            score.MaximumStatistics.Clear();
            Assert.That(FullComboScoreInfo.Create(score), Is.Null);
        }

        private static ScoreInfo createScore() => new ScoreInfo
        {
            Ruleset = new OsuRuleset().RulesetInfo,
            Passed = true,
            Rank = ScoreRank.A,
            Accuracy = 0.91,
            MaxCombo = 70,
            PP = 40,
            Statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 88,
                [HitResult.Ok] = 8,
                [HitResult.Meh] = 2,
                [HitResult.Miss] = 2,
                [HitResult.LargeTickHit] = 16,
                [HitResult.LargeTickMiss] = 4,
                [HitResult.SliderTailHit] = 10,
                [HitResult.LargeBonus] = 7,
            },
            MaximumStatistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 100,
                [HitResult.LargeTickHit] = 20,
                [HitResult.SliderTailHit] = 10,
                [HitResult.LargeBonus] = 50,
            },
        };
    }
}
