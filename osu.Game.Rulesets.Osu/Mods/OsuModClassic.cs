// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Localisation.Osu;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModClassic : ModClassic, IApplicableToHitObject, IApplicableToDrawableHitObject, IApplicableToDrawableRuleset<OsuHitObject>, IApplicableHealthProcessor
    {
        public override Type[] IncompatibleMods => base.IncompatibleMods.Append(typeof(OsuModStrictTracking)).ToArray();

        [SettingSource(typeof(ModsStrings), nameof(ModsStrings.ClassicNoSliderHeadAccuracyLabel), nameof(ModsStrings.ClassicNoSliderHeadAccuracyDescription))]
        public Bindable<bool> NoSliderHeadAccuracy { get; } = new BindableBool(true);

        [SettingSource(typeof(ModsStrings), nameof(ModsStrings.ClassicApplyNoteLockLabel), nameof(ModsStrings.ClassicApplyNoteLockDescription))]
        public Bindable<bool> ClassicNoteLock { get; } = new BindableBool(true);

        [SettingSource(typeof(ModsStrings), nameof(ModsStrings.ClassicAlwaysPlayTailSampleLabel), nameof(ModsStrings.ClassicAlwaysPlayTailSampleDescription))]
        public Bindable<bool> AlwaysPlayTailSample { get; } = new BindableBool(true);

        [SettingSource(typeof(ModsStrings), nameof(ModsStrings.ClassicFadeOutHitCirclesEarlierLabel), nameof(ModsStrings.ClassicFadeOutHitCirclesEarlierDescription))]
        public Bindable<bool> FadeHitCircleEarly { get; } = new Bindable<bool>(true);

        [SettingSource(typeof(ModsStrings), nameof(ModsStrings.ClassicHealthLabel), nameof(ModsStrings.ClassicHealthDescription))]
        public Bindable<bool> ClassicHealth { get; } = new Bindable<bool>(true);

        [SettingSource(typeof(ModsStrings), nameof(ModsStrings.ClassicSliderJudgementLabel), nameof(ModsStrings.ClassicSliderJudgementDescription))]
        public Bindable<bool> ClassicSliderJudgement { get; } = new BindableBool(false);

        private bool usingHiddenFading;
        private LegacyHitPolicy? legacyHitPolicy;
        private OsuPlayfield? playfield;
        private bool useLegacyNoteLock;

        public void ApplyToHitObject(HitObject hitObject)
        {
            if (hitObject is OsuHitObject osuObject)
                OsuCompatibilityRules.ApplyClassic(osuObject, ClassicSliderJudgement.Value, !NoSliderHeadAccuracy.Value);
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            var osuRuleset = (DrawableOsuRuleset)drawableRuleset;

            playfield = osuRuleset.Playfield;
            useLegacyNoteLock = ClassicNoteLock.Value || OsuCompatibilityRules.IsStableProfile(drawableRuleset.Mods);
            legacyHitPolicy = null;
            if (useLegacyNoteLock)
            {
                double hittableRange = OsuHitWindows.MISS_WINDOW - (drawableRuleset.Mods.OfType<OsuModAutopilot>().Any() ? 200 : 0);
                osuRuleset.Playfield.HitPolicy = legacyHitPolicy = new LegacyHitPolicy(hittableRange);
            }

            usingHiddenFading = drawableRuleset.Mods.OfType<OsuModHidden>().SingleOrDefault()?.OnlyFadeApproachCircles.Value == false;
        }

        public void ApplyToDrawableHitObject(DrawableHitObject obj)
        {
            switch (obj)
            {
                case DrawableSpinner spinner:
                    spinner.CanTrack = () => !spinner.HitObject.ExclusiveLegacyInput
                                             || ReferenceEquals(playfield?.HitObjectContainer.AliveObjects.OfType<DrawableSpinner>()
                                                 .FirstOrDefault(candidate => candidate.RotationTracker.IsSpinnableTime), spinner);
                    break;

                case DrawableSliderHead head:
                    if (FadeHitCircleEarly.Value && !usingHiddenFading)
                        applyEarlyFading(head);

                    if (useLegacyNoteLock)
                        blockInputToObjectsUnderSliderHead(head);

                    break;

                case DrawableSliderTail tail:
                    tail.SamplePlaysOnlyOnHit = !AlwaysPlayTailSample.Value;
                    break;

                case DrawableHitCircle circle:
                    if (FadeHitCircleEarly.Value && !usingHiddenFading)
                        applyEarlyFading(circle);

                    break;
            }
        }

        /// <summary>
        /// Keeps ordinary head blocking while allowing a hittable overlapping successor circle or slider head to receive the press.
        /// </summary>
        private void blockInputToObjectsUnderSliderHead(DrawableSliderHead slider)
        {
            slider.HitArea.CanBeHit = () => !slider.DrawableSlider.AllJudged
                                           && legacyHitPolicy?.ShouldYieldSliderHeadInput(slider) != true;
        }

        private void applyEarlyFading(DrawableHitCircle circle)
        {
            circle.ApplyCustomUpdateState += (dho, state) =>
            {
                using (dho.BeginAbsoluteSequence(dho.StateUpdateTime))
                {
                    if (state != ArmedState.Hit)
                    {
                        double okWindow = dho.HitObject.HitWindows.WindowFor(HitResult.Ok);
                        double lateMissFadeTime = dho.HitObject.HitWindows.WindowFor(HitResult.Meh) - okWindow;
                        dho.Delay(okWindow).FadeOut(lateMissFadeTime);
                    }
                }
            };
        }

        public HealthProcessor? CreateHealthProcessor(double drainStartTime) => ClassicHealth.Value ? new OsuLegacyHealthProcessor(drainStartTime) : null;
    }
}
