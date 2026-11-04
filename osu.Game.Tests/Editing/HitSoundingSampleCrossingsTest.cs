// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.HitSounding;
using osuTK;

namespace osu.Game.Tests.Editing
{
    [TestFixture]
    public class HitSoundingSampleCrossingsTest
    {
        [Test]
        public void TestForwardCrossingExcludesStartAndIncludesDestination()
        {
            var nodes = createHitCircles(100, 200, 300);
            var result = collect(nodes, 100, 300);

            Assert.That(result.Select(node => node.Time), Is.EqualTo(new[] { 200d, 300d }));
        }

        [Test]
        public void TestBackwardCrossingExcludesStartAndIsDescending()
        {
            var nodes = createHitCircles(100, 200, 300);
            var result = collect(nodes, 300, 100);

            Assert.That(result.Select(node => node.Time), Is.EqualTo(new[] { 200d, 100d }));
        }

        [Test]
        public void TestSimultaneousNodesAreAllCollected()
        {
            var nodes = createHitCircles(200, 200, 200);
            var result = collect(nodes, 100, 200);

            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result.All(node => node.Time == 200), Is.True);
        }

        [Test]
        public void TestBodyNodeIsNeverACrossing()
        {
            Slider slider = createSlider();
            HitSoundingNode[] nodes =
            {
                new(slider, -1),
                new(slider, 0),
                new(slider, 1),
                new(slider, 2),
            };

            nodes = nodes.OrderBy(node => node.Time).ToArray();

            Assert.That(nodes.Count(node => node.IsBody), Is.EqualTo(1));

            var result = collect(nodes, slider.StartTime - 1, slider.EndTime + 1);

            Assert.That(result.Any(node => node.IsBody), Is.False);
            Assert.That(result.Select(node => node.NodeIndex), Is.EquivalentTo(new[] { 0, 1, 2 }));
        }

        [TestCase(100, 100, 200, 100)]
        [TestCase(90, 100, 200, 100)] // overlapping call: the tracked position wins, so the node cannot replay.
        [TestCase(150, 100, 200, 150)] // gap: skipped rather than replayed.
        [TestCase(250, 100, 200, 250)] // a caller move in the opposite direction is a real scrub, not a dedupe.
        [TestCase(110, 100, 50, 100)] // backward overlap.
        [TestCase(80, 100, 50, 80)] // backward gap.
        [TestCase(40, 100, 50, 40)] // a new forward session far behind the previous cursor.
        [TestCase(0, 10000, 500, 0)] // new forward session after an unrelated seek.
        [TestCase(10000, 0, 9000, 10000)] // new backward session after an unrelated seek.
        public void TestPreviewStart(double from, double tracked, double to, double expected)
        {
            Assert.That(HitSoundingSampleCrossings.GetPreviewStart(from, tracked, to), Is.EqualTo(expected));
        }

        [Test]
        public void TestPreviewStartWithNaNCallerPositionUsesTrackedPosition()
        {
            Assert.That(HitSoundingSampleCrossings.GetPreviewStart(double.NaN, 100, 200), Is.EqualTo(100));
        }

        private static HitSoundingNode[] createHitCircles(params double[] times)
            => times.Select(time => new HitSoundingNode(new HitCircle { StartTime = time }, -1)).ToArray();

        private static Slider createSlider()
        {
            var slider = new Slider
            {
                StartTime = 1000,
                RepeatCount = 1,
                Path = new SliderPath(new[]
                {
                    new PathControlPoint(Vector2.Zero),
                    new PathControlPoint(new Vector2(100, 0)),
                }),
            };

            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

            return slider;
        }

        private static List<HitSoundingNode> collect(IReadOnlyList<HitSoundingNode> nodes, double from, double to)
        {
            var result = new List<HitSoundingNode>();
            HitSoundingSampleCrossings.Collect(nodes, from, to, result);
            return result;
        }
    }
}
