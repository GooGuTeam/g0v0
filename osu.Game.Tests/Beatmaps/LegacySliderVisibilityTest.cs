// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class LegacySliderVisibilityTest
    {
        [TestCase(null, false)]
        [TestCase(0, false)]
        [TestCase(200, false)]
        [TestCase(0.1, false)]
        [TestCase(0.001, false)]
        [TestCase(0.00001, true)]
        [TestCase(-1, true)]
        public void TestStableLengthTrimming(double? length, bool hidden)
        {
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0), new Vector2(200, 0) }, length);
            Assert.That(path.LegacyTrimRemovesAllSegments, Is.EqualTo(hidden));
        }

        [Test]
        public void TestLengthEditsInvalidateVisibility()
        {
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(200, 0) }, 0.00001);
            Assert.That(path.LegacyTrimRemovesAllSegments, Is.True);
            path.ExpectedDistance.Value = 200;
            Assert.That(path.LegacyTrimRemovesAllSegments, Is.False);
            path.ExpectedDistance.Value = 0.00001;
            Assert.That(path.LegacyTrimRemovesAllSegments, Is.True);
        }
    }
}
