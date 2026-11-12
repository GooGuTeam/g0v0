// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
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

        private void performTest(double holdEndTime)
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
                    new OsuReplayFrame { Time = 0, Position = Vector2.Zero },
                    new OsuReplayFrame { Time = time_circle + 1000, Position = Vector2.Zero },
                };

                var p = new ScoreAccessibleReplayPlayer(new Score { Replay = new Replay { Frames = frames } });

                p.OnLoadComplete += _ =>
                {
                    p.ScoreProcessor.NewJudgement += result =>
                    {
                        if (currentPlayer == p) judgementResults.Add(result);
                    };
                };

                LoadScreen(currentPlayer = p);
                judgementResults = new List<JudgementResult>();
            });

            AddUntilStep("Beatmap at 0", () => Beatmap.Value.Track.CurrentTime == 0);
            AddUntilStep("Wait until player is loaded", () => currentPlayer.IsCurrentScreen());
            AddUntilStep("Wait for completion", () => currentPlayer.ScoreProcessor.HasCompleted.Value);
        }

        private partial class ScoreAccessibleReplayPlayer : ReplayPlayer
        {
            public new ScoreProcessor ScoreProcessor => base.ScoreProcessor;

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
