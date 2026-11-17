// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// Headless timing of the stable (oversized) slider body path: the per-snaking-frame geometry update on the
    /// update thread and the full mesh emission performed by each framebuffer redraw on the draw thread.
    /// </summary>
    [TestFixture]
    [Explicit("Benchmark")]
    public class LegacySliderBodyBenchmark
    {
        private const float ratio = 2.25f;
        private const float field_radius = 32;
        private const int snaking_in_frames = 120;
        private const int snaking_out_frames = 480;

        [TestCase(200_000, false)]
        [TestCase(30_000, true)]
        public void TestSnaking(int edgeCount, bool straight)
        {
            var edges = createEdges(edgeCount, straight);
            float radius = field_radius * ratio;
            Vector2 offset = new Vector2(256, 84);

            var frames = new List<(double start, double end)>();
            for (int i = 1; i <= snaking_in_frames; i++)
                frames.Add((0, i / (double)snaking_in_frames));
            for (int i = 1; i <= snaking_out_frames; i++)
                frames.Add((i / (double)snaking_out_frames, 1));

            // Warm up both phases once.
            var geometry = new Geometry(edges);
            geometry.Update(0, 0.5, offset, ratio, radius);
            emit(geometry.Lines, geometry.Count, radius, geometry.Viewport);

            int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
            long updateBytes = 0, drawBytes = 0;
            var update = new Stopwatch();
            var draw = new Stopwatch();
            ulong hash = 14695981039346656037;
            long vertices = 0;

            foreach (var (start, end) in frames)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                update.Start();
                geometry.Update(start, end, offset, ratio, radius);
                update.Stop();
                long middle = GC.GetAllocatedBytesForCurrentThread();
                draw.Start();
                var (frameHash, frameVertices) = emit(geometry.Lines, geometry.Count, radius, geometry.Viewport);
                draw.Stop();
                drawBytes += GC.GetAllocatedBytesForCurrentThread() - middle;
                updateBytes += middle - before;
                hash = (hash ^ frameHash) * 1099511628211;
                vertices += frameVertices;
            }

            string result = $"edges={edgeCount} straight={straight} frames={frames.Count}: "
                            + $"update {update.Elapsed.TotalMilliseconds / frames.Count:F3} ms/frame, {updateBytes / frames.Count / 1024.0:F1} KiB/frame; "
                            + $"draw-emit {draw.Elapsed.TotalMilliseconds / frames.Count:F3} ms/frame, {drawBytes / frames.Count / 1024.0:F1} KiB/frame; "
                            + $"GC gen0/1/2 {GC.CollectionCount(0) - gen0}/{GC.CollectionCount(1) - gen1}/{GC.CollectionCount(2) - gen2}; "
                            + $"vertices {vertices}, hash {hash:X16}";
            TestContext.Out.WriteLine(result);
            Console.WriteLine(result);
        }

        private static List<LegacySliderBallPath.Edge> createEdges(int count, bool straight)
        {
            var edges = new List<LegacySliderBallPath.Edge>(count);
            Vector2 previous = new Vector2(10000, 10000);

            for (int i = 1; i <= count; i++)
            {
                Vector2 next;

                if (straight)
                {
                    // Long, independently placed lines (every one is its own stable line).
                    next = new Vector2(10000 + (i % 2 == 0 ? 0 : 400), 10000 + i * 7);
                }
                else
                {
                    // A long, tight spiral of short curve edges, as produced by bezier/arc approximation.
                    double t = i * 0.01;
                    double r = 2000 + i * 0.05;
                    next = new Vector2(10000 + (float)(Math.Cos(t) * r), 10000 + (float)(Math.Sin(t) * r));
                }

                edges.Add(new LegacySliderBallPath.Edge(previous, next, straight, straight || i % 997 == 0));
                previous = next;
            }

            return edges;
        }

        private static (ulong hash, long count) emit(LegacySliderBodyMesh.Edge[] lines, int count, float radius, LegacySliderBodyMesh.Viewport viewport)
        {
            var projection = new LegacySliderBodyMesh.WindowProjection(viewport, new Vector2(32768));
            const float window_height = 1080;

            // Mirrors BodyDrawNode.Draw.
            var span = new ReadOnlySpan<LegacySliderBodyMesh.Edge>(lines, 0, count);
            if (frames.Length < count)
                frames = new LegacySliderBodyMesh.LineFrame[count + count / 2];
            var frameSpan = frames.AsSpan(0, count);
            LegacySliderBodyMesh.ComputeFrames(span, frameSpan);
            var sink = new HashSink(new LegacySliderBodyRenderer.WindowSink(null, projection, window_height));
            LegacySliderBodyMesh.EmitQuads(span, frameSpan, radius, ref sink);
            LegacySliderBodyMesh.EmitCaps(span, frameSpan, radius, ref sink);
            return (sink.Hash, sink.Vertices);
        }

        private static LegacySliderBodyMesh.LineFrame[] frames = Array.Empty<LegacySliderBodyMesh.LineFrame>();

        private struct HashSink : LegacySliderBodyMesh.IVertexSink
        {
            private readonly LegacySliderBodyRenderer.WindowSink window;
            public ulong Hash;
            public long Vertices;

            public HashSink(LegacySliderBodyRenderer.WindowSink window)
            {
                this.window = window;
                Hash = 14695981039346656037;
                Vertices = 0;
            }

            public TexturedVertex3D Transform(TexturedVertex3D vertex) => window.Transform(vertex);

            public void Add(in TexturedVertex3D vertex)
            {
                Hash = (Hash ^ (uint)BitConverter.SingleToInt32Bits(vertex.Position.X)) * 1099511628211;
                Hash = (Hash ^ (uint)BitConverter.SingleToInt32Bits(vertex.Position.Y)) * 1099511628211;
                Hash = (Hash ^ (uint)BitConverter.SingleToInt32Bits(vertex.Position.Z)) * 1099511628211;
                Hash = (Hash ^ (uint)BitConverter.SingleToInt32Bits(vertex.TexturePosition.X)) * 1099511628211;
                Vertices++;
            }
        }

        /// <summary>
        /// Mirrors the update-thread work in <c>LegacySliderBodyRenderer.Configure</c> for a snaking change.
        /// </summary>
        private class Geometry
        {
            private readonly LegacySliderBodyMesh.LineCache cache = new LegacySliderBodyMesh.LineCache();

            public LegacySliderBodyMesh.Edge[] Lines { get; private set; } = Array.Empty<LegacySliderBodyMesh.Edge>();
            public int Count { get; private set; }
            public LegacySliderBodyMesh.Viewport Viewport { get; private set; }

            public Geometry(List<LegacySliderBallPath.Edge> edges)
            {
                cache.SetSource(edges);
            }

            public void Update(double start, double end, Vector2 offset, float ratio, float radius)
            {
                Viewport = cache.Prepare(start, end, offset, ratio, radius, null);

                // Steady state of the pooled snapshot: a copy into a recycled array.
                if (Lines.Length < cache.Lines.Count)
                    Lines = new LegacySliderBodyMesh.Edge[cache.Lines.Count + cache.Lines.Count / 2];
                CollectionsMarshal.AsSpan(cache.Lines).CopyTo(Lines);
                Count = cache.Lines.Count;
            }
        }
    }
}
