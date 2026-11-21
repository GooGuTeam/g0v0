// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Overlays.Screenshots;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarScreenshotButton : ToolbarOverlayToggleButton
    {
        protected override Anchor TooltipAnchor => Anchor.TopRight;

        [BackgroundDependencyLoader(true)]
        private void load(ScreenshotOverlay? screenshots)
        {
            if (screenshots != null)
                StateContainer = screenshots;
        }
    }
}
