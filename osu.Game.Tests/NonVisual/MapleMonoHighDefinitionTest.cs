// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Game.Resources;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MapleMonoHighDefinitionTest
    {
        [TestCase("Light")]
        [TestCase("Regular")]
        [TestCase("Medium")]
        [TestCase("SemiBold")]
        [TestCase("Bold")]
        [TestCase("Black")]
        public void TestAsciiCompanion(string weight)
        {
            using var resources = new ResourceStore<byte[]>();
            resources.AddStore(new DllResourceStore(OsuResources.ResourceAssembly));
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), "Resources"));

            using var glyphs = new GlyphStore(resources, $"Fonts/MapleMono/MapleMono-{weight}");
            glyphs.LoadFontAsync().GetAwaiter().GetResult();

            using var hdGlyphs = new GlyphStore(resources, $"Fonts/MapleMono/MapleMono-{weight}-ASCII-HD");
            hdGlyphs.LoadFontAsync().GetAwaiter().GetResult();

            Assert.That(hdGlyphs.Baseline, Is.Not.Null, $"{weight}: HD companion failed to load");
            Assert.That(glyphs.Baseline, Is.Not.Null, $"{weight}: base font failed to load");

            // The companion's baseline must scale with its four-times-resolution glyphs.
            float ratio = hdGlyphs.Baseline!.Value / glyphs.Baseline!.Value;
            Assert.That(ratio, Is.EqualTo(4).Within(0.01f), $"{weight}: HD/base Baseline ratio should be 4");

            for (char character = ' '; character <= '~'; character++)
            {
                Assert.That(hdGlyphs.HasGlyph(character), Is.True, $"{weight}: HD companion missing glyph '{character}'");
                using var texture = glyphs.Get(character.ToString());
                Assert.That(texture, Is.Not.Null, $"{weight}: base font missing texture for '{character}'");
                using var hdTexture = hdGlyphs.Get(character.ToString());
                Assert.That(hdTexture, Is.Not.Null, $"{weight}: HD companion missing texture for '{character}'");
            }

            // CJK uses the base font rather than the ASCII companion.
            Assert.That(hdGlyphs.HasGlyph('中'), Is.False, $"{weight}: HD companion must not contain CJK");
        }
    }
}
