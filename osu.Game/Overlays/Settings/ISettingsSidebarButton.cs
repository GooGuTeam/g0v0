// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace osu.Game.Overlays.Settings
{
    /// <summary>
    /// A sidebar button navigating to a <see cref="SettingsSection"/>, as created by a <see cref="Rendering.ISettingsRenderer"/>.
    /// </summary>
    public interface ISettingsSidebarButton : IDrawable
    {
        SettingsSection Section { get; }

        /// <summary>
        /// Whether <see cref="Section"/> is the currently visible section.
        /// </summary>
        bool Selected { get; set; }
    }
}
