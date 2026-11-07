// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK.Graphics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// The fixed palette of the osu!stable options menu. Kept in one place so the stable controls stay consistent.
    /// </summary>
    public static class StableSettingsColours
    {
        /// <summary>
        /// The main accent (checkboxes, sliders).
        /// </summary>
        public static readonly Color4 MAIN = new Color4(236, 117, 140, 255);

        /// <summary>
        /// The secondary accent (buttons, category headers).
        /// </summary>
        public static readonly Color4 SECONDARY = new Color4(34, 153, 187, 255);

        /// <summary>
        /// The colour of the indicator shown beside settings which are not at their default value.
        /// </summary>
        public static readonly Color4 DEFAULT_INDICATOR = new Color4(255, 186, 0, 255);

        /// <summary>
        /// The colour of warning notes (stable's options text warnings).
        /// </summary>
        public static readonly Color4 WARNING_TEXT = new Color4(255, 198, 23, 255);

        /// <summary>
        /// The colour of critical notes.
        /// </summary>
        public static readonly Color4 CRITICAL_TEXT = new Color4(255, 90, 90, 255);

        /// <summary>
        /// The tint applied to disabled settings.
        /// </summary>
        public static readonly Color4 DISABLED = Color4.Gray;

        /// <summary>
        /// The faint guide line drawn beside each section.
        /// </summary>
        public static readonly Color4 SECTION_LINE = new Color4(255, 255, 255, 40);
    }
}
