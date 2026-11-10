// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Utils;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Beatmaps.Formats;
using osuTK;

namespace osu.Game.Rulesets.Osu.Objects.Drawables
{
    /// <summary>
    /// Builds the absolute, independently rounded lines consumed by stable's ball animations.
    /// This geometry is separate from both gameplay paths and body rasterisation.
    /// </summary>
    internal static class LegacySliderBallPath
    {
        internal readonly record struct Edge(Vector2 From, Vector2 To);

        public static bool Build(SliderPath path, Vector2 origin, List<Edge> edges)
        {
            edges.Clear();
            if (path.LegacyPath is { } legacy)
            {
                if (!appendOriginal(legacy.PointString, legacy.Origin, legacy.FormatVersion, edges))
                    return false;
            }
            else if (!appendRelative(path, origin, edges))
                return false;

            bool valid = trim(path.ExpectedDistance.Value, edges);
            if (valid && path.LegacyPath is { } original && original.Origin != origin)
            {
                Vector2 offset = origin - original.Origin;
                for (int i = 0; i < edges.Count; i++)
                    edges[i] = new Edge(edges[i].From + offset, edges[i].To + offset);
            }
            return valid;
        }

        private static bool appendRelative(SliderPath path, Vector2 origin, List<Edge> edges)
        {
            var controls = path.ControlPoints;
            int start = 0;
            for (int i = 1; i < controls.Count; i++)
            {
                if (i < controls.Count - 1 && controls[i].Type == null)
                    continue;

                var type = controls[start].Type ?? PathType.LINEAR;
                if (type.Degree != null)
                    return false;

                if (type.Type == SplineType.Linear || i - start == 1)
                {
                    for (int j = start + 1; j <= i; j++)
                    {
                        Vector2 from = controls[j - 1].Position + origin;
                        Vector2 to = controls[j].Position + origin;
                        // Even stable's two-point lines go through this arithmetic. At Aspire
                        // coordinates From + (To - From) can differ from To, leaving a gap to
                        // the next line. A contiguous vertex list cannot preserve those gaps.
                        edges.Add(new Edge(from + (to - from) * 0f, from + (to - from) * 1f));
                    }
                }
                else
                {
                    var points = new Vector2[i - start + 1];
                    for (int j = 0; j < points.Length; j++)
                        points[j] = controls[start + j].Position + origin;

                    switch (type.Type)
                    {
                        case SplineType.Catmull:
                            // Stable uses every 1/50-knot interval for motion. Lazer's bulb
                            // optimisation and duplicate-vertex removal are drawing shortcuts.
                            var catmull = PathApproximator.CatmullToPiecewiseLinear(points);
                            for (int j = 0; j + 1 < catmull.Count; j += 2)
                                edges.Add(new Edge(catmull[j], catmull[j + 1]));
                            break;

                        case SplineType.PerfectCurve:
                            if (points.Length == 3 && appendArc(points, edges))
                                break;
                            appendBezier(points, edges);
                            break;

                        default:
                            appendBezier(points, edges);
                            break;
                    }
                }
                start = i;
            }
            return true;
        }

        private static bool appendOriginal(string pointString, Vector2 origin, int version, List<Edge> edges)
        {
            ReadOnlySpan<char> data = pointString.AsSpan();
            var points = new List<Vector2> { origin };
            bool first = true;
            foreach (var range in data.Split('|'))
            {
                ReadOnlySpan<char> token = data[range];
                if (first)
                {
                    first = false;
                    if (token.Length != 1)
                        return false;
                    continue;
                }
                int separator = token.IndexOf(':');
                if (separator < 0)
                    return false;
                ReadOnlySpan<char> y = token[(separator + 1)..];
                int extra = y.IndexOf(':');
                if (extra >= 0)
                    y = y[..extra];
                points.Add(new Vector2(coordinate(token[..separator]), coordinate(y)));
            }
            if (points.Count < 2)
                return false;

            char type = data[0];
            if (type == 'P' && points.Count == 3)
                return appendArc(points.ToArray(), edges);
            if (type == 'L' || (type == 'P' && points.Count < 3))
            {
                for (int i = 1; i < points.Count; i++)
                    appendStraight(points[i - 1], points[i], edges);
                return true;
            }
            if (type == 'C')
            {
                var curve = PathApproximator.CatmullToPiecewiseLinear(points.ToArray());
                for (int i = 0; i + 1 < curve.Count; i += 2)
                    edges.Add(new Edge(curve[i], curve[i + 1]));
                return true;
            }
            if (type != 'B' && type != 'P')
                return false;

            int start = 0;
            for (int i = 0; i < points.Count; i++)
            {
                bool duplicate = version > 6
                    ? i < points.Count - 2 && points[i] == points[i + 1]
                    : i > 0 && points[i] == points[i - 1];
                if (!duplicate && i != points.Count - 1)
                    continue;
                Vector2[] segment = points.GetRange(start, i - start + 1).ToArray();
                if (version > 8 && segment.Length == 2)
                    appendStraight(segment[0], segment[1], edges);
                else if (version == 9)
                {
                    // CreateBezierWrong deliberately omits t=1 in v9 maps.
                    Vector2[] scratch = new Vector2[segment.Length];
                    Vector2 previous = segment[0];
                    int samples = 50 * segment.Length;
                    for (int sample = 1; sample < samples; sample++)
                    {
                        segment.CopyTo(scratch, 0);
                        for (int depth = 0; depth < scratch.Length; depth++)
                            for (int j = 0; j < scratch.Length - depth - 1; j++)
                                scratch[j] = scratch[j] + (scratch[j + 1] - scratch[j]) * ((float)sample / samples);
                        edges.Add(new Edge(previous, scratch[0]));
                        previous = scratch[0];
                    }
                }
                else
                    appendBezier(segment, edges);
                if (duplicate && version > 6)
                    i++;
                start = i;
            }
            return true;

            static int coordinate(ReadOnlySpan<char> value)
                => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer) && integer != int.MinValue
                    ? integer : (int)Parsing.ParseDouble(value);
        }

