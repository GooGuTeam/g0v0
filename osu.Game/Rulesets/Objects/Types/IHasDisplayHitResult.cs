// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Objects.Types
{
    /// <summary>
    /// A HitObject that specifies a custom <see cref="HitResult"/> or visibility behavior for hit error display.
    /// </summary>
    public interface IHasDisplayHitResult
    {
        /// <summary>
        /// Whether this hit object should be ignored on hit error meters.
        /// </summary>
        bool IgnoreHitErrorMeter { get; }

        /// <summary>
        /// The <see cref="HitResult"/> to display on hit error meters, or <see cref="HitResult.None"/> to use default judgement type.
        /// </summary>
        HitResult DisplayHitResult { get; }
    }
}
