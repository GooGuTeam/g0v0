// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Moq;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Skinning;
using osu.Game.Tests.Resources;
using osuTK;

namespace osu.Game.Tests.Visual.Ranking
{
    [HeadlessTest]
    public partial class TestSceneResultsAudio : OsuTestScene
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TestTickPlaybackEnds(bool modern, bool missedS)
        {
            ManualClock clock = null!;
            CountingSample sample = null!;
            int completedCount = 0;

            AddStep("load accuracy circle", () =>
            {
                clock = new ManualClock();
                sample = new CountingSample();
                var skin = new Mock<ISkin>();
                skin.Setup(s => s.GetSample(It.Is<ISampleInfo>(i => i.LookupNames.Contains("Results/score-tick"))))
                    .Returns(sample);

                var score = TestResources.CreateTestScoreInfo(new OsuRuleset().RulesetInfo);
                score.Accuracy = missedS ? 0.99 : 1;
                score.Rank = missedS ? ScoreRank.A : ScoreRank.X;

                Child = new SkinProvidingContainer(skin.Object)
                {
                    Clock = new FramedClock(clock),
                    Child = new AccuracyCircle(score, true)
                    {
                        UseV2Style = modern,
                        Size = new Vector2(300),
                    },
                };
            });

            AddStep("advance to ticking", () => clock.CurrentTime = 600);
            AddAssert("tick played", () => sample.PlayCount > 0);
            AddStep("record tick count", () => completedCount = sample.PlayCount);
            AddStep("advance less than debounce interval", () => clock.CurrentTime = 601);
            AddAssert("no per-frame retrigger", () => sample.PlayCount, () => Is.EqualTo(completedCount));
            AddStep("advance beyond classic ticking window", () => clock.CurrentTime = 2000);
            AddStep("record completed playback count", () => completedCount = sample.PlayCount);
            AddStep("advance before delayed modern rank", () => clock.CurrentTime = 2500);
            AddAssert("no extended ticking", () => sample.PlayCount, () => Is.EqualTo(completedCount));
            AddStep("advance past all animations", () => clock.CurrentTime = 10000);
            AddAssert("no further ticking", () => sample.PlayCount, () => Is.EqualTo(completedCount));
            AddStep("hide panel", () => Child.Hide());
            AddStep("advance while hidden", () => clock.CurrentTime = 20000);
            AddStep("show panel", () => Child.Show());
            AddAssert("ticking does not restart", () => sample.PlayCount, () => Is.EqualTo(completedCount));
        }

        private class CountingSample : Sample
        {
            public int PlayCount { get; private set; }

            public CountingSample()
                : base("Results/score-tick")
            {
            }

            public override double Length => 0;

            protected override SampleChannel CreateChannel()
            {
                PlayCount++;
                return new SilentChannel();
            }

            private class SilentChannel : SampleChannel
            {
                public SilentChannel()
                    : base("Results/score-tick")
                {
                }

                public override bool Playing => false;
            }
        }
    }
}
