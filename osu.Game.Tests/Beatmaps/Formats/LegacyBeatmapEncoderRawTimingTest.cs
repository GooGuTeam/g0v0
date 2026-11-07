// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Beatmaps.Legacy;
using osu.Game.IO;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

namespace osu.Game.Tests.Beatmaps.Formats
{
    [TestFixture]
    public class LegacyBeatmapEncoderRawTimingTest
    {
        [Test]
        public void TestNumericRawTimingRoundtrip(
            [Values(0, 1, 2, 3)] int sourceMode,
            [Values(0.01, 60001, 0, -50, -5000, -20000, -1, 500)] double rawBeatLength,
            [Values(null, "-100", "-50", "-1000", "-20000")] string? inheritedBeatLength)
        {
            string green = inheritedBeatLength == null ? string.Empty : $"1000,{inheritedBeatLength},4,1,0,100,0,0";
            var original = decode(FormattableString.Invariant($"""
                osu file format v14
                [General]
                Mode:{sourceMode}
                [TimingPoints]
                1000,{rawBeatLength},4,1,0,100,1,0
                {green}
                """));
            var restored = decode(encode(original));
            double multiplier = inheritedBeatLength == null
                ? sourceMode != 0 && rawBeatLength < 0 ? 100 / -rawBeatLength : 1
                : 100 / -double.Parse(inheritedBeatLength, CultureInfo.InvariantCulture);

            foreach (var beatmap in new[] { original, restored })
            {
                Assert.That(beatmap.ControlPointInfo.TimingPoints, Has.Count.EqualTo(1));
                Assert.That(beatmap.ControlPointInfo.TimingPoints.Single().LegacyBeatLength, Is.EqualTo(rawBeatLength));
                var difficulty = ((LegacyControlPointInfo)beatmap.ControlPointInfo).DifficultyPointAt(1000);
                Assert.That(difficulty.SliderVelocity, Is.EqualTo(Math.Clamp(multiplier, 0.1, 10)));
                Assert.That(difficulty.GenerateTicks, Is.True);
                Assert.That(beatmap.ControlPointInfo.EffectPointAt(1000).ScrollSpeed,
                    Is.EqualTo(sourceMode is 1 or 3 ? Math.Clamp(multiplier, 0.01, 10) : 1));
            }
        }

        [TestCase(1)]
        [TestCase(3)]
        public void TestSameTimeGreenMustOverrideNativeRedlineScrollSpeed(int mode)
        {
            string red = FormattableString.Invariant($"""
                osu file format v14
                [General]
                Mode:{mode}
                [TimingPoints]
                1000,-5000,4,1,0,100,1,0
                """);
            Assert.That(decode(red).ControlPointInfo.EffectPointAt(1000).ScrollSpeed, Is.EqualTo(0.02));
            var original = decode(red + "\n1000,-1000,4,1,0,100,0,0");
            var restored = decode(encode(original));
            Assert.That(original.ControlPointInfo.EffectPointAt(1000).ScrollSpeed, Is.EqualTo(0.1));
            Assert.That(restored.ControlPointInfo.EffectPointAt(1000).ScrollSpeed, Is.EqualTo(0.1));
        }

        [TestCase(0.01)]
        [TestCase(60001)]
        [TestCase(0)]
        [TestCase(-50)]
        [TestCase(500)]
        public void TestRawTimingSaveEditUndoRedoPreservesNumericPlayback(double rawBeatLength)
        {
            // Close nodes (.01 timing) have an existing sample-volume save gap. This test
            // protects timing/history only; the unchanged failing sample repro is in roundtrip.md.
            checkHistory(rawBeatLength, assertNumericPlaybackEquivalent);
        }

        [TestCase(60001)]
        [TestCase(0)]
        [TestCase(-50)]
        [TestCase(500)]
        public void TestSpacedNodesSaveEditUndoRedoPreservesSamples(double rawBeatLength) =>
            checkHistory(rawBeatLength, assertPlaybackEquivalent);

