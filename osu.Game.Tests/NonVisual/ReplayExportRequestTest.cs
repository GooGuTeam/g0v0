// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Recording.Export;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ReplayExportRequestTest
    {
        [Test]
        public void TestAutoplayGeneratesReplay()
        {
            var ruleset = new OsuRuleset().RulesetInfo;
            var beatmap = new TestWorkingBeatmap(new TestBeatmap(ruleset));

            var request = ReplayExportRequest.FromAutoplay(beatmap, ruleset, new Mod[] { new OsuModHidden() });

            Assert.That(request.Score.Replay.Frames, Is.Not.Empty);
            Assert.That(request.Score.ScoreInfo.Mods.OfType<ModAutoplay>().Count(), Is.EqualTo(1));
            Assert.That(request.Score.ScoreInfo.Mods.OfType<OsuModHidden>().Count(), Is.EqualTo(1));
            Assert.That(request.Score.ScoreInfo.Ruleset, Is.EqualTo(ruleset));
            Assert.That(request.Score.ScoreInfo.User.Username, Is.EqualTo(@"Autoplay"));
            Assert.That(request.EndTime, Is.GreaterThan(beatmap.Beatmap.HitObjects.Last().StartTime));
        }

        [Test]
        public void TestAutoplayReplacesIncompatibleMods()
        {
            var ruleset = new OsuRuleset().RulesetInfo;
            var beatmap = new TestWorkingBeatmap(new TestBeatmap(ruleset));

            var request = ReplayExportRequest.FromAutoplay(beatmap, ruleset, new Mod[] { new OsuModRelax(), new OsuModDoubleTime() });

            Assert.That(request.Score.ScoreInfo.Mods.OfType<OsuModRelax>(), Is.Empty);
            Assert.That(request.Score.ScoreInfo.Mods.OfType<ModAutoplay>().Count(), Is.EqualTo(1));
            Assert.That(request.Rate, Is.EqualTo(1.5).Within(1e-9));
            Assert.That(request.Duration.TotalMilliseconds, Is.EqualTo((request.EndTime - request.StartTime) / 1.5).Within(1e-6));
        }

        [Test]
        public void TestExistingReplayModIsKept()
        {
            var ruleset = new OsuRuleset().RulesetInfo;
            var beatmap = new TestWorkingBeatmap(new TestBeatmap(ruleset));

            var request = ReplayExportRequest.FromAutoplay(beatmap, ruleset, new Mod[] { new OsuModCinema() });

            Assert.That(request.Score.ScoreInfo.Mods.OfType<ICreateReplayData>().Single(), Is.TypeOf<OsuModCinema>());
        }

        [Test]
        public void TestScoreWithoutReplayIsRejected()
        {
            var ruleset = new OsuRuleset().RulesetInfo;
            var beatmap = new TestWorkingBeatmap(new TestBeatmap(ruleset));

            Assert.Throws<ArgumentException>(() => ReplayExportRequest.FromScore(beatmap, new Score { Replay = null! }));
        }
    }
}
