// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NUnit.Framework;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class LegacySliderBallPositionTest
    {
        [TestCase(257174, 201460016.6074524, 257430, -63f, 40f)]
        [TestCase(206640, 201459915.8592682, 206771, 216f, 104f)]
        [TestCase(30873, 201459911.4829864, 30969, -52f, 48f)]
        [TestCase(129174, 201460136.25486755, 129233, 175f, 112f)]
        [TestCase(96176, 3.1923817218740043, 96242, 291.87347f, 161.28427f)]
        public void TestFmSynthesisSourceSamples(int start, double velocity, int clock, float expectedX, float expectedY)
        {
            var decoded = AspireRuntimeBeatmaps.Decode(ReadFmSynthesisObject(start));
            var original = (IHasPath)decoded.HitObjects.Single();
            Vector2 origin = ((IHasPosition)original).Position;
            var slider = new Slider { Position = origin, Path = original.Path };
            Assert.That(slider.Path.LegacyPath, Is.SameAs(original.Path.LegacyPath).And.Not.Null);
            var sampler = new LegacySliderBallPosition();
            Assert.That(sampler.TryGetPosition(slider.Path, start, velocity, original is IHasRepeats repeats ? repeats.SpanCount() : 1, clock, out var relative, origin), Is.True);
            Assert.That((relative + origin - new Vector2(expectedX, expectedY)).Length, Is.LessThan(0.001f));
        }

        [Test]
        public void TestOriginalPointsInvalidateOnlyWhenEdited()
        {
            var decoded = AspireRuntimeBeatmaps.Decode(ReadFmSynthesisObject(206640));
            var path = ((IHasPath)decoded.HitObjects.Single()).Path;
            LegacySliderPath? original = path.LegacyPath;
            Assert.That(original, Is.Not.Null);
            path.ExpectedDistance.Value = 100;
            Assert.That(path.LegacyPath, Is.SameAs(original));
            path.OptimiseCatmull = true;
            Assert.That(path.LegacyPath, Is.SameAs(original));
            path.ControlPoints[1].Position += Vector2.One;
            Assert.That(path.LegacyPath, Is.Null);
        }

        internal static string ReadFmSynthesisObject(int start)
        {
            using var stream = typeof(LegacySliderBallPositionTest).Assembly.GetManifestResourceStream($"osu.Game.Rulesets.Osu.Tests.Resources.Testing.Beatmaps.SliderBall.{start}.txt")!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        [TestCase(1000, 0.1, 1)]
        [TestCase(1000, 100 / 0.3, 20)]
        [TestCase(-3, 100 / 0.3, 20)]
        [TestCase(1023.9, 100 / 0.3, 30)]
        [TestCase(-1025, 100 / 0.3, 30)]
        [TestCase(16777215, 100 / 0.3, 30)]
        public void TestMatchesMovementReference(double start, double velocity, int spans)
        {
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0), new Vector2(100, 100) });
            compare(path, start, velocity, spans, new Random(729));
        }

        [Test]
        public void TestCollapsedMovementAndIntegerClock()
        {
            var sampler = new LegacySliderBallPosition();
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) });
            Assert.That(sampler.TryGetPosition(path, 1000, 100 / 0.3, 20, 1000.3, out var first), Is.True);
            Assert.That(first, Is.EqualTo(Vector2.Zero));
            Assert.That(sampler.TryGetPosition(path, 1000, 100 / 0.3, 20, 1001, out var second), Is.True);
            Assert.That(second, Is.EqualTo(Vector2.Zero));
            // Explicitly seek backwards through the same collapsed animation interval.
            Assert.That(sampler.TryGetPosition(path, 1000, 100 / 0.3, 20, 1000.3, out var rewound), Is.True);
            Assert.That(rewound, Is.EqualTo(first));
        }

        [Test]
        public void TestClockUsesRoundToEven()
        {
            var sampler = new LegacySliderBallPosition();
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) });
            sampler.TryGetPosition(path, 1000, 1, 1, 1000.5, out var even);
            sampler.TryGetPosition(path, 1000, 1, 1, 1001.5, out var odd);
            Assert.That(even.X, Is.Zero);
            Assert.That(odd.X, Is.EqualTo(2));
        }

        [Test]
        public void TestRandomPathsAndSeeks()
        {
            var random = new Random(211);
            for (int test = 0; test < 100; test++)
            {
                var points = Enumerable.Range(0, random.Next(2, 10)).Select(_ => new Vector2(random.Next(-200, 201), random.Next(-200, 201))).ToArray();
                var path = new SliderPath(PathType.LINEAR, points);
                double start = random.Next(-2048, 2048) + random.NextDouble();
                double velocity = Math.Pow(10, random.NextDouble() * 6 - 2);
                compare(path, start, velocity, random.Next(1, 300), random);
            }
        }

        [TestCase(0, 333.3333333333333)]
        [TestCase(-4097, 333.3333333333333)]
        [TestCase(1048575, 0.0001)]
        public void TestAcceleratedSpanStartsMatchCumulativeAddition(double start, double velocity)
        {
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(0.125f, 0), new Vector2(0.125f, 17.25f), new Vector2(50, 17.25f) });
            compare(path, start, velocity, 2000, new Random(63));
        }

        [Test]
        public void TestCurvesAndPathChanges()
        {
            var random = new Random(211);
            var path = new SliderPath(PathType.BEZIER, new[] { Vector2.Zero, new Vector2(100, 200), new Vector2(200, 0) });
            var geometry = path.CalculatedPath.ToArray();
            double distance = path.Distance;
            compare(path, 1000, 500, 40, random);
            Assert.That(path.CalculatedPath, Is.EqualTo(geometry));
            Assert.That(path.Distance, Is.EqualTo(distance));

            var sampler = new LegacySliderBallPosition();
            sampler.TryGetPosition(path, 1000, 500, 40, 1001, out _);
            path.ControlPoints.Clear();
            path.ControlPoints.AddRange(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(100, 0)) });
            Assert.That(sampler.TryGetPosition(path, 1000, 1, 1, 1050, out var edited), Is.True);
            Assert.That(edited.X, Is.EqualTo(50));
            Assert.That(sampler.TryGetPosition(path, 1020, 1, 1, 1050, out var retimed), Is.True);
            Assert.That(retimed.X, Is.EqualTo(30));
            Assert.That(sampler.TryGetPosition(path, 1000, 2, 2, 1075, out var reversed), Is.True);
            Assert.That(reversed.X, Is.EqualTo(50));
        }

        [Test]
        [CancelAfter(5000)]
        public void TestLargeRepeatCountDoesNotExpandMovements()
        {
            var sampler = new LegacySliderBallPosition();
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) });
            // One hundred million spans would require billions of bytes as Movement objects.
            Assert.That(sampler.TryGetPosition(path, 1000, 100 / 0.3, 100_000_000, 20_000_000, out var position), Is.True);
            Assert.That(float.IsFinite(position.X) && float.IsFinite(position.Y), Is.True);
            sampler.TryGetPosition(path, 1000, 100 / 0.3, 100_000_000, 1001, out var rewound);
            Assert.That(rewound, Is.EqualTo(Vector2.Zero));
        }

        [TestCase(0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1)]
        public void TestInvalidVelocityFallsBack(double velocity)
        {
            var sampler = new LegacySliderBallPosition();
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) });
            Assert.That(sampler.TryGetPosition(path, 1000, velocity, 1, 1001, out _), Is.False);
        }

        [Test]
        public void TestOverflowAndEmptyPathsFallBack()
        {
            var sampler = new LegacySliderBallPosition();
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) });
            Assert.That(sampler.TryGetPosition(path, int.MaxValue - 10, 1, 1, int.MaxValue, out _), Is.False);
            Assert.That(sampler.TryGetPosition(new SliderPath(), 0, 1, 1, 0, out _), Is.False);
            Assert.That(sampler.TryGetPosition(path, 0, 1, 1, double.NaN, out _), Is.False);
            Assert.That(sampler.TryGetPosition(path, 0, 1, 1, double.PositiveInfinity, out _), Is.False);
        }

        private static void compare(SliderPath path, double start, double velocity, int spans, Random random)
        {
            List<Movement> movements = buildReference(path.CalculatedPath, start, velocity, spans);
            var sampler = new LegacySliderBallPosition();
            var times = new List<double> { start - 1, start, start + 0.5, start + 1, movements[^1].End + 1 };
            foreach (var movement in movements.Where((_, i) => i % Math.Max(1, movements.Count / 100) == 0))
            {
                times.Add(movement.Start);
                times.Add(movement.End);
            }
            for (int i = 0; i < 200; i++)
                times.Add(Math.Truncate(start) + random.NextDouble() * (movements[^1].End - Math.Truncate(start)));

            // Non-monotonic queries exercise cache reuse, forward seek, backward seek and ties.
            foreach (double time in times.OrderBy(_ => random.Next()))
            {
                Assert.That(sampler.TryGetPosition(path, start, velocity, spans, time, out var actual), Is.True);
                Assert.That(actual, Is.EqualTo(referencePosition(movements, (int)Math.Round(time))),
                    $"start={start:R} speed={velocity:R} spans={spans} time={time:R}");
            }
        }

        private static List<Movement> buildReference(IReadOnlyList<Vector2> points, double start, double speed, int spans)
        {
            var ends = points.ToArray();
            // Even with no length change, stable normalises and reconstructs its final
            // line endpoint. This can differ by one float ULP from the original vertex.
            if (ends.Length > 1 && ends[^1] != ends[^2])
            {
                Vector2 direction = ends[^1] - ends[^2];
                ends[^1] = ends[^2] + direction.Normalized() * direction.Length;
            }
            points = ends;
            double[] lengths = new double[points.Count - 1];
            double cumulative = 0;
            for (int i = 0; i < lengths.Length; i++)
            {
                double previous = cumulative;
                cumulative += (points[i + 1] - points[i]).Length;
                lengths[i] = (float)(cumulative - previous);
            }

            var movements = new List<Movement>();
            double time = Math.Truncate(start);
            for (int span = 0; span < spans; span++)
            {
                bool reverse = span % 2 == 1;
                for (int i = 0; i < lengths.Length; i++)
                {
                    int edge = reverse ? lengths.Length - 1 - i : i;
                    double delta = (double)(1000f * (float)lengths[edge]) / (speed * 1000);
                    movements.Add(new Movement(points[reverse ? edge + 1 : edge], points[reverse ? edge : edge + 1], (int)time, (int)(time + delta)));
                    time += delta;
                }
            }
            return movements;
        }

        private static Vector2 referencePosition(List<Movement> movements, int clock)
        {
            // Direct movement-only transcription of pSprite.UpdateTransformations and
            // OsuMathHelper.TweenValues(Vector2). Deliberately use a linear list scan here.
            foreach (var movement in movements)
            {
                if (movement.Start > clock || movement.End < clock)
                    continue;
                float elapsed = (float)((double)clock - movement.Start);
                float duration = (float)((double)(float)movement.End - movement.Start);
                if (duration == 0 || elapsed == 0)
                    return movement.From;
                return new Vector2(
                    (float)((movement.To.X - movement.From.X) * ((double)elapsed / duration) + movement.From.X),
                    (float)((movement.To.Y - movement.From.Y) * ((double)elapsed / duration) + movement.From.Y));
            }
            for (int i = movements.Count - 1; i >= 0; i--)
                if (movements[i].End < clock)
                    return movements[i].To;
            return movements.First(m => m.Start >= clock).From;
        }

        private readonly record struct Movement(Vector2 From, Vector2 To, int Start, int End);
    }
}
