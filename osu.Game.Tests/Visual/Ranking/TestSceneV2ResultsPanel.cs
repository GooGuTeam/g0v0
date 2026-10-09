// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Overlays;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Ranking.Expanded;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Screens.Ranking.Expanded.Statistics;
using osu.Game.Skinning;
using osu.Game.Tests.Resources;
using osu.Game.Users;
using osuTK;

namespace osu.Game.Tests.Visual.Ranking
{
    public partial class TestSceneV2ResultsPanel : OsuTestScene
    {
        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [Test]
        public void TestDetailsNavigation()
        {
            V2ResultsPanel panel = null!;
            AddStep("load panel", () => Child = panel = new V2ResultsPanel(TestResources.CreateTestScoreInfo()));
            AddUntilStep("panel loaded", () => panel.IsLoaded);
            AddAssert("details initially closed", () => !panel.CloseDetails());
            AddStep("show details", () => panel.ShowDetails());
            AddStep("show details again", () => panel.ShowDetails());
            AddAssert("back closes details", () => panel.CloseDetails());
            AddAssert("second back is not consumed", () => !panel.CloseDetails());
            AddAssert("detail controls have generous hit areas", () => panel.ChildrenOfType<V2ResultsButton>()
                .Where(b => b.Name is "Show score details" or "Close score details")
                .All(b => b.Width >= 48 && b.Height >= 48));
            AddAssert("no debug button", () => panel.ChildrenOfType<V2ResultsButton>().All(b => b.Text.ToString() != "Debug Δ"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestRetryActionVisibility(bool available)
        {
            V2ResultsFooter footer = null!;
            AddStep("load footer", () => Child = footer = new V2ResultsFooter
            {
                RetryAction = available ? () => { } : null,
            });
            AddUntilStep("footer loaded", () => footer.IsLoaded);
            AddAssert("retry only attached when available", () => footer.RetryButton.Parent != null, () => Is.EqualTo(available));
            AddAssert("retry enabled only when available", () => footer.RetryButton.Enabled.Value, () => Is.EqualTo(available));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestScoreCounterInitialisation(bool withFlair)
        {
            V2ResultsPanel panel = null!;
            TotalScoreCounter counter = null!;

            AddStep("load score", () =>
            {
                var score = TestResources.CreateTestScoreInfo(rulesets.GetRuleset(0)!);
                score.TotalScore = 987654;
                Child = panel = new V2ResultsPanel(score, withFlair);
            });
            AddUntilStep("counter initialised", () =>
            {
                counter = panel.ChildrenOfType<TotalScoreCounter>().Single();
                return counter.IsLoaded && counter.Current.Value > 0;
            });

            if (!withFlair)
                AddAssert("existing score does not roll on entry", () => counter.DisplayedCount, () => Is.EqualTo(counter.Current.Value));

            AddUntilStep("counter reaches final score", () => counter.DisplayedCount == counter.Current.Value);
            AddAssert("accuracy sounds only loaded for a new play", () =>
                panel.ChildrenOfType<AccuracyCircle>().Single().ChildrenOfType<PoolableSkinnableSample>().Any(), () => Is.EqualTo(withFlair));

            if (withFlair)
                AddUntilStep("accuracy tick and rank impact played", () =>
                    panel.ChildrenOfType<AccuracyCircle>().Single().ChildrenOfType<PoolableSkinnableSample>().Count(s => s.Played) >= 3);
        }

        [TestCase(0, 960, 540)]
        [TestCase(0, 1280, 720)]
        [TestCase(0, 1920, 1080)]
        [TestCase(1, 1024, 768)]
        [TestCase(2, 1280, 720)]
        [TestCase(3, 1920, 1080)]
        [TestCase(0, 2560, 1080)]
        public void TestLayout(int rulesetId, int width, int height)
        {
            Container viewport = null!;
            V2ResultsPanel panel = null!;
            V2ResultsFooter footer = null!;

            AddStep("load long metadata and large score", () =>
            {
                RulesetInfo ruleset = rulesets.GetRuleset(rulesetId)!;
                var score = TestResources.CreateTestScoreInfo(ruleset);
                score.BeatmapInfo!.Metadata.Title = new string('W', 160);
                score.BeatmapInfo.DifficultyName = new string('W', 160);
                score.Statistics[HitResult.SliderTailHit] = 999;
                score.MaximumStatistics[HitResult.SliderTailHit] = 999;
                score.Statistics[HitResult.LargeBonus] = 999;
                score.MaximumStatistics[HitResult.LargeBonus] = 999;
                score.TotalScore = long.MaxValue;
                score.Position = int.MaxValue;
                score.Rank = ScoreRank.X;
                score.Accuracy = 1;
                score.PP = 12345;
                score.MaxCombo = 123456;

                Child = viewport = new Container
                {
                    Size = new Vector2(width, height),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Bottom = width * V2ResultsFooter.DESIGN_HEIGHT / 1280 },
                            Child = panel = new V2ResultsPanel(score),
                        },
                        footer = new V2ResultsFooter
                        {
                            Score = score,
                            AllowWatchingReplay = false,
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.X,
                            Height = width * V2ResultsFooter.DESIGN_HEIGHT / 1280,
                        },
                    },
                };
            });
            AddUntilStep("panel loaded", () => panel.IsLoaded && footer.IsLoaded);
            AddWaitStep("finish counters", 20);
            AddAssert("finite panel geometry", () => float.IsFinite(panel.DrawWidth) && float.IsFinite(panel.DrawHeight));
            AddAssert("actions stay in viewport", () => footer.ChildrenOfType<V2ResultsButton>().All(b =>
                b.ScreenSpaceDrawQuad.AABBFloat.Left >= viewport.ScreenSpaceDrawQuad.AABBFloat.Left - 1
                && b.ScreenSpaceDrawQuad.AABBFloat.Right <= viewport.ScreenSpaceDrawQuad.AABBFloat.Right + 1));
            AddAssert("buttons leave vertical breathing room", () => footer.ChildrenOfType<V2ResultsButton>().All(b =>
                b.ScreenSpaceDrawQuad.AABBFloat.Top >= footer.ScreenSpaceDrawQuad.AABBFloat.Top + 2
                && b.ScreenSpaceDrawQuad.AABBFloat.Bottom <= footer.ScreenSpaceDrawQuad.AABBFloat.Bottom - 2));
            AddAssert("header sits flush with top of panel", () =>
                Math.Abs(panel.ChildrenOfType<Box>().Single(c => c.Name == "Beatmap header background").ScreenSpaceDrawQuad.AABBFloat.Top
                         - panel.ScreenSpaceDrawQuad.AABBFloat.Top) < 0.5f);
            AddAssert("difficulty capsule aligned with ruleset icon", () =>
            {
                var indicators = panel.ChildrenOfType<Container>().Single(c => c.Name == "Difficulty indicators");
                float[] centres = indicators.Children.Select(c => c.ScreenSpaceDrawQuad.AABBFloat.Centre.Y).ToArray();
                return Math.Abs(centres[0] - centres[1]) < 0.5f;
            });
            AddAssert("score fits its column", () =>
            {
                var counter = panel.ChildrenOfType<TotalScoreCounter>().Single();
                return counter.DrawableCount.DrawWidth * counter.DrawableCount.Scale.X <= counter.Parent!.DrawWidth + 1;
            });
            AddAssert("statistics fit their cells", () => panel.ChildrenOfType<StatisticDisplay>().All(s =>
                s.ChildrenOfType<StatisticCounter>().All(c => c.ScreenSpaceDrawQuad.AABBFloat.Right <= s.ScreenSpaceDrawQuad.AABBFloat.Right + 1)));
            AddAssert("text uses the bundled font", () => panel.ChildrenOfType<SpriteText>().All(t => t.Font.Family == OsuFont.Default.Family));
            AddAssert("statistics all have labels", () => panel.ChildrenOfType<StatisticDisplay>().All(s => !string.IsNullOrEmpty(s.DisplayedHeader.ToString())));
            AddAssert("rank circle stays in viewport", () =>
            {
                var circle = panel.ChildrenOfType<AccuracyCircle>().Single().ScreenSpaceDrawQuad.AABBFloat;
                var bounds = viewport.ScreenSpaceDrawQuad.AABBFloat;
                return circle.Right <= bounds.Right && circle.Bottom <= bounds.Bottom;
            });
            AddAssert("unavailable replay disabled", () => !footer.ReplayButton.Enabled.Value && !footer.ExportButton.Enabled.Value);
            AddAssert("retry without player disabled", () => !footer.RetryButton.Enabled.Value);
            AddAssert("buttons have labels", () => footer.ChildrenOfType<V2ResultsButton>().All(b => !string.IsNullOrEmpty(b.Text.ToString())));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestModsPresentation(bool hasMods)
        {
            V2ResultsPanel panel = null!;

            AddStep("load score", () =>
            {
                var score = TestResources.CreateTestScoreInfo();
                score.Mods = hasMods ? new Mod[] { new OsuModHardRock(), new OsuModDoubleTime() } : Array.Empty<Mod>();
                Child = panel = new V2ResultsPanel(score);
            });
            AddUntilStep("panel loaded", () => panel.IsLoaded);
            AddAssert("mods header only shown with mods", () => panel.ChildrenOfType<SpriteText>().Single(t => t.Name == "Mods header").IsPresent == hasMods);
            AddAssert("mods area contains no extra text", () =>
            {
                var area = panel.ChildrenOfType<Container>().Single(c => c.Name == "Mods area");
                return !area.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().Contains('×'))
                       && (hasMods || !area.ChildrenOfType<SpriteText>().Any());
            });
        }

        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public void TestFeaturedArtistMetadata(bool featured, bool requestFails)
        {
            V2ResultsPanel panel = null!;

            AddStep("handle metadata lookup", () => ((DummyAPIAccess)API).HandleRequest = request =>
            {
                if (request is not GetBeatmapSetRequest beatmapSetRequest || requestFails)
                    return false;

                beatmapSetRequest.TriggerSuccess(new APIBeatmapSet
                {
                    OnlineID = beatmapSetRequest.ID,
                    TrackId = featured ? 123 : null,
                });
                return true;
            });
            AddStep("load overview", () =>
            {
                var score = TestResources.CreateTestScoreInfo();
                score.BeatmapInfo!.BeatmapSet!.OnlineID = 123;
                Child = panel = new V2ResultsPanel(score);
            });
            AddUntilStep("panel loaded", () => panel.IsLoaded);
            AddUntilStep("badge reflects confirmed metadata", () =>
                panel.ChildrenOfType<Container>().Single(c => c.Name == "Featured artist").Alpha == (featured && !requestFails ? 1 : 0));
            AddStep("clear metadata handler", () => ((DummyAPIAccess)API).HandleRequest = null);
        }

        [TestCase(0.0, 0, false, ScoreRank.D, "0.00%")]
        [TestCase(0.6999, 0, false, ScoreRank.D, "69.99%")]
        [TestCase(0.7, 0, false, ScoreRank.C, "70.00%")]
        [TestCase(0.7999, 0, false, ScoreRank.C, "79.99%")]
        [TestCase(0.8, 0, false, ScoreRank.B, "80.00%")]
        [TestCase(0.8999, 0, false, ScoreRank.B, "89.99%")]
        [TestCase(0.9, 0, false, ScoreRank.A, "90.00%")]
        [TestCase(0.9499, 0, false, ScoreRank.A, "94.99%")]
        [TestCase(0.95, 0, false, ScoreRank.S, "95.00%")]
        [TestCase(0.99, 1, false, ScoreRank.A, "99.00%")]
        [TestCase(0.999999, 0, false, ScoreRank.S, "99.99%")]
        [TestCase(1.0, 0, false, ScoreRank.X, "100.00%")]
        [TestCase(0.98, 0, true, ScoreRank.SH, "98.00%")]
        [TestCase(1.0, 0, true, ScoreRank.XH, "100.00%")]
        public void TestAccuracyAndGrade(double accuracy, int misses, bool hidden, ScoreRank expectedRank, string expectedAccuracy)
        {
            V2ResultsPanel panel = null!;
            ScoreInfo score = null!;
            AddStep("load accuracy and calculated grade", () =>
            {
                score = TestResources.CreateTestScoreInfo();
                score.Accuracy = accuracy;
                score.Mods = hidden ? new Mod[] { new OsuModHidden() } : Array.Empty<Mod>();
                score.Statistics[HitResult.Miss] = misses;
                using var processor = score.Ruleset.CreateInstance().CreateScoreProcessor();
                score.Rank = processor.RankFromScore(accuracy, score.Statistics);
                foreach (var mod in score.Mods.OfType<IApplicableToScoreProcessor>())
                    score.Rank = mod.AdjustRank(score.Rank, accuracy);
                Child = panel = new V2ResultsPanel(score);
            });
            AddUntilStep("accuracy counter settled", () => panel.ChildrenOfType<AccuracyStatistic>().Single()
                                                    .ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == expectedAccuracy));
            AddAssert("correct grade", () => score.Rank, () => Is.EqualTo(expectedRank));
            AddUntilStep("grade visible", () => panel.ChildrenOfType<RankText>().First().Alpha == 1);
            AddAssert("displayed grade matches score", () => panel.ChildrenOfType<V2RankEmblem>().First().ChildrenOfType<SpriteText>().Single().Text.ToString(),
                () => Is.EqualTo(osu.Game.Online.Leaderboards.DrawableRank.GetRankLetter(expectedRank)));
            AddAssert("only earned grade badges visible", () => panel.ChildrenOfType<RankBadge>().All(b => !b.IsPresent || b.Rank <= expectedRank));
        }

