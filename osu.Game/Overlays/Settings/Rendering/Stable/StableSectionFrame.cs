// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's options category (<c>OptionCategory</c>): a right-aligned 24pt upper-cased header in the secondary colour,
    /// faded unless it is the current category. Stable has no separators or dimming, and every category accepts input.
    /// </summary>
    public partial class StableSectionFrame : CompositeDrawable, ISettingsSectionFrame
    {
        private static readonly Color4 highlighted_colour = lighten(StableSettingsColours.SECONDARY, 0.4f);

        private readonly OsuSpriteText header;

        public bool RequiresSelectionForInput => false;

        public StableSectionFrame(LocalisableString header, Drawable content)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            content.Margin = new MarginPadding { Top = M.CATEGORY_HEIGHT };

            InternalChildren = new[]
            {
                this.header = new OsuSpriteText
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Right = StableSettingsItem.CONTENT_RIGHT },
                    Text = header.ToUpper(),
                    Font = M.Font(M.CATEGORY_FONT_SIZE),
                    Colour = StableSettingsColours.SECONDARY,
                    Alpha = M.CATEGORY_INACTIVE_ALPHA,
                    // the category only reserves CATEGORY_HEIGHT; its text overlaps the following section's padding, as in stable.
                    BypassAutoSizeAxes = Axes.Y,
                },
                content,
            };
        }

        public void UpdateState(bool isCurrent, bool isHovered)
        {
            header.FadeTo(isCurrent ? 1 : M.CATEGORY_INACTIVE_ALPHA, 100);
            header.FadeColour(isCurrent ? highlighted_colour : StableSettingsColours.SECONDARY, 100);
        }

        /// <summary>
        /// Stable's <c>ColourHelper.Lighten2</c>: with <c>a = amount / 2</c>, each channel becomes <c>min(1, c * (1 + a / 2) + a)</c>.
        /// </summary>
        private static Color4 lighten(Color4 colour, float amount)
        {
            float a = amount * 0.5f;

            return new Color4(
                Math.Min(1, colour.R * (1 + 0.5f * a) + a),
                Math.Min(1, colour.G * (1 + 0.5f * a) + a),
                Math.Min(1, colour.B * (1 + 0.5f * a) + a),
                colour.A);
        }
    }
}
