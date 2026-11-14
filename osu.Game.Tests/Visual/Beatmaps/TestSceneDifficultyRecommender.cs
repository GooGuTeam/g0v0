// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Users;

namespace osu.Game.Tests.Visual.Beatmaps
{
    public partial class TestSceneDifficultyRecommender : OsuTestScene
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestStatisticsDuringLoad(bool updateBeforeAdding)
        {
            DifficultyRecommender recommender = null!;
            TestStatisticsProvider provider = null!;
            RulesetInfo ruleset = new OsuRuleset().RulesetInfo;

            AddStep("load recommender", () =>
            {
                Clear();
                provider = new TestStatisticsProvider();
                recommender = new DifficultyRecommender(provider);
                LoadComponentAsync(recommender, loaded =>
                {
                    if (updateBeforeAdding)
                        provider.Publish(ruleset);

                    Add(loaded);
                });
            });
            AddUntilStep("recommender loaded", () => recommender.IsLoaded);
            AddStep("update loaded statistics", () =>
            {
                if (!updateBeforeAdding)
                    provider.Publish(ruleset);
            });
            AddAssert("recommendation reflects statistics", () => recommender.GetRecommendedStarRatingFor(ruleset), () => Is.EqualTo(2).Within(0.01));
        }

        private partial class TestStatisticsProvider : LocalUserStatisticsProvider
        {
            public void Publish(RulesetInfo ruleset) => UpdateStatistics(new UserStatistics { PP = 337 }, ruleset);
        }
    }
}
