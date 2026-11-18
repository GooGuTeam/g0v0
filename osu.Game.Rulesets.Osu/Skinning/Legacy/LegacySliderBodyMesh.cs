// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Legacy
{
    /// <summary>
    ///     Stable's screen-space ridge mesh. No inverse drawable transform or epsilon-clamped
    ///     scale participates in projection, even for paths spanning hundreds of millions of pixels.
    /// </summary>
    internal static class LegacySliderBodyMesh
    {
        internal readonly record struct Edge(Vector2 From, Vector2 To);

        internal readonly record struct Viewport(Vector2 Origin, Vector2 RequestedSize)
        {
            /// <summary>
            /// The integer rectangle stable hands to <c>ResetOrtho</c>: left, right, and the two vertical
            /// extents named after their place in the (vertically flipped) GL window.
            /// </summary>
            public int OrthoLeft { get; init; }

            public int OrthoRight { get; init; }

            public int OrthoBottom { get; init; }

            public int OrthoTop { get; init; }
        }

        /// <summary>
        /// Reproduces what stable's vertex shader and the driver's viewport transform do to a display-space
        /// vertex, in 32-bit floats. Aspire sliders place sub-pixel lines exactly on pixel centres at this
        /// precision, so a more accurate projection changes which pixels they cover.
        /// </summary>
        internal readonly struct WindowProjection
        {
            private const double subpixels = 256;

            private readonly float m11, m41, m22, m42;
            private readonly double halfWidth, halfHeight;

            public WindowProjection(Viewport viewport, Vector2 limit)
            {
                // Matrix4.CreateOrthographicOffCenter, with stable's integer bounds converted to floats.
                float left = viewport.OrthoLeft, right = viewport.OrthoRight, bottom = viewport.OrthoBottom, top = viewport.OrthoTop;
                float inverseWidth = 1f / (right - left);
                float inverseHeight = 1f / (top - bottom);

                m11 = 2f * inverseWidth;
                m41 = -(right + left) * inverseWidth;
                m22 = 2f * inverseHeight;
                m42 = -(top + bottom) * inverseHeight;

                // glViewport silently clamps to the driver's maximum dimensions.
                halfWidth = Math.Min((double)viewport.OrthoRight - viewport.OrthoLeft, limit.X) / 2;
                halfHeight = Math.Min((double)viewport.OrthoTop - viewport.OrthoBottom, limit.Y) / 2;
            }

            /// <summary>
            /// Window coordinates in pixels, snapped to the rasteriser's sub-pixel grid. Y increases with
            /// display Y; stable's window has it pointing up, so its image of the body is upside down.
            /// </summary>
            public Vector2 ToWindow(Vector3 position)
            {
                float clipX = position.X * m11;
                clipX += m41;
                float clipY = position.Y * m22;
                clipY += m42;

                return new Vector2(snap((clipX + 1.0) * halfWidth), snap((clipY + 1.0) * halfHeight));
            }

            private static float snap(double value) => (float)(Math.Round(value * subpixels, MidpointRounding.ToEven) / subpixels);
        }

        public static Viewport Prepare(IReadOnlyList<LegacySliderBallPath.Edge> source, double progressStart, double progressEnd,
                                       Vector2 origin, Vector2 offset, float ratio, float radius, double? expectedLength, List<Edge> lines)
        {
            var prepared = new LineCache();
            prepared.SetSource(source);
            Viewport viewport = prepared.Prepare(progressStart, progressEnd, offset, ratio, radius, expectedLength);
            lines.Clear();
            lines.AddRange(prepared.Lines);
            return viewport;
        }

        /// <summary>
        /// Stable's merged line list for one ball path, kept between snaking frames.
        /// </summary>
        /// <remarks>
        /// Stable rebuilds this list from scratch for every frame. The result only depends on the first visible edge,
        /// so while that is unchanged (snaking in) every merged line that ends before the last two visible edges is
        /// reused verbatim and merging resumes after it. Cumulative edge lengths are computed once per path.
        /// </remarks>
        internal sealed class LineCache
        {
            private readonly List<Edge> lines = new List<Edge>();

            // The last source edge of each line, and the display bounds of all lines up to and including it.
            private readonly List<int> lineEnds = new List<int>();
            private readonly List<Vector2> boundsMin = new List<Vector2>();
            private readonly List<Vector2> boundsMax = new List<Vector2>();

            private IReadOnlyList<LegacySliderBallPath.Edge> source = Array.Empty<LegacySliderBallPath.Edge>();
            private double[] cumulativeLengths = Array.Empty<double>();
            private double length;

            private bool reusable;
            private int previousFirst, previousLast;
            private double previousBegin;
            private Vector2 previousOffset;
            private float previousRatio;

            public List<Edge> Lines => lines;

            /// <summary>
            /// Replaces the ball path. Must be called whenever the contents of <paramref name="edges"/> change.
            /// </summary>
            public void SetSource(IReadOnlyList<LegacySliderBallPath.Edge> edges)
            {
                source = edges;
                if (cumulativeLengths.Length < edges.Count)
                    cumulativeLengths = new double[edges.Count];

                double cumulative = 0;

                for (int i = 0; i < edges.Count; i++)
                {
                    cumulative += (edges[i].To - edges[i].From).Length;
                    cumulativeLengths[i] = cumulative;
                }

                length = cumulative;
                reset();
            }

            public Viewport Prepare(double progressStart, double progressEnd, Vector2 offset, float ratio, float radius, double? expectedLength)
            {
                if (!(ratio > 0) || !float.IsFinite(ratio) || !(radius > 0) || !float.IsFinite(radius)
                    || !double.IsFinite(progressStart) || !double.IsFinite(progressEnd))
                {
                    reset();
                    return default;
                }

                progressStart = Math.Clamp(progressStart, 0, 1);
                progressEnd = Math.Clamp(progressEnd, progressStart, 1);
                double spatialLength = expectedLength is > 0 ? expectedLength.Value : length;
                double begin = spatialLength * progressStart;
                double finish = progressEnd >= 1 ? length : spatialLength * progressEnd;
                int count = source.Count;
                int first, last;

                if (double.IsNaN(length))
                    findRangeLinear(begin, finish, out first, out last);
                else
                {
                    // Cumulative lengths never decrease, so the linear scans reduce to binary searches.
                    first = 0;
                    int high = count;

                    while (first < high)
                    {
                        int mid = first + (high - first) / 2;
                        if (cumulativeLengths[mid] < begin) first = mid + 1;
                        else high = mid;
                    }

                    last = first;
                    high = count;

                    while (last < high)
                    {
                        int mid = last + (high - last) / 2;
                        if (cumulativeLengths[mid] > finish) high = mid;
                        else last = mid + 1;
                    }

                    if (last == count) last = count - 1;
                }

                int resume = first;

                if (reusable && first == previousFirst && begin.Equals(previousBegin) && ratio == previousRatio && offset == previousOffset)
                {
                    // Lines ending at least two edges before both the old and new last edge did not see either end.
                    int stableEnd = Math.Min(previousLast, last) - 2;
                    int keep = lines.Count;
                    while (keep > 0 && lineEnds[keep - 1] > stableEnd) keep--;
                    truncate(keep);
                    if (keep > 0) resume = lineEnds[keep - 1] + 1;
                }
                else
                    truncate(0);

                bool pending = false;
                int mergeStart = resume;

                for (int i = resume; i <= last && i < count; i++)
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

                        add(new Edge(from * ratio + offset, to * ratio + offset), i);
                        pending = false;
                    }
                    else pending = true;
                }

                reusable = true;
                previousFirst = first;
                previousLast = last;
                previousBegin = begin;
                previousRatio = ratio;
                previousOffset = offset;

                if (lines.Count == 0) return default;

                Vector2 min = boundsMin[^1], max = boundsMax[^1];

                if (!float.IsFinite(min.X) || !float.IsFinite(min.Y) || !float.IsFinite(max.X) || !float.IsFinite(max.Y))
                {
                    reset();
                    return default;
                }

                double rawX = min.X - radius * 1.15;
                double rawY = min.Y - radius * 1.15;
                // Stable subtracts the bounds as floats before widening.
                double rawWidth = (double)(max.X - min.X) + radius * 2.3;
                double rawHeight = (double)(max.Y - min.Y) + radius * 2.3;

                if (!double.IsFinite(rawX) || !double.IsFinite(rawY) || !double.IsFinite(rawWidth) || !double.IsFinite(rawHeight)
                    || rawX < int.MinValue || rawX > int.MaxValue
                    || rawY < int.MinValue || rawY > int.MaxValue
                    || rawWidth < 0 || rawWidth > int.MaxValue
                    || rawHeight < 0 || rawHeight > int.MaxValue)
                {
                    reset();
                    return default;
                }

                int x = (int)rawX;
                int y = (int)rawY;
                int width = (int)rawWidth;
                int height = (int)rawHeight;

                return new Viewport(new Vector2(Math.Max(x, 0), Math.Max(y, 0)), new Vector2(width, (float)(height - Math.Max(-(double)y, 0))))
                {
                    // Clipping a negative left edge shifts the projection; clipping the top shortens it.
                    OrthoLeft = Math.Max(x, 0),
                    OrthoRight = (int)Math.Clamp((long)Math.Max(x, 0) + width, int.MinValue, int.MaxValue),
                    OrthoBottom = Math.Max(y, 0),
                    OrthoTop = (int)Math.Clamp((long)y + height, int.MinValue, int.MaxValue),
                };
            }

            /// <summary>
            /// Stable's scans, for paths whose lengths are not ordered (NaN).
            /// </summary>
            private void findRangeLinear(double begin, double finish, out int first, out int last)
            {
                first = 0;
                last = source.Count - 1;

                for (int i = 0; i < source.Count; i++)
                {
                    if (cumulativeLengths[i] < begin) first = i + 1;
                    if (cumulativeLengths[i] > finish && last == source.Count - 1) last = i;
                }
            }

            private void add(Edge line, int end)
            {
                Vector2 min, max;

                if (lines.Count == 0)
                {
                    min = max = line.From;
                }
                else
                {
                    min = boundsMin[^1];
                    max = boundsMax[^1];
                }

                lines.Add(line);
                lineEnds.Add(end);
                boundsMin.Add(Vector2.ComponentMin(min, line.To));
                boundsMax.Add(Vector2.ComponentMax(max, line.To));
            }

            private void truncate(int count)
            {
                if (count >= lines.Count) return;

                lines.RemoveRange(count, lines.Count - count);
                lineEnds.RemoveRange(count, lineEnds.Count - count);
                boundsMin.RemoveRange(count, boundsMin.Count - count);
                boundsMax.RemoveRange(count, boundsMax.Count - count);
            }

            private void reset()
            {
                truncate(0);
                reusable = false;
            }
        }

        public static void Build(IReadOnlyList<Edge> lines, float radius, List<TexturedVertex3D> quads, List<TexturedVertex3D> caps)
        {
            quads.Clear();
            caps.Clear();
            Emit(lines, radius, quads.Add, caps.Add);
        }

        public static void Emit(IReadOnlyList<Edge> lines, float radius, Action<TexturedVertex3D>? quad, Action<TexturedVertex3D>? cap)
        {
            Edge[] array = lines as Edge[] ?? new List<Edge>(lines).ToArray();
            var frames = new LineFrame[array.Length];
            ComputeFrames(array, frames);

            if (quad != null)
            {
                var sink = new ActionSink(quad);
                EmitQuads(array, frames, radius, ref sink);
            }

            if (cap != null)
            {
                var sink = new ActionSink(cap);
                EmitCaps(array, frames, radius, ref sink);
            }
        }

        /// <summary>
        /// Receives mesh vertices. <see cref="Transform"/> must be a pure function of its input; vertices shared
        /// between triangles are transformed once and then added repeatedly.
        /// </summary>
        internal interface IVertexSink
        {
            TexturedVertex3D Transform(TexturedVertex3D vertex);

            void Add(in TexturedVertex3D vertex);
        }

        /// <summary>
        /// The direction of a line, as stable computes it.
        /// </summary>
        internal readonly record struct LineFrame(float Angle, float Cos, float Sin);

        public static void ComputeFrames(ReadOnlySpan<Edge> lines, Span<LineFrame> frames)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                float angle = theta(lines[i]);
                frames[i] = new LineFrame(angle, (float)Math.Cos(angle), (float)Math.Sin(angle));
            }
        }

        public static void EmitQuads<TSink>(ReadOnlySpan<Edge> lines, ReadOnlySpan<LineFrame> frames, float radius, ref TSink sink)
            where TSink : struct, IVertexSink
        {
            for (int i = 0; i < lines.Length; i++)
            {
                Edge line = lines[i];
                float length = (line.To - line.From).Length;
                float cos = frames[i].Cos, sin = frames[i].Sin;
                float m11 = length * cos, m12 = length * sin;
                float m21 = -radius * sin, m22 = radius * cos;

                var outerStart = sink.Transform(transformed(-0.0003f, -1, 0, m11, m12, m21, m22, line.From));
                var outerEnd = sink.Transform(transformed(1.0003f, -1, 0, m11, m12, m21, m22, line.From));
                var ridgeEnd = sink.Transform(transformed(1.0003f, 0.0001f, 1, m11, m12, m21, m22, line.From));
                var ridgeStart = sink.Transform(transformed(-0.0003f, 0.0001f, 1, m11, m12, m21, m22, line.From));

                sink.Add(outerStart);
                sink.Add(outerEnd);
                sink.Add(ridgeEnd);
                sink.Add(ridgeStart);
                sink.Add(ridgeStart);
                sink.Add(ridgeEnd);
                sink.Add(sink.Transform(transformed(1.0003f, 1, 0, m11, m12, m21, m22, line.From)));
                sink.Add(sink.Transform(transformed(-0.0003f, 1, 0, m11, m12, m21, m22, line.From)));
            }
        }

        public static void EmitCaps<TSink>(ReadOnlySpan<Edge> lines, ReadOnlySpan<LineFrame> frames, float radius, ref TSink sink)
            where TSink : struct, IVertexSink
        {
            for (int i = 0; i < lines.Length; i++)
            {
                Edge line = lines[i];
                float angle = frames[i].Angle, cos = frames[i].Cos, sin = frames[i].Sin;
                float turn = i + 1 < lines.Length ? frames[i + 1].Angle - angle : (float)Math.PI;
                if (turn > Math.PI) turn -= (float)Math.PI * 2;
                if (turn < -Math.PI) turn += (float)Math.PI * 2;
                int count = Math.Min(24, (int)Math.Ceiling(Math.Abs(turn * 24f) / Math.PI));
                emitCap(line.To, radius, turn < 0 ? -radius : radius, cos, sin, count, ref sink);
                if (i == 0 || line.From != lines[i - 1].To)
                    emitCap(line.From, -radius, -radius, cos, sin, 24, ref sink);
            }
        }

        private static void emitCap<TSink>(Vector2 centre, float sx, float sy, float cos, float sin, int triangles, ref TSink sink)
            where TSink : struct, IVertexSink
        {
            if (triangles <= 0) return;

            float m11 = sx * cos, m12 = sx * sin, m21 = -sy * sin, m22 = sy * cos;
            var middle = sink.Transform(vertex(new Vector3(centre.X, centre.Y, 1), 1));
            var previous = sink.Transform(rim(0));

            for (int i = 0; i < triangles; i++)
            {
                var next = sink.Transform(rim(i + 1));
                sink.Add(middle);
                sink.Add(previous);
                sink.Add(next);
                previous = next;
            }

            TexturedVertex3D rim(int i)
            {
                Vector2 point = capVertex(i);
                return transformed(point.X, point.Y, 0, m11, m12, m21, m22, centre);
            }
        }

        private static Vector2 capVertex(int i)
        {
            float angle = i * ((float)Math.PI / 24f);
            return new Vector2(i is 0 or 24 ? 0 : (float)Math.Sin(angle), i == 0 ? -1 : i == 24 ? 1 : -(float)Math.Cos(angle));
        }

        private static TexturedVertex3D transformed(float x, float y, float z, float m11, float m12, float m21, float m22, Vector2 origin)
            => vertex(new Vector3((float)((double)x * m11 + (double)y * m21 + origin.X), (float)((double)x * m12 + (double)y * m22 + origin.Y), z), z);

        private static float theta(Edge line)
        {
            return (float)Math.Atan2(line.To.Y - line.From.Y, line.To.X - line.From.X);
        }

        private static TexturedVertex3D vertex(Vector3 position, float gradient)
        {
            return new TexturedVertex3D
            {
                Position = position,
                Colour = Color4.White,
                TexturePosition = new Vector2(gradient * (1f - 1f / 128), 0.5f),
            };
        }

        private readonly struct ActionSink : IVertexSink
        {
            private readonly Action<TexturedVertex3D> add;

            public ActionSink(Action<TexturedVertex3D> add)
            {
                this.add = add;
            }

            public TexturedVertex3D Transform(TexturedVertex3D vertex) => vertex;

            public void Add(in TexturedVertex3D vertex) => add(vertex);
        }
    }
}
