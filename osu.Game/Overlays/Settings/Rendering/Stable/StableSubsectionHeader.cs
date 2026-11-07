// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's options section header (<c>OptionSection</c>): 12pt bold upper-cased text, 20 units of padding before it,
    /// and the start of the section guide line.
    /// </summary>
    public partial class StableSubsectionHeader : CompositeDrawable
    {
        private readonly Box line;

        public StableSubsectionHeader(LocalisableString header)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Margin = new MarginPadding { Top = M.SECTION_PADDING_BEFORE };

            InternalChildren = new Drawable[]
            {
                line = new Box
                {
                    X = M.SECTION_LINE_X,
                    Width = M.SECTION_LINE_WIDTH,
                    Colour = M.SECTION_LINE_COLOUR,
                    Blending = BlendingParameters.Additive,
                    BypassAutoSizeAxes = Axes.Both,
                },
                new OsuSpriteText
                {
                    Text = header.ToUpper(),
                    Font = M.Font(M.HEADER_FONT_SIZE, FontWeight.Bold),
                    Margin = new MarginPadding { Left = M.HEADER_X, Vertical = StableSettingsItem.ROW_PADDING },
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            // continue the line through the gap down to the first element, without overlapping its own segment
            // (both are additive, so an overlap would draw brighter).
            line.Height = DrawHeight + SettingsSection.ITEM_SPACING_V2;
        }
    }
}
