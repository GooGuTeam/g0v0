// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning;
using osu.Game.Tests.Resources;
using osuTK;

namespace osu.Game.Tests.Visual.Ranking
{
    public partial class TestSceneLegacyRankingPanel : OsuTestScene
    {
        private ScoreInfo createTestScore()
        {
            var score = TestResources.CreateTestScoreInfo(new OsuRuleset().RulesetInfo);
            score.TotalScore = 12345678;
            score.Accuracy = 0.9876;
            score.MaxCombo = 500;
            score.Rank = ScoreRank.S;
            score.Statistics[HitResult.Great] = 400;
            score.Statistics[HitResult.Ok] = 20;
            score.Statistics[HitResult.Meh] = 5;
            score.Statistics[HitResult.Miss] = 1;
            return score;
        }

        [Test]
        public void TestStaticDisplay()
        {
            LegacyRankingPanel panel = null!;

            AddStep("load panel without animation", () =>
            {
                Child = new DrawSizePreservingFillContainer
                {
                    TargetDrawSize = new Vector2(1366, 768),
                    Strategy = DrawSizePreservationStrategy.Minimum,
                    Child = panel = new LegacyRankingPanel(createTestScore(), animate: false)
                };
            });

            AddAssert("not animating", () => !panel.IsAnimating);
            AddAssert("score text contains 12345678", () =>
            {
                var text = panel.ChildrenOfType<LegacySpriteText>().FirstOrDefault(t => t.Text.ToString().Contains("12345678"));
                return text != null;
            });
        }

        [Test]
        public void TestEntranceAnimationAndSkip()
        {
            LegacyRankingPanel panel = null!;

            AddStep("load panel with animation", () =>
            {
                Child = new DrawSizePreservingFillContainer
                {
                    TargetDrawSize = new Vector2(1366, 768),
                    Strategy = DrawSizePreservationStrategy.Minimum,
                    Child = panel = new LegacyRankingPanel(createTestScore(), animate: true)
                };
            });

            AddAssert("animating", () => panel.IsAnimating);

            AddStep("finish animation", () => panel.FinishAnimation());

            AddAssert("no longer animating", () => !panel.IsAnimating);
            AddAssert("score text contains 12345678", () =>
            {
                var text = panel.ChildrenOfType<LegacySpriteText>().FirstOrDefault(t => t.Text.ToString().Contains("12345678"));
                return text != null;
            });
        }

        [Test]
        public void TestClickSkipsAnimation()
        {
            LegacyRankingPanel panel = null!;

            AddStep("load panel with animation", () =>
            {
                Child = new DrawSizePreservingFillContainer
                {
                    TargetDrawSize = new Vector2(1366, 768),
                    Strategy = DrawSizePreservationStrategy.Minimum,
                    Child = panel = new LegacyRankingPanel(createTestScore(), animate: true)
                };
            });

            AddAssert("animating", () => panel.IsAnimating);

            AddStep("click panel", () => panel.TriggerClick());

            AddAssert("no longer animating after click", () => !panel.IsAnimating);
            AddAssert("score text contains 12345678", () =>
            {
                var text = panel.ChildrenOfType<LegacySpriteText>().FirstOrDefault(t => t.Text.ToString().Contains("12345678"));
                return text != null;
            });
        }
    }
}
