// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Imaging;
using osu.Game.Utils;

namespace osu.Game.Tests.Editing
{
    [TestFixture]
    public class BackgroundComboColourExtractorTest
    {
        [Test]
        public void TestExtractFindsDistinctColours()
        {
            using var stream = createImage(100, 100, (x, y) => (x < 50, y < 50) switch
            {
                (true, true) => new Rgba32(30, 120, 220), // blue
                (false, true) => new Rgba32(220, 180, 30), // yellow
                (true, false) => new Rgba32(40, 180, 60), // green
                (false, false) => new Rgba32(220, 40, 40), // red
            });

            var colours = BackgroundComboColourExtractor.Extract(stream);

            Assert.That(colours.Any(c => c.B > c.R && c.B > c.G), Is.True); // blue
            Assert.That(colours.Any(c => c.R > c.B && c.G > c.B), Is.True); // yellow
            Assert.That(colours.Any(c => c.G > c.R && c.G > c.B), Is.True); // green
            Assert.That(colours.Any(c => c.R > c.G && c.R > c.B), Is.True); // red
        }

        private static MemoryStream createImage(int width, int height, Func<int, int, Rgba32> getPixel)
        {
            using (var image = new Image<Rgba32>(width, height))
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                        image[x, y] = getPixel(x, y);
                }

                var encoded = new MemoryStream();
                image.SaveAsPng(encoded);
                encoded.Position = 0;
                return encoded;
            }
        }
    }
}
