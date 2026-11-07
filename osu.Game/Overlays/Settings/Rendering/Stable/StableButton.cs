// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings.Entries;
using osuTK.Graphics;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's options button (<c>OptionButton</c> / <c>pButton</c>): 244 x 20 units with centred 12pt text.
    /// </summary>
    /// <remarks>
    /// Stable draws the tinted <c>button-left/middle/right</c> textures. Those are not redistributable, so their shape is reproduced:
    /// the body is inset ~2 units vertically and ~3.5 horizontally inside a soft dark shadow, with a vertical gradient from 84% to full
    /// brightness at mid height and a darker bottom row.
    /// </remarks>
    public partial class StableButton : StableSettingsItem, IHasPopover
    {
        private readonly ButtonEntry entry;

        public StableButton(ButtonEntry entry)
            : base(entry)
        {
            this.entry = entry;
        }

        protected override Drawable CreateContent()
        {
            var button = new ButtonBody(entry.BackgroundColour ?? (entry.Dangerous ? StableSettingsColours.MAIN : StableSettingsColours.SECONDARY))
            {
                RelativeSizeAxes = Axes.X,
                Height = M.BUTTON_HEIGHT,
                Action = trigger,
                Text = entry.Caption,
            };

            button.Enabled.BindTo(entry.Enabled);

            return button;
        }

        protected override void OnRowClicked() => trigger();

        private void trigger()
        {
            if (entry.Popover != null)
                this.ShowPopover();
            else
                entry.Action?.Invoke();
        }

        public Popover GetPopover() => entry.Popover!();

        /// <summary>
        /// The button itself, without the element frame; also used by other stable controls (e.g. key binding actions).
        /// </summary>
        public partial class ButtonBody : OsuClickableContainer
        {
            private const float vertical_inset = 2;
            private const float horizontal_inset = 3.5f;

            private readonly Color4 litUp;
            private readonly Container body;
            private readonly TruncatingSpriteText text;

            public LocalisableString Text
            {
                set => text.Text = value;
            }

            public ButtonBody(Color4 colour)
            {
                litUp = colour;

                Children = new Drawable[]
                {
                    body = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = horizontal_inset, Vertical = vertical_inset },
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 1,
                            EdgeEffect = new EdgeEffectParameters
                            {
                                Type = EdgeEffectType.Shadow,
                                Colour = Color4.Black.Opacity(0.4f),
                                Radius = 2,
                            },
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Height = 0.5f,
                                            Colour = ColourInfo.GradientVertical(new Color4(215, 215, 215, 255), Color4.White),
                                        },
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Height = 0.5f,
                                        },
                                    },
                                },
                                new Box
                                {
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    RelativeSizeAxes = Axes.X,
                                    Height = 0.5f,
                                    Colour = new Color4(0, 0, 0, 45),
                                },
                            },
                        },
                    },
                    text = new TruncatingSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = M.Font(M.LABEL_FONT_SIZE),
                    },
                };
            }

            private Color4 idleColour => new Color4(
                Math.Max(0, litUp.R - M.BUTTON_IDLE_DARKEN / 255f),
                Math.Max(0, litUp.G - M.BUTTON_IDLE_DARKEN / 255f),
                Math.Max(0, litUp.B - M.BUTTON_IDLE_DARKEN / 255f),
                1);

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Enabled.BindValueChanged(e =>
                {
                    body.FadeColour(e.NewValue ? idleColour : StableSettingsColours.DISABLED);
                    text.FadeColour(e.NewValue ? Color4.White : Color4.LightGray);
                }, true);
            }

            protected override void Update()
            {
                base.Update();

                // keep the label centred like stable, truncating only when it would overflow the button.
                text.MaxWidth = Math.Max(0, DrawWidth - horizontal_inset * 2 - 8);
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (Enabled.Value)
                    body.FadeColour(litUp, M.BUTTON_HOVER_DURATION);

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (Enabled.Value)
                    body.FadeColour(idleColour, M.BUTTON_HOVER_DURATION);

                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (Enabled.Value)
                    body.FlashColour(Color4.White, M.BUTTON_FLASH_DURATION);

                return base.OnClick(e);
            }
        }
    }
}
