// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Beatmaps.Timing;
using osu.Game.IO;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit;

namespace osu.Game.Tests.Beatmaps.Formats
{
    [TestFixture]
    public class LegacySignedTimingTest
    {
        private const string timing_lines = """
            552,-9.88131291682493E-324,-4,2,0,5,1,0
            3552,400,-4,1,3,60,1,0
            108311,9.8E+304,4,1,7,5,1,0
            """;

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestExactTimingLinesAndSourceModeRoundtrip(int sourceMode)
        {
            var original = decode(source(sourceMode));
            var restored = decode(encode(original));
            foreach (var beatmap in new[] { original, restored })
            {
                var points = (LegacyControlPointInfo)beatmap.ControlPointInfo;
                Assert.That(points.TimingPoints.Select(p => p.Time), Is.EqualTo(new double[] { 552, 3552, 108311 }));
                Assert.That(points.TimingPoints.Select(p => p.LegacyBeatLength), Is.EqualTo(new double[] { -2 * double.Epsilon, 400, 9.8E+304 }));
                Assert.That(points.TimingPoints.Select(p => p.LegacyTimeSignature), Is.EqualTo(new int?[] { -4, -4, null }));
                Assert.That(points.TimingPoints.Select(p => p.TimeSignature.Numerator), Is.EqualTo(new[] { 4, 4, 4 }));
                Assert.That(points.TimingPoints.Select(p => p.BeatLength), Is.EqualTo(new double[] { 6, 400, 60000 }));
                Assert.That(points.SamplePointAt(552).SampleBank, Is.EqualTo("soft"));
                Assert.That(points.SamplePointAt(552).SampleVolume, Is.EqualTo(5));
                Assert.That(points.SamplePointAt(3552).SampleBank, Is.EqualTo("normal"));
                Assert.That(points.SamplePointAt(3552).SampleVolume, Is.EqualTo(60));
                Assert.That(points.SamplePointAt(108311).SampleVolume, Is.EqualTo(5));
                Assert.That(points.SamplePointAt(3552).ApplyTo(new osu.Game.Rulesets.Objects.Legacy.ConvertHitObjectParser.LegacyHitSampleInfo("hitnormal")).LookupNames.First(), Does.Contain("3"));
                Assert.That(points.SamplePointAt(108311).ApplyTo(new osu.Game.Rulesets.Objects.Legacy.ConvertHitObjectParser.LegacyHitSampleInfo("hitnormal")).LookupNames.First(), Does.Contain("7"));
                Assert.That(points.EffectPointAt(552).KiaiMode, Is.False);
                Assert.That(points.EffectPointAt(3552).KiaiMode, Is.False);
                Assert.That(points.EffectPointAt(108311).KiaiMode, Is.False);
                Assert.That(points.DifficultyPointAt(552).SliderVelocity, Is.EqualTo(sourceMode == 0 ? 1 : 10));
                Assert.That(points.DifficultyPointAt(3552).SliderVelocity, Is.EqualTo(1));
                Assert.That(points.DifficultyPointAt(108311).SliderVelocity, Is.EqualTo(1));
                Assert.That(points.EffectPointAt(552).ScrollSpeed, Is.EqualTo(sourceMode is 1 or 3 ? 10 : 1));
                Assert.That(points.EffectPointAt(3552).ScrollSpeed, Is.EqualTo(1));
                Assert.That(points.DifficultyPointAt(552).GenerateTicks, Is.True);
            }
        }

        [Test]
        public void TestSignedMeterDoesNotDiscardEffects()
        {
            var original = decode(source().Replace("3552,400,-4,1,3,60,1,0", "3552,400,-4,1,3,60,1,9", StringComparison.Ordinal));
            foreach (var beatmap in new[] { original, decode(encode(original)) })
            {
                Assert.That(beatmap.ControlPointInfo.TimingPointAt(3552).LegacyTimeSignature, Is.EqualTo(-4));
                Assert.That(beatmap.ControlPointInfo.TimingPointAt(3552).OmitFirstBarLine, Is.True);
                Assert.That(beatmap.ControlPointInfo.EffectPointAt(3552).KiaiMode, Is.True);
            }
        }

