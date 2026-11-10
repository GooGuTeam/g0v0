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

        internal readonly record struct Viewport(Vector2 Origin, Vector2 RequestedSize);

        public static Viewport Prepare(IReadOnlyList<LegacySliderBallPath.Edge> source, double progressStart, double progressEnd,
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
            int width = (int)((double)max.X - min.X + radius * 2.3);
            int height = (int)((double)max.Y - min.Y + radius * 2.3);
            return new Viewport(new Vector2(Math.Max(x, 0), Math.Max(y, 0)), new Vector2(width, (float)(height - Math.Max(-(double)y, 0))));

            Vector2 display(Vector2 point) => point * ratio + offset;
        }

        public static void Build(IReadOnlyList<Edge> lines, float radius, List<TexturedVertex3D> quads, List<TexturedVertex3D> caps)
        {
            quads.Clear();
            caps.Clear();
            Emit(lines, radius, quads.Add, caps.Add);
        }

        // A bounded renderer batch consumes vertices directly; retaining 11M expanded
        // cap vertices per drawable is unnecessary when the resulting FBO is cached.
        public static void Emit(IReadOnlyList<Edge> lines, float radius, Action<TexturedVertex3D>? quad, Action<TexturedVertex3D>? cap)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                Edge line = lines[i];
                float length = (line.To - line.From).Length;
                float angle = theta(line);
                float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);

                if (quad != null)
                {
                    float m11 = length * cos, m12 = length * sin;
                    float m21 = -radius * sin, m22 = radius * cos;
                    quad(transformed(-0.0003f, -1, 0, m11, m12, m21, m22, line.From));
                    quad(transformed(1.0003f, -1, 0, m11, m12, m21, m22, line.From));
                    quad(transformed(1.0003f, 0.0001f, 1, m11, m12, m21, m22, line.From));
                    quad(transformed(-0.0003f, 0.0001f, 1, m11, m12, m21, m22, line.From));
                    quad(transformed(-0.0003f, 0.0001f, 1, m11, m12, m21, m22, line.From));
                    quad(transformed(1.0003f, 0.0001f, 1, m11, m12, m21, m22, line.From));
                    quad(transformed(1.0003f, 1, 0, m11, m12, m21, m22, line.From));
                    quad(transformed(-0.0003f, 1, 0, m11, m12, m21, m22, line.From));
                }

                if (cap == null) continue;

                float turn = i + 1 < lines.Count ? theta(lines[i + 1]) - angle : (float)Math.PI;
                if (turn > Math.PI) turn -= (float)Math.PI * 2;
                if (turn < -Math.PI) turn += (float)Math.PI * 2;
                int count = Math.Min(24, (int)Math.Ceiling(Math.Abs(turn * 24f) / Math.PI));
                emitCap(line.To, radius, turn < 0 ? -radius : radius, cos, sin, count, cap);
                if (i == 0 || line.From != lines[i - 1].To)
                    emitCap(line.From, -radius, -radius, cos, sin, 24, cap);
            }
        }

        private static void emitCap(Vector2 centre, float sx, float sy, float cos, float sin, int triangles, Action<TexturedVertex3D> add)
        {
            float m11 = sx * cos, m12 = sx * sin, m21 = -sy * sin, m22 = sy * cos;

            for (int i = 0; i < triangles; i++)
            {
                add(vertex(new Vector3(centre.X, centre.Y, 1), 1));
                Vector2 a = capVertex(i), b = capVertex(i + 1);
                add(transformed(a.X, a.Y, 0, m11, m12, m21, m22, centre));
                add(transformed(b.X, b.Y, 0, m11, m12, m21, m22, centre));
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
    }
}
