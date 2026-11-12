// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Imaging;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class LegacySliderBodyGradientTest
    {
        // Byte endpoints from original 20260924.1 ComputeSliderColour IL, not from
        // the implementation under test. Alpha must ignore the input skin alpha.
        [TestCase(255, 210, 0, 231, 190, 0, 255, 255, 63)]
        [TestCase(228, 228, 228, 207, 207, 207, 255, 255, 255)]
        [TestCase(227, 223, 198, 206, 202, 180, 255, 255, 255)]
        [TestCase(108, 133, 122, 98, 120, 110, 185, 213, 201)]
        [TestCase(121, 43, 57, 110, 39, 51, 199, 112, 127)]
        [TestCase(0, 0, 0, 0, 0, 0, 63, 63, 63)]
        [TestCase(255, 255, 255, 231, 231, 231, 255, 255, 255)]
        public void TestOriginalByteEndpoints(byte r, byte g, byte b, byte outerR, byte outerG, byte outerB, byte innerR, byte innerG, byte innerB)
        {
            LegacySliderBodyGradient.ComputeColours(new Color4(r, g, b, 13), out var inner, out var outer);
            Assert.That(inner, Is.EqualTo(new Color4(innerR, innerG, innerB, 180)));
            Assert.That(outer, Is.EqualTo(new Color4(outerR, outerG, outerB, 180)));
        }

        [Test]
        public void TestEveryInputByteUsesOriginalArithmetic()
        {
            // These two byte-domain formulae were checked for all 256 values against
            // the original IL. In particular, darkening is not multiplication by .9.
            for (int i = 0; i <= 255; i++)
            {
                byte channel = (byte)i;
                LegacySliderBodyGradient.ComputeColours(new Color4(channel, channel, channel, 255), out var inner, out var outer);
                byte expectedInner = (byte)Math.Min(255f, i * 1.125f + 63.75f);
                byte expectedOuter = (byte)(i / 1.1f);
                Assert.That(inner, Is.EqualTo(new Color4(expectedInner, expectedInner, expectedInner, 180)));
                Assert.That(outer, Is.EqualTo(new Color4(expectedOuter, expectedOuter, expectedOuter, 180)));
            }
        }

        [Test]
        public void TestShadowIsBlackEvenWhenPartiallyVisible()
        {
            var gradient = new LegacySliderBodyGradient(Color4.Yellow, Color4.White, 64);
            Assert.That(gradient.Sample(0), Is.EqualTo(default(Color4)));
            Color4 shadow = gradient.Sample(0.04f);
            Assert.That(shadow.R, Is.Zero);
            Assert.That(shadow.G, Is.Zero);
            Assert.That(shadow.B, Is.Zero);
            Assert.That(shadow.A, Is.GreaterThan(0));
        }

        // Frozen CPU ramp expectations: original IL byte endpoints, stable's six
        // breakpoints and pixel-centre sampling. Not claimed to be GPU readbacks;
        // GL line rasterisation can differ by a byte from ideal interpolation.
        [TestCase(255, 210, 0, "2696E384AB647E880FD7A8140991E4D04BD3943E16BE2849A3C929C2E8D4C7D5")]
        [TestCase(228, 228, 228, "074E0CDED738F9A877D2ACA2625B602CE6776E1EBB6C55A3D085AA3357E7E816")]
        [TestCase(227, 223, 198, "4F09E88AFE4E131E2E49AB149FB877B51FA9863687607649C1D45CCC0CC956D7")]
        [TestCase(108, 133, 122, "88C85D4CEDB57060BDCE54F8B9AAE3879812BD63D48AEBFDB2BAB66D8907A52E")]
        [TestCase(121, 43, 57, "B4A4BE8D1E41CEB2A427465E7BF5CDAA840D916D5A2830A201B46E6CD915B5D4")]
        [TestCase(0, 0, 0, "7E3E67FE41D4631F7581BACADCE074C55BD12D0D71785E98593EC6C7ED06606B")]
        [TestCase(255, 255, 255, "855A4E720373CEBDCA83BBA4BC5304D0EE869E18D0314E87AB2A4AE7C6094024")]
        public void TestCompleteTextureAt720And1080(byte r, byte g, byte b, string expectedHash)
        {
            foreach (float ratio in new[] { 1.5f, 2.25f })
            {
                var gradient = new LegacySliderBodyGradient(new Color4(r, g, b, 255), Color4.White, 32.909485f * ratio);
                byte[] bytes = new byte[LegacySliderBodyGradient.TEXTURE_WIDTH * 4];

                for (int i = 0; i < LegacySliderBodyGradient.TEXTURE_WIDTH; i++)
                {
                    Color4 colour = gradient.Sample((i + 0.5f) / LegacySliderBodyGradient.TEXTURE_WIDTH);
                    var pixel = new Rgba32(colour.R, colour.G, colour.B, colour.A);
                    bytes[i * 4] = pixel.R;
                    bytes[i * 4 + 1] = pixel.G;
                    bytes[i * 4 + 2] = pixel.B;
                    bytes[i * 4 + 3] = pixel.A;
                }

                Assert.That(Convert.ToHexString(SHA256.HashData(bytes)), Is.EqualTo(expectedHash));
            }
        }

        [Test]
        public void TestBorderColourAndAlphaArePreserved()
        {
            var border = new Color4(31, 127, 213, 93);
            var gradient = new LegacySliderBodyGradient(Color4.Red, border, 64);
            Assert.That(gradient.Sample(0.125f), Is.EqualTo(border));
        }

        [TestCase(4f, 0.0625f)]
        [TestCase(20f, 0.025f)]
        [TestCase(64f, 0.01171875f)]
        public void TestAntiAliasingUsesScreenRadiusAndClamps(float screenRadius, float expectedWidth)
        {
            var gradient = new LegacySliderBodyGradient(Color4.Red, Color4.White, screenRadius);
            Assert.That(gradient.Sample(5f / 64 - expectedWidth), Is.EqualTo(new Color4(0, 0, 0, 64)));
            Assert.That(gradient.Sample(5f / 64 + expectedWidth), Is.EqualTo(Color4.White));
            LegacySliderBodyGradient.ComputeColours(Color4.Red, out _, out var outer);
            Assert.That(gradient.Sample(3f / 16 + expectedWidth), Is.EqualTo(outer));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestRatioChangesInvalidateOnlyClassicGradient(bool customProfile)
        {
            var graphics = new DummyRenderer();
            using var texture = graphics.CreateTexture(128, 1);
            using var renderer = new LegacySliderBodyRenderer();
            // Exercise the real Configure cache without loading shaders or a GPU host.
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(Drawable).GetField("loadState", flags)!.SetValue(renderer, LoadState.Loaded);
            typeof(LegacySliderBodyRenderer).GetField("gradient", flags)!.SetValue(renderer, texture);
            FieldInfo revision = typeof(LegacySliderBodyRenderer).GetField("revision", flags)!;
            FieldInfo gradientRadius = typeof(LegacySliderBodyRenderer).GetField("gradientRadius", flags)!;
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }, 100);
            Func<float, Color4>? sample = customProfile ? _ => Color4.Red : null;

            configure(1.5f);
            int firstRevision = (int)revision.GetValue(renderer)!;
            configure(1.5f);
            Assert.That(revision.GetValue(renderer), Is.EqualTo(firstRevision), "Unchanged input must remain cached");
            configure(2.25f);
            Assert.That(revision.GetValue(renderer), Is.EqualTo(firstRevision + (customProfile ? 1 : 2)), "Geometry always changes; only classic also reuploads its gradient");
            Assert.That(gradientRadius.GetValue(renderer), Is.EqualTo(customProfile ? 20f : 45f));
            configure(1.5f);
            Assert.That(gradientRadius.GetValue(renderer), Is.EqualTo(customProfile ? 20f : 30f));

            void configure(float ratio) => Assert.That(renderer.Configure(path, Vector2.Zero, Vector2.Zero, ratio, 20,
                0, 1, Color4.Yellow, Color4.White, new Vector2(32767), sample), Is.True);
        }
    }
}
