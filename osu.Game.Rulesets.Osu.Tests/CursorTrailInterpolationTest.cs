// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Osu.UI.Cursor;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class CursorTrailInterpolationTest
    {
        [TestCase(100, 10, false, 9)]
        [TestCase(100, 10, true, 8)]
        [TestCase(105, 10, false, 10)]
        [TestCase(10, 10, false, 0)]
        [TestCase(0, 10, false, 0)]
        public void TestOrdinaryMovement(float distance, float interval, bool avoid, int expected)
        {
            int count = CursorTrail.GetInterpolationCount(distance, interval, avoid, out double first);
            Assert.That(count, Is.EqualTo(expected));
            if (count > 0)
                Assert.That(first, Is.EqualTo(interval));
        }

        [TestCase(100729540f, 1f)]
        [TestCase(float.MaxValue, 0.01f)]
        public void TestAspireJumpHasBoundedWork(float distance, float interval)
        {
            int count = CursorTrail.GetInterpolationCount(distance, interval, false, out double first);
            Assert.That(count, Is.EqualTo(2048));
            Assert.That(double.IsFinite(first), Is.True);
            Assert.That(first, Is.GreaterThan(0));
        }

        [TestCase(float.PositiveInfinity, 1)]
        [TestCase(float.NaN, 1)]
        [TestCase(100, 0)]
        [TestCase(100, -1)]
        [TestCase(100, float.NaN)]
        public void TestInvalidMovement(float distance, float interval)
        {
            Assert.That(CursorTrail.GetInterpolationCount(distance, interval, false, out _), Is.Zero);
        }

        [Test]
        public void TestRetainsOnlyRingBufferTail()
        {
            int count = CursorTrail.GetInterpolationCount(100000, 10, false, out double first);
            Assert.That(count, Is.EqualTo(2048));
            Assert.That(first + (count - 1) * 10, Is.EqualTo(99990));
        }
    }
}
