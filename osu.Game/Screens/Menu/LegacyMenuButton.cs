// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// Interactive button widget replicating osu!stable's menu button (from Menu.cs).
    /// Supports skins with "menu-button-{kind}" and "-over" glow overlay.
    /// </summary>
    public partial class LegacyMenuButton : ClickableContainer
    {
        public readonly string Kind;
        public readonly Vector2 InitialPosition;

        private Action<UIEvent>? actionWithEvent;

        /// <summary>
        /// An action that receives the triggering event. Like <see cref="ClickableContainer.Action"/>, setting it enables the button.
        /// </summary>
        public Action<UIEvent>? ActionWithEvent
        {
            get => actionWithEvent;
            set
            {
                actionWithEvent = value;
                Enabled.Value = value != null || Action != null;
            }
        }

        private Sprite baseSprite = null!;
        private Sprite overSprite = null!;
        private OsuSpriteText fallbackText = null!;

        private Sample? hoverSample;
        private Sample? clickSample;

        public LegacyMenuButton(string kind, Vector2 initialPosition)
        {
            Kind = kind;
            InitialPosition = initialPosition;

            Anchor = Anchor.Centre;
            Origin = Anchor.TopLeft;
            Position = initialPosition;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, SkinManager skins, AudioManager audio)
        {
            Texture? getTex(string name)
            {
                return skin.GetStableTexture(name) ?? skins.DefaultClassicSkin.GetStableTexture(name);
            }

            var baseTex = getTex($"menu-button-{Kind}");
            var overTex = getTex($"menu-button-{Kind}-over") ?? baseTex;

            hoverSample = audio.Samples.Get($"menu-{Kind}-hover") ?? audio.Samples.Get("menuclick");
            clickSample = audio.Samples.Get($"menu-{Kind}-click") ?? audio.Samples.Get("menuhit");

            if (baseTex == null)
            {
                Size = new Vector2(300, 50);
            }
            else
            {
                AutoSizeAxes = Axes.Both;
            }

            Children = new Drawable[]
            {
                baseSprite = new Sprite
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Texture = baseTex,
                    Scale = new Vector2(1 / LegacySkin.STABLE_MAGIC_SCALE_FACTOR),
                },
                overSprite = new Sprite
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Texture = overTex,
                    Scale = new Vector2(1 / LegacySkin.STABLE_MAGIC_SCALE_FACTOR),
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                },
                fallbackText = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Font = OsuFont.GetFont(size: 24, weight: FontWeight.Bold),
                    Colour = Color4.White,
                    Text = Kind.ToUpperInvariant(),
                    Alpha = baseTex == null ? 1 : 0,
                }
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (!Enabled.Value)
                return base.OnHover(e);

            // Stable Menu.cs line 621: Moves +20px X over 580ms with EasingTypes.ElasticOut (25)
            this.MoveToX(InitialPosition.X + 20, 580, Easing.OutElastic);
            overSprite.FadeIn(30);

            hoverSample?.Play();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);

            // Stable Menu.cs line 626: Moves back to initial position over 400ms with EasingTypes.Out (7)
            this.MoveToX(InitialPosition.X, 400, Easing.OutQuad);
            overSprite.FadeOut(500);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (!Enabled.Value)
                return false;

            clickSample?.Play();
            ActionWithEvent?.Invoke(e);
            Action?.Invoke();
            return true;
        }

        /// <summary>
        /// Stable's <c>Menu.SetIdle(false)</c>: fade in over 350ms while sliding in from 100 units to the left over 400ms.
        /// </summary>
        public void SlideIn()
        {
            this.FadeIn(350);
            this.MoveTo(InitialPosition - new Vector2(100, 0))
                .MoveTo(InitialPosition, 400, Easing.Out);
        }

        /// <summary>
        /// Stable's <c>Menu.SetIdle(true)</c>: fade out over 500ms while drifting 100 units to the left over 2000ms.
        /// </summary>
        public void SlideOut(bool instant = false)
        {
            this.FadeOut(instant ? 0 : 500);
            this.MoveTo(InitialPosition - new Vector2(100, 0), instant ? 0 : 2000, Easing.Out);
        }

        /// <summary>
        /// Stable's <c>Menu.fadeAllButtons</c>, run when leaving the menu: move 50 units to the left and fade out over 100ms.
        /// </summary>
        public void FadeAway()
        {
            ClearTransforms();
            this.MoveToOffset(new Vector2(-50, 0), 100);
            this.FadeOut(100);
        }

        public void TransitionOut(bool movingUp)
        {
            this.FadeOut(80, Easing.OutQuad);
            this.MoveTo(InitialPosition + new Vector2(movingUp ? 15 : -30, 0), 80, Easing.OutQuad);
        }

        public void TransitionIn(bool movingUp)
        {
            Position = InitialPosition + new Vector2(movingUp ? 15 : -30, 0);
            this.FadeIn(80, Easing.OutQuad);
            this.MoveTo(InitialPosition, 200, Easing.OutQuad);
        }
    }
}
