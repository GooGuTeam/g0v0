// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Osu.Tests
{
    [HeadlessTest]
    public partial class TestSceneDrainingHealthProcessorAspire : OsuTestScene
    {
        private ManualClock clock = null!;
        private DrainingHealthProcessor processor = null!;
        private double healthAtLastStart;

        [Test]
        public void TestDecodedLastEndAndRewind(
            [Values(false, true)] bool slider,
            [Values(false, true)] bool classic,
            [Values(0, 1, 2)] int scoreVersion)
        {
            AddStep("decode overlapping objects and apply health", () =>
            {
                var mods = AspireRuntimeBeatmaps.Mods(classic, scoreVersion);
                var beatmap = new TestWorkingBeatmap(AspireRuntimeBeatmaps.Decode(slider
                        ? "256,192,1000,2,0,L|656:192,1,400\n64,64,2000,1,0"
                        : "256,192,1000,8,0,5000\n64,64,2000,1,0"))
                    .GetPlayableBeatmap(new OsuRuleset().RulesetInfo, mods);

                Assert.That(beatmap.HitObjects[0].GetEndTime(), Is.EqualTo(5000));
                Assert.That(beatmap.HitObjects[1].GetEndTime(), Is.EqualTo(2000));
                processor = classic ? new OsuLegacyHealthProcessor(1000) : new OsuHealthProcessor(1000);
                processor.Clock = new FramedClock(clock = new ManualClock { CurrentTime = 1000 });
                processor.ApplyBeatmap(beatmap);
                Assert.That(processor.DrainRate, Is.GreaterThan(0));
                Child = processor;
            });

            setTime(2000);
            AddStep("remember health at last start", () => healthAtLastStart = processor.Health.Value);
            setTime(2500);
            AddAssert("drain continues during earlier long object", () => processor.Health.Value,
                () => Is.EqualTo(healthAtLastStart - 500 * processor.DrainRate).Within(1e-8));
            setTime(2000);
            AddAssert("rewind restores that drain", () => processor.Health.Value, () => Is.EqualTo(healthAtLastStart).Within(1e-8));
            setTime(5000);
            AddStep("reset health at maximum end", () => processor.Health.Value = 0.5);
            setTime(5500);
            AddAssert("no drain past maximum end", () => processor.Health.Value, () => Is.EqualTo(0.5));
            setTime(4900);
            AddAssert("rewind only restores in-range drain", () => processor.Health.Value,
                () => Is.EqualTo(0.5 + 100 * processor.DrainRate).Within(1e-8));
            AddStep("leave less health than next drain", () => processor.Health.Value = processor.DrainRate);
            setTime(5000);
            AddAssert("drained to zero", () => processor.Health.Value, () => Is.Zero);
            AddAssert("zero HP alone does not immediately fail", () => !processor.HasFailed);
        }

        private void setTime(double time) => AddStep($"set time {time}", () => clock.CurrentTime = time);
    }
}
