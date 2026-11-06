// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// Plays overlapping (2B) patterns on autoplay and seeks across them, checking that every visual ordering proxy is shown exactly
    /// while its object is within its lifetime (see <see cref="OsuVisualOrdering.LifetimeViolations"/>).
    /// </summary>
    public partial class TestSceneVisualOrderingLifetimes : PlayerTestScene
    {
        private const double last_object_end = 6000;

        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        protected override bool Autoplay => true;

        protected override bool AllowBackwardsSeeks => true;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private OsuVisualOrdering ordering => ((OsuPlayfield)Player.DrawableRuleset.Playfield).VisualOrdering;

        public override void SetUpSteps()
        {
            AddStep("use triangles skin", () => skins.CurrentSkinInfo.Value = TrianglesSkin.CreateInfo().ToLiveUnmanaged());
            AddStep("enable classic mod", () => SelectedMods.Value = new Mod[] { new OsuModClassic() });
            AddStep("enable visual ordering", () => ((OsuRulesetConfigManager)RulesetConfigs.GetConfigFor(Ruleset.Value.CreateInstance()).AsNonNull()).SetValue(OsuRulesetSetting.KeepOverlapCirclesVisible, true));
            base.SetUpSteps();
            AddUntilStep("wait for gameplay", () => Player.IsLoaded && Player.GameplayClockContainer.IsRunning);
            AddStep("enable lifetime checks", () => ordering.CheckLifetimes = true);
        }

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new Beatmap<OsuHitObject>
        {
            BeatmapInfo = { Ruleset = ruleset },
            HitObjects =
            {
                // A circle inside a long slider, hit while the slider is still running.
                slider(1000, new Vector2(100, 192), 300, repeats: 0),
                new HitCircle { StartTime = 1300, Position = new Vector2(250, 192) },

                // A slider inside a slider, both with repeats.
                slider(2000, new Vector2(100, 100), 250, repeats: 2),
                slider(2300, new Vector2(150, 100), 150, repeats: 1),

                // A stack of circles on top of a slider head.
                slider(3500, new Vector2(256, 300), 200, repeats: 0),
                new HitCircle { StartTime = 3550, Position = new Vector2(256, 300) },
                new HitCircle { StartTime = 3600, Position = new Vector2(256, 300) },

                // A circle and slider at the same time.
                new HitCircle { StartTime = 5000, Position = new Vector2(400, 100) },
                slider(5000, new Vector2(400, 100), 150, repeats: 0),
            },
        };

        private static Slider slider(double startTime, Vector2 position, float length, int repeats) => new Slider
        {
            StartTime = startTime,
            Position = position,
            RepeatCount = repeats,
            Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(length, 0) }),
        };

        [Test]
        public void TestPlayThrough()
        {
            AddAssert("visual ordering is active", () => ordering.IsActive);
            AddUntilStep("play to the end", () => Player.GameplayClockContainer.CurrentTime > last_object_end);
            assertNoViolations();
        }

        [Test]
        public void TestSeeking()
        {
            AddAssert("visual ordering is active", () => ordering.IsActive);
            // Forward into the middle of objects, back across their judgements, and back to before they were alive.
            foreach (double time in new[] { 1200, 2600, 1350, 3580, 2100, 5050, 3520, 800, 5500 })
            {
                AddStep($"seek to {time}", () => Player.Seek(time));
                AddUntilStep("wait for seek", () => Player.GameplayClockContainer.CurrentTime, () => Is.EqualTo(time).Within(500));
                AddWaitStep("let objects update", 10);
                assertNoViolations();
            }
        }

        [Test]
        public void TestSeekingWithClassicSkin()
        {
            AddAssert("visual ordering is active", () => ordering.IsActive);
            // Legacy skins register each circle piece separately, and changing skin mid-play recreates and re-registers them.
            AddUntilStep("play into the first slider", () => Player.GameplayClockContainer.CurrentTime > 1100);
            AddStep("switch to classic skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddUntilStep("legacy circle pieces in use", () => Player.DrawableRuleset.Playfield.ChildrenOfType<LegacyMainCirclePiece>().Any());

            foreach (double time in new[] { 2600, 1350, 3580, 2100, 5050, 800, 5500 })
            {
                AddStep($"seek to {time}", () => Player.Seek(time));
                AddUntilStep("wait for seek", () => Player.GameplayClockContainer.CurrentTime, () => Is.EqualTo(time).Within(500));
                AddWaitStep("let objects update", 10);
                assertNoViolations();
            }
        }

        private void assertNoViolations()
            => AddAssert("no lifetime violations", () => ordering.LifetimeViolations.ToArray(), () => Is.Empty);
    }

    /// <summary>
    /// Exercises dense Aspire-style patterns with enough objects to recycle approach-circle proxies, then seeks across their lifetimes.
    /// </summary>
    public partial class TestSceneVisualOrderingPoolReuse : PlayerTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        protected override bool Autoplay => true;

        protected override bool AllowBackwardsSeeks => true;

        private OsuVisualOrdering ordering => ((OsuPlayfield)Player.DrawableRuleset.Playfield).VisualOrdering;

        public override void SetUpSteps()
        {
            AddStep("enable visual ordering", () => ((OsuRulesetConfigManager)RulesetConfigs.GetConfigFor(Ruleset.Value.CreateInstance()).AsNonNull()).SetValue(OsuRulesetSetting.KeepOverlapCirclesVisible, true));
            base.SetUpSteps();
            AddUntilStep("wait for gameplay", () => Player.IsLoaded && Player.GameplayClockContainer.IsRunning);
            AddStep("enable lifetime checks", () => ordering.CheckLifetimes = true);
        }

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            var beatmap = new Beatmap<OsuHitObject>
            {
                BeatmapInfo = { Ruleset = ruleset },
            };

            // The playfield's initial circle pool has 20 entries. Dense overlapping circles ensure that
            // later objects reuse existing drawables and assign new declaration indices to their approach proxies.
            for (int i = 0; i < 80; i++)
            {
                beatmap.HitObjects.Add(new HitCircle
                {
                    StartTime = 1000 + i * 50,
                    Position = new Vector2(256 + i % 3 * 10, 192),
                });
            }

            return beatmap;
        }

        [Test]
        public void TestApproachProxiesSurvivePoolReuseAndSeeking()
        {
            AddAssert("visual ordering is active", () => ordering.IsActive);
            AddUntilStep("play into pool reuse", () => Player.GameplayClockContainer.CurrentTime > 3000);
            AddStep("refresh all declaration indices", () =>
            {
                // Change every existing entry's key in one refresh. This deterministically covers the same
                // partially-updated container state that can occur when many pooled objects are reused together.
                var beatmap = ((DrawableRuleset<OsuHitObject>)Player.DrawableRuleset).Beatmap;

                for (int i = 0; i < beatmap.HitObjects.Count; i++)
                {
                    double shuffledAppearanceTime = i * 37 % beatmap.HitObjects.Count * 10;
                    beatmap.HitObjects[i].TimePreempt = beatmap.HitObjects[i].StartTime - shuffledAppearanceTime;
                }

                beatmap.HitObjects.Reverse();
                ordering.RefreshBeatmap();
            });
            AddWaitStep("let ordering refresh", 2);

            foreach (double time in new[] { 1200, 4000, 1500, 4500, 800, 5000 })
            {
                AddStep($"seek to {time}", () => Player.Seek(time));
                AddUntilStep("wait for seek", () => Player.GameplayClockContainer.CurrentTime, () => Is.EqualTo(time).Within(500));
                AddWaitStep("let recycled objects update", 10);
                AddAssert("no lifetime violations", () => ordering.LifetimeViolations.ToArray(), () => Is.Empty);
            }
        }
    }

    public partial class TestSceneDisabledVisualOrdering : PlayerTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();
        protected override bool Autoplay => true;

        private OsuVisualOrdering ordering => ((OsuPlayfield)Player.DrawableRuleset.Playfield).VisualOrdering;

        public override void SetUpSteps()
        {
            AddStep("disable visual ordering", () => ((OsuRulesetConfigManager)RulesetConfigs.GetConfigFor(Ruleset.Value.CreateInstance()).AsNonNull()).SetValue(OsuRulesetSetting.KeepOverlapCirclesVisible, false));
            base.SetUpSteps();
            AddUntilStep("wait for gameplay", () => Player.IsLoaded && Player.GameplayClockContainer.IsRunning);
        }

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new Beatmap<OsuHitObject>
        {
            BeatmapInfo = { Ruleset = ruleset },
            HitObjects =
            {
                new HitCircle { StartTime = 1000, Position = new Vector2(256, 192) },
                new HitCircle { StartTime = 1500, Position = new Vector2(256, 192) },
            },
        };

        [Test]
        public void TestDisabledVisualOrderingUsesHeadLogic()
        {
            AddAssert("visual ordering is inactive", () => !ordering.IsActive);
            AddUntilStep("play through", () => Player.GameplayClockContainer.CurrentTime > 2000);
            AddAssert("no lifetime violations", () => ordering.LifetimeViolations.Count == 0);
        }
    }
}
