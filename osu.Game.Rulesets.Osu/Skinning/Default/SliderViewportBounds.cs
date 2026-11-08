// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Lines;
using osuTK;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    /// <summary>
    /// Range bounds for snaking paths. A tree over blocks keeps storage small without rescanning
    /// the full path on every frame, including backwards seeks and snaking out.
    /// </summary>
    internal sealed class SliderViewportBounds
    {
        private const int block_size = 128;
        private IReadOnlyList<Vector2>? vertices;
        private int version;
        private int count;
        private int leafCount;
        private Bounds[] tree = Array.Empty<Bounds>();

        public (Vector2 Minimum, Vector2 Maximum) Query(IReadOnlyList<Vector2> points, int pathVersion, PathDrawRange range)
        {
            if (!ReferenceEquals(vertices, points) || version != pathVersion || count != points.Count)
                rebuild(points, pathVersion);

            var result = Bounds.Empty;
            result.Include(range.StartPoint);
            result.Include(range.EndPoint);
            int first = range.FirstVertex;
            int end = first + range.VertexCount;

            while (first < end && first % block_size != 0)
                result.Include(points[first++]);
            while (first < end && end % block_size != 0)
                result.Include(points[--end]);

            int left = leafCount + first / block_size;
            int right = leafCount + end / block_size;
            while (left < right)
            {
                if ((left & 1) != 0)
                    result.Include(tree[left++]);
                if ((right & 1) != 0)
                    result.Include(tree[--right]);
                left /= 2;
                right /= 2;
            }

            return (result.Minimum, result.Maximum);
        }

        private void rebuild(IReadOnlyList<Vector2> points, int pathVersion)
        {
            vertices = points;
            version = pathVersion;
            count = points.Count;
            leafCount = 1;
            while (leafCount < (count + block_size - 1) / block_size)
                leafCount *= 2;
            tree = new Bounds[leafCount * 2];
            Array.Fill(tree, Bounds.Empty);
            for (int i = 0; i < count; i++)
                tree[leafCount + i / block_size].Include(points[i]);
            for (int i = leafCount - 1; i > 0; i--)
            {
                tree[i].Include(tree[i * 2]);
                tree[i].Include(tree[i * 2 + 1]);
            }
        }

        private struct Bounds
        {
            public Vector2 Minimum;
            public Vector2 Maximum;
            public static Bounds Empty => new Bounds
            {
                Minimum = new Vector2(float.PositiveInfinity),
                Maximum = new Vector2(float.NegativeInfinity)
            };

            public void Include(Vector2 point)
            {
                Minimum = Vector2.ComponentMin(Minimum, point);
                Maximum = Vector2.ComponentMax(Maximum, point);
            }

            public void Include(Bounds bounds)
            {
                Minimum = Vector2.ComponentMin(Minimum, bounds.Minimum);
                Maximum = Vector2.ComponentMax(Maximum, bounds.Maximum);
            }
        }
    }
}
