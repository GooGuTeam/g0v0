// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [HeadlessTest]
    public partial class TestSceneVisualOrderingFrameLifetimes : OsuTestScene
    {
        private static readonly PropertyInfo original_property = typeof(Drawable).GetProperty("Original", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private ManualClock clock = null!;
        private OsuPlayfield playfield = null!;
        private readonly List<string> mismatches = new List<string>();
        private Action? afterFrame;
        private int framesChecked;
        private int enabledGatesChecked;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        protected override Ruleset CreateRuleset() => new OsuRuleset();

        [Test]
        public void TestEveryFrame([Values(false, true)] bool legacy, [Values(false, true)] bool seeking, [Values(false, true)] bool frameStable)
        {
            createPlayfield(legacy, frameStable);
            int frames = seeking ? 150 : 600;

            AddStep("start consecutive updates", () => afterFrame = () =>
            {
                checkFrame();
                if (framesChecked >= frames)
                    afterFrame = null;
                else
                    clock.CurrentTime = seeking ? new Random(framesChecked).Next(-1000, 10000) : framesChecked * 16.7;
            });
            AddUntilStep("all frames checked", () => framesChecked >= frames);
            AddAssert("enabled proxies were checked", () => enabledGatesChecked > 0);
            assertNoMismatches();
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TestLifetimeShortenedDuringUpdate(bool nested, bool expireParent)
        {
            createPlayfield(true, false);
            AddStep("seek into fade-in", () => clock.CurrentTime = 500);
            AddUntilStep("objects loaded", () => playfield.HitObjectContainer.AliveObjects.Any());
            AddStep("expire after container lifetime processing", () =>
            {
                DrawableOsuHitObject owner = nested
                    ? playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().First().HeadCircle
                    : playfield.HitObjectContainer.AliveObjects.OfType<DrawableHitCircle>().First();

                void expire(Drawable _)
                {
                    owner.OnUpdate -= expire;
                    var expired = expireParent ? owner.VisualParentHitObject! : owner;
                    expired.LifetimeEnd = owner.Time.Current;
                    Assert.That(expired.IsAlive, Is.True, "container has not processed the new lifetime yet");
                    afterFrame = () =>
                    {
                        checkFrame();
                        // Keep this exact frame available to the offscreen previewer; do not let a second
                        // lifetime pass remove the source and conceal the same-update regression.
                        ((TestPlayfield)playfield).Freeze = true;
                        afterFrame = null;
                    };
                }

                owner.OnUpdate += expire;
            });
            AddUntilStep("same frame checked", () => framesChecked > 0);
            assertNoMismatches();
        }

        [Test]
        public void TestEditorKeepAlive()
        {
            createPlayfield(false, false);
            AddStep("keep objects alive", () => typeof(Playfield).GetMethod("KeepAllAlive", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(playfield, null));
            AddStep("seek before normal preempt", () => clock.CurrentTime = -5000);
            AddStep("check expanded lifetimes", () => afterFrame = () =>
            {
                checkFrame();
                afterFrame = null;
            });
            AddUntilStep("frame checked", () => framesChecked > 0);
            AddAssert("kept-alive proxies remain enabled", () => enabledGatesChecked > 0);
            assertNoMismatches();
        }

        private void createPlayfield(bool legacy, bool frameStable)
        {
            AddStep("create pooled playfield with fixed settings", () =>
            {
                afterFrame = null;
                mismatches.Clear();
                framesChecked = 0;
                enabledGatesChecked = 0;
                ((OsuRulesetConfigManager)RulesetConfigs.GetConfigFor(new OsuRuleset())!).SetValue(OsuRulesetSetting.KeepOverlapCirclesVisible, true);
                var beatmap = new OsuBeatmap();

                for (int i = 0; i < 15; i++)
                {
                    beatmap.HitObjects.Add(new Slider
                    {
                        StartTime = 1000 + i * 200,
                        Position = new Vector2(100 + i % 4 * 50, 100 + i % 3 * 60),
                        RepeatCount = i % 4,
                        Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(200, 0) }),
                    });
                    beatmap.HitObjects.Add(new HitCircle
                    {
                        StartTime = 1100 + i * 200,
                        Position = i == 0 ? new Vector2(400, 300) : new Vector2(256, 192),
                    });
                }

                foreach (var model in beatmap.HitObjects)
                    model.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

                ISkin skin = legacy
                    ? new OsuLegacySkinTransformer(skins.GetSkin(DefaultLegacySkin.CreateInfo()))
                    : new OsuTrianglesSkinTransformer(skins.GetSkin(TrianglesSkin.CreateInfo()));

                playfield = new TestPlayfield();
                Child = new OsuInputManager(new OsuRuleset().RulesetInfo)
                {
                    Child = new SkinProvidingContainer(skin)
                    {
                        Child = new TestContainer(beatmap, () => afterFrame?.Invoke())
                        {
                            Clock = new FramedClock(clock = new ManualClock()),
                            Child = frameStable ? new FrameStabilityContainer { Child = playfield } : playfield,
                        },
                    },
                };

                foreach (var model in beatmap.HitObjects)
                    playfield.Add(model);

                playfield.VisualOrdering.CheckLifetimes = true;
            });
            AddUntilStep("playfield loaded", () => playfield.IsLoaded);
        }

        private void checkFrame()
        {
            framesChecked++;
            double now = playfield.Time.Current;

            // Include the approach-circle fallback layer, not only the globally ordered proxies.
            // Observe the end of the entire playfield update, independently of the ordering's own diagnostics.
            foreach (var gate in playfield.ChildrenOfType<Container>().Where(c => c.GetType().Name == "VisualProxy"))
            {
                if (gate.Alpha == 0)
                    continue;

                enabledGatesChecked++;
                var source = (Drawable)original_property.GetValue(gate.Child)!;
                DrawableOsuHitObject? owner = null;

                for (Drawable? ancestor = source; ancestor != null; ancestor = ancestor.Parent)
                {
                    if (ancestor is DrawableOsuHitObject hitObject)
                    {
                        owner = hitObject;
                        break;
                    }
                }

                if (owner == null)
                {
                    record($"{now}: enabled proxy has no hit object ancestor");
                    continue;
                }

                var parent = owner.VisualParentHitObject ?? owner;

                if (owner.Entry == null || !owner.IsAlive || !parent.IsAlive
                    || now < owner.LifetimeStart || now >= owner.LifetimeEnd
                    || now < parent.LifetimeStart || now >= parent.LifetimeEnd)
                {
                    record($"{now}: {owner.GetType().Name}, source={source.GetType().Name}, "
                           + $"owner={owner.LifetimeStart}-{owner.LifetimeEnd}, parent={parent.LifetimeStart}-{parent.LifetimeEnd}");
                }
            }
        }

        private void record(string message)
        {
            if (mismatches.Count < 30)
                mismatches.Add(message);
        }

        private void assertNoMismatches()
        {
            AddAssert("no enabled gates outside current lifetimes", () => mismatches.ToArray(), () => Is.Empty);
            AddAssert("no diagnostic mismatches", () => playfield.VisualOrdering.LifetimeViolations.ToArray(), () => Is.Empty);
        }

        private partial class TestPlayfield : OsuPlayfield
        {
            public bool Freeze { get; set; }

            public override bool UpdateSubTree() => Freeze || base.UpdateSubTree();

            protected override void OnNewDrawableHitObject(DrawableHitObject drawable)
            {
                base.OnNewDrawableHitObject(drawable);
                drawable.OnUpdate += _ =>
                {
                    // Exercise successful judgements as well as the ordinary missed-object path.
                    if (drawable is DrawableHitCircle && !drawable.Judged && drawable.HitObject.GetEndTime() - 10 <= drawable.Time.Current)
                        ((DrawableOsuHitObject)drawable).HitForcefully();
                };
            }
        }

        private partial class TestContainer : Container
        {
            [Cached(typeof(IBeatmap))]
            private readonly IBeatmap beatmap;

            private readonly Action check;

            public TestContainer(IBeatmap beatmap, Action check)
            {
                this.beatmap = beatmap;
                this.check = check;
                RelativeSizeAxes = Axes.Both;
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();
                check();
            }
        }
    }
}
