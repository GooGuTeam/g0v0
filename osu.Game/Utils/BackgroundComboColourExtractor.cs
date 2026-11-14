// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Imaging;

namespace osu.Game.Utils
{
    /// <summary>
    /// Extracts prominent combo colours from a beatmap background.
    /// </summary>
    public static class BackgroundComboColourExtractor
    {
        public const int MAX_COLOUR_COUNT = 8;

        // Resolution to downscale to before quantizing.
        private const int max_dimension = 128;

        // Pixels more transparent than this are not considered part of the background's palette.
        private const byte min_alpha = 128;

        // Ranking criteria: avoid colours with lightness <50 or >220.
        private const float min_lightness = 50 / 255f;
        private const float max_lightness = 220 / 255f;

        public static Colour4[] Extract(Stream imageStream)
        {
            using var image = Image.Load<Rgba32>(imageStream);

            var size = FitWithin(image.Size, max_dimension);
            image.Mutate(context => { context.Resize(size, ImageResampler.Bicubic); });

            return ColourQuantizer.Quantize(image.Pixels, MAX_COLOUR_COUNT, min_alpha)
                                  .Select(clampLightness)
                                  .ToArray();
        }

        /// <summary>
        /// Scales the given size down until both dimensions fit within <paramref name="maxDimension"/>, preserving aspect ratio.
        /// </summary>
        internal static Size FitWithin(Size size, int maxDimension)
        {
            if (size.Width <= maxDimension && size.Height <= maxDimension)
                return size;

            float scale = maxDimension / (float)Math.Max(size.Width, size.Height);

            return new Size(
                Math.Clamp((int)MathF.Round(size.Width * scale), 1, maxDimension),
                Math.Clamp((int)MathF.Round(size.Height * scale), 1, maxDimension));
        }

        private static Colour4 clampLightness(Colour4 colour)
        {
            var hsl = colour.ToHSL();
            return Colour4.FromHSL(hsl.X, hsl.Y, Math.Clamp(hsl.Z, min_lightness, max_lightness));
        }
    }
}
