// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [HeadlessTest]
    public partial class TestSceneOsuVisualOrdering : OsuTestScene
    {
        private ManualClock clock = null!;
        private OsuPlayfield playfield = null!;
        private TestSkinProvider skinProvider = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        protected override Ruleset CreateRuleset() => new OsuRuleset();

        [Test]
        public void TestPlayThrough([Values(false, true)] bool legacy)
        {
            createPlayfield(legacy);

            for (int i = 0; i < 180; i++)
            {
                double time = i * 100;
                AddStep($"advance to {time}", () => clock.CurrentTime = time);
            }

            assertProxiesValid();
        }

        [Test]
        public void TestSeeking([Values(false, true)] bool legacy)
        {
            createPlayfield(legacy);

            foreach (double time in new[] { 1200, 2600, 1350, 8000, 2100, 15000, 3520, -20000, 5500, 14000, 20000, 1500 })
            {
                AddStep($"seek to {time}", () => clock.CurrentTime = time);
                AddWaitStep("let pooled objects update", 3);
                assertProxiesValid();
            }
        }

        [Test]
        public void TestLegacyRepeatArrowProxyOwnership([Values(false, true)] bool enabled)
        {
            createPlayfield(true, enabled: enabled, singleSlider: true);
            AddStep("seek before repeat", () => clock.CurrentTime = 1400);
            AddWaitStep("load arrows", 3);
            AddAssert("legacy repeat arrows loaded", () => loadedArrows().Any());
            assertProxiesValid();
            AddAssert("arrows use the correct proxy layer", () =>
            {
                var proxies = enabled
                    ? playfield.VisualOrdering.ChildrenOfType<Container>().SelectMany(gate => gate.Children)
                    : playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>()
                               .SelectMany(slider => slider.OverlayElementContainer.ChildrenOfType<Container>().SelectMany(gate => gate.Children));
                return loadedArrows().All(arrow => proxies.Any(proxy => ReferenceEquals(original(proxy), arrow)));
            });
        }

        [Test]
        public void TestOrderingModeChangesReactivelyDuringPlayback([Values(false, true)] bool enabled)
        {
            createPlayfield(true, enabled: enabled, singleSlider: true);
            AddStep("seek into objects", () => clock.CurrentTime = 1400);
            AddWaitStep("update proxies", 3);
            assertProxiesValid();

            AddStep("toggle ordering setting", () => ((OsuRulesetConfigManager)RulesetConfigs.GetConfigFor(new OsuRuleset())!).SetValue(OsuRulesetSetting.KeepOverlapCirclesVisible, !enabled));
            AddAssert("current playfield updates its ordering mode immediately", () => playfield.VisualOrdering.IsActive, () => Is.EqualTo(!enabled));
            AddWaitStep("update proxies after toggle", 3);
            assertProxiesValid();
            AddAssert("arrows moved to new proxy layer", () =>
            {
                var proxies = !enabled
                    ? playfield.VisualOrdering.ChildrenOfType<Container>().SelectMany(gate => gate.Children)
                    : playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>()
                               .SelectMany(slider => slider.OverlayElementContainer.ChildrenOfType<Container>().SelectMany(gate => gate.Children));
                return loadedArrows().All(arrow => proxies.Any(proxy => ReferenceEquals(original(proxy), arrow)));
            });

            AddStep("seek outside all lifetimes", () => clock.CurrentTime = 30000);
            AddWaitStep("update proxies", 3);
            AddAssert("expired proxy gates are hidden", () =>
            {
                var gates = playfield.VisualOrdering.ChildrenOfType<Container>().Where(c => c.Children.Any(d => d.IsProxy));
                return gates.All(c => c.Alpha == 0);
            });
            AddStep("seek back into objects", () => clock.CurrentTime = 1400);
            AddWaitStep("reuse pooled objects", 3);
            assertProxiesValid();
        }

        [Test]
        public void TestShowHiddenSlidersWorksRegardlessOfOverlapSetting([Values(false, true)] bool keepOverlap)
        {
            AddStep("create playfield with hidden slider", () =>
            {
                var config = (OsuRulesetConfigManager)RulesetConfigs.GetConfigFor(new OsuRuleset())!;
                config.SetValue(OsuRulesetSetting.KeepOverlapCirclesVisible, keepOverlap);
                config.SetValue(OsuRulesetSetting.ShowHiddenSliders, false);

                var beatmap = new OsuBeatmap();
                var hiddenSlider = new Slider
                {
                    StartTime = 1000,
                    Position = new Vector2(256, 192),
                    Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }, expectedDistance: 1e-5),
                };
                hiddenSlider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                beatmap.HitObjects.Add(hiddenSlider);

                Child = new OsuInputManager(new OsuRuleset().RulesetInfo)
                {
                    Child = skinProvider = new TestSkinProvider(createSkin(false))
                    {
                        Child = new BeatmapContainer(beatmap)
                        {
                            Child = playfield = new OsuPlayfield
                            {
                                Clock = new FramedClock(clock = new ManualClock()),
                            },
                        },
                    },
                };

                playfield.Add(hiddenSlider);
                playfield.VisualOrdering.CheckLifetimes = true;
            });

            AddUntilStep("playfield loaded", () => playfield.IsLoaded);
            AddStep("seek into hidden slider", () => clock.CurrentTime = 1000);
            AddWaitStep("update proxies", 3);
            assertProxiesValid();
            AddAssert("hidden slider proxy gates have zero alpha", () =>
            {
                var gates = playfield.VisualOrdering.ChildrenOfType<Container>().Where(c => c.Children.Any(d => d.IsProxy));
                return gates.Any() && gates.All(c => c.Alpha == 0);
            });

            AddStep("enable show hidden sliders", () => ((OsuRulesetConfigManager)RulesetConfigs.GetConfigFor(new OsuRuleset())!).SetValue(OsuRulesetSetting.ShowHiddenSliders, true));
            AddWaitStep("update proxies", 3);
            assertProxiesValid();
            AddAssert("hidden slider proxy gates become visible", () =>
            {
                var gates = playfield.VisualOrdering.ChildrenOfType<Container>().Where(c => c.Children.Any(d => d.IsProxy));
                return gates.Any(c => c.Alpha == 1);
            });
        }

        [Test]
        public void TestPoolOverflow([Values(false, true)] bool legacy)
        {
            createPlayfield(legacy, dense: true);

            foreach (double time in new[] { 1500, 30000, 2000, -20000, 1800, 30000 })
            {
                AddStep($"seek to {time}", () => clock.CurrentTime = time);
                AddWaitStep("let excess pooled objects dispose", 10);
                assertProxiesValid();
            }
        }

        [Test]
        public void TestSkinChanges()
        {
            createPlayfield(false);

            foreach (double time in new[] { 1200, 2600, 1350, 8000, 2100, 15000, 3520, -20000, 5500 })
            {
                AddStep($"seek to {time} and change skin", () =>
                {
                    clock.CurrentTime = time;
                    skinProvider.ChangeSkin(createSkin(true));
                });
                AddWaitStep("let skin load", 3);
                assertProxiesValid();
                AddStep("restore triangles skin", () => skinProvider.ChangeSkin(createSkin(false)));
                AddWaitStep("let old skin unregister", 3);
                assertProxiesValid();
            }
        }

        private void createPlayfield(bool legacy, bool dense = false, bool enabled = true, bool singleSlider = false)
        {
            AddStep("create pooled playfield", () =>
            {
                var config = (OsuRulesetConfigManager)RulesetConfigs.GetConfigFor(new OsuRuleset())!;
                config.SetValue(OsuRulesetSetting.KeepOverlapCirclesVisible, enabled);
                config.SetValue(OsuRulesetSetting.ShowHiddenSliders, true);
                var beatmap = new OsuBeatmap();

                for (int i = 0; i < (singleSlider ? 1 : dense ? 150 : 80); i++)
                {
                    beatmap.HitObjects.Add(new Slider
                    {
                        StartTime = 1000 + i * (dense ? 1 : 180),
                        Position = new Vector2(100 + i % 4 * 60, 100 + i % 3 * 60),
                        RepeatCount = singleSlider ? 1 : i % 3,
                        Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100 + i % 3 * 25, 0) }),
                    });

                    if (singleSlider)
                        continue;

                    beatmap.HitObjects.Add(new HitCircle
                    {
                        StartTime = 1000 + i * (dense ? 1 : 180),
                        Position = new Vector2(256, 192),
                    });
                }

                foreach (var hitObject in beatmap.HitObjects)
                    hitObject.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

                Child = new OsuInputManager(new OsuRuleset().RulesetInfo)
                {
                    Child = skinProvider = new TestSkinProvider(createSkin(legacy))
                    {
                        Child = new BeatmapContainer(beatmap)
                        {
                            Child = playfield = new OsuPlayfield
                            {
                                Clock = new FramedClock(clock = new ManualClock()),
                            },
                        },
                    },
                };

                foreach (var hitObject in beatmap.HitObjects)
                    playfield.Add(hitObject);

                playfield.VisualOrdering.CheckLifetimes = true;
            });
            AddUntilStep("playfield loaded", () => playfield.IsLoaded);
            AddAssert("visual ordering matches initial setting", () => playfield.VisualOrdering.IsActive, () => Is.EqualTo(enabled));
        }

        private ISkin createSkin(bool legacy) => legacy
            ? new OsuLegacySkinTransformer(skins.GetSkin(DefaultLegacySkin.CreateInfo()))
            : new OsuTrianglesSkinTransformer(skins.GetSkin(TrianglesSkin.CreateInfo()));

        private static Drawable? original(Drawable proxy)
        {
            var property = typeof(Drawable).GetProperty("Original", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (Drawable?)property.GetValue(proxy);
        }

        private LegacyReverseArrow[] loadedArrows()
        {
            var arrows = playfield.HitObjectContainer.AliveObjects.SelectMany(s => s.ChildrenOfType<LegacyReverseArrow>());
            return arrows.Where(arrow => arrow.IsLoaded).ToArray();
        }

        private void assertProxiesValid()
        {
            AddAssert("no lifetime violations", () => playfield.VisualOrdering.LifetimeViolations.ToArray(), () => Is.Empty);
            AddAssert("loaded legacy arrows retain their ordered proxies", () =>
            {
                if (!playfield.VisualOrdering.IsActive)
                    return true;

                var proxies = playfield.VisualOrdering.ChildrenOfType<Container>().SelectMany(gate => gate.Children);
                return loadedArrows().All(arrow => proxies.Any(proxy => ReferenceEquals(original(proxy), arrow) && proxy.Alpha > 0));
            });
        }

        private partial class BeatmapContainer : Container
        {
            [Cached(typeof(IBeatmap))]
            private readonly IBeatmap beatmap;

            public BeatmapContainer(IBeatmap beatmap)
            {
                this.beatmap = beatmap;
                RelativeSizeAxes = Axes.Both;
            }
        }

        private partial class TestSkinProvider : SkinProvidingContainer
        {
            public TestSkinProvider(ISkin skin)
                : base(skin)
            {
            }

            public void ChangeSkin(ISkin skin) => SetSources(new[] { skin });
        }
    }
}
