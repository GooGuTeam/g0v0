// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    public enum ResultsScreenStyle
    {
        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.DefaultResultsStyle))]
        Default,

        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.V2ResultsStyle))]
        V2,

        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.LegacyResultsStyle))]
        Legacy,
    }
}
