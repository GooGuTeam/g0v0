// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class LegacySliderViewportTest
    {
        [Test]
        public void TestPaddingAndTruncation()
        {
            var viewport = LegacySliderViewport.FromBounds(new Vector2(100.75f, 200.75f), new Vector2(200.75f, 400.75f), 10);
            Assert.That(viewport.Origin, Is.EqualTo(new Vector2(89, 189)));
            Assert.That(viewport.Size, Is.EqualTo(new Vector2(123, 223)));
        }

        [Test]
        public void TestNegativeCoordinatesAreAsymmetric()
        {
            var viewport = LegacySliderViewport.FromBounds(new Vector2(-100, -100), new Vector2(100, 100), 10);
            Assert.That(viewport.Origin, Is.EqualTo(Vector2.Zero));
            Assert.That(viewport.Size, Is.EqualTo(new Vector2(223, 112)));
        }

        [Test]
        public void TestUnexpandedDistantEndpointDoesNotCompressBody()
        {
            var visible = LegacySliderViewport.FromBounds(new Vector2(100), new Vector2(200), 10);
            var complete = LegacySliderViewport.FromBounds(new Vector2(100), new Vector2(100000), 10);
            Assert.That(visible.ScaleFor(new Vector2(16384)), Is.EqualTo(Vector2.One));
            Assert.That(complete.ScaleFor(new Vector2(16384)).X, Is.LessThan(1));
        }

        [Test]
        public void TestNonFiniteBoundsDoNotPropagateToDrawTransform()
        {
            var viewport = LegacySliderViewport.FromBounds(Vector2.Zero, new Vector2(float.PositiveInfinity), 10);
            Assert.That(viewport.Origin, Is.EqualTo(Vector2.Zero));
            Assert.That(viewport.ScaleFor(new Vector2(16384)), Is.EqualTo(Vector2.One));
        }

        [Test]
        public void TestAxesClampIndependently()
        {
            var viewport = new LegacySliderViewport(Vector2.Zero, new Vector2(32768, 1024));
            Assert.That(viewport.ScaleFor(new Vector2(16384)), Is.EqualTo(new Vector2(0.5f, 1)));
        }
    }
}
