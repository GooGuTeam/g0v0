// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Screens.Ranking.Expanded.Accuracy
{
    /// <summary>
    /// Circular progress with colour following the arc, not interpolated across the bounding rectangle.
    /// The framework still renders the outline and round caps analytically at the current resolution.
    /// </summary>
    internal partial class V2AccuracyRing : CircularProgress
    {
        private static readonly Colour4 start_colour = Colour4.FromHex("#7CF6FF");
        private static readonly Colour4 end_colour = Colour4.FromHex("#BAFFA9");

        public V2AccuracyRing()
        {
            Rotation = (float)V2AccuracyScale.RING_START;
            InnerRadius = 0.095f;
            RoundedCaps = true;
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer)
        {
            const int texture_size = 256;
            var image = new Image<Rgba32>(texture_size, texture_size);

            for (int y = 0; y < texture_size; y++)
            {
                for (int x = 0; x < texture_size; x++)
                {
                    double angle = Math.Atan2(x + 0.5 - texture_size / 2.0, texture_size / 2.0 - y - 0.5) * 180 / Math.PI;
                    if (angle < 0)
                        angle += 360;

                    // The seam lies in the open gap. Both round caps retain their terminal colour.
                    if (angle > 355)
                        angle = 0;

                    float t = (float)Math.Clamp(angle / (V2AccuracyScale.RING_END - V2AccuracyScale.RING_START), 0, 1);
                    var colour = GetArcColour(t);
                    image[x, y] = new Rgba32(colour.R, colour.G, colour.B, 1);
                }
            }

            var texture = new DisposableTexture(renderer.CreateTexture(texture_size, texture_size, true));
            texture.SetData(new TextureUpload(image));
            Vector2 size = Size;
            Texture = texture;
            Size = size;
        }

        internal static Colour4 GetArcColour(float progress) => new Colour4(
            start_colour.R + (end_colour.R - start_colour.R) * progress,
            start_colour.G + (end_colour.G - start_colour.G) * progress,
            start_colour.B + (end_colour.B - start_colour.B) * progress,
            1);
    }
}
