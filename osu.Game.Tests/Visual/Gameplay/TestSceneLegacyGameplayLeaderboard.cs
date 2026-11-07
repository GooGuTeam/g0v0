// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;
using osu.Game.Tests.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.Gameplay
{
    [Description("osu!stable's in-game scoreboard")]
    public partial class TestSceneLegacyGameplayLeaderboard : OsuTestScene
    {
        private const int other_scores = 10;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Cached(typeof(IGameplayLeaderboardProvider))]
        private readonly TestLeaderboardProvider provider = new TestLeaderboardProvider();

        [Cached]
        private readonly GameplayState gameplayState;

        private readonly Bindable<LocalUserPlayingState> playingState = new Bindable<LocalUserPlayingState>(LocalUserPlayingState.Playing);

        private LegacyGameplayLeaderboard leaderboard = null!;

        public TestSceneLegacyGameplayLeaderboard()
        {
            gameplayState = TestGameplayState.Create(new OsuRuleset(), null, new Score { ScoreInfo = new ScoreInfo { User = new APIUser { Username = "You" } } }, playingState);
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("show leaderboard", () => config.SetValue(OsuSetting.GameplayLeaderboard, true));
            AddStep("set playing", () => playingState.Value = LocalUserPlayingState.Playing);
        }

        [TestCase(1)]
        [TestCase(4)]
        [TestCase(10)]
        public void TestDefaultSkin(int trackedPosition)
        {
            AddStep("default skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            createLeaderboard(trackedPosition);
        }

        [Test]
        public void TestWindowFollowsTrackedScore()
        {
            AddStep("default skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            createLeaderboard(10);

            AddUntilStep("six shown", () => shownEntries(), () => Is.EqualTo(6));

            AddStep("hide when toggled off", () => config.SetValue(OsuSetting.GameplayLeaderboard, false));
            AddUntilStep("hidden during play", () => leaderboard.ChildrenOfType<Drawable>().First(d => d.Parent == leaderboard).Alpha, () => Is.Zero);

            AddStep("enter break", () => playingState.Value = LocalUserPlayingState.Break);
            AddUntilStep("shown during break", () => leaderboard.ChildrenOfType<Drawable>().First(d => d.Parent == leaderboard).Alpha, () => Is.EqualTo(1));

            AddStep("overtake to first", () => provider.SetTrackedPosition(1));
            AddUntilStep("still six shown", () => shownEntries(), () => Is.EqualTo(6));
        }

        /// <summary>
        /// Shows the scoreboard with the legacy skin folder named by <c>G0V0_PREVIEW_SKIN</c>.
        /// </summary>
        [TestCase(4)]
        [TestCase(10)]
        public void TestWithSkin(int trackedPosition)
        {
            string? directory = Environment.GetEnvironmentVariable(@"G0V0_PREVIEW_SKIN");

            if (string.IsNullOrEmpty(directory))
                Assert.Ignore(@"Set G0V0_PREVIEW_SKIN to a legacy skin folder to run this test.");

            AddStep("switch skin", () => skins.CurrentSkinInfo.Value = skins.Import(new ImportTask(directory)).GetResultSafely()!);
            createLeaderboard(trackedPosition);
            AddWaitStep("wait for animations", 5);
        }

        private int shownEntries() => leaderboard.ChildrenOfType<CompositeDrawable>().Count(d => d.GetType().Name == @"Entry" && d.Alpha > 0);

        private void createLeaderboard(int trackedPosition)
        {
            AddStep("create leaderboard", () =>
            {
                provider.Reset(trackedPosition);

                Child = new SkinProvidingContainer(skins.CurrentSkin.Value)
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(new Color4(40, 60, 90, 255), new Color4(90, 40, 70, 255)),
                        },
                        leaderboard = new LegacyGameplayLeaderboard
                        {
                            Y = LegacyGameplayLeaderboard.TopFor(@"osu"),
                        },
                    },
                };
            });
        }

        private class TestLeaderboardProvider : IGameplayLeaderboardProvider
        {
            IBindableList<GameplayLeaderboardScore> IGameplayLeaderboardProvider.Scores => Scores;
            public BindableList<GameplayLeaderboardScore> Scores { get; } = new BindableList<GameplayLeaderboardScore>();

            private GameplayLeaderboardScore tracked = null!;

            public void Reset(int trackedPosition)
            {
                Scores.Clear();

                for (int i = 0; i < other_scores; i++)
                {
                    Scores.Add(new GameplayLeaderboardScore(new ScoreInfo
                    {
                        User = new APIUser { Username = i == 2 ? @"AVeryLongPlayerNameThatGetsCut" : $@"Player {i + 1}" },
                        TotalScore = 1_000_000 - i * 80_000,
                        MaxCombo = 900 - i * 60,
                    }, false, GameplayLeaderboardScore.ComboDisplayMode.Highest));
                }

                tracked = new GameplayLeaderboardScore(new APIUser { Username = @"You" }, true, new BindableLong());
                tracked.Combo.Value = 321;
                Scores.Add(tracked);

                SetTrackedPosition(trackedPosition);
            }

            public void SetTrackedPosition(int position)
            {
                var others = Scores.Where(s => !s.Tracked).ToList();

                // score the tracked player just above the one it should overtake.
                tracked.TotalScore.Value = position > others.Count ? 100_000 : others[position - 1].TotalScore.Value + 1_000;

                var ordered = Scores.OrderByDescending(s => s.TotalScore.Value).ToList();

                for (int i = 0; i < ordered.Count; i++)
                {
                    ordered[i].DisplayOrder.Value = i;
                    ordered[i].Position.Value = i + 1;
                }
            }
        }
    }
}
