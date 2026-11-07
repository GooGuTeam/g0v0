// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
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
using osu.Game.Rulesets.Osu.Skinning;
using osu.Game.Rulesets.Osu.Skinning.Argon;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [HeadlessTest]
    public partial class TestSceneHiddenVisualFeedback : OsuTestScene
    {
        private ManualClock clock = null!;
        private OsuRulesetConfigManager config = null!;
        private FeedbackPlayfield playfield = null!;
        private TestSkinProvider provider = null!;
        private TestHidden hidden = null!;
        private OsuHitObject model = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        protected override Ruleset CreateRuleset() => new OsuRuleset();

        private DrawableOsuHitObject drawable => playfield.HitObjectContainer.AliveObjects.OfType<DrawableOsuHitObject>().Single();
        private DrawableHitCircle circle => drawable is DrawableSlider slider ? slider.HeadCircle : (DrawableHitCircle)drawable;

        [Test]
        public void TestDefaultsAndProductionTiming()
        {
            create("slider", 0);
            AddAssert("feedback and tick preferences default off", () => !config.Get<bool>(OsuRulesetSetting.HiddenHitFeedback)
                && !config.Get<bool>(OsuRulesetSetting.LegacySliderTickFading) && !config.Get<bool>(OsuRulesetSetting.LegacySliderTickVisibility));
            AddStep("seek halfway into Hidden head fade-in", () => clock.CurrentTime = 1040);
            AddAssert("head fade-in is beatmap-adjusted", () => effectiveAlpha(feedbackTarget()), () => Is.EqualTo(0.5f).Within(0.01));
            AddAssert("slider keeps ordinary fade-in duration", () => model.TimeFadeIn, () => Is.EqualTo(400));
            AddStep("seek halfway into Hidden head fade-out", () => clock.CurrentTime = 1460);
            AddAssert("head fade-out", () => effectiveAlpha(feedbackTarget()), () => Is.EqualTo(0.5f).Within(0.01));
            AddAssert("slider body remains visible", () => effectiveAlpha(((DrawableSlider)drawable).Body) > 0);
        }

        [Test]
        public void TestSuccessfulHit([Values(0, 1, 2, 3)] int skin, [Values(false, true)] bool head, [Values(-190, -150, 0, 140)] int offset)
        {
            create(head ? "slider" : "circle", skin, approachRate: 11);
            AddStep("seek to hit time", () => clock.CurrentTime = 2000 + offset);
            int resultCount = 0;
            AddStep("observe judgements", () => circle.OnNewResult += (_, _) => resultCount++);
            AddStep("successful user hit", () => circle.HitArea.Hit());
            AddAssert("successful judgement", () => circle.IsHit);
            AddStep("sample hit animation", () => clock.CurrentTime = 2000 + offset + 60);
            float defaultAlpha = 0;
            AddStep("record default feedback", () => defaultAlpha = effectiveAlpha(feedbackTarget()));
            AddAssert("default Hidden alpha follows pre-hit fade", () => circle.Alpha, () => Is.EqualTo(offset == -190 ? 4f / 9 : 0).Within(0.01));
            setOption(OsuRulesetSetting.HiddenHitFeedback, true);
            AddUntilStep("skin feedback has effective visible alpha", () => effectiveAlpha(feedbackTarget()) > 0.1f);
            if (skin < 2)
                AddAssert("legacy child retains 240ms fade", () => effectiveAlpha(feedbackTarget()), () => Is.EqualTo(0.75f).Within(0.01));
            AddAssert("approach circle not revealed", () => effectiveAlpha(circle.ApproachCircle), () => Is.Zero);
            AddAssert("actual judgement time retained", () => circle.HitStateUpdateTime, () => Is.EqualTo(2000 + offset));
            setOption(OsuRulesetSetting.HiddenHitFeedback, false);
            AddUntilStep("live disable restores lazer", () => effectiveAlpha(feedbackTarget()), () => Is.EqualTo(defaultAlpha).Within(0.01));
            setOption(OsuRulesetSetting.HiddenHitFeedback, true);
            AddUntilStep("live enable restores current animation", () => effectiveAlpha(feedbackTarget()) > 0.1f);
            setOption(OsuRulesetSetting.HitAnimations, false);
            AddUntilStep("disabled hit animations suppress feedback", () => effectiveAlpha(feedbackTarget()), () => Is.Zero);
            AddStep("advance with animations disabled", () => clock.CurrentTime += 100);
            AddAssert("refresh did not replay judgement", () => resultCount, () => Is.EqualTo(1));
            AddAssert("no delayed reappearance", () => !playfield.HitObjectContainer.AliveObjects.Any() || effectiveAlpha(feedbackTarget()) == 0);
        }

        [Test]
        public void TestFirstObjectAndApproachOnly([Values(false, true)] bool first, [Values(false, true)] bool approachOnly, [Values(false, true)] bool head)
        {
            create(head ? "slider" : "circle", 0, first, approachOnly);
            AddStep("seek after Hidden fade", () => clock.CurrentTime = 1700);
            AddAssert("beatmap stage applied to head or circle", () => circle.HitObject.TimeFadeIn, () => Is.EqualTo(480));
            AddAssert("existing approach policy", () => effectiveAlpha(circle.ApproachCircle) > 0, () => Is.EqualTo(first));
            AddAssert("existing pre-hit policy", () => effectiveAlpha(feedbackTarget()) > 0, () => Is.EqualTo(approachOnly));
            setOption(OsuRulesetSetting.HiddenHitFeedback, true);
            AddAssert("option does not reveal pre-hit circle", () => effectiveAlpha(feedbackTarget()) > 0, () => Is.EqualTo(approachOnly));
            AddStep("hit", () => { clock.CurrentTime = 2000; });
            AddStep("click", () => circle.HitArea.Hit());
            AddStep("sample", () => clock.CurrentTime = 2060);
            AddUntilStep("feedback visible", () => effectiveAlpha(feedbackTarget()), () => Is.EqualTo(0.75f).Within(0.01));
            AddAssert("hit hides even first approach", () => effectiveAlpha(circle.ApproachCircle), () => Is.Zero);
            setOption(OsuRulesetSetting.HiddenHitFeedback, false);
            AddUntilStep("approach-only feedback unchanged", () => effectiveAlpha(feedbackTarget()), () => Is.EqualTo(approachOnly ? 0.75f : 0).Within(0.01));
        }

        [Test]
        public void TestMiss([Values(false, true)] bool first, [Values(false, true)] bool head)
        {
            create(head ? "slider" : "circle", 0, first);
            setOption(OsuRulesetSetting.HiddenHitFeedback, true);
            AddStep("miss", () => clock.CurrentTime = 2250);
            AddUntilStep("miss judged", () => circle.Judged && !circle.IsHit);
            AddAssert("miss not revealed", () => effectiveAlpha(feedbackTarget()), () => Is.Zero);
        }

        [Test]
        public void TestCirclePoolSeekRetryAndSkinSwitch()
        {
            DrawableHitCircle previous = null!;
            create("circle", 0);
            setOption(OsuRulesetSetting.HiddenHitFeedback, true);
            AddStep("hit time", () => clock.CurrentTime = 2000);
            AddStep("hit", () => circle.HitArea.Hit());
            AddStep("sample", () => clock.CurrentTime = 2060);
            foreach (int skin in new[] { 1, 2, 3, 0 })
            {
                AddStep($"skin {skin}", () => provider.ChangeSkin(makeSkin(skin)));
                AddWaitStep("skin changes settle", 4);
                AddAssert("effective feedback survives skin replacement", () => effectiveAlpha(feedbackTarget()) > 0.1f);
                AddAssert("no approach flash", () => effectiveAlpha(circle.ApproachCircle), () => Is.Zero);
            }
            AddStep("rewind before hit", () => clock.CurrentTime = 1700);
            AddUntilStep("judgement reverted", () => !circle.Judged);
            AddAssert("pre-hit Hidden remains hidden", () => effectiveAlpha(feedbackTarget()), () => Is.Zero);
            AddStep("leave lifetime", () => { previous = circle; clock.CurrentTime = -20000; });
            AddUntilStep("returned to pool", () => previous.Entry == null);
            setOption(OsuRulesetSetting.HiddenHitFeedback, false);
            AddStep("re-enter", () => clock.CurrentTime = 1700);
            AddUntilStep("same pooled circle", () => playfield.HitObjectContainer.AliveObjects.Contains(previous));
            AddStep("retry remove", () => playfield.Remove(model));
            AddUntilStep("freed for retry", () => previous.Entry == null);
            setOption(OsuRulesetSetting.HiddenHitFeedback, true);
            AddStep("retry add", () => playfield.Add(model));
            AddUntilStep("retry ready", () => playfield.HitObjectContainer.AliveObjects.Any());
            AddStep("retry hit time", () => clock.CurrentTime = 2000);
            AddStep("retry hit", () => circle.HitArea.Hit());
            AddStep("retry sample", () => clock.CurrentTime = 2060);
            AddUntilStep("retry uses current setting", () => effectiveAlpha(feedbackTarget()) > 0.1f);
        }

        [Test]
        public void TestSpinnerCallbackLifetime([Values(false, true)] bool approachOnly)
        {
            DrawableSpinner spinner = null!;
            create("spinner", 0, approachOnly: approachOnly);
            AddStep("capture spinner", () => spinner = (DrawableSpinner)drawable);
            AddAssert("spinner beatmap timing applied", () => spinner.HitObject.TimeFadeIn, () => Is.EqualTo(480));
            AddStep("refresh repeatedly", () => { for (int i = 0; i < 30; i++) spinner.RefreshStateTransforms(); });
            AddAssert("one drawable-owned skin callback", () => callbacks(spinner).Length, () => Is.EqualTo(1));
            AddAssert("callback has no mod closure", () => callbacks(spinner).Single().Target, () => Is.SameAs(spinner));
            foreach (int skin in new[] { 0, 1, 2, 3, 0 })
            {
                AddStep($"switch spinner skin {skin}", () => provider.ChangeSkin(makeSkin(skin)));
                AddWaitStep("skin refresh", 4);
                AddAssert("callback count stable", () => callbacks(spinner).Length, () => Is.EqualTo(1));
                if (skin < 2)
                    AddAssert("legacy approach actually exists", () => (spinner.Body.Drawable as IHasApproachCircle)?.ApproachCircle != null);
                if (skin == 0)
                    AddAssert("old background retained", () => spinner.Body.ChildrenOfType<Sprite>().Any(d => d.Texture != null && effectiveAlpha(d) > 0));
                AddAssert("replacement approach hidden", () => (spinner.Body.Drawable as IHasApproachCircle)?.ApproachCircle is not Drawable ac || effectiveAlpha(ac) == 0);
                AddAssert("spinner body not suppressed", () => effectiveAlpha(spinner.Body) > 0);
            }
            AddStep("spinner judgement time", () => clock.CurrentTime = 2900);
            AddStep("judge spinner", () => spinner.MissForcefully());
            AddUntilStep("spinner judged", () => spinner.Judged);
            AddStep("rewind judgement", () => clock.CurrentTime = 1800);
            AddUntilStep("spinner reverted", () => !spinner.Judged);
            AddAssert("one callback after judgement and rewind", () => callbacks(spinner).Length, () => Is.EqualTo(1));
            AddStep("return spinner", () => clock.CurrentTime = -20000);
            AddUntilStep("spinner free", () => spinner.Entry == null);
            AddStep("reuse without Hidden policy", () => { playfield.UseHidden = false; clock.CurrentTime = 1800; });
            AddUntilStep("same spinner reused", () => playfield.HitObjectContainer.AliveObjects.Contains(spinner));
            AddStep("replace with old skin", () => provider.ChangeSkin(makeSkin(0)));
            AddWaitStep("skin settles", 4);
            AddAssert("old mod policy not retained", () => effectiveAlpha(((IHasApproachCircle)spinner.Body.Drawable!).ApproachCircle!) > 0);
            AddAssert("still exactly one callback", () => callbacks(spinner).Length, () => Is.EqualTo(1));
        }

        private static Delegate[] callbacks(DrawableSpinner spinner) => ((Delegate?)typeof(SkinReloadableDrawable)
            .GetField("OnSkinChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(spinner.Body))?.GetInvocationList() ?? Array.Empty<Delegate>();

        private Drawable feedbackTarget() => circle.CirclePiece.Drawable is TestLegacyCircle legacy ? legacy.BaseCircle : circle.CirclePiece.Drawable!;

        private static float effectiveAlpha(Drawable drawable)
        {
            float alpha = 1;
            for (Drawable? d = drawable; d != null; d = d.Parent)
                alpha *= d.Alpha;
            return alpha;
        }

        private void setOption(OsuRulesetSetting setting, bool value)
        {
            AddStep($"{setting}={value}", () => config.SetValue(setting, value));
            AddWaitStep("refresh transforms", 3);
        }

        private ISkin makeSkin(int variant) => new TestSkin(new OsuLegacySkinTransformer(skins.GetSkin(DefaultLegacySkin.CreateInfo())), variant);

        private void create(string kind, int skin, bool first = false, bool approachOnly = false, float approachRate = 5)
        {
            AddStep("create production-stage Hidden fixture", () =>
            {
                config = (OsuRulesetConfigManager)RulesetConfigs.GetConfigFor(new OsuRuleset())!;
                config.SetValue(OsuRulesetSetting.HiddenHitFeedback, false);
                config.SetValue(OsuRulesetSetting.HitAnimations, true);
                config.SetValue(OsuRulesetSetting.KeepOverlapCirclesVisible, false);
                model = kind switch
                {
                    "slider" => new Slider { Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(200, 0) }) },
                    "spinner" => new Spinner { EndTime = 3000 },
                    _ => new HitCircle(),
                };
                model.StartTime = 2000;
                model.Position = new Vector2(256, 192);
                model.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { ApproachRate = approachRate, OverallDifficulty = 0 });
                hidden = new TestHidden();
                hidden.SetFirstVisibility(first);
                hidden.OnlyFadeApproachCircles.Value = approachOnly;
                hidden.ApplyToBeatmap(new Beatmap<OsuHitObject> { HitObjects = { model } });
                playfield = new FeedbackPlayfield(hidden) { Clock = new FramedClock(clock = new ManualClock { CurrentTime = 1800 }) };
                playfield.Add(model);
                Child = new OsuInputManager(new OsuRuleset().RulesetInfo)
                {
                    Child = provider = new TestSkinProvider(makeSkin(skin)) { Child = playfield }
                };
            });
            AddUntilStep("drawable loaded", () => playfield.HitObjectContainer.AliveObjects.Any());
            AddWaitStep("skin loaded", 3);
        }

        private class TestHidden : OsuModHidden
        {
            public void SetFirstVisibility(bool value) => IncreaseFirstObjectVisibility.Value = value;

            public bool Enabled = true;

            protected override void ApplyNormalVisibilityState(DrawableHitObject d, ArmedState state)
            {
                if (Enabled)
                    base.ApplyNormalVisibilityState(d, state);
            }

            protected override void ApplyIncreasedVisibilityState(DrawableHitObject d, ArmedState state)
            {
                if (Enabled)
                    base.ApplyIncreasedVisibilityState(d, state);
            }
        }

        private partial class FeedbackPlayfield : Playfield
        {
            private readonly TestHidden hidden;
            public bool UseHidden
            {
                set => hidden.Enabled = value;
            }

            public FeedbackPlayfield(TestHidden hidden)
            {
                this.hidden = hidden;
                RegisterPool<HitCircle, DrawableHitCircle>(1);
                RegisterPool<Slider, DrawableSlider>(1);
                RegisterPool<SliderHeadCircle, DrawableSliderHead>(1);
                RegisterPool<SliderTailCircle, DrawableSliderTail>(1);
                RegisterPool<SliderTick, DrawableSliderTick>(8);
                RegisterPool<Spinner, DrawableSpinner>(1);
                RegisterPool<SpinnerTick, DrawableSpinnerTick>(8);
                RegisterPool<SpinnerBonusTick, DrawableSpinnerBonusTick>(8);
            }

            protected override void OnNewDrawableHitObject(DrawableHitObject d)
            {
                base.OnNewDrawableHitObject(d);
                ((DrawableOsuHitObject)d).CheckHittable = (_, _, _) => ClickAction.Hit;
                if (d is DrawableHitCircle and not DrawableSliderHead or DrawableSlider or DrawableSpinner)
                    hidden.ApplyToDrawableHitObject(d);
            }
        }

        private partial class TestSkinProvider : SkinProvidingContainer
        {
            public TestSkinProvider(ISkin skin) : base(skin) { }
            public void ChangeSkin(ISkin skin) => SetSources(new[] { skin });
        }

        private class TestSkin : SkinTransformer
        {
            private readonly int variant;
            public TestSkin(ISkin skin, int variant) : base(skin) => this.variant = variant;

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
            {
                if (lookup is OsuSkinComponentLookup osu)
                {
                    if (osu.Component is OsuSkinComponents.HitCircle or OsuSkinComponents.SliderHeadHitCircle)
                        return variant < 2 ? new TestLegacyCircle() : variant == 2 ? new MainCirclePiece() : new ArgonMainCirclePiece(true);
                    if (osu.Component == OsuSkinComponents.SpinnerBody)
                        return variant == 0 ? new LegacyOldStyleSpinner() : variant == 1 ? new LegacyNewStyleSpinner() : variant == 2 ? new DefaultSpinner() : new ArgonSpinner();
                }
                return base.GetDrawableComponent(lookup);
            }

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            {
                if (lookup is SkinConfiguration.LegacySetting.Version)
                    return (IBindable<TValue>)(object)new Bindable<decimal>(variant == 0 ? 1 : 2);
                return base.GetConfig<TLookup, TValue>(lookup);
            }
        }

        private partial class TestLegacyCircle : LegacyMainCirclePiece
        {
            public Drawable BaseCircle => CircleSprite;
        }
    }
}
