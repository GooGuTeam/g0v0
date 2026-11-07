// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class SliderEventGeneratorAspireTest
    {
        [TestCase(99999)]
        [TestCase(100000)]
        [TestCase(120000)]
        [TestCase(131072)]
        public void TestCapDoesNotRemapTicks(double distance)
        {
            const double velocity = 0.2;
            double spanDuration = distance / velocity;
            var events = SliderEventGenerator.Generate(1000, spanDuration, velocity, 100, distance, 2).ToArray();
            var ticks = events.Where(e => e.Type == SliderEventType.Tick).ToArray();

            Assert.That(ticks, Has.Length.EqualTo(1998));
            for (int i = 0; i < 999; i++)
            {
                double tickDistance = (i + 1) * 100;
                var forward = ticks[i];
                var reverse = ticks[^(i + 1)];
                Assert.That(forward.PathProgress * distance, Is.EqualTo(tickDistance).Within(0.000001));
                Assert.That(reverse.PathProgress, Is.EqualTo(forward.PathProgress));
                Assert.That(forward.Time, Is.EqualTo(1000 + tickDistance / velocity).Within(0.000001));
                Assert.That(reverse.Time, Is.EqualTo(1000 + 2 * spanDuration - tickDistance / velocity).Within(0.000001));
            }

            Assert.That(events.Select(e => e.Time), Is.Ordered);
            Assert.That(events.Count(e => e.Type == SliderEventType.Repeat), Is.EqualTo(1));
            Assert.That(events[^1].Type, Is.EqualTo(SliderEventType.Tail));
            Assert.That(events[^1].Time, Is.EqualTo(1000 + 2 * spanDuration));
        }

        [Test]
        public void TestCancellationDuringReverseSpan()
        {
            using var cancellation = new CancellationTokenSource();
            using var events = SliderEventGenerator.Generate(0, 600000, 0.2, 100, 120000, 2, cancellation.Token).GetEnumerator();

            while (events.MoveNext())
            {
                if (events.Current.Type != SliderEventType.Repeat)
                    continue;

                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() => events.MoveNext());
                return;
            }

            Assert.Fail("Expected a repeat before cancellation.");
        }
    }
}
