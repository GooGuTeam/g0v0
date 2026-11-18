// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osuTK;
using Edge = osu.Game.Rulesets.Osu.Skinning.Legacy.LegacySliderBodyMesh.Edge;
using Viewport = osu.Game.Rulesets.Osu.Skinning.Legacy.LegacySliderBodyMesh.Viewport;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// The incremental line cache must reproduce stable's per-frame rebuild exactly.
    /// </summary>
    [TestFixture]
    public class LegacySliderBodyLineCacheTest
    {
        [TestCase(false, null)]
        [TestCase(true, null)]
        [TestCase(false, 3000.0)]
        public void TestMatchesFullRebuild(bool straight, double? expectedLength)
        {
            var random = new Random(1234);
            var edges = new List<LegacySliderBallPath.Edge>();
            Vector2 previous = new Vector2(500, 300);

            for (int i = 0; i < 2000; i++)
            {
                Vector2 next = straight
                    ? previous + new Vector2(random.Next(-60, 61), random.Next(-60, 61))
                    : previous + new Vector2((float)Math.Cos(i * 0.05) * 1.7f, (float)Math.Sin(i * 0.05) * 1.7f);
                // Occasional gaps exercise independent line endpoints.
                Vector2 from = i % 211 == 0 ? previous + new Vector2(0.25f, 0) : previous;
                edges.Add(new LegacySliderBallPath.Edge(from, next, straight, straight || i % 97 == 0));
                previous = next;
            }

            var cache = new LegacySliderBodyMesh.LineCache();
            cache.SetSource(edges);
            var expected = new List<Edge>();
            var offsets = new[] { new Vector2(256, 84), new Vector2(256, 84), new Vector2(250, 90) };

            for (int frame = 0; frame < 900; frame++)
            {
                double start, end;

                if (frame < 300)
                    (start, end) = (0, frame / 299.0);
                else if (frame < 600)
                    (start, end) = ((frame - 300) / 299.0, 1);
                else
                {
                    // Unordered seeks, including shrinking ranges and repeated values.
                    start = random.NextDouble() < 0.5 ? 0 : random.NextDouble() * 0.5;
                    end = random.NextDouble() < 0.2 ? 1 : start + random.NextDouble() * (1 - start);
                }

                Vector2 offset = offsets[frame / 300];
                Viewport actual = cache.Prepare(start, end, offset, 2.25f, 72, expectedLength);
                Viewport reference = referencePrepare(edges, start, end, Vector2.Zero, offset, 2.25f, 72, expectedLength, expected);

                Assert.That(actual, Is.EqualTo(reference), $"viewport at frame {frame}");
                Assert.That(actual.OrthoLeft, Is.EqualTo(reference.OrthoLeft));
                Assert.That(actual.OrthoRight, Is.EqualTo(reference.OrthoRight));
                Assert.That(actual.OrthoBottom, Is.EqualTo(reference.OrthoBottom));
                Assert.That(actual.OrthoTop, Is.EqualTo(reference.OrthoTop));
                Assert.That(cache.Lines, Is.EqualTo(expected), $"lines at frame {frame}");
            }
        }

        [TestCase(1.5e9f)]
        [TestCase(-1.5e9f)]
        public void TestExtremeCoordinatesDoNotOverflowIntBounds(float baseCoord)
        {
            var edges = new List<LegacySliderBallPath.Edge>
            {
                new LegacySliderBallPath.Edge(new Vector2(baseCoord, baseCoord), new Vector2(baseCoord + 50, baseCoord + 50), true, true),
            };

            var cache = new LegacySliderBodyMesh.LineCache();
            cache.SetSource(edges);

            // Multiplied by ratio = 2, min * ratio = +/-3e9 exceeds Int32 range even though max - min is only 100px.
            Viewport viewport = cache.Prepare(0, 1, Vector2.Zero, 2f, 64f, null);
            Assert.That(viewport, Is.EqualTo(default(Viewport)));
            Assert.That(cache.Lines, Is.Empty);

            var legacyViewport = LegacySliderViewport.FromBounds(new Vector2(baseCoord * 2, baseCoord * 2), new Vector2(baseCoord * 2 + 100, baseCoord * 2 + 100), 64f);
            Assert.That(legacyViewport.Origin, Is.EqualTo(Vector2.Zero));
            Assert.That(legacyViewport.Size, Is.EqualTo(Vector2.Zero));
        }

        /// <summary>
        /// The stateless implementation that preceded <see cref="LegacySliderBodyMesh.LineCache"/>.
        /// </summary>
        private static Viewport referencePrepare(IReadOnlyList<LegacySliderBallPath.Edge> source, double progressStart, double progressEnd,
                                                       Vector2 origin, Vector2 offset, float ratio, float radius, double? expectedLength, List<Edge> lines)
        {
            lines.Clear();
            if (!(ratio > 0) || !float.IsFinite(ratio) || !(radius > 0) || !float.IsFinite(radius)
                || !double.IsFinite(progressStart) || !double.IsFinite(progressEnd))
                return default;

            progressStart = Math.Clamp(progressStart, 0, 1);
            progressEnd = Math.Clamp(progressEnd, progressStart, 1);
            double length = 0;
            foreach (var edge in source)
                length += (edge.To - edge.From).Length;
            double spatialLength = expectedLength is > 0 ? expectedLength.Value : length;
            double begin = spatialLength * progressStart;
            double finish = progressEnd >= 1 ? length : spatialLength * progressEnd;
            double cumulative = 0;
            int first = 0, last = source.Count - 1;
            double[] cumulativeLengths = new double[source.Count];

            for (int i = 0; i < source.Count; i++)
            {
                cumulative += (source[i].To - source[i].From).Length;
                cumulativeLengths[i] = cumulative;
                if (cumulative < begin) first = i + 1;
                if (cumulative > finish && last == source.Count - 1) last = i;
            }

            bool pending = false;
            int mergeStart = first;

            for (int i = first; i <= last && i < source.Count; i++)
            {
                if (!pending) mergeStart = i;

                if ((source[i].To - source[mergeStart].From).Length > (source[i].Straight ? 32 : 6) || i == last || source[i].ForceEnd || i == last - 1)
                {
                    Vector2 from = source[mergeStart].From, to = source[i].To;

                    if (mergeStart == first && begin > 0)
                    {
                        double previous = first == 0 ? 0 : cumulativeLengths[first - 1];
                        from += (source[first].To - from).Normalized() * (float)(begin - previous);
                    }

                    if (i == last && progressEnd < 1)
                    {
                        float remainder = (float)finish - (i == 0 ? 0 : (float)cumulativeLengths[i - 1]);
                        if (remainder > 0) to = from + (to - from).Normalized() * remainder;
                    }

                    lines.Add(new Edge(display(from), display(to)));
                    pending = false;
                }
                else pending = true;
            }

            if (lines.Count == 0) return default;

            Vector2 min = lines[0].From, max = min;

            foreach (var edge in lines)
            {
                min = Vector2.ComponentMin(min, edge.To);
                max = Vector2.ComponentMax(max, edge.To);
            }

            if (!float.IsFinite(min.X) || !float.IsFinite(min.Y) || !float.IsFinite(max.X) || !float.IsFinite(max.Y)
                || (double)max.X - min.X + radius * 2.3 > int.MaxValue
                || (double)max.Y - min.Y + radius * 2.3 > int.MaxValue)
            {
                lines.Clear();
                return default;
            }

            int x = (int)(min.X - radius * 1.15);
            int y = (int)(min.Y - radius * 1.15);
            // Stable subtracts the bounds as floats before widening.
            int width = (int)((double)(max.X - min.X) + radius * 2.3);
            int height = (int)((double)(max.Y - min.Y) + radius * 2.3);

            return new Viewport(new Vector2(Math.Max(x, 0), Math.Max(y, 0)), new Vector2(width, (float)(height - Math.Max(-(double)y, 0))))
            {
                // Clipping a negative left edge shifts the projection; clipping the top shortens it.
                OrthoLeft = Math.Max(x, 0),
                OrthoRight = (int)Math.Clamp((long)Math.Max(x, 0) + width, int.MinValue, int.MaxValue),
                OrthoBottom = Math.Max(y, 0),
                OrthoTop = (int)Math.Clamp((long)y + height, int.MinValue, int.MaxValue),
            };

            Vector2 display(Vector2 point) => point * ratio + offset;
        }
    }
}
