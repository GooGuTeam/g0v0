// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Beatmaps;
using osu.Game.Localisation.Osu;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModHidden : ModHidden, IHidesApproachCircles
    {
        [SettingSource(typeof(ModsStrings), nameof(ModsStrings.HiddenOnlyFadeApproachCirclesLabel), nameof(ModsStrings.HiddenOnlyFadeApproachCirclesDescription))]
        public Bindable<bool> OnlyFadeApproachCircles { get; } = new BindableBool();

        public override LocalisableString Description => ModsStrings.HiddenDescription;

        public override Type[] IncompatibleMods => new[] { typeof(IRequiresApproachCircles), typeof(OsuModSpinIn), typeof(OsuModDepth), typeof(OsuModFreezeFrame) };

        public const double FADE_IN_DURATION_MULTIPLIER = 0.4;
        public const double FADE_OUT_DURATION_MULTIPLIER = 0.3;

        protected override bool IsFirstAdjustableObject(HitObject hitObject) => !(hitObject is Spinner || hitObject is SpinnerTick);

        public override void ApplyToBeatmap(IBeatmap beatmap)
        {
            base.ApplyToBeatmap(beatmap);

            foreach (var obj in beatmap.HitObjects.OfType<OsuHitObject>())
                applyFadeInAdjustment(obj);

            static void applyFadeInAdjustment(OsuHitObject osuObject)
            {
                // Sliders retain their default TimeFadeIn to match Stable
                if (osuObject is not Slider)
                    osuObject.TimeFadeIn = osuObject.TimePreempt * FADE_IN_DURATION_MULTIPLIER;

                foreach (var nested in osuObject.NestedHitObjects.OfType<OsuHitObject>())
                    applyFadeInAdjustment(nested);
            }
        }

        protected override void ApplyIncreasedVisibilityState(DrawableHitObject hitObject, ArmedState state)
        {
            applyHiddenState(hitObject, true);
        }

        protected override void ApplyNormalVisibilityState(DrawableHitObject hitObject, ArmedState state)
        {
            applyHiddenState(hitObject, false);
        }

        private void applyHiddenState(DrawableHitObject drawableObject, bool increaseVisibility)
        {
            if (!(drawableObject is DrawableOsuHitObject drawableOsuObject))
                return;

            OsuHitObject hitObject = drawableOsuObject.HitObject;

            (double fadeStartTime, double fadeDuration) = getFadeOutParameters(drawableOsuObject);

            // process approach circle hiding first (to allow for early return below).
            if (!increaseVisibility)
            {
                if (drawableObject is DrawableHitCircle circle)
                {
                    using (circle.BeginAbsoluteSequence(hitObject.StartTime - hitObject.TimePreempt))
                        circle.ApproachCircle.Hide();
                }
                else if (drawableObject is DrawableSpinner spinner)
                {
                    spinner.HideApproachCircle();
                }
            }

            if (OnlyFadeApproachCircles.Value)
                return;

            switch (drawableObject)
            {
                case DrawableSliderTail:
                    using (drawableObject.BeginAbsoluteSequence(fadeStartTime))
                        drawableObject.FadeOut(fadeDuration);

                    break;

                case DrawableSliderRepeat sliderRepeat:
                    using (drawableObject.BeginAbsoluteSequence(fadeStartTime))
                        // only apply to circle piece – reverse arrow is not affected by hidden.
                        sliderRepeat.CirclePiece.FadeOut(fadeDuration);

                    using (drawableObject.BeginAbsoluteSequence(drawableObject.HitStateUpdateTime))
                        sliderRepeat.FadeOut();

                    break;

                case DrawableHitCircle circle:
                    Drawable fadeTarget = circle;

                    if (increaseVisibility)
                    {
                        // only fade the circle piece (not the approach circle) for the increased visibility object.
                        fadeTarget = circle.CirclePiece;
                    }

                    using (drawableObject.BeginAbsoluteSequence(fadeStartTime))
                        fadeTarget.FadeOut(fadeDuration);

                    circle.ApplyHiddenHitFeedback();
                    break;

                case DrawableSlider slider:
                    using (slider.BeginAbsoluteSequence(fadeStartTime))
                        slider.Body.FadeOut(fadeDuration, Easing.Out);

                    break;

                case DrawableSliderTick sliderTick:
                    using (sliderTick.BeginAbsoluteSequence(fadeStartTime))
                        sliderTick.FadeOut(fadeDuration);

                    break;

                case DrawableSpinner spinner:
                    // hide elements we don't care about.
                    // todo: hide background

                    using (spinner.BeginAbsoluteSequence(fadeStartTime))
                        spinner.FadeOut(fadeDuration);

                    break;
            }
        }

        private (double fadeStartTime, double fadeDuration) getFadeOutParameters(DrawableOsuHitObject drawableObject)
        {
            switch (drawableObject)
            {
                case DrawableSliderTick tick:
                    return GetTickFadeOutParameters((SliderTick)tick.HitObject, tick.FadeInDuration);

                case DrawableSliderTail tail:
                    // Use the same fade sequence as the slider head.
                    Debug.Assert(tail.Slider != null);
                    return getParameters(tail.Slider.HeadCircle);

                case DrawableSliderRepeat repeat:
                    // Use the same fade sequence as the slider head.
                    Debug.Assert(repeat.Slider != null);
                    return getParameters(repeat.Slider.HeadCircle);

                default:
                    return getParameters(drawableObject.HitObject);
            }

            static (double fadeStartTime, double fadeDuration) getParameters(OsuHitObject hitObject)
            {
                double fadeOutStartTime = hitObject.StartTime - hitObject.TimePreempt + hitObject.TimeFadeIn;
                double fadeOutDuration = hitObject.TimePreempt * FADE_OUT_DURATION_MULTIPLIER;

                // new duration from completed fade in to end (before fading out)
                double longFadeDuration = hitObject.GetEndTime() - fadeOutStartTime;

                switch (hitObject)
                {
                    case Slider:
                        return (fadeOutStartTime, longFadeDuration);

                    case Spinner:
                        return (fadeOutStartTime + longFadeDuration, fadeOutDuration);

                    default:
                        return (fadeOutStartTime, fadeOutDuration);
                }
            }
        }

        internal static (double StartTime, double Duration) GetTickFadeOutParameters(SliderTick tick, double fadeInDuration)
        {
            // Match the end of the span-specific fade-in, capped to the final second before the tick.
            double duration = Math.Min(tick.TimePreempt - fadeInDuration, 1000);
            return (tick.StartTime - duration, duration);
        }

    }
}
