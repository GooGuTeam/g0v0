// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneOsuAutoGeneratorAspire : ScreenTestScene
    {
        private ScoreAccessibleReplayPlayer player = null!;

        [Test]
        public void TestDecodedShortSpinnerAutoplay(
            [Values(false, true)] bool classic,
            [Values(0, 1, 2)] int scoreVersion)
        {
            loadReplay(300, 5, classic, scoreVersion);
            AddAssert("spinner receives Great", () => player.ScoreProcessor.HitEvents.Single(e => e.HitObject is Spinner).Result,
                () => Is.EqualTo(HitResult.Great));
        }

        [Test]
        public void TestDecodedBoundaryReplayDiagnostics(
            [Values(0, 5, 10)] int overallDifficulty,
            [Values(false, true)] bool classic,
            [Values(0, 1, 2)] int scoreVersion)
        {
            // Observe rather than canonise the nonzero Classic threshold at the first
            // required half-spin. This regression only promises that autoplay is no longer skipped.
            int duration = overallDifficulty switch
            {
                0 => 334,
                5 => 200,
                _ => 134
            };
            loadReplay(duration, overallDifficulty, classic, scoreVersion);
            AddStep("record boundary judgement", () => TestContext.WriteLine(
                $"OD{overallDifficulty} CL={classic} SV={scoreVersion} duration={duration}: " +
                player.ScoreProcessor.HitEvents.Single(e => e.HitObject is Spinner).Result));
        }

        private void loadReplay(int duration, int overallDifficulty, bool classic, int scoreVersion)
        {
            AddStep("decode and generate autoplay replay", () =>
            {
                var mods = AspireRuntimeBeatmaps.Mods(classic, scoreVersion);
                SelectedMods.Value = mods;
                Beatmap.Value = CreateWorkingBeatmap(AspireRuntimeBeatmaps.Decode($"256,192,1000,8,0,{1000 + duration}", overallDifficulty));
                var playable = Beatmap.Value.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, mods);
                LoadScreen(player = new ScoreAccessibleReplayPlayer(new Score
                {
                    ScoreInfo = new ScoreInfo { Mods = mods },
                    Replay = new OsuAutoGenerator(playable, mods).Generate()
                }));
            });
            AddUntilStep("player loaded", () => player.IsCurrentScreen());
            AddUntilStep("spinner completed", () => player.ScoreProcessor.HasCompleted.Value);
        }

        protected override void Update()
        {
            base.Update();

            if (Beatmap.Value.TrackLoaded)
                Beatmap.Value.Track.Tempo.Value = Clock.Rate;
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
