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
using osu.Game.IO;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Tests.Beatmaps;
using osu.Game.Screens.Edit;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

namespace osu.Game.Tests.Beatmaps.Formats
{
    [TestFixture]
    public class LegacyBeatmapEncoderAspireTest
    {
        [TestCase(131073d)]
        [TestCase(795250d)]
        [TestCase(2147483648d)]
        public void TestExplicitLengthIsNotLimitedByCoordinates(double length)
        {
            var beatmap = decode(FormattableString.Invariant($"""
                osu file format v14
                [Difficulty]
                SliderMultiplier:2.5
                SliderTickRate:0.5
                [TimingPoints]
                107152,0.4,4,1,7,30,1,0
                107152,NaN,4,1,7,30,0,0
                [HitObjects]
                216,28,107152,6,0,L|228:36,1,{length},8|0,0:0|0:0,0:0:0:0:
                """));

            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(1));
            Assert.That(((IHasPath)beatmap.HitObjects.Single()).Path.ExpectedDistance.Value, Is.EqualTo(length));
            var restored = decode(encode(beatmap));
            Assert.That(restored.HitObjects, Has.Count.EqualTo(1));
            Assert.That(((IHasPath)restored.HitObjects.Single()).Path.ExpectedDistance.Value, Is.EqualTo(length));

            // Use the original NaN timing for playable validation, without encoding the known tick-suppression save gap.
            var slider = (Slider)playable(beatmap).HitObjects.Single();
            Assert.That(slider.GenerateTicks, Is.False);
            Assert.That(slider.NestedHitObjects.OfType<SliderTick>(), Is.Empty);
            Assert.That(slider.Path.Distance, Is.EqualTo(length));
        }

        [TestCase("L|131072:0|0:0", "0", 262144)]
        [TestCase("L|-131072:0|0:0", "0", 262144)]
        [TestCase("L|131072:0|0:0", "", 262144)]
        [TestCase("L|400:0", "0", 400)]
        [TestCase("L|400:0", "200", 200)]
        [TestCase("L|0:0", "0", 0)]
        public void TestPathLengthRoundtrip(string path, string length, double distance)
        {
            string lengthField = length.Length == 0 ? string.Empty : $",{length}";
            var decoded = decode($"""
                osu file format v14
                [Difficulty]
                SliderMultiplier:1
                SliderTickRate:1
                [TimingPoints]
                0,500,4,1,0,100,1,0
                [HitObjects]
                0,0,1000,2,0,{path},1{lengthField}
                """);

            Assert.That(decoded.HitObjects, Has.Count.EqualTo(1));
            var original = (IHasPath)decoded.HitObjects.Single();
            Assert.That(original.Path.Distance, Is.EqualTo(distance));
            Assert.That(original.Path.ExpectedDistance.Value, Is.EqualTo(length is "" or "0" ? (double?)null : distance));

            foreach (var source in new IBeatmap[] { decoded, playable(decoded) })
            {
                var roundtrip = decode(encode(source));
                Assert.That(roundtrip.HitObjects, Has.Count.EqualTo(1));
                var restored = (IHasPath)roundtrip.HitObjects.Single();
                Assert.That(restored.Path.ExpectedDistance.Value, Is.EqualTo(original.Path.ExpectedDistance.Value));
                Assert.That(restored.Path.Distance, Is.EqualTo(distance));
                Assert.That(restored.Path.ControlPoints.Select(p => p.Position), Is.EqualTo(original.Path.ControlPoints.Select(p => p.Position)));
                Assert.That(restored.Path.PositionAt(1), Is.EqualTo(original.Path.PositionAt(1)));

                var slider = (Slider)playable(roundtrip).HitObjects.Single();
                int ticks = distance > 100000 ? 999 : distance == 400 ? 3 : distance == 200 ? 1 : 0;
                Assert.That(slider.NestedHitObjects.OfType<SliderTick>().Count(), Is.EqualTo(ticks));
                Assert.That(slider.NestedHitObjects.Count, Is.EqualTo(ticks + 2));
            }
        }

