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

            for (char character = ' '; character <= '~'; character++)
            {
                Assert.That(glyphs.GetTextureScale(character), Is.EqualTo(4), $"{weight}: {character}");
                using var texture = glyphs.Get(character.ToString());
                Assert.That(texture, Is.Not.Null);
            }

            Assert.That(glyphs.GetTextureScale('中'), Is.EqualTo(1));
        }
    }
}
