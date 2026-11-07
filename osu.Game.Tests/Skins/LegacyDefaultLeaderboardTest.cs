// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class LegacyDefaultLeaderboardTest
    {
        [Test]
        public void TestDefaultLayoutUsesLegacyLeaderboard()
        {
            using var skin = new TestSceneSkinConfigurationLookup.UserSkinSource();
            using var layout = (DefaultSkinComponentsContainer)skin.GetDrawableComponent(
                new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, new OsuRuleset().RulesetInfo))!;

            Assert.That(layout.Children.OfType<LegacyGameplayLeaderboard>().Count(), Is.EqualTo(1));
            Assert.That(layout.Children.OfType<DrawableGameplayLeaderboard>(), Is.Empty);
        }
    }
}
