// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class LegacySliderBodyMeshTest
    {
        [Test]
        public void TestStraightLinesKeepIndependentEndpointsAndFlags()
        {
            var edges = new[]
            {
                new LegacySliderBallPath.Edge(new Vector2(1, 1), new Vector2(2, 1), true, true),
                new LegacySliderBallPath.Edge(new Vector2(3, 1), new Vector2(4, 1), true, true)
            };
            var lines = new List<LegacySliderBodyMesh.Edge>();
            LegacySliderBodyMesh.Prepare(edges, 0, 1, Vector2.Zero, new Vector2(256, 84), 1.5f, 30, null, lines);
            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[0].To, Is.EqualTo(new Vector2(259, 85.5f)));
            Assert.That(lines[1].From, Is.EqualTo(new Vector2(260.5f, 85.5f)));
        }

        [Test]
        public void TestCurveReductionAndForcedEnd()
        {
            var edges = new List<LegacySliderBallPath.Edge>();
            for (int i = 0; i < 10; i++)
                edges.Add(new LegacySliderBallPath.Edge(new Vector2(i, 0), new Vector2(i + 1, 0), false, i == 3));

            var lines = new List<LegacySliderBodyMesh.Edge>();
            LegacySliderBodyMesh.Prepare(edges, 0, 1, Vector2.Zero, Vector2.Zero, 1, 20, null, lines);
            Assert.That(lines[0].To.X, Is.EqualTo(4));
            Assert.That(lines[^1].To.X, Is.EqualTo(10));
            Assert.That(lines.Count, Is.LessThan(edges.Count));
        }

        [Test]
        public void TestHugeViewportDoesNotRequireDrawableScale()
        {
            var edges = new[] { new LegacySliderBallPath.Edge(Vector2.Zero, new Vector2(0, 100000000), true, true) };
            var lines = new List<LegacySliderBodyMesh.Edge>();
            LegacySliderBodyMesh.Viewport viewport = LegacySliderBodyMesh.Prepare(edges, 0, 1, Vector2.Zero, new Vector2(256, 84), 1.5f, 30, null, lines);
            Assert.That(viewport.RequestedSize.Y, Is.GreaterThan(100000000));
            Assert.That(lines[0].To.Y, Is.EqualTo(150000080f));

            var quads = new List<TexturedVertex3D>();
            var caps = new List<TexturedVertex3D>();
            LegacySliderBodyMesh.Build(lines, 30, quads, caps);
            Assert.That(quads.Count, Is.EqualTo(8));
            Assert.That(caps.Count, Is.EqualTo(144));
            foreach (var vertex in quads)
                Assert.That(float.IsFinite(vertex.Position.X) && float.IsFinite(vertex.Position.Y), Is.True);
        }

        [Test]
        [CancelAfter(1000)]
        public void TestHostileVisualTickCountIsBounded()
        {
            var edges = new[] { new LegacySliderBallPath.Edge(Vector2.Zero, new Vector2(100, 0)) };
            var ticks = new List<int>();
            Assert.That(LegacySliderBallTiming.Build(edges, 0, 100, int.MaxValue, 1, 0, 100, ticks), Is.False);
            Assert.That(LegacySliderBallTiming.Build(edges, 0, 100, int.MaxValue, 1, 100, 100, ticks), Is.False);
        }
    }
}
