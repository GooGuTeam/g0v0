// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Overlays.Settings.Rendering;

namespace osu.Game.Overlays.Settings.Entries
{
    /// <summary>
    /// A boolean toggle.
    /// </summary>
    public sealed class CheckboxEntry : ValueSettingsEntry<bool>
    {
        public override Drawable CreateDrawable(ISettingsRenderer renderer) => renderer.CreateCheckbox(this);
    }
}
