// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Imaging;
using osu.Framework.Platform;

namespace Previewer
{
    /// <summary>
    /// Reads frames from an offscreen host and writes them, whole or cropped to an area, as PNGs.
    /// Everything that captures or saves an image goes through here, whichever scene is being previewed.
    /// </summary>
    public static class FrameCapture
    {
        /// <summary>
        /// Captures the frame the host renders next. The task completes off the update thread, so callers wait on it.
        /// </summary>
        public static Task<Image<Rgba32>> Async(GameHost host) => ((OffscreenGameHost)host).CaptureAsync();

        /// <summary>
        /// Saves <paramref name="image"/>, or the part of it inside <paramref name="area"/>, as a PNG.
        /// </summary>
        public static void Save(Image<Rgba32> image, string path, RectangleF? area)
        {
            if (area is not { } rect)
            {
                using var stream = File.Create(path);
                image.SaveAsPng(stream);

                return;
            }

            int left = Math.Clamp((int)Math.Floor(rect.Left), 0, image.Width - 1);
            int top = Math.Clamp((int)Math.Floor(rect.Top), 0, image.Height - 1);
            int right = Math.Clamp((int)Math.Ceiling(rect.Right), left + 1, image.Width);
            int bottom = Math.Clamp((int)Math.Ceiling(rect.Bottom), top + 1, image.Height);

            using var crop = new Image<Rgba32>(right - left, bottom - top);

            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                    crop[x - left, y - top] = image[x, y];
            }

            using (var stream = File.Create(path))
                crop.SaveAsPng(stream);
        }
    }
}
