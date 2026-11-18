// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu.Judgements;

namespace osu.Game.Rulesets.Osu.Objects
{
    public class HitCircle : OsuHitObject, IHasLegacyHoldEndTime
    {
        /// <summary>
        /// The end time of the legacy "Hold" object this circle was converted from, if any.
        /// </summary>
        /// <remarks>
        /// osu!stable creates a <c>HitCircleHold</c> for such an object: it cannot be hit by player input,
        /// and instead judges automatically once the time passes this end time plus the 50 hit window,
        /// with the result of a hit made at that moment. Where the end time precedes the start time the
        /// circle may therefore complete itself.
        /// </remarks>
        public double? LegacyHoldEndTime { get; set; }

        double IHasLegacyHoldEndTime.LegacyEndTime => LegacyHoldEndTime ?? double.NaN;

        internal static int TruncateLegacyTime(double time) => unchecked((int)(long)Math.Clamp(Math.Truncate(time), -4e18, 4e18));

        public override Judgement CreateJudgement() => new OsuJudgement();
    }
}
