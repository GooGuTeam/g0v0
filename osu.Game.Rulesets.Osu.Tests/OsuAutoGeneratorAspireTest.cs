// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class OsuAutoGeneratorAspireTest
    {
        [Test]
        public void TestDecodedRequirementBoundaries(
            [Values(0, 5, 10)] int overallDifficulty,
            [Values(false, true)] bool classic,
            [Values(0, 1, 2)] int scoreVersion,
            [Values(-1, 0, 1)] int boundaryOffset,
            [Values(1, 2)] int halfSpins)
        {
            double rps = overallDifficulty switch
            {
                0 => 1.5,
                5 => 2.5,
                _ => 3.75
            };
            int duration = (int)Math.Ceiling(halfSpins * 1000 / (rps * 2)) + boundaryOffset;
            var mods = AspireRuntimeBeatmaps.Mods(classic, scoreVersion);
            var playable = new TestWorkingBeatmap(AspireRuntimeBeatmaps.Decode($"256,192,1000,8,0,{1000 + duration}", overallDifficulty))
                .GetPlayableBeatmap(new OsuRuleset().RulesetInfo, mods);
            var spinner = (Spinner)playable.HitObjects.Single();
            var replay = new OsuAutoGenerator(playable, mods).Generate();
            var frames = replay.Frames.Cast<OsuReplayFrame>().ToArray();
            bool requiresInput = classic || scoreVersion != 0 ? spinner.LegacyHalfSpinsRequired > 0 : spinner.SpinsRequired > 0;

            Assert.That(frames.All(f => double.IsFinite(f.Time) && float.IsFinite(f.Position.X) && float.IsFinite(f.Position.Y)), Is.True);
            Assert.That(frames.Select(f => f.Time), Is.Ordered);
            Assert.That(frames.Length, Is.LessThan(300));
            Assert.That(frames.Any(f => f.Actions.Count > 0), Is.EqualTo(requiresInput));

            if (requiresInput)
            {
                Assert.That(frames.Single(f => f.Time == spinner.StartTime).Actions, Is.Not.Empty);
                Assert.That(frames.Where(f => f.Time > spinner.StartTime && f.Time <= spinner.EndTime).All(f => f.Actions.Count > 0), Is.True);
                Assert.That(frames.Single(f => f.Time == spinner.EndTime).Actions, Is.Not.Empty);
                Assert.That(frames.Last().Actions, Is.Empty);
            }
        }
    }
}
