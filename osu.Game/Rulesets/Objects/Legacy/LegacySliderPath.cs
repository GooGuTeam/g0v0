// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK;

namespace osu.Game.Rulesets.Objects.Legacy
{
    /// <summary>
    /// Original absolute control points for stable visual sampling. Subtracting the head
    /// into float relative coordinates can irreversibly round large Aspire coordinates.
    /// </summary>
    public sealed record LegacySliderPath(string PointString, Vector2 Origin, int FormatVersion);
}
