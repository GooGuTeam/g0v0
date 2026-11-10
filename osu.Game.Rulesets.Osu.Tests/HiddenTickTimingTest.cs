// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class HiddenTickTimingTest
    {
        [Test]
        public void TestLazerRepeatTickFading()
        {
            var tick = new SliderTick { SpanIndex = 1, SpanStartTime = 5000, StartTime = 5100 };
            tick.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            double fadeIn = DrawableSliderTick.GetFadeInDuration(tick, false);
            Assert.That(fadeIn, Is.EqualTo(150));
            Assert.That(OsuModHidden.GetTickFadeOutParameters(tick, fadeIn).Duration, Is.EqualTo(100));
        }

        [TestCase(0, 100)]
        [TestCase(0, 4000)]
        [TestCase(1, 100)]
        [TestCase(1, 4000)]
        [TestCase(2, 100)]
        public void TestFadeStartsAfterSpanSpecificFadeIn(int span, double offset)
        {
            var tick = new SliderTick { SpanIndex = span, SpanStartTime = 5000, StartTime = 5000 + offset };
            tick.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            double fadeIn = span == 0 ? 150 : 200;
            Assert.That(DrawableSliderTick.GetFadeInDuration(tick, true), Is.EqualTo(fadeIn));
            double expectedStart = System.Math.Max(tick.StartTime - tick.TimePreempt + fadeIn, tick.StartTime - 1000);
            var fade = OsuModHidden.GetTickFadeOutParameters(tick, fadeIn);
            Assert.That(fade.StartTime, Is.EqualTo(expectedStart));
            Assert.That(fade.StartTime + fade.Duration, Is.EqualTo(tick.StartTime));
            if (span > 0 && offset == 100)
                Assert.That(fade.Duration, Is.EqualTo(50));
        }
    }
}
