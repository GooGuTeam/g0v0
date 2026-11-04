// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// Shared layout metrics of the hitsounding timeline.
    /// </summary>
    /// <remarks>
    /// These are constants rather than user-adjustable values so that the lane headers, backgrounds,
    /// hit object markers and drop targets always agree on where each bank and set lives.
    /// </remarks>
    public static class HitSoundingLayout
    {
        /// <summary>
        /// The height of a single sample point badge, derived from the previous 22px badge scaled by sqrt(1.5).
        /// </summary>
        public const float SAMPLE_HEIGHT = 27;

        /// <summary>
        /// The corner radius of a sample point badge.
        /// </summary>
        public const float SAMPLE_CORNER_RADIUS = 5;

        /// <summary>
        /// The font size of a sample point badge label.
        /// </summary>
        public const float SAMPLE_FONT_SIZE = 12;

        /// <summary>
        /// The horizontal padding inside a sample point badge.
        /// </summary>
        public const float SAMPLE_HORIZONTAL_PADDING = 6;

        /// <summary>
        /// The height of one bank lane. Must leave enough room for a sample point badge without clipping.
        /// </summary>
        public const float BANK_HEIGHT = 38;

        /// <summary>
        /// The vertical gap between adjacent bank lanes.
        /// </summary>
        public const float BANK_SPACING = 4;

        /// <summary>
        /// The height of one set lane, containing three bank lanes.
        /// </summary>
        public const float SET_HEIGHT = 3 * BANK_HEIGHT + 2 * BANK_SPACING;

        /// <summary>
        /// The height of a collapsed set lane: one 14px text line with 4px padding above and below.
        /// </summary>
        public const float COLLAPSED_SET_HEIGHT = 22;

        /// <summary>
        /// The diameter of the marker drawn for each sample point in a collapsed set lane.
        /// </summary>
        public const float COLLAPSED_SAMPLE_SIZE = 10;

        /// <summary>
        /// The vertical gap between adjacent set lanes.
        /// </summary>
        public const float SET_SPACING = 5;

        /// <summary>
        /// The vertical distance between the top of adjacent bank lanes.
        /// </summary>
        public const float BANK_PITCH = BANK_HEIGHT + BANK_SPACING;

    }
}
