// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics.Sprites;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's options title area (<c>Options.headerText</c> / <c>headerText2</c>): a centred 20pt title at y = 40
    /// and a 14pt subtitle in the main colour at y = 60, both with a shadow.
    /// </summary>
    public partial class StablePanelHeader : CompositeDrawable
    {
        public StablePanelHeader(LocalisableString title, LocalisableString description)
        {
            RelativeSizeAxes = Axes.X;
            Height = M.HEADER_HEIGHT;

            InternalChildren = new Drawable[]
            {
                new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = M.HEADER_TITLE_Y,
                    Text = title,
                    Font = M.Font(M.Text(20)),
                    Shadow = true,
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = M.HEADER_SUBTITLE_Y,
                    Text = description,
                    Font = M.Font(M.Text(14)),
                    Colour = StableSettingsColours.MAIN,
                    Shadow = true,
                },
            };
        }
    }
}
