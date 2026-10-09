// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Expanded.Statistics;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.Ranking
{
    public partial class TestSceneV2PerformanceStatistic : OsuTestScene
    {
        [Cached(typeof(BeatmapDifficultyCache))]
        private readonly TestDifficultyCache difficultyCache = new TestDifficultyCache();

        [TestCase(false)]
        [TestCase(true)]
        public void TestFullComboEstimate(bool perfect)
        {
            PerformanceStatistic statistic = null!;
            ScoreInfo score = null!;
            double expected = 0;
            AddStep("load score and local difficulty", () =>
            {
                score = TestResources.CreateTestScoreInfo();
                score.Mods = Array.Empty<Mod>();
                score.Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Great] = perfect ? 100 : 85,
                    [HitResult.Ok] = perfect ? 0 : 10,
                    [HitResult.Miss] = perfect ? 0 : 5,
                };
                score.MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Great] = 100 };
                score.Accuracy = perfect ? 1 : 26500.0 / 30000;
                score.MaxCombo = perfect ? 100 : 30;
                score.PP = 12;
                score.Rank = perfect ? ScoreRank.X : ScoreRank.B;
                var attributes = new OsuDifficultyAttributes { MaxCombo = 100, HitCircleCount = 100 };
                var calculator = new OsuRuleset().CreatePerformanceCalculator();
                expected = calculator.Calculate(FullComboScoreInfo.Create(score)!, attributes).Total;
                difficultyCache.Difficulty = new StarDifficulty(attributes, new PerformanceAttributes { Total = 123 });
                Child = new Container
                {
                    Width = 165,
                    Y = 50,
                    AutoSizeAxes = Axes.Y,
                    Child = statistic = new PerformanceStatistic(score) { UseV2Style = true },
                };
            });
            AddUntilStep("if FC calculated", () => statistic.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == $"if FC {Math.Round(expected, MidpointRounding.AwayFromZero)}pp"));
            AddStep("show values", () => statistic.Appear());
            AddUntilStep("server PP preserved", () => statistic.ChildrenOfType<StatisticCounter>().Single().DisplayedCount, () => Is.EqualTo(12));
            AddAssert("maximum PP retained", () => statistic.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == "/123"));
            AddAssert("FC sits above PP", () => statistic.ChildrenOfType<SpriteText>().Single(t => t.Name == "If FC performance").ScreenSpaceDrawQuad.AABBFloat.Bottom
                                    <= statistic.ScreenSpaceDrawQuad.AABBFloat.Top);
            AddAssert("original miss count unchanged", () => score.Statistics[HitResult.Miss], () => Is.EqualTo(perfect ? 0 : 5));
        }

        [TestCase(112.34, "+12.34pp")]
        [TestCase(87.66, "-12.34pp")]
        [TestCase(100, "+0.00pp")]
        public void TestPersonalBestPP(double pp, string expected)
        {
            PerformanceStatistic statistic = null!;
            AddStep("load score and previous best", () =>
            {
                difficultyCache.Difficulty = null;
                var score = TestResources.CreateTestScoreInfo();
                score.PP = pp;
                var previous = score.DeepClone();
                previous.PP = 100;
                Child = statistic = new PerformanceStatistic(score)
                {
                    UseV2Style = true,
                    ComparisonScore = { Value = previous },
                };
            });
            AddUntilStep("loaded", () => statistic.IsLoaded);
            AddStep("show values", () => statistic.Appear());
            AddUntilStep("PP delta is relative to beatmap PB", () => statistic.ChildrenOfType<osu.Game.Screens.Ranking.V2StatisticChange>().Single()
                                                                  .ChildrenOfType<SpriteText>().Single().Text.ToString(), () => Is.EqualTo(expected));
        }

        [Test]
        public void TestUnavailableDifficulty()
        {
            PerformanceStatistic statistic = null!;
            AddStep("load without local attributes", () =>
            {
                difficultyCache.Difficulty = null;
                var score = TestResources.CreateTestScoreInfo();
                score.PP = 42;
                Child = statistic = new PerformanceStatistic(score) { UseV2Style = true };
            });
            AddUntilStep("loaded", () => statistic.IsLoaded);
            AddStep("show values", () => statistic.Appear());
            AddUntilStep("server PP displayed", () => statistic.ChildrenOfType<StatisticCounter>().Single().DisplayedCount, () => Is.EqualTo(42));
            AddAssert("no fabricated FC estimate", () => statistic.ChildrenOfType<SpriteText>().Single(t => t.Name == "If FC performance").Text.ToString(), () => Is.Empty);
        }

        private partial class TestDifficultyCache : BeatmapDifficultyCache
        {
            public StarDifficulty? Difficulty { get; set; }

            public override Task<StarDifficulty?> GetDifficultyAsync(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo = null, IEnumerable<Mod>? mods = null,
                                                                     CancellationToken cancellationToken = default, int computationDelay = 0)
                => Task.FromResult(Difficulty);
        }
    }
}
