// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using osu.Framework.Graphics;
using osu.Framework.Imaging;

namespace osu.Game.Utils
{
    /// <summary>
    /// Reduces the pixels of an image to a small set of representative colours.
    /// </summary>
    internal static class ColourQuantizer
    {
        /// <summary>
        /// Bits of precision kept per colour channel when bucketing. 5 bits keeps near-identical colours
        /// together while still separating colours which are distinguishable by eye.
        /// </summary>
        private const int bits_per_channel = 5;

        private const int buckets_per_channel = 1 << bits_per_channel;
        private const int bucket_count = buckets_per_channel * buckets_per_channel * buckets_per_channel;
        private const int channel_shift = 8 - bits_per_channel;

        private const int max_iterations = 32;

        /// <summary>
        /// Returns at most <paramref name="maxColours"/> representative colours of the given pixels,
        /// ordered by descending prominence. Pixels more transparent than <paramref name="minimumAlpha"/> are ignored.
        /// </summary>
        public static Colour4[] Quantize(ReadOnlySpan<Rgba32> pixels, int maxColours, byte minimumAlpha)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxColours);

            var points = buildHistogram(pixels, minimumAlpha, out int[] weights);

            if (points.Length == 0)
                return Array.Empty<Colour4>();

            var centroids = seed(points, weights, Math.Min(maxColours, points.Length));
            int[] assignment = new int[points.Length];

            assign(points, centroids, assignment);

            for (int iteration = 0; iteration <= max_iterations; iteration++)
            {
                rebuildCentroids(points, weights, assignment, centroids);

                if (iteration == max_iterations || !assign(points, centroids, assignment))
                    break;
            }

            return collect(points, weights, assignment, centroids);
        }

        /// <summary>
        /// Aggregates the pixels into weighted buckets, returning the mean colour of each non-empty bucket.
        /// </summary>
        private static Vector3[] buildHistogram(ReadOnlySpan<Rgba32> pixels, byte minimumAlpha, out int[] weights)
        {
            var sums = new Vector3[bucket_count];
            int[] counts = new int[bucket_count];
            int used = 0;

            foreach (var pixel in pixels)
            {
                if (pixel.A < minimumAlpha)
                    continue;

                int bucket = ((pixel.R >> channel_shift) << (bits_per_channel * 2))
                             | ((pixel.G >> channel_shift) << bits_per_channel)
                             | (pixel.B >> channel_shift);

                if (counts[bucket]++ == 0)
                    used++;

                sums[bucket] += new Vector3(pixel.R, pixel.G, pixel.B);
            }

            var points = new Vector3[used];
            weights = new int[used];

            for (int bucket = 0, index = 0; bucket < bucket_count; bucket++)
            {
                if (counts[bucket] == 0)
                    continue;

                points[index] = sums[bucket] / counts[bucket];
                weights[index] = counts[bucket];
                index++;
            }

            return points;
        }

        /// <summary>
        /// Chooses initial centroids deterministically: an anchor at the most prominent colour, then repeatedly
        /// the colour which scores the highest on both prominence and distance from everything picked so far.
        /// </summary>
        private static Vector3[] seed(Vector3[] points, int[] weights, int clusterCount)
        {
            var centroids = new Vector3[clusterCount];
            int anchor = 0;

            for (int i = 1; i < points.Length; i++)
            {
                if (weights[i] > weights[anchor])
                    anchor = i;
            }

            centroids[0] = points[anchor];

            if (clusterCount == 1)
                return centroids;

            float[] nearest = new float[points.Length];

            for (int i = 0; i < points.Length; i++)
                nearest[i] = Vector3.DistanceSquared(points[i], centroids[0]);

            for (int c = 1; c < clusterCount; c++)
            {
                int best = 0;
                float bestScore = -1;

                for (int i = 0; i < points.Length; i++)
                {
                    float score = nearest[i] * weights[i];

                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    best = i;
                }

                centroids[c] = points[best];

                for (int i = 0; i < points.Length; i++)
                    nearest[i] = Math.Min(nearest[i], Vector3.DistanceSquared(points[i], centroids[c]));
            }

            return centroids;
        }

        /// <summary>
        /// Assigns every point to its nearest centroid. Returns whether any assignment changed.
        /// </summary>
        private static bool assign(Vector3[] points, Vector3[] centroids, int[] assignment)
        {
            bool changed = false;

            for (int i = 0; i < points.Length; i++)
            {
                int closest = 0;
                float bestDistance = Vector3.DistanceSquared(points[i], centroids[0]);

                for (int c = 1; c < centroids.Length; c++)
                {
                    float distance = Vector3.DistanceSquared(points[i], centroids[c]);

                    if (distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    closest = c;
                }

                if (assignment[i] == closest)
                    continue;

                assignment[i] = closest;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Moves every centroid to the weighted mean of the points assigned to it. Clusters which lost all
        /// their points keep their previous position so that they can still attract points in later iterations.
        /// </summary>
        private static void rebuildCentroids(Vector3[] points, int[] weights, int[] assignment, Vector3[] centroids)
        {
            var sums = new Vector3[centroids.Length];
            int[] counts = new int[centroids.Length];

            for (int i = 0; i < points.Length; i++)
            {
                int cluster = assignment[i];

                sums[cluster] += points[i] * weights[i];
                counts[cluster] += weights[i];
            }

            for (int c = 0; c < centroids.Length; c++)
            {
                if (counts[c] > 0)
                    centroids[c] = sums[c] / counts[c];
            }
        }

        /// <summary>
        /// Converts the non-empty clusters to colours, heaviest first.
        /// </summary>
        private static Colour4[] collect(Vector3[] points, int[] weights, int[] assignment, Vector3[] centroids)
        {
            int[] clusterWeights = new int[centroids.Length];

            for (int i = 0; i < points.Length; i++)
                clusterWeights[assignment[i]] += weights[i];

            var colours = new Colour4[centroids.Length];
            bool[] taken = new bool[centroids.Length];
            int count = 0;

            while (count < colours.Length)
            {
                int next = -1;

                for (int c = 0; c < centroids.Length; c++)
                {
                    if (taken[c] || clusterWeights[c] == 0)
                        continue;

                    if (next < 0 || clusterWeights[c] > clusterWeights[next])
                        next = c;
                }

                if (next < 0)
                    break;

                taken[next] = true;
                colours[count++] = toColour(centroids[next]);
            }

            return count == colours.Length ? colours : colours.AsSpan(0, count).ToArray();
        }

        private static Colour4 toColour(Vector3 centroid) => new Colour4(toByte(centroid.X), toByte(centroid.Y), toByte(centroid.Z), byte.MaxValue);

        private static byte toByte(float value) => (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
    }
}
