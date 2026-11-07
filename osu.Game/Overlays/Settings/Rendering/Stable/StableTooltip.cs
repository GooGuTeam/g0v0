// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osuTK;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's tooltip (<c>pTooltip</c>): small white text on a (10, 10, 10, 230) box with 4 unit corners and a (80, 80, 80) border,
    /// wrapped at 400 units, following the cursor exactly and fading in and out over 200ms.
    /// </summary>
    public partial class StableTooltip : VisibilityContainer, ITooltip<LocalisableString>
    {
        private readonly OsuTextFlowContainer text;

        public StableTooltip()
        {
            AutoSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = M.TOOLTIP_CORNER_RADIUS;
            BorderThickness = M.TOOLTIP_BORDER;
            BorderColour = M.TOOLTIP_BORDER_COLOUR;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = M.TOOLTIP_BACKGROUND,
                },
                text = new OsuTextFlowContainer(s => s.Font = M.Font(M.TOOLTIP_FONT_SIZE))
                {
                    AutoSizeAxes = Axes.Both,
                    MaximumSize = new Vector2(M.TOOLTIP_MAX_WIDTH, float.PositiveInfinity),
                    Margin = new MarginPadding { Horizontal = M.TOOLTIP_PADDING_H, Vertical = M.TOOLTIP_PADDING_V },
                },
            };
        }

        public void SetContent(LocalisableString content) => text.Text = content;

        // stable repositions the tooltip at the cursor every frame, without easing.
        public void Move(Vector2 pos) => Position = pos;

        protected override void PopIn() => this.FadeIn(M.TOOLTIP_FADE_DURATION);

        protected override void PopOut() => this.FadeOut(M.TOOLTIP_FADE_DURATION);
    }
}
