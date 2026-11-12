// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Imaging;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Legacy
{
    /// <summary>
    /// The classic oversized body's 128-texel gradient. Ordinary paths and other skins
    /// retain their own profiles and anti-aliasing pipelines.
    /// </summary>
    internal readonly struct LegacySliderBodyGradient
    {
        public const int TEXTURE_WIDTH = 128;

        private readonly float aaWidth;
        private readonly Color4 border;
        private readonly Color4 inner;
        private readonly Color4 outer;

        public LegacySliderBodyGradient(Color4 colour, Color4 borderColour, float screenRadius)
        {
            // Stable's SkinManager passes HitObjectRadius * GameField.Ratio, not the
            // unscaled field radius, to MmSliderRendererGL.DefaultRadius.
            aaWidth = Math.Clamp(0.5f / screenRadius, 3f / 256, 1f / 16);
            var borderBytes = new Rgba32(borderColour.R, borderColour.G, borderColour.B, borderColour.A);
            border = new Color4(borderBytes.R, borderBytes.G, borderBytes.B, borderBytes.A);
            ComputeColours(colour, out inner, out outer);
        }

        public static void ComputeColours(Color4 colour, out Color4 innerColour, out Color4 outerColour)
        {
            // Recover the skin's byte colour before applying the original byte-domain
            // arithmetic. Stable truncates each endpoint BEFORE rasterising the ramp.
            var bytes = new Rgba32(colour.R, colour.G, colour.B);
            innerColour = new Color4(lighten(bytes.R), lighten(bytes.G), lighten(bytes.B), 180);
            outerColour = new Color4(darken(bytes.R), darken(bytes.G), darken(bytes.B), 180);
        }

        private static byte lighten(byte value) => (byte)Math.Min(255f, value * 1.125f + 255f * 0.25f);

        private static byte darken(byte value) => (byte)(value / 1.1f);

        public Color4 Sample(float position)
        {
            const float shadow_portion = 5f / 64;
            const float border_portion = 3f / 16;
            var shadow = new Color4(0, 0, 0, 64);

            if (position <= shadow_portion - aaWidth)
                // Color4.Transparent is transparent WHITE. Interpolating it would
                // introduce visible grey into this otherwise black shadow.
                return interpolate(position, default, shadow, 0, shadow_portion - aaWidth);

            if (position <= shadow_portion + aaWidth)
                return interpolate(position, shadow, border, shadow_portion - aaWidth, shadow_portion + aaWidth);

            if (position <= border_portion - aaWidth)
                return border;

            if (position <= border_portion + aaWidth)
                return interpolate(position, border, outer, border_portion - aaWidth, border_portion + aaWidth);

            return interpolate(position, outer, inner, border_portion + aaWidth, 1);
        }

        private static Color4 interpolate(float position, Color4 from, Color4 to, float start, float end)
        {
            float t = (position - start) / (end - start);
            return new Color4(from.R + (to.R - from.R) * t, from.G + (to.G - from.G) * t,
                from.B + (to.B - from.B) * t, from.A + (to.A - from.A) * t);
        }
    }
}
