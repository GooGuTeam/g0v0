// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Skinning;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// The space stable lays its results screen out in: 480 units (768 px) tall, as wide as the window, with the panel pinned to the left
    /// and the grade, mods, buttons and title pinned to the right (<c>Ranking</c>'s widescreen-aware sprite managers).
    /// </summary>
    public static class LegacyRankingLayout
    {
        public const float HEIGHT = 768;

        /// <summary>
        /// The narrowest width stable lays out at (4:3). Narrower windows scale down to fit it.
        /// </summary>
        public const float MIN_WIDTH = 1024;

        // 4:3, 16:10, 16:9 and 21:9 at 768 px tall.
        private static readonly float[] standard_widths = { 1024, 1229, 1366, 1792 };

        /// <summary>
        /// The window width a skin's results screen art was drawn for, or <c>null</c> when it fits any width.
        /// </summary>
        /// <remarks>
        /// Stable pins <c>ranking-panel</c> to the left edge and <c>ranking-title</c> to the right edge (20 units in), so a skin whose
        /// panel or title spans the whole screen only lines up with itself and the grade at the width it was drawn for.
        /// Such skins are laid out at that width instead of the window's, scaled to fit it.
        /// </remarks>
        public static float? GetDesignWidth(ISkin skin)
        {
            float width = 0;

            var panel = skin.GetStableTexture(@"ranking-panel");
            if (panel != null)
                width = Math.Max(width, panel.DisplayWidth);

            var title = skin.GetStableTexture(@"ranking-title");
            if (title != null)
                width = Math.Max(width, title.DisplayWidth + 20 * 1.6f);

            // art no wider than a 4:3 screen fits every window, as does art wider than any screen it could have been drawn for.
            if (width <= MIN_WIDTH * 1.04f || width > standard_widths[^1] * 1.04f)
                return null;

            float nearest = standard_widths.MinBy(w => Math.Abs(w - width));
            return Math.Abs(nearest - width) / nearest < 0.04f ? nearest : width;
        }
    }
}
