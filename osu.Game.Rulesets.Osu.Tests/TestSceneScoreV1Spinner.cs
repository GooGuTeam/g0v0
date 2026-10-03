// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneScoreV1Spinner : RateAdjustedBeatmapTestScene
    {
        private ScoreAccessibleReplayPlayer currentPlayer = null!;

        [TestCase(3115, 14400)]
        [TestCase(866, 2600)]
        [TestCase(2711, 13100)]
        [TestCase(2134, 10400)]
        public void TestStableAutoplaySpinnerScore(double duration, long expectedBonus)
        {
            AddStep("load autoplay", () =>
            {
                Mod[] mods = { new OsuModClassic(), new OsuModScoreV1(), new OsuModAutoplay() };
                SelectedMods.Value = mods;
                Beatmap.Value = CreateWorkingBeatmap(new Beatmap<OsuHitObject>
                {
                    HitObjects =
                    {
                        new Spinner
                        {
                            StartTime = 2000,
                            Duration = duration,
                            Position = OsuPlayfield.BASE_SIZE / 2
                        }
                    },
                    BeatmapInfo =
                    {
                        Difficulty = new BeatmapDifficulty { OverallDifficulty = 8.8f },
                        Ruleset = new OsuRuleset().RulesetInfo
                    }
                });
                var playable = Beatmap.Value.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, mods);
                LoadScreen(currentPlayer = new ScoreAccessibleReplayPlayer(new Score
                {
                    ScoreInfo = new ScoreInfo { Mods = mods },
                    Replay = new OsuAutoGenerator(playable, mods).Generate()
                }));
            });
            AddUntilStep("player loaded", () => currentPlayer.IsCurrentScreen());
            AddUntilStep("spinner completed", () => currentPlayer.ScoreProcessor.HasCompleted.Value);
            AddAssert("stable score", () => currentPlayer.ScoreProcessor.TotalScore.Value, () => Is.EqualTo(expectedBonus + 300));
        }

        [Test]
        [Explicit("Requires the locally installed Cross The Finish Line beatmap via OSU_SCORE_V1_BEATMAP.")]
        public void TestCrossTheFinishLineAutoplay()
        {
            AddStep("load beatmap autoplay", () =>
            {
                string path = Environment.GetEnvironmentVariable("OSU_SCORE_V1_BEATMAP")
                              ?? throw new InvalidOperationException("Set OSU_SCORE_V1_BEATMAP to the .osu file path.");
                Mod[] mods = { new OsuModClassic(), new OsuModScoreV1(), new OsuModAutoplay() };
                SelectedMods.Value = mods;
                Beatmap.Value = CreateWorkingBeatmap(new FlatWorkingBeatmap(path).Beatmap);
                var playable = Beatmap.Value.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, mods);
                LoadScreen(currentPlayer = new ScoreAccessibleReplayPlayer(new Score
                {
                    ScoreInfo = new ScoreInfo { Mods = mods },
                    Replay = new OsuAutoGenerator(playable, mods).Generate()
                }));
            });
            AddUntilStep("player loaded", () => currentPlayer.IsCurrentScreen());
            AddUntilStep("autoplay completed", () => currentPlayer.ScoreProcessor.HasCompleted.Value);
            AddAssert("stable autoplay score", () => currentPlayer.ScoreProcessor.TotalScore.Value, () => Is.EqualTo(10568888));
        }

        private partial class ScoreAccessibleReplayPlayer : ReplayPlayer
        {
            public new ScoreProcessor ScoreProcessor => base.ScoreProcessor;

            protected override bool PauseOnFocusLost => false;

            public ScoreAccessibleReplayPlayer(Score score)
                : base(score, new PlayerConfiguration { AllowPause = false, ShowResults = false })
            {
            }
        }
    }
}
