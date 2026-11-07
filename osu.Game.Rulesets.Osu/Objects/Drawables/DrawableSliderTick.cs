// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK;
using osuTK.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Skinning;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Osu.Objects.Drawables
{
    public partial class DrawableSliderTick : DrawableOsuHitObject
    {
        public const double ANIM_DURATION = 150;

        public const float DEFAULT_TICK_SIZE = 16;

        protected DrawableSlider DrawableSlider => (DrawableSlider)ParentHitObject;

        private readonly Bindable<bool> legacyTickFading = new Bindable<bool>();

        private readonly Bindable<bool> legacyTickVisibility = new Bindable<bool>();

        [Resolved(CanBeNull = true)]
        private GameplayState gameplayState { get; set; }

        internal double FadeInDuration => GetFadeInDuration((SliderTick)HitObject, legacyTickFading.Value);

        private SkinnableDrawable scaleContainer;
        private readonly Container visualProxies = new Container { RelativeSizeAxes = Axes.Both };

        [Resolved(CanBeNull = true)]
        private OsuVisualOrdering visualOrdering { get; set; }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            legacyTickFading.BindValueChanged(_ => refreshVisualSettings());
            legacyTickVisibility.BindValueChanged(_ => refreshVisualSettings());

            if (visualOrdering?.IsActive == true)
            {
                AddInternal(visualProxies);
                visualOrdering.Register(scaleContainer, this, OsuVisualKind.Tick, () => visualProxies);
            }
        }

        public DrawableSliderTick()
            : base(null)
        {
        }

        public DrawableSliderTick(SliderTick sliderTick)
            : base(sliderTick)
        {
        }

        [BackgroundDependencyLoader]
        private void load(OsuRulesetConfigManager config)
        {
            config.BindWith(OsuRulesetSetting.LegacySliderTickFading, legacyTickFading);
            config.BindWith(OsuRulesetSetting.LegacySliderTickVisibility, legacyTickVisibility);
            Size = OsuHitObject.OBJECT_DIMENSIONS;
            Origin = Anchor.Centre;

            AddInternal(scaleContainer = new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.SliderScorePoint), _ => new CircularContainer
            {
                Masking = true,
                Origin = Anchor.Centre,
                Size = new Vector2(DEFAULT_TICK_SIZE),
                BorderThickness = DEFAULT_TICK_SIZE / 4,
                BorderColour = Color4.White,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = AccentColour.Value,
                    Alpha = 0.3f,
                }
            })
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            });

            ScaleBindable.BindValueChanged(scale => scaleContainer.Scale = new Vector2(scale.NewValue));
        }

        protected override void OnApply()
        {
            base.OnApply();

            Position = HitObject.Position - DrawableSlider.HitObject.Position;
        }

        private void refreshVisualSettings() => Schedule(() =>
        {
            // A config change may also reach a free pooled drawable. Its next Apply() will use the current settings.
            if (Entry != null)
                RefreshStateTransforms();
        });

        protected override void CheckForResult(bool userTriggered, double timeOffset) => DrawableSlider.SliderInputManager.TryJudgeNestedObject(this, timeOffset);

        // Stable's first-span ticks fade in over 150ms; subsequent spans use 200ms.
        internal static double GetFadeInDuration(SliderTick tick, bool legacyFading) => legacyFading && tick.SpanIndex > 0 ? 200 : ANIM_DURATION;

        protected override void UpdateInitialTransforms()
        {
            // Stable's Position2 is the geometric path endpoint, not the final position after reversals.
            // Radius already includes the historical 1.00041 scale allowance. Equality remains visible.
            var slider = DrawableSlider.HitObject;
            double radiusSquared = slider.Radius * slider.Radius;
            bool insideEndpoint = Vector2.DistanceSquared(HitObject.Position, slider.Position) < radiusSquared
                                  || Vector2.DistanceSquared(HitObject.Position, slider.Position + slider.Path.PositionAt(1)) < radiusSquared;

            // Hide only the visual source (also used by 2B proxies), never the judging drawable or its lifetime.
            // Outside gameplay, notably in the editor, stable keeps these ticks visible.
            scaleContainer.Alpha = gameplayState != null && legacyTickVisibility.Value && insideEndpoint ? 0 : 1;

            this.FadeOut().FadeIn(FadeInDuration);
            this.ScaleTo(0.5f).ScaleTo(1f, ANIM_DURATION * 4, Easing.OutElasticHalf);
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            base.UpdateHitStateTransforms(state);

            switch (state)
            {
                case ArmedState.Idle:
                    this.Delay(HitObject.TimePreempt).FadeOut();
                    break;

                case ArmedState.Miss:
                    this.FadeOut(ANIM_DURATION, Easing.OutQuint);
                    break;

                case ArmedState.Hit:
                    this.FadeOut(ANIM_DURATION, Easing.OutQuint);
                    this.ScaleTo(Scale * 1.5f, ANIM_DURATION, Easing.Out);
                    break;
            }
        }
    }
}
