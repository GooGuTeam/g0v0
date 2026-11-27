// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Database
{
    /// <summary>
    /// An installation which user content can be migrated from.
    /// </summary>
    public enum ImportSource
    {
        /// <summary>
        /// A classic (stable) installation.
        /// </summary>
        [LocalisableDescription(typeof(ScreenImportStrings), nameof(ScreenImportStrings.SourceStable))]
        Stable,

        /// <summary>
        /// Another g0v0! or lazer installation.
        /// </summary>
        [LocalisableDescription(typeof(ScreenImportStrings), nameof(ScreenImportStrings.SourceLazer))]
        Lazer,
    }
}
