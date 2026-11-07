// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [HeadlessTest]
    public partial class TestSceneSliderTickVisualOptions : OsuTestScene
    {
        private OsuRulesetConfigManager config = null!;
        private ManualClock clock = null!;
        private DrawableSlider slider = null!;
        private TestSkinProvider skinProvider = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        protected override Ruleset CreateRuleset() => new OsuRuleset();

        [Test]
        public void TestDefaults()
        {
            AddStep("check independent lazer defaults", () =>
            {
                using var defaults = new OsuRulesetConfigManager(null, new OsuRuleset().RulesetInfo);
                Assert.That(defaults.Get<bool>(OsuRulesetSetting.LegacySliderTickFading), Is.False);
                Assert.That(defaults.Get<bool>(OsuRulesetSetting.LegacySliderTickVisibility), Is.False);
            });
        }

        [Test]
        public void TestLiveFading([Values(false, true)] bool hidden, [Values(0, 1)] int span)
        {
            createSlider(hidden, false);
            AddStep("seek into fade-in", () => clock.CurrentTime = tick(span).HitObject.StartTime - tick(span).HitObject.TimePreempt + 100);
            assertAlpha("lazer fade-in", span, 100f / 150);
            setOption(OsuRulesetSetting.LegacySliderTickFading, true);
            assertAlpha("stable fade-in refreshed on the same drawable", span, span == 0 ? 100f / 150 : 0.5f);
            AddStep("seek to 175ms into fade", () => clock.CurrentTime = tick(span).HitObject.StartTime - tick(span).HitObject.TimePreempt + 175);
            // Repeat tick preempt is 325ms: stable is still fading in at +175ms.
            AddUntilStep("stable fade after 175ms", () => tick(span).Alpha,
                () => Is.EqualTo(span == 0 ? firstSpanAlphaAt175(hidden) : 0.875f).Within(0.001));
            setOption(OsuRulesetSetting.LegacySliderTickFading, false);
            // Lazer finished fading in at +150ms; Hidden is now 25ms into its 175ms fade-out.
            AddUntilStep("lazer fade restored including Hidden", () => tick(span).Alpha,
                () => Is.EqualTo(span == 0 ? firstSpanAlphaAt175(hidden) : hidden ? 150f / 175 : 1).Within(0.001));
            if (span == 1)
            {
                AddStep("seek into Hidden fade-out", () => clock.CurrentTime = tick(span).HitObject.StartTime - 75);
                assertAlpha("lazer Hidden fade-out", span, hidden ? 75f / 175 : 1);
                setOption(OsuRulesetSetting.LegacySliderTickFading, true);
                assertAlpha("stable Hidden fade-out live refresh", span, hidden ? 75f / 125 : 1);
                setOption(OsuRulesetSetting.LegacySliderTickFading, false);
                assertAlpha("lazer Hidden fade-out restored", span, hidden ? 75f / 175 : 1);
            }
            AddStep("seek backwards into fade", () => clock.CurrentTime = tick(span).HitObject.StartTime - tick(span).HitObject.TimePreempt + 75);
            assertAlpha("fade survives backwards seek", span, 0.5f);
        }

        [Test]
        public void TestEndpointVisibility([Values(false, true)] bool hidden, [Values(false, true)] bool proxies)
        {
            createSlider(hidden, proxies);
            AddStep("seek into first tick fade", () => clock.CurrentTime = tick(0).HitObject.StartTime - tick(0).HitObject.TimePreempt + 100);
            AddAssert("all tick visual sources initially visible", () => ticks.All(t => visual(t).Alpha == 1));
            AddAssert("proxy registration matches rendering mode", () => ticks.All(t => visual(t).HasProxy == proxies));
            AddAssert("reversed final endpoint is at the head", () => slider.HitObject.EndPosition == slider.HitObject.Position);
            setOption(OsuRulesetSetting.LegacySliderTickVisibility, true);
            assertEndpointVisibility(true);

            AddStep("switch to classic skin", () => skinProvider.ChangeSkin(skins.GetSkin(DefaultLegacySkin.CreateInfo())));
            AddWaitStep("skin refresh", 3);
            assertEndpointVisibility(true);
            AddStep("switch to triangles skin", () => skinProvider.ChangeSkin(skins.GetSkin(TrianglesSkin.CreateInfo())));
            AddWaitStep("skin refresh", 3);
            assertEndpointVisibility(true);
            setOption(OsuRulesetSetting.LegacySliderTickFading, true);
            assertEndpointVisibility(true);
            setOption(OsuRulesetSetting.LegacySliderTickVisibility, false);
            assertEndpointVisibility(false);
            setOption(OsuRulesetSetting.LegacySliderTickVisibility, true);
            AddStep("seek past all ticks without input", () => clock.CurrentTime = slider.HitObject.EndTime + 1);
            AddUntilStep("hidden and visible ticks all judged", () => ticks.All(t => t.Judged));
            AddAssert("all ticks remain in nested objects", () => ticks.Length, () => Is.EqualTo(14));
            setOption(OsuRulesetSetting.LegacySliderTickVisibility, false);
            setOption(OsuRulesetSetting.LegacySliderTickFading, false);
            AddAssert("live refresh preserves existing judgements", () => ticks.All(t => t.Judged && !t.IsHit));
        }

        [Test]
        public void TestPooledReuseAndRetry([Values(false, true)] bool hidden, [Values(false, true)] bool proxies)
        {
            TickPlayfield playfield = null!;
            Slider model = null!;
            DrawableSliderTick[] previousTicks = null!;
            createSlider(hidden, proxies);
            AddStep("move slider to pooled playfield", () =>
            {
                model = slider.HitObject;
                var beatmap = new Beatmap<OsuHitObject> { HitObjects = { model } };
                skinProvider.Child = playfield = new TickPlayfield(new GameplayState(beatmap, new OsuRuleset()), hidden, true)
                {
                    Clock = new FramedClock(clock = new ManualClock { CurrentTime = 1400 }),
                };
                playfield.Add(model);
            });
            AddUntilStep("pooled slider acquired", () => playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Any());
            AddStep("capture pooled slider", () => slider = playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Single());
            setOption(OsuRulesetSetting.LegacySliderTickVisibility, true);
            assertEndpointVisibility(true);
            AddStep("retain pooled tick identities and rewind out of lifetime", () =>
            {
                previousTicks = ticks;
                clock.CurrentTime = -20000;
            });
            AddUntilStep("ticks returned to pool", () => previousTicks.All(t => t.Entry == null));
            setOption(OsuRulesetSetting.LegacySliderTickVisibility, false);
            setOption(OsuRulesetSetting.LegacySliderTickFading, true);
            AddStep("seek back into slider", () => clock.CurrentTime = 1400);
            AddUntilStep("pooled slider reacquired", () => playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Any());
            AddStep("capture reused slider", () => slider = playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Single());
            AddAssert("tick instances reused", () => ticks.All(previousTicks.Contains));
            assertEndpointVisibility(false);
            AddStep("seek repeat fade-in", () => clock.CurrentTime = tick(1).HitObject.StartTime - tick(1).HitObject.TimePreempt + 100);
            assertAlpha("pooled tick uses current stable fade setting", 1, 0.5f);
            setOption(OsuRulesetSetting.LegacySliderTickVisibility, true);
            assertEndpointVisibility(true);
            AddStep("retry removes old entries", () => playfield.Remove(model));
            AddUntilStep("retry frees old slider", () => !playfield.HitObjectContainer.AliveObjects.Any());
            AddStep("retry adds fresh entries", () =>
            {
                clock.CurrentTime = 1400;
                playfield.Add(model);
            });
            AddUntilStep("retry slider acquired", () => playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Any());
            AddStep("capture retry slider", () => slider = playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Single());
            assertEndpointVisibility(true);
        }

        [Test]
        public void TestGeometricRadiusAndNonGameplay([Values(false, true)] bool gameplay)
        {
            createSlider(false, false, gameplay);
            AddStep("place ticks at radius boundaries", () =>
            {
                var model = slider.HitObject;
                model.Scale = 0.5f; // Exactly 32px radius, so equality is representable.
                var positions = new[]
                {
                    new Vector2(31.99f, 0), new Vector2(32, 0), new Vector2(32.01f, 0),
                    new Vector2(168.01f, 0), new Vector2(168, 0), new Vector2(167.99f, 0),
                    new Vector2(0, 31.99f),
                };
                foreach (var t in ticks)
                {
                    t.HitObject.Position = model.Position + positions[Array.IndexOf(ticks, t) % positions.Length];
                    t.RefreshStateTransforms();
                }
            });
            setOption(OsuRulesetSetting.LegacySliderTickVisibility, true);
            for (int i = 0; i < 14; i++)
            {
                int index = i;
                AddStep($"seek to boundary tick {index}", () => clock.CurrentTime = ticks[index].HitObject.StartTime - ticks[index].HitObject.TimePreempt + 100);
                AddUntilStep("strict geometric radius, not progress or final reverse endpoint", () => visual(ticks[index]).Alpha,
                    () => Is.EqualTo(gameplay && index % 7 is 0 or 3 or 6 ? 0f : 1f));
            }
            setOption(OsuRulesetSetting.LegacySliderTickVisibility, false);
            assertEndpointVisibility(false);
        }

        private DrawableSliderTick[] ticks => slider.NestedHitObjects.OfType<DrawableSliderTick>().ToArray();

        private DrawableSliderTick tick(int span) => ticks.First(t => ((SliderTick)t.HitObject).SpanIndex == span);

        private static SkinnableDrawable visual(DrawableSliderTick tick) => tick.ChildrenOfType<SkinnableDrawable>().Single();

        private float firstSpanAlphaAt175(bool hidden)
        {
            // The first span retains the existing 150ms fade in either mode. Its preempt depends on AR.
            double fadeDuration = tick(0).HitObject.TimePreempt - 150;
            return hidden ? (float)((fadeDuration - 25) / fadeDuration) : 1;
        }

        private void assertAlpha(string description, int span, float expected)
            => AddUntilStep(description, () => tick(span).Alpha, () => Is.EqualTo(expected).Within(0.001));

        private void assertEndpointVisibility(bool enabled)
        {
            foreach (int index in new[] { 0, 3, 6, 7, 10, 13 })
            {
                AddStep($"seek into tick {index} fade", () => clock.CurrentTime = ticks[index].HitObject.StartTime - ticks[index].HitObject.TimePreempt + 100);
                AddUntilStep("only ticks inside the two geometric endpoints are suppressed", () => visual(ticks[index]).Alpha,
                    () => Is.EqualTo(enabled && index % 7 is 0 or 6 ? 0f : 1f));
                AddAssert("judging drawable remains visible and alive", () => ticks[index].Alpha > 0 && ticks[index].IsAlive);
            }
        }

        private void setOption(OsuRulesetSetting setting, bool enabled)
        {
            AddStep($"{setting} = {enabled}", () => config.SetValue(setting, enabled));
            AddWaitStep("apply live config", 2);
        }

        private void createSlider(bool hidden, bool proxies, bool gameplay = true)
        {
            AddStep("create slider", () =>
            {
                config = (OsuRulesetConfigManager)RulesetConfigs.GetConfigFor(new OsuRuleset())!;
                config.SetValue(OsuRulesetSetting.LegacySliderTickFading, false);
                config.SetValue(OsuRulesetSetting.LegacySliderTickVisibility, false);
                config.SetValue(OsuRulesetSetting.KeepOverlapCirclesVisible, proxies);
                var model = new Slider
                {
                    StartTime = 2000,
                    Position = new Vector2(100, 100),
                    StackHeight = 2,
                    RepeatCount = 1,
                    Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(200, 0) }),
                };
                model.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { SliderMultiplier = 1, SliderTickRate = 4, CircleSize = 5 });
                var beatmap = new Beatmap<OsuHitObject> { HitObjects = { model } };
                var mod = new OsuModHidden();
                if (hidden)
                    mod.ApplyToBeatmap(beatmap);
                slider = new DrawableSlider(model);
                if (hidden)
                    mod.ApplyToDrawableHitObject(slider);
                var playfield = new TickPlayfield(gameplay ? new GameplayState(beatmap, new OsuRuleset()) : null)
                {
                    Clock = new FramedClock(clock = new ManualClock { CurrentTime = 1100 }),
                };
                playfield.Add(slider);
                Child = new OsuInputManager(new OsuRuleset().RulesetInfo)
                {
                    Child = skinProvider = new TestSkinProvider(skins.GetSkin(TrianglesSkin.CreateInfo())) { Child = playfield }
                };
            });
            AddUntilStep("slider loaded", () => slider.IsLoaded);
            AddStep("load all tick visuals", () => clock.CurrentTime = 5999);
            AddUntilStep("all ticks loaded", () => ticks.Length == 14 && ticks.All(t => t.IsLoaded));
            AddStep("rewind before ticks", () => clock.CurrentTime = 1100);
        }

        private partial class TestSkinProvider : SkinProvidingContainer
        {
            public TestSkinProvider(ISkin skin)
                : base(skin)
            {
            }

            public void ChangeSkin(ISkin skin) => SetSources(new[] { skin });
        }

        private partial class TickPlayfield : Playfield
        {
            [Cached]
            public OsuVisualOrdering VisualOrdering { get; } = new OsuVisualOrdering();

            private readonly GameplayState? gameplay;
            private readonly OsuModHidden? hidden;

            public TickPlayfield(GameplayState? gameplay, bool hidden = false, bool pooled = false)
            {
                this.gameplay = gameplay;
                this.hidden = hidden ? new OsuModHidden() : null;
                if (pooled)
                {
                    RegisterPool<Slider, DrawableSlider>(1);
                    RegisterPool<SliderTick, DrawableSliderTick>(14);
                    RegisterPool<SliderHeadCircle, DrawableSliderHead>(1);
                    RegisterPool<SliderTailCircle, DrawableSliderTail>(1);
                    RegisterPool<SliderRepeat, DrawableSliderRepeat>(1);
                }

                AddRangeInternal(new Drawable[] { HitObjectContainer, VisualOrdering });
            }

            protected override void OnNewDrawableHitObject(DrawableHitObject drawableHitObject)
            {
                base.OnNewDrawableHitObject(drawableHitObject);
                if (drawableHitObject is DrawableSlider)
                    hidden?.ApplyToDrawableHitObject(drawableHitObject);
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                if (gameplay != null)
                    dependencies.Cache(gameplay);
                return dependencies;
            }
        }
    }
}
