// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Osu.Tests
{
    [HeadlessTest]
    public partial class TestSceneDrawableSpinnerAspire : OsuManualInputManagerTestScene
    {
        private ManualClock source = null!;
        private DrawableSpinner spinner = null!;

        [Test]
        public void TestDecodedZeroRequiredReceivesGreatWithoutInput(
            [Values(900, 1000, 1100)] int endTime,
            [Values(false, true)] bool classic,
            [Values(0, 1, 2)] int scoreVersion)
        {
            AddStep("decode and load zero-required spinner", () =>
            {
                var mods = AspireRuntimeBeatmaps.Mods(classic, scoreVersion);
                var beatmap = new TestWorkingBeatmap(AspireRuntimeBeatmaps.Decode($"256,192,1000,8,0,{endTime}"))
                    .GetPlayableBeatmap(new OsuRuleset().RulesetInfo, mods);
                var hitObject = (Spinner)beatmap.HitObjects.Single();
                Assert.That(hitObject.Duration, Is.EqualTo(System.Math.Max(0, endTime - 1000)));
                Assert.That(hitObject.SpinsRequired, Is.Zero);
                Assert.That(hitObject.LegacyHalfSpinsRequired, Is.Zero);
                Assert.That(hitObject.ClassicSpinnerBehaviour, Is.EqualTo(classic));
                Assert.That(hitObject.LegacySpinnerScoring, Is.EqualTo(scoreVersion != 0));

                var playfield = new TestPlayfield
                {
                    Clock = new FramedClock(source = new ManualClock { CurrentTime = 999 })
                };
                playfield.Add(spinner = new DrawableSpinner(hitObject));
                Child = playfield;
            });
            AddAssert("not judged before start", () => !spinner.Result.HasResult);
            AddStep("advance past decoded end", () => source.CurrentTime = spinner.HitObject.EndTime + 1);
            AddUntilStep("spinner judged", () => spinner.Result.HasResult);
            AddAssert("no input rotation", () => spinner.Result.TotalRotation, () => Is.Zero);
            AddAssert("zero-required spinner receives 300", () => spinner.Result.Type, () => Is.EqualTo(HitResult.Great));
        }

        private partial class TestPlayfield : Playfield
        {
        }
    }
}