        private static void appendStraight(Vector2 from, Vector2 to, List<Edge> edges)
            => edges.Add(new Edge(from + (to - from) * 0f, from + (to - from) * 1f));

        private static bool trim(double? expectedDistance, List<Edge> edges)
        {

            // SliderOsu.UpdateCalculations trims its float line list, rather than using
            // lazer's already length-adjusted relative vertices. Preserve the cast order.
            double length = 0;
            foreach (var edge in edges)
                length += (edge.To - edge.From).Length;
            if (!double.IsFinite(length))
                return false;
            if (length > 0)
            {
                double expected = expectedDistance is double value && value != 0 ? value : length;
                if (!double.IsFinite(expected))
                    return false;
                double excess = length - expected;
                while (edges.Count > 0)
                {
                    Edge edge = edges[^1];
                    float distance = (edge.To - edge.From).Length;
                    if (distance > excess + 0.0001)
                    {
                        if (edge.To != edge.From)
                            edges[^1] = edge with { To = edge.From + (edge.To - edge.From).Normalized() * (distance - (float)excess) };
                        break;
                    }
                    edges.RemoveAt(edges.Count - 1);
                    excess -= distance;
                }
            }

            return edges.Count > 0;
        }

        private static void appendBezier(Vector2[] points, List<Edge> edges)
        {
            var curve = PathApproximator.BSplineToPiecewiseLinear(points, points.Length);
            for (int i = 1; i < curve.Count; i++)
                edges.Add(new Edge(curve[i - 1], curve[i]));
        }

        private static bool appendArc(Vector2[] points, List<Edge> edges)
        {
            Vector2 a = points[0];
            Vector2 b = points[1];
            Vector2 c = points[2];
            if ((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y) == 0)
            {
                edges.Add(new Edge(a + (b - a) * 0f, a + (b - a) * 1f));
                edges.Add(new Edge(b + (c - b) * 0f, b + (c - b) * 1f));
                return true;
            }

            // These float intermediates and the float 2*pi constant are intentional:
            // they match OsuMathHelper.CircleThroughPoints, not CircularArcProperties.
            float denominator = 2f * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
            float aa = a.LengthSquared;
            float bb = b.LengthSquared;
            float cc = c.LengthSquared;
            var centre = new Vector2(
                (aa * (b.Y - c.Y) + bb * (c.Y - a.Y) + cc * (a.Y - b.Y)) / denominator,
                (aa * (c.X - b.X) + bb * (a.X - c.X) + cc * (b.X - a.X)) / denominator);
            float radius = (centre - a).Length;
            double initial = Math.Atan2(a.Y - centre.Y, a.X - centre.X);
            double middle = Math.Atan2(b.Y - centre.Y, b.X - centre.X);
            double final = Math.Atan2(c.Y - centre.Y, c.X - centre.X);
            while (middle < initial)
                middle += 6.2831854820251465;
            while (final < initial)
                final += 6.2831854820251465;
            if (middle > final)
                final -= 6.2831854820251465;

            double count = Math.Abs((final - initial) * radius) * 0.125;
            if (!double.IsFinite(count) || count > 1_000_000)
                return false;
            int subdivisions = (int)count;
            Vector2 previous = a;
            for (int i = 1; i < subdivisions; i++)
            {
                double progress = (double)i / subdivisions;
                double angle = final * progress + initial * (1 - progress);
                Vector2 next = new Vector2((float)(Math.Cos(angle) * radius), (float)(Math.Sin(angle) * radius)) + centre;
                edges.Add(new Edge(previous, next));
                previous = next;
            }
            edges.Add(new Edge(previous, c));
            return true;
        }
    }
}
