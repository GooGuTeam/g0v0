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
    public partial class TestSceneScoreV1Spinner : ScreenTestScene
    {
        private ScoreAccessibleReplayPlayer currentPlayer = null!;
        private readonly List<string> replayDiagnostics = new List<string>();
        private Replay? diagnosticReplay;
        private int diagnosticFrame;
        private bool steppingReplay;
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

        [TestCase(false)]
        [TestCase(true)]
        [Explicit("Requires the locally installed Cross The Finish Line beatmap via OSU_SCORE_V1_BEATMAP.")]
        public void TestCrossTheFinishLineAutoplay(bool scoreV2)
        {
            AddStep("load beatmap autoplay", () =>
            {
                preserveModRate = false;
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
            var spinnerAwards = new List<(int normal, int bonus)>();
            AddStep("load beatmap with ScoreV2 DT autoplay", () =>
            {
                preserveModRate = true;
                string path = Environment.GetEnvironmentVariable("OSU_SCORE_V2_BEATMAP")
                              ?? throw new InvalidOperationException("Set OSU_SCORE_V2_BEATMAP to the .osu file path.");
                Mod[] mods = { new OsuModScoreV2(), new OsuModDoubleTime(), new OsuModAutoplay() };
                SelectedMods.Value = mods;
                Beatmap.Value = CreateWorkingBeatmap(new FlatWorkingBeatmap(path).Beatmap);
                var playable = Beatmap.Value.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, mods);
                var replay = new OsuAutoGenerator(playable, mods).Generate();
                spinnerAwards.Clear();
                int normalAwards = 0;
                int bonusAwards = 0;
                var player = new ScoreAccessibleReplayPlayer(new Score
                {
                    ScoreInfo = new ScoreInfo { Mods = mods },
                    Replay = replay
                });
                player.OnLoadComplete += _ => player.ScoreProcessor.NewJudgement += result =>
                {
                    if (result.HitObject is SpinnerTick && result.IsHit)
                    {
                        if (result.HitObject is SpinnerBonusTick)
                            bonusAwards++;
                        else
                            normalAwards++;
                    }

                    if (result is OsuSpinnerJudgementResult spinner)
                    {
                        spinnerAwards.Add((normalAwards, bonusAwards));
                        var statistics = player.ScoreProcessor.GetScoreProcessorStatistics();
                        TestContext.Out.WriteLine($"Spinner={spinner.HitObject.StartTime}, Rotation={spinner.TotalRotation}, HalfTurns={(int)(spinner.TotalRotation / 180)}, NormalAwards={normalAwards}, BonusAwards={bonusAwards}, CumulativeBonus={statistics.BonusPortion}");
                        normalAwards = bonusAwards = 0;
                    }
                };
                LoadScreen(currentPlayer = player);
            });
            AddUntilStep("player loaded", () => currentPlayer.IsCurrentScreen());
            AddUntilStep("autoplay completed", () => currentPlayer.ScoreProcessor.HasCompleted.Value);
            AddStep("log scoring components", () =>
            {
                var processor = currentPlayer.ScoreProcessor;
                var statistics = processor.GetScoreProcessorStatistics();
                TestContext.Out.WriteLine($"ScoreV2=True, DoubleTime=True, Total={processor.TotalScore.Value}, Accuracy={processor.Accuracy.Value}, Combo={processor.Combo.Value}, ComboPortion={statistics.ComboPortion}, MaximumComboPortion={processor.MaximumComboPortion}, Bonus={statistics.BonusPortion}");
            });
            AddAssert("DT rate preserved", () => currentPlayer.GameplayClockContainer.GetTrueGameplayRate(), () => Is.EqualTo(1.5));
            AddAssert("six normal and six bonus awards on each spinner", () => spinnerAwards, () => Is.EqualTo(Enumerable.Repeat((6, 6), 4)));
            AddAssert("reported stable autoplay score", () => currentPlayer.ScoreProcessor.TotalScore.Value, () => Is.EqualTo(1217280));
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
                preserveModRate = false;
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

            if (!preserveModRate && Beatmap.Value.TrackLoaded)
                // Retain test-runner rate adjustment for the existing NT and legacy replay diagnostics.
                Beatmap.Value.Track.Tempo.Value = Clock.Rate;
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
