// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using NUnit.Framework;
using osu.Framework.Graphics.Lines;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class SliderViewportBoundsTest
    {
        [Test]
        public void TestRangesMatchFullScanIncludingBackwardsSeeks()
        {
            var random = new Random(1234);
            var points = new Vector2[4097];
            for (int i = 0; i < points.Length; i++)
                points[i] = new Vector2(random.Next(-100000, 100000), random.Next(-100000, 100000));
            var bounds = new SliderViewportBounds();
            for (int iteration = 0; iteration < 1000; iteration++)
            {
                int first = random.Next(points.Length + 1);
                int count = random.Next(points.Length - first + 1);
                var range = new PathDrawRange(first, count, new Vector2(-200000, 0), new Vector2(0, 200000));
                Vector2 minimum = Vector2.ComponentMin(range.StartPoint, range.EndPoint);
                Vector2 maximum = Vector2.ComponentMax(range.StartPoint, range.EndPoint);
                for (int i = first; i < first + count; i++)
                {
                    minimum = Vector2.ComponentMin(minimum, points[i]);
                    maximum = Vector2.ComponentMax(maximum, points[i]);
                }
                Assert.That(bounds.Query(points, 0, range), Is.EqualTo((minimum, maximum)));
            }
        }

        [Test]
        public void TestMutationInvalidatesBlocks()
        {
            var points = new Vector2[256];
            var bounds = new SliderViewportBounds();
            var range = new PathDrawRange(0, points.Length, Vector2.Zero, Vector2.Zero);
            Assert.That(bounds.Query(points, 0, range).Maximum, Is.EqualTo(Vector2.Zero));
            points[128] = new Vector2(100);
            Assert.That(bounds.Query(points, 1, range).Maximum, Is.EqualTo(new Vector2(100)));
            Assert.That(bounds.Query(new Vector2[256], 1, range).Maximum, Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void TestRepeatedQueriesDoNotAllocate()
        {
            var points = new Vector2[350000];
            var bounds = new SliderViewportBounds();
            var range = new PathDrawRange(0, points.Length, Vector2.Zero, Vector2.One);
            bounds.Query(points, 0, range);
            long started = Stopwatch.GetTimestamp();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
                bounds.Query(points, 0, range with { FirstVertex = i, VertexCount = points.Length - i });
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Assert.That(allocated, Is.Zero);
            TestContext.WriteLine($"1,000 changing range queries over 350,000 points: {elapsed:F2} ms; allocated {allocated} bytes.");
        }
    }
}