        [TestCase(1.0, 100, ScoreRank.X, true)]
        [TestCase(1.0, 99, ScoreRank.X, false)]
        [TestCase(0.9999, 100, ScoreRank.X, false)]
        [TestCase(0.98, 100, ScoreRank.S, false)]
        [TestCase(1.0, 100, ScoreRank.F, false)]
        public void TestPerfectComboFlair(double accuracy, int combo, ScoreRank rank, bool visible)
        {
            V2ResultsPanel panel = null!;
            AddStep("load score", () =>
            {
                var score = TestResources.CreateTestScoreInfo();
                score.Accuracy = accuracy;
                score.Rank = rank;
                score.MaxCombo = combo;
                score.Statistics.Clear();
                score.Statistics[HitResult.Great] = 100;
                score.MaximumStatistics.Clear();
                score.MaximumStatistics[HitResult.Great] = 100;
                Child = panel = new V2ResultsPanel(score);
            });
            AddUntilStep("loaded", () => panel.IsLoaded);
            AddAssert("star only shown for PFC", () => panel.ChildrenOfType<V2ScoreFlair>().Single().IsPresent, () => Is.EqualTo(visible));
        }

        [TestCase(1.0, 100, "+1.00%", "+10×")]
        [TestCase(0.98, 80, "-1.00%", "-10×")]
        public void TestPersonalBestComparisons(double accuracy, int combo, string accuracyText, string comboText)
        {
            V2ResultsPanel panel = null!;
            AddStep("load PB comparisons", () =>
            {
                var score = TestResources.CreateTestScoreInfo();
                score.Accuracy = accuracy;
                score.MaxCombo = combo;
                var previous = score.DeepClone();
                previous.Accuracy = 0.99;
                previous.MaxCombo = 90;
                previous.PP = 100;
                Child = panel = new V2ResultsPanel(score) { ComparisonScore = { Value = previous } };
            });
            AddUntilStep("loaded", () => panel.IsLoaded);
            AddAssert("accuracy delta", () => panel.ChildrenOfType<V2StatisticChange>().Single(d => d.Name == "Personal best accuracy change")
                                       .ChildrenOfType<SpriteText>().Single().Text.ToString(), () => Is.EqualTo(accuracyText));
            AddAssert("combo delta", () => panel.ChildrenOfType<V2StatisticChange>().Single(d => d.Name == "Personal best combo change")
                                    .ChildrenOfType<SpriteText>().Single().Text.ToString(), () => Is.EqualTo(comboText));
            AddAssert("positive green or negative red", () => panel.ChildrenOfType<V2StatisticChange>().Single(d => d.Name == "Personal best accuracy change")
                                                       .ChildrenOfType<SpriteText>().Single().Colour.TopLeft.SRGB,
                () => Is.EqualTo((osuTK.Graphics.Color4)(accuracy > 0.99 ? Colour4.FromHex("#B6FF86") : Colour4.FromHex("#ED1B53"))));
        }

