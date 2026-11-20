// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
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

        [Test]
        public void TestQuantizeMergesUnequalCountsByWeight()
        {
            var pixels = new Rgba32[5];

            for (int i = 0; i < 4; i++)
                pixels[i] = new Rgba32(220, 40, 40);

            pixels[4] = new Rgba32(30, 120, 220);

            var colours = ColourQuantizer.Quantize(pixels, 1, 128);

            // a single cluster must land on the count-weighted mean rather than the midpoint of the two colours.
            Assert.That(colours, Has.Length.EqualTo(1));
            assertColour(colours[0], 182, 56, 76);
        }

        [Test]
        public void TestQuantizeFiltersPixelsBelowAlphaThreshold()
        {
            var pixels = new[]
            {
                new Rgba32(10, 20, 30, 127), // just below the threshold
                new Rgba32(200, 100, 50, 128), // exactly on the threshold
            };

            var colours = ColourQuantizer.Quantize(pixels, 8, 128);

            Assert.That(colours, Has.Length.EqualTo(1));
            assertColour(colours[0], 200, 100, 50);
        }

        [Test]
        public void TestQuantizeReturnsEmptyForFullyTransparentInput()
        {
            var pixels = new Rgba32[16];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Rgba32(255, 255, 255, 0);

            Assert.That(ColourQuantizer.Quantize(pixels, 8, 128), Is.Empty);
        }

        [Test]
        public void TestQuantizeRespectsRequestedColourLimit()
        {
            var pixels = new[]
            {
                new Rgba32(220, 40, 40),
                new Rgba32(220, 140, 40),
                new Rgba32(200, 200, 40),
                new Rgba32(40, 180, 60),
                new Rgba32(40, 180, 180),
                new Rgba32(30, 120, 220),
                new Rgba32(120, 60, 200),
                new Rgba32(200, 60, 180),
                new Rgba32(60, 60, 60),
                new Rgba32(150, 150, 150),
            };

            Assert.That(ColourQuantizer.Quantize(pixels, 4, 128), Has.Length.EqualTo(4));
        }

        [TestCase(100, 100, 100, 100, TestName = "Images already within the limit are not upscaled")]
        [TestCase(128, 128, 128, 128, TestName = "An exact fit is left untouched")]
        [TestCase(256, 128, 128, 64, TestName = "Landscape scales by width")]
        [TestCase(128, 256, 64, 128, TestName = "Portrait scales by height")]
        [TestCase(1024, 64, 128, 8, TestName = "Wide images keep their aspect ratio")]
        [TestCase(4000, 10, 128, 1, TestName = "Degenerate dimensions clamp to one pixel")]
        public void TestFitWithin(int width, int height, int expectedWidth, int expectedHeight)
        {
            Assert.That(BackgroundComboColourExtractor.FitWithin(new Size(width, height), 128),
                Is.EqualTo(new Size(expectedWidth, expectedHeight)));
        }

        private static void assertColour(Colour4 colour, byte r, byte g, byte b)
        {
            Assert.That(MathF.Round(colour.R * 255), Is.EqualTo(r), "red component");
            Assert.That(MathF.Round(colour.G * 255), Is.EqualTo(g), "green component");
            Assert.That(MathF.Round(colour.B * 255), Is.EqualTo(b), "blue component");
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
