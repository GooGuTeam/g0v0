// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Objects
{
    public class SliderHeadCircle : HitCircle, IHasDisplayHitResult
    {
        /// <summary>
        /// If <see langword="false"/>, treat this <see cref="SliderHeadCircle"/> as a normal <see cref="HitCircle"/> for judgement purposes.
        /// If <see langword="true"/>, this <see cref="SliderHeadCircle"/> will be judged as a <see cref="SliderTick"/> instead.
        /// </summary>
        public bool ClassicSliderBehaviour;

        /// <summary>
        /// If <see langword="true"/>, this <see cref="SliderHeadCircle"/> will retain its raw hit accuracy under ScoreV2 rules.
        /// </summary>
        public bool ScoreV2SliderBehaviour;

        public HitResult RawHitResult { get; set; } = HitResult.None;

        public bool IgnoreHitErrorMeter => ClassicSliderBehaviour && !ScoreV2SliderBehaviour;

        public HitResult DisplayHitResult => ScoreV2SliderBehaviour && RawHitResult != HitResult.None ? RawHitResult : HitResult.None;

        public override Judgement CreateJudgement() => ClassicSliderBehaviour ? new SliderTickJudgement() : base.CreateJudgement();
    }
}
