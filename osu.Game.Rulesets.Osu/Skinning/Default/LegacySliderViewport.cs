// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    /// <summary>
    /// The viewport requested by stable's SliderOsu.Draw, before driver clamping.
    /// Coordinates are display pixels, not playfield coordinates.
    /// </summary>
    internal readonly record struct LegacySliderViewport(Vector2 Origin, Vector2 Size)
    {
        public static LegacySliderViewport FromBounds(Vector2 minimum, Vector2 maximum, float radius)
        {
            // Keep the double intermediates and truncation used by stable. In particular, clipping
            // a negative X does not reduce viewport width, whereas clipping Y reduces its height.
            if (!float.IsFinite(minimum.X) || !float.IsFinite(minimum.Y)
                || !float.IsFinite(maximum.X) || !float.IsFinite(maximum.Y) || !float.IsFinite(radius))
                return new LegacySliderViewport(Vector2.Zero, Vector2.Zero);

            int left = (int)(minimum.X - (double)radius * 1.15);
            int top = (int)(minimum.Y - (double)radius * 1.15);
            int width = (int)((maximum.X - minimum.X) + (double)radius * 2.3);
            int height = (int)((maximum.Y - minimum.Y) + (double)radius * 2.3);
            double clippedTop = Math.Max(-(double)top, 0);
            return new LegacySliderViewport(
                new Vector2(Math.Max(left, 0), Math.Max(top, 0)),
                new Vector2(width, (float)(height - clippedTop)));
        }

        public Vector2 ScaleFor(Vector2 limit) => new Vector2(
            Size.X > 0 ? Math.Min(1, limit.X / Size.X) : 1,
            Size.Y > 0 ? Math.Min(1, limit.Y / Size.Y) : 1);
    }
}
