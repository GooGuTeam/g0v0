// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// A subsection header that switches its subsection on or off (lazer's input device headers). Stable has no such header, so it is made
    /// of the stable elements: <see cref="StableSubsectionHeader"/>'s bold upper-cased text and guide line, with a stable checkbox circle
    /// before the text. Clicking the header toggles it; the text dims while switched off.
    /// </summary>
    public partial class StableToggleableSubsectionHeader : CompositeDrawable
    {
        private static readonly float circle_size = M.CIRCLE_SIZE * M.CHECKBOX_SCALE;

        public readonly BindableBool Current = new BindableBool(true);

        private readonly bool toggleable;

        private readonly Box line;
        private readonly Container circle;
        private readonly Box fill;
        private readonly OsuSpriteText text;

        public StableToggleableSubsectionHeader(LocalisableString header, bool toggleable)
        {
            this.toggleable = toggleable;

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
                new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Margin = new MarginPadding { Left = M.HEADER_X, Vertical = StableSettingsItem.ROW_PADDING },
                    Children = new Drawable[]
                    {
                        circle = new CircularContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.Centre,
                            X = M.CHECKBOX_CIRCLE_CENTRE,
                            Size = new Vector2(circle_size),
                            Masking = true,
                            BorderThickness = 2 * M.CHECKBOX_SCALE,
                            BorderColour = StableSettingsColours.MAIN,
                            Alpha = toggleable ? 1 : 0,
                            Child = fill = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = StableSettingsColours.MAIN,
                                AlwaysPresent = true,
                            },
                        },
                        text = new OsuSpriteText
                        {
                            Text = header.ToUpper(),
                            Font = M.Font(M.HEADER_FONT_SIZE, FontWeight.Bold),
                            X = toggleable ? M.CHECKBOX_LABEL_X : 0,
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(v =>
            {
                fill.Alpha = v.NewValue ? 1 : 0;
                text.FadeColour(v.NewValue || !toggleable ? Color4.White : StableSettingsColours.DISABLED, 200, Easing.OutQuint);

                // the checkbox pop (pCheckbox.UpdateStatus).
                if (toggleable)
                    circle.ScaleTo(v.NewValue ? 1.2f : 0.8f).ScaleTo(1, M.CHECKBOX_POP_DURATION, Easing.Out);
            }, true);

            FinishTransforms(true);
        }

        protected override void Update()
        {
            base.Update();

            // continue the guide line down through the gap to the first element, as StableSubsectionHeader does.
            line.Height = DrawHeight + SettingsSection.ITEM_SPACING_V2;
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (!toggleable || Current.Disabled)
                return false;

            Current.Toggle();
            return true;
        }
    }
}
