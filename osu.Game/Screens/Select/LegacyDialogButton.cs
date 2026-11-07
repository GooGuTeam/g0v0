// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// A button of stable's song select dialogs (<c>pDialog.AddOption</c>, <c>pButton</c>): mod selection, collections and the beatmap options.
    /// Stable builds it from the skin's <c>button-left</c>, <c>button-middle</c> and <c>button-right</c>, 460 x 40 units, tinted a little darker than
    /// its colour and lit to the full colour while hovered. When the skin has no such textures, a drawn bar stands in for stable's own.
    /// </summary>
    public partial class LegacyDialogButton : CompositeDrawable
    {
        public Action? Action { get; set; }

        internal float LabelSize { get; init; } = 28;

        private Vector2 skinnedSize = new Vector2(460, 40);

        /// <summary>
        /// The skinned button's size: by default <c>pDialog.AddOption</c>'s, in the 640 x 480 units the dialogs are laid out in. Setting it
        /// also sizes the drawn bar that stands in for skins without button textures.
        /// </summary>
        public Vector2 ButtonSize
        {
            get => skinnedSize;
            init
            {
                skinnedSize = value;
                Size = value;
            }
        }

        private readonly LocalisableString text;
        private readonly Color4 textColour;

        private Container? skinnedButton;
        private Container? drawnButton;
        private Box? drawnFlash;

        private Sample? hoverSample;
        private Sample? clickSample;

        public LegacyDialogButton(LocalisableString text, Color4 textColour, Action? action = null)
        {
            this.text = text;
            this.textColour = textColour;
            Action = action;

            Size = new Vector2(452, 35);
            Origin = Anchor.Centre;
        }

        // pButton.updateColour: the button rests 20/255 darker than its colour, fully opaque.
        private Color4 restingColour => new Color4(Math.Max(0, textColour.R - 20 / 255f), Math.Max(0, textColour.G - 20 / 255f), Math.Max(0, textColour.B - 20 / 255f), 1);

        private Color4 litColour => new Color4(textColour.R, textColour.G, textColour.B, 1);

        private Color4 drawnLitColour => new Color4(textColour.R, textColour.G, textColour.B, 1).Lighten(0.2f);

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            hoverSample = audio.Samples.Get("click-short") ?? audio.Samples.Get("menuclick");
            clickSample = audio.Samples.Get("click-short-confirm") ?? audio.Samples.Get("menuhit");

            rebuild();
            skin.SourceChanged += onSkinChanged;
        }

        private void onSkinChanged() => Schedule(rebuild);

        protected override void Dispose(bool isDisposing)
        {
            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;

            base.Dispose(isDisposing);
        }

        private void rebuild()
        {
            skinnedButton = null;
            drawnButton = null;
            drawnFlash = null;

            var label = new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = text,
                Font = OsuFont.Inter.With(size: LabelSize),
                Colour = Color4.White,
                Shadow = true,
            };

            // clamped to their edge pixels: the middle is a few pixels stretched across the whole button, and sampling past its edge would
            // fade both ends of the button to transparent.
            var left = skin.GetStableTexture(@"button-left");
            var middle = skin.GetStableTexture(@"button-middle");
            var right = skin.GetStableTexture(@"button-right");

            if (left != null && middle != null && right != null)
            {
                // the end caps are scaled to the button's height; the middle stretches between them.
                float leftWidth = left.DisplayWidth * skinnedSize.Y / left.DisplayHeight;
                float rightWidth = right.DisplayWidth * skinnedSize.Y / right.DisplayHeight;

                InternalChildren = new Drawable[]
                {
                    skinnedButton = new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = skinnedSize,
                        Colour = restingColour,
                        // absolute sizes: a sprite given a texture before relative sizing keeps the texture's size as its relative size.
                        Children = new Drawable[]
                        {
                            new Sprite
                            {
                                Texture = left,
                                Size = new Vector2(leftWidth, skinnedSize.Y),
                            },
                            new Sprite
                            {
                                Texture = middle,
                                X = leftWidth,
                                Size = new Vector2(skinnedSize.X - leftWidth - rightWidth, skinnedSize.Y),
                            },
                            new Sprite
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Texture = right,
                                Size = new Vector2(rightWidth, skinnedSize.Y),
                            },
                        },
                    },
                    label,
                };

                return;
            }

            // the drawn bar mirrors the skinned one: it rests a little darker than its colour, lights up while hovered and flashes white on click.
            InternalChildren = new Drawable[]
            {
                drawnButton = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    CornerRadius = 2,
                    Masking = true,
                    Colour = restingColour,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.5f,
                    Colour = ColourInfo.GradientVertical(new Color4(0f, 0f, 0f, 0.22f), Color4.Transparent),
                },
                drawnFlash = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                },
                label,
            };
        }

        // the skinned button takes its whole 460 x 40 for input, as stable's sprites do.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => skinnedButton?.ReceivePositionalInputAt(screenSpacePos) ?? base.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnHover(HoverEvent e)
        {
            skinnedButton?.FadeColour(litColour, 50);
            // a flat bar has no texture shading to carry stable's subtle tint change, so it also brightens a little.
            drawnButton?.FadeColour(drawnLitColour, 50);

            hoverSample?.Play();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            skinnedButton?.FadeColour(restingColour, 50);
            drawnButton?.FadeColour(restingColour, 50);

            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            // stable flashes the button white on click (pButton.p_OnClick).
            skinnedButton?.FlashColour(Color4.White, 400);
            drawnFlash?.FadeTo(0.6f).FadeOut(400);

            clickSample?.Play();
            Action?.Invoke();
            return true;
        }
    }
}