        private static void checkHistory(double rawBeatLength, Action<Slider, Slider> assertEquivalent)
        {
            using var editor = new EditorBeatmap(playable(decode(standardSource(rawBeatLength))));
            using var history = new BeatmapEditorChangeHandler(editor);
            var originalSlider = (Slider)editor.HitObjects.Single();
            double duration = 4 * (rawBeatLength > 0 ? rawBeatLength : 1000);
            Assert.That(originalSlider.Duration, Is.EqualTo(duration).Within(1e-8));
            Assert.That(originalSlider.NodeSamples.Select(n => (n.Single().Bank, n.Single().Volume)),
                Is.EqualTo(new[] { ("normal", 100), ("soft", 20), ("drum", 70) }));
            assertEquivalent(originalSlider, reloadSlider(editor));

            history.SaveState();
            string originalHash = history.CurrentStateHash;
            editor.BeginChange();
            editor.ControlPointInfo.TimingPoints.First().BeatLength = 250;
            editor.UpdateAllHitObjects();
            editor.EndChange();
            Assert.That(editor.ControlPointInfo.TimingPoints.First().LegacyBeatLength, Is.Null);
            var editedSlider = (Slider)editor.HitObjects.Single();
            Assert.That(editedSlider.Duration, Is.EqualTo(1000));
            assertEquivalent(editedSlider, reloadSlider(editor));
            string editedHash = history.CurrentStateHash;
            Assert.That(editedHash, Is.Not.EqualTo(originalHash));
            Assert.That(history.CanUndo.Value, Is.True);

            history.RestoreState(-1);
            Assert.That(history.CurrentStateHash, Is.EqualTo(originalHash));
            Assert.That(editor.ControlPointInfo.TimingPoints.First().LegacyBeatLength, Is.EqualTo(rawBeatLength));
            var undoneSlider = (Slider)editor.HitObjects.Single();
            Assert.That(undoneSlider.Duration, Is.EqualTo(duration).Within(1e-8));
            assertEquivalent(undoneSlider, reloadSlider(editor));
            Assert.That(history.CanRedo.Value, Is.True);

            history.RestoreState(1);
            Assert.That(history.CurrentStateHash, Is.EqualTo(editedHash));
            Assert.That(editor.ControlPointInfo.TimingPoints.First().LegacyBeatLength, Is.EqualTo(250));
            var redoneSlider = (Slider)editor.HitObjects.Single();
            Assert.That(redoneSlider.Duration, Is.EqualTo(1000));
            assertEquivalent(redoneSlider, reloadSlider(editor));
        }

        private static void assertNumericPlaybackEquivalent(Slider expected, Slider actual)
        {
            Assert.That(actual.Velocity, Is.EqualTo(expected.Velocity));
            Assert.That(actual.Duration, Is.EqualTo(expected.Duration));
            Assert.That(actual.SliderVelocityMultiplier, Is.EqualTo(expected.SliderVelocityMultiplier));
            Assert.That(actual.Path.Distance, Is.EqualTo(expected.Path.Distance));
            Assert.That(actual.NestedHitObjects.Select(h => (h.GetType(), h.StartTime)),
                Is.EqualTo(expected.NestedHitObjects.Select(h => (h.GetType(), h.StartTime))));
        }

        private static void assertPlaybackEquivalent(Slider expected, Slider actual)
        {
            assertNumericPlaybackEquivalent(expected, actual);
            Assert.That(expected.NodeSamples.Select(n => (n.Single().Bank, n.Single().Volume)),
                Is.EqualTo(new[] { ("normal", 100), ("soft", 20), ("drum", 70) }));
            Assert.That(actual.NodeSamples.SelectMany(n => n).Select(s => (s.Bank, s.Volume)),
                Is.EqualTo(expected.NodeSamples.SelectMany(n => n).Select(s => (s.Bank, s.Volume))));
            Assert.That(actual.NestedHitObjects.SelectMany(h => h.Samples).Select(s => (s.Bank, s.Volume)),
                Is.EqualTo(expected.NestedHitObjects.SelectMany(h => h.Samples).Select(s => (s.Bank, s.Volume))));
        }

        private static string standardSource(double rawBeatLength)
        {
            double effectiveBeatLength = rawBeatLength > 0 ? rawBeatLength : 1000;
            double repeatSampleTime = 1000 + 2 * effectiveBeatLength + LegacyBeatmapDecoder.CONTROL_POINT_LENIENCY;
            double tailSampleTime = 1000 + 4 * effectiveBeatLength + LegacyBeatmapDecoder.CONTROL_POINT_LENIENCY;
            return FormattableString.Invariant($"""
                osu file format v14
                [Difficulty]
                SliderMultiplier:1
                SliderTickRate:1
                [TimingPoints]
                0,{rawBeatLength},4,1,0,100,1,0
                {repeatSampleTime},-100,4,2,0,20,0,0
                {tailSampleTime},-100,4,3,0,70,0,0
                [HitObjects]
                256,192,1000,2,0,L|456:192,2,200
                """);
        }

        private static Slider reloadSlider(IBeatmap source) => (Slider)playable(decode(encode(source))).HitObjects.Single();

        private static IBeatmap playable(Beatmap beatmap) => new TestWorkingBeatmap(beatmap).GetPlayableBeatmap(new OsuRuleset().RulesetInfo);

        private static Beatmap decode(string text)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            using var reader = new LineBufferedReader(stream);
            return Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
        }

        private static string encode(IBeatmap beatmap)
        {
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            new LegacyBeatmapEncoder(beatmap, null, null).Encode(writer);
            return writer.ToString();
        }
    }
}
