// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Objects.Legacy
{
    /// <summary>
    /// A legacy "Hold" hit object which retains the end time written in the beatmap file.
    /// </summary>
    public interface IHasLegacyHoldEndTime
    {
        /// <summary>
        /// The end time exactly as specified by the beatmap. Unlike the hold's duration this is not
        /// clamped to the start time: osu!stable keeps end times which precede the start of the object.
        /// </summary>
        double LegacyEndTime { get; }
    }
}
