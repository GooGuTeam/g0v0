// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    [HeadlessTest]
    public partial class TestSceneAutoGeneration : OsuTestScene
    {
        [TestCase(866, false)]
        [TestCase(1667, false)]
        [TestCase(3115, false)]
        [TestCase(866, true)]
        [TestCase(1667, true)]
        [TestCase(3115, true)]
        public void TestLegacySpinnerAutoplaySharedInitialisation(double duration, bool doubleTime)
        {
            var scoreV1Frames = generateSpinnerFrames(duration, doubleTime, 1);
            var scoreV2Frames = generateSpinnerFrames(duration, doubleTime, 2);

            Assert.That(scoreV2Frames.Select(f => (f.Time, f.Position)), Is.EqualTo(scoreV1Frames.Select(f => (f.Time, f.Position))));
            Assert.That(scoreV2Frames.Select(f => f.Actions), Is.EqualTo(scoreV1Frames.Select(f => f.Actions)));
            // The first intermediate frame snaps to the fixed spin radius without changing angle.
            Assert.That(spinnerAngle(scoreV2Frames[1]), Is.EqualTo(spinnerAngle(scoreV2Frames[0])).Within(0.000001));
            Assert.That(scoreV2Frames[2].Position, Is.Not.EqualTo(scoreV2Frames[1].Position));

            // Ordinary lazer autoplay still starts rotating on its first intermediate frame.
            var nonLegacyFrames = generateSpinnerFrames(duration, doubleTime, 0);
            Assert.That(spinnerAngle(nonLegacyFrames[1]), Is.Not.EqualTo(spinnerAngle(nonLegacyFrames[0])).Within(0.000001));
        }

        [TestCase(866, 1)]
        [TestCase(1667, 1)]
        [TestCase(3115, 1)]
        [TestCase(6000, 1)]
        [TestCase(866, 1.5)]
        [TestCase(1667, 1.5)]
        [TestCase(3115, 1.5)]
        [TestCase(6000, 1.5)]
        public void TestLegacySpinnerAutoplayAcceleration(double duration, double rate)
        {
            var frames = generateSpinnerFrames(duration, rate == 1.5, 2);
            const double frame_duration = 1000.0 / 60;
            double acceleration = 0.00008 + Math.Max(0, (5000 - (int)duration) / 1000.0 / 2000);
            double velocity = 0;
            double expectedRotation = 0;
            double actualRotation = 0;
            double previousTime = 2000;

            for (int i = 1; i < frames.Length; i++)
            {
                double time = i == frames.Length - 1 ? 2000 + duration : 2000 + i * frame_duration * rate;

                if (i > 1)
                {
                    double elapsed = (time - previousTime) / rate;
                    velocity = Math.Min(0.05, velocity + acceleration * elapsed);
                    expectedRotation += velocity * elapsed;
                }

                double delta = spinnerAngle(frames[i]) - spinnerAngle(frames[i - 1]);
                actualRotation += Math.Abs(Math.Atan2(Math.Sin(delta), Math.Cos(delta)));
                Assert.That(actualRotation, Is.EqualTo(expectedRotation).Within(0.001), $"Frame {i}");
                previousTime = time;
            }

            Assert.That(velocity, Is.EqualTo(0.05));
            Assert.That(actualRotation, Is.LessThan(duration * 0.05 / rate));
        }

        private static double spinnerAngle(OsuReplayFrame frame) => Math.Atan2(frame.Position.Y - 192, frame.Position.X - 256);

        private static OsuReplayFrame[] generateSpinnerFrames(double duration, bool doubleTime, int scoringVersion)
        {
            var beatmap = new OsuBeatmap();
            var spinner = new Spinner { StartTime = 2000, Duration = duration };
            Mod[] mods = doubleTime ? new Mod[] { new OsuModDoubleTime() } : [];

            switch (scoringVersion)
            {
                case 1:
                    spinner.ApplyLegacyScoreV1(beatmap.ControlPointInfo, beatmap.Difficulty);
                    break;

                case 2:
                    spinner.ApplyLegacyScoreV2(beatmap.ControlPointInfo, beatmap.Difficulty);
                    break;

                default:
                    spinner.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                    break;
            }

            beatmap.HitObjects.Add(spinner);
            var generated = new OsuAutoGenerator(beatmap, mods).Generate();
            return generated.Frames.OfType<OsuReplayFrame>()
                            .Where(f => f.Time >= spinner.StartTime && f.Time <= spinner.EndTime)
                            .ToArray();
        }

        [TestCase(-1, true)]
        [TestCase(0, false)]
        [TestCase(1, false)]
        public void TestAlternating(double offset, bool shouldAlternate)
        {
            const double first_object_time = 1000;
            double secondObjectTime = first_object_time + AutoGenerator.KEY_UP_DELAY + OsuAutoGenerator.MIN_FRAME_SEPARATION_FOR_ALTERNATING + offset;

            var beatmap = new OsuBeatmap();
            beatmap.HitObjects.Add(new HitCircle { StartTime = first_object_time });
            beatmap.HitObjects.Add(new HitCircle { StartTime = secondObjectTime });

            var generated = new OsuAutoGenerator(beatmap, []).Generate();
            var frames = generated.Frames.OfType<OsuReplayFrame>().ToList();

            Assert.That(frames.Exists(f => f.Time == first_object_time && f.Actions.SingleOrDefault() == OsuAction.LeftButton));
            Assert.That(frames.Exists(f => f.Time == first_object_time + AutoGenerator.KEY_UP_DELAY && !f.Actions.Any()));

            Assert.That(frames.Exists(f => f.Time == secondObjectTime && f.Actions.SingleOrDefault() == (shouldAlternate ? OsuAction.RightButton : OsuAction.LeftButton)));
            Assert.That(frames.Exists(f => f.Time == secondObjectTime + AutoGenerator.KEY_UP_DELAY && !f.Actions.Any()));
        }

        [TestCase(300)]
        [TestCase(600)]
        [TestCase(1200)]
        public void TestAlternatingSpecificBPM(double bpm)
        {
            const double first_object_time = 1000;
            double secondObjectTime = first_object_time + 60000 / bpm;

            var beatmap = new OsuBeatmap();
            beatmap.HitObjects.Add(new HitCircle { StartTime = first_object_time });
            beatmap.HitObjects.Add(new HitCircle { StartTime = secondObjectTime });

            var generated = new OsuAutoGenerator(beatmap, []).Generate();
            var frames = generated.Frames.OfType<OsuReplayFrame>().ToList();

            Assert.That(frames.Exists(f => f.Time == first_object_time && f.Actions.SingleOrDefault() == OsuAction.LeftButton));
            Assert.That(frames.Exists(f => f.Time == first_object_time + AutoGenerator.KEY_UP_DELAY && !f.Actions.Any()));

            Assert.That(frames.Exists(f => f.Time == secondObjectTime && f.Actions.SingleOrDefault() == OsuAction.RightButton));
            Assert.That(frames.Exists(f => f.Time == secondObjectTime + AutoGenerator.KEY_UP_DELAY && !f.Actions.Any()));
        }
    }
}
