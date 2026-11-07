// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    public enum LegacySongSelectAspectRatio
    {
        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.LegacySongSelectFollowWindow))]
        FollowWindow,

        [Description("16:9")]
        Widescreen,

        [Description("16:10")]
        WidescreenTall,
    }
}
