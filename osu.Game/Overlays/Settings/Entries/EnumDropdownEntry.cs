// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Game.Overlays.Settings.Rendering;

namespace osu.Game.Overlays.Settings.Entries
{
    /// <summary>
    /// A <see cref="DropdownEntry{T}"/> listing every value of an enum.
    /// </summary>
    public sealed class EnumDropdownEntry<T> : DropdownEntry<T>
        where T : struct, Enum
    {
        public EnumDropdownEntry()
        {
            ItemSource.AddRange(Enum.GetValues<T>());
        }

        public override Drawable CreateDrawable(ISettingsRenderer renderer) => renderer.CreateEnumDropdown(this);
    }
}
