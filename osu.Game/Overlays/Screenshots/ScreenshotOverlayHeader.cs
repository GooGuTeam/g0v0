// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Screenshots
{
    public partial class ScreenshotOverlayHeader : OverlayHeader
    {
        protected override OverlayTitle CreateTitle() => new ScreenshotHeaderTitle();

        private partial class ScreenshotHeaderTitle : OverlayTitle
        {
            public ScreenshotHeaderTitle()
            {
                Title = ScreenshotOverlayStrings.Title;
                Description = ScreenshotOverlayStrings.Description;
                Icon = FontAwesome.Solid.Camera;
            }
        }
    }
}
