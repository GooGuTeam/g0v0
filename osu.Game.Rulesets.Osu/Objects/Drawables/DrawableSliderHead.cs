// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Objects.Drawables
{
    public partial class DrawableSliderHead : DrawableHitCircle
    {
        public new SliderHeadCircle HitObject => (SliderHeadCircle)base.HitObject;

        public DrawableSlider DrawableSlider => (DrawableSlider)ParentHitObject;

        private readonly IBindable<int> pathVersion = new Bindable<int>();
        private readonly Bindable<bool> showSliderHeadJudgements = new BindableBool(false);

        protected override OsuSkinComponents CirclePieceComponent => OsuSkinComponents.SliderHeadHitCircle;

        public DrawableSliderHead()
        {
        }

        public DrawableSliderHead(SliderHeadCircle h)
            : base(h)
        {
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuRulesetConfigManager osuConfig)
        {
            osuConfig?.BindWith(OsuRulesetSetting.ShowSliderHeadJudgements, showSliderHeadJudgements);
        }

        protected override void OnFree()
        {
            base.OnFree();

            pathVersion.UnbindFrom(DrawableSlider.PathVersion);
        }

        protected override void UpdatePosition()
        {
            // Slider head is always drawn at (0,0).
        }

        protected override void OnApply()
        {
            base.OnApply();

            pathVersion.BindTo(DrawableSlider.PathVersion);

            CheckHittable = (d, t, r) => DrawableSlider.CheckHittable?.Invoke(d, t, r) ?? ClickAction.Hit;
        }

        public override bool DisplayResult
        {
            get
            {
                if (HitObject.ScoreV2SliderBehaviour)
                    return showSliderHeadJudgements.Value;

                return base.DisplayResult;
            }
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (!userTriggered && !Judged && !HitObject.HitWindows.CanBeHit(timeOffset))
                HitObject.RawHitResult = HitResult.Miss;

            base.CheckForResult(userTriggered, timeOffset);
            DrawableSlider.SliderInputManager.PostProcessHeadJudgement(this);
        }

        protected override HitResult ResultFor(double timeOffset)
        {
            Debug.Assert(HitObject != null);

            var rawResult = base.ResultFor(timeOffset);
            if (HitObject.ScoreV2SliderBehaviour)
                HitObject.RawHitResult = rawResult;

            if (HitObject.ClassicSliderBehaviour)
            {
                // With classic slider behaviour, heads are considered fully hit if in the largest hit window.
                // We can't award a full Great because the true Great judgement is awarded on the Slider itself,
                // reduced based on number of ticks hit,
                // so we use the most suitable LargeTick judgement here instead.
                return rawResult.IsHit() ? HitResult.LargeTickHit : HitResult.LargeTickMiss;
            }

            return rawResult;
        }

        public override void Shake()
        {
            base.Shake();
            DrawableSlider.Shake();
        }
    }
}