        [TestCase(99999)]
        [TestCase(100000)]
        [TestCase(100001)]
        [TestCase(120000)]
        [TestCase(131072)]
        public void TestCappedTicksRetainDistanceAndTime(int distance)
        {
            var decoded = decode($"""
                osu file format v14
                [Difficulty]
                SliderMultiplier:1
                SliderTickRate:1
                [TimingPoints]
                0,500,4,1,0,100,1,0
                [HitObjects]
                0,0,1000,2,0,L|{distance}:0,2,{distance}
                """);

            foreach (var beatmap in new[] { decoded, decode(encode(decoded)) })
            {
                var slider = (Slider)playable(beatmap).HitObjects.Single();
                Assert.That(slider.Path.Distance, Is.EqualTo(distance));
                Assert.That(slider.NestedHitObjects, Has.Count.EqualTo(2001));
                var ticks = slider.NestedHitObjects.OfType<SliderTick>().ToArray();
                Assert.That(ticks, Has.Length.EqualTo(1998));

                foreach (var tick in ticks)
                {
                    double tickDistance = tick.PathProgress * distance;
                    double expectedTime = slider.StartTime + (tick.SpanIndex == 0 ? tickDistance : 2 * distance - tickDistance) / slider.Velocity;
                    Assert.That(tick.StartTime, Is.EqualTo(expectedTime).Within(0.000001));
                    Assert.That(tick.Position.X, Is.EqualTo(tickDistance).Within(0.02));
                    Assert.That(tickDistance, Is.LessThan(Math.Min(distance, 100000) - slider.Velocity * 10));
                }

                Assert.That(ticks[0].StartTime, Is.EqualTo(1500).Within(0.000001));
                Assert.That(ticks[0].Position.X, Is.EqualTo(100).Within(0.00002));
                Assert.That(ticks[0].Position.Y, Is.Zero);
                Assert.That(ticks[^1].StartTime, Is.EqualTo(slider.EndTime - 500).Within(0.000001));
                Assert.That(ticks[^1].Position.X, Is.EqualTo(100).Within(0.00002));
                Assert.That(ticks[^1].Position.Y, Is.Zero);
                Assert.That(ticks.Select(t => t.StartTime), Is.Ordered);
                Assert.That(ticks.Where(t => t.SpanIndex == 0).Select(t => t.Position),
                    Is.EqualTo(ticks.Where(t => t.SpanIndex == 1).Reverse().Select(t => t.Position)));
            }
        }

        [Test]
        public void TestEditedSuppressedSliderVelocitySurvivesSaveAndHistory()
        {
            var decoded = decode("""
                osu file format v14
                [Difficulty]
                SliderMultiplier:1
                SliderTickRate:1
                [TimingPoints]
                0,500,4,1,0,100,1,0
                1000,NaN,4,1,0,100,0,0
                [HitObjects]
                0,0,1000,2,0,L|400:0,1,400
                """);
            var editor = new EditorBeatmap(playable(decoded));
            using var history = new BeatmapEditorChangeHandler(editor);
            var slider = (Slider)editor.HitObjects.Single();
            Assert.That(slider.GenerateTicks, Is.False);
            Assert.That(slider.Duration, Is.EqualTo(2000));

            history.SaveState();
            string initialHash = history.CurrentStateHash;

            // Match the velocity popover's transaction, property edit and update sequence.
            editor.BeginChange();
            slider.SliderVelocityMultiplier = 2;
            editor.Update(slider);
            editor.EndChange();

            Assert.That(slider.GenerateTicks, Is.False);
            Assert.That(slider.Velocity, Is.EqualTo(0.4));
            Assert.That(slider.Duration, Is.EqualTo(1000));
            Assert.That(history.CurrentStateHash, Is.Not.EqualTo(initialHash));
            Assert.That(history.CanUndo.Value, Is.True);
            string editedHash = history.CurrentStateHash;

            var restored = (Slider)playable(decode(encode(editor))).HitObjects.Single();
            Assert.That(restored.SliderVelocityMultiplier, Is.EqualTo(2));
            Assert.That(restored.Velocity, Is.EqualTo(0.4));
            Assert.That(restored.Duration, Is.EqualTo(1000));
            // This only protects velocity/history. The legacy format cannot store disabled ticks
            // together with non-default SV; GenerateTicks roundtrip remains unresolved (D01).

            history.RestoreState(-1);
            slider = (Slider)editor.HitObjects.Single();
            Assert.That(slider.SliderVelocityMultiplier, Is.EqualTo(1));
            Assert.That(slider.Duration, Is.EqualTo(2000));
            Assert.That(history.CurrentStateHash, Is.EqualTo(initialHash));
            Assert.That(history.CanRedo.Value, Is.True);

            history.RestoreState(1);
            slider = (Slider)editor.HitObjects.Single();
            Assert.That(slider.SliderVelocityMultiplier, Is.EqualTo(2));
            Assert.That(slider.Duration, Is.EqualTo(1000));
            Assert.That(history.CurrentStateHash, Is.EqualTo(editedHash));
        }

        private static Beatmap decode(string text)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            using var reader = new LineBufferedReader(stream);
            return Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
        }

        private static IBeatmap playable(Beatmap beatmap) => new TestWorkingBeatmap(beatmap).GetPlayableBeatmap(new OsuRuleset().RulesetInfo, Array.Empty<Mod>());

        private static string encode(IBeatmap beatmap)
        {
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            new LegacyBeatmapEncoder(beatmap, null, null).Encode(writer);
            return writer.ToString();
        }
    }
}
