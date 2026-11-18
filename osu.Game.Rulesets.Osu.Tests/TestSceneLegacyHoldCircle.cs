// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Replays;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// Circles converted from legacy holds are judged automatically from their end time, as in stable.
    /// </summary>
    public partial class TestSceneLegacyHoldCircle : RateAdjustedBeatmapTestScene
    {
        private const double time_circle = 1500;

        private ScoreAccessibleReplayPlayer currentPlayer = null!;
        private List<JudgementResult> judgementResults = null!;
        private List<double> judgementTimes = null!;

        [Test]
        public void TestEndShortlyBeforeStartCompletesItself()
        {
            // At OD10 the 50 window is 99.5ms: the circle is judged about 10ms before its start time.
            performTest(time_circle - 110);

            AddAssert("judged great", () => judgementResults.Single().Type, () => Is.EqualTo(HitResult.Great));
            AddAssert("judged around start", () => judgementResults.Single().TimeOffset, () => Is.EqualTo(-10).Within(9));
        }

        [Test]
        public void TestEndLongBeforeStartMissesEarly()
        {
            performTest(time_circle - 1000);

            AddAssert("judged miss", () => judgementResults.Single().Type, () => Is.EqualTo(HitResult.Miss));
            AddAssert("judged before start", () => judgementResults.Single().TimeOffset, () => Is.LessThan(-100));
        }

        [Test]
        public void TestEndAtIntegerLimitMissesEarly()
        {
            // The end time plus the hit window wraps around in stable's 32-bit arithmetic.
            performTest(int.MaxValue);

            AddAssert("judged miss", () => judgementResults.Single().Type, () => Is.EqualTo(HitResult.Miss));
            AddAssert("judged before start", () => judgementResults.Single().TimeOffset, () => Is.LessThan(-100));
        }

        [Test]
        public void TestEndAtStartBehavesAsCircle()
        {
            performTest(time_circle);

            AddAssert("judged miss", () => judgementResults.Single().Type, () => Is.EqualTo(HitResult.Miss));
            AddAssert("judged after hit window", () => judgementResults.Single().TimeOffset, () => Is.GreaterThan(99));
        }

        [Test]
        public void TestPositiveDurationHoldIgnoresPlayerClickAndJudgesAfterHoldEnd()
        {
            const double hold_duration = 600;

            performTest(time_circle + hold_duration, clickAtStart: true);

            AddAssert("not hit by click; judged miss at hold end", () => judgementResults.Single().Type, () => Is.EqualTo(HitResult.Miss));
            AddAssert("survived until hold end + hit window", () => judgementTimes.Single(), () => Is.GreaterThan(time_circle + hold_duration + 99));
        }

        [TestCase(2_147_483_560, false)]
        [TestCase(2_147_483_540, true)]
        public void TestHoldHitsoundSuppressionByDistanceToEndTime(double judgementTime, bool expectPlayed)
        {
            ManualClock manualClock = null!;
            OsuPlayfield playfield = null!;
            DrawableHitCircle? drawableCircle = null;

            AddStep("create hold circle near int32 wraparound", () =>
            {
                var hitCircle = new HitCircle
                {
                    StartTime = 2_147_483_500,
                    Position = OsuPlayfield.BASE_SIZE / 2,
                    LegacyHoldEndTime = 2_147_483_600,
                    Samples = new[] { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) },
                };
                hitCircle.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { OverallDifficulty = 10 });

                Child = playfield = new OsuPlayfield
                {
                    Clock = new FramedClock(manualClock = new ManualClock { CurrentTime = 2_147_482_000 }),
                };

                playfield.Add(hitCircle);
            });

            AddUntilStep("playfield loaded", () => playfield.IsLoaded);
            AddStep("advance to judgement time", () => manualClock.CurrentTime = judgementTime);
            AddUntilStep("circle judged as hit", () =>
            {
                drawableCircle = playfield.ChildrenOfType<DrawableHitCircle>().SingleOrDefault();
                return drawableCircle?.Result.IsHit == true;
            });
            AddAssert($"hitsound {(expectPlayed ? "played" : "suppressed")}",
                () => drawableCircle!.ChildrenOfType<PoolableSkinnableSample>().Any(s => s.Played),
                () => Is.EqualTo(expectPlayed));
        }

        private void performTest(double holdEndTime, bool clickAtStart = false)
        {
            AddStep("load player", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(new Beatmap<OsuHitObject>
                {
                    HitObjects =
                    {
                        new HitCircle
                        {
                            StartTime = time_circle,
                            Position = OsuPlayfield.BASE_SIZE / 2,
                            LegacyHoldEndTime = holdEndTime,
                        }
                    },
                    BeatmapInfo =
                    {
                        Difficulty = new BeatmapDifficulty { OverallDifficulty = 10 },
                        Ruleset = new OsuRuleset().RulesetInfo
                    },
                });

                var frames = new List<ReplayFrame>
                {
                    new OsuReplayFrame { Time = 0, Position = OsuPlayfield.BASE_SIZE / 2 },
                };

                if (clickAtStart)
                {
                    frames.Add(new OsuReplayFrame(time_circle, OsuPlayfield.BASE_SIZE / 2, OsuAction.LeftButton));
                    frames.Add(new OsuReplayFrame(time_circle + 50, OsuPlayfield.BASE_SIZE / 2));
                }

                frames.Add(new OsuReplayFrame { Time = Math.Max(time_circle, holdEndTime == int.MaxValue ? time_circle : holdEndTime) + 1000, Position = OsuPlayfield.BASE_SIZE / 2 });

                var p = new ScoreAccessibleReplayPlayer(new Score { Replay = new Replay { Frames = frames } });

                p.OnLoadComplete += _ =>
                {
                    p.ScoreProcessor.NewJudgement += result =>
                    {
                        if (currentPlayer != p)
                            return;

                        judgementResults.Add(result);
                        judgementTimes.Add(p.GameplayClockContainer.CurrentTime);
                    };
                };

                LoadScreen(currentPlayer = p);
                judgementResults = new List<JudgementResult>();
                judgementTimes = new List<double>();
            });

            AddUntilStep("Beatmap at 0", () => Beatmap.Value.Track.CurrentTime == 0);
            AddUntilStep("Wait until player is loaded", () => currentPlayer.IsCurrentScreen());
            AddUntilStep("Wait for completion", () => currentPlayer.ScoreProcessor.HasCompleted.Value);
        }

        private partial class ScoreAccessibleReplayPlayer : ReplayPlayer
        {
            public new ScoreProcessor ScoreProcessor => base.ScoreProcessor;
            public new GameplayClockContainer GameplayClockContainer => base.GameplayClockContainer;

            protected override bool PauseOnFocusLost => false;

            public ScoreAccessibleReplayPlayer(Score score)
                : base(score, new PlayerConfiguration
                {
                    AllowPause = false,
                    ShowResults = false,
                })
            {
            }
        }
    }
}
