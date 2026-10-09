// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Framework.Testing;
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
    public partial class TestSceneScoreV1Spinner : ScreenTestScene
    {
        private ScoreAccessibleReplayPlayer currentPlayer = null!;
        private bool preserveModRate;

        [TestCase(3115, 8.8f, false, false, 14400)]
        [TestCase(866, 8.8f, false, false, 2600)]
        [TestCase(2711, 8.8f, false, false, 13100)]
        [TestCase(2134, 8.8f, false, false, 10400)]
        [TestCase(1667, 6f, false, true, 7200)]
        [TestCase(1667, 6f, true, true, 3600)]
        public void TestStableAutoplaySpinnerScore(double duration, float overallDifficulty, bool scoreV2, bool doubleTime, long expectedBonus)
        {
            AddStep("load autoplay", () =>
            {
                preserveModRate = doubleTime;
                Mod[] mods = { new OsuModClassic(), scoreV2 ? new OsuModScoreV2() : new OsuModScoreV1(), new OsuModAutoplay() };

                if (doubleTime)
                    mods = mods.Append(new OsuModDoubleTime()).ToArray();

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
                        Difficulty = new BeatmapDifficulty { OverallDifficulty = overallDifficulty },
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
            AddAssert("stable spinner awards", () => currentPlayer.ScoreProcessor.GetScoreProcessorStatistics().BonusPortion, () => Is.EqualTo(expectedBonus));
            AddAssert("stable score", () => currentPlayer.ScoreProcessor.TotalScore.Value,
                () => Is.EqualTo((long)Math.Round(((scoreV2 ? 1000000 : 300) + expectedBonus) * (scoreV2 && doubleTime ? 1.2 : 1))));
        }

        protected override void Update()
        {
            base.Update();

            if (!preserveModRate && Beatmap.Value.TrackLoaded)
                Beatmap.Value.Track.Tempo.Value = Clock.Rate;
        }

        private partial class ScoreAccessibleReplayPlayer : ReplayPlayer
        {
            public new ScoreProcessor ScoreProcessor => base.ScoreProcessor;
            public new GameplayClockContainer GameplayClockContainer => base.GameplayClockContainer;

            protected override bool PauseOnFocusLost => false;

            public ScoreAccessibleReplayPlayer(Score score)
                : base(score, new PlayerConfiguration { AllowPause = false, ShowResults = false })
            {
            }
        }
    }
}
