// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Osu.Judgements;

namespace osu.Game.Rulesets.Osu.Objects
{
    public class HitCircle : OsuHitObject
    {
        /// <summary>
        /// The end time of the legacy "Hold" object this circle was converted from, if any.
        /// </summary>
        /// <remarks>
        /// osu!stable plays such an object as a circle, but judges it automatically once the time passes
        /// this end time plus the 50 hit window, with the result of a hit made at that moment. Where the
        /// end time precedes the start time the circle may therefore complete itself.
        /// </remarks>
        public double? LegacyHoldEndTime { get; set; }

        public override Judgement CreateJudgement() => new OsuJudgement();
    }
}
