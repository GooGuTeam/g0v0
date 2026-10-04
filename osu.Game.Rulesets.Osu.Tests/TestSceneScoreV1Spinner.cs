// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneScoreV1Spinner : RateAdjustedBeatmapTestScene
    {
        private ScoreAccessibleReplayPlayer currentPlayer = null!;
        private readonly List<string> replayDiagnostics = new List<string>();
        private Replay? diagnosticReplay;
        private int diagnosticFrame;
        private bool steppingReplay;

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

        [TestCase(false)]
        [TestCase(true)]
        [Explicit("Requires the locally installed Cross The Finish Line beatmap via OSU_SCORE_V1_BEATMAP.")]
        public void TestCrossTheFinishLineAutoplay(bool scoreV2)
        {
            AddStep("load beatmap autoplay", () =>
            {
                string path = Environment.GetEnvironmentVariable("OSU_SCORE_V1_BEATMAP")
                              ?? throw new InvalidOperationException("Set OSU_SCORE_V1_BEATMAP to the .osu file path.");
                Mod[] mods = { new OsuModClassic(), scoreV2 ? new OsuModScoreV2() : new OsuModScoreV1(), new OsuModAutoplay() };
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
            AddStep("log scoring components", () =>
            {
                var processor = currentPlayer.ScoreProcessor;
                var statistics = processor.GetScoreProcessorStatistics();
                TestContext.Out.WriteLine($"ScoreV2={scoreV2}, Total={processor.TotalScore.Value}, Accuracy={processor.Accuracy.Value}, Combo={processor.Combo.Value}, ComboPortion={statistics.ComboPortion}, MaximumComboPortion={processor.MaximumComboPortion}, Bonus={statistics.BonusPortion}");
            });
            AddAssert("perfect autoplay score", () => currentPlayer.ScoreProcessor.TotalScore.Value, () => Is.EqualTo(scoreV2 ? 1021302 : 10568888));
        }

        [Test]
        [Explicit("Requires the locally installed Byoushin Zenkai Girl [Hard] beatmap via OSU_SCORE_V2_BEATMAP.")]
        public void TestByoushinZenkaiGirlScoreV2DoubleTimeAutoplay()
        {
            AddStep("load beatmap with ScoreV2 DT autoplay", () =>
            {
                string path = Environment.GetEnvironmentVariable("OSU_SCORE_V2_BEATMAP")
                              ?? throw new InvalidOperationException("Set OSU_SCORE_V2_BEATMAP to the .osu file path.");
                Mod[] mods = { new OsuModScoreV2(), new OsuModDoubleTime(), new OsuModAutoplay() };
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
            AddStep("log scoring components", () =>
            {
                var processor = currentPlayer.ScoreProcessor;
                var statistics = processor.GetScoreProcessorStatistics();
                TestContext.Out.WriteLine($"ScoreV2=True, DoubleTime=True, Total={processor.TotalScore.Value}, Accuracy={processor.Accuracy.Value}, Combo={processor.Combo.Value}, ComboPortion={statistics.ComboPortion}, MaximumComboPortion={processor.MaximumComboPortion}, Bonus={statistics.BonusPortion}");
            });
            AddAssert("reported stable autoplay score", () => currentPlayer.ScoreProcessor.TotalScore.Value, () => Is.EqualTo(12117280));
        }

        [Test]
        [Explicit("Requires OSU_SCORE_V2_BEATMAP and OSU_SCORE_V2_REPLAY.")]
        public void TestScoreV2LegacyReplay() => testScoreV2LegacyReplay(frameStepped: true);

        [Test]
        [Explicit("Requires OSU_SCORE_V2_BEATMAP and OSU_SCORE_V2_REPLAY; exercises ordinary replay playback.")]
        public void TestScoreV2LegacyReplayPlayback() => testScoreV2LegacyReplay(frameStepped: false);

        private void testScoreV2LegacyReplay(bool frameStepped)
        {
            AddStep("load legacy replay", () =>
            {
                string beatmapPath = Environment.GetEnvironmentVariable("OSU_SCORE_V2_BEATMAP")
                                     ?? throw new InvalidOperationException("Set OSU_SCORE_V2_BEATMAP to the .osu file path.");
                string replayPath = Environment.GetEnvironmentVariable("OSU_SCORE_V2_REPLAY")
                                    ?? throw new InvalidOperationException("Set OSU_SCORE_V2_REPLAY to the .osr file path.");
                var beatmap = new FlatWorkingBeatmap(beatmapPath).Beatmap;
                Beatmap.Value = frameStepped
                    ? new ClockBackedTestWorkingBeatmap(beatmap, null, new FramedClock(new ManualClock { Rate = 0 }), Audio)
                    : CreateWorkingBeatmap(beatmap);
                using var stream = File.OpenRead(replayPath);
                var score = new LocalReplayDecoder(Beatmap.Value).Parse(stream);
                SelectedMods.Value = score.ScoreInfo.Mods;
                diagnosticReplay = score.Replay;
                diagnosticFrame = 0;
                steppingReplay = false;
                replayDiagnostics.Clear();
                var player = new ScoreAccessibleReplayPlayer(score);
                player.OnLoadComplete += _ => player.ScoreProcessor.NewJudgement += result =>
                {
                    var statistics = player.ScoreProcessor.GetScoreProcessorStatistics();
                    double endTime = result.HitObject.GetEndTime();
                    replayDiagnostics.Add($"{endTime + result.TimeOffset}\t{result.HitObject.StartTime}\t{result.HitObject.GetType().Name}\t{result.Type}\t{result.TimeOffset}\t{result.ComboAtJudgement}\t{result.ComboAfterJudgement}\t{statistics.ComboPortion}\t{statistics.BonusPortion}\t{(result is OsuSpinnerJudgementResult spinner ? spinner.TotalRotation : 0)}");
                };
                LoadScreen(currentPlayer = player);
            });
            AddUntilStep("player loaded", () => currentPlayer.IsCurrentScreen());

            if (frameStepped)
            {
                AddStep("start frame stepping", () =>
                {
                    currentPlayer.GameplayClockContainer.Stop();
                    // Diagnostic only: process each recorded replay frame directly, without interpolation.
                    // This must not be used as evidence of ordinary replay playback parity.
                    typeof(DrawableRuleset).GetProperty("FrameStablePlayback", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(currentPlayer.ChildrenOfType<DrawableRuleset>().Single(), false);
                    steppingReplay = true;
                });
            }

            AddUntilStep("replay completed", () => currentPlayer.ScoreProcessor.HasCompleted.Value);

            if (frameStepped)
                AddStep("stop frame stepping", () => steppingReplay = false);

            AddStep("log replay scoring", () =>
            {
                var processor = currentPlayer.ScoreProcessor;
                var statistics = processor.GetScoreProcessorStatistics();
                TestContext.Out.WriteLine($"FrameStepped={frameStepped}, Total={processor.TotalScore.Value}, Accuracy={processor.Accuracy.Value}, Combo={processor.Combo.Value}, ComboPortion={statistics.ComboPortion}, MaximumComboPortion={processor.MaximumComboPortion}, Bonus={statistics.BonusPortion}");
                var score = new ScoreInfo();
                processor.PopulateScore(score);
                TestContext.Out.WriteLine($"HighestCombo={score.MaxCombo}");
                foreach (var count in score.Statistics)
                    TestContext.Out.WriteLine($"{count.Key}: {count.Value}");
                File.WriteAllLines(Path.Combine(Path.GetTempPath(), frameStepped ? "g0v0-v2-judgements.tsv" : "g0v0-v2-playback-judgements.tsv"), replayDiagnostics);
            });
            AddAssert("stable replay score", () => currentPlayer.ScoreProcessor.TotalScore.Value, () => Is.EqualTo(long.Parse(Environment.GetEnvironmentVariable("OSU_SCORE_V2_EXPECTED") ?? "553207")));
        }

        protected override void Update()
        {
            if (steppingReplay && diagnosticReplay != null && diagnosticFrame < diagnosticReplay.Frames.Count)
                currentPlayer.GameplayClockContainer.Seek(diagnosticReplay.Frames[diagnosticFrame++].Time);
            base.Update();
        }

        private class LocalReplayDecoder : LegacyScoreDecoder
        {
            private readonly WorkingBeatmap beatmap;

            public LocalReplayDecoder(WorkingBeatmap beatmap) => this.beatmap = beatmap;

            protected override Ruleset GetRuleset(int rulesetId) => new OsuRuleset();

            protected override WorkingBeatmap GetBeatmap(string md5Hash) => beatmap;
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