        [TestCase(0, 0, false)]
        [TestCase(7, 0, true)]
        [TestCase(0, 20, true)]
        [TestCase(7, 20, true)]
        [TestCase(999, 999, true)]
        public void TestSpinnerBonus(int count, int maximum, bool visible)
        {
            V2ResultsPanel panel = null!;
            ScoreInfo score = null!;
            AddStep("load spinner bonus", () =>
            {
                score = TestResources.CreateTestScoreInfo();
                score.Statistics[HitResult.LargeBonus] = count;
                score.MaximumStatistics[HitResult.LargeBonus] = maximum;
                Child = panel = new V2ResultsPanel(score);
            });
            AddUntilStep("panel loaded", () => panel.IsLoaded);
            AddAssert("bonus visibility", () => panel.ChildrenOfType<HitResultStatistic>().Any(s => s.Result == HitResult.LargeBonus), () => Is.EqualTo(visible));
            if (!visible)
                return;

            AddAssert("bonus count and denominator", () =>
            {
                var bonus = score.GetStatisticsForDisplay().Single(s => s.Result == HitResult.LargeBonus);
                return bonus.Count == count && bonus.MaxCount == (maximum > 0 ? maximum : null);
            });
            AddAssert("spinner label", () => panel.ChildrenOfType<HitResultStatistic>().Single(s => s.Result == HitResult.LargeBonus).DisplayedHeader.ToString(),
                () => Is.EqualTo("SPINNER BONUS"));
            AddUntilStep("bonus counter settled", () => panel.ChildrenOfType<HitResultStatistic>().Single(s => s.Result == HitResult.LargeBonus)
                                                 .ChildrenOfType<StatisticCounter>().Single().DisplayedCount, () => Is.EqualTo(count));
        }

