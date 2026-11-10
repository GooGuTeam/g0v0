// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Objects.Drawables;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class LegacySliderBallAnimationTest
    {
        [Test]
        public void TestReversePreservesPhaseAndUsesPreviousTickCursor()
        {
            var animation = new LegacySliderBallAnimation();
            int[] ticks = new[] { 96275, 96339 };
            var samples = new Dictionary<int, int> { [96176] = 0, [96242] = 3, [96276] = 6, [96325] = 4, [96374] = 1 };

            for (int clock = 96175; clock <= 96376; clock++)
            {
                int frame = animation.Update(clock, 96176, 96375, 2, ticks, 10, 1000.0 / 60);
                if (samples.TryGetValue(clock, out int expected)) Assert.That(frame, Is.EqualTo(expected), $"clock {clock}");
            }
        }

        [Test]
        public void TestRepeatedUpdateAtBoundaryAdvancesPreviousTickCursor()
        {
            var animation = new LegacySliderBallAnimation();
            int[] ticks = new[] { 100, 164 };
            animation.Update(99, 0, 200, 2, ticks, 10, 1000.0 / 60);
            int first = animation.Update(100, 0, 200, 2, ticks, 10, 1000.0 / 60);
            Assert.That(animation.Reverse, Is.False);
            animation.Update(100, 0, 200, 2, ticks, 10, 1000.0 / 60);
            Assert.That(animation.Reverse, Is.True);
            Assert.That(first, Is.InRange(0, 9));
            animation.Update(101, 0, 200, 2, ticks, 10, 1000.0 / 60);
            Assert.That(animation.Reverse, Is.True);
        }
    }
}