        [Test]
        public void TestCloneJsonAndMeterEditLifecycle()
        {
            var point = decode(source()).ControlPointInfo.TimingPoints.First();
            var clone = (TimingControlPoint)point.DeepClone();
            Assert.That(clone, Is.EqualTo(point));
            Assert.That(clone.GetHashCode(), Is.EqualTo(point.GetHashCode()));
            clone.CopyFrom(clone);
            Assert.That(clone.LegacyTimeSignature, Is.EqualTo(-4));
            clone.TimeSignatureBindable.Value = clone.TimeSignature;
            clone.TimeSignatureBindable.TriggerChange();
            Assert.That(clone.LegacyTimeSignature, Is.EqualTo(-4));
            var restored = point.Serialize().Deserialize<TimingControlPoint>();
            restored.Time = point.Time;
            Assert.That(restored, Is.EqualTo(point));

            int changes = 0;
            clone.Changed += _ => changes++;
            clone.TimeSignature = clone.TimeSignature;
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(clone.LegacyTimeSignature, Is.Null);
            Assert.That(clone.LegacyBeatLength, Is.EqualTo(point.LegacyBeatLength));
            Assert.That(clone, Is.Not.EqualTo(point));
            clone.CopyFrom(point);
            clone.TimeSignatureBindable.Value = TimeSignature.SimpleTriple;
            Assert.That(clone.LegacyTimeSignature, Is.Null);
            Assert.That(point.LegacyTimeSignature, Is.EqualTo(-4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimeSignature(-4));
        }

        [Test]
        public void TestEditorMeterEditUndoRedo()
        {
            using var editor = new EditorBeatmap(new TestWorkingBeatmap(decode(source(withObjects: true))).GetPlayableBeatmap(new OsuRuleset().RulesetInfo));
            using var history = new BeatmapEditorChangeHandler(editor);
            history.SaveState();
            string originalHash = history.CurrentStateHash;
            editor.BeginChange();
            var point = editor.ControlPointInfo.TimingPoints[1];
            point.TimeSignature = point.TimeSignature;
            editor.EndChange();
            Assert.That(point.LegacyTimeSignature, Is.Null);
            Assert.That(decode(encode(editor)).ControlPointInfo.TimingPoints[1].LegacyTimeSignature, Is.Null);
            Assert.That(history.CurrentStateHash, Is.Not.EqualTo(originalHash));
            history.RestoreState(-1);
            Assert.That(editor.ControlPointInfo.TimingPoints[1].LegacyTimeSignature, Is.EqualTo(-4));
            Assert.That(editor.ControlPointInfo.TimingPoints[0].LegacyBeatLength, Is.EqualTo(-2 * double.Epsilon));
            Assert.That(editor.ControlPointInfo.TimingPoints[2].LegacyBeatLength, Is.EqualTo(9.8E+304));
            Assert.That(history.CurrentStateHash, Is.EqualTo(originalHash));
            history.RestoreState(1);
            Assert.That(editor.ControlPointInfo.TimingPoints[1].LegacyTimeSignature, Is.Null);
        }

        [Test]
        public void TestNativeBoundedConsumersRemainBounded()
        {
            var beatmap = decode(source(withObjects: true));
            Assert.That(beatmap.ControlPointInfo.BPMMinimum, Is.EqualTo(1));
            Assert.That(beatmap.ControlPointInfo.BPMMaximum, Is.EqualTo(10000));
            Assert.That(double.IsFinite(beatmap.GetMostCommonBeatLength()), Is.True);
            Assert.That(double.IsFinite(beatmap.ControlPointInfo.GetClosestSnappedTime(108500, 4)), Is.True);
            var bars = new BarLineGenerator<osu.Game.Rulesets.Mania.Objects.BarLine>(beatmap).BarLines;
            Assert.That(bars.Count, Is.InRange(1, 300));
            Assert.That(bars.All(b => double.IsFinite(b.StartTime)), Is.True);
            Assert.That(bars.Select(b => b.StartTime), Is.Ordered);
        }

        [TestCase(-4)]
        [TestCase(-3)]
        [TestCase(-5)]
        [TestCase(-536870912)]
        [TestCase(-int.MaxValue)]
        public void TestNightcoreSignedMeterSourceSampleSelection(int meter)
        {
            var original = decode(source().Replace("3552,400,-4,", $"3552,400,{meter},", StringComparison.Ordinal));
            Assert.That(original.ControlPointInfo.TimingPoints[1].LegacyTimeSignature, Is.EqualTo(meter));
            Assert.That(decode(encode(original)).ControlPointInfo.TimingPoints[1].LegacyTimeSignature, Is.EqualTo(meter));

            foreach (bool hats in new[] { false, true })
                foreach (bool omit in new[] { false, true })
                {
                    foreach (int beat in Enumerable.Range(0, 65).Append(int.MaxValue))
                    {
                        bool segment = beat % (8L * meter) == 0;
                        int inBar = beat % meter;
                        var actual = ModNightcore<HitObject>.NightcoreBeatContainer.GetLegacyBeatSamples(beat, meter, omit, hats);
                        Assert.That(actual.Kick, Is.EqualTo(segment || inBar % 4 == 0));
                        Assert.That(actual.Finish, Is.EqualTo(segment && (beat > 0 || !omit)));
                        Assert.That(actual.Clap, Is.EqualTo(!segment && inBar % 4 == 2));
                        Assert.That(actual.Hat, Is.EqualTo(!segment && inBar % 4 != 0 && inBar % 4 != 2 && hats));
                    }
                }
        }

        private static string source(int mode = 0, bool withObjects = false) => $"""
            osu file format v14
            [General]
            Mode:{mode}
            [Difficulty]
            CircleSize:4
            SliderMultiplier:1
            SliderTickRate:1
            [TimingPoints]
            {timing_lines}
            [HitObjects]
            """ + (withObjects ? "\n256,192,1000,1,0\n256,192,4000,1,0\n256,192,108500,1,0" : string.Empty);

        private static Beatmap decode(string text)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            using var reader = new LineBufferedReader(stream);
            return new LegacyBeatmapDecoder(14).Decode(reader);
        }

        private static string encode(IBeatmap beatmap)
        {
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            new LegacyBeatmapEncoder(beatmap, null, null).Encode(writer);
            return writer.ToString();
        }
    }
}
