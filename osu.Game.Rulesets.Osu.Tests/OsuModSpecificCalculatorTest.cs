// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class OsuModSpecificCalculatorTest : DifficultyCalculatorTest
    {
        protected override string ResourceAssembly => "osu.Game.Rulesets.Osu.Tests";

        [Test]
        public void TestRulesetUsesRequestedCalculators()
        {
            var beatmap = GetBeatmap("diffcalc-test");
            var ruleset = new OsuRuleset();

            Assert.That(ruleset.CreateDifficultyCalculator(beatmap), Is.TypeOf<OsuDifficultyCalculator>());
            Assert.That(ruleset.CreateModSpecificDifficultyCalculator(beatmap), Is.TypeOf<OsuModSpecificDifficultyCalculator>());
            Assert.That(ruleset.CreatePerformanceCalculator(), Is.TypeOf<OsuPerformanceCalculator>());
            Assert.That(ruleset.CreateModSpecificPerformanceCalculator(), Is.TypeOf<OsuModSpecificPerformanceCalculator>());
        }

        [Test]
        public void TestModSpecificDifficultyMatchesUpstreamWithoutRelax()
        {
            var beatmap = GetBeatmap("diffcalc-test");

            var upstream = new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap).Calculate();
            var modSpecific = CreateDifficultyCalculator(beatmap).Calculate();

            Assert.That(modSpecific, Is.EqualTo(upstream).UsingPropertiesComparer());
        }

        [Test]
        public void TestRelaxDifficultyUsesDedicatedRelaxSkill()
        {
            var beatmap = GetBeatmap("diffcalc-test");
            var mods = new[] { new OsuModRelax() };

            var upstream = (OsuDifficultyAttributes)new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap).Calculate(mods);
            var modSpecific = (OsuDifficultyAttributes)CreateDifficultyCalculator(beatmap).Calculate(mods);
            var noMods = (OsuDifficultyAttributes)CreateDifficultyCalculator(beatmap).Calculate();

            Assert.That(upstream.SpeedDifficulty, Is.Zero);
            Assert.That(modSpecific.SpeedDifficulty, Is.GreaterThan(0));
            Assert.That(modSpecific.AimDifficulty, Is.GreaterThan(0));
            Assert.That(modSpecific.AimDifficulty, Is.Not.EqualTo(upstream.AimDifficulty));
            Assert.That(modSpecific.StarRating, Is.Not.EqualTo(upstream.StarRating));

            double expectedSliderFactor = noMods.SliderFactor * noMods.AimDifficulty / modSpecific.AimDifficulty;
            Assert.That(modSpecific.SliderFactor, Is.EqualTo(expectedSliderFactor).Within(CHECK_PRECISION));
        }

        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
            => new OsuModSpecificDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap);

        protected override Ruleset CreateRuleset() => new OsuRuleset();
    }
}
