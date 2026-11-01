// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class PlayableWorkingBeatmapTest
    {
        [Test]
        public void TestPlayableWorkingBeatmapModdedAttributes()
        {
            var ruleset = new OsuRuleset();
            var rawBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                Difficulty = new BeatmapDifficulty
                {
                    ApproachRate = 8,
                    CircleSize = 4,
                    DrainRate = 5,
                    OverallDifficulty = 7
                }
            };
            rawBeatmap.BeatmapInfo.Difficulty = rawBeatmap.Difficulty.Clone();

            var sourceWorking = new TestWorkingBeatmap(rawBeatmap);
            var mods = new Mod[] { new OsuModHardRock() };

            var playable = sourceWorking.GetPlayableBeatmap(ruleset.RulesetInfo, mods);
            // With HardRock, CS becomes 5.2, AR becomes 10, OD becomes 9.8, HP becomes 7
            Assert.That(playable.Difficulty.CircleSize, Is.GreaterThan(4f));
            Assert.That(playable.Difficulty.ApproachRate, Is.GreaterThan(8f));

            var playableWorking = new PlayableWorkingBeatmap(playable, sourceWorking, starRating: 6.5);

            // Verify modded difficulty is correctly mapped onto BeatmapInfo
            Assert.That(playableWorking.BeatmapInfo.Difficulty.CircleSize, Is.EqualTo(playable.Difficulty.CircleSize));
            Assert.That(playableWorking.BeatmapInfo.Difficulty.ApproachRate, Is.EqualTo(playable.Difficulty.ApproachRate));
            Assert.That(playableWorking.BeatmapInfo.StarRating, Is.EqualTo(6.5));

            // Verify GetPlayableBeatmap and GetBeatmap return the modded playable beatmap
            Assert.That(playableWorking.GetPlayableBeatmap(ruleset.RulesetInfo), Is.SameAs(playable));
            Assert.That(playableWorking.Beatmap, Is.SameAs(playable));
        }
    }
}