        [TestCase(12.34, "+12.34pp")]
        [TestCase(0, "+0.00pp")]
        [TestCase(-1.25, "-1.25pp")]
        public void TestProfilePerformanceChange(double delta, string expected)
        {
            V2PerformanceChange change = null!;
            ScoreInfo score = null!;
            AddStep("wait for server statistics", () =>
            {
                score = TestResources.CreateTestScoreInfo();
                score.OnlineID = 1234;
                Child = change = new V2PerformanceChange(score) { WaitForUpdate = true, Scale = new Vector2(3) };
            });
            AddUntilStep("loading indicator visible", () => change.ChildrenOfType<LoadingSpinner>().Single().State.Value == Visibility.Visible);
            AddAssert("no fabricated delta", () => change.ChildrenOfType<SpriteText>().Single(t => t.Name == "Profile PP change").Text.ToString(), () => Is.Empty);
            AddStep("ignore another score's update", () => change.LatestUpdate.Value = new ScoreBasedUserStatisticsUpdate(
                TestResources.CreateTestScoreInfo(), new UserStatistics { PP = 100 }, new UserStatistics { PP = 200 }));
            AddAssert("still waiting", () => change.ChildrenOfType<LoadingSpinner>().Single().State.Value == Visibility.Visible);
            AddStep("receive this score's update", () => change.LatestUpdate.Value = new ScoreBasedUserStatisticsUpdate(
                score.DeepClone(), new UserStatistics { PP = 1000, GlobalRank = 1000 }, new UserStatistics { PP = 1000 + (decimal)delta, GlobalRank = delta < 0 ? 1050 : 950 }));
            AddUntilStep("delta displayed with two decimals", () => change.ChildrenOfType<SpriteText>().Single(t => t.Name == "Profile PP change").Text.ToString(), () => Is.EqualTo(expected));
            AddAssert("loading indicator stopped", () => change.ChildrenOfType<LoadingSpinner>().Single().State.Value == Visibility.Hidden);
            AddAssert("rank change uses lower-is-better direction", () => change.ChildrenOfType<SpriteText>().Single(t => t.Name == "Profile rank change").Text.ToString(),
                () => Is.EqualTo(delta < 0 ? $"#{1050:N0} (-50)" : $"#{950:N0} (+50)"));
            AddAssert("rank change colour", () => change.ChildrenOfType<SpriteText>().Single(t => t.Name == "Profile rank change").Colour.TopLeft.SRGB,
                () => Is.EqualTo((osuTK.Graphics.Color4)(delta < 0 ? Colour4.FromHex("#ED1B53") : Colour4.FromHex("#B6FF86"))));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestHistoricalPerformanceChange(bool updateAlreadyAvailable)
        {
            V2PerformanceChange change = null!;
            AddStep("load history", () =>
            {
                var score = TestResources.CreateTestScoreInfo();
                Child = change = new V2PerformanceChange(score);
                if (updateAlreadyAvailable)
                    change.LatestUpdate.Value = new ScoreBasedUserStatisticsUpdate(score, new UserStatistics { PP = 100 }, new UserStatistics { PP = 101 });
            });
            AddUntilStep("loaded", () => change.IsLoaded);
            AddAssert("history never starts spinner", () => change.ChildrenOfType<LoadingSpinner>().Single().State.Value == Visibility.Hidden);
            AddAssert("only a known delta is shown", () => change.IsPresent == updateAlreadyAvailable);
        }

        [TestCase(ScoreRank.D, 0.0)]
        [TestCase(ScoreRank.C, 0.65)]
        [TestCase(ScoreRank.B, 0.8)]
        [TestCase(ScoreRank.X, 1.0)]
        [TestCase(ScoreRank.F, 0.0)]
        [TestCase(ScoreRank.S, 0.0)]
        [TestCase(ScoreRank.A, 0.975)]
        [TestCase(ScoreRank.SH, 0.98)]
        [TestCase(ScoreRank.XH, 1.0)]
        public void TestRankEdgeCases(ScoreRank rank, double accuracy)
        {
            AccuracyCircle circle = null!;
            AddStep("load rank", () =>
            {
                var score = TestResources.CreateTestScoreInfo();
                score.Rank = rank;
                score.Accuracy = accuracy;
                Child = circle = new AccuracyCircle(score)
                {
                    UseV2Style = true,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(414),
                };
            });
            AddUntilStep("rank animation completes", () => circle.ChildrenOfType<RankText>().First().Alpha == 1);
            AddAssert("ring progress is finite", () => circle.ChildrenOfType<CircularProgress>().All(p => double.IsFinite(p.Progress)));
        }

        [TestCase(ScoreRank.X)]
        [TestCase(ScoreRank.S)]
        [TestCase(ScoreRank.A)]
        [TestCase(ScoreRank.B)]
        [TestCase(ScoreRank.D)]
        public void TestV2RankEmblemAppear(ScoreRank rank)
        {
            V2RankEmblem emblem = null!;
            AddStep("load emblem", () =>
            {
                Child = emblem = new V2RankEmblem(rank)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };
            });
            AddUntilStep("emblem loaded", () => emblem.IsLoaded);
            AddStep("trigger appear", () => emblem.Appear());
            AddAssert("displayed grade letter", () => emblem.ChildrenOfType<SpriteText>().Single().Text.ToString(),
                () => Is.EqualTo(osu.Game.Online.Leaderboards.DrawableRank.GetRankLetter(rank)));
        }
    }
}
