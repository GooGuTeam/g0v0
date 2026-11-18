// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Mods;
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

        [Test]
        public void TestPreV5HoldAppliesOffsetOnce()
        {
            const string v4_text = @"osu file format v4

[General]
Mode: 0

[TimingPoints]
0,500,4,1,0,100,1,0

[HitObjects]
100,100,1000,128,0,1500:0:0:0:0:
";
            var objects = convert(v4_text);
            var holdCircle = (HitCircle)objects.Single();

            Assert.That(holdCircle.StartTime, Is.EqualTo(1024));
            Assert.That(holdCircle.LegacyHoldEndTime, Is.EqualTo(1524));
        }

        [Test]
        public void TestLegacyHoldAndTickSuppressionRoundTripInOsuMode()
        {
            const string roundtrip_text = @"osu file format v14

[General]
Mode: 0

[Difficulty]
HPDrainRate:5
CircleSize:4
OverallDifficulty:8
ApproachRate:9
SliderMultiplier:1.4
SliderTickRate:1

[TimingPoints]
0,500,4,1,0,100,1,0
1000,NaN,4,1,0,100,0,0
2000,-100,4,1,0,100,0,0

[HitObjects]
100,100,1000,132,0,1600:0:0:0:0:
200,100,2000,128,0,800:0:0:0:0:
";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(roundtrip_text));
            using var reader = new LineBufferedReader(stream);

            var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
            var converted = new OsuRuleset().CreateBeatmapConverter(decoded).Convert();

            var builder = new StringBuilder();
            using (var writer = new StringWriter(builder))
                new LegacyBeatmapEncoder(converted, null, null).Encode(writer);

            string encoded = builder.ToString();

            var redecodedObjects = convert(encoded);
            Assert.That(redecodedObjects, Is.All.TypeOf<HitCircle>());
            Assert.That(redecodedObjects.Select(h => ((HitCircle)h).LegacyHoldEndTime), Is.EqualTo(new double?[] { 1600, 800 }));
            Assert.That(redecodedObjects[0].NewCombo, Is.True);
            Assert.That(redecodedObjects[1].NewCombo, Is.False);

            using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes(encoded));
            using var reader2 = new LineBufferedReader(stream2);
            var redecodedBeatmap = Decoder.GetDecoder<Beatmap>(reader2).Decode(reader2);

            var legacyControlPoints = (osu.Game.Beatmaps.Legacy.LegacyControlPointInfo)redecodedBeatmap.ControlPointInfo;
            Assert.That(legacyControlPoints.DifficultyPointAt(1000).GenerateTicks, Is.False);
            Assert.That(legacyControlPoints.DifficultyPointAt(2000).GenerateTicks, Is.True);
        }

        [Test]
        public void TestNonFiniteAndExtremeSliderLengthsAreClamped()
        {
            const string slider_text = @"osu file format v14

[General]
Mode: 0

[Difficulty]
SliderMultiplier:1.4
SliderTickRate:1

[TimingPoints]
0,500,4,1,0,100,1,0

[HitObjects]
100,100,1000,2,0,L|200:100,1,NaN
100,100,2000,2,0,L|200:100,1,Infinity
100,100,3000,2,0,L|200:100,1,-Infinity
100,100,4000,2,0,L|200:100,1,1e35
";
            var objects = convert(slider_text);
            var sliders = objects.Cast<Slider>().ToArray();

            Assert.That(sliders, Has.Length.EqualTo(4));
            Assert.That(sliders[0].Path.Distance, Is.EqualTo(100));
            Assert.That(sliders[2].Path.Distance, Is.EqualTo(100));

            foreach (var slider in sliders)
            {
                Assert.That(double.IsFinite(slider.Path.Distance), Is.True);
                Assert.That(double.IsFinite(slider.EndTime), Is.True);
                Assert.That(slider.Path.CalculatedPath.All(v => float.IsFinite(v.X) && float.IsFinite(v.Y)), Is.True);
            }
        }

        [Test]
        public void TestZeroMeterVariantsAndNightcoreZeroDivisorGuard()
        {
            const string meter_text = @"osu file format v14

[General]
Mode: 0

[TimingPoints]
0,500, 0,1,0,100,1,0
1000,500,-0,1,0,100,1,0
2000,500,+0,1,0,100,1,0
3000,500,00,1,0,100,1,0
";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(meter_text));
            using var reader = new LineBufferedReader(stream);

            var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
            Assert.That(decoded.ControlPointInfo.TimingPoints, Has.Count.EqualTo(4));

            foreach (var tp in decoded.ControlPointInfo.TimingPoints)
            {
                Assert.That(tp.TimeSignature.Numerator, Is.EqualTo(4));
                Assert.That(tp.LegacyTimeSignature, Is.Null);
            }

            Assert.DoesNotThrow(() => _ = ModNightcore<OsuHitObject>.NightcoreBeatContainer.GetLegacyBeatSamples(0, 0, false, true));
            Assert.DoesNotThrow(() => _ = ModNightcore<OsuHitObject>.NightcoreBeatContainer.GetLegacyBeatSamples(int.MinValue, -1, false, true));
        }

        [TestCase(2e9)]
        [TestCase(1e15)]
        public void TestExtremeSpinnerDurationCapsTicksWithoutOverflow(double duration)
        {
            var legacySpinner = new Spinner { StartTime = 1000, Duration = duration };
            legacySpinner.ApplyLegacyScoreV1(new ControlPointInfo(), new BeatmapDifficulty());

            Assert.That(legacySpinner.LegacyHalfSpinsRequired, Is.GreaterThan(0));
            Assert.That(legacySpinner.NestedHitObjects.Count, Is.LessThanOrEqualTo(5000));

            var modernSpinner = new Spinner { StartTime = 1000, Duration = duration };
            modernSpinner.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty(), CancellationToken.None);

            Assert.That(modernSpinner.SpinsRequired, Is.GreaterThan(0));
            Assert.That(modernSpinner.NestedHitObjects.Count, Is.LessThanOrEqualTo(5000));
        }

        private static OsuHitObject[] convert(string text = beatmap_text)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            using var reader = new LineBufferedReader(stream);

            var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
            var converted = new OsuRuleset().CreateBeatmapConverter(decoded).Convert();

            foreach (var h in converted.HitObjects)
                h.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);

            return converted.HitObjects.Cast<OsuHitObject>().ToArray();
        }
    }
}
