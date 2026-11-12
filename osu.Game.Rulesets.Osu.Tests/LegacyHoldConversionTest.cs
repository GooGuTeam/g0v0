// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Play;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class LegacyHoldConversionTest
    {
        private const string beatmap_text = @"osu file format v14

[General]
Mode: 0

[Difficulty]
HPDrainRate:5
CircleSize:4
OverallDifficulty:10
ApproachRate:10
SliderMultiplier:1.4
SliderTickRate:1

[TimingPoints]
0,500,4,1,0,100,1,0

[HitObjects]
100,100,1000,132,0,884:0:0:0:0:
200,100,2000,128,0,1000:0:0:0:0:
256,192,3000,128,0,2147483647:0:0:0:0:
300,100,4000,128,0
256,192,5000,12,0,6000,0:0:0:0:
";

        [Test]
        public void TestHoldsBecomeCirclesKeepingTheirEndTime()
        {
            var objects = convert();

            Assert.That(objects.Take(4), Is.All.TypeOf<HitCircle>());
            Assert.That(objects.Take(4).Select(h => ((HitCircle)h).LegacyHoldEndTime), Is.EqualTo(new double?[] { 884, 1000, int.MaxValue, 4000 }));
            Assert.That(objects.Take(4).Select(h => h.StartTime), Is.EqualTo(new double[] { 1000, 2000, 3000, 4000 }));
            Assert.That(objects[0].NewCombo, Is.True);
            Assert.That(objects[1].NewCombo, Is.False);
        }

        [Test]
        public void TestLongHoldDoesNotBecomeSpinner()
        {
            var objects = convert();

            Assert.That(objects.OfType<Spinner>().Count(), Is.EqualTo(1));
            Assert.That(objects.OfType<Spinner>().Single().StartTime, Is.EqualTo(5000));
            Assert.That(objects.Sum(h => h.NestedHitObjects.Count), Is.LessThan(100));
        }

        [TestCase(-2147481848, 450, true)]
        [TestCase(-2147481847, 450, false)]
        [TestCase(-2147481847, 1801, true)]
        [TestCase(-165385, 450, false)]
        [TestCase(0, 1800, false)]
        public void TestLeadInOverflow(double startTime, double preempt, bool overflows)
        {
            Assert.That(MasterGameplayClockContainer.OverflowsLegacyLeadIn(startTime, preempt), Is.EqualTo(overflows));
        }

        private static OsuHitObject[] convert()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(beatmap_text));
            using var reader = new LineBufferedReader(stream);

            var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
            var converted = new OsuRuleset().CreateBeatmapConverter(decoded).Convert();

            foreach (var h in converted.HitObjects)
                h.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);

            return converted.HitObjects.Cast<OsuHitObject>().ToArray();
        }
    }
}
