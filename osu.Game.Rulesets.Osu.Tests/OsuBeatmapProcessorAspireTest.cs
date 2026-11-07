// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class OsuBeatmapProcessorAspireTest
    {
        [Test]
        public void TestDecodedSliderEndBeyondIntegerRange()
        {
            var decoded = AspireRuntimeBeatmaps.Decode(@"256,192,2147483000,2,0,L|356:192,1,100
356,192,2147483500,1,0");
            var playable = new TestWorkingBeatmap(decoded).GetPlayableBeatmap(new OsuRuleset().RulesetInfo, Array.Empty<Mod>());
            var slider = (Slider)playable.HitObjects[0];
            var circle = (HitCircle)playable.HitObjects[1];

            Assert.That(slider.EndTime, Is.EqualTo(2147484000d));
            Assert.That(slider.EndTime, Is.GreaterThan(int.MaxValue));
            Assert.That(slider.NestedHitObjects.Count, Is.LessThan(10));
            Assert.That(slider.StackHeight, Is.Zero);
            Assert.That(circle.StackHeight, Is.EqualTo(-1));
        }

        [TestCase(-2000000000, 2000000000, 0)]
        [TestCase(-2000000000, 0, 0)]
        [TestCase(0, 2000000000, 0)]
        [TestCase(-2000000000, -1999999999, 1)]
        [TestCase(1999999999, 2000000000, 1)]
        [TestCase(1000.1, 1840.9, 1)]
        [TestCase(1000.9, 1841.1, 0)]
        [TestCase(-1000.9, -160.1, 1)]
        public void TestDecodedStackingDifference(double firstTime, double secondTime, int expectedStackHeight)
        {
            // AR5 and leniency 0.7 give an 840ms threshold. Truncate each operand,
            // not their difference, including for ordinary fractional timestamps.
            var decoded = AspireRuntimeBeatmaps.Decode(FormattableString.Invariant($@"256,192,{firstTime},1,0
256,192,{secondTime},1,0"));
            var playable = new TestWorkingBeatmap(decoded).GetPlayableBeatmap(new OsuRuleset().RulesetInfo, Array.Empty<Mod>());
            var objects = playable.HitObjects.Cast<HitCircle>().ToArray();

            Assert.That(objects, Has.Length.EqualTo(2));
            Assert.That(objects[0].StartTime, Is.EqualTo(firstTime));
            Assert.That(objects[1].StartTime, Is.EqualTo(secondTime));
            Assert.That(objects[0].StackHeight, Is.EqualTo(expectedStackHeight));
            Assert.That(objects[1].StackHeight, Is.Zero);
        }
    }
}
