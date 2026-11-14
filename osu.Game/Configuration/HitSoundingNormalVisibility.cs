// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    public enum HitSoundingNormalVisibility
    {
        [LocalisableDescription(typeof(HitSoundingStrings), nameof(HitSoundingStrings.ShowAllNormal))]
        ShowAll,

        [LocalisableDescription(typeof(HitSoundingStrings), nameof(HitSoundingStrings.HideNormal0))]
        HideNormal0,

        [LocalisableDescription(typeof(HitSoundingStrings), nameof(HitSoundingStrings.HideNormal1))]
        HideNormal1
    }
}
