// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace osu.Game.Overlays.Settings.Rendering
{
    /// <summary>
    /// The decoration around a <see cref="SettingsSection"/>'s content (its header, separators and inactive state), created by a <see cref="ISettingsRenderer"/>.
    /// </summary>
    public interface ISettingsSectionFrame : IDrawable
    {
        /// <summary>
        /// Whether only the current section accepts input (the first click on another section scrolls to it instead).
        /// </summary>
        bool RequiresSelectionForInput { get; }

        /// <summary>
        /// Updates the frame for the section being the panel's current section and/or hovered.
        /// </summary>
        void UpdateState(bool isCurrent, bool isHovered);
    }
}
