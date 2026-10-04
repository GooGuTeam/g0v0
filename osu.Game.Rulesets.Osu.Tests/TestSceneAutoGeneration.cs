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
