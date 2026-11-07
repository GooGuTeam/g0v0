// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Select;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Stands in for a stable texture the skin doesn't provide, as osu!stable's own come from osu!ui.dll, which can't be shipped: a plate of
    /// the texture's size in the element's colour, with its label and the name of the texture that would replace it.
    /// </summary>
    public partial class LegacyMissingTexture : CompositeDrawable
    {
        // stable's 640x480 layout drawn at 1.6x, as the legacy screens do.
        private const float scale = 1.6f;

        private readonly Box flash;

        public LegacyMissingTexture(string textureName, LocalisableString text, Color4 colour, Vector2 size)
        {
            Size = size;
            Masking = true;
            CornerRadius = 10 * scale;
            BorderThickness = 2 * scale;
            BorderColour = colour;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientVertical(colour.Opacity(0.55f), colour.Darken(0.6f).Opacity(0.55f)),
                },
                // diagonal stripes, so it reads as a stand-in rather than a finished button.
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.08f,
                    ChildrenEnumerable = Enumerable.Range(0, 16).Select(i => new Box
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        RelativePositionAxes = Axes.X,
                        X = i / 15f,
                        Width = 6 * scale,
                        Height = 200 * scale,
                        Rotation = 35,
                    }),
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = text,
                            Font = LegacyFonts.Get(22 * scale, FontWeight.Bold),
                            Shadow = true,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = $@"{textureName}.png",
                            Font = OsuFont.GetFont(Typeface.MapleMono, size: 9 * scale),
                            Alpha = 0.6f,
                        },
                    },
                },
                flash = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                },
            };
        }

        public void Flash() => flash.FadeOutFromOne(400);
    }
}
