// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's options tab column: a fixed 40 unit wide #080808 strip with the tab icons centred vertically.
    /// It never has a back button of its own; the root panel shows stable's <see cref="StableBackButton"/> instead.
    /// </summary>
    public partial class StableSettingsSidebar : SettingsSidebar
    {
        public StableSettingsSidebar()
            : base(false, M.SIDEBAR_WIDTH, M.SIDEBAR_WIDTH)
        {
        }

        protected override Drawable CreateBackground(OverlayColourProvider colourProvider) => new Box
        {
            Colour = M.SIDEBAR_BACKGROUND,
            RelativeSizeAxes = Axes.Both,
        };
    }
}
